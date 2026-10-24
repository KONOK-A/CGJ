using UnityEditor;
using YLFramework.Editor.Resource;
using YLFramework.Resource;

namespace YLFramework.Samples.Resource.Editor
{
    /// <summary>在已稳定的 Editor 主线程调用；不打开窗口、不依赖 Selection，不代表独立 CI/batchmode 验收。</summary>
    public static class ResourceBuildRecipeExample
    {
        public static ResourceEditorBuildReport Build(string recipeAssetPath, IResourceContentKeyProvider applicationKeys = null)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<ResourceBuildRecipe>(recipeAssetPath);
            return ResourceEditorOperations.ExecuteRecipe(recipe, provider: applicationKeys);
        }
    }
}
