using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>自有资源项目。SO GUID 表示配置归属，ProjectId 表示稳定技术身份；不读取旧 Importer 标记。</summary>
    [CreateAssetMenu(menuName = "YLFramework/Resource/Project", fileName = "ResourceProject")]
    public sealed class ResourceProject : ScriptableObject
    {
        public string ProjectId;
        public string DisplayName;
        [Tooltip("资源根文件夹的 Unity GUID；依赖必须位于该根内，脚本与引擎内置资源除外。")]
        public string AssetRootGuid;
        [Tooltip("仅选择Bundle压缩方式；StrictMode及身份/完整性验证始终启用。Lz4适合日常加载，Lzma侧重交付体积，Uncompressed用于明确测量或调试。")]
        public ResourceCompression Compression;
        [Tooltip("None 保留原始交付；加密覆盖全部 Bundle 与二进制 root manifest，仅支持 Windows x64 Editor/Mono。密钥不保存于工程。")]
        public ResourceContentEncryption ContentEncryption;
        [Tooltip("非秘密、区分大小写的稳定内容 KeyId，1..64 个 ASCII 字母/数字/._-。轮换密钥必须使用新 KeyId。")]
        public string ContentKeyId;
        [Tooltip("通过 Project Inspector 显式登记的新自有图集范围；规则输出直接进入目标 Group，不修改源 Importer。")]
        public ResourceAtlasConfiguration AutomaticAtlases;
        [SerializeField, HideInInspector] internal string AutomaticAtlasConfigurationGuid;
        public List<ResourceGroup> Groups = new();
    }

    public enum ResourcePacking { Together, PerAsset }
    public enum ResourceCompression { Lz4, Lzma, Uncompressed }

    /// <summary>Group 同时定义资源选择和打包意图；最终 Bundle 可因 Scene 隔离、共享依赖或环归并而变化。</summary>
    [Serializable]
    public sealed class ResourceGroup
    {
        public string Id;
        public ResourcePacking Packing;
        public List<ResourceSelection> Selections = new();
    }

    /// <summary>显式文件/文件夹 GUID 选择。文件可覆写地址；文件夹递归选择，Extensions 为空表示所有可打包资产。</summary>
    [Serializable]
    public sealed class ResourceSelection
    {
        public string Guid;
        public string Address;
        [Tooltip("同一 canonical 资产的额外逻辑地址；仅文件选择支持，子资产继承这些 alias。")]
        public string[] Aliases = Array.Empty<string>();
        public string[] Extensions = Array.Empty<string>();
    }
}
