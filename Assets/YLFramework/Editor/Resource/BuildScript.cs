using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YLFramework.Utilities.Extensions;

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>记录 AssetBundle 信息的类。</summary>
    public class AssetBundleInfo
    {
        public readonly string Name;

        public AssetBundleInfo(string name)
        {
            Name = name;
        }
        /// <summary>
        /// 该AssetBundle里记录的所有Asset名称
        /// </summary>
        public string[] Assets;
    }
    
    /// <summary>
    /// AssetBundle 构建入口：把工程中的 Bundle 拆分为默认与各子工程的构建列表，
    /// 交给 BuildPipeline 构建，并把产物与资源数据表复制到 StreamingAssets。
    /// </summary>
    public class BuildScript
    {
        /// <summary>
        /// 预检并确认当前平台的 YL 输出，全部 Bundle 与数据表在暂存目录成功后统一发布。
        /// 未知归属、取消及构建失败不替换旧产物。平台须已在 Unity Build Settings 激活。
        /// </summary>
        /// <param name="buildTarget">构建目标平台。</param>
        public static void BuildAssetBundles(BuildTarget buildTarget)
        {
            BuildWithConfirmation(buildTarget);
        }

        internal static bool BuildWithConfirmation(BuildTarget buildTarget)
        {
            if (!RunBuild(buildTarget, summary => EditorUtility.DisplayDialog("确认构建 YL AssetBundle", summary, "构建", "取消"),
                    (path, builds, target) => BuildPipeline.BuildAssetBundles(path, builds,
                        BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target))) return false;
            Debug.Log("YL AssetBundle 与 ResData 构建及发布成功。");
            // 常量工具保留既有入口，与 Bundle 发布分开报告；完整修复在阶段 6.3。
            try { GenerateVersionConfig(); }
            catch (Exception exception) { Debug.LogWarning("Bundle 已发布，但资源常量生成失败：" + exception.Message); }
            return true;
        }

        internal static bool RunBuild(BuildTarget target, Func<string, bool> confirm,
            Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> builder) =>
            ResourceBuildOperation.Execute(ResourceBuildOperation.CreatePlan(target, true), true, confirm, builder);

        private static void GenerateVersionConfig()
        {
            if (ResServiceEditorWindow.EnableGenerateClass)
            {
                WriteResReferenceClass();
            }
        }

        /// <summary>生成资源名常量类到 Assets/YLFrameworkData/YLResData.cs。</summary>
        public static void WriteResReferenceClass()
        {
            "Assets/YLFrameworkData".CreateDirIfNotExists();
            var path = Path.GetFullPath(
                Application.dataPath + Path.DirectorySeparatorChar + "YLFrameworkData/YLResData.cs");
            var writer = new StreamWriter(File.Open(path, FileMode.Create));
            ResDataCodeGenerator.WriteClass(writer,"YLFramework.Services.Resource");
            writer.Close();
            AssetDatabase.Refresh();
        }

    }
}
