using UnityEditor;
using UnityEngine;

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
            BuildScript.BuildAssetBundles(EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>
        /// 确认后清除当前平台归属明确的 YL 产物叶目录。未知目录拒绝操作，不清空共享父目录。
        /// </summary>
        public static void ForceClearAssetBundlesHaveBuilt()
        {
            ClearWithConfirmation();
        }

        internal static bool ClearWithConfirmation()
        {
            var plan = ResourceBuildOperation.CreatePlan(EditorUserBuildSettings.activeBuildTarget, false);
            if (ResourceBuildOperation.Execute(plan, false,
                    summary => EditorUtility.DisplayDialog("确认清理 YL AssetBundle", summary, "清理", "取消"), null))
            {
                Debug.Log("YL AssetBundle 当前平台产物已清理：\n" + plan.Summary);
                return true;
            }
            return false;
        }
    }
}
