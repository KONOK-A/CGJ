using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>本次解析的有限来源副本；不持有可编辑Project/规则，也不进入Catalog或持久身份。</summary>
    public sealed class ResourceBuildSource
    {
        public string ProjectGuid { get; }
        public string ProjectId { get; }
        public string GroupId { get; }
        public ResourcePacking Packing { get; }
        public string SelectionGuid { get; }
        public string SelectionPath { get; }
        public string SelectionAddress { get; }
        public IReadOnlyList<string> Aliases { get; }
        public IReadOnlyList<string> Extensions { get; }
        public string AtlasConfigurationGuid { get; }
        public string AtlasRuleId { get; }
        internal string Definition { get; }
        internal string DirectoryIdentity { get; }

        internal ResourceBuildSource(ResourceProject project, string projectGuid, ResourceGroup group,
            ResourceSelection selection, string path, ResourceAtlasChange generated)
        {
            ProjectGuid = projectGuid; ProjectId = project.ProjectId; GroupId = group.Id; Packing = group.Packing;
            SelectionGuid = selection.Guid; SelectionPath = path; SelectionAddress = selection.Address ?? "";
            Aliases = Array.AsReadOnly((string[])(selection.Aliases ?? Array.Empty<string>()).Clone());
            Extensions = Array.AsReadOnly((string[])(selection.Extensions ?? Array.Empty<string>()).Clone());
            AtlasConfigurationGuid = generated == null ? null : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project.AutomaticAtlases));
            AtlasRuleId = generated?.Rule.Id;
            Definition = generated == null ? JsonUtility.ToJson(selection) : JsonUtility.ToJson(generated.Rule);
            DirectoryIdentity = ResourceProjectPaths.SemanticIdentity(project);
        }
    }

    /// <summary>Editor 主资产身份和加载描述。子资产的正式描述与持久化 Catalog 不在此定义。</summary>
    public sealed class ResourceBuildAsset
    {
        public string Guid { get; }
        public long LocalId { get; }
        public string AssetId => Guid + "_" + LocalId;
        public string SourcePath { get; }
        public ResourceAddress Address { get; }
        public IReadOnlyList<ResourceAddress> Aliases { get; }
        public Type AssetType { get; }
        public string BundleName { get; internal set; }
        public ResourceBuildSource Source { get; }
        public bool IsScene => AssetType == typeof(SceneAsset);

        internal ResourceBuildAsset(string path, ResourceAddress address, IEnumerable<string> aliases, ResourceBuildSource source)
        {
            Source = source;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (!asset || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                throw new InvalidOperationException("Cannot resolve Unity asset identity: " + path);
            Guid = guid;
            LocalId = localId;
            SourcePath = path;
            Address = address;
            Aliases = aliases.Select(x => new ResourceAddress(address.ProjectId, x)).OrderBy(x => x.Path, StringComparer.Ordinal).ToList().AsReadOnly();
            AssetType = ResourceBuildMap.BuildType(asset.GetType());
        }
    }

    public enum ResourceBundlePlacement { Group, PerAsset, SharedDependencies, SceneDependencies, CycleMerge }

    /// <summary>每个源文件恰好属于一个 Bundle；数组按 ordinal 排序并以副本公开。</summary>
    public sealed class ResourceBuildBundle
    {
        private readonly string[] paths;
        private readonly string[] dependencies;
        private readonly string[] dependents;
        public string Name { get; }
        public bool IsScene { get; }
        public bool IsShared { get; }
        public ResourceBundlePlacement Placement { get; }
        /// <summary>源文件磁盘字节之和，不含meta，不是压缩Bundle大小或native内存。</summary>
        public long SourceBytes { get; }
        public string[] AssetPaths => (string[])paths.Clone();
        public string[] Dependencies => (string[])dependencies.Clone();
        public string[] Dependents => (string[])dependents.Clone();

        internal ResourceBuildBundle(string name, IEnumerable<string> assets, IEnumerable<string> edges, IEnumerable<string> incoming,
            bool shared, ResourceBundlePlacement placement)
        {
            Name = name;
            paths = assets.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            dependencies = edges.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            dependents = incoming.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            IsScene = paths.All(x => Path.GetExtension(x).Equals(".unity", StringComparison.OrdinalIgnoreCase));
            if (!IsScene && paths.Any(x => Path.GetExtension(x).Equals(".unity", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Scene and ordinary assets cannot share a Bundle: " + name);
            IsShared = shared;
            Placement = placement;
            SourceBytes = paths.Sum(path => new FileInfo(ResourceBuildMap.Absolute(path)).Length);
        }
    }

    /// <summary>从已保存的自有 Project 生成稳定显式 BuildMap；不修改任何源资源或 Importer。</summary>
    public sealed class ResourceBuildMap
    {
        private const int Limit = 16384;
        public string ProjectGuid { get; }
        public string ProjectId { get; }
        public string ProjectToken { get; }
        public IReadOnlyList<ResourceProjectPaths.DirectoryEntry> SourceDirectories { get; }
        public IReadOnlyList<string> AssetRoots { get; }
        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
        public bool ContainsUnsavedDefinitions { get; }
        public string Platform { get; }
        public IReadOnlyList<ResourceBuildAsset> Assets { get; }
        public IReadOnlyList<ResourceBuildBundle> Bundles { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Groups { get; }
        /// <summary>基于源文件字节的重复避免估计，不代表压缩产物或 native memory 的测量。</summary>
        public long AvoidedDuplicateSourceBytes { get; }
        public int MergedBundleCount { get; }
        internal IReadOnlyList<string> FingerprintPaths { get; }

        private ResourceBuildMap(ResourceProject project, string guid, ResourceProjectPaths paths, List<ResourceBuildAsset> assets,
            Dictionary<string, IReadOnlyList<string>> groups, Dictionary<string, string[]> graph,
            HashSet<string> fingerprintPaths, Dictionary<string, string> assignments, HashSet<string> shared,
            long avoidedBytes, int merged)
        {
            ProjectGuid = guid;
            ProjectId = project.ProjectId;
            ProjectToken = Token(ProjectId);
            SourceDirectories = paths.Directories;
            AssetRoots = Array.AsReadOnly(paths.Directories.Select(x => x.Path).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            ContainsUnsavedDefinitions = EditorUtility.IsDirty(project) || project.AutomaticAtlases && EditorUtility.IsDirty(project.AutomaticAtlases);
            Platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            Assets = assets.OrderBy(x => x.Address.Path, StringComparer.Ordinal).ToList().AsReadOnly();
            Groups = new ReadOnlyDictionary<string, IReadOnlyList<string>>(groups);
            FingerprintPaths = fingerprintPaths.OrderBy(x => x, StringComparer.Ordinal).ToList().AsReadOnly();
            var edges = BundleEdges(assignments, graph);
            var incoming = edges.Keys.ToDictionary(name => name, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var edge in edges) foreach (var dependency in edge.Value) incoming[dependency].Add(edge.Key);
            var prefixLength = ("ylr_" + ProjectToken + "_").Length;
            ResourceBundlePlacement Placement(string name) => name.Substring(prefixLength, 2) switch
            {
                "g_" => ResourceBundlePlacement.Group,
                "a_" => ResourceBundlePlacement.PerAsset,
                "s_" => ResourceBundlePlacement.SharedDependencies,
                "d_" => ResourceBundlePlacement.SceneDependencies,
                "c_" => ResourceBundlePlacement.CycleMerge,
                _ => throw new InvalidOperationException("Unknown Bundle placement: " + name)
            };
            Bundles = assignments.GroupBy(x => x.Value).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new ResourceBuildBundle(x.Key, x.Select(a => a.Key), edges[x.Key], incoming[x.Key], shared.Contains(x.Key), Placement(x.Key)))
                .ToList().AsReadOnly();
            foreach (var asset in assets) asset.BundleName = assignments[asset.SourcePath];
            AvoidedDuplicateSourceBytes = avoidedBytes;
            MergedBundleCount = merged;
            // 复用 8.1 有界 DAG 合同，仅验证，不挂载 Runtime 或引入另一个目录权威。
            _ = new ResourceCatalog(ProjectId, Array.Empty<ResourceCatalogEntry>(),
                Bundles.Select(x => new ResourceDependencyNode(x.Name, x.Dependencies)));
        }

        public static ResourceBuildMap Create(ResourceProject project)
        {
            if (!project) throw new ArgumentNullException(nameof(project));
            ValidateId(project.ProjectId);
            var projectPath = AssetDatabase.GetAssetPath(project);
            var guid = AssetDatabase.AssetPathToGUID(projectPath);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("ResourceProject must be saved with its own Unity GUID.");
            var directories = ResourceProjectPaths.Resolve(project);
            var generatedAtlases = ResourceAtlasGenerator.CurrentOutputs(project);
            var atlasDirectory = project.AutomaticAtlases ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(project.AutomaticAtlases))?.Replace('\\', '/') : null;
            var assets = new List<ResourceBuildAsset>();
            var groups = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
            var foldedAddresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var foldedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefix = "ylr_" + Token(project.ProjectId) + "_";
            var editorAssemblies = new HashSet<string>(CompilationPipeline.GetAssemblies()
                .Where(x => (x.flags & AssemblyFlags.EditorAssembly) != 0).Select(x => x.name), StringComparer.Ordinal);
            if (project.Groups == null || project.Groups.Count == 0 || project.Groups.Count > Limit)
                throw new InvalidOperationException("A bounded nonempty Group collection is required.");
            foreach (var group in project.Groups.OrderBy(x => x?.Id, StringComparer.Ordinal))
            {
                if (group == null) throw new InvalidOperationException("Null Group.");
                ValidateId(group.Id);
                if (!foldedGroups.Add(group.Id)) throw new InvalidOperationException("Duplicate/case-folded Group: " + group.Id);
                if (!Enum.IsDefined(typeof(ResourcePacking), group.Packing) || group.Selections == null)
                    throw new InvalidOperationException("Invalid Group packing or empty selection: " + group.Id);
                var members = new List<string>();
                var selections = group.Selections.Concat(generatedAtlases.Where(x => x.GroupId == group.Id)
                    .Select(x => new ResourceSelection { Guid = x.Guid, Address = x.Address }));
                foreach (var selection in selections)
                {
                    if (selection == null) throw new InvalidOperationException("Null resource selection.");
                    var selected = ResolveGuid(selection.Guid);
                    var generated = generatedAtlases.FirstOrDefault(x => x.Guid == selection.Guid && x.GroupId == group.Id && x.Address == selection.Address);
                    if (generated == null) directories.RequireSource(selected);
                    if (atlasDirectory != null && (selected == atlasDirectory || Within(atlasDirectory, selected)) &&
                        generated == null)
                        throw new InvalidOperationException("Generated Atlas assets are selected by their rules, not manual selections: " + selected);
                    var folder = AssetDatabase.IsValidFolder(selected);
                    var aliases = selection.Aliases ?? Array.Empty<string>();
                    if (aliases.Length > Limit) throw new InvalidOperationException("Resource alias limit exceeded.");
                    if (folder && (!string.IsNullOrEmpty(selection.Address) || aliases.Length != 0))
                        throw new InvalidOperationException("Folder selections cannot override individual addresses.");
                    var extensions = selection.Extensions ?? Array.Empty<string>();
                    if (extensions.Any(x => string.IsNullOrEmpty(x) || x[0] != '.' || x.IndexOfAny(new[] { '/', '\\', '*' }) >= 0))
                        throw new InvalidOperationException("Selection extensions must be exact dotted extensions.");
                    var source = new ResourceBuildSource(project, guid, group, selection, selected,
                        generated);
                    var paths = folder ? AssetDatabase.FindAssets("", new[] { selected }).Select(AssetDatabase.GUIDToAssetPath)
                        .Where(x => !AssetDatabase.IsValidFolder(x) && (atlasDirectory == null || !Within(atlasDirectory, x)) && CanBundle(x, editorAssemblies)) : new[] { selected };
                    foreach (var path in paths.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        if (extensions.Length != 0 && !extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) continue;
                        if (!CanBundle(path, editorAssemblies)) throw new InvalidOperationException("Unsupported selected asset: " + path);
                        if (assignments.ContainsKey(path)) throw new InvalidOperationException("Asset selected more than once: " + path);
                        if (assets.Count == Limit) throw new InvalidOperationException("Resource selection limit exceeded.");
                        ResourceBuildOperation.RejectLinks(Absolute(path));
                        var addressText = string.IsNullOrEmpty(selection.Address) ? directories.DefaultAddress(path) : selection.Address;
                        ResourceAddress address;
                        try { address = new ResourceAddress(project.ProjectId, addressText); }
                        catch (ArgumentException error) { throw new InvalidOperationException("Invalid resource address: " + addressText, error); }
                        foreach (var key in new[] { addressText }.Concat(aliases))
                        {
                            try { _ = new ResourceAddress(project.ProjectId, key); }
                            catch (ArgumentException error) { throw new InvalidOperationException("Invalid resource alias/address: " + key, error); }
                            if (foldedAddresses.TryGetValue(key, out var previous))
                                throw new InvalidOperationException("两个资源使用了相同的加载地址或别名 (Duplicate/case-folded address)：" + key + "\n" + previous + "\n" + path +
                                    "\n请为其中一个设置不同的地址或目录地址前缀。");
                            if (foldedAddresses.Count == Limit) throw new InvalidOperationException("Resource address limit exceeded.");
                            foldedAddresses.Add(key, path);
                        }
                        var asset = new ResourceBuildAsset(path, address, aliases, source);
                        var bundle = prefix + (group.Packing == ResourcePacking.PerAsset ? "a_" + asset.Guid :
                            "g_" + Token(group.Id) + (asset.IsScene ? "_scenes" : "_assets"));
                        assets.Add(asset);
                        members.Add(asset.AssetId);
                        assignments.Add(path, bundle);
                    }
                }
                if (members.Count == 0) throw new InvalidOperationException("Group selects no buildable assets: " + group.Id);
                groups.Add(group.Id, members.OrderBy(x => x, StringComparer.Ordinal).ToList().AsReadOnly());
            }
            var fingerprints = new HashSet<string>(StringComparer.Ordinal) { projectPath };
            if (project.AutomaticAtlases) fingerprints.Add(AssetDatabase.GetAssetPath(project.AutomaticAtlases));
            var graph = CollectDependencies(directories, generatedAtlases.Select(x => x.Path), atlasDirectory, assignments.Keys, fingerprints, editorAssemblies);
            ResourceAtlasRules.Validate(graph.Keys, assets);
            var original = new Dictionary<string, string>(assignments, StringComparer.Ordinal);
            var owners = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var bundle in original.GroupBy(x => x.Value).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var queue = new Queue<string>(bundle.Select(x => x.Key));
                var visited = new HashSet<string>(StringComparer.Ordinal);
                while (queue.Count != 0)
                {
                    var path = queue.Dequeue();
                    if (!visited.Add(path)) continue;
                    // 已显式归属的另一 Bundle 是图边，不再把它的私有依赖重新归属给当前包。
                    if (original.TryGetValue(path, out var explicitOwner) && explicitOwner != bundle.Key) continue;
                    if (!original.ContainsKey(path))
                    {
                        if (!owners.TryGetValue(path, out var set)) owners.Add(path, set = new HashSet<string>(StringComparer.Ordinal));
                        set.Add(bundle.Key);
                    }
                    foreach (var dependency in graph[path]) queue.Enqueue(dependency);
                }
            }
            var shared = new HashSet<string>(StringComparer.Ordinal);
            var sceneBundles = new HashSet<string>(assets.Where(x => x.IsScene).Select(x => original[x.SourcePath]), StringComparer.Ordinal);
            long avoided = 0;
            foreach (var bucket in owners.GroupBy(x => string.Join("\n", x.Value.OrderBy(a => a, StringComparer.Ordinal))).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var entries = bucket.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
                var users = entries[0].Value;
                var name = users.Count == 1 && !sceneBundles.Contains(users.Single()) ? users.Single() :
                    prefix + (users.Count == 1 ? "d_" : "s_") + Token(bucket.Key + "\n" +
                    string.Join("\n", entries.Select(x => AssetDatabase.AssetPathToGUID(x.Key)).OrderBy(x => x, StringComparer.Ordinal)));
                if (users.Count > 1) shared.Add(name);
                foreach (var entry in entries)
                {
                    assignments.Add(entry.Key, name);
                    avoided += new FileInfo(Absolute(entry.Key)).Length * (users.Count - 1);
                }
            }
            var merged = MergeCycles(prefix, assignments, graph, shared);
            return new ResourceBuildMap(project, guid, directories, assets, groups, graph, fingerprints, assignments, shared, avoided, merged);
        }

        private static Dictionary<string, string[]> CollectDependencies(ResourceProjectPaths directories, IEnumerable<string> generatedPaths, string atlasDirectory,
            IEnumerable<string> selected, HashSet<string> fingerprints, HashSet<string> editorAssemblies)
        {
            var generated = new HashSet<string>(generatedPaths, StringComparer.Ordinal);
            void RequirePath(string path)
            {
                if (generated.Contains(path)) return;
                if (ResourceProjectPaths.Contains(atlasDirectory, path))
                    throw new InvalidOperationException("不能将生成目录中的配置或失效输出作为依赖：" + path);
                directories.RequireSource(path);
            }
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var queue = new Queue<string>(selected);
            var foldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var edgeCount = 0;
            while (queue.Count != 0)
            {
                var path = queue.Dequeue();
                if (result.ContainsKey(path)) continue;
                if (result.Count == Limit || !foldedPaths.Add(path)) throw new InvalidOperationException("Asset graph limit or case-folded path conflict: " + path);
                RequirePath(path);
                ResourceBuildOperation.RejectLinks(Absolute(path));
                fingerprints.Add(path);
                var dependencies = new List<string>();
                var atlas = AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(UnityEngine.U2D.SpriteAtlas) ?
                    AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(path) : null;
                var inputs = AssetDatabase.GetDependencies(path, false).AsEnumerable();
                if (atlas) inputs = inputs.Where(value => !AssetDatabase.IsValidFolder(value)).Concat(ResourceAtlasRules.SpritePaths(atlas));
                foreach (var dependency in inputs.Distinct().OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (dependency == path || IsBuiltIn(dependency)) continue;
                    fingerprints.Add(dependency);
                    if (IsCode(dependency)) continue;
                    RequirePath(dependency);
                    if (!CanBundle(dependency, editorAssemblies)) throw new InvalidOperationException("Unsupported asset dependency: " + path + " -> " + dependency);
                    if (++edgeCount > Limit * 4) throw new InvalidOperationException("Asset dependency edge limit exceeded.");
                    dependencies.Add(dependency);
                    queue.Enqueue(dependency);
                }
                result.Add(path, dependencies.ToArray());
            }
            return result;
        }

        private static Dictionary<string, HashSet<string>> BundleEdges(Dictionary<string, string> assignments, Dictionary<string, string[]> graph)
        {
            var edges = assignments.Values.Distinct().ToDictionary(x => x, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var asset in assignments)
                foreach (var dependency in graph[asset.Key])
                {
                    var to = assignments[dependency];
                    if (to != asset.Value) edges[asset.Value].Add(to);
                }
            return edges;
        }

        private static int MergeCycles(string prefix, Dictionary<string, string> assignments, Dictionary<string, string[]> graph, HashSet<string> shared)
        {
            var edges = BundleEdges(assignments, graph);
            var reverse = edges.Keys.ToDictionary(x => x, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var edge in edges) foreach (var dependency in edge.Value) reverse[dependency].Add(edge.Key);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var root in edges.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var stack = new Stack<(string Id, bool Exit)>();
                stack.Push((root, false));
                while (stack.Count != 0)
                {
                    var item = stack.Pop();
                    if (item.Exit) { order.Add(item.Id); continue; }
                    if (!visited.Add(item.Id)) continue;
                    stack.Push((item.Id, true));
                    foreach (var dependency in edges[item.Id].OrderByDescending(x => x, StringComparer.Ordinal)) stack.Push((dependency, false));
                }
            }
            visited.Clear();
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            var merged = 0;
            for (var i = order.Count - 1; i >= 0; i--)
            {
                var component = new List<string>();
                var stack = new Stack<string>();
                stack.Push(order[i]);
                while (stack.Count != 0)
                {
                    var id = stack.Pop();
                    if (!visited.Add(id)) continue;
                    component.Add(id);
                    foreach (var dependent in reverse[id]) stack.Push(dependent);
                }
                if (component.Count <= 1) continue;
                var paths = assignments.Where(x => component.Contains(x.Value)).Select(x => x.Key).ToArray();
                var scenes = paths.Count(x => Path.GetExtension(x).Equals(".unity", StringComparison.OrdinalIgnoreCase));
                if (scenes != 0 && scenes != paths.Length)
                    throw new InvalidOperationException("Scene/asset Bundle cycle cannot be merged: " + string.Join(" -> ", component));
                var name = prefix + "c_" + Token(string.Join("\n", component.OrderBy(x => x, StringComparer.Ordinal)));
                if (component.Any(shared.Contains)) shared.Add(name);
                foreach (var id in component) replacements.Add(id, name);
                merged += component.Count - 1;
            }
            foreach (var path in assignments.Keys.ToArray())
                if (replacements.TryGetValue(assignments[path], out var replacement)) assignments[path] = replacement;
            return merged;
        }

        private static void ValidateId(string id)
        {
            try { ResourceAddress.ValidateId(id, nameof(id)); }
            catch (ArgumentException error) { throw new InvalidOperationException("Invalid stable resource identifier: " + id, error); }
        }

        private static string ResolveGuid(string guid)
        {
            if (guid == null || guid.Length != 32 || guid.Any(x => !Uri.IsHexDigit(x)))
                throw new InvalidOperationException("An exact Unity GUID is required: " + guid);
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Unresolved Unity GUID: " + guid);
            return path;
        }

        private static bool Within(string root, string path) => path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        private static bool IsBuiltIn(string path) => path == "Resources/unity_builtin_extra" || path == "Library/unity default resources";
        private static bool IsCode(string path) => new[] { ".cs", ".dll", ".asmdef", ".asmref" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        private static bool CanBundle(string path, HashSet<string> editorAssemblies)
        {
            if (AssetDatabase.IsValidFolder(path) || IsCode(path) || path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            var sourceType = AssetDatabase.GetMainAssetTypeAtPath(path);
            var type = BuildType(sourceType);
            return type != null && type != typeof(DefaultAsset) && type != typeof(MonoScript) &&
                (type == typeof(SceneAsset) || !type.Assembly.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal) &&
                    !editorAssemblies.Contains(type.Assembly.GetName().Name));
        }

        // Unity 的 Controller/BlendTree 编辑类型有合法的运行时产物；不能把 Editor 类型统一视为不可构建。
        internal static Type BuildType(Type source) => source == typeof(UnityEditor.Animations.AnimatorController) ? typeof(RuntimeAnimatorController) :
            source == typeof(UnityEditor.Animations.BlendTree) ? typeof(Motion) : source;

        internal static string Absolute(string path) => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), path));
        internal static string Token(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant().Substring(0, 32);
        }

        internal AssetBundleBuild[] UnityBuilds() => Bundles.Select(x => new AssetBundleBuild { assetBundleName = x.Name, assetNames = x.AssetPaths }).ToArray();
    }
}
