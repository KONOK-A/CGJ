using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace YLFramework.Services.Resource
{
    public sealed partial class ArtifactStore
    {
        internal IReadOnlyList<ResourceLocalSnapshot> Commit(PreparedUpdate update)
        {
            if (update == null || update.Root != RootPath) throw new ArgumentException("PreparedUpdate belongs to another store.");
            using var guard = Enter(false);
            RequireOwner();
            RecoverLocked();
            var transition = Transition(update.Projects);
            ReadTransition(transition);
            if (!update.IsRollback)
            {
                var preparedPath = PreparedPath(transition);
                if (!File.Exists(preparedPath)) throw new InvalidDataException("PreparedUpdate was discarded or consumed; Check and Prepare again.");
                ValidatePreparedRecord(Read(preparedPath), preparedPath);
            }
            var current = Select().ToDictionary(x => x.Document.ProjectId, StringComparer.Ordinal);
            foreach (var item in update.Projects)
            {
                if (current[item.ProjectId].Document.CatalogDigest != item.FromDigest)
                    throw new ResourceException(ResourceError.StaleBase, "The persistent activation base changed: " + item.ProjectId);
                Open(item.ProjectId, item.ToDigest, true);
            }
            var journal = new JObject { ["Version"] = 1, ["OwnerId"] = ownerId, ["Phase"] = "Verified", ["Transition"] = transition,
                ["ActiveBefore"] = ValidIndex("active.json"), ["GoodBefore"] = ValidIndex("last-known-good.json") };
            Write(JournalPath, journal);
            Checkpoint?.Invoke("JournalVerified");
            var from = TransitionIndex(transition, "From");
            var to = TransitionIndex(transition, "To");
            // LKG 是上一整个闭包，而不是各项目最近一次变化的版本。
            foreach (var item in update.Projects)
            {
                RequireJournal(journal);
                Write(Path.Combine(ProjectRoot(item.ProjectId), "last-known-good.json"), from, () => RequireJournal(journal));
                Checkpoint?.Invoke("LastGoodWritten:" + item.ProjectId);
                RequireJournal(journal);
                Write(Path.Combine(ProjectRoot(item.ProjectId), "active.json"), to, () => RequireJournal(journal));
                Checkpoint?.Invoke("ActiveWritten:" + item.ProjectId);
            }
            RequireJournal(journal);
            var committed = (JObject)journal.DeepClone();
            committed["Phase"] = "Committed";
            Checkpoint?.Invoke("BeforeCommitDecision");
            Write(JournalPath, committed, () => RequireJournal(journal));
            Checkpoint?.Invoke("CommitDecided");
            RecoverLocked();
            return Select();
        }

        private JObject TransitionIndex(JArray transition, string field) => new()
        {
            ["Version"] = 1, ["OwnerId"] = ownerId,
            ["Projects"] = new JArray(transition.Cast<JObject>().Select(x => new JObject
                { ["ProjectId"] = (string)x["ProjectId"], ["Platform"] = (string)x["Platform"], ["CatalogDigest"] = (string)x[field] }))
        };

        private JObject ValidateJournal(JObject journal)
        {
            ResourceCatalogCodec.Fields(journal, "Version,OwnerId,Phase,Transition,ActiveBefore,GoodBefore");
            if (ResourceCatalogCodec.Integer(journal, "Version") != 1 || ResourceCatalogCodec.Text(journal, "OwnerId") != ownerId ||
                !new[] { "Verified", "Committed" }.Contains(ResourceCatalogCodec.Text(journal, "Phase")))
                throw new InvalidDataException("Invalid commit journal identity/state.");
            ReadTransition(journal["Transition"]);
            foreach (var field in new[] { "ActiveBefore", "GoodBefore" })
            {
                if (journal[field]?.Type == JTokenType.Null) continue;
                if (journal[field] is not JObject index) throw new InvalidDataException("Invalid journal index snapshot.");
                ValidateIndex(index);
            }
            return journal;
        }

        private void RequireJournal(JObject expected)
        {
            RequireOwner();
            if (!File.Exists(JournalPath) || !JToken.DeepEquals(ValidateJournal(Read(JournalPath)), expected))
                throw new InvalidDataException("Commit journal changed before the protected write.");
        }

        private void RequireNoJournal()
        {
            if (Exists(JournalPath)) throw new ResourceException(ResourceError.Busy, "A commit needs recovery at initialization or inside the activation barrier.");
        }

        private void RecoverLocked()
        {
            if (!ValidateOwner()) return;
            RecoverMetadata();
            if (!File.Exists(JournalPath)) return;
            var journal = ValidateJournal(Read(JournalPath));
            var transition = ReadTransition(journal["Transition"]);
            var committed = (string)journal["Phase"] == "Committed";
            var from = TransitionIndex(transition, "From");
            var to = TransitionIndex(transition, "To");
            try { OpenIndex(committed ? to : from); }
            catch (Exception error) when (committed && IsFileFailure(error))
            {
                OpenIndex(from);
                committed = false;
            }
            var active = committed ? to : journal["ActiveBefore"] as JObject;
            var good = committed ? from : journal["GoodBefore"] as JObject;
            if (active != null) OpenIndex(active);
            if (good != null) OpenIndex(good);
            // 全部验证完成后才恢复镜像。每个镜像自身也携带完整闭包，journal 收尾后仍能识别混代。
            foreach (var project in Projects)
            {
                RestorePointer(project.ProjectId, "last-known-good.json", good, () => RequireJournal(journal));
                RestorePointer(project.ProjectId, "active.json", active, () => RequireJournal(journal));
            }
            RequireJournal(journal);
            if (committed) RemovePreparedRecord(transition);
            File.Delete(JournalPath);
            Checkpoint?.Invoke("Recovered");
        }

        private void RecoverMetadata()
        {
            RequireOwner();
            RemoveCandidate(JournalPath + ".next", x => ValidateJournal(x));
            foreach (var project in Projects)
            {
                foreach (var name in new[] { "active.json", "last-known-good.json" })
                    RemoveCandidate(Path.Combine(ProjectRoot(project.ProjectId), name) + ".next", x => ValidateIndex(x));
                var versions = Path.Combine(ProjectRoot(project.ProjectId), "versions");
                ResourceLocalSnapshot.RejectLinks(versions);
                foreach (var version in Directory.GetDirectories(versions))
                {
                    var path = Path.Combine(version, VersionFile);
                    if (!File.Exists(path) || !Exists(path + ".next")) continue;
                    ValidateVersionMarker(Read(path), project.ProjectId, Path.GetFileName(version));
                    RemoveCandidate(path + ".next", x => ValidateVersionMarker(x, project.ProjectId, Path.GetFileName(version)));
                }
            }
            ResourceLocalSnapshot.RejectLinks(PreparedRoot);
            foreach (var path in Directory.GetFiles(PreparedRoot, "*.json.next"))
            {
                // 已登记的正式记录为同名槽提供归属。首次发布中断的未知候选不进入 Prepared 枚举。
                if (File.Exists(path.Substring(0, path.Length - 5))) RemoveCandidate(path, x => ValidatePreparedRecord(x, path.Substring(0, path.Length - 5)));
            }
        }

        private void RemoveCandidate(string path, Action<JObject> validate)
        {
            if (!Exists(path)) return;
            RequireOwner();
            ResourceLocalSnapshot.RejectLinks(path);
            if (!File.Exists(path)) throw new InvalidDataException("A replacement control slot is not a file.");
            JObject candidate = null;
            try { candidate = Read(path); }
            catch (InvalidDataException) { /* 未提交且有归属依据的截断候选不是恢复决策。 */ }
            if (candidate != null) validate(candidate); // 完整但属于其他 owner 的内容明确拒绝。
            RequireOwner();
            File.Delete(path);
        }

        private void RestorePointer(string project, string name, JObject index, Action guard = null)
        {
            var path = Path.Combine(ProjectRoot(project), name);
            JObject previous = null;
            if (File.Exists(path))
                try { previous = Read(path); }
                catch (InvalidDataException) { /* 正式槽已由 root marker 归属；损坏字节不阻止已验证闭包修复它。 */ }
            if (previous != null && (string)previous["OwnerId"] != ownerId) throw new InvalidDataException("Foreign ownership in an activation control slot.");
            RequireOwner(); guard?.Invoke();
            if (index == null) { if (File.Exists(path)) File.Delete(path); return; }
            if (JToken.DeepEquals(previous, index)) return;
            var checkpoint = Checkpoint;
            try { Checkpoint = null; Write(path, index, guard); }
            finally { Checkpoint = checkpoint; }
        }

        private void ValidatePreparedRecord(JObject record, string path)
        {
            ResourceCatalogCodec.Fields(record, "Version,OwnerId,State,Transition");
            if (ResourceCatalogCodec.Integer(record, "Version") != 1 || ResourceCatalogCodec.Text(record, "OwnerId") != ownerId ||
                ResourceCatalogCodec.Text(record, "State") != "Complete" || PreparedPath(ReadTransition(record["Transition"])) != path)
                throw new InvalidDataException("Prepared closure record ownership/identity mismatch.");
        }

        public void DiscardPreparedUpdate(PreparedUpdate update)
        {
            if (update == null || update.Root != RootPath) throw new ArgumentException("PreparedUpdate belongs to another store.");
            using var guard = Enter(false);
            RequireOwner(); RecoverMetadata(); RequireNoJournal();
            RemovePreparedRecord(Transition(update.Projects));
        }

        private void RemovePreparedRecord(JArray transition)
        {
            var path = PreparedPath(transition);
            if (!File.Exists(path)) return;
            ValidatePreparedRecord(Read(path), path);
            RequireOwner();
            File.Delete(path);
        }

        internal PreparedUpdate Rollback(IEnumerable<ResourceLocalSnapshot> current)
        {
            using var guard = Enter(false);
            RequireOwner(); RecoverMetadata(); RequireNoJournal();
            var from = Closure(current);
            var good = ReadIndex("last-known-good.json") ?? throw new ResourceException(ResourceError.AddressNotFound, "No last-known-good closure is available.");
            var entries = OpenIndex(good).Select(x => new UpdateProject(from[x.Document.ProjectId], x, Array.Empty<UpdateFile>())).ToList();
            if (entries.All(x => !x.IsChanged)) throw new ResourceException(ResourceError.AddressNotFound, "No distinct complete last-known-good version is available.");
            return new PreparedUpdate(RootPath, entries, true);
        }

        internal VersionCleanupResult Cleanup(IEnumerable<ResourceLocalSnapshot> held)
        {
            using var guard = Enter(false);
            if (!ValidateOwner()) return new VersionCleanupResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
            RecoverMetadata(); RequireNoJournal();
            var protectedPaths = new HashSet<string>(held.Select(x => x.DirectoryPath), StringComparer.OrdinalIgnoreCase);
            var deleted = new List<string>(); var errors = new List<string>();
            foreach (var name in new[] { "active.json", "last-known-good.json" })
            {
                var index = ReadIndex(name);
                if (index != null) foreach (var snapshot in OpenIndex(index)) protectedPaths.Add(snapshot.DirectoryPath);
            }
            foreach (var prepared in PreparedRecords())
                foreach (JObject entry in ReadTransition(prepared["Transition"]))
                    foreach (var field in new[] { "From", "To" }) protectedPaths.Add(VersionPath((string)entry["ProjectId"], (string)entry[field]));
            foreach (var mount in Projects)
            {
                var root = ProjectRoot(mount.ProjectId);
                foreach (var path in Directory.GetDirectories(Path.Combine(root, "versions")))
                {
                    if (!File.Exists(Path.Combine(path, VersionFile))) continue; // 未知目录不是本系统清理对象。
                    try
                    {
                        if (protectedPaths.Contains(path)) continue;
                        var marker = Read(Path.Combine(path, VersionFile));
                        ValidateVersionMarker(marker, mount.ProjectId, Path.GetFileName(path));
                        DeleteKnownDirectory(path, VersionFile, marker, RegisteredFiles(marker));
                        verified.Remove(path);
                        deleted.Add(path);
                    }
                    catch (Exception error) when (IsFileFailure(error)) { errors.Add(path + ": " + error.Message); }
                }
                foreach (var path in Directory.GetDirectories(Path.Combine(root, "staging")))
                {
                    const string stageFile = ".ylr-stage.json";
                    if (!File.Exists(Path.Combine(path, stageFile))) continue;
                    try
                    {
                        var marker = Read(Path.Combine(path, stageFile));
                        ValidateStage(marker, mount.ProjectId, Path.GetFileName(path));
                        DeleteKnownDirectory(path, stageFile, marker, RegisteredFiles(marker).Concat(new[] { VersionFile, VersionFile + ".next", stageFile + ".next" }));
                        deleted.Add(path);
                    }
                    catch (Exception error) when (IsFileFailure(error)) { errors.Add(path + ": " + error.Message); }
                }
            }
            return new VersionCleanupResult(deleted, protectedPaths, errors);
        }

        private void ValidateStage(JObject marker, string project, string attempt)
        {
            ResourceCatalogCodec.Fields(marker, "Version,OwnerId,ProjectId,Platform,CatalogDigest,Attempt,Files");
            if (ResourceCatalogCodec.Integer(marker, "Version") != 1 || ResourceCatalogCodec.Text(marker, "OwnerId") != ownerId ||
                ResourceCatalogCodec.Text(marker, "ProjectId") != project || ResourceCatalogCodec.Text(marker, "Platform") != mounts[project].Platform ||
                ResourceCatalogCodec.Text(marker, "Attempt") != attempt) throw new InvalidDataException("Unknown staging ownership.");
            ResourceCatalogCodec.Hex(attempt, 32);
            ResourceCatalogCodec.Hex(ResourceCatalogCodec.Text(marker, "CatalogDigest"), 64);
            RegisteredFiles(marker);
        }

        private void DeleteKnownDirectory(string path, string markerName, JObject marker, IEnumerable<string> files)
        {
            RequireOwner(); ResourceLocalSnapshot.RejectLinks(path);
            var allowed = new HashSet<string>(files.Append(markerName), StringComparer.Ordinal);
            var entries = Directory.GetFileSystemEntries(path);
            if (entries.Any(x => !File.Exists(x) || !allowed.Contains(Path.GetFileName(x))))
                throw new InvalidDataException("Owned cleanup encountered an unknown entry; nothing in this directory was removed.");
            foreach (var entry in entries) ResourceLocalSnapshot.RejectLinks(entry);
            // marker 最后删除。文件占用造成中断后，不要求残余目录重新成为完整可加载版本才能续清。
            foreach (var name in allowed.Where(x => x != markerName).OrderBy(x => x, StringComparer.Ordinal).Append(markerName))
            {
                RequireOwner();
                if (!JToken.DeepEquals(Read(Path.Combine(path, markerName)), marker)) throw new InvalidDataException("Cleanup ownership changed.");
                var file = Path.Combine(path, name);
                ResourceLocalSnapshot.RejectLinks(file);
                if (File.Exists(file)) File.Delete(file);
            }
            Directory.Delete(path);
        }
    }
}
