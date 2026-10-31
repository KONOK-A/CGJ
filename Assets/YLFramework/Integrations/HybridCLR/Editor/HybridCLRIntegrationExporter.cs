using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    /// <summary>显式导出可选集成源码及依赖说明，不接管默认框架导出流程。</summary>
    public static class HybridCLRIntegrationExporter
    {
        private const string Root = "Assets/YLFramework/Integrations/HybridCLR";

        /// <summary>明确选择脚本/asmdef/说明，不使用递归资源依赖扩张，不分发生成制品或官方包。</summary>
        /// <param name="includeSample">是否额外选择本集成隔离 Samples 下的源码及程序集定义。</param>
        /// <returns>已检查文件、配套 .meta 与导入 GUID 的稳定去重路径集合。</returns>
        public static IReadOnlyList<string> GetExportPaths(bool includeSample = false)
        {
            var paths = new List<string> { "Assets/YLFramework/LICENSE.md", Root + "/Dependencies.json", Root + "/README.md" };
            foreach (var directory in new[] { Root + "/Runtime", Root + "/Editor" }.Concat(includeSample ? new[] { Root + "/Samples" } : Array.Empty<string>()))
            {
                HybridCLRBuildBaseline.RejectLinks(directory);
                if (!Directory.Exists(directory))
                {
                    throw new DirectoryNotFoundException(directory);
                }

                paths.AddRange(HybridCLRBuildBaseline.SafeFiles(directory).Where(p => Path.GetExtension(p) == ".cs" || Path.GetExtension(p) == ".asmdef")
                    .Select(p => p.Substring(Path.GetFullPath(".").TrimEnd('\\', '/').Length + 1).Replace('\\', '/')));
            }
            foreach (var path in paths)
            {
                HybridCLRBuildBaseline.RejectLinks(path);
                if (!File.Exists(path) || !File.Exists(path + ".meta") || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                {
                    throw new FileNotFoundException("Import all explicitly selected integration assets before export.", path);
                }
            }
            return Array.AsReadOnly(paths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray());
        }

        /// <summary>在空闲 Editor 中把明确选择的集成资产导出到本地 unitypackage。</summary>
        /// <param name="packagePath">父目录已存在的本地绝对 .unitypackage 路径，不接受网络路径或链接。</param>
        /// <param name="includeSample">是否包含隔离示例源码，不在当前工程生成示例资产。</param>
        /// <param name="overwrite">是否显式允许覆盖已有导出文件，默认拒绝。</param>
        public static void Export(string packagePath, bool includeSample = false, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !Path.IsPathFullyQualified(packagePath) ||
                packagePath.StartsWith("//", StringComparison.Ordinal) || packagePath.StartsWith("\\\\", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(packagePath), ".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Choose an explicit absolute local .unitypackage destination.", nameof(packagePath));
            }

            var full = Path.GetFullPath(packagePath);
            HybridCLRBuildBaseline.RejectLinks(full);
            if (!Directory.Exists(Path.GetDirectoryName(full)))
            {
                throw new DirectoryNotFoundException("The explicit export parent must already exist.");
            }

            if (Directory.Exists(full) || File.Exists(full) && !overwrite)
            {
                throw new IOException("The optional export destination already exists.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer || EditorUtility.scriptCompilationFailed)
            {
                throw new InvalidOperationException("Export requires an idle Editor without script compilation errors.");
            }

            AssetDatabase.ExportPackage(GetExportPaths(includeSample).ToArray(), full, ExportPackageOptions.Default);
            if (!File.Exists(full) || new FileInfo(full).Length == 0)
            {
                throw new IOException("No complete optional package was produced.");
            }
        }
    }
}
