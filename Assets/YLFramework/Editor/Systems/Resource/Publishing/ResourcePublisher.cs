using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    public sealed class ResourceRelease
    {
        public string RootPath { get; }
        public string DirectoryPath { get; }
        public ResourceCatalogDocument Catalog { get; }
        public bool IsRepeat { get; }
        public long CopiedBytes { get; }
        internal ResourceRelease(string root, ResourceCatalogDocument catalog, bool repeat = false, long copied = 0)
        { RootPath = root; DirectoryPath = Path.Combine(root, catalog.CatalogDigest); Catalog = catalog; IsRepeat = repeat; CopiedBytes = copied; }
    }

    /// <summary>本地/内部受控发布。只新增不可变版本，最后原子替换 Index；不写 StreamingAssets，不删除任何版本。</summary>
    [InitializeOnLoad]
    public static class ResourcePublisher
    {
        private const string OwnerFile = ".ylr-release-owner.json";
        private const string LockFile = ".ylr-release.lock";
        private const string JournalFile = ".ylr-publish-journal.json";
        private const string IndexFile = "release-index.json";
        private const string CatalogFile = "resource-catalog.json";
        private const string CompleteFile = ".ylr-complete.json";
        private const string Staging = ".staging";
        private static readonly int MainThread;
        private static bool publishing;
        private static readonly JsonSerializerSettings JsonSettings = new()
        { TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 32, Culture = CultureInfo.InvariantCulture };

        private sealed class Owner
        {
            public int Version = 1;
            public string ProjectRoot;
            public string ProjectGuid;
            public string ProjectId;
            public string Platform;
            public string[] KnownVersions = Array.Empty<string>();
        }
        private sealed class Journal
        {
            public int Version = 1;
            public string ProjectGuid;
            public string ProjectId;
            public string Platform;
            public string Attempt;
            public JObject From;
            public JObject To;
            public string[] Files;
        }
        private sealed class Complete
        {
            public int Version = 1;
            public string ProjectGuid;
            public string ProjectId;
            public string Platform;
            public string BuildId;
            public string CatalogDigest;
        }

        static ResourcePublisher() => MainThread = Thread.CurrentThread.ManagedThreadId;

        /// <summary>只返回预期输出，不创建或认领目录。</summary>
        public static string GetReleaseRoot(ResourceBuildResult result)
        {
            if (result?.Map == null) throw new ArgumentNullException(nameof(result));
            return Root(result.Map.ProjectId, result.Platform);
        }

        internal static string Preflight(ResourceProject project, BuildTarget target)
        {
            ValidateEditor();
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("A saved ResourceProject is required.");
            var root = Root(project.ProjectId, target.ToString());
            var owner = ReadOwner(root, guid, project.ProjectId, target.ToString());
            if (owner != null)
            {
                ValidateRoot(root, owner);
                var index = ReadIndex(root, owner);
                if (index != null) VerifyVersion(root, owner, index);
            }
            return root;
        }

        /// <summary>只读取得当前 owned 发布，不执行 Recover 的提交点恢复或临时目录清理。</summary>
        public static ResourceRelease ReadCurrent(ResourceProject project, BuildTarget target, string appContentCompatibilityId)
        {
            ValidateEditor();
            if (project && EditorUtility.IsDirty(project)) throw new InvalidOperationException("发布集合候选定义未保存，拒绝按临时身份遗漏项目：" + AssetDatabase.GetAssetPath(project));
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("A saved ResourceProject is required.");
            var root = Root(project.ProjectId, target.ToString());
            var owner = ReadOwner(root, guid, project.ProjectId, target.ToString());
            if (owner == null) return null;
            ValidateRoot(root, owner);
            var index = ReadIndex(root, owner);
            if (index == null) return null;
            if (index.UnityCompatibility != Application.unityVersion || index.AppContentCompatibilityId != appContentCompatibilityId)
                throw new InvalidOperationException("Published Project has different Unity/App compatibility: " + project.ProjectId);
            return new ResourceRelease(root, VerifyVersion(root, owner, index));
        }

        /// <summary>取消只在 Index 提交前生效。提交后的 IO 故障可能使调用结果未定，Recover 以实际 Index 确认完整快照。</summary>
        public static ResourceRelease Publish(ResourceBuildResult result, string appContentCompatibilityId, CancellationToken cancellation = default) =>
            Execute(result, appContentCompatibilityId, cancellation, null);

        internal static ResourceRelease Execute(ResourceBuildResult result, string app, CancellationToken cancellation, Action<string> checkpoint)
        {
            ValidateEditor();
            if (publishing) throw new InvalidOperationException("A resource publication is already running.");
            cancellation.ThrowIfCancellationRequested();
            publishing = true;
            try
            {
                using var workspaceLock = ResourceCatalogBuilder.LockWorkspace(result);
                var catalog = ResourceCatalogBuilder.CreateLocked(result, app);
                var root = GetReleaseRoot(result);
                var owner = ReadOwner(root, result.Map.ProjectGuid, result.Map.ProjectId, result.Platform);
                using var releaseLock = Acquire(root, ref owner, result.Map.ProjectGuid, result.Map.ProjectId, result.Platform);
                RecoverLocked(root, owner);
                var current = ReadIndex(root, owner);
                if (current != null) VerifyVersion(root, owner, current);
                var target = ResourceReleaseIndex.Create(catalog);
                var directory = Path.Combine(root, target.CatalogDigest);
                var existing = Directory.Exists(directory);
                if (existing) VerifyVersion(root, owner, target);
                if (current?.CatalogDigest == target.CatalogDigest) return new ResourceRelease(root, catalog, true);
                cancellation.ThrowIfCancellationRequested();
                var journal = new Journal
                {
                    ProjectGuid = owner.ProjectGuid, ProjectId = owner.ProjectId, Platform = owner.Platform,
                    Attempt = Guid.NewGuid().ToString("N"), From = current == null ? null : Parse(current.Write()), To = Parse(target.Write()),
                    Files = AllFiles(catalog).Select(x => x.RelativeFile).Append(CatalogFile).Append(CompleteFile).OrderBy(x => x, StringComparer.Ordinal).ToArray()
                };
                AtomicWrite(root, JournalFile, Metadata(journal));
                var stage = Path.Combine(root, Staging, journal.Attempt);
                long copied = 0;
                void Step(string name) { checkpoint?.Invoke(name); cancellation.ThrowIfCancellationRequested(); ValidateRoot(root, owner, journal); }
                if (!existing)
                {
                    Directory.CreateDirectory(stage);
                    Step("StagingCreated");
                    foreach (var file in result.Files)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        ResourceBuildOperation.RejectLinks(result.WorkspacePath);
                        using (var source = File.Open(Path.Combine(result.WorkspacePath, file.Name), FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var output = File.Open(Path.Combine(stage, ResourceCatalogBuilder.PublishedFile(result, file.Name)), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { source.CopyTo(output); output.Flush(true); copied += source.Length; }
                        Step("FileCopied");
                    }
                    WriteNew(Path.Combine(stage, CatalogFile), ResourceCatalogCodec.Write(catalog));
                    Step("CatalogWritten");
                    VerifyFiles(stage, catalog, false);
                    VerifyNative(stage, catalog, result, cancellation);
                    WriteNew(Path.Combine(stage, CompleteFile), Metadata(new Complete { ProjectGuid = owner.ProjectGuid,
                        ProjectId = owner.ProjectId, Platform = owner.Platform, BuildId = catalog.BuildId, CatalogDigest = catalog.CatalogDigest }));
                    VerifyComplete(stage, owner, target);
                    Step("StageVerified");
                    owner.KnownVersions = owner.KnownVersions.Append(target.CatalogDigest).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    if (owner.KnownVersions.Length > 1024) throw new InvalidOperationException("Owned release version registration limit exceeded.");
                    AtomicWrite(root, OwnerFile, Metadata(owner));
                    VerifyComplete(stage, owner, target);
                    Directory.Move(stage, directory);
                }
                Step("VersionInstalled");
                VerifyVersion(root, owner, target);
                Step("BeforeIndex");
                // 回调/外部受控工具也不能在提交前改掉基线；不以目录里的“最新”猜测授权。
                if (!SameIndex(ReadIndex(root, owner), current)) throw new InvalidOperationException("Release Index changed during publication.");
                if (current != null) VerifyVersion(root, owner, current);
                VerifyVersion(root, owner, target);
                ValidateRoot(root, owner, journal);
                cancellation.ThrowIfCancellationRequested();
                AtomicWrite(root, IndexFile, target.Write());
                // 这是提交点：之后不再处理 cancellation。故障留 journal，恢复按实际 From/To，而不是无条件降级。
                checkpoint?.Invoke("IndexCommitted");
                RecoverLocked(root, owner);
                return new ResourceRelease(root, catalog, false, copied);
            }
            finally { publishing = false; }
        }

        /// <summary>显式恢复 owned 本地事务。只认 journal 的 From/To 与完整快照，不激活 Runtime、不挑最新目录。</summary>
        public static ResourceRelease Recover(ResourceProject project, BuildTarget target, string appContentCompatibilityId)
        {
            ValidateEditor();
            if (publishing) throw new InvalidOperationException("A resource publication is already running.");
            if (!project) throw new ArgumentNullException(nameof(project));
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("A saved ResourceProject is required.");
            var root = Root(project.ProjectId, target.ToString());
            var owner = ReadOwner(root, guid, project.ProjectId, target.ToString());
            if (owner == null) return null;
            using var locked = Acquire(root, ref owner, guid, project.ProjectId, target.ToString());
            RecoverLocked(root, owner);
            var index = ReadIndex(root, owner);
            if (index == null) return null;
            var catalog = VerifyVersion(root, owner, index);
            if (catalog.UnityCompatibility != Application.unityVersion || catalog.AppContentCompatibilityId != appContentCompatibilityId)
                throw new InvalidDataException("Current published release is not compatible with the explicit recovery configuration.");
            return new ResourceRelease(root, catalog);
        }

        private static void RecoverLocked(string root, Owner owner)
        {
            ValidateRoot(root, owner);
            var journal = ReadJournal(root, owner);
            if (journal == null) return;
            var from = JournalIndex(journal.From, owner);
            var to = JournalIndex(journal.To, owner);
            var current = ReadIndex(root, owner);
            if (!SameIndex(current, from) && !SameIndex(current, to))
                throw new InvalidOperationException("Journal has a stale/conflicting base; refusing to overwrite the current Release Index.");
            if (current != null) VerifyVersion(root, owner, current);
            // From 仍有效即中止；To 已提交即确认成功。完整但未被索引的新版本也保留，不删除已安装目录。
            var stage = Path.Combine(root, Staging, journal.Attempt);
            if (Directory.Exists(stage))
            {
                ValidateRoot(root, owner, journal);
                foreach (var file in journal.Files)
                {
                    var path = Path.Combine(stage, file);
                    if (File.Exists(path)) File.Delete(path);
                }
                Directory.Delete(stage, false);
            }
            ValidateRoot(root, owner, journal);
            File.Delete(Path.Combine(root, JournalFile));
            if (File.Exists(Path.Combine(root, JournalFile + ".next"))) File.Delete(Path.Combine(root, JournalFile + ".next"));
        }

        private static FileStream Acquire(string root, ref Owner owner, string guid, string project, string platform)
        {
            var creating = owner == null;
            if (creating) Directory.CreateDirectory(root);
            var locked = File.Open(Path.Combine(root, LockFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (creating && !File.Exists(Path.Combine(root, OwnerFile)))
                {
                    if (Directory.GetFileSystemEntries(root).Any(x => Path.GetFileName(x) != LockFile))
                        throw new InvalidOperationException("Unknown content appeared before output ownership registration.");
                    owner = new Owner { ProjectRoot = ResourceBuildMap.Absolute("."), ProjectGuid = guid, ProjectId = project, Platform = platform };
                    AtomicWrite(root, OwnerFile, Metadata(owner));
                }
                else owner = ReadOwner(root, guid, project, platform);
                ValidateRoot(root, owner);
                return locked;
            }
            catch { locked.Dispose(); throw; }
        }

        private static Owner ReadOwner(string root, string guid, string project, string platform)
        {
            ResourceBuildOperation.RejectLinks(root);
            if (File.Exists(root) || File.Exists(root + ".meta")) throw new InvalidOperationException("Release root is occupied by a file or orphan meta.");
            if (!Directory.Exists(root)) return null;
            if (!File.Exists(Path.Combine(root, OwnerFile))) throw new InvalidOperationException("Unknown release output; refusing adoption.");
            var owner = ReadMetadata<Owner>(Path.Combine(root, OwnerFile), "Version,ProjectRoot,ProjectGuid,ProjectId,Platform,KnownVersions");
            if (owner.Version != 1 || owner.ProjectRoot != ResourceBuildMap.Absolute(".") || owner.ProjectGuid != guid || owner.ProjectId != project || owner.Platform != platform ||
                owner.KnownVersions == null || owner.KnownVersions.Length > 1024 || owner.KnownVersions.Distinct(StringComparer.Ordinal).Count() != owner.KnownVersions.Length)
                throw new InvalidOperationException("Release output ownership conflict.");
            foreach (var digest in owner.KnownVersions) Hex(digest, 64);
            ValidateRoot(root, owner);
            return owner;
        }

        private static void ValidateRoot(string root, Owner owner, Journal expectedJournal = null)
        {
            ResourceBuildOperation.RejectLinks(root);
            if (owner == null || !File.Exists(Path.Combine(root, OwnerFile)))
                throw new InvalidOperationException("Release ownership was removed during the transaction.");
            var liveOwner = ReadMetadata<Owner>(Path.Combine(root, OwnerFile), "Version,ProjectRoot,ProjectGuid,ProjectId,Platform,KnownVersions");
            if (!Metadata(liveOwner).SequenceEqual(Metadata(owner)))
                throw new InvalidOperationException("Release ownership/version registration changed during the transaction.");
            var journal = ReadJournal(root, owner);
            if (expectedJournal != null && (journal == null || !Metadata(journal).SequenceEqual(Metadata(expectedJournal))))
                throw new InvalidOperationException("Publication journal no longer authorizes this attempt.");
            var controls = new[] { OwnerFile, OwnerFile + ".next", LockFile, JournalFile, JournalFile + ".next", IndexFile, IndexFile + ".next" };
            foreach (var path in Directory.GetFileSystemEntries(root))
            {
                var name = Path.GetFileName(path);
                if (File.Exists(path) && controls.Contains(name, StringComparer.Ordinal)) continue;
                if (Directory.Exists(path) && name == Staging)
                {
                    foreach (var entry in Directory.GetFileSystemEntries(path))
                    {
                        if (journal == null || !Directory.Exists(entry) || Path.GetFileName(entry) != journal.Attempt)
                            throw new InvalidOperationException("Unregistered staging content: " + entry);
                        foreach (var file in Directory.GetFileSystemEntries(entry))
                            if (!File.Exists(file) || !journal.Files.Contains(Path.GetFileName(file), StringComparer.Ordinal))
                                throw new InvalidOperationException("Unregistered stage file/directory: " + file);
                    }
                    continue;
                }
                if (Directory.Exists(path) && owner.KnownVersions.Contains(name, StringComparer.Ordinal))
                {
                    VerifyComplete(path, owner, null, false);
                    continue;
                }
                throw new InvalidOperationException("Unknown release content: " + path);
            }
        }

        private static Journal ReadJournal(string root, Owner owner)
        {
            var path = Path.Combine(root, JournalFile);
            if (!File.Exists(path)) return null;
            var journal = ReadMetadata<Journal>(path, "Version,ProjectGuid,ProjectId,Platform,Attempt,From,To,Files");
            if (journal.Version != 1 || journal.ProjectGuid != owner.ProjectGuid || journal.ProjectId != owner.ProjectId || journal.Platform != owner.Platform)
                throw new InvalidOperationException("Publication journal ownership conflict.");
            Hex(journal.Attempt, 32);
            if (JournalIndex(journal.To, owner) == null || journal.Files == null || journal.Files.Length == 0 || journal.Files.Length > 65540 ||
                journal.Files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != journal.Files.Length || !journal.Files.Contains(CatalogFile) || !journal.Files.Contains(CompleteFile))
                throw new InvalidDataException("Invalid publication journal file registration.");
            JournalIndex(journal.From, owner);
            foreach (var file in journal.Files)
            {
                if (file == CatalogFile || file == CompleteFile) continue;
                var native = file.EndsWith(".manifest", StringComparison.Ordinal) ? file.Substring(0, file.Length - 9) : file;
                if (native == "manifest") continue;
                if (!native.StartsWith("b_", StringComparison.Ordinal)) throw new InvalidDataException("Unsafe publication journal file.");
                Hex(native.Substring(2), 32);
            }
            return journal;
        }

        private static ResourceReleaseIndex ReadIndex(string root, Owner owner) => !File.Exists(Path.Combine(root, IndexFile)) ? null :
            JournalIndex(Parse(ReadBounded(Path.Combine(root, IndexFile))), owner);

        private static ResourceReleaseIndex JournalIndex(JObject data, Owner owner)
        {
            if (data == null) return null;
            return ResourceReleaseIndex.Read(Canonical(data), owner.ProjectId, owner.Platform,
                (string)data["UnityCompatibility"], (string)data["AppContentCompatibilityId"]);
        }
        private static bool SameIndex(ResourceReleaseIndex left, ResourceReleaseIndex right) => left == null ? right == null :
            right != null && left.Write().SequenceEqual(right.Write());

        private static ResourceCatalogDocument VerifyVersion(string root, Owner owner, ResourceReleaseIndex index)
        {
            if (!owner.KnownVersions.Contains(index.CatalogDigest, StringComparer.Ordinal)) throw new InvalidOperationException("Release Index references an unowned version.");
            return VerifyComplete(Path.Combine(root, index.CatalogDigest), owner, index);
        }

        private static ResourceCatalogDocument VerifyComplete(string directory, Owner owner, ResourceReleaseIndex expected, bool hashFiles = true)
        {
            ResourceBuildOperation.RejectLinks(directory);
            if (!Directory.Exists(directory)) throw new InvalidDataException("Published version directory is missing.");
            var complete = ReadMetadata<Complete>(Path.Combine(directory, CompleteFile), "Version,ProjectGuid,ProjectId,Platform,BuildId,CatalogDigest");
            if (complete.Version != 1 || complete.ProjectGuid != owner.ProjectGuid || complete.ProjectId != owner.ProjectId || complete.Platform != owner.Platform)
                throw new InvalidOperationException("Published snapshot ownership conflict.");
            Hex(complete.BuildId, 64); Hex(complete.CatalogDigest, 64);
            if (Path.GetFileName(directory) != complete.CatalogDigest && Path.GetFileName(Path.GetDirectoryName(directory)) != Staging)
                throw new InvalidDataException("Version directory does not match CatalogDigest.");
            var bytes = ReadBounded(Path.Combine(directory, CatalogFile));
            if (ResourceCatalogCodec.Digest(bytes) != complete.CatalogDigest) throw new InvalidDataException("CatalogDigest mismatch.");
            var header = Parse(bytes);
            var catalog = ResourceCatalogCodec.Read(bytes, owner.ProjectId, owner.Platform,
                (string)header["UnityCompatibility"], (string)header["AppContentCompatibilityId"]);
            if (catalog.BuildId != complete.BuildId || expected != null && !expected.Write().SequenceEqual(ResourceReleaseIndex.Create(catalog).Write()))
                throw new InvalidDataException("Complete/Catalog/Release Index identity mismatch.");
            VerifyFiles(directory, catalog, true, hashFiles);
            return catalog;
        }

        private static IEnumerable<ResourceCatalogFile> AllFiles(ResourceCatalogDocument catalog) => catalog.Bundles.Cast<ResourceCatalogFile>().Concat(catalog.SupportFiles);

        private static void VerifyFiles(string directory, ResourceCatalogDocument catalog, bool complete, bool hashFiles = true)
        {
            var expected = AllFiles(catalog).Select(x => x.RelativeFile).Append(CatalogFile).ToList();
            if (complete) expected.Add(CompleteFile);
            if (!Directory.GetFileSystemEntries(directory).All(File.Exists) || !Directory.GetFiles(directory).Select(Path.GetFileName)
                .OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidOperationException("Snapshot contains missing or unregistered files/directories.");
            if (ResourceCatalogCodec.Digest(ReadBounded(Path.Combine(directory, CatalogFile))) != catalog.CatalogDigest)
                throw new InvalidDataException("Staged Catalog changed during publication.");
            if (!hashFiles) return;
            foreach (var file in AllFiles(catalog))
            {
                using var stream = File.OpenRead(Path.Combine(directory, file.RelativeFile));
                using var sha = SHA256.Create();
                if (stream.Length != file.Length || BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != file.Sha256)
                    throw new InvalidDataException("Snapshot file integrity mismatch: " + file.RelativeFile);
            }
        }

        private static void VerifyNative(string directory, ResourceCatalogDocument catalog, ResourceBuildResult result, CancellationToken cancellation)
        {
            if (AssetBundle.GetAllLoadedAssetBundles().Any(x => x)) throw new InvalidOperationException("Native publication readback requires no externally loaded Bundles.");
            var loaded = new List<AssetBundle>();
            var bundles = new Dictionary<string, AssetBundle>(StringComparer.Ordinal);
            try
            {
                var root = AssetBundle.LoadFromFile(Path.Combine(directory, catalog.ManifestFile));
                if (!root) throw new InvalidDataException("Cannot read staged Manifest Bundle.");
                loaded.Add(root);
                var manifest = root.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                if (!manifest || !manifest.GetAllAssetBundles().OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(catalog.Bundles.Select(x => x.LogicalId)))
                    throw new InvalidDataException("Catalog/Manifest Bundle set mismatch.");
                while (bundles.Count != catalog.Bundles.Count)
                {
                    var batch = catalog.Bundles.Where(x => !bundles.ContainsKey(x.LogicalId) && x.Dependencies.All(bundles.ContainsKey)).ToArray();
                    if (batch.Length == 0) throw new InvalidDataException("Catalog Bundle dependency cycle.");
                    foreach (var entry in batch)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (manifest.GetAssetBundleHash(entry.NativeName).ToString() != entry.UnityHash ||
                            !manifest.GetDirectDependencies(entry.NativeName).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(entry.Dependencies))
                            throw new InvalidDataException("Catalog/Manifest dependency/hash mismatch.");
                        var bundle = AssetBundle.LoadFromFile(Path.Combine(directory, entry.RelativeFile));
                        if (!bundle) throw new InvalidDataException("Cannot read staged Bundle: " + entry.RelativeFile);
                        loaded.Add(bundle); bundles.Add(entry.LogicalId, bundle);
                        if (bundle.name != entry.NativeName) throw new InvalidDataException("Staged native Bundle identity mismatch.");
                        var paths = entry.IsScene ? bundle.GetAllScenePaths() : bundle.GetAllAssetNames();
                        if (!paths.Select(x => x.ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(
                            result.Map.Bundles.Single(x => x.Name == entry.LogicalId).AssetPaths.Select(x => x.ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal)))
                            throw new InvalidDataException("Staged Bundle file contents mismatch.");
                        foreach (var asset in result.Map.Assets.Where(x => x.BundleName == entry.LogicalId && !x.IsScene))
                        {
                            var value = bundle.LoadAsset(asset.SourcePath, asset.AssetType);
                            if (!value || !asset.AssetType.IsInstanceOfType(value)) throw new InvalidDataException("Staged main asset type mismatch.");
                        }
                        foreach (var sub in result.SubAssets.Where(x => x.MainAsset.BundleName == entry.LogicalId))
                            if (bundle.LoadAssetWithSubAssets(sub.MainAsset.SourcePath, sub.AssetType).Count(x => x && x.name == sub.Name && sub.AssetType.IsInstanceOfType(x)) != 1)
                                throw new InvalidDataException("Staged subasset is ambiguous/missing.");
                    }
                }
            }
            finally { for (var i = loaded.Count - 1; i >= 0; i--) if (loaded[i]) loaded[i].Unload(true); }
        }

        private static string Root(string project, string platform)
        {
            ResourceAddress.ValidateId(project, nameof(project));
            var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (project.Length > 128 || project.EndsWith(".", StringComparison.Ordinal) || reserved.Contains(project.Split('.')[0], StringComparer.OrdinalIgnoreCase) ||
                !new[] { "StandaloneWindows64", "StandaloneOSX", "StandaloneLinux64" }.Contains(platform, StringComparer.Ordinal))
                throw new InvalidOperationException("Unsafe release ProjectId or unsupported platform.");
            var root = ResourceBuildMap.Absolute("AssetBundles/YLFramework/" + project + "/" + platform);
            if (Path.DirectorySeparatorChar == '\\' && Path.Combine(root, new string('0', 64), "b_" + new string('0', 32) + ".manifest").Length >= 260)
                throw new InvalidOperationException("Release paths exceed this Unity Mono Windows file API limit; use a shorter project root/ProjectId.");
            return root;
        }
        private static void ValidateEditor()
        {
            if (Thread.CurrentThread.ManagedThreadId != MainThread || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Publication requires a settled Editor main thread outside Play mode.");
        }
        private static void Hex(string value, int count)
        {
            if (value == null || value.Length != count || value.Any(x => !(x >= '0' && x <= '9' || x >= 'a' && x <= 'f')))
                throw new InvalidDataException("Invalid canonical publication identity/token.");
        }
        private static byte[] ReadBounded(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > ResourceCatalogCodec.MaximumBytes) throw new InvalidDataException("Missing or oversized publication metadata: " + path);
            return File.ReadAllBytes(path);
        }
        private static JObject Parse(byte[] bytes)
        {
            try
            {
                if (bytes.Length == 0 || bytes.Length > ResourceCatalogCodec.MaximumBytes) throw new InvalidDataException("Publication metadata limit exceeded.");
                using var input = new StringReader(new UTF8Encoding(false, true).GetString(bytes));
                using var reader = new JsonTextReader(input) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
                var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                if (reader.Read()) throw new InvalidDataException("Additional publication metadata content.");
                if (!bytes.SequenceEqual(Canonical(data))) throw new InvalidDataException("Noncanonical publication metadata.");
                return data;
            }
            catch (Exception error) when (error is JsonException || error is DecoderFallbackException)
            { throw new InvalidDataException("Invalid publication metadata.", error); }
        }
        private static T ReadMetadata<T>(string path, string fields)
        {
            var data = Parse(ReadBounded(path));
            if (!data.Properties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(fields.Split(',').OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidDataException("Unknown/missing publication metadata fields.");
            try { return data.ToObject<T>(JsonSerializer.Create(JsonSettings)); }
            catch (JsonException error) { throw new InvalidDataException("Invalid publication metadata field types.", error); }
        }
        private static byte[] Metadata(object value) => Canonical(JObject.FromObject(value, JsonSerializer.Create(JsonSettings)));
        private static byte[] Canonical(JToken value)
        {
            JToken Order(JToken token) => token switch
            {
                JObject obj => new JObject(obj.Properties().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => new JProperty(x.Name, Order(x.Value)))),
                JArray array => new JArray(array.Select(Order)),
                _ => token.DeepClone()
            };
            var builder = new StringBuilder();
            using (var writer = new JsonTextWriter(new StringWriter(builder, CultureInfo.InvariantCulture))
                { Formatting = Formatting.None, StringEscapeHandling = StringEscapeHandling.EscapeNonAscii }) Order(value).WriteTo(writer);
            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            if (bytes.Length > ResourceCatalogCodec.MaximumBytes) throw new InvalidDataException("Publication metadata limit exceeded.");
            return bytes;
        }
        private static void WriteNew(string path, byte[] bytes)
        {
            using var stream = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
        private static void AtomicWrite(string root, string name, byte[] bytes)
        {
            var path = Path.Combine(root, name);
            var next = path + ".next";
            using (var stream = File.Open(next, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(next, path, null);
            else File.Move(next, path);
        }
    }
}
