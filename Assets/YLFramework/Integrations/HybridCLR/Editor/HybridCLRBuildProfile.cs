using System;
using UnityEngine;
using YLFramework.Editor.Resource;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    /// <summary>显式构建编排配置；Project/Target/基座身份只读取 Resource Recipe，不覆盖官方程序集设置。</summary>
    [CreateAssetMenu(menuName = "YLFramework/HybridCLR/Build Profile", fileName = "HybridCLRBuildProfile")]
    public sealed class HybridCLRBuildProfile : ScriptableObject
    {
        public ResourceBuildRecipe Recipe;
        public bool Development;
        public string GeneratedAssetDirectory;
        public string BaselineDirectory;
        [Tooltip("留空发布官方登记的全部程序集；指定入口时自动包含其热更依赖，不改变全局原生登记。")]
        public string[] HotUpdateAssemblies = Array.Empty<string>();
        [Tooltip("可选：显式复用另一个配置拥有的原生基座。只允许一层绑定，基座目录与兼容身份必须一致。")]
        public HybridCLRBuildProfile BaseProfile;
    }
}
