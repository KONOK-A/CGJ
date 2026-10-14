
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源数据表的构建工具：从 AssetDatabase 收集 AssetBundle 信息（编辑器专用）。
    /// </summary>
    public class ConfigFileUtility
    {
        /// <summary>创建一个从 AssetDatabase 收集 AssetBundle 信息的 <see cref="ResData"/>；非编辑器环境返回空表。</summary>
        public static ResData BuildEditorDataTable()
        {
            var resData = new ResData();
            AddABInfoToResDate(resData);
            return resData;
        }

        /// <summary>
        /// 把 AssetBundle 信息收集到数据表中：为每个 Bundle 添加记录，
        /// 并为其中的每个资产添加 AssetData（以 .unity 结尾的标记为 ABScene，其余为 ABAsset）。
        /// 非编辑器环境该方法不做任何事。
        /// </summary>
        /// <param name="abResDataFile">要写入的数据表。</param>
        /// <param name="abNames">要处理的 Bundle 名集合；null 时取 AssetDatabase 中全部 Bundle。</param>
        public static void AddABInfoToResDate(IResData abResDataFile, string[] abNames = null)
        {
#if UNITY_EDITOR
            AssetDatabase.RemoveUnusedAssetBundleNames();
            var assetBundleNames = abNames ?? AssetDatabase.GetAllAssetBundleNames();
            foreach (var abName in assetBundleNames)
            {
                var depends = AssetDatabase.GetAssetBundleDependencies(abName,false);
                var abIndex = abResDataFile.AddAssetBundleRecord(abName,depends,out var group);
                if (abIndex < 0)
                {
                    continue;
                }
                var assets = AssetDatabase.GetAssetPathsFromAssetBundle(abName);
                foreach (var asset in assets)
                {
                    var type = AssetDatabase.GetMainAssetTypeAtPath(asset);
                    var code = type.ToCode();
                    //以.unity结尾的文件为场景
                    group.AddAssetData(asset.EndsWith(".unity")
                        ? new AssetData(AssetPathToName(asset), ResLoadType.ABScene, abIndex, abName, code)
                        : new AssetData(AssetPathToName(asset), ResLoadType.ABAsset, abIndex, abName, code));
                }
            }
#endif
        }

        /// <summary>把工程内资产路径转换为小写资源名：取文件名部分并去掉扩展名。</summary>
        public static string AssetPathToName(string assetPath)
        {
            var startIndex = assetPath.LastIndexOf('/') + 1;
            var endIndex = assetPath.LastIndexOf('.');
            if (endIndex > 0)
            {
                var length = endIndex - startIndex;
                return assetPath.Substring(startIndex, length).ToLowerInvariant();
            }
            return assetPath[startIndex..].ToLowerInvariant();
        }
        
         
    }
}