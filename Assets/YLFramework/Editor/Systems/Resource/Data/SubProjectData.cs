using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

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
                            Folder = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/'),
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
            var owners = new[] { defaultSubProjectData }.Concat(subProjectDatas).ToArray();
            var pending = owners.ToDictionary(owner => owner, _ => new List<AssetBundleBuild>());
            foreach (var owner in subProjectDatas)
            {
                ResourceBuildOperation.ValidateOwnerName(owner.Name);
                if (string.IsNullOrEmpty(owner.Folder) || !owner.Folder.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !AssetDatabase.IsValidFolder(owner.Folder))
                    throw new InvalidOperationException("子工程必须位于 Assets 子目录内：" + owner.Path);
                ResourceBuildOperation.ValidateAssetPath(owner.Path);
            }
            for (var i = 0; i < subProjectDatas.Count; i++)
            for (var j = i + 1; j < subProjectDatas.Count; j++)
            {
                var first = subProjectDatas[i];
                var second = subProjectDatas[j];
                if (string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase) ||
                    ContainsPath(first.Folder, second.Folder) || ContainsPath(second.Folder, first.Folder))
                    throw new InvalidOperationException("子工程名称或目录归属冲突：\n" + first.Path + "\n" + second.Path);
            }
            foreach (var assetBundleName in AssetDatabase.GetAllAssetBundleNames().OrderBy(name => name, StringComparer.Ordinal))
            {
                var assetBundleBuild = new AssetBundleBuild
                {
                    assetBundleName = assetBundleName,
                    assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName)
                };
                if (assetBundleBuild.assetNames.Length == 0) continue;
                ResourceBuildOperation.ValidateBundleName(assetBundleName);
                var bundleOwners = new HashSet<SubProjectData>();
                foreach (var assetName in assetBundleBuild.assetNames)
                {
                    if (!assetName.StartsWith("Assets/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Bundle 包含非 Assets 资产：" + assetName);
                    ResourceBuildOperation.ValidateAssetPath(assetName);
                    bundleOwners.Add(subProjectDatas.SingleOrDefault(owner => ContainsPath(owner.Folder, assetName)) ?? defaultSubProjectData);
                }
                if (bundleOwners.Count != 1)
                    throw new InvalidOperationException("Bundle 跨多个 owner，构建已拒绝：" + assetBundleName + "\n" +
                                                        string.Join("\n", assetBundleBuild.assetNames));
                pending[bundleOwners.Single()].Add(assetBundleBuild);
            }
            var bundleOwnerMap = pending.SelectMany(pair => pair.Value.Select(build => new { build.assetBundleName, Owner = pair.Key }))
                .ToDictionary(item => item.assetBundleName, item => item.Owner);
            foreach (var name in bundleOwnerMap.Keys)
            {
                var parent = name;
                while (parent.Contains('/'))
                {
                    parent = parent.Substring(0, parent.LastIndexOf('/'));
                    if (bundleOwnerMap.ContainsKey(parent))
                        throw new InvalidOperationException("Bundle 文件与目录名称冲突：" + parent + "\n" + name);
                }
            }
            foreach (var pair in bundleOwnerMap)
            foreach (var dependency in AssetDatabase.GetAssetBundleDependencies(pair.Key, false))
                if (!bundleOwnerMap.TryGetValue(dependency, out var dependencyOwner) || dependencyOwner != pair.Value)
                    throw new InvalidOperationException("Bundle 依赖跨 owner，不能独立发布：" + pair.Key + " -> " + dependency);
            foreach (var owner in owners) owner.Builds = pending[owner];
        }

        private static bool ContainsPath(string folder, string path) =>
            string.Equals(folder, path, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    }
}
