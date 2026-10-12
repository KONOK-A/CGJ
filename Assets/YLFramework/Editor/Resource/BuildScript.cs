using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
            var plan = ResourceBuildOperation.CreatePlan(buildTarget, true);
            if (!ResourceBuildOperation.Execute(plan, true, summary => EditorUtility.DisplayDialog("确认构建 YL AssetBundle", summary, "构建", "取消"),
                    (path, builds, target) => BuildPipeline.BuildAssetBundles(path, builds,
                        BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target))) return false;
            Debug.Log("YL AssetBundle 与 ResData 构建及发布成功。");
            // 常量对应本次已发布的 owner 快照；其结果仍与 Bundle 发布分开报告。
            try { GenerateVersionConfig(plan); }
            catch (Exception exception) { Debug.LogWarning("Bundle 已发布，但资源常量生成失败：" + exception.Message); }
            return true;
        }

        internal static bool RunBuild(BuildTarget target, Func<string, bool> confirm,
            Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> builder) =>
            ResourceBuildOperation.Execute(ResourceBuildOperation.CreatePlan(target, true), true, confirm, builder);

        private static void GenerateVersionConfig(ResourceBuildOperation.Plan plan)
        {
            if (ResServiceEditorWindow.EnableGenerateClass)
            {
                foreach (var output in plan.Outputs) ResourceBuildOperation.ValidatePublishedSources(output);
                var builds = plan.Outputs.Where((_, index) => index % 2 == 0).SelectMany(output => output.Builds);
                PublishReferences(ResDataCodeGenerator.FromBuilds(builds));
            }
        }

        /// <summary>
        /// 校验当前资源归属后生成 Assets/YLFrameworkData/YLResData.cs 和独立消费者程序集。
        /// 不覆盖手写/修改过的生成物，失败保留旧源码及 .meta；业务 asmdef 须显式引用生成程序集。
        /// </summary>
        public static void WriteResReferenceClass()
        {
            PublishReferences(ResDataCodeGenerator.Collect());
        }

        private static void PublishReferences(AssetBundleInfo[] bundles)
        {
            var source = ResDataCodeGenerator.Generate(bundles, "YLFramework.Services.Resource");
            ResReferenceOutput.Publish(ResReferenceOutput.DirectoryPath, source);
            AssetDatabase.Refresh();
        }

    }
}
