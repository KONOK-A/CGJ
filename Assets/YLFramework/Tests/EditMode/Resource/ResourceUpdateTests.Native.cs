using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public partial class ResourceUpdateTests
    {
        private static T Take<T>(ResourceRequest<T> request)
        {
            using (request)
            {
                Assert.That(request.Status, Is.EqualTo(ResourceRequestStatus.Succeeded), request.Error?.ToString());
                return request.TakeResult();
            }
        }

        private static ResourceSystem Initialize(ArtifactStore store)
        {
            var system = Take(ResourceSystem.InitializeAsync(new ResourceConfiguration(store)));
            Assert.That(system, Is.Not.Null, "Successful initialization must return the unique resource authority.");
            return system;
        }
        private static ResourceAddress Address(string path = "entry.txt", string project = "a") => new(Snapshot(project).Document.ProjectId, path);
        private static PreparedUpdate Prepare(ResourceSystem system, int version = 2, bool multi = false) => Take(system.PrepareUpdateAsync(
            Take(system.CheckUpdatesAsync(multi ? new[] { Snapshot("a", version), Snapshot("b", Math.Min(version, 2)) } : new[] { Snapshot("a", version) }))));

        private static void Busy(ResourceRequest<bool> request)
        {
            using (request) Assert.That(request.Error?.Kind, Is.EqualTo(ResourceError.Busy), request.Error?.ToString());
            Assert.That(ResourceSystem.Current.IsAvailable, Is.True);
            using var old = ResourceSystem.Current.Acquire<TextAsset>(Address());
            Assert.That(old.Asset.text, Is.EqualTo("first release content"));
        }

        private static IEnumerator Wait<T>(ResourceRequest<T> request)
        {
            var deadline = Time.realtimeSinceStartup + 10;
            while (!request.IsDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.IsDone, Is.True, "A native operation did not settle within the fixture deadline.");
        }

        private static IEnumerator Drain(ResourceSystem system)
        {
            var deadline = Time.realtimeSinceStartup + 10;
            while (system.GetDiagnosticsSnapshot().BundleRecordCount != 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(system.GetDiagnosticsSnapshot().BundleRecordCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NativeAssetInstanceSceneAndCanceledOperationBlockUntilNeutralBoundaryThenLoadNewVersionAndRollback()
        {
            yield return new EnterPlayMode();
            var store = NewStore();
            var system = Initialize(store);
            var update = Prepare(system);
            var oldLease = system.Acquire<TextAsset>(Address());
            var oldObject = oldLease.Asset;
            Busy(system.ActivatePreparedUpdateAsync(update));
            oldLease.Dispose();
            Assert.That(oldObject == null, Is.True, "The old native asset must actually be destroyed before switching generations.");

            var instance = system.Instantiate(Address("A.prefab"));
            var liveObject = instance.GameObject;
            Busy(system.ActivatePreparedUpdateAsync(update));
            Assert.That(liveObject, Is.Not.Null, "Activation cannot destroy an external consumer's checkout.");
            instance.Return();
            Busy(system.ActivatePreparedUpdateAsync(update));
            yield return Drain(system);

            using var sceneRequest = system.LoadSceneAsync(Address("Owned.unity"), LoadSceneMode.Additive);
            yield return Wait(sceneRequest);
            var sceneLease = sceneRequest.TakeResult();
            var scene = sceneLease.Scene;
            sceneLease.Dispose();
            Busy(system.ActivatePreparedUpdateAsync(update));
            Assert.That(scene.isLoaded, Is.True);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return Drain(system);

            using var native = system.AcquireAsync<GameObject>(Address("B.prefab"));
            native.Cancel();
            Busy(system.ActivatePreparedUpdateAsync(update));
            yield return Drain(system);
            Assert.That(native.Status, Is.EqualTo(ResourceRequestStatus.Canceled));

            var provider = system.BindCoreResourceProvider();
            using (var prefab = provider.AcquirePrefab(Snapshot().Document.ProjectId, "A.prefab"))
                Busy(system.ActivatePreparedUpdateAsync(update));
            yield return Drain(system);
            Assert.That(Take(system.ActivatePreparedUpdateAsync(update)), Is.True);
            Assert.That(system.GetActiveCatalogDigest(Snapshot().Document.ProjectId), Is.EqualTo(Snapshot("a", 2).Document.CatalogDigest));
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("release87-a-v2"));
            var active = File.ReadAllBytes(ProjectPath(store));
            store.Checkpoint = _ => Assert.Fail("already-active cannot perform another persistent transaction.");
            Assert.That(Take(system.ActivatePreparedUpdateAsync(update)), Is.True);
            CollectionAssert.AreEqual(active, File.ReadAllBytes(ProjectPath(store)));
            store.Checkpoint = null;
            using (var text = system.Acquire<TextAsset>(Address()))
            {
                using var busy = system.RollbackAsync();
                Assert.That(busy.Error?.Kind, Is.EqualTo(ResourceError.Busy));
                Assert.That(text.Asset.text, Is.EqualTo("release87-a-v2"));
            }
            Assert.That(Take(system.RollbackAsync()), Is.True);
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("first release content"));
            Assert.That(Take(system.ShutdownAsync()), Is.True);
        }

        [UnityTest]
        public IEnumerator CommitFailureRestoresOldAcquireAndPostDecisionFailureReconcilesNewMemoryAndDisk()
        {
            yield return new EnterPlayMode();
            VerifyCommitRecovery();
        }

        // 不让 TestRunner 跨 Domain Reload 序列化编译器生成的捕获闭包。
        private static void VerifyCommitRecovery()
        {
            var store = NewStore();
            var system = Initialize(store);
            var update = Prepare(system);
            var hit = false;
            var blocked = new System.Collections.Generic.Dictionary<string, ResourceError?>();
            store.Checkpoint = stage =>
            {
                if (stage != "JournalVerified" || hit) return;
                hit = true;
                using var asset = system.AcquireAsync<TextAsset>(Address());
                blocked["asset"] = asset.Error?.Kind;
                using var scene = system.LoadSceneAsync(Address("Owned.unity"), LoadSceneMode.Additive);
                blocked["scene"] = scene.Error?.Kind;
                using var group = system.AcquireGroupAsync(Snapshot().Document.ProjectId, "entry");
                blocked["group"] = group.Error?.Kind;
                using var reentry = system.ActivatePreparedUpdateAsync(update);
                blocked["reentry"] = reentry.Error?.Kind;
                using var shutdown = system.ShutdownAsync();
                blocked["shutdown"] = shutdown.Error?.Kind;
                throw new IOException("injected pre-commit interruption");
            };
            using (var failed = system.ActivatePreparedUpdateAsync(update)) Assert.That(failed.Error?.Kind, Is.EqualTo(ResourceError.UpdateFailed));
            Assert.That(hit, Is.True);
            Assert.That(blocked.Count, Is.EqualTo(5));
            foreach (var pair in blocked) Assert.That(pair.Value, Is.EqualTo(ResourceError.Busy), pair.Key);
            using (var old = system.Acquire<TextAsset>(Address())) Assert.That(old.Asset.text, Is.EqualTo("first release content"));
            Assert.That(File.Exists(ProjectPath(store)), Is.False);
            hit = false;
            store.Checkpoint = stage => { if (stage == "CommitDecided" && !hit) { hit = true; throw new IOException("injected post-decision interruption"); } };
            Assert.That(Take(system.ActivatePreparedUpdateAsync(update)), Is.True);
            Assert.That(hit, Is.True);
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("release87-a-v2"));
            Assert.That(Store(store.RootPath).Mount().Single().Document.CatalogDigest, Is.EqualTo(system.GetActiveCatalogDigest(Snapshot().Document.ProjectId)));
            store.Checkpoint = null;
            var third = Prepare(system, 3);
            store.Checkpoint = stage =>
            {
                if (stage == "CommitDecided") File.WriteAllBytes(Path.Combine(VersionPath(store, 3), Snapshot("a", 3).Document.Bundles[0].RelativeFile), new byte[] { 1 });
            };
            using (var failed = system.ActivatePreparedUpdateAsync(third)) Assert.That(failed.Status, Is.EqualTo(ResourceRequestStatus.Failed),
                "Recovering From after target corruption must not report successful activation of To.");
            Assert.That(system.GetActiveCatalogDigest(Snapshot().Document.ProjectId), Is.EqualTo(Snapshot("a", 2).Document.CatalogDigest));
            store.Checkpoint = null;
            File.WriteAllBytes(Path.Combine(VersionPath(store, 2), Snapshot("a", 2).Document.Bundles[0].RelativeFile), new byte[] { 1 });
            using (var failed = system.ActivatePreparedUpdateAsync(third)) Assert.That(failed.Status, Is.EqualTo(ResourceRequestStatus.Failed));
            Assert.That(system.GetActiveCatalogDigest(Snapshot().Document.ProjectId), Is.EqualTo(Store(store.RootPath).Mount().Single().Document.CatalogDigest),
                "Failure recovery must reconcile memory with the complete fallback closure too, not only with a successful target.");
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("first release content"));
            Assert.That(Take(system.ShutdownAsync()), Is.True);
        }

        [UnityTest]
        public IEnumerator CompleteAndActiveSurviveActualDomainReloadAndStalePreparedCannotDowngrade()
        {
            yield return new EnterPlayMode();
            var store = NewStore();
            SessionState.SetString(Key + "restart", store.RootPath);
            var system = Initialize(store);
            Prepare(system);
            Assert.That(Take(system.ShutdownAsync()), Is.True);
            yield return new ExitPlayMode();
            yield return new EnterPlayMode();
            store = Store(SessionState.GetString(Key + "restart", ""));
            system = Initialize(store);
            var recovered = system.GetPreparedUpdates().Single();
            Assert.That(Take(system.ActivatePreparedUpdateAsync(recovered)), Is.True);
            var third = Prepare(system, 3);
            Assert.That(Take(system.ActivatePreparedUpdateAsync(third)), Is.True);
            using (var stale = system.ActivatePreparedUpdateAsync(recovered)) Assert.That(stale.Error?.Kind, Is.EqualTo(ResourceError.StaleBase));
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("release87-a-v3"));
            Assert.That(Take(system.ShutdownAsync()), Is.True);
            yield return new ExitPlayMode();
            yield return new EnterPlayMode();
            store = Store(SessionState.GetString(Key + "restart", ""));
            system = Initialize(store);
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("release87-a-v3"));
            Assert.That(Take(system.RollbackAsync()), Is.True);
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("release87-a-v2"));
            Assert.That(Take(system.ShutdownAsync()), Is.True);
        }

        [UnityTest]
        public IEnumerator ExplicitMultiProjectClosureNeverPartiallyActivatesOnBusyOrCorruptTarget()
        {
            yield return new EnterPlayMode();
            var store = NewStore(true);
            var system = Initialize(store);
            using (var incomplete = system.CheckUpdatesAsync(new[] { Snapshot("a", 2) }))
                Assert.That(incomplete.Status, Is.EqualTo(ResourceRequestStatus.Failed));
            var update = Prepare(system, 2, true);
            using (var pin = system.Acquire<TextAsset>(Address(project: "b"))) Busy(system.ActivatePreparedUpdateAsync(update));
            Assert.That(File.Exists(ProjectPath(store)), Is.False);
            Assert.That(File.Exists(ProjectPath(store, project: "b")), Is.False);
            var file = Path.Combine(VersionPath(store, 2, "b"), Snapshot("b", 2).Document.Bundles[0].RelativeFile);
            var bytes = File.ReadAllBytes(file);
            File.WriteAllBytes(file, new byte[] { 1 });
            using (var failed = system.ActivatePreparedUpdateAsync(update)) Assert.That(failed.Status, Is.EqualTo(ResourceRequestStatus.Failed));
            foreach (var project in new[] { "a", "b" }) Assert.That(system.GetActiveCatalogDigest(Snapshot(project).Document.ProjectId), Is.EqualTo(Snapshot(project).Document.CatalogDigest));
            File.WriteAllBytes(file, bytes);
            Assert.That(Take(system.ActivatePreparedUpdateAsync(update)), Is.True);
            foreach (var project in new[] { "a", "b" })
            {
                using var text = system.Acquire<TextAsset>(Address(project: project));
                Assert.That(text.Asset.text, Is.EqualTo("release87-" + project + "-v2"));
            }
            Assert.That(Take(system.RollbackAsync()), Is.True);
            Assert.That(Take(system.ShutdownAsync()), Is.True);
        }

        [UnityTest]
        public IEnumerator ExternalLoadedProjectBundleIsBusyAndIsNeverUnloadedOrAdoptedByActivation()
        {
            yield return new EnterPlayMode();
            VerifyExternalNativeGate();
        }

        private static void VerifyExternalNativeGate()
        {
            var store = NewStore();
            var system = Initialize(store);
            var update = Prepare(system);
            var snapshot = Snapshot();
            var asset = snapshot.Document.Assets.Single(x => x.Address == "A.prefab");
            var bundle = snapshot.Document.Bundles.Single(x => x.LogicalId == asset.BundleId);
            var external = AssetBundle.LoadFromFile(Path.Combine(snapshot.DirectoryPath, bundle.RelativeFile));
            Assert.That(external, Is.Not.Null);
            try
            {
                Busy(system.ActivatePreparedUpdateAsync(update));
                Assert.That(external, Is.Not.Null, "The barrier must not take ownership of an external native bundle.");
                Assert.That(system.GetDiagnosticsSnapshot().BundleRecordCount, Is.Zero);
            }
            finally { if (external) external.Unload(true); }
            Assert.That(Take(system.ActivatePreparedUpdateAsync(update)), Is.True);
            using (var text = system.Acquire<TextAsset>(Address())) Assert.That(text.Asset.text, Is.EqualTo("release87-a-v2"));
            Assert.That(Take(system.ShutdownAsync()), Is.True);
        }
    }
}
