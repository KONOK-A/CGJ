using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>自有资源项目。SO GUID 表示配置归属，ProjectId 表示稳定技术身份；不读取旧 Importer 标记。</summary>
    [CreateAssetMenu(menuName = "YLFramework/Resource/Project", fileName = "ResourceProject")]
    public sealed class ResourceProject : ScriptableObject
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("ProjectId")]
        public string projectId;
        [global::UnityEngine.Serialization.FormerlySerializedAs("DisplayName")]
        public string displayName;
        [global::UnityEngine.Serialization.FormerlySerializedAs("SourceDirectories")]
        [Tooltip("允许选择源资源的文件夹。登记目录不会自动选择其中的资源；由 Group 中的资源条目决定构建内容。")]
        public List<ResourceSourceDirectory> sourceDirectories = new();

        [global::UnityEngine.Serialization.FormerlySerializedAs("Compression")]
        [Tooltip("仅选择Bundle压缩方式；StrictMode及身份/完整性验证始终启用。Lz4适合日常加载，Lzma侧重交付体积，Uncompressed用于明确测量或调试。")]
        public ResourceCompression compression;
        [global::UnityEngine.Serialization.FormerlySerializedAs("ContentEncryption")]
        [Tooltip("None 保留原始交付；加密覆盖全部 Bundle 与二进制 root manifest，仅支持 Windows x64 Editor/Mono。密钥不保存于工程。")]
        public ResourceContentEncryption contentEncryption;
        [global::UnityEngine.Serialization.FormerlySerializedAs("ContentKeyId")]
        [Tooltip("非秘密、区分大小写的稳定内容 KeyId，1..64 个 ASCII 字母/数字/._-。轮换密钥必须使用新 KeyId。")]
        public string contentKeyId;
        [global::UnityEngine.Serialization.FormerlySerializedAs("AutomaticAtlases")]
        [Tooltip("通过 Project Inspector 显式登记的新自有图集范围；规则输出直接进入目标 Group，不修改源 Importer。")]
        public ResourceAtlasConfiguration automaticAtlases;
        [global::UnityEngine.Serialization.FormerlySerializedAs("AutomaticAtlasConfigurationGuid")]
        [SerializeField, HideInInspector] internal string automaticAtlasConfigurationGuid;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Groups")]
        public List<ResourceGroup> groups = new();
    }

    [Serializable]
    public sealed class ResourceSourceDirectory
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("Guid")]
        public string guid;
        [global::UnityEngine.Serialization.FormerlySerializedAs("AddressPrefix")]
        [Tooltip("默认加载地址的固定前缀。留空使用目录内相对路径；修改会影响下次构建，不会改写历史发布或自动增加别名。")]
        public string addressPrefix = "";
    }

    public enum ResourcePacking
    {
        Together,
        PerAsset
    }
    public enum ResourceCompression
    {
        Lz4,
        Lzma,
        Uncompressed
    }

    /// <summary>Group 同时定义资源选择和打包意图；最终 Bundle 可因 Scene 隔离、共享依赖或环归并而变化。</summary>
    [Serializable]
    public sealed class ResourceGroup
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("Id")]
        public string id;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Packing")]
        public ResourcePacking packing;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Selections")]
        public List<ResourceSelection> selections = new();
    }

    /// <summary>显式文件/文件夹 GUID 选择。文件可覆写地址；文件夹递归选择，Extensions 为空表示所有可打包资产。</summary>
    [Serializable]
    public sealed class ResourceSelection
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("Guid")]
        public string guid;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Address")]
        public string address;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Aliases")]
        [Tooltip("同一 canonical 资产的额外逻辑地址；仅文件选择支持，子资产继承这些 alias。")]
        public string[] aliases = Array.Empty<string>();
        [global::UnityEngine.Serialization.FormerlySerializedAs("Extensions")]
        public string[] extensions = Array.Empty<string>();
    }
}
