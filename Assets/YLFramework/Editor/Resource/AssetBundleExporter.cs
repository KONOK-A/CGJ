using UnityEditor;
using YLFramework.Utilities.Extensions;

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// 资源数据表导出：收集 AssetBundle 信息并保存为二进制配置文件。
    /// </summary>
    public static class AssetBundleExporter
    {
        /// <summary>
        /// 构建资源数据表并保存到输出目录（默认为 StreamingAssets 下当前平台的 AssetBundles 目录）。
        /// </summary>
        /// <param name="abNames">要收集的 Bundle 名；null 时收集工程内全部 Bundle。</param>
        /// <param name="outputPath">输出目录；null 时使用 StreamingAssets 的默认位置。</param>
        public static void BuildDataTable(string[] abNames = null, string outputPath = null)
        {
            var table = new ResData();
            ConfigFileUtility.AddABInfoToResDate(table,abNames);
            var filePath = (outputPath ?? AssetBundleHelper.StreamingAssetsPath + 
                    AssetBundleSetting.RELATIVE_AB_ROOT_FOLDER).CreateDirIfNotExists() + ResData.FileName;
            table.Save(filePath);
            AssetDatabase.Refresh();
        }
    }
}