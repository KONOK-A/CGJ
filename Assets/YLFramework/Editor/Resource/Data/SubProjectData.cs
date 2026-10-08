using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using YLFramework.Utilities.Extensions;

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// 单个子工程的构建数据：子工程资产所在目录中被标记的 AssetBundle 会归入 <see cref="Builds"/>。
    /// </summary>
    public class SubProjectData
    {
        public string Name { get; set; }
        public string Path { get; set; }
        private string Folder { get; set; }
        
        public List<AssetBundleBuild> Builds { get; set; } = new();

        /// <summary>查找工程内全部 <see cref="SubProject"/> 资产并构建数据（Folder 为资产所在目录）。</summary>
        public static List<SubProjectData> SearchAllInProject()
        {
            return AssetDatabase.FindAssets("t:SubProject")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(assetPath => assetPath.EndsWith(".asset"))
                .Select(assetPath =>
                {
                    var subProject = AssetDatabase.LoadAssetAtPath<SubProject>(assetPath);
                    if (subProject)
                    {
                        return new SubProjectData
                        {
                            Path = assetPath,
                            Folder = assetPath.RemoveString(subProject.name + ".asset"),
                            Name = subProject.name
                        };
                    }
                    return null;
                })
                .Where(data => data != null)
                .ToList();
        }
        
        /// <summary>
        /// 按资产路径是否位于子工程目录内，把所有 AssetBundle 拆分为默认构建列表与各子工程的构建列表。
        /// </summary>
        public static void SplitAssetBundles2DefaultAndSubProjectDatas(SubProjectData defaultSubProjectData,
            List<SubProjectData> subProjectDatas)
        {
            var assetBundleNames = AssetDatabase.GetAllAssetBundleNames();

            foreach (var assetBundleName in assetBundleNames)
            {
                var assetBundleBuild = new AssetBundleBuild
                {
                    assetBundleName = assetBundleName,
                    assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName)
                };
                var isDefault = true;


                foreach (var subProjectData in subProjectDatas)
                {
                    foreach (var assetName in assetBundleBuild.assetNames)
                    {
                        if (assetName.Contains(subProjectData.Folder))
                        {
                            subProjectData.Builds.Add(assetBundleBuild);
                            isDefault = false;
                            break;
                        }
                    }
                }

                if (isDefault)
                {
                    defaultSubProjectData.Builds.Add(assetBundleBuild);
                }
            }
        }
    }
}