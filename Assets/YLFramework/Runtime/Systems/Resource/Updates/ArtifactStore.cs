using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 本地制品的唯一写入/归属边界。版本只新增，发布源只读；目录摘要不代表生产签名授权。
    /// 同一 store 的显式项目集合是事务闭包，任何启动和提交都整体恢复，不独立挑选各项目新旧指针。
    /// </summary>
    public sealed partial class ArtifactStore
    {
        private const string OwnerFile = ".ylr-store-owner.json";
        private const string VersionFile = ".ylr-version.json";
        private const string JournalFile = "commit-journal.json";
        private readonly Dictionary<string, ResourceProjectMount> mounts;
        private readonly Dictionary<string, ResourceLocalSnapshot> verified = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> recoveryNotes = new(StringComparer.Ordinal);
        private string ownerId;
        private int operationState;
        private bool operating => Volatile.Read(ref operationState) != 0;
        private bool creating;
        private CancellationToken operationCancellation;
        private readonly string unityCompatibility = Application.unityVersion;
        // 仅用于使分段只读观测失效，不是版本索引；任一 Store 事务都保守地要求重试。
        private static long activitySerial;
        private readonly int mainThreadId = Thread.CurrentThread.ManagedThreadId;
        internal Action<string> Checkpoint;
        public string RootPath { get; }
        public IReadOnlyList<ResourceProjectMount> Projects { get; }
        public IReadOnlyList<string> RecoveryNotes => ResourceCatalog.Copy(recoveryNotes.Values);
        public static string DefaultRoot => Path.Combine(Application.persistentDataPath, "YLFramework", "Resource");

        public ArtifactStore(string root, IEnumerable<ResourceProjectMount> projects)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw new ArgumentException("An absolute owned root is required.", nameof(root));
            RootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (var protectedRoot in new[] { Application.dataPath, Path.Combine(projectRoot, "Packages"), Path.Combine(projectRoot, "ProjectSettings"), Path.Combine(projectRoot, "AssetBundles") })
                if (Within(RootPath, protectedRoot) || Within(protectedRoot, RootPath)) throw new ArgumentException("ArtifactStore overlaps a protected project directory.");
            if (RootPath == Path.GetPathRoot(RootPath)?.TrimEnd(Path.DirectorySeparatorChar) ||
                string.Equals(RootPath, Path.GetFullPath(Application.persistentDataPath).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A private child directory is required.");
            Projects = ResourceCatalog.Copy(projects ?? throw new ArgumentNullException(nameof(projects)));
            if (Projects.Count == 0 || Projects.Count > 64 || Projects.Any(x => x == null) ||
                Projects.Select(x => x.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Projects.Count)
                throw new ArgumentException("A bounded explicit, case-distinct project closure is required.");
            mounts = Projects.ToDictionary(x => x.ProjectId, StringComparer.Ordinal);
            foreach (var mount in Projects) SafeSegment(mount.ProjectId);
            ResourceLocalSnapshot.RejectLinks(RootPath);
        }

        private static bool Within(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static void SafeSegment(string value)
        {
            ResourceCatalogCodec.Id(value);
            var stem = value.Split('.')[0].ToUpperInvariant();
            if (value.EndsWith(".", StringComparison.Ordinal) || new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem))
                throw new InvalidDataException("Unsafe store path segment: " + value);
        }

        private string ProjectRoot(string project) => Path.Combine(RootPath, project, mounts[project].Platform);
        internal string VersionPath(string project, string digest)
        {
            ResourceCatalogCodec.Hex(digest, 64);
            var path = Path.Combine(ProjectRoot(project), "versions", digest);
            if (path.Length > 210) throw new PathTooLongException("ArtifactStore root leaves insufficient room for version files.");
            return path;
        }
        // journal 放在稳定的首项目目录，记录整个闭包。配置不匹配时拒绝恢复，绝不部分挂载。
        private string CoordinatorRoot => ProjectRoot(Projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal).First().ProjectId);
        private string JournalPath => Path.Combine(CoordinatorRoot, JournalFile);
        private string PreparedRoot => Path.Combine(CoordinatorRoot, "prepared");
        private string PreparedPath(JArray transition) => Path.Combine(PreparedRoot, ResourceCatalogCodec.Digest(ResourceCatalogCodec.CanonicalBytes(transition)) + ".json");
        private string OwnerPath => Path.Combine(RootPath, OwnerFile);
        private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

        private JObject Read(string path)
        {
            operationCancellation.ThrowIfCancellationRequested();
            ResourceLocalSnapshot.RejectLinks(path);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > 1024 * 1024) throw new InvalidDataException("Store metadata exceeds its bound.");
            using var buffer = new MemoryStream(); input.CopyTo(buffer);
            return ResourceCatalogCodec.ParseObject(buffer.ToArray());
        }

        private void Write(string path, JObject data, Action beforeReplace = null)
        {
            if (!creating) RequireOwner();
            ResourceLocalSnapshot.RejectLinks(path);
            var next = path + ".next";
            if (Exists(next)) throw new InvalidDataException("Unrecovered store replacement file: " + next);
            var bytes = ResourceCatalogCodec.CanonicalBytes(data);
            using (var output = new FileStream(next, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
            Checkpoint?.Invoke("BeforeReplace:" + Path.GetFileName(path));
            if (!creating) RequireOwner();
            beforeReplace?.Invoke();
            if (File.Exists(path)) File.Replace(next, path, null);
            else File.Move(next, path);
        }

        private JObject Owner() => new()
        {
            ["Version"] = 1, ["Kind"] = "YLFramework.Resource.ArtifactStore", ["Root"] = RootPath, ["OwnerId"] = ownerId,
            ["Projects"] = new JArray(Projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal).Select(x => new JObject { ["ProjectId"] = x.ProjectId, ["Platform"] = x.Platform })),
            // 这些固定控制槽明确属于此 owner；未提交的截断 next 也不会被当成 foreign 文件或有效决策。
            ["ControlFiles"] = new JArray(ControlFiles().SelectMany(x => new[] { x, x + ".next" }).Select(x => x.Substring(RootPath.Length + 1).Replace('\\', '/')))
        };

        private IEnumerable<string> ControlFiles() => Projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal)
            .SelectMany(x => new[] { "active.json", "last-known-good.json" }.Select(name => Path.Combine(ProjectRoot(x.ProjectId), name))).Append(JournalPath);

        private void RequireOwner()
        {
            if (!ValidateOwner()) throw new InvalidDataException("ArtifactStore ownership disappeared.");
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId) throw new InvalidOperationException("ArtifactStore must be used on its Unity thread.");
        }

        private bool ValidateOwner()
        {
            ResourceLocalSnapshot.RejectLinks(RootPath);
            if (!Exists(RootPath)) return false;
            if (!Directory.Exists(RootPath) || !File.Exists(OwnerPath)) throw new InvalidDataException("Unknown ArtifactStore root; refusing to adopt it.");
            var data = Read(OwnerPath);
            var id = ResourceCatalogCodec.Text(data, "OwnerId"); ResourceCatalogCodec.Hex(id, 32);
            if (ownerId != null && ownerId != id) throw new InvalidDataException("ArtifactStore owner changed during this session.");
            ownerId = id;
            if (!JToken.DeepEquals(data, Owner())) throw new InvalidDataException("ArtifactStore ownership or project closure mismatch.");
            foreach (var mount in Projects)
            {
                var root = ProjectRoot(mount.ProjectId);
                ResourceLocalSnapshot.RejectLinks(root);
                if (!Directory.Exists(root) || !JToken.DeepEquals(Read(Path.Combine(root, OwnerFile)), ProjectOwner(mount.ProjectId)))
                    throw new InvalidDataException("Project store ownership mismatch.");
            }
            return true;
        }

        private JObject ProjectOwner(string project) => new() { ["Version"] = 1, ["OwnerId"] = ownerId, ["ProjectId"] = project, ["Platform"] = mounts[project].Platform };

        private IDisposable Enter(bool create, CancellationToken cancellation = default, bool contentIO = false, bool readOnly = false)
        {
            if (!contentIO) CheckThread();
            if (Interlocked.CompareExchange(ref operationState, 1, 0) != 0) throw new ResourceException(ResourceError.Busy, "ArtifactStore is already operating.");
            operationCancellation = cancellation;
            Interlocked.Increment(ref activitySerial);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (!ValidateOwner())
                {
                    if (!create) return new StoreLock(this, null);
                    ownerId = Guid.NewGuid().ToString("N");
                    creating = true;
                    try
                    {
                        Directory.CreateDirectory(RootPath);
                        foreach (var mount in Projects)
                        {
                            var root = ProjectRoot(mount.ProjectId);
                            Directory.CreateDirectory(root);
                            Write(Path.Combine(root, OwnerFile), ProjectOwner(mount.ProjectId));
                            foreach (var folder in new[] { "versions", "staging", "parts" }) Directory.CreateDirectory(Path.Combine(root, folder));
                        }
                        Directory.CreateDirectory(PreparedRoot);
                        Write(OwnerPath, Owner());
                    }
                    finally { creating = false; }
                }
                // Check只取得进程内读责任；缺失的持久lock也不能由只读查询创建。
                if (readOnly) return new StoreLock(this, null);
                var lockPath = Path.Combine(RootPath, ".ylr-store.lock");
                ResourceLocalSnapshot.RejectLinks(lockPath);
                return new StoreLock(this, new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch { operationCancellation = default; Volatile.Write(ref operationState, 0); Interlocked.Increment(ref activitySerial); throw; }
        }

        private sealed class StoreLock : IDisposable
        {
            private readonly ArtifactStore owner;
            private readonly FileStream stream;
            internal StoreLock(ArtifactStore owner, FileStream stream) { this.owner = owner; this.stream = stream; }
            public void Dispose()
            {
                try { stream?.Dispose(); }
                finally { owner.operationCancellation = default; Volatile.Write(ref owner.operationState, 0); Interlocked.Increment(ref activitySerial); }
            }
        }

        private ResourceLocalSnapshot Open(string project, string digest, bool refresh = false, CancellationToken cancellation = default)
        {
            var path = VersionPath(project, digest);
            var marker = Read(Path.Combine(path, VersionFile));
            ValidateVersionMarker(marker, project, digest);
            if (!refresh && verified.TryGetValue(path, out var cached)) return cached;
            var mount = mounts[project];
            var snapshot = ResourceLocalSnapshot.OpenOwned(path, project, digest, mount.Platform, mount.AppContentCompatibilityId,
                unityCompatibility, cancellation.CanBeCanceled ? cancellation : operationCancellation);
            verified[path] = snapshot;
            return snapshot;
        }

        private void ValidateVersionMarker(JObject marker, string project, string digest)
        {
            ResourceCatalogCodec.Fields(marker, "Version,OwnerId,ProjectId,Platform,CatalogDigest,State,Files");
            if (ResourceCatalogCodec.Integer(marker, "Version") != 1 || ResourceCatalogCodec.Text(marker, "OwnerId") != ownerId ||
                ResourceCatalogCodec.Text(marker, "ProjectId") != project || ResourceCatalogCodec.Text(marker, "Platform") != mounts[project].Platform ||
                ResourceCatalogCodec.Text(marker, "CatalogDigest") != digest || ResourceCatalogCodec.Text(marker, "State") != "Complete")
                throw new InvalidDataException("Version Complete ownership/identity mismatch.");
            RegisteredFiles(marker);
        }

        private static string[] RegisteredFiles(JObject marker)
        {
            var files = ReadArray(marker, "Files").Select(x => x.Type == JTokenType.String ? (string)x : throw new InvalidDataException("Expected registered filename.")).ToArray();
            if (files.Length < 2 || files.Length != files.Distinct(StringComparer.OrdinalIgnoreCase).Count() ||
                !files.Contains("resource-catalog.json") || !files.Contains(".ylr-complete.json")) throw new InvalidDataException("Invalid owned file allowlist.");
            foreach (var name in files)
                if (string.IsNullOrEmpty(name) || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    name.IndexOfAny(new[] { '/', '\\', ':', '%', '?' }) >= 0 || name.EndsWith(".", StringComparison.Ordinal))
                    throw new InvalidDataException("Unsafe registered filename.");
            return files;
        }

        private static JArray ReadArray(JObject data, string field) => data[field] as JArray ?? throw new InvalidDataException("Expected array: " + field);

        private JObject VersionMarker(ResourceLocalSnapshot snapshot) => VersionMarker(snapshot.Document);
        private JObject VersionMarker(ResourceCatalogDocument document) => new()
        {
            ["Version"] = 1, ["OwnerId"] = ownerId ?? new string('0', 32), ["ProjectId"] = document.ProjectId, ["Platform"] = document.Platform,
            ["CatalogDigest"] = document.CatalogDigest, ["State"] = "Complete",
            ["Files"] = new JArray(Files(document).Select(x => x.RelativeFile).Concat(new[] { "resource-catalog.json", ".ylr-complete.json" }))
        };

        private ResourceLocalSnapshot Reopen(ResourceLocalSnapshot snapshot, CancellationToken cancellation = default)
        {
            var doc = snapshot.Document;
            if (!mounts.TryGetValue(doc.ProjectId, out var mount) || doc.Platform != mount.Platform || doc.AppContentCompatibilityId != mount.AppContentCompatibilityId ||
                doc.UnityCompatibility != unityCompatibility)
                throw new InvalidDataException("The selected snapshot is outside the explicit compatible project closure.");
            if (snapshot.DirectoryPath == VersionPath(doc.ProjectId, doc.CatalogDigest)) return Open(doc.ProjectId, doc.CatalogDigest, true, cancellation);
            return snapshot.Reopen(cancellation.CanBeCanceled ? cancellation : operationCancellation);
        }

        private JObject Index(IEnumerable<ResourceLocalSnapshot> snapshots) => WithFloors(new JObject
        {
            ["Version"] = 1, ["OwnerId"] = ownerId,
            ["Projects"] = new JArray(snapshots.OrderBy(x => x.Document.ProjectId, StringComparer.Ordinal).Select(x => new JObject
                { ["ProjectId"] = x.Document.ProjectId, ["Platform"] = x.Document.Platform, ["CatalogDigest"] = x.Document.CatalogDigest }))
        }, ReadReleaseFloors());

        private JObject ValidateIndex(JObject index)
        {
            var version = ResourceCatalogCodec.Integer(index, "Version");
            ResourceCatalogCodec.Fields(index, version == 2 ? "Version,OwnerId,Projects,ReleaseFloors" : "Version,OwnerId,Projects");
            if (version != 1 && version != 2 || ResourceCatalogCodec.Text(index, "OwnerId") != ownerId)
                throw new InvalidDataException("Activation index ownership mismatch.");
            if (version == 2) ValidateFloors(index["ReleaseFloors"]);
            var projects = ReadArray(index, "Projects");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in projects)
            {
                if (token is not JObject item) throw new InvalidDataException("Invalid activation project.");
                ResourceCatalogCodec.Fields(item, "ProjectId,Platform,CatalogDigest");
                var project = ResourceCatalogCodec.Text(item, "ProjectId");
                if (!mounts.TryGetValue(project, out var mount) || !seen.Add(project) || ResourceCatalogCodec.Text(item, "Platform") != mount.Platform)
                    throw new InvalidDataException("Activation index project/platform closure mismatch.");
                ResourceCatalogCodec.Hex(ResourceCatalogCodec.Text(item, "CatalogDigest"), 64);
            }
            if (seen.Count != mounts.Count) throw new InvalidDataException("Incomplete activation closure.");
            return index;
        }

        private ResourceLocalSnapshot[] OpenIndex(JObject index, bool refresh = true) => ReadArray(ValidateIndex(index), "Projects")
            .Cast<JObject>().Select(x => Open((string)x["ProjectId"], (string)x["CatalogDigest"], refresh)).ToArray();

        private JObject ReadIndex(string name, Action<string> observe = null)
        {
            JObject selected = null;
            var missing = 0;
            foreach (var project in Projects)
            {
                var path = Path.Combine(ProjectRoot(project.ProjectId), name);
                observe?.Invoke(path);
                if (!Exists(path)) { missing++; continue; }
                var index = ValidateIndex(Read(path));
                if (selected != null && !JToken.DeepEquals(selected, index)) throw new InvalidDataException("Activation index mirrors disagree on the complete closure.");
                selected = index;
            }
            if (missing != 0 && missing != mounts.Count) throw new InvalidDataException("Activation index closure has missing mirrors.");
            return selected;
        }

        private JObject ValidIndex(string name, bool refresh = true)
        {
            try { var index = ReadIndex(name); if (index != null) OpenIndex(index, refresh); return index; }
            catch (Exception error) when (IsFileFailure(error)) { recoveryNotes[name] = name + ": " + error.Message; return null; }
        }

        private ResourceLocalSnapshot[] Select(bool refresh = true)
        {
            foreach (var name in new[] { "active.json", "last-known-good.json" })
            {
                var index = ValidIndex(name, refresh);
                if (index != null) return OpenIndex(index, false);
            }
            if (Projects.Any(x => x.BuiltIn == null)) throw new InvalidDataException("No complete compatible active, last-known-good or explicitly bound built-in closure.");
            return Projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal).Select(x => refresh ? Reopen(x.BuiltIn) : x.BuiltIn).ToArray();
        }

        internal IReadOnlyList<ResourceLocalSnapshot> Mount()
        {
            CheckThread();
            return MountForContent(CancellationToken.None);
        }

        internal IReadOnlyList<ResourceLocalSnapshot> MountForContent(CancellationToken cancellation)
        {
            using var guard = Enter(false, cancellation, true);
            recoveryNotes.Clear();
            if (Directory.Exists(RootPath)) RecoverLocked();
            var selected = Select();
            if (Directory.Exists(RootPath) && selected.All(x => x.DirectoryPath == VersionPath(x.Document.ProjectId, x.Document.CatalogDigest)))
            {
                var active = ValidIndex("active.json");
                var repair = Index(selected);
                if (!JToken.DeepEquals(active, repair))
                    foreach (var project in Projects) RestorePointer(project.ProjectId, "active.json", repair);
            }
            return selected;
        }

        internal UpdatePlan Check(IEnumerable<ResourceLocalSnapshot> current, IEnumerable<ResourceLocalSnapshot> targetClosure)
        {
            CheckThread();
            if (operating) throw new ResourceException(ResourceError.Busy, "ArtifactStore is already operating.");
            return CheckCore(current, targetClosure, CancellationToken.None);
        }

        internal UpdatePlan CheckForContent(IEnumerable<ResourceLocalSnapshot> current, IEnumerable<ResourceLocalSnapshot> targetClosure, CancellationToken cancellation)
        {
            using var guard = Enter(false, cancellation, true, true);
            return CheckCore(current, targetClosure, cancellation);
        }

        internal void RequireCheckedPlan(UpdatePlan plan)
        {
            CheckThread();
            if (plan == null || plan.Root != RootPath) throw new ArgumentException("The checked plan belongs to another store.");
            if (operating) throw new ResourceException(ResourceError.Busy, "ArtifactStore changed to an active operation before check handoff.");
            ValidateOwner(); RequireNoJournal();
            ReadTransition(Transition(plan.Projects));
        }

        private UpdatePlan CheckCore(IEnumerable<ResourceLocalSnapshot> current, IEnumerable<ResourceLocalSnapshot> targetClosure, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateOwner(); // Check 不创建 root/lock/staging，也不执行恢复写入。
            if (File.Exists(JournalPath)) throw new ResourceException(ResourceError.Busy, "Recover the pending commit before checking another update.");
            var from = Closure(current);
            var targets = Closure(targetClosure);
            var entries = new List<UpdateProject>();
            long required = 0;
            var budgeted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in mounts.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                cancellation.ThrowIfCancellationRequested();
                var target = Reopen(targets[project], cancellation);
                var old = from[project];
                var files = new List<UpdateFile>();
                if (target.Document.CatalogDigest != old.Document.CatalogDigest && !Exists(VersionPath(project, target.Document.CatalogDigest)))
                {
                    foreach (var file in Files(target.Document))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var reusable = Files(old.Document).Any(x => x.RelativeFile == file.RelativeFile && x.Length == file.Length && x.Sha256 == file.Sha256);
                        if (reusable) using (old.OpenVerified(file, cancellation)) { }
                        files.Add(new UpdateFile(file.RelativeFile, file.Length, reusable, Path.Combine(reusable ? old.DirectoryPath : target.DirectoryPath, file.RelativeFile)));
                    }
                }
                else if (Exists(VersionPath(project, target.Document.CatalogDigest))) Open(project, target.Document.CatalogDigest, true, cancellation);
                foreach (var snapshot in new[] { old, target })
                    if (!Exists(VersionPath(project, snapshot.Document.CatalogDigest)) && budgeted.Add(project + ":" + snapshot.Document.CatalogDigest))
                        required = checked(required + Files(snapshot.Document).Sum(x => x.Length) +
                            new FileInfo(Path.Combine(snapshot.DirectoryPath, "resource-catalog.json")).Length +
                            new FileInfo(Path.Combine(snapshot.DirectoryPath, ".ylr-complete.json")).Length +
                            ResourceCatalogCodec.CanonicalBytes(VersionMarker(snapshot)).Length * 2L + 512);
                entries.Add(new UpdateProject(old, target, files));
            }
            // 保守预算还包含整组镜像、journal/next 和归属记录；不把控制文件开销藏在每版本固定 16 KiB 中。
            var fromBytes = ResourceCatalogCodec.CanonicalBytes(Index(entries.Select(x => x.From))).Length + 32L;
            var toBytes = ResourceCatalogCodec.CanonicalBytes(Index(entries.Select(x => x.Target))).Length + 32L;
            required = checked(required + (fromBytes + toBytes) * mounts.Count + Math.Max(fromBytes, toBytes) +
                ResourceCatalogCodec.CanonicalBytes(Transition(entries)).Length * 4L + ResourceCatalogCodec.CanonicalBytes(Owner()).Length +
                Projects.Sum(x => ResourceCatalogCodec.CanonicalBytes(ProjectOwner(x.ProjectId)).Length + 32L) + 4096);
            var available = AvailableSpace();
            ValidateOwner(); RequireNoJournal();
            cancellation.ThrowIfCancellationRequested();
            return new UpdatePlan(RootPath, entries, available, required);
        }

        private Dictionary<string, ResourceLocalSnapshot> Closure(IEnumerable<ResourceLocalSnapshot> snapshots)
        {
            var result = new Dictionary<string, ResourceLocalSnapshot>(StringComparer.Ordinal);
            foreach (var snapshot in snapshots ?? throw new ArgumentNullException(nameof(snapshots)))
            {
                if (snapshot == null || !mounts.ContainsKey(snapshot.Document.ProjectId) || result.ContainsKey(snapshot.Document.ProjectId))
                    throw new InvalidDataException("Unknown or duplicate project in the explicit version closure.");
                result.Add(snapshot.Document.ProjectId, snapshot);
            }
            if (result.Count != mounts.Count) throw new InvalidDataException("Every mounted store project must have an explicit target CatalogDigest.");
            return result;
        }

        private static IEnumerable<ResourceCatalogFile> Files(ResourceCatalogDocument document) => document.Bundles.Cast<ResourceCatalogFile>().Concat(document.SupportFiles);

        internal PreparedUpdate Prepare(UpdatePlan plan, CancellationToken cancellation, ResourceDecryptionOptions decryption = null)
        {
            InstallPreparation(plan, cancellation);
            using var receipt = CapturePreparation(plan, decryption);
            receipt.Validate(cancellation);
            return CompletePreparation(plan, receipt, cancellation);
        }

        internal void InstallPreparation(UpdatePlan plan, CancellationToken cancellation, Action ensureAlive = null)
            => InstallPreparationCore(plan, cancellation, ensureAlive, false);

        internal void InstallPreparationForContent(UpdatePlan plan, CancellationToken cancellation)
            => InstallPreparationCore(plan, cancellation, null, true);

        private void InstallPreparationCore(UpdatePlan plan, CancellationToken cancellation, Action ensureAlive, bool contentIO)
        {
            if (plan == null || plan.Root != RootPath) throw new ArgumentException("The plan belongs to another store.");
            if (plan.IsRemote) throw new ResourceException(ResourceError.Unsupported, "Remote plans require the ResourceSystem asynchronous download path.");
            cancellation.ThrowIfCancellationRequested();
            using var guard = Enter(true, cancellation, contentIO);
            RecoverMetadata();
            RequireNoJournal();
            foreach (var entry in plan.Projects)
            {
                cancellation.ThrowIfCancellationRequested();
                ensureAlive?.Invoke();
                Install(entry.From, cancellation, ensureAlive: ensureAlive);
                Install(entry.Target, cancellation, entry.Files, ensureAlive);
            }
            cancellation.ThrowIfCancellationRequested();
            ensureAlive?.Invoke();
        }

        internal ValidationReceipt CapturePreparation(UpdatePlan plan, ResourceDecryptionOptions decryption = null, ResourceContentBudget budget = null)
        {
            if (plan == null || plan.Root != RootPath) throw new ArgumentException("The plan belongs to another store.");
            ResourceLocalSnapshot[] targets;
            ResourceLocalSnapshot[] sources;
            using (Enter(false))
            {
                RequireOwner(); RecoverMetadata(); RequireNoJournal();
                ReadTransition(Transition(plan.Projects));
                targets = plan.Projects.Select(x => x.Remote == null ? Open(x.ProjectId, x.ToDigest) : OpenRemoteVersion(x.Remote, false)).ToArray();
                sources = plan.Projects.Select(x => Open(x.ProjectId, x.FromDigest)).ToArray();
            }
            // 任意应用provider只在排他锁释放后调用；收据在调用前已绑定本次事务代次。
            return ValidationReceipt.Capture(this, plan, targets, sources, decryption, budget);
        }

        internal PreparedUpdate CompletePreparation(UpdatePlan plan, ValidationReceipt receipt, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            receipt.Require(this, plan);
            using var guard = Enter(false);
            RequireOwner(); RecoverMetadata(); RequireNoJournal();
            receipt.Consume();
            RequireFrom(plan.Projects);
            foreach (var entry in plan.Projects)
            {
                entry.Remote?.RequireFresh(DateTimeOffset.UtcNow);
                if (entry.Remote != null) CheckReleaseFloor(entry.Remote);
            }
            cancellation.ThrowIfCancellationRequested();
            return RecordPrepared(plan, receipt);
        }

        private PreparedUpdate RecordPrepared(UpdatePlan plan, ValidationReceipt receipt)
        {
            receipt.RequireStable();
            var transition = Transition(plan.Projects);
            var releases = plan.Projects.Where(x => x.Remote != null).Select(x => x.Remote).ToArray();
            var preparedPath = PreparedPath(transition);
            // 此时完整闭包已逐一验证，同名候选有本次显式选择作为归属依据。
            RemoveCandidate(preparedPath + ".next", x => ValidatePreparedRecord(x, preparedPath));
            JObject previous = null;
            if (File.Exists(preparedPath))
            {
                previous = Read(preparedPath);
                ValidatePreparedRecord(previous, preparedPath);
                if (!JToken.DeepEquals(ReadTransition(previous["Transition"]), transition)) throw new InvalidDataException("Prepared closure identity changed.");
                var authorizations = ReadReleaseRecords(previous).ToDictionary(x => x.ProjectId, StringComparer.Ordinal);
                foreach (var release in releases)
                {
                    if (authorizations.TryGetValue(release.ProjectId, out var existing) && release.Sequence < existing.Sequence)
                        throw new InvalidDataException("A prepared authorization cannot be silently downgraded; discard it explicitly before choosing an older authorized release.");
                    authorizations[release.ProjectId] = release;
                }
                releases = authorizations.Values.OrderBy(x => x.ProjectId, StringComparer.Ordinal).ToArray();
            }
            var record = new JObject { ["Version"] = releases.Length == 0 ? 1 : 2, ["OwnerId"] = ownerId, ["State"] = "Complete", ["Transition"] = transition };
            if (releases.Length != 0) record["Releases"] = ReleaseRecords(releases);
            if (!JToken.DeepEquals(previous, record)) Write(preparedPath, record, () =>
            {
                receipt.RequireStable();
                RequireFrom(plan.Projects);
                if (previous != null && !JToken.DeepEquals(Read(preparedPath), previous)) throw new InvalidDataException("Prepared authorization changed during refresh.");
            });
            return new PreparedUpdate(RootPath, plan.Projects.Select(x => new UpdateProject(x.From, Open(x.ProjectId, x.ToDigest), Array.Empty<UpdateFile>())), releases: releases);
        }

        private void RequireFrom(IEnumerable<UpdateProject> projects)
        {
            var current = Select(false).ToDictionary(x => x.Document.ProjectId, StringComparer.Ordinal);
            foreach (var project in projects)
                if (current[project.ProjectId].Document.CatalogDigest != project.FromDigest)
                    throw new ResourceException(ResourceError.StaleBase, "The persistent activation base changed: " + project.ProjectId);
        }

        // 这是一次事务的短期认证责任，不是第二份版本索引或可持久复用的解密标记。
        internal sealed class ValidationReceipt : IDisposable
        {
            private readonly ArtifactStore store;
            private readonly object operation;
            private readonly string transition;
            private readonly string owner;
            private readonly long epoch;
            private readonly List<(FileStream stream, ResourceCatalogFile file, long length)> files = new();
            private ResourceContentValidation content;
            private bool validated;
            private bool consumed;
            private bool disposing;
            private bool disposed;
            internal IReadOnlyList<ResourceLocalSnapshot> Targets { get; }
            internal IReadOnlyList<ResourceLocalSnapshot> Snapshots { get; }

            private ValidationReceipt(ArtifactStore store, object operation, ResourceLocalSnapshot[] targets, ResourceLocalSnapshot[] sources)
            {
                this.store = store; this.operation = operation;
                owner = store.ownerId; epoch = Interlocked.Read(ref activitySerial);
                transition = Identity(operation);
                Targets = ResourceCatalog.Copy(targets);
                Snapshots = ResourceCatalog.Copy(targets.Concat(sources).GroupBy(x => x.DirectoryPath, StringComparer.OrdinalIgnoreCase).Select(x => x.First()));
            }

            private static string Identity(object operation) => Convert.ToBase64String(ResourceCatalogCodec.CanonicalBytes(
                Transition(operation is UpdatePlan plan ? plan.Projects : ((PreparedUpdate)operation).Projects)));

            internal static ValidationReceipt Capture(ArtifactStore store, object operation, ResourceLocalSnapshot[] targets, ResourceLocalSnapshot[] sources,
                ResourceDecryptionOptions decryption, ResourceContentBudget budget)
            {
                var receipt = new ValidationReceipt(store, operation, targets, sources);
                try
                {
                    foreach (var snapshot in receipt.Snapshots)
                    {
                        var document = snapshot.Document;
                        var target = targets.Any(x => x.DirectoryPath == snapshot.DirectoryPath);
                        var files = target ? document.SupportFiles.Where(x => x.RelativeFile != document.ManifestFile) : Files(document);
                        foreach (var file in files.Append(
                            new ResourceCatalogFile(new JObject { ["RelativeFile"] = "resource-catalog.json",
                                ["Length"] = ResourceCatalogCodec.Write(document).LongLength, ["Sha256"] = document.CatalogDigest })))
                        {
                            var stream = snapshot.OpenContentFile(file);
                            receipt.files.Add((stream, file, stream.Length));
                        }
                        foreach (var name in new[] { ".ylr-complete.json", VersionFile })
                        {
                            var path = Path.Combine(snapshot.DirectoryPath, name);
                            ResourceLocalSnapshot.RejectLinks(path);
                            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                            receipt.files.Add((stream, null, stream.Length));
                        }
                        receipt.RequireMetadata(snapshot);
                    }
                    receipt.content = ResourceContentValidation.Capture(targets, decryption, budget: budget);
                    return receipt;
                }
                catch (Exception error)
                {
                    if (error is ResourceException { InnerException: ResourceContentCleanupException { Owner: ResourceContentValidation retained } } resource)
                    {
                        receipt.content = retained;
                        throw new ResourceException(resource.Kind, "Update content capture failed; the unfinished validation and its protected snapshots remain owned.",
                            new ResourceContentCleanupException(receipt, error));
                    }
                    try { receipt.Dispose(); }
                    catch (Exception cleanup)
                    {
                        throw new ResourceException(error is ResourceException failure ? failure.Kind : ResourceError.UpdateFailed,
                            "Update validation capture and cleanup failed; ownership remains in the cause.",
                            new ResourceContentCleanupException(receipt, new AggregateException(error, cleanup)));
                    }
                    throw;
                }
            }

            internal void Validate(CancellationToken cancellation = default)
            {
                if (disposing) throw new ObjectDisposedException(nameof(ValidationReceipt));
                cancellation.ThrowIfCancellationRequested();
                store.Checkpoint?.Invoke("ReceiptShaStarted");
                try
                {
                    foreach (var file in files)
                        if (file.file != null) ResourceLocalSnapshot.VerifyContentFile(file.stream, file.file, cancellation);
                }
                finally { store.Checkpoint?.Invoke("ReceiptShaFinished"); }
                content.Validate(cancellation, store.Checkpoint);
                cancellation.ThrowIfCancellationRequested();
                validated = true;
            }

            internal void Require(ArtifactStore expectedStore, object expectedOperation)
            {
                store.CheckThread();
                if (!ReferenceEquals(store, expectedStore) || !ReferenceEquals(operation, expectedOperation) || transition != Identity(expectedOperation) ||
                    owner != store.ownerId || disposing || consumed || !validated)
                    throw new InvalidOperationException("A completed, matching, unused update content receipt is required.");
                if (epoch != Interlocked.Read(ref activitySerial)) throw new ResourceException(ResourceError.StaleBase, "The store changed during content validation; explicitly retry the operation.");
                RequireStable();
            }

            internal void Consume() { RequireStable(); consumed = true; }

            internal void RequireStable()
            {
                if (disposing || !validated) throw new InvalidOperationException("Update content validation has not completed or was released.");
                content.RequireStable();
                foreach (var file in files)
                    if (!file.stream.CanRead || file.stream.Length != file.length)
                        throw new InvalidDataException("A validated update file changed before commit.");
                foreach (var snapshot in Snapshots) RequireMetadata(snapshot);
            }

            private void RequireMetadata(ResourceLocalSnapshot snapshot)
            {
                var document = snapshot.Document;
                if (snapshot.DirectoryPath != store.VersionPath(document.ProjectId, document.CatalogDigest) ||
                    !JToken.DeepEquals(store.Read(Path.Combine(snapshot.DirectoryPath, VersionFile)), store.VersionMarker(document)))
                    throw new InvalidDataException("The validated update version ownership changed.");
                ResourceLocalSnapshot.ValidateComplete(store.Read(Path.Combine(snapshot.DirectoryPath, ".ylr-complete.json")),
                    document.ProjectId, document.Platform, document.CatalogDigest, document.BuildId);
                var names = Files(document).Select(x => x.RelativeFile).Concat(new[] { "resource-catalog.json", ".ylr-complete.json", VersionFile });
                KnownDirectoryFiles(snapshot.DirectoryPath, new HashSet<string>(names, StringComparer.Ordinal)).ToArray();
                if (Directory.GetFiles(snapshot.DirectoryPath).Length != names.Count()) throw new InvalidDataException("The validated update file closure changed.");
            }

            public void Dispose()
            {
                if (disposed) return;
                disposing = true;
                var failures = new List<Exception>();
                if (content != null)
                    try { content.Dispose(); content = null; }
                    catch (Exception error) { failures.Add(error); }
                for (var i = files.Count - 1; i >= 0; i--)
                    try { files[i].stream.Dispose(); files.RemoveAt(i); }
                    catch (Exception error) { failures.Add(error); }
                if (failures.Count != 0)
                    throw new ResourceContentCleanupException(this, new AggregateException("Update content cleanup failed; unresolved inputs remain owned.", failures));
                disposed = true;
            }
        }

        private ResourceLocalSnapshot Install(ResourceLocalSnapshot source, CancellationToken cancellation, IReadOnlyList<UpdateFile> reuse = null, Action ensureAlive = null)
        {
            ensureAlive?.Invoke();
            var project = source.Document.ProjectId;
            var digest = source.Document.CatalogDigest;
            var target = VersionPath(project, digest);
            if (Exists(target))
            {
                return Open(project, digest, cancellation: cancellation);
            }
            source = Reopen(source, cancellation);
            var stage = Path.Combine(ProjectRoot(project), "staging", Guid.NewGuid().ToString("N"));
            ResourceLocalSnapshot.RejectLinks(stage);
            Directory.CreateDirectory(stage);
            var registered = Files(source.Document).Select(x => x.RelativeFile).Concat(new[] { "resource-catalog.json", ".ylr-complete.json" }).ToArray();
            var stageMarker = new JObject { ["Version"] = 1, ["OwnerId"] = ownerId, ["ProjectId"] = project, ["Platform"] = mounts[project].Platform,
                ["CatalogDigest"] = digest, ["Attempt"] = Path.GetFileName(stage), ["Files"] = new JArray(registered) };
            Write(Path.Combine(stage, ".ylr-stage.json"), stageMarker);
            foreach (var name in registered)
            {
                cancellation.ThrowIfCancellationRequested();
                ensureAlive?.Invoke();
                var inputPath = reuse?.FirstOrDefault(x => x.RelativeFile == name)?.Input ?? Path.Combine(source.DirectoryPath, name);
                ResourceLocalSnapshot.RejectLinks(inputPath);
                using (var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(Path.Combine(stage, name), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[64 * 1024];
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                    { cancellation.ThrowIfCancellationRequested(); ensureAlive?.Invoke(); output.Write(buffer, 0, count); }
                    output.Flush(true);
                }
                Checkpoint?.Invoke("FileCopied");
                ensureAlive?.Invoke();
            }
            // 先验证普通发布文件，最后写 owned Complete。取消/失败留下的 staging 从不参与挂载。
            var mount = mounts[project];
            ResourceLocalSnapshot.OpenStaging(stage, project, digest, mount.Platform, mount.AppContentCompatibilityId, unityCompatibility, cancellation);
            cancellation.ThrowIfCancellationRequested();
            ensureAlive?.Invoke();
            return PromoteStage(stage, stageMarker, source.Document, ensureAlive, cancellation);
        }

        private ResourceLocalSnapshot PromoteStage(string stage, JObject stageMarker, ResourceCatalogDocument document, Action ensureAlive = null,
            CancellationToken cancellation = default)
        {
            ensureAlive?.Invoke();
            var project = document.ProjectId; var digest = document.CatalogDigest;
            RequireOwner();
            var versionMarker = VersionMarker(document);
            Write(Path.Combine(stage, VersionFile), versionMarker);
            Checkpoint?.Invoke("CompleteWritten");
            ensureAlive?.Invoke();
            RequireOwner();
            if (!JToken.DeepEquals(Read(Path.Combine(stage, ".ylr-stage.json")), stageMarker) || !JToken.DeepEquals(Read(Path.Combine(stage, VersionFile)), versionMarker))
                throw new InvalidDataException("Staging ownership changed before promotion.");
            File.Delete(Path.Combine(stage, ".ylr-stage.json"));
            Directory.Move(stage, VersionPath(project, digest));
            Checkpoint?.Invoke("VersionInstalled");
            ensureAlive?.Invoke();
            return Open(project, digest, cancellation: cancellation);
        }

        private static JArray Transition(IEnumerable<UpdateProject> projects) => new(projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal)
            .Select(x => new JObject { ["ProjectId"] = x.ProjectId, ["Platform"] = x.Platform, ["From"] = x.FromDigest, ["To"] = x.ToDigest }));

        private JArray ReadTransition(JToken token)
        {
            if (token is not JArray array || array.Count != mounts.Count) throw new InvalidDataException("Invalid version transaction closure.");
            var projects = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in array)
            {
                if (item is not JObject entry) throw new InvalidDataException("Expected transaction project.");
                ResourceCatalogCodec.Fields(entry, "ProjectId,Platform,From,To");
                var project = ResourceCatalogCodec.Text(entry, "ProjectId");
                if (!mounts.TryGetValue(project, out var mount) || !projects.Add(project) || ResourceCatalogCodec.Text(entry, "Platform") != mount.Platform)
                    throw new InvalidDataException("Transaction project/platform closure mismatch.");
                ResourceCatalogCodec.Hex(ResourceCatalogCodec.Text(entry, "From"), 64);
                ResourceCatalogCodec.Hex(ResourceCatalogCodec.Text(entry, "To"), 64);
            }
            return array;
        }

        public IReadOnlyList<PreparedUpdate> GetPreparedUpdates()
        {
            using var guard = Enter(false);
            if (!ValidateOwner()) return Array.Empty<PreparedUpdate>();
            RecoverMetadata();
            RequireNoJournal();
            return PreparedRecords().Select(x => RecoverPrepared(ReadTransition(x["Transition"]), ReadReleaseRecords(x))).ToArray();
        }

        private IEnumerable<JObject> PreparedRecords()
        {
            ResourceLocalSnapshot.RejectLinks(PreparedRoot);
            foreach (var path in Directory.EnumerateFiles(PreparedRoot, "*.json"))
            {
                yield return ReadPreparedRecord(path);
            }
        }

        private JObject ReadPreparedRecord(string path)
        {
            var record = Read(path);
            ValidatePreparedRecord(record, path);
            return record;
        }

        private PreparedUpdate RecoverPrepared(JArray transition, ResourceRemoteRelease[] releases)
        {
            var projects = new List<UpdateProject>();
            foreach (JObject item in transition)
            {
                var project = (string)item["ProjectId"]; var from = (string)item["From"]; var to = (string)item["To"];
                var target = Open(project, to, true);
                projects.Add(new UpdateProject(Open(project, from), target, Array.Empty<UpdateFile>()));
            }
            return new PreparedUpdate(RootPath, projects, releases: releases);
        }

        internal static bool IsFileFailure(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is ArgumentException;
    }
}
