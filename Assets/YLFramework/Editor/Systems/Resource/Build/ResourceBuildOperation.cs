using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    // 仅管理 YL 产物的叶目录；父目录可以共享，不能以其存在或 Git 状态推断所有权。
    internal static class ResourceBuildOperation
    {
        internal const string OwnershipFile = ".yl-resource-output.json";
        private static readonly string[] Platforms = { "Windows", "Linux", "OSX", "Android", "iOS", "WebGL", "WSAPlayer" };

        [Serializable]
        private sealed class Ownership
        {
            public int Version;
            public string Project;
            public string Owner;
            public string Platform;
            public string[] Files;
            public string SourceFingerprint;
        }

        internal sealed class Output
        {
            internal string Root;
            internal string Path;
            internal string Owner;
            internal string Platform;
            internal AssetBundleBuild[] Builds;
            internal string SourceFingerprint;
        }

        internal sealed class Plan
        {
            internal BuildTarget Target;
            internal string Platform;
            internal List<Output> Outputs = new();
            internal string Summary => "目标：" + Target + " / " + Platform + "\n\n" +
                string.Join("\n\n", Outputs.Select(output => output.Owner + "，Bundle 数：" + output.Builds.Length + "\n" + output.Path)) +
                "\n\n仅操作以上平台叶目录；保留父目录、其他平台及无关兄弟目录。构建、清理不支持 Undo。";
        }

        internal static string GetPlatformName(BuildTarget target) => target switch
        {
            BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => "Windows",
            BuildTarget.StandaloneLinux64 => "Linux",
            BuildTarget.StandaloneOSX => "OSX",
            BuildTarget.Android => "Android",
            BuildTarget.iOS => "iOS",
            BuildTarget.WebGL => "WebGL",
            BuildTarget.WSAPlayer => "WSAPlayer",
            _ => throw new NotSupportedException("YL Resource 不支持构建目标：" + target)
        };

        internal static void ValidateOwnerName(string name)
        {
            ValidateSegment(name);
            if (string.Equals(name, "AssetBundles", StringComparison.OrdinalIgnoreCase) ||
                Platforms.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("子工程名称与默认/平台输出目录冲突：" + name);
        }

        private static void ValidateSegment(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.EndsWith(".", StringComparison.Ordinal) ||
                name.EndsWith(" ", StringComparison.Ordinal) || name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)) ||
                new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                    "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
                    .Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("不安全的输出名称：" + name);
        }

        internal static void ValidateBundleName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Contains('\\') || name != name.ToLowerInvariant())
                throw new InvalidOperationException("Bundle 名必须为小写安全相对路径：" + name);
            foreach (var segment in name.Split('/')) ValidateSegment(segment);
            var first = name.Split('/')[0];
            if (name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, ResData.FileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(first, OwnershipFile, StringComparison.OrdinalIgnoreCase) ||
                Platforms.Contains(first, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Bundle 名与构建文件冲突：" + name);
        }

        internal static Plan CreatePlan(BuildTarget target, bool build)
        {
            var platform = GetPlatformName(target);
            if (build && (target != EditorUserBuildSettings.activeBuildTarget ||
                          !BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target)))
                throw new InvalidOperationException("请在 Unity Build Settings 激活并安装该目标平台后构建：" + target);
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请在非播放、非编译、非导入状态执行资源操作。");
            var defaults = new SubProjectData();
            var owners = SubProjectData.SearchAllInProject();
            SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(defaults, owners);
            var plan = new Plan { Target = target, Platform = platform };
            var bundleRoot = FullPath(ResServiceAssetsMenu.AssetBundleOutputPath);
            var streamingRoot = FullPath(Application.streamingAssetsPath);
            AddOwner(defaults, "default", bundleRoot, System.IO.Path.Combine(streamingRoot, "AssetBundles"));
            foreach (var owner in owners)
            {
                var identity = "subproject:" + AssetDatabase.AssetPathToGUID(owner.Path);
                if (identity == "subproject:") throw new InvalidOperationException("子工程资产身份无效：" + owner.Path);
                var outputContainer = System.IO.Path.Combine(bundleRoot, owner.Name);
                var streamingContainer = System.IO.Path.Combine(streamingRoot, owner.Name);
                ValidateContainer(outputContainer, identity, false);
                ValidateContainer(streamingContainer, identity, true);
                AddOwner(owner, identity, outputContainer, System.IO.Path.Combine(streamingContainer, "AssetBundles"));
            }
            ValidatePlan(plan);
            return plan;

            void AddOwner(SubProjectData owner, string identity, string outputRoot, string finalRoot)
            {
                if (build && owner.Builds.Count == 0) return;
                var builds = owner.Builds.ToArray();
                var fingerprint = GetSourceFingerprint(builds);
                foreach (var root in new[] { outputRoot, finalRoot })
                    plan.Outputs.Add(new Output { Root = root, Path = System.IO.Path.Combine(root, platform), Owner = identity,
                        Platform = platform, Builds = builds, SourceFingerprint = fingerprint });
            }
        }

        private static string FullPath(string path) => System.IO.Path.GetFullPath(path).TrimEnd('\\', '/');

        internal static void ValidateAssetPath(string path)
        {
            if (!IsWithin(Application.dataPath, path)) throw new InvalidOperationException("资产路径越界：" + path);
            RejectLinks(path);
        }

        private static string GetSourceFingerprint(AssetBundleBuild[] builds)
        {
            // DependencyHash 包含路径、源文件、meta、目标平台及 importer 版本，避免用当前标记重写旧 Bundle 的表。
            var parts = new List<string>();
            foreach (var build in builds.OrderBy(item => item.assetBundleName, StringComparer.Ordinal))
            {
                parts.Add(build.assetBundleName);
                parts.Add(build.assetBundleVariant ?? string.Empty);
                foreach (var asset in AssetDatabase.GetAssetPathsFromAssetBundle(build.assetBundleName).OrderBy(path => path, StringComparer.Ordinal))
                {
                    parts.Add(asset);
                    parts.Add(AssetDatabase.GetAssetDependencyHash(asset).ToString());
                    parts.Add(AssetDatabase.GetMainAssetTypeAtPath(asset)?.AssemblyQualifiedName ?? string.Empty);
                }
                parts.AddRange(AssetDatabase.GetAssetBundleDependencies(build.assetBundleName, false).OrderBy(name => name, StringComparer.Ordinal));
            }
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", parts))));
        }

        internal static void ValidatePublishedSources(Output output)
        {
            var record = ReadOwnership(FullPath(output.Path), output.Owner, output.Platform);
            if (string.IsNullOrEmpty(record.SourceFingerprint) || record.SourceFingerprint != GetSourceFingerprint(output.Builds))
                throw new InvalidOperationException("当前资源标记、名称、内容或依赖与已构建 Bundle 快照不一致，请重新构建；旧数据表未修改：" + output.Path);
        }

        internal static void ValidateOutput(Output output)
        {
            var root = FullPath(output.Root);
            var path = FullPath(output.Path);
            var project = FullPath(System.IO.Path.GetDirectoryName(Application.dataPath));
            if (!IsWithin(project, root) || !IsWithin(root, path) ||
                !string.Equals(path, FullPath(System.IO.Path.Combine(root, output.Platform)), StringComparison.OrdinalIgnoreCase) ||
                !Platforms.Contains(output.Platform, StringComparer.Ordinal))
                throw new InvalidOperationException("拒绝父目录、越界或非平台叶目录：" + output.Path);
            RejectLinks(path);
            if (File.Exists(path)) throw new InvalidOperationException("输出路径已被文件占用：" + path);
            if (!Directory.Exists(path))
            {
                if (File.Exists(path + ".meta")) throw new InvalidOperationException("输出路径存在未知孤立 meta，拒绝接管：" + path);
                return;
            }
            ReadOwnership(path, output.Owner, output.Platform);
        }

        private static void ValidateContainer(string path, string owner, bool streaming)
        {
            RejectLinks(path);
            if (File.Exists(path)) throw new InvalidOperationException("子工程输出容器已被文件占用：" + path);
            if (!Directory.Exists(path)) return;
            if (streaming)
            {
                foreach (var entry in Directory.GetFileSystemEntries(path))
                    if (!string.Equals(System.IO.Path.GetFileName(entry), "AssetBundles", StringComparison.Ordinal) &&
                        !(string.Equals(System.IO.Path.GetFileName(entry), "AssetBundles.meta", StringComparison.Ordinal) &&
                          Directory.Exists(System.IO.Path.Combine(path, "AssetBundles"))))
                        throw new InvalidOperationException("子工程输出容器含未知内容，不能接管：" + entry);
                path = System.IO.Path.Combine(path, "AssetBundles");
                if (!Directory.Exists(path)) return;
            }
            foreach (var entry in Directory.GetFileSystemEntries(path))
            {
                var name = System.IO.Path.GetFileName(entry);
                if (File.Exists(entry) && name.EndsWith(".meta", StringComparison.Ordinal) && Directory.Exists(entry[..^5])) continue;
                if (!Directory.Exists(entry) || !Platforms.Contains(name, StringComparer.Ordinal))
                    throw new InvalidOperationException("子工程输出容器含未知内容，不能接管：" + entry);
                ReadOwnership(entry, owner, name);
            }
        }

        private static Ownership ReadOwnership(string path, string owner, string platform)
        {
            RejectLinks(path);
            var marker = System.IO.Path.Combine(path, OwnershipFile);
            if (!File.Exists(marker)) throw new InvalidOperationException("输出目录归属未知，拒绝清理/替换：" + path);
            Ownership record;
            try { record = JsonUtility.FromJson<Ownership>(File.ReadAllText(marker)); }
            catch (ArgumentException exception) { throw new InvalidOperationException("输出归属记录损坏：" + marker, exception); }
            if (record == null || record.Version != 1 || record.Project != FullPath(Application.dataPath) ||
                record.Owner != owner || record.Platform != platform || record.Files == null)
                throw new InvalidOperationException("输出归属冲突或记录无效：" + marker);
            var files = new HashSet<string>(record.Files, StringComparer.OrdinalIgnoreCase);
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file) || file.Contains('\\') || file.Split('/').Any(segment => segment == "." || segment == ".." || segment.Length == 0) ||
                    !IsWithin(path, System.IO.Path.Combine(path, file)) || !File.Exists(System.IO.Path.Combine(path, file)))
                    throw new InvalidOperationException("产物清单无效或文件缺失：" + file + "\n" + path);
                var parent = System.IO.Path.GetDirectoryName(file)?.Replace('\\', '/');
                while (!string.IsNullOrEmpty(parent))
                {
                    directories.Add(parent);
                    parent = System.IO.Path.GetDirectoryName(parent)?.Replace('\\', '/');
                }
            }
            files.Add(OwnershipFile);
            foreach (var entry in Directory.GetFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                var relative = entry.Substring(path.Length + 1).Replace('\\', '/');
                if (Directory.Exists(entry) ? directories.Contains(relative) : files.Contains(relative) ||
                    (relative.EndsWith(".meta", StringComparison.Ordinal) && (files.Contains(relative[..^5]) || directories.Contains(relative[..^5])))) continue;
                throw new InvalidOperationException("产物目录含未登记的内容，拒绝清理/替换：" + entry);
            }
            return record;
        }

        private static bool IsWithin(string root, string path) =>
            FullPath(path).StartsWith(FullPath(root) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static void RejectLinks(string path)
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
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("拒绝含符号链接/junction 的产物：" + entry);
                    if ((attributes & FileAttributes.Directory) != 0) Walk(entry);
                }
            }
        }

        private static void ValidatePlan(Plan plan)
        {
            foreach (var output in plan.Outputs) ValidateOutput(output);
            for (var i = 0; i < plan.Outputs.Count; i++)
            for (var j = i + 1; j < plan.Outputs.Count; j++)
                if (string.Equals(FullPath(plan.Outputs[i].Path), FullPath(plan.Outputs[j].Path), StringComparison.OrdinalIgnoreCase) ||
                    IsWithin(plan.Outputs[i].Path, plan.Outputs[j].Path) || IsWithin(plan.Outputs[j].Path, plan.Outputs[i].Path))
                    throw new InvalidOperationException("输出目录重叠：" + plan.Outputs[i].Path + "\n" + plan.Outputs[j].Path);
        }

        internal static bool Execute(Plan plan, bool build, Func<string, bool> confirm,
            Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> builder)
        {
            ValidatePlan(plan);
            if (plan.Outputs.Count == 0 || (!build && plan.Outputs.All(output => !Directory.Exists(output.Path)))) return false;
            if (!confirm(plan.Summary)) return false;
            ValidatePlan(plan);
            var workspace = FullPath(System.IO.Path.Combine("Temp", "YLResourceBuild", Guid.NewGuid().ToString("N")));
            RejectLinks(workspace);
            var stages = new Dictionary<Output, string>();
            var backups = new Dictionary<Output, string>();
            var published = new List<Output>();
            var createdParents = new List<string>();
            var committed = false;
            try
            {
                Directory.CreateDirectory(workspace);
                if (build)
                {
                    for (var i = 0; i < plan.Outputs.Count; i += 2)
                    {
                        var output = plan.Outputs[i];
                        if (output.Builds.Length == 0) throw new InvalidOperationException("拒绝空构建列表。");
                        var stage = System.IO.Path.Combine(workspace, "build-" + i, plan.Platform);
                        Directory.CreateDirectory(stage);
                        var manifest = builder(stage, output.Builds, plan.Target);
                        if (!manifest) throw new InvalidOperationException("AssetBundle 构建失败，原产物未修改：" + output.Owner);
                        RejectLinks(stage);
                        var names = output.Builds.Select(item => item.assetBundleName).ToArray();
                        if (!manifest.GetAllAssetBundles().OrderBy(name => name).SequenceEqual(names.OrderBy(name => name)))
                            throw new InvalidOperationException("构建结果与预检 Bundle 列表不一致：" + output.Owner);
                        foreach (var name in names)
                            if (!File.Exists(System.IO.Path.Combine(stage, name)))
                                throw new InvalidOperationException("构建文件缺失：" + name);
                        if (output.SourceFingerprint != GetSourceFingerprint(output.Builds))
                            throw new InvalidOperationException("构建期间资源发生变化，拒绝发布：" + output.Owner);
                        AssetBundleExporter.WriteDataTable(names, stage, manifest);
                        WriteOwnership(stage, output);
                        stages.Add(output, stage);
                        var final = plan.Outputs[i + 1];
                        var copy = System.IO.Path.Combine(workspace, "copy-" + i, plan.Platform);
                        CopyDirectory(stage, copy);
                        stages.Add(final, copy);
                    }
                }
                // 构建回调可能修改文件系统，发布前再次检查所有旧输出，而不是逐 owner 删除后继续构建。
                ValidatePlan(plan);
                if (build)
                    foreach (var output in plan.Outputs)
                    {
                        if (output.SourceFingerprint != GetSourceFingerprint(output.Builds))
                            throw new InvalidOperationException("构建期间资源发生变化，拒绝发布：" + output.Owner);
                        if (Directory.Exists(output.Path)) PreserveMetadata(output.Path, stages[output]);
                    }
                for (var i = 0; i < plan.Outputs.Count; i++)
                {
                    var output = plan.Outputs[i];
                    if (Directory.Exists(output.Path))
                    {
                        var backup = System.IO.Path.Combine(workspace, "backup-" + i);
                        Directory.Move(output.Path, backup);
                        backups.Add(output, backup);
                        if (!build && File.Exists(output.Path + ".meta")) File.Move(output.Path + ".meta", backup + ".meta");
                    }
                    if (!build) continue;
                    var missing = new Stack<string>();
                    for (var parent = System.IO.Path.GetDirectoryName(output.Path); !Directory.Exists(parent); parent = System.IO.Path.GetDirectoryName(parent))
                        missing.Push(parent);
                    while (missing.Count > 0)
                    {
                        var parent = missing.Pop();
                        Directory.CreateDirectory(parent);
                        createdParents.Add(parent);
                    }
                    Directory.Move(stages[output], output.Path);
                    published.Add(output);
                }
                committed = true;
            }
            catch
            {
                // 回滚只处理本次发布的目录及本次移走的原目录，恢复失败时保留 workspace 供人工恢复。
                foreach (var output in published.AsEnumerable().Reverse()) Directory.Delete(output.Path, true);
                foreach (var pair in backups.Reverse())
                {
                    Directory.Move(pair.Value, pair.Key.Path);
                    if (File.Exists(pair.Value + ".meta")) File.Move(pair.Value + ".meta", pair.Key.Path + ".meta");
                }
                foreach (var parent in createdParents.AsEnumerable().Reverse())
                    if (!Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
                throw;
            }
            finally
            {
                if (committed || backups.Values.All(path => !Directory.Exists(path) && !File.Exists(path + ".meta")))
                {
                    try { if (Directory.Exists(workspace)) Directory.Delete(workspace, true); }
                    catch (IOException exception) { Debug.LogWarning("临时资源事务目录需手动清理：" + workspace + "\n" + exception.Message); }
                    catch (UnauthorizedAccessException exception) { Debug.LogWarning("临时资源事务目录需手动清理：" + workspace + "\n" + exception.Message); }
                }
                else Debug.LogError("资源事务恢复未完成，保留原产物备份：" + workspace);
            }
            AssetDatabase.Refresh();
            return true;
        }

        private static void WriteOwnership(string path, Output output)
        {
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Select(file => file.Substring(path.Length + 1).Replace('\\', '/')).OrderBy(file => file, StringComparer.Ordinal).ToArray();
            File.WriteAllText(System.IO.Path.Combine(path, OwnershipFile), JsonUtility.ToJson(new Ownership
                { Version = 1, Project = FullPath(Application.dataPath), Owner = output.Owner, Platform = output.Platform,
                    Files = files, SourceFingerprint = output.SourceFingerprint }, true));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(System.IO.Path.Combine(destination, directory.Substring(source.Length + 1)));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, System.IO.Path.Combine(destination, file.Substring(source.Length + 1)));
        }

        private static void PreserveMetadata(string source, string stage)
        {
            foreach (var meta in Directory.GetFiles(source, "*.meta", SearchOption.AllDirectories))
            {
                var destination = System.IO.Path.Combine(stage, meta.Substring(source.Length + 1));
                if (File.Exists(destination[..^5]) || Directory.Exists(destination[..^5])) File.Copy(meta, destination, true);
            }
        }
    }
}
