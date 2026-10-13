using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>
    /// 资源数据表导出：收集 AssetBundle 信息并保存为二进制配置文件。
    /// </summary>
    public static class AssetBundleExporter
    {
        /// <summary>
        /// 仅更新当前平台已构建、归属明确且源快照未变化的输出数据表；失败保留旧文件。
        /// </summary>
        /// <param name="abNames">该输出 owner 内要收集的 Bundle 名；null 时收集该 owner 的全部 Bundle。</param>
        /// <param name="outputPath">输出目录；null 时使用 StreamingAssets 的默认位置。</param>
        public static void BuildDataTable(string[] abNames = null, string outputPath = null)
        {
            var plan = ResourceBuildOperation.CreatePlan(EditorUserBuildSettings.activeBuildTarget, false);
            var target = Path.GetFullPath(outputPath ?? Path.Combine(Application.streamingAssetsPath, "AssetBundles", plan.Platform))
                .TrimEnd('/', '\\');
            var output = plan.Outputs.SingleOrDefault(item => string.Equals(item.Path, target, StringComparison.OrdinalIgnoreCase));
            if (output == null || !Directory.Exists(target))
                throw new InvalidOperationException("数据表只能更新当前平台已构建且归属明确的 YL 输出目录：" + target);
            ResourceBuildOperation.ValidateOutput(output);
            ResourceBuildOperation.ValidatePublishedSources(output);
            var names = abNames ?? output.Builds.Select(build => build.assetBundleName).ToArray();
            if (names.Except(output.Builds.Select(build => build.assetBundleName), StringComparer.Ordinal).Any())
                throw new InvalidOperationException("数据表包含不属于该输出 owner 的 Bundle。");
            var temporary = Path.Combine(target, ".yl-table-" + Guid.NewGuid().ToString("N"));
            try
            {
                WriteDataTable(names, temporary, null, true);
                File.Replace(temporary, Path.Combine(target, ResData.FileName), null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            AssetDatabase.Refresh();
        }

        internal static void WriteDataTable(string[] names, string path, AssetBundleManifest manifest, bool isFile = false)
        {
            var table = new ResData();
            foreach (var name in names)
            {
                var index = table.AddAssetBundleRecord(name, manifest ? manifest.GetDirectDependencies(name) : AssetDatabase.GetAssetBundleDependencies(name, false), out var group);
                foreach (var asset in AssetDatabase.GetAssetPathsFromAssetBundle(name))
                {
                    var type = AssetDatabase.GetMainAssetTypeAtPath(asset);
                    if (type == null) throw new InvalidOperationException("资源类型不可读取：" + asset);
                    group.AddAssetData(new AssetData(ConfigFileUtility.AssetPathToName(asset),
                        asset.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? ResLoadType.ABScene : ResLoadType.ABAsset,
                        index, name, type.ToCode()));
                }
            }
            var data = new ResData.SerializeData { AssetDataGroup = table.AllAssetDataGroup.Select(group => group.GetSerializeData()).ToArray() };
            var file = isFile ? path : Path.Combine(path, ResData.FileName);
            if (!BinarySerializer.Instance.SerializeBinary(file, data) ||
                BinarySerializer.Instance.DeserializeBinary(file) is not ResData.SerializeData restored ||
                restored.AssetDataGroup.Length != data.AssetDataGroup.Length)
                throw new InvalidOperationException("资源数据表写入/回读失败：" + file);
        }
    }
}
