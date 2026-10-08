using System.IO;
using System.Linq;
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
        /// 构建 AssetBundle：清理未使用的 Bundle 名、构建默认与各子工程的 Bundle，
        /// 生成版本配置与资源名常量类，最后把产物复制到 StreamingAssets 并生成资源数据表。
        /// </summary>
        /// <param name="buildTarget">构建目标平台。</param>
        public static void BuildAssetBundles(BuildTarget buildTarget)
        {
            // 先清空一下没用的 ab 名字
            AssetDatabase.RemoveUnusedAssetBundleNames();

            var defaultSubProjectData = new SubProjectData();
            var subProjectDatas = SubProjectData.SearchAllInProject();
            SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(defaultSubProjectData, subProjectDatas);
            
            var outputPath = Path.Combine("AssetBundles", GetPlatformName());
            outputPath.CreateDirIfNotExists();
            
            BuildPipeline.BuildAssetBundles(outputPath,defaultSubProjectData.Builds.ToArray(),
                BuildAssetBundleOptions.ChunkBasedCompression,buildTarget);
            
            GenerateVersionConfig();
            
            var finalDir = Application.streamingAssetsPath + "/AssetBundles/" + GetPlatformName();
            finalDir.DeleteDirIfExists();
            finalDir.CreateDirIfNotExists();
            
            FileUtil.ReplaceDirectory(outputPath, finalDir);

            AssetBundleExporter.BuildDataTable(defaultSubProjectData.Builds.Select(b => b.assetBundleName).ToArray());

            foreach (var subProjectData in subProjectDatas)
            {
                outputPath = Path.Combine("AssetBundles/" + subProjectData.Name,
                    GetPlatformName());
                outputPath.CreateDirIfNotExists();

                BuildPipeline.BuildAssetBundles(outputPath, subProjectData.Builds.ToArray(),
                    BuildAssetBundleOptions.ChunkBasedCompression, buildTarget);
                finalDir = Application.streamingAssetsPath + "/" + subProjectData.Name + "/AssetBundles/" +
                           GetPlatformName();

                finalDir.DeleteDirIfExists();
                finalDir.CreateDirIfNotExists();

                FileUtil.ReplaceDirectory(outputPath, finalDir);
                AssetBundleExporter.BuildDataTable(subProjectData.Builds.Select(b => b.assetBundleName).ToArray(),
                    finalDir + "/");
            }
            AssetDatabase.Refresh();    
        }

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

        private static string GetPlatformName()
        {
            return AssetBundleHelper.GetPlatformName();
        }
    }
}