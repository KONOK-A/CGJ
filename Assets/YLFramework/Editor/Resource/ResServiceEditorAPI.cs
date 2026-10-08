using UnityEditor;
using UnityEngine;
using YLFramework.Utilities.Extensions;

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// 资源服务的编辑器操作入口（窗口按钮与外部脚本调用）。
    /// </summary>
    public static class ResServiceEditorAPI
    {
        /// <summary>模拟模式开关（转发 <see cref="AssetBundleHelper.SimulationMode"/>）。</summary>
        public static bool SimulationMode
        {
            get => AssetBundleHelper.SimulationMode;
            set => AssetBundleHelper.SimulationMode = value;
        }
        
        /// <summary>按当前激活的构建目标构建 AssetBundle（调用 <see cref="BuildScript.BuildAssetBundles"/>）。</summary>
        public static void BuildAssetBundles()
        {
            AssetDatabase.RemoveUnusedAssetBundleNames();
            AssetDatabase.Refresh();
            BuildScript.BuildAssetBundles(EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>
        /// 强制清除已经构建了的AssetBundle：删除工程内的输出目录与 StreamingAssets 下的 AssetBundles。
        /// </summary>
        public static void ForceClearAssetBundlesHaveBuilt()
        {
            ResServiceAssetsMenu.AssetBundleOutputPath.DeleteDirIfExists();
            (Application.streamingAssetsPath + "/AssetBundles").DeleteDirIfExists();
            AssetDatabase.Refresh();
        }
    }
}