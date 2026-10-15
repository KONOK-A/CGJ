using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public sealed partial class ArtifactStore
    {
        internal IEnumerator CheckRemote(IEnumerable<ResourceLocalSnapshot> current, Action<UpdatePlan> complete, Action ensureAlive = null)
        {
            CheckThread();
            ResourceRemoteSource.RequireSupportedRuntime();
            if (operating) throw new ResourceException(ResourceError.Busy, "ArtifactStore is already operating.");
            ValidateOwner(); RequireNoJournal();
            var from = Closure(current);
            var entries = new List<UpdateProject>();
            long required = 16384;
            foreach (var mount in Projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal))
            {
                ensureAlive?.Invoke();
                var old = from[mount.ProjectId];
                if (mount.Remote == null) { entries.Add(new UpdateProject(old, old, Array.Empty<UpdateFile>())); continue; }
                using var metadata = new ResourceTransfer(mount.Remote, mount.Remote.Resolve("release.json"));
                yield return metadata;
                var release = new ResourceRemoteRelease(metadata.Bytes, mount, Application.unityVersion, DateTimeOffset.UtcNow);
                CheckReleaseFloor(release);
                using var catalog = new ResourceTransfer(mount.Remote, mount.Remote.Resolve(release.CatalogDigest + "/resource-catalog.json"), release.CatalogLength, release.CatalogDigest);
                yield return catalog;
                release.ReadCatalog(catalog.Bytes, mount, Application.unityVersion);
                var files = new List<UpdateFile>();
                if (Exists(VersionPath(mount.ProjectId, release.CatalogDigest))) OpenRemoteVersion(release);
                else
                {
                    foreach (var file in Files(release.Catalog))
                    {
                        var reusable = Files(old.Document).Any(x => x.RelativeFile == file.RelativeFile && x.Length == file.Length && x.Sha256 == file.Sha256);
                        if (reusable) using (old.OpenVerified(file)) { }
                        files.Add(new UpdateFile(file.RelativeFile, file.Length, reusable, reusable ? Path.Combine(old.DirectoryPath, file.RelativeFile) : null));
                    }
                    // partial、正式staging以及8.7保留From副本的保守上界；不是空间预留。
                    required = checked(required + Files(release.Catalog).Sum(x => x.Length) * 2 + release.CatalogLength * 2 + release.CompleteLength * 2 + 16384);
                }
                entries.Add(new UpdateProject(old, release, files));
            }
            if (entries.All(x => x.Remote == null)) throw new ResourceException(ResourceError.Unsupported, "No explicitly trusted remote projects are configured.");
            foreach (var old in from.Values)
                if (!Exists(VersionPath(old.Document.ProjectId, old.Document.CatalogDigest)))
                    required = checked(required + Files(old.Document).Sum(x => x.Length) + ResourceCatalogCodec.Write(old.Document).LongLength + 16384);
            ValidateOwner(); RequireNoJournal();
            ensureAlive?.Invoke();
            complete(new UpdatePlan(RootPath, entries, AvailableSpace(), required));
        }

        private long AvailableSpace() => new DriveInfo(Path.GetPathRoot(RootPath)).AvailableFreeSpace;

        internal IEnumerator PrepareRemote(UpdatePlan plan, Action<PreparedUpdate> complete, Action ensureAlive = null)
        {
            if (plan == null || plan.Root != RootPath || !plan.IsRemote) throw new ArgumentException("A remote plan from this store is required.");
            ResourceRemoteSource.RequireSupportedRuntime();
            ensureAlive?.Invoke();
            // 复用store排他锁，取消/退出由枚举器Dispose释放；旧版Acquire不需要此磁盘写锁。
            using var guard = Enter(true);
            RecoverMetadata(); RequireNoJournal();
            if (AvailableSpace() < plan.RequiredBytes) throw new IOException("Insufficient free disk space for remote preparation.");
            foreach (var entry in plan.Projects)
            {
                ensureAlive?.Invoke();
                entry.Remote?.RequireFresh(DateTimeOffset.UtcNow);
                if (entry.Remote != null) CheckReleaseFloor(entry.Remote);
                Install(entry.From, CancellationToken.None, ensureAlive: ensureAlive);
                if (entry.Remote == null) Install(entry.Target, CancellationToken.None, entry.Files, ensureAlive);
                else yield return InstallRemote(entry, ensureAlive);
            }
            foreach (var entry in plan.Projects) entry.Remote?.RequireFresh(DateTimeOffset.UtcNow);
            ensureAlive?.Invoke();
            complete(RecordPrepared(plan));
        }

        private IEnumerator InstallRemote(UpdateProject entry, Action ensureAlive)
        {
            ensureAlive?.Invoke();
            var release = entry.Remote;
            if (Exists(VersionPath(entry.ProjectId, entry.ToDigest))) { OpenRemoteVersion(release); yield break; }
            var inputs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Files(release.Catalog).Concat(new[] { new ResourceCatalogFile(new JObject { ["RelativeFile"] = ".ylr-complete.json", ["Length"] = release.CompleteLength, ["Sha256"] = release.CompleteSha256 }) }))
            {
                ensureAlive?.Invoke();
                release.RequireFresh(DateTimeOffset.UtcNow);
                var reusable = entry.Files.FirstOrDefault(x => x.RelativeFile == file.RelativeFile && x.Reusable);
                if (reusable != null) { inputs.Add(file.RelativeFile, reusable.Input); continue; }
                var part = new DownloadPart(this, release, file.RelativeFile, file.Length, file.Sha256);
                if (AvailableSpace() < Math.Max(0, file.Length - part.Length) + 16384) throw new IOException("Insufficient free disk space for release content.");
                using var transfer = new ResourceTransfer(release.Source, release.Source.Resolve(release.CatalogDigest + "/" + file.RelativeFile), file.Length, file.Sha256, part: part);
                yield return transfer;
                ensureAlive?.Invoke();
                part.Verify(); inputs.Add(file.RelativeFile, part.Path);
            }
            ensureAlive?.Invoke();
            var stage = Path.Combine(ProjectRoot(entry.ProjectId), "staging", Guid.NewGuid().ToString("N"));
            ResourceLocalSnapshot.RejectLinks(stage); RequireOwner(); Directory.CreateDirectory(stage);
            var stageMarker = new JObject { ["Version"] = 1, ["OwnerId"] = ownerId, ["ProjectId"] = entry.ProjectId, ["Platform"] = entry.Platform,
                ["CatalogDigest"] = entry.ToDigest, ["Attempt"] = Path.GetFileName(stage),
                ["Files"] = new JArray(Files(release.Catalog).Select(x => x.RelativeFile).Concat(new[] { "resource-catalog.json", ".ylr-complete.json" })) };
            Write(Path.Combine(stage, ".ylr-stage.json"), stageMarker);
            foreach (var input in inputs)
            {
                ensureAlive?.Invoke();
                RequireOwner(); ResourceLocalSnapshot.RejectLinks(input.Value);
                using var stream = new FileStream(input.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var output = new FileStream(Path.Combine(stage, input.Key), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buffer = new byte[64 * 1024];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) != 0) { ensureAlive?.Invoke(); output.Write(buffer, 0, count); }
                output.Flush(true);
                Checkpoint?.Invoke("RemoteFileCopied");
                ensureAlive?.Invoke();
            }
            var bytes = ResourceCatalogCodec.Write(release.Catalog);
            using (var output = new FileStream(Path.Combine(stage, "resource-catalog.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
            var mount = mounts[entry.ProjectId];
            ResourceLocalSnapshot.OpenStaging(stage, entry.ProjectId, entry.ToDigest, entry.Platform, mount.AppContentCompatibilityId);
            release.RequireFresh(DateTimeOffset.UtcNow);
            ensureAlive?.Invoke();
            PromoteStage(stage, stageMarker, release.Catalog, ensureAlive);
        }

        private ResourceLocalSnapshot OpenRemoteVersion(ResourceRemoteRelease release)
        {
            var snapshot = Open(release.ProjectId, release.CatalogDigest, true);
            // 既有Complete的结构有效不等于符合本次发布签名；8.7本地版本升级为可信远端版本时同样不能跳过。
            foreach (var file in new[]
            {
                new ResourceCatalogFile(new JObject { ["RelativeFile"] = "resource-catalog.json", ["Length"] = release.CatalogLength, ["Sha256"] = release.CatalogDigest }),
                new ResourceCatalogFile(new JObject { ["RelativeFile"] = ".ylr-complete.json", ["Length"] = release.CompleteLength, ["Sha256"] = release.CompleteSha256 })
            }) using (snapshot.OpenVerified(file)) { }
            return snapshot;
        }

        private static JArray ReleaseRecords(IEnumerable<ResourceRemoteRelease> releases) => new(releases.OrderBy(x => x.ProjectId, StringComparer.Ordinal).Select(x =>
            new JObject { ["ProjectId"] = x.ProjectId, ["Envelope"] = Convert.ToBase64String(x.Envelope) }));

        private ResourceRemoteRelease[] ReadReleaseRecords(JObject record, bool fresh = false)
        {
            if (ResourceCatalogCodec.Integer(record, "Version") == 1) return Array.Empty<ResourceRemoteRelease>();
            var array = ReadArray(record, "Releases");
            if (array.Count == 0 || array.Count > mounts.Count) throw new InvalidDataException("Invalid prepared release closure.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return array.Select(token =>
            {
                if (token is not JObject item) throw new InvalidDataException("Expected prepared release.");
                ResourceCatalogCodec.Fields(item, "ProjectId,Envelope");
                var project = ResourceCatalogCodec.Text(item, "ProjectId");
                if (!mounts.TryGetValue(project, out var mount) || !seen.Add(project)) throw new InvalidDataException("Unknown or duplicate prepared release project.");
                var release = new ResourceRemoteRelease(ResourceReleaseSignature.Base64(item, "Envelope", ResourceReleaseSignature.MaximumBytes), mount, Application.unityVersion,
                    DateTimeOffset.UtcNow, fresh);
                if (ReadTransition(record["Transition"]).Cast<JObject>().Single(x => (string)x["ProjectId"] == project)["To"].Value<string>() != release.CatalogDigest)
                    throw new InvalidDataException("Prepared release does not authorize its target digest.");
                return release;
            }).ToArray();
        }
    }
}
