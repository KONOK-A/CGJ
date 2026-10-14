using System;
using System.Collections.Generic;
using UnityEngine;

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
        public List<ResourceGroup> Groups = new();
    }

    public enum ResourcePacking { Together, PerAsset }

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
        public string[] Extensions = Array.Empty<string>();
    }
}
