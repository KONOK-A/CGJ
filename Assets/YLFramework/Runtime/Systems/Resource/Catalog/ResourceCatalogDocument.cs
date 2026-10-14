using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace YLFramework.Services.Resource
{
    /// <summary>8.2c 冻结的 Schema v1 持久目录。它不挂载 ResourceSystem，也不按类型字符串反射创建对象。</summary>
    public sealed class ResourceCatalogDocument
    {
        private readonly byte[] bytes;
        public int SchemaVersion => 1;
        public string ProjectId { get; }
        public string BuildId { get; }
        public string CatalogDigest { get; }
        public string Platform { get; }
        public string UnityCompatibility { get; }
        public string AppContentCompatibilityId { get; }
        public string BuildSettingsFingerprint { get; }
        public string ManifestFile { get; }
        public IReadOnlyList<ResourceCatalogBundle> Bundles { get; }
        public IReadOnlyList<ResourceCatalogAsset> Assets { get; }
        public IReadOnlyList<ResourceCatalogGroup> Groups { get; }
        public IReadOnlyList<ResourceCatalogFile> SupportFiles { get; }

        internal ResourceCatalogDocument(JObject data, byte[] canonical)
        {
            bytes = (byte[])canonical.Clone();
            ProjectId = (string)data["ProjectId"];
            BuildId = (string)data["BuildId"];
            CatalogDigest = ResourceCatalogCodec.Digest(bytes);
            Platform = (string)data["Platform"];
            UnityCompatibility = (string)data["UnityCompatibility"];
            AppContentCompatibilityId = (string)data["AppContentCompatibilityId"];
            BuildSettingsFingerprint = (string)data["BuildSettingsFingerprint"];
            ManifestFile = (string)data["ManifestFile"];
            Bundles = ((JArray)data["Bundles"]).Select(x => new ResourceCatalogBundle((JObject)x)).ToList().AsReadOnly();
            Assets = ((JArray)data["Assets"]).Select(x => new ResourceCatalogAsset((JObject)x)).ToList().AsReadOnly();
            Groups = ((JArray)data["Groups"]).Select(x => new ResourceCatalogGroup((JObject)x)).ToList().AsReadOnly();
            SupportFiles = ((JArray)data["SupportFiles"]).Select(x => new ResourceCatalogFile((JObject)x)).ToList().AsReadOnly();
        }
        internal byte[] CopyBytes() => (byte[])bytes.Clone();
    }

    public class ResourceCatalogFile
    {
        public string RelativeFile { get; }
        public long Length { get; }
        public string Sha256 { get; }
        internal ResourceCatalogFile(JObject data)
        { RelativeFile = (string)data["RelativeFile"]; Length = (long)data["Length"]; Sha256 = (string)data["Sha256"]; }
    }

    public sealed class ResourceCatalogBundle : ResourceCatalogFile
    {
        public string LogicalId { get; }
        public string NativeName { get; }
        public string UnityHash { get; }
        public bool IsScene { get; }
        public IReadOnlyList<string> Dependencies { get; }
        internal ResourceCatalogBundle(JObject data) : base(data)
        {
            LogicalId = (string)data["LogicalId"]; NativeName = (string)data["NativeName"];
            UnityHash = (string)data["UnityHash"]; IsScene = (bool)data["IsScene"];
            Dependencies = ((JArray)data["Dependencies"]).Select(x => (string)x).ToList().AsReadOnly();
        }
    }

    /// <summary>GUID/localId 是 canonical 身份；SubAssetKey 是 invariant 十进制 localId。类型由 FullName+程序集简单名描述，native 按唯一 name+编译类型定位。</summary>
    public sealed class ResourceCatalogAsset
    {
        public string AssetId { get; }
        public string Guid { get; }
        public long LocalId { get; }
        public string Address { get; }
        public IReadOnlyList<string> Aliases { get; }
        public string SubAssetKey { get; }
        public string BundleId { get; }
        public string LoadName { get; }
        public string AssetType { get; }
        public string AssetAssembly { get; }
        public string Kind { get; }
        public string SubAssetName { get; }
        internal ResourceCatalogAsset(JObject data)
        {
            AssetId = (string)data["AssetId"]; Guid = (string)data["Guid"]; LocalId = (long)data["LocalId"];
            Address = (string)data["Address"]; SubAssetKey = (string)data["SubAssetKey"]; BundleId = (string)data["BundleId"];
            Aliases = ((JArray)data["Aliases"]).Select(x => (string)x).ToList().AsReadOnly();
            LoadName = (string)data["LoadName"]; AssetType = (string)data["AssetType"]; Kind = (string)data["Kind"];
            AssetAssembly = (string)data["AssetAssembly"];
            SubAssetName = (string)data["SubAssetName"];
        }
    }

    public sealed class ResourceCatalogGroupMember
    {
        public string AssetId { get; }
        public bool Required { get; }
        internal ResourceCatalogGroupMember(JObject data) { AssetId = (string)data["AssetId"]; Required = (bool)data["Required"]; }
    }

    public sealed class ResourceCatalogGroup
    {
        public string Id { get; }
        public IReadOnlyList<ResourceCatalogGroupMember> Members { get; }
        internal ResourceCatalogGroup(JObject data)
        {
            Id = (string)data["Id"];
            Members = ((JArray)data["Members"]).Select(x => new ResourceCatalogGroupMember((JObject)x)).ToList().AsReadOnly();
        }
    }
}
