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
        private bool operating;
        private bool creating;
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
        private string VersionPath(string project, string digest)
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

        private IDisposable Enter(bool create)
        {
            CheckThread();
            if (operating) throw new ResourceException(ResourceError.Busy, "ArtifactStore is already operating.");
            operating = true;
            try
            {
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
                var lockPath = Path.Combine(RootPath, ".ylr-store.lock");
                ResourceLocalSnapshot.RejectLinks(lockPath);
                return new StoreLock(this, new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch { operating = false; throw; }
        }

        private sealed class StoreLock : IDisposable
        {
            private readonly ArtifactStore owner;
            private readonly FileStream stream;
            internal StoreLock(ArtifactStore owner, FileStream stream) { this.owner = owner; this.stream = stream; }
            public void Dispose() { stream?.Dispose(); owner.operating = false; }
        }

        private ResourceLocalSnapshot Open(string project, string digest, bool refresh = false)
        {
            var path = VersionPath(project, digest);
            var marker = Read(Path.Combine(path, VersionFile));
            ValidateVersionMarker(marker, project, digest);
            if (!refresh && verified.TryGetValue(path, out var cached)) return cached;
            var mount = mounts[project];
            var snapshot = ResourceLocalSnapshot.OpenOwned(path, project, digest, mount.Platform, mount.AppContentCompatibilityId);
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

        private ResourceLocalSnapshot Reopen(ResourceLocalSnapshot snapshot)
        {
            var doc = snapshot.Document;
            if (!mounts.TryGetValue(doc.ProjectId, out var mount) || doc.Platform != mount.Platform || doc.AppContentCompatibilityId != mount.AppContentCompatibilityId)
                throw new InvalidDataException("The selected snapshot is outside the explicit compatible project closure.");
            if (snapshot.DirectoryPath == VersionPath(doc.ProjectId, doc.CatalogDigest)) return Open(doc.ProjectId, doc.CatalogDigest, true);
            return ResourceLocalSnapshot.Open(snapshot.DirectoryPath, doc.ProjectId, doc.CatalogDigest, mount.Platform, mount.AppContentCompatibilityId);
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

        private JObject ReadIndex(string name)
        {
            JObject selected = null;
            var missing = 0;
            foreach (var project in Projects)
            {
                var path = Path.Combine(ProjectRoot(project.ProjectId), name);
                if (!Exists(path)) { missing++; continue; }
                var index = ValidateIndex(Read(path));
                if (selected != null && !JToken.DeepEquals(selected, index)) throw new InvalidDataException("Activation index mirrors disagree on the complete closure.");
                selected = index;
            }
            if (missing != 0 && missing != mounts.Count) throw new InvalidDataException("Activation index closure has missing mirrors.");
            return selected;
        }

        private JObject ValidIndex(string name)
        {
            try { var index = ReadIndex(name); if (index != null) OpenIndex(index); return index; }
            catch (Exception error) when (IsFileFailure(error)) { recoveryNotes[name] = name + ": " + error.Message; return null; }
        }

        private ResourceLocalSnapshot[] Select()
        {
            foreach (var name in new[] { "active.json", "last-known-good.json" })
            {
                var index = ValidIndex(name);
                if (index != null) return OpenIndex(index, false);
            }
            if (Projects.Any(x => x.BuiltIn == null)) throw new InvalidDataException("No complete compatible active, last-known-good or explicitly bound built-in closure.");
            return Projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal).Select(x => Reopen(x.BuiltIn)).ToArray();
        }

        internal IReadOnlyList<ResourceLocalSnapshot> Mount()
        {
            using var guard = Enter(false);
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
            ValidateOwner(); // Check 不创建 root/lock/staging，也不执行恢复写入。
            if (File.Exists(JournalPath)) throw new ResourceException(ResourceError.Busy, "Recover the pending commit before checking another update.");
            var from = Closure(current);
            var targets = Closure(targetClosure);
            var entries = new List<UpdateProject>();
            long required = 0;
            var budgeted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in mounts.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var target = Reopen(targets[project]);
                var old = from[project];
                var files = new List<UpdateFile>();
                if (target.Document.CatalogDigest != old.Document.CatalogDigest && !Exists(VersionPath(project, target.Document.CatalogDigest)))
                {
                    foreach (var file in Files(target.Document))
                    {
                        var reusable = Files(old.Document).Any(x => x.RelativeFile == file.RelativeFile && x.Length == file.Length && x.Sha256 == file.Sha256);
                        if (reusable) using (old.OpenVerified(file)) { }
                        files.Add(new UpdateFile(file.RelativeFile, file.Length, reusable, Path.Combine(reusable ? old.DirectoryPath : target.DirectoryPath, file.RelativeFile)));
                    }
                }
                else if (Exists(VersionPath(project, target.Document.CatalogDigest))) Open(project, target.Document.CatalogDigest, true);
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
            var available = new DriveInfo(Path.GetPathRoot(RootPath)).AvailableFreeSpace;
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

        internal PreparedUpdate Prepare(UpdatePlan plan, CancellationToken cancellation)
        {
            if (plan == null || plan.Root != RootPath) throw new ArgumentException("The plan belongs to another store.");
            if (plan.IsRemote) throw new ResourceException(ResourceError.Unsupported, "Remote plans require the ResourceSystem asynchronous download path.");
            cancellation.ThrowIfCancellationRequested();
            using var guard = Enter(true);
            RecoverMetadata();
            RequireNoJournal();
            foreach (var entry in plan.Projects)
            {
                cancellation.ThrowIfCancellationRequested();
                Install(entry.From, cancellation);
                Install(entry.Target, cancellation, entry.Files);
            }
            cancellation.ThrowIfCancellationRequested();
            return RecordPrepared(plan);
        }

        private PreparedUpdate RecordPrepared(UpdatePlan plan)
        {
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
                if (previous != null && !JToken.DeepEquals(Read(preparedPath), previous)) throw new InvalidDataException("Prepared authorization changed during refresh.");
            });
            return new PreparedUpdate(RootPath, plan.Projects.Select(x => new UpdateProject(x.From, Open(x.ProjectId, x.ToDigest), Array.Empty<UpdateFile>())), releases: releases);
        }

        private ResourceLocalSnapshot Install(ResourceLocalSnapshot source, CancellationToken cancellation, IReadOnlyList<UpdateFile> reuse = null, Action ensureAlive = null)
        {
            ensureAlive?.Invoke();
            var project = source.Document.ProjectId;
            var digest = source.Document.CatalogDigest;
            var target = VersionPath(project, digest);
            if (Exists(target))
            {
                return Open(project, digest);
            }
            source = Reopen(source);
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
            ResourceLocalSnapshot.OpenStaging(stage, project, digest, mount.Platform, mount.AppContentCompatibilityId);
            cancellation.ThrowIfCancellationRequested();
            ensureAlive?.Invoke();
            return PromoteStage(stage, stageMarker, source.Document, ensureAlive);
        }

        private ResourceLocalSnapshot PromoteStage(string stage, JObject stageMarker, ResourceCatalogDocument document, Action ensureAlive = null)
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
            return Open(project, digest);
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

        private JObject[] PreparedRecords()
        {
            ResourceLocalSnapshot.RejectLinks(PreparedRoot);
            return Directory.GetFiles(PreparedRoot, "*.json").Select(path =>
            {
                var record = Read(path);
                ValidatePreparedRecord(record, path);
                return record;
            }).ToArray();
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
