using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    /// <summary>专属一个 ResourceProject 的自动图集规则及生成归属；仅由显式登记入口创建。</summary>
    public sealed class ResourceAtlasConfiguration : ScriptableObject
    {
        public List<ResourceAtlasRule> Rules = new();

        [SerializeField, HideInInspector] internal string ProjectGuid;
        [SerializeField, HideInInspector] internal string RootGuid;
        [SerializeField, HideInInspector] internal string DirectoryGuid;
        [SerializeField, HideInInspector] internal string ConfigurationGuid;
        [SerializeField, HideInInspector] internal string ConfigurationMetaHash;
        [SerializeField, HideInInspector] internal string DirectoryMetaHash;
        [SerializeField, HideInInspector] internal List<Output> Outputs = new();

        [Serializable]
        internal sealed class Output
        {
            public string Key;
            public string FileName;
            public string Guid;
            public string InputHash;
            public string AssetHash;
            public string MetaHash;
        }
    }

    public enum ResourceAtlasGrouping { Together, PerFolder }

    /// <summary>Id 是稳定技术身份。移动源文件夹不改变其 GUID；修改 Id 则视为新规则。</summary>
    [Serializable]
    public sealed class ResourceAtlasRule
    {
        [Tooltip("稳定规则 Id，生成后不要为了改显示/地址而重命名；重命名会产生新输出并保留旧输出为 Obsolete。")]
        public string Id = "ui";
        [Tooltip("目标 ResourceGroup.Id；输出直接成为其正式主资产，不用手动再加入选择。")]
        public string GroupId;
        public ResourceAtlasGrouping Grouping;
        [Tooltip("Together 的完整逻辑地址；PerFolder 在其后追加资源根相对文件夹路径。")]
        public string Address = "atlases/ui";
        [Tooltip("专用资源根内的目录或 Sprite 类型 Texture2D；目录递归收集已导入的 Sprite，不修改 Importer。不能包含生成范围。")]
        public UnityEngine.Object[] Sources = Array.Empty<UnityEngine.Object>();
        public bool AllowRotation;
        public bool TightPacking;
        [Tooltip("合法值为 2、4、8。")]
        public int Padding = 4;
        [Tooltip("32..8192 的二次幂；由 Unity 处理多页，不隐式拆分或更换图集身份。")]
        public int MaxTextureSize = 2048;
        public bool Readable;
        public bool MipMaps;
        public bool SRgb = true;
        public FilterMode FilterMode = FilterMode.Bilinear;
        public UnityEditor.TextureImporterCompression Compression = UnityEditor.TextureImporterCompression.Uncompressed;
    }
}
