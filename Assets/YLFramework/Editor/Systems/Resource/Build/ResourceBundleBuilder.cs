using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Resource
{
    public sealed class ResourceBuiltFile
    {
        public string Name { get; }
        public long Length { get; }
        public string Sha256 { get; }
        internal ResourceBuiltFile(ResourceBuildWorkspace.FileDigest value) { Name = value.Name; Length = value.Length; Sha256 = value.Sha256; }
    }

    public sealed class ResourceBuiltBundle
    {
        public string Name { get; }
        public string UnityHash { get; }
        public IReadOnlyList<string> Dependencies { get; }
        internal ResourceBuiltBundle(string name, string hash, string[] dependencies)
        { Name = name; UnityHash = hash; Dependencies = Array.AsReadOnly(dependencies); }
    }

    /// <summary>已在真实 Bundle 中按唯一 name+type 验证过的公开子资产；localId 只作稳定身份。</summary>
    public sealed class ResourceBuiltSubAsset
    {
        public ResourceBuildAsset MainAsset { get; }
        public long LocalId { get; }
        public string Name { get; }
        public Type AssetType { get; }
        internal ResourceBuiltSubAsset(ResourceBuildAsset main, long localId, string name, Type type)
        { MainAsset = main; LocalId = localId; Name = name; AssetType = type; }
    }

    /// <summary>已验证的 Editor 构建结果及真实工作量；Workspace 是可变缓存，不是已发布版本或持久化 Catalog。</summary>
    public sealed class ResourceBuildResult
    {
        public ResourceBuildMap Map { get; internal set; }
        public string WorkspacePath { get; internal set; }
        public string ManifestBundleFile { get; internal set; }
        public string InputFingerprint { get; internal set; }
        public string BuildSettingsFingerprint { get; internal set; }
        public string Platform { get; internal set; }
        public string UnityCompatibility { get; internal set; }
        public IReadOnlyList<ResourceBuiltFile> Files { get; internal set; }
        public IReadOnlyList<ResourceBuiltBundle> Bundles { get; internal set; }
        public IReadOnlyList<ResourceBuiltSubAsset> SubAssets { get; internal set; }
        public bool IsCacheHit { get; internal set; }
        public bool UnityBuildInvoked { get; internal set; }
        public bool ForcedRebuild { get; internal set; }
        /// <summary>无法冻结完整输入的 Editor 回调；有此类回调时禁用整体跳构建并强制刷新 native 产物。</summary>
        public IReadOnlyList<string> CacheReuseBlockedBy { get; internal set; }
        public double PreflightMilliseconds { get; internal set; }
        public double BuildMilliseconds { get; internal set; }
        public double ValidationMilliseconds { get; internal set; }
        public long SourceHashBytes { get; internal set; }
        public long ArtifactHashBytes { get; internal set; }
        public long CopiedBytes => 0;
        public int RetainedObsoleteFiles { get; internal set; }
    }

    /// <summary>主线程显式 BuildMap 构建入口。固定 LZ4+StrictMode，仅写独立 owned Workspace，不发布或写入 StreamingAssets。</summary>
    [InitializeOnLoad]
    public static class ResourceBundleBuilder
    {
        private const BuildAssetBundleOptions Options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode;
        private static readonly int MainThread;
        private static bool building;

        static ResourceBundleBuilder() => MainThread = Thread.CurrentThread.ManagedThreadId;

        /// <summary>预览兼容分区路径，不创建或认领目录；目标必须已是当前活动平台，避免隐式改动项目设置。</summary>
        public static string GetWorkspacePath(ResourceProject project, BuildTarget target)
        {
            ValidateEditor(target);
            var map = ResourceBuildMap.Create(project);
            long bytes = 0;
            return new ResourceBuildWorkspace(map, Settings(map, project, target, ref bytes), target.ToString()).Output;
        }

        internal static string PreflightWorkspace(ResourceProject project, BuildTarget target)
        {
            var map = ResourceBuildMap.Create(project);
            long bytes = 0;
            var workspace = new ResourceBuildWorkspace(map, Settings(map, project, target, ref bytes), target.ToString());
            _ = workspace.Read();
            return workspace.Output;
        }

        /// <summary>取消在 native 构建前后及验证检查点生效；Unity 同步 BuildPipeline 本身不可由 token 中断。</summary>
        public static ResourceBuildResult Build(ResourceProject project, BuildTarget target, bool forceRebuild = false,
            CancellationToken cancellation = default) => Execute(project, target, forceRebuild, cancellation, BuildPipeline.BuildAssetBundles);

        internal static ResourceBuildResult Execute(ResourceProject project, BuildTarget target, bool forceRebuild,
            CancellationToken cancellation, Func<string, AssetBundleBuild[], BuildAssetBundleOptions, BuildTarget, AssetBundleManifest> pipeline)
        {
            if (building) throw new InvalidOperationException("An AssetBundle build is already running; reentrant builds are not allowed.");
            ValidateEditor(target);
            cancellation.ThrowIfCancellationRequested();
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            building = true;
            AssetBundleManifest generated = null;
            try
            {
                var watch = Stopwatch.StartNew();
                var map = ResourceBuildMap.Create(project);
                long sourceBytes = 0;
                var settings = Settings(map, project, target, ref sourceBytes);
                var callbacks = UntrackedCallbacks();
                var workspace = new ResourceBuildWorkspace(map, settings, target.ToString());
                var record = workspace.Read();
                RejectLoadedBundles();
                var fingerprint = Fingerprint(map, settings, ref sourceBytes);
                cancellation.ThrowIfCancellationRequested();
                using var locked = workspace.AcquireLock(ref record);
                var result = new ResourceBuildResult { Map = map, WorkspacePath = workspace.Output, InputFingerprint = fingerprint,
                    BuildSettingsFingerprint = settings, Platform = target.ToString(), UnityCompatibility = Application.unityVersion,
                    CacheReuseBlockedBy = Array.AsReadOnly(callbacks) };
                var expected = workspace.ExpectedFiles();
                long artifactBytes = 0;
                var integrity = record.Complete && record.Files.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected);
                if (integrity)
                {
                    foreach (var file in record.Files)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var path = Path.Combine(workspace.Output, file.Name);
                        if (!File.Exists(path)) { integrity = false; break; }
                        var digest = HashFile(path, ref artifactBytes);
                        if (digest.Length != file.Length || digest.Sha256 != file.Sha256) { integrity = false; break; }
                    }
                }
                result.IsCacheHit = CanSkipBuild(forceRebuild, integrity, record.Fingerprint == fingerprint, callbacks);
                result.ForcedRebuild = forceRebuild || !integrity || callbacks.Length != 0;
                result.PreflightMilliseconds = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                if (!result.IsCacheHit)
                {
                    workspace.BeginAttempt(record);
                    cancellation.ThrowIfCancellationRequested();
                    result.UnityBuildInvoked = true;
                    generated = pipeline(workspace.Output, map.UnityBuilds(), Options |
                        (result.ForcedRebuild ? BuildAssetBundleOptions.ForceRebuildAssetBundle : BuildAssetBundleOptions.None), target);
                    cancellation.ThrowIfCancellationRequested();
                    if (!generated) throw new InvalidOperationException("BuildPipeline returned no AssetBundle Manifest.");
                    if (!generated.GetAllAssetBundles().OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(map.Bundles.Select(x => x.Name)))
                        throw new InvalidOperationException("BuildPipeline Manifest does not match the explicit BuildMap.");
                }
                result.BuildMilliseconds = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                workspace.ValidateContents(record);
                var built = ReadBack(map, workspace, cancellation, out var subAssets);
                result.ManifestBundleFile = workspace.Leaf;
                result.Bundles = built.AsReadOnly();
                result.SubAssets = subAssets.AsReadOnly();
                ResourceBuildWorkspace.FileDigest[] files;
                if (result.IsCacheHit) files = record.Files;
                else files = expected.Select(name =>
                {
                    cancellation.ThrowIfCancellationRequested();
                    var digest = HashFile(Path.Combine(workspace.Output, name), ref artifactBytes);
                    digest.Name = name;
                    return digest;
                }).ToArray();
                // BuildPipeline 会卸载未使用的 SO，局部变量不会保证 Unity 对象存活；按持久 GUID 重取配置后复验。
                var currentProject = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(map.ProjectGuid));
                if (!currentProject) throw new InvalidOperationException("Resource project definition was removed during the build.");
                var afterMap = ResourceBuildMap.Create(currentProject);
                var afterSettings = Settings(afterMap, currentProject, target, ref sourceBytes);
                if (afterSettings != settings || Fingerprint(afterMap, afterSettings, ref sourceBytes) != fingerprint)
                    throw new InvalidOperationException("Resource sources/configuration changed during the build; workspace remains unverified.");
                cancellation.ThrowIfCancellationRequested();
                if (!result.IsCacheHit)
                {
                    record.Files = files;
                    record.Fingerprint = fingerprint;
                    record.Complete = true;
                    workspace.Write(record);
                }
                result.Files = files.Select(x => new ResourceBuiltFile(x)).ToList().AsReadOnly();
                result.RetainedObsoleteFiles = record.KnownFiles.Except(expected, StringComparer.Ordinal).Count();
                result.SourceHashBytes = sourceBytes;
                result.ArtifactHashBytes = artifactBytes;
                result.ValidationMilliseconds = watch.Elapsed.TotalMilliseconds;
                return result;
            }
            finally
            {
                if (generated) Object.DestroyImmediate(generated);
                building = false;
            }
        }

        internal static bool CanSkipBuild(bool force, bool integrity, bool matchingInput, string[] callbacks) =>
            !force && integrity && matchingInput && callbacks.Length == 0;

        private static string[] UntrackedCallbacks()
        {
            var engine = typeof(EditorApplication).Assembly;
            var contracts = new[] { typeof(IPreprocessShaders), typeof(IPreprocessComputeShaders), typeof(IProcessSceneWithReport),
                typeof(IProcessSceneWithReport).Assembly.GetType("UnityEditor.Build.IProcessScene") };
            // Unity 自带版本固定的基础流程仍由原生依赖系统负责；包/用户回调可能读取任意配置、EditorPrefs 或外部状态。
            // 8.2a 不猜这些输入，也不提供不真实的“可缓存”开关；有此类回调就每次刷新，未来再接明确定义的输入协议。
            return contracts.Where(x => x != null).SelectMany(x => TypeCache.GetTypesDerivedFrom(x).AsEnumerable())
                .Where(x => !x.IsAbstract && !x.IsInterface && x.Assembly != engine).Select(x => x.FullName)
                .Concat(TypeCache.GetMethodsWithAttribute<UnityEditor.Callbacks.PostProcessSceneAttribute>()
                    .Where(x => x.DeclaringType != null && x.DeclaringType.Assembly != engine)
                    .Select(x => x.DeclaringType.FullName + "." + x.Name))
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        internal static void ValidateEditor(BuildTarget target)
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Resource builds require a settled Editor on its main thread, outside Play mode.");
            if (target != EditorUserBuildSettings.activeBuildTarget ||
                !BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("The build target must already be active and installed; implicit platform switching is not allowed.");
            var hostTarget = Application.platform switch
            {
                RuntimePlatform.WindowsEditor => BuildTarget.StandaloneWindows64,
                RuntimePlatform.OSXEditor => BuildTarget.StandaloneOSX,
                RuntimePlatform.LinuxEditor => BuildTarget.StandaloneLinux64,
                _ => BuildTarget.NoTarget
            };
            if (target != hostTarget)
                throw new InvalidOperationException("Native readback requires the active desktop target matching this Editor host.");
            for (var i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
                if (UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Dirty scenes must not be saved or replaced by the resource builder.");
        }

        internal static void RejectLoadedBundles()
        {
            // 实测 native build 会卸载其他项目已经加载的 Bundle；只防同 namespace 不足以保护外部所有权。
            if (AssetBundle.GetAllLoadedAssetBundles().Any(x => x))
                throw new InvalidOperationException("AssetBundles are already loaded; native building could unload externally owned resources.");
        }

        private static List<ResourceBuiltBundle> ReadBack(ResourceBuildMap map, ResourceBuildWorkspace workspace,
            CancellationToken cancellation, out List<ResourceBuiltSubAsset> subAssets)
        {
            subAssets = DescribeSubAssets(map);
            var descriptions = subAssets;
            var loaded = new List<AssetBundle>();
            try
            {
                cancellation.ThrowIfCancellationRequested();
                var root = AssetBundle.LoadFromFile(Path.Combine(workspace.Output, workspace.Leaf));
                if (!root) throw new InvalidOperationException("Cannot read the generated Manifest Bundle.");
                loaded.Add(root);
                if (!root.GetAllAssetNames().SequenceEqual(new[] { "assetbundlemanifest" }))
                    throw new InvalidOperationException("Unexpected contents in the root Manifest Bundle.");
                var manifest = root.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                if (!manifest || !manifest.GetAllAssetBundles().OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(map.Bundles.Select(x => x.Name)))
                    throw new InvalidOperationException("Persisted Manifest Bundle does not match the BuildMap.");
                var result = new List<ResourceBuiltBundle>();
                var done = new HashSet<string>(StringComparer.Ordinal);
                while (done.Count < map.Bundles.Count)
                {
                    var batch = map.Bundles.Where(x => !done.Contains(x.Name) && x.Dependencies.All(done.Contains)).ToArray();
                    if (batch.Length == 0) throw new InvalidOperationException("Bundle dependency cycle during native readback.");
                    foreach (var entry in batch)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var dependencies = manifest.GetDirectDependencies(entry.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                        if (!dependencies.SequenceEqual(entry.Dependencies))
                            throw new InvalidOperationException("Manifest dependency mismatch: " + entry.Name + " => " + string.Join(", ", dependencies));
                        var bundle = AssetBundle.LoadFromFile(Path.Combine(workspace.Output, entry.Name));
                        if (!bundle) throw new InvalidOperationException("Cannot read generated Bundle: " + entry.Name);
                        loaded.Add(bundle);
                        if (bundle.name != entry.Name) throw new InvalidOperationException("Bundle native identity mismatch: " + bundle.name);
                        var actual = entry.IsScene ? bundle.GetAllScenePaths() : bundle.GetAllAssetNames();
                        // GetAllScenePaths 保留源路径大小写，GetAllAssetNames 则小写；BuildMap 已拒绝 case-folded 路径冲突。
                        if (!actual.Select(x => x.ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(entry.AssetPaths.Select(x => x.ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal)))
                            throw new InvalidOperationException("Bundle asset/Scene contents differ from explicit BuildMap: " + entry.Name + " => " + string.Join(", ", actual));
                        foreach (var asset in map.Assets.Where(x => x.BundleName == entry.Name && !x.IsScene))
                        {
                            var value = bundle.LoadAsset(asset.SourcePath, asset.AssetType);
                            if (!value || !asset.AssetType.IsInstanceOfType(value))
                                throw new InvalidOperationException("Bundle resource identity/type readback failed: " + asset.Address);
                            foreach (var sub in descriptions.Where(x => x.MainAsset == asset))
                            {
                                var type = sub.AssetType;
                                var matches = bundle.LoadAssetWithSubAssets(asset.SourcePath, type).Where(x => x && x.name == sub.Name && type.IsInstanceOfType(x)).ToArray();
                                if (matches.Length != 1)
                                    throw new InvalidOperationException("Subasset name/type cannot uniquely locate native content: " + asset.Address + "#" + sub.LocalId.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                        result.Add(new ResourceBuiltBundle(entry.Name, manifest.GetAssetBundleHash(entry.Name).ToString(), dependencies));
                        done.Add(entry.Name);
                    }
                }
                return result.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            }
            finally
            {
                for (var i = loaded.Count - 1; i >= 0; i--) if (loaded[i]) loaded[i].Unload(true);
            }
        }

        // 构建读回与零打包模拟共用身份/歧义规则，只公开 Project view 可见、可运行时消费的子资产。
        internal static List<ResourceBuiltSubAsset> DescribeSubAssets(ResourceBuildMap map)
        {
            var result = new List<ResourceBuiltSubAsset>();
            foreach (var asset in map.Assets.Where(x => !x.IsScene))
            {
                // native 的 typed 查询包含 main 和隐藏对象，并按可赋值关系匹配；localId 不能掩盖其名称歧义。
                var candidates = AssetDatabase.LoadAllAssetsAtPath(asset.SourcePath).Where(x => x)
                    .Select(x => (x.name, Type: ResourceBuildMap.BuildType(x.GetType()))).Where(x => x.Type != null)
                    .GroupBy(x => x.name, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Select(v => v.Type).ToArray(), StringComparer.Ordinal);
                var keys = new HashSet<(string, Type)>();
                var ids = new HashSet<long> { asset.LocalId };
                foreach (var source in AssetDatabase.LoadAllAssetRepresentationsAtPath(asset.SourcePath))
                {
                    if (!source) continue;
                    var type = ResourceBuildMap.BuildType(source.GetType());
                    if (type == null || type.Assembly.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal)) continue;
                    if (!candidates.TryGetValue(source.name, out var matches) || matches.Count(type.IsAssignableFrom) != 1)
                        throw new InvalidOperationException("Subasset name/type cannot uniquely locate native content: " + asset.Address + "#" + source.name);
                    if (string.IsNullOrWhiteSpace(source.name) || !keys.Add((source.name, type)) ||
                        !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId) ||
                        guid != asset.Guid || localId == 0 || !ids.Add(localId))
                        throw new InvalidOperationException("Ambiguous subasset identity: " + asset.Address);
                    if (map.Assets.Count + result.Count >= 16384) throw new InvalidOperationException("Catalog asset limit exceeded.");
                    result.Add(new ResourceBuiltSubAsset(asset, localId, source.name, type));
                }
            }
            return result;
        }

        private static string Settings(ResourceBuildMap map, ResourceProject project, BuildTarget target, ref long bytes)
        {
            var data = new StringBuilder();
            Add(data, "ylr-build-workspace-v1");
            Add(data, Application.unityVersion);
            Add(data, target.ToString());
            Add(data, ((int)Options).ToString());
            Add(data, map.ProjectGuid);
            Add(data, map.ProjectId);
            Add(data, project.AssetRootGuid);
            foreach (var group in project.Groups.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                Add(data, group.Id);
                Add(data, group.Packing.ToString());
                Add(data, group.Selections.Count.ToString());
                foreach (var selection in group.Selections.OrderBy(x => x.Guid, StringComparer.Ordinal))
                {
                    Add(data, selection.Guid);
                    Add(data, selection.Address ?? "");
                    Add(data, (selection.Aliases?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
                    foreach (var alias in (selection.Aliases ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal)) Add(data, alias);
                    Add(data, (selection.Extensions?.Length ?? 0).ToString());
                    foreach (var extension in (selection.Extensions ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal)) Add(data, extension);
                }
            }
            // 保守隔离所有实际设置与编译输入，包括 Editor 构建回调；不尝试靠有限 PlayerSettings 字段猜缓存兼容性。
            var settingsRoot = ResourceBuildMap.Absolute("ProjectSettings");
            ResourceBuildOperation.RejectLinks(settingsRoot);
            var assemblies = CompilationPipeline.GetAssemblies();
            foreach (var assembly in assemblies.OrderBy(x => x.name, StringComparer.Ordinal))
            {
                Add(data, assembly.name);
                Add(data, assembly.flags.ToString());
                foreach (var define in assembly.defines.OrderBy(x => x, StringComparer.Ordinal)) Add(data, define);
                foreach (var reference in assembly.assemblyReferences.OrderBy(x => x.name, StringComparer.Ordinal)) Add(data, reference.name);
            }
            var generated = new[] { "Library/ScriptAssemblies/", "Library/Bee/", "Temp/" }.Select(ResourceBuildMap.Absolute).ToArray();
            var files = Directory.GetFiles(settingsRoot, "*", SearchOption.AllDirectories)
                .Concat(new[] { ResourceBuildMap.Absolute("Packages/manifest.json"), ResourceBuildMap.Absolute("Packages/packages-lock.json") })
                .Concat(assemblies.SelectMany(x => x.sourceFiles))
                .Concat(assemblies.SelectMany(x => x.compiledAssemblyReferences).Where(x => !generated.Any(p => ResourceBuildMap.Absolute(x).StartsWith(p, StringComparison.OrdinalIgnoreCase))))
                .Concat(assemblies.Select(x => CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(x.name)).Where(x => !string.IsNullOrEmpty(x)))
                .Concat(AssetDatabase.GetAllAssetPaths().Where(x => Path.GetExtension(x).Equals(".asmref", StringComparison.OrdinalIgnoreCase)));
            Add(data, InputFilesFingerprint(files, out var readBytes));
            bytes += readBytes;
            foreach (var callback in UntrackedCallbacks()) Add(data, callback);
            return HashText(data.ToString());
        }

        internal static string InputFilesFingerprint(IEnumerable<string> files, out long bytes)
        {
            bytes = 0;
            var data = new StringBuilder();
            foreach (var path in files.Select(ResourceBuildMap.Absolute).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal))
            {
                ResourceBuildOperation.RejectLinks(path);
                Add(data, path);
                Add(data, HashFile(path, ref bytes).Sha256);
                if (!File.Exists(path + ".meta")) continue;
                ResourceBuildOperation.RejectLinks(path + ".meta");
                Add(data, HashFile(path + ".meta", ref bytes).Sha256);
            }
            return HashText(data.ToString());
        }

        private static string Fingerprint(ResourceBuildMap map, string settings, ref long bytes)
        {
            var data = new StringBuilder();
            Add(data, settings);
            Add(data, map.AssetRoot);
            foreach (var asset in map.Assets)
            {
                Add(data, asset.AssetId);
                Add(data, asset.Address.ToString());
                foreach (var alias in asset.Aliases) Add(data, alias.ToString());
                Add(data, asset.AssetType.AssemblyQualifiedName);
                Add(data, asset.BundleName);
            }
            foreach (var bundle in map.Bundles)
            {
                Add(data, bundle.Name);
                foreach (var path in bundle.AssetPaths) Add(data, path);
                foreach (var dependency in bundle.Dependencies) Add(data, dependency);
            }
            foreach (var group in map.Groups) { Add(data, group.Key); foreach (var member in group.Value) Add(data, member); }
            foreach (var path in map.FingerprintPaths)
            {
                Add(data, path);
                Add(data, AssetDatabase.GetAssetDependencyHash(path).ToString());
                var full = ResourceBuildMap.Absolute(path);
                ResourceBuildOperation.RejectLinks(full);
                Add(data, HashFile(full, ref bytes).Sha256);
                if (File.Exists(full + ".meta")) Add(data, HashFile(full + ".meta", ref bytes).Sha256);
            }
            return HashText(data.ToString());
        }

        private static void Add(StringBuilder builder, string value) => builder.Append(value.Length).Append(':').Append(value).Append('\n');
        private static string HashText(string value)
        {
            using var hash = SHA256.Create();
            return Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }
        private static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
        private static ResourceBuildWorkspace.FileDigest HashFile(string path, ref long bytes)
        {
            using var file = File.OpenRead(path);
            using var hash = SHA256.Create();
            var length = file.Length;
            var digest = Hex(hash.ComputeHash(file));
            bytes += length;
            return new ResourceBuildWorkspace.FileDigest { Length = length, Sha256 = digest };
        }
    }
}
