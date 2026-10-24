using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public partial class ResourceUpdateTests
    {
        private static ArtifactStoreReport QueryArtifacts(ArtifactStore store)
        {
            ArtifactStoreReport report = null;
            var routine = new ResourceConfiguration(store).QueryArtifactsAsync(value => report = value);
            try
            {
                while (routine.MoveNext())
                {
                }
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
            }
            Assert.That(report, Is.Not.Null);
            return report;
        }

        [UnityTest]
        public IEnumerator ArtifactQueriesObserveRealHeldAndInFlightVersionsWithoutPinsAndCleanupRechecksOldPreviews()
        {
            yield return new EnterPlayMode();
            yield return VerifyArtifactQueries();
        }

        // 捕获闭包只在进入 Play 后创建，避免 TestRunner 跨 Domain Reload 序列化它。
        private static IEnumerator VerifyArtifactQueries()
        {
            var store = NewStore(true);
            yield return Initialize(store);
            var system = ResourceSystem.Current;
            Assert.That(Take(system.ActivatePreparedUpdateAsync(Prepare(system, 2, true))), Is.True);
            Assert.That(Take(system.ActivatePreparedUpdateAsync(Prepare(system, 3, true))), Is.True);
            var obsolete = VersionPath(store, 1);
            var preview = QueryArtifacts(store);
            Assert.That(preview.Status, Is.EqualTo(ArtifactQueryStatus.Ready), string.Join("\n", preview.Issues));
            Assert.That(preview.Entries.Single(x => x.Path == obsolete).IsCleanupCandidate, Is.True);
            Prepare(system, 1, true);
            using var text = system.Acquire<TextAsset>(Address());
            using var loading = system.AcquireAsync<GameObject>(Address("B.prefab"));
            var before = system.GetDiagnosticsSnapshot();
            var files = ResourceReleaseFixture.Snapshot(store.RootPath);
            var report = QueryArtifacts(store);
            Assert.That(report.Status, Is.EqualTo(ArtifactQueryStatus.Ready), string.Join("\n", report.Issues));
            var active = report.Entries.Single(x => x.Path == VersionPath(store, 3));
            Assert.That(active.Protection.HasFlag(ArtifactProtection.Active | ArtifactProtection.Mounted | ArtifactProtection.Held), Is.True);
            Assert.That(report.ProtectedVersions.Any(x => x.Reasons.HasFlag(ArtifactProtection.InFlight)), Is.True);
            var after = system.GetDiagnosticsSnapshot();
            Assert.That(after.LeaseCount, Is.EqualTo(before.LeaseCount));
            Assert.That(after.BundleRootPinCount, Is.EqualTo(before.BundleRootPinCount));
            Assert.That(after.BundleRecordCount, Is.EqualTo(before.BundleRecordCount));
            CollectionAssert.AreEqual(files, ResourceReleaseFixture.Snapshot(store.RootPath));
            var cleanup = Take(system.CleanupVersionsAsync());
            Assert.That(cleanup.ProtectedVersions, Does.Contain(obsolete));
            Assert.That(Directory.Exists(obsolete), Is.True, "A prior candidate snapshot must not authorize deletion after Prepare changes protection.");
            loading.Cancel();
            loading.Dispose();
            text.Dispose();
            yield return Drain(system);
            ArtifactStoreReport changed = null;
            var pending = store.QueryAsync(value => changed = value);
            Assert.That(pending.MoveNext(), Is.True);
            Assert.That(Take(system.ShutdownAsync()), Is.True);
            try
            {
                while (pending.MoveNext())
                {
                }
            }
            finally
            {
                (pending as IDisposable)?.Dispose();
            }
            Assert.That(changed.Status, Is.EqualTo(ArtifactQueryStatus.Changed));
            Assert.That(changed.CleanupCandidateBytes, Is.Null);
            Assert.That(ResourceSystem.Current, Is.Null);
        }
    }
}
