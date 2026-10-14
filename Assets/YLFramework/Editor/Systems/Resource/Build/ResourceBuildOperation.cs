using System;
using System.IO;
using System.Linq;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>沿用旧 YL 的窄路径/链接校验。旧标记构建、双目录发布及清理事务已退出，不提供第二个构建入口。</summary>
    internal static class ResourceBuildOperation
    {
        private static readonly string[] Platforms = { "Windows", "Linux", "OSX", "Android", "iOS", "WebGL", "WSAPlayer" };
        internal static void ValidateOwnerName(string name)
        {
            ValidateSegment(name);
            if (string.Equals(name, "AssetBundles", StringComparison.OrdinalIgnoreCase) || Platforms.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("子工程名称与默认/平台输出目录冲突：" + name);
        }
        private static void ValidateSegment(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.EndsWith(".", StringComparison.Ordinal) ||
                name.EndsWith(" ", StringComparison.Ordinal) || name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)) ||
                new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                    "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("不安全的输出名称：" + name);
        }
        // SubProjectData 仍服务旧 Runtime 的模拟元数据；这里只保留纯验证，不执行旧构建或改 Importer。
        internal static void ValidateBundleName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Contains('\\') || name != name.ToLowerInvariant())
                throw new InvalidOperationException("Bundle 名必须为小写安全相对路径：" + name);
            foreach (var segment in name.Split('/')) ValidateSegment(segment);
            var first = name.Split('/')[0];
            if (name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, ResData.FileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, ".yl-resource-output.json", StringComparison.OrdinalIgnoreCase) || Platforms.Contains(first, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bundle 名与构建文件冲突：" + name);
        }
        private static string FullPath(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');
        internal static void ValidateAssetPath(string path)
        {
            if (!FullPath(path).StartsWith(FullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("资产路径越界：" + path);
            RejectLinks(path);
        }
        internal static void RejectLinks(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("拒绝符号链接文件：" + path);
            for (var directory = new DirectoryInfo(FullPath(path)); directory != null; directory = directory.Parent)
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("拒绝符号链接/junction 输出路径：" + directory.FullName);
            if (!Directory.Exists(path)) return;
            Walk(path);
            void Walk(string folder)
            {
                foreach (var entry in Directory.GetFileSystemEntries(folder))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("拒绝含符号链接/junction 的产物：" + entry);
                    if ((attributes & FileAttributes.Directory) != 0) Walk(entry);
                }
            }
        }
    }
}
