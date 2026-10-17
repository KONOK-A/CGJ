#if UNITY_EDITOR

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor
{
    public static class ExportUnityPackage
    {
        private const string FrameworkRoot = "Assets/YLFramework";
        private const string PackageVersion = "1.0.0";

        // 六个目录递归导出，并显式携带根级 XF 许可；排除 Tests 与包外依赖。
        private static readonly string[] ExportRoots =
        {
            "Assets/YLFramework/Editor",
            "Assets/YLFramework/Plugins",
            "Assets/YLFramework/Prefabs",
            "Assets/YLFramework/Runtime",
            "Assets/YLFramework/Samples",
            "Assets/YLFramework/Documentation",
            "Assets/YLFramework/LICENSE.md",
        };

        /// <summary>向明确的现有本地父目录导出同一框架闭包；不保存资产、创建目录或自动覆盖已有交付。</summary>
        /// <param name="packagePath">绝对 .unitypackage 文件路径。</param>
        /// <param name="overwrite">仅在调用方已确认目标归属及覆盖时显式启用。</param>
        public static void Export(string packagePath, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !Path.IsPathFullyQualified(packagePath) ||
                !string.Equals(Path.GetExtension(packagePath), ".unitypackage", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select an explicit absolute local .unitypackage destination.", nameof(packagePath));
            var full = Path.GetFullPath(packagePath);
            if (full.StartsWith("\\\\", StringComparison.Ordinal))
                throw new ArgumentException("Network and device export paths are not supported.", nameof(packagePath));
            if (!Directory.Exists(Path.GetDirectoryName(full))) throw new DirectoryNotFoundException("The explicit export parent must already exist.");
            for (var cursor = full; cursor != null; cursor = Path.GetDirectoryName(cursor))
                if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing linked export paths.");
            if (Directory.Exists(full) || File.Exists(full) && !overwrite) throw new IOException("The export destination already exists.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Export requires an idle Editor.");
            foreach (var root in ExportRoots)
                if (!AssetDatabase.LoadMainAssetAtPath(root)) throw new FileNotFoundException("Missing framework export root.", root);
            AssetDatabase.ExportPackage(ExportRoots, full, ExportPackageOptions.Recurse);
            if (!File.Exists(full) || new FileInfo(full).Length == 0) throw new IOException("No complete framework package was produced.");
        }

        [MenuItem("YLFramework/Toolkit/导出框架包", priority = 1)] //%e 表示快捷键 ctrl + e 
        private static void MenuClicked()
        {
            if (!AssetDatabase.IsValidFolder(FrameworkRoot))
            {
                Debug.LogError("导出框架包失败：找不到框架目录 " + FrameworkRoot);
                return;
            }

            var hasMissingRoot = false;
            foreach (var exportRoot in ExportRoots)
            {
                if (!AssetDatabase.LoadMainAssetAtPath(exportRoot))
                {
                    Debug.LogError("导出框架包失败：找不到导出资源 " + exportRoot);
                    hasMissingRoot = true;
                }
            }

            if (hasMissingRoot)
            {
                return;
            }

            AssetDatabase.SaveAssets();

            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var packageName = $"YLFramework_{PackageVersion}_{DateTime.Now:yyyyMMdd_HHmmss}.unitypackage";
            var packagePath = Path.Combine(projectRoot, packageName);

            if (File.Exists(packagePath) && !Application.isBatchMode)
            {
                if (!EditorUtility.DisplayDialog("覆盖确认", "已存在同名包文件，是否覆盖？\n" + packagePath, "覆盖", "取消"))
                {
                    return;
                }
            }

            try
            {
                Export(packagePath, overwrite: true);
            }
            catch (Exception e)
            {
                Debug.LogError("导出框架包失败：" + e);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("导出失败", "导出框架包时发生异常：\n" + e.Message, "确定");
                }
                return;
            }

            if (!File.Exists(packagePath) || new FileInfo(packagePath).Length <= 0)
            {
                Debug.LogError("导出框架包失败：未生成有效的包文件 " + packagePath);
                return;
            }

            //将包名复制到剪切板
            GUIUtility.systemCopyBuffer = packageName;
            //提示
            Debug.Log("已导出框架的Unity Package：" + packagePath);
            //打开包所在目录 复用 MenuItem
            EditorApplication.ExecuteMenuItem("YLFramework/Toolkit/打开目录/Unity Package");
        }
    }
}

#endif
