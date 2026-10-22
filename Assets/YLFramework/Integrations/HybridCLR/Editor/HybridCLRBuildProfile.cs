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
    }
}
