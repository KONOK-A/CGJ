using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    /// <summary>专属一个 ResourceProject 的自动图集规则及生成归属；仅由显式登记入口创建。</summary>
    public sealed class ResourceAtlasConfiguration : ScriptableObject
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("Rules")]
        public List<ResourceAtlasRule> rules = new();

        [global::UnityEngine.Serialization.FormerlySerializedAs("ProjectGuid")]
        [SerializeField, HideInInspector] internal string projectGuid;
        // 生成范围归属独立于来源目录集合；父目录由显式登记入口指定。
        [global::UnityEngine.Serialization.FormerlySerializedAs("GenerationParentGuid")]
        [SerializeField, HideInInspector] internal string generationParentGuid;
        [global::UnityEngine.Serialization.FormerlySerializedAs("DirectoryGuid")]
        [SerializeField, HideInInspector] internal string directoryGuid;
        [global::UnityEngine.Serialization.FormerlySerializedAs("ConfigurationGuid")]
        [SerializeField, HideInInspector] internal string configurationGuid;
        [global::UnityEngine.Serialization.FormerlySerializedAs("ConfigurationMetaHash")]
        [SerializeField, HideInInspector] internal string configurationMetaHash;
        [global::UnityEngine.Serialization.FormerlySerializedAs("DirectoryMetaHash")]
        [SerializeField, HideInInspector] internal string directoryMetaHash;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Outputs")]
        [SerializeField, HideInInspector] internal List<Output> outputs = new();

        [Serializable]
        internal sealed class Output
        {
            [global::UnityEngine.Serialization.FormerlySerializedAs("Key")]
            public string key;
            [global::UnityEngine.Serialization.FormerlySerializedAs("FileName")]
            public string fileName;
            [global::UnityEngine.Serialization.FormerlySerializedAs("Guid")]
            public string guid;
            [global::UnityEngine.Serialization.FormerlySerializedAs("InputHash")]
            public string inputHash;
            [global::UnityEngine.Serialization.FormerlySerializedAs("AssetHash")]
            public string assetHash;
            [global::UnityEngine.Serialization.FormerlySerializedAs("MetaHash")]
            public string metaHash;
        }
    }

    public enum ResourceAtlasGrouping
    {
        Together,
        PerFolder
    }

    /// <summary>Id 是稳定技术身份。移动源文件夹不改变其 GUID；修改 Id 则视为新规则。</summary>
    [Serializable]
    public sealed class ResourceAtlasRule
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("Id")]
        [Tooltip("稳定规则 Id，生成后不要为了改显示/地址而重命名；重命名会产生新输出并保留旧输出为 Obsolete。")]
        public string id = "ui";
        [global::UnityEngine.Serialization.FormerlySerializedAs("GroupId")]
        [Tooltip("目标 ResourceGroup.Id；输出直接成为其正式主资产，不用手动再加入选择。")]
        public string groupId;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Grouping")]
        public ResourceAtlasGrouping grouping;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Address")]
        [Tooltip("Together 的完整逻辑地址；PerFolder 在其后追加来源目录的固定地址前缀与目录内相对文件夹路径。")]
        public string address = "atlases/ui";
        [global::UnityEngine.Serialization.FormerlySerializedAs("Sources")]
        [Tooltip("已登记来源目录内的文件夹或 Sprite 类型 Texture2D；可跨多个来源，目录递归收集已导入的 Sprite，不修改 Importer。不能包含生成范围。")]
        public UnityEngine.Object[] sources = Array.Empty<UnityEngine.Object>();
        [global::UnityEngine.Serialization.FormerlySerializedAs("AllowRotation")]
        public bool allowRotation;
        [global::UnityEngine.Serialization.FormerlySerializedAs("TightPacking")]
        public bool tightPacking;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Padding")]
        [Tooltip("合法值为 2、4、8。")]
        public int padding = 4;
        [global::UnityEngine.Serialization.FormerlySerializedAs("MaxTextureSize")]
        [Tooltip("32..8192 的二次幂；由 Unity 处理多页，不隐式拆分或更换图集身份。")]
        public int maxTextureSize = 2048;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Readable")]
        public bool readable;
        [global::UnityEngine.Serialization.FormerlySerializedAs("MipMaps")]
        public bool mipMaps;
        [global::UnityEngine.Serialization.FormerlySerializedAs("SRgb")]
        public bool srgb = true;
        [global::UnityEngine.Serialization.FormerlySerializedAs("FilterMode")]
        public FilterMode filterMode = FilterMode.Bilinear;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Compression")]
        public UnityEditor.TextureImporterCompression compression = UnityEditor.TextureImporterCompression.Uncompressed;
    }
}
