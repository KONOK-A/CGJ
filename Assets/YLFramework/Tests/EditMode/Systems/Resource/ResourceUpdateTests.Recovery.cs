using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using YLFramework.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public partial class ResourceUpdateTests
    {
        [Test]
        public void RollbackPreservesTheWholePreviousClosureIncludingUnchangedProjects()
        {
            var store = NewStore(true);
            store.Commit(Prepare(store, 2, true));
            store.Commit(Prepare(store, 3, true));
            store.Commit(store.Rollback(store.Mount()));
            var mounted = Store(store.RootPath, true).Mount();
            foreach (var project in new[] { "a", "b" })
            {
                Assert.That(mounted.Single(x => x.Document.ProjectId == Snapshot(project).Document.ProjectId).Document.CatalogDigest,
                    Is.EqualTo(Snapshot(project, 2).Document.CatalogDigest));
            }
        }

        [Test]
        public void StartupFallsBackAsAWholeClosureAfterOneActiveProjectIsCorrupt()
        {
            var store = NewStore(true);
            store.Commit(Prepare(store, 2, true));
            File.WriteAllBytes(Path.Combine(VersionPath(store, 2), Snapshot("a", 2).Document.Bundles[0].RelativeFile), new byte[] { 1 });
            var mounted = Store(store.RootPath, true).Mount();
            foreach (var project in new[] { "a", "b" })
            {
                Assert.That(mounted.Single(x => x.Document.ProjectId == Snapshot(project).Document.ProjectId).Document.CatalogDigest,
                    Is.EqualTo(Snapshot(project).Document.CatalogDigest), "Do not combine fallback A1 with active B2.");
            }
        }

        [Test]
        public void CommitAndPromotionRecheckLiveOwnershipAndJournalBeforeWriting()
        {
            foreach (var removed in new[] { ".ylr-store-owner.json", "commit-journal.json" })
            {
                var store = NewStore();
                var prepared = Prepare(store);
                var ownerPath = Path.Combine(store.RootPath, ".ylr-store-owner.json");
                var owner = File.ReadAllBytes(ownerPath);
                store.Checkpoint = stage =>
                {
                    if (stage == "JournalVerified")
                    {
                        File.Delete(removed == "commit-journal.json" ? ProjectPath(store, removed) : ownerPath);
                    }
                };
                Assert.Throws<InvalidDataException>(() => store.Commit(prepared), removed);
                Assert.That(File.Exists(ProjectPath(store)), Is.False, removed);
                Assert.That(File.Exists(ProjectPath(store, "last-known-good.json")), Is.False, removed);
                File.WriteAllBytes(ownerPath, owner);
            }
            var promotion = NewStore();
            byte[] savedOwner = null;
            promotion.Checkpoint = stage =>
            {
                if (stage != "CompleteWritten")
                {
                    return;
                }

                var path = Path.Combine(promotion.RootPath, ".ylr-store-owner.json");
                savedOwner = File.ReadAllBytes(path);
                File.Delete(path);
            };
            Assert.Throws<InvalidDataException>(() => Prepare(promotion));
            Assert.That(Directory.GetDirectories(ProjectPath(promotion, "versions")), Is.Empty);
            File.WriteAllBytes(Path.Combine(promotion.RootPath, ".ylr-store-owner.json"), savedOwner);
        }

        [Test]
        public void TruncatedOwnedReplacementCandidatesCannotOverrideOrBlockAValidJournal()
        {
            var store = NewStore();
            var prepared = Prepare(store);
            store.Checkpoint = stage =>
            {
                if (stage == "JournalVerified")
                {
                    throw new IOException("interrupted");
                }
            };
            Assert.Throws<IOException>(() => store.Commit(prepared));
            foreach (var name in new[] { "commit-journal.json", "active.json", "last-known-good.json" })
            {
                File.WriteAllText(ProjectPath(store, name) + ".next", "{\"Version\":");
            }

            Assert.That(Store(store.RootPath).Mount().Single().Document.CatalogDigest, Is.EqualTo(Snapshot().Document.CatalogDigest));
            foreach (var name in new[] { "commit-journal.json", "active.json", "last-known-good.json" })
            {
                Assert.That(File.Exists(ProjectPath(store, name) + ".next"), Is.False);
            }
        }

        [Test]
        public void PreparedClosureIsPublishedOnlyWhenAllProjectsFinishAndOverlappingPlansRemainRecoverable()
        {
            var store = NewStore(true);
            using var cancellation = new CancellationTokenSource();
            var installed = 0;
            store.Checkpoint = stage =>
            {
                if (stage == "VersionInstalled" && ++installed == 2)
                {
                    cancellation.Cancel();
                }
            };
            var plan = store.Check(store.Mount(), new[] { Snapshot("a", 2), Snapshot("b", 2) });
            Assert.Throws<OperationCanceledException>(() => store.Prepare(plan, cancellation.Token));
            Assert.That(Store(store.RootPath, true).GetPreparedUpdates(), Is.Empty, "A complete individual version is not a complete prepared closure.");
            store.Checkpoint = null;
            Prepare(store, 2, true);
            Prepare(store, 3, true);
            var restored = Store(store.RootPath, true).GetPreparedUpdates();
            Assert.That(restored.Count, Is.EqualTo(2));
            Assert.That(restored.SelectMany(x => x.Projects).Select(x => x.ToDigest), Does.Contain(Snapshot("a", 2).Document.CatalogDigest));
            Assert.That(restored.SelectMany(x => x.Projects).Select(x => x.ToDigest), Does.Contain(Snapshot("a", 3).Document.CatalogDigest));
        }

        [Test]
        public void OwnedVersionMarkerReplacementIsRecoveredBeforeStrictSnapshotOpening()
        {
            var store = NewStore();
            store.Commit(Prepare(store));
            var path = Path.Combine(VersionPath(store, 2), ".ylr-version.json");
            File.Copy(path, path + ".next");
            Assert.That(Store(store.RootPath).Mount().Single().Document.CatalogDigest, Is.EqualTo(Snapshot("a", 2).Document.CatalogDigest));
            Assert.That(File.Exists(path + ".next"), Is.False);
        }

        [Test]
        public void ForeignReplacementIsRejectedWithoutDeletingItOrChangingActive()
        {
            var store = NewStore();
            var prepared = Prepare(store);
            store.Checkpoint = stage =>
            {
                if (stage == "JournalVerified")
                {
                    throw new IOException("interrupted");
                }
            };
            Assert.Throws<IOException>(() => store.Commit(prepared));
            var candidate = JObject.Parse(File.ReadAllText(ProjectPath(store, "commit-journal.json")));
            candidate["OwnerId"] = new string('f', 32);
            var path = ProjectPath(store, "commit-journal.json") + ".next";
            File.WriteAllText(path, candidate.ToString());
            var before = File.ReadAllBytes(path);
            Assert.Throws<InvalidDataException>(() => Store(store.RootPath).Mount());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.That(File.Exists(ProjectPath(store)), Is.False);
        }

        [Test]
        public void DiscardedPreparedUpdateCannotBeUsedAsAnUnprotectedActivationToken()
        {
            var store = NewStore();
            var prepared = Prepare(store);
            store.DiscardPreparedUpdate(prepared);
            Assert.That(store.GetPreparedUpdates(), Is.Empty);
            Assert.Throws<InvalidDataException>(() => store.Commit(prepared));
            Assert.That(File.Exists(ProjectPath(store)), Is.False);
            store.Commit(Prepare(store));
            Assert.That(store.Mount().Single().Document.CatalogDigest, Is.EqualTo(Snapshot("a", 2).Document.CatalogDigest));
        }

        [Test]
        public void FallbackFromAnInvalidPointerCanBeFollowedByANormalUpdate()
        {
            var store = NewStore();
            store.Commit(Prepare(store));
            File.WriteAllText(ProjectPath(store), "broken active pointer");
            store = Store(store.RootPath);
            Assert.That(store.Mount().Single().Document.CatalogDigest, Is.EqualTo(Snapshot().Document.CatalogDigest));
            store.Commit(Prepare(store, 3));
            Assert.That(Store(store.RootPath).Mount().Single().Document.CatalogDigest, Is.EqualTo(Snapshot("a", 3).Document.CatalogDigest));
        }

        [Test]
        public void CleanupReclaimsCanceledStagesAndCanContinueAfterADeletionFailure()
        {
            var store = NewStore();
            using var cancellation = new CancellationTokenSource();
            store.Checkpoint = stage =>
            {
                if (stage == "FileCopied")
                {
                    cancellation.Cancel();
                }
            };
            Assert.Throws<OperationCanceledException>(() => store.Prepare(store.Check(store.Mount(), new[] { Snapshot("a", 2) }), cancellation.Token));
            store.Checkpoint = null;
            var staging = ProjectPath(store, "staging");
            var unknown = Path.Combine(staging, "foreign");
            Directory.CreateDirectory(unknown);
            File.WriteAllText(Path.Combine(unknown, "mine"), "keep");
            Assert.That(store.Cleanup(store.Mount()).HasSucceeded, Is.True);
            CollectionAssert.AreEqual(new[] { unknown }, Directory.GetDirectories(staging));
            store.Commit(Prepare(store));
            store.Commit(Prepare(store, 3));
            var obsolete = VersionPath(store, 1);
            using (var locked = new FileStream(Path.Combine(obsolete, Snapshot().Document.Bundles.Last().RelativeFile), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.That(store.Cleanup(store.Mount()).HasSucceeded, Is.False);
            }

            var retry = store.Cleanup(store.Mount());
            Assert.That(retry.HasSucceeded, Is.True, string.Join("\n", retry.Errors));
            Assert.That(Directory.Exists(obsolete), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(unknown, "mine")), Is.EqualTo("keep"));
        }

        [Test]
        public void RequiredSpaceIncludesTheActualLargeBuiltInCatalogAndCountsEachVersionOnce()
        {
            var source = Snapshot();
            var doc = source.Document;
            var payload = ResourceCatalogCodec.ParseObject(ResourceCatalogCodec.Write(doc));
            payload.Remove("BuildId");
            ((JArray)payload["Assets"]).OfType<JObject>().Single(asset => (string)asset["Kind"] == "Asset" && (string)asset["Address"] == "entry.txt")["Aliases"] =
                new JArray(Enumerable.Range(0, 4096).Select(n => "large-explicit-alias-" + n.ToString("D5")));
            var bytes = ResourceCatalogCodec.Seal(ResourceCatalogCodec.CanonicalBytes(payload));
            var large = ResourceCatalogCodec.Read(bytes, doc.ProjectId, doc.Platform, doc.UnityCompatibility, doc.AppContentCompatibilityId);
            var sourceRoot = NewStore().RootPath;
            var path = Path.Combine(sourceRoot, large.CatalogDigest);
            Directory.CreateDirectory(path);
            foreach (var file in Directory.GetFiles(source.DirectoryPath))
            {
                File.Copy(file, Path.Combine(path, Path.GetFileName(file)));
            }

            File.WriteAllBytes(Path.Combine(path, "resource-catalog.json"), ResourceCatalogCodec.Write(large));
            var complete = JObject.Parse(File.ReadAllText(Path.Combine(path, ".ylr-complete.json")));
            complete["BuildId"] = large.BuildId;
            complete["CatalogDigest"] = large.CatalogDigest;
            File.WriteAllText(Path.Combine(path, ".ylr-complete.json"), complete.ToString());
            var builtIn = ResourceLocalSnapshot.Open(path, large.ProjectId, large.CatalogDigest, large.Platform, large.AppContentCompatibilityId);
            var store = new ArtifactStore(NewStore().RootPath, new[] { new ResourceProjectMount(large.ProjectId, large.Platform, large.AppContentCompatibilityId, builtIn) });
            var expected = Directory.GetFiles(path).Sum(x => new FileInfo(x).Length) + Directory.GetFiles(Snapshot("a", 2).DirectoryPath).Sum(x => new FileInfo(x).Length);
            Assert.That(new FileInfo(Path.Combine(path, "resource-catalog.json")).Length, Is.GreaterThan(65536));
            Assert.That(store.Check(store.Mount(), new[] { Snapshot("a", 2) }).RequiredBytes, Is.GreaterThanOrEqualTo(expected));
            var same = store.Check(store.Mount(), new[] { builtIn });
            Assert.That(same.RequiredBytes, Is.LessThan(expected), "The same from/to version is installed only once.");
        }
    }
}
