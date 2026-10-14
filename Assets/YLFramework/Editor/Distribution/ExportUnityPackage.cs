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
                AssetDatabase.ExportPackage(ExportRoots, packagePath, ExportPackageOptions.Recurse);
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
