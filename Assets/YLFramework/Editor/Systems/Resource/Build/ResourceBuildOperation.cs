using System;
using System.IO;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    /// <summary>正式构建、发布与生成共用的窄路径/链接校验，不提供另一个构建入口。</summary>
    internal static class ResourceBuildOperation
    {
        private static string FullPath(string path) => Path.GetFullPath(path).TrimEnd('\\', '/');
        internal static void ValidateAssetPath(string path)
        {
            if (!FullPath(path).StartsWith(FullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("资产路径越界：" + path);
            }

            RejectLinks(path);
        }
        internal static void RejectLinks(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("拒绝符号链接文件：" + path);
            }

            for (var directory = new DirectoryInfo(FullPath(path)); directory != null; directory = directory.Parent)
            {
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("拒绝符号链接/junction 输出路径：" + directory.FullName);
                }
            }

            if (!Directory.Exists(path))
            {
                return;
            }

            Walk(path);
            void Walk(string folder)
            {
                foreach (var entry in Directory.GetFileSystemEntries(folder))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException("拒绝含符号链接/junction 的产物：" + entry);
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        Walk(entry);
                    }
                }
            }
        }
    }
}
