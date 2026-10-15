using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// Editor 专用的零打包取得/预览。每次操作从正式 Project 重建 Catalog 内存快照，不缓存 Unity 对象或第二份地址表。
    /// 可绑定已发布 Catalog，定义/身份不一致时明确失效。结果是 AssetDatabase 借用对象，不得 Destroy/Unload。
    /// 预览不初始化 Runtime；CreateConfiguration 可装配真实 Editor Scene 加载，但不模拟 Bundle 生命周期。
    /// </summary>
    [InitializeOnLoad]
    public sealed class ResourceEditorSimulation
    {
        private static readonly int MainThread;
        private readonly Dictionary<string, string> projects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ResourceCatalogDocument> catalogs = new(StringComparer.Ordinal);
        static ResourceEditorSimulation() => MainThread = Thread.CurrentThread.ManagedThreadId;

        public ResourceEditorSimulation(ResourceProject[] projects, ResourceCatalogDocument[] catalogs = null)
        {
            CheckEditor();
            if (projects == null || projects.Length == 0 || projects.Length > 16384) throw new ArgumentException("Explicit resource projects are required.");
            foreach (var project in projects)
            {
                var map = ResourceBuildMap.Create(project);
                if (!this.projects.TryAdd(map.ProjectId, map.ProjectGuid)) throw new ArgumentException("Duplicate simulation project.");
            }
            foreach (var catalog in catalogs ?? Array.Empty<ResourceCatalogDocument>())
            {
                if (catalog == null || !this.projects.ContainsKey(catalog.ProjectId) || !this.catalogs.TryAdd(catalog.ProjectId, catalog) ||
                    catalog.UnityCompatibility != Application.unityVersion || catalog.Platform != EditorUserBuildSettings.activeBuildTarget.ToString())
                    throw new ArgumentException("Simulation catalog identity/compatibility mismatch.");
            }
        }

        public ResourceCatalogEntry Resolve(ResourceAddress address) => Snapshot(address.ProjectId).Catalog.Resolve(address);

        /// <summary>调用方显式给出已编译类型；不根据 Catalog 类型字符串反射，也不回退到 XFAB/Resources。</summary>
        public Object Load(ResourceAddress address, Type type)
        {
            if (type == null || !typeof(Object).IsAssignableFrom(type) || type.ContainsGenericParameters) throw new ArgumentException("A compiled Unity asset type is required.");
            var entry = Resolve(address);
            if (!type.IsAssignableFrom(entry.Location.AssetType)) throw new InvalidOperationException("Simulation asset type mismatch: " + address);
            var path = entry.Location.Locator;
            var candidates = entry.Address.SubAsset == null ? new[] { AssetDatabase.LoadMainAssetAtPath(path) } : AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            var matches = candidates.Where(x => x && type.IsInstanceOfType(x) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out string guid, out long id) &&
                guid + "_" + id.ToString(CultureInfo.InvariantCulture) == entry.AssetId).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Simulation asset identity is missing or ambiguous: " + address);
            return matches[0];
        }

        public T Load<T>(ResourceAddress address) where T : Object => (T)Load(address, typeof(T));

        /// <summary>只枚举 Catalog canonical 成员；Scene 成员是预览描述，不表示已加载场景。</summary>
        public IReadOnlyList<ResourceCatalogEntry> GetGroup(string projectId, string groupId)
        {
            var snapshot = Snapshot(projectId);
            if (!snapshot.Map.Groups.TryGetValue(groupId, out var members)) throw new ArgumentException("Unknown simulation Group: " + groupId);
            return snapshot.Catalog.Entries.Where(x => members.Contains(x.AssetId)).ToList().AsReadOnly();
        }

        public ResourceCatalog GetCatalog(string projectId) => Snapshot(projectId).Catalog;

        /// <summary>显式装配 Editor borrowed Source。地址/类型身份在每次生产时重验，不根据旧 XF 设置自动启用。</summary>
        public ResourceConfiguration CreateConfiguration() => new(projects.Keys.Select(id => Snapshot(id).Catalog),
            new IResourceSource[] { new UnityResourcesSource(), new TextureResourceSource(true), new TextureResourceSource(false), new SimulationSource(this) });

        private sealed class SimulationSource : IResourceSource, IResourceSceneSource
        {
            private readonly ResourceEditorSimulation simulation;
            internal SimulationSource(ResourceEditorSimulation simulation) => this.simulation = simulation;
            public ResourceSourceId Id => ResourceSourceId.Editor;
            public bool SupportsSynchronousLoad => true;
            public void Validate(ResourceCatalogEntry entry)
            {
                var current = simulation.Resolve(entry.Address);
                if (current.AssetId != entry.AssetId || !current.Location.Equals(entry.Location)) throw new InvalidOperationException("Editor Source snapshot is stale: " + entry.Address);
            }
            public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type)
            {
                Validate(entry);
                if (entry.Location.AssetType == typeof(SceneAsset)) throw new ResourceException(ResourceError.Unsupported, "Scene previews are not Runtime asset acquisitions.");
                return new ResourceSourceResult(simulation.Load(entry.Address, type));
            }
            public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type) => new Operation(Load(entry, type).Asset);
            public void Release(Object asset) { }
            public string ResolveScenePath(ResourceCatalogEntry entry)
            {
                Validate(entry);
                if (entry.Location.AssetType != typeof(SceneAsset)) throw new ResourceException(ResourceError.TypeMismatch, "The Editor address is not a Scene.");
                var path = AssetDatabase.GUIDToAssetPath(entry.AssetId.Substring(0, 32));
                if (path != entry.Location.Locator) throw new ResourceException(ResourceError.AddressNotFound, "Editor Scene GUID/path changed.");
                return path;
            }
            public AsyncOperation LoadSceneAsync(string path, LoadSceneParameters parameters) => EditorSceneManager.LoadSceneAsyncInPlayMode(path, parameters);
            private sealed class Operation : ResourceSourceOperation { internal Operation(Object asset) => Complete(asset, null); }
        }

        private (ResourceBuildMap Map, ResourceCatalog Catalog) Snapshot(string projectId)
        {
            CheckEditor();
            if (projectId == null || !projects.TryGetValue(projectId, out var guid)) throw new ArgumentException("Unknown simulation ProjectId.");
            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
            if (!project || project.ProjectId != projectId) throw new InvalidOperationException("Simulation project removed or identity changed; select it again.");
            var map = ResourceBuildMap.Create(project);
            var entries = new List<ResourceCatalogEntry>();
            void Add(ResourceBuildAsset main, ResourceBuiltSubAsset sub = null)
            {
                var key = sub?.LocalId.ToString(CultureInfo.InvariantCulture);
                entries.Add(new ResourceCatalogEntry(main.Guid + "_" + (sub?.LocalId ?? main.LocalId).ToString(CultureInfo.InvariantCulture),
                    new ResourceAddress(projectId, main.Address.Path, key), new ResourceLocation(new ResourceSourceId("editor"), main.SourcePath, sub?.AssetType ?? main.AssetType),
                    main.Aliases.Select(x => new ResourceAddress(projectId, x.Path, key))));
            }
            foreach (var main in map.Assets) Add(main);
            foreach (var sub in ResourceBundleBuilder.DescribeSubAssets(map)) Add(sub.MainAsset, sub);
            var snapshot = new ResourceCatalog(projectId, entries) { Groups = map.Groups.Select(x => new ResourceCatalogGroup(x.Key,
                x.Value.Select(id => new ResourceCatalogGroupMember(id, true)))).ToList().AsReadOnly() };
            if (catalogs.TryGetValue(projectId, out var published))
            {
                // immutable Catalog 不随源重命名/定义切换偷偷漂移；仅允许内容重导入而身份描述不变。
                if (published.Assets.Count != entries.Count || published.Groups.Count != map.Groups.Count)
                    throw new InvalidOperationException("Published Catalog is stale; rebuild the resource project.");
                foreach (var asset in published.Assets)
                {
                    var current = entries.SingleOrDefault(x => x.AssetId == asset.AssetId);
                    var main = map.Assets.SingleOrDefault(x => x.Guid == asset.Guid);
                    if (current == null || main == null || current.Address.Path != asset.Address || current.Address.SubAsset != asset.SubAssetKey ||
                        current.Location.AssetType.FullName != (asset.Kind == "Scene" ? typeof(SceneAsset).FullName : asset.AssetType) ||
                        (main.IsScene ? null : current.Location.AssetType.Assembly.GetName().Name) != asset.AssetAssembly ||
                        (main.IsScene ? main.SourcePath : main.SourcePath.ToLowerInvariant()) != asset.LoadName ||
                        !current.Aliases.Select(x => x.Path).SequenceEqual(asset.Aliases) ||
                        asset.SubAssetKey != null && !AssetDatabase.LoadAllAssetRepresentationsAtPath(main.SourcePath).Any(x => x && x.name == asset.SubAssetName &&
                            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out string _, out long id) && id == asset.LocalId))
                        throw new InvalidOperationException("Published Catalog descriptor is stale: " + asset.AssetId);
                }
                foreach (var group in published.Groups)
                    if (!map.Groups.TryGetValue(group.Id, out var members) || !members.SequenceEqual(group.Members.Select(x => x.AssetId)) || group.Members.Any(x => !x.Required))
                        throw new InvalidOperationException("Published Catalog Group is stale: " + group.Id);
            }
            return (map, snapshot);
        }

        private static void CheckEditor()
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Simulation requires the settled Editor main thread.");
        }
    }
}
