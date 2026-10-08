using System;
using System.IO;
using UnityEditor;
using Object = UnityEngine.Object;

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// 资源标记菜单与 API：把选中的目录/文件标记为 AssetBundle（Bundle 名取目录名，'.' 替换为 '_'）。
    /// </summary>
    public class ResServiceAssetsMenu
    {
        /// <summary>Bundle 输出目录（相对项目根目录）。</summary>
        public const string AssetBundleOutputPath = "AssetBundles";
        private const string AssetBundleMark = "Assets/YLFramework - AssetBundle Mark";

        private ResServiceAssetsMenu()
        {
            Selection.selectionChanged = OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            var path = GetSelectedPathOrFallback();
            if (!string.IsNullOrEmpty(path))
            {
                Menu.SetChecked(AssetBundleMark,IsMarked(path));
            }
        }

        /// <summary>指定资产路径是否已按目录名规则标记为 AssetBundle；路径无效时返回 false。</summary>
        public static bool IsMarked(string path)
        {
            try
            {
                var assetImporter = AssetImporter.GetAtPath(path);
                var dir = new DirectoryInfo(path);
                return string.Equals(assetImporter.assetBundleName, dir.Name.Replace(".", "_").ToLower());
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>标记指定资产路径为 AssetBundle；已标记时取消标记（清空 assetBundleName）。</summary>
        public static void MarkAB(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var assetImporter = AssetImporter.GetAtPath(path);
                var dir = new DirectoryInfo(path);
                if (IsMarked(path))
                {
                    Menu.SetChecked(AssetBundleMark,false);
                    assetImporter.assetBundleName = null;
                }
                else
                {
                    Menu.SetChecked(AssetBundleMark,true);
                    assetImporter.assetBundleName = dir.Name.Replace(".", "_");
                }
                AssetDatabase.RemoveUnusedAssetBundleNames();
            }
        }

        [MenuItem(AssetBundleMark)]
        public static void MarkDirOrFile()
        {
            var path = GetSelectedPathOrFallback();
            MarkAB(path);
        }
        /// <summary>
        /// 获得当前选择的资产的路径
        /// </summary>
        /// <returns>当前选择中的资产路径；无有效选择时返回空字符串。</returns>
        private static string GetSelectedPathOrFallback()
        {
            var path = string.Empty;
            foreach (var obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }
            return path;
        }
    }
}