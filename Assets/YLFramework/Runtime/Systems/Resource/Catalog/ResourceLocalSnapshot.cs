using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>显式绑定的本地 v1 发布快照。完整验证只读目录，不选择版本、不写索引、不加载 native Bundle。</summary>
    public sealed class ResourceLocalSnapshot
    {
        public string DirectoryPath { get; }
        public ResourceCatalogDocument Document { get; }
        internal readonly Dictionary<string, ResourceCatalogBundle> Bundles;

        private ResourceLocalSnapshot(string directory, ResourceCatalogDocument document)
        {
            DirectoryPath = directory;
            Document = document;
            Bundles = document.Bundles.ToDictionary(x => x.LogicalId, StringComparer.Ordinal);
        }

        /// <summary>摘要来自调用方的明确发布选择，不能从待验证文件自身推导信任。仅支持普通可读本地目录。</summary>
        public static ResourceLocalSnapshot Open(string directory, string projectId, string catalogDigest, string platform, string appContentCompatibilityId)
        {
            ResourceCatalogCodec.Hex(catalogDigest, 64);
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) throw new ArgumentException("An absolute local publication directory is required.");
            directory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            RejectLinks(directory);
            if (Path.GetFileName(directory) != catalogDigest) throw new InvalidDataException("The explicit version directory must equal CatalogDigest.");
            var bytes = ReadBounded(Path.Combine(directory, "resource-catalog.json"));
            if (ResourceCatalogCodec.Digest(bytes) != catalogDigest) throw new InvalidDataException("Explicit CatalogDigest mismatch.");
            var document = ResourceCatalogCodec.Read(bytes, projectId, platform, Application.unityVersion, appContentCompatibilityId);
            var complete = ResourceCatalogCodec.ParseObject(ReadBounded(Path.Combine(directory, ".ylr-complete.json")));
            ResourceCatalogCodec.Fields(complete, "Version,ProjectGuid,ProjectId,Platform,BuildId,CatalogDigest");
            ResourceCatalogCodec.Hex(ResourceCatalogCodec.Text(complete, "ProjectGuid"), 32);
            if (ResourceCatalogCodec.Integer(complete, "Version") != 1 || ResourceCatalogCodec.Text(complete, "ProjectId") != projectId ||
                ResourceCatalogCodec.Text(complete, "Platform") != platform || ResourceCatalogCodec.Text(complete, "BuildId") != document.BuildId ||
                ResourceCatalogCodec.Text(complete, "CatalogDigest") != catalogDigest)
                throw new InvalidDataException("Publication Complete/Catalog identity mismatch.");
            var files = document.Bundles.Cast<ResourceCatalogFile>().Concat(document.SupportFiles).ToArray();
            var expected = files.Select(x => x.RelativeFile).Concat(new[] { "resource-catalog.json", ".ylr-complete.json" }).OrderBy(x => x, StringComparer.Ordinal);
            if (!Directory.GetFileSystemEntries(directory).All(File.Exists) || !Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected))
                throw new InvalidDataException("Publication contains missing or unregistered content.");
            var snapshot = new ResourceLocalSnapshot(directory, document);
            foreach (var file in files) using (snapshot.OpenVerified(file)) { }
            return snapshot;
        }

        /// <summary>显式编译类型映射。默认包含常用 Unity 资产；自定义 ScriptableObject 等须提供 typeof，不反射扫描或从字符串创建类型。</summary>
        public ResourceCatalog CreateCatalog(IEnumerable<Type> assetTypes = null)
        {
            var types = new Dictionary<(string, string), Type>();
            foreach (var type in new[] { typeof(TextAsset), typeof(GameObject), typeof(Texture2D), typeof(Sprite), typeof(Material), typeof(Shader),
                typeof(AudioClip), typeof(AnimationClip), typeof(Mesh), typeof(Font), typeof(RuntimeAnimatorController), typeof(PhysicMaterial), typeof(PhysicsMaterial2D) }
                .Concat(assetTypes ?? Array.Empty<Type>()))
            {
                if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type) || type.ContainsGenericParameters)
                    throw new ArgumentException("A compiled Unity asset type is required.");
                types[(type.FullName, type.Assembly.GetName().Name)] = type;
            }
            var entries = new List<ResourceCatalogEntry>();
            foreach (var asset in Document.Assets.Where(x => x.Kind != "Scene"))
            {
                if (!types.TryGetValue((asset.AssetType, asset.AssetAssembly), out var type))
                    throw new ResourceException(ResourceError.TypeMismatch, $"No explicit compiled type for {asset.AssetType}, {asset.AssetAssembly}.");
                var entry = new ResourceCatalogEntry(asset.AssetId, new ResourceAddress(Document.ProjectId, asset.Address, asset.SubAssetKey),
                    new ResourceLocation(ResourceSourceId.AssetBundle, asset.LoadName, type),
                    asset.Aliases.Select(x => new ResourceAddress(Document.ProjectId, x, asset.SubAssetKey)));
                entry.Snapshot = this;
                entry.Descriptor = asset;
                entries.Add(entry);
            }
            return new ResourceCatalog(Document.ProjectId, entries) { Snapshot = this, Groups = Document.Groups };
        }

        // 文件首次进入 native 前复验并保持只读句柄至卸载，防止校验后被受控工具改写/替换。
        internal FileStream OpenVerified(ResourceCatalogFile file)
        {
            var path = Path.Combine(DirectoryPath, file.RelativeFile);
            RejectLinks(path);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                using var sha = SHA256.Create();
                if (stream.Length != file.Length || BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != file.Sha256)
                    throw new InvalidDataException("Snapshot file integrity mismatch: " + file.RelativeFile);
                stream.Position = 0;
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }

        private static byte[] ReadBounded(string path)
        {
            RejectLinks(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0 || stream.Length > ResourceCatalogCodec.MaximumBytes) throw new InvalidDataException("Publication metadata size limit exceeded.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        internal static void RejectLinks(string path)
        {
            for (var cursor = Path.GetFullPath(path); !string.IsNullOrEmpty(cursor); cursor = Path.GetDirectoryName(cursor))
                if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked resource input is not supported: " + cursor);
        }
    }
}
