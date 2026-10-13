using System;
using System.IO;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

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

        /// <summary>指定资产路径是否已按目录名规则标记为 AssetBundle；路径无效时返回 false。</summary>
        public static bool IsMarked(string path)
        {
            if (!IsValidAssetPath(path)) return false;
            var importer = AssetImporter.GetAtPath(path);
            return importer && string.IsNullOrEmpty(importer.assetBundleVariant) &&
                   string.Equals(importer.assetBundleName, GetMarkName(path), StringComparison.Ordinal);
        }

        /// <summary>标记或取消单个资产，保存并重新导入。两次操作互为可逆操作，不提供 importer Undo。</summary>
        public static void MarkAB(string path)
        {
            if (!IsValidAssetPath(path))
                throw new ArgumentException("只能标记 Assets 内的单个有效文件或子目录，不能标记根目录、Packages、越界或 symlink/junction 路径。", nameof(path));
            var importer = AssetImporter.GetAtPath(path);
            if (!importer || !AssetDatabase.IsOpenForEdit(path))
                throw new InvalidOperationException("资产不可编辑：" + path);
            if (!string.IsNullOrEmpty(importer.assetBundleVariant) ||
                (!string.IsNullOrEmpty(importer.assetBundleName) && !IsMarked(path)))
                throw new InvalidOperationException("资产已有其他 Bundle 标记，请先在 Inspector 中确认并处理：" + path);
            importer.assetBundleName = IsMarked(path) ? string.Empty : GetMarkName(path);
            importer.SaveAndReimport();
            ValidateMarkDirOrFile();
        }

        private static string GetMarkName(string path) => Path.GetFileName(path).Replace(".", "_").ToLowerInvariant();

        private static bool IsValidAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains('\\') || path.Split('/').Any(segment => segment == "." || segment == ".." || segment.Length == 0) ||
                !(AssetDatabase.IsValidFolder(path) || File.Exists(path)) || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return false;
            try { ResourceBuildOperation.ValidateAssetPath(path); return true; }
            catch (InvalidOperationException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        [MenuItem(AssetBundleMark)]
        public static void MarkDirOrFile()
        {
            try { MarkAB(GetSelectedPathOrFallback()); }
            catch (Exception exception) { EditorUtility.DisplayDialog("资源标记未执行", exception.Message, "关闭"); }
        }

        [MenuItem(AssetBundleMark, true)]
        private static bool ValidateMarkDirOrFile()
        {
            var path = GetSelectedPathOrFallback();
            Menu.SetChecked(AssetBundleMark, IsMarked(path));
            return IsValidAssetPath(path);
        }
        /// <summary>
        /// 获得当前选择的资产的路径
        /// </summary>
        /// <returns>当前选择中的资产路径；无有效选择时返回空字符串。</returns>
        private static string GetSelectedPathOrFallback()
        {
            var objects = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
            return objects.Length == 1 && Selection.objects.Length == 1 ? AssetDatabase.GetAssetPath(objects[0]) : string.Empty;
        }
    }
}
