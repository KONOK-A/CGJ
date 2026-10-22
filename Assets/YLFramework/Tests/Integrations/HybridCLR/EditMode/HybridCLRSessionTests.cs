using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Integrations.HybridCLR;
using YLFramework.Services.Resource;
using YLFramework.Core;
using UnityEngine.SceneManagement;

namespace YLFramework.Tests.Integrations.HybridCLR
{
    public class HybridCLRSessionTests
    {
        private SceneSetup[] originalScenes;
        private string ownedFolder;
        private bool changedOptions;
        private bool oldEnabled;
        private EnterPlayModeOptions oldOptions;
        private HybridCLRLoader firstLoader;
        private HybridCLRLoader secondLoader;

        [UnityTest]
        public IEnumerator PendingModuleBecomesTerminalWhenTheRealPlaySessionEnds()
        {
            PrepareOwnedScene();
            yield return new EnterPlayMode();
            var resources = Initialize(); firstLoader = new HybridCLRLoader(resources, "base-test");
            CoreResourceAccess.Bind(new WaitingProvider());
            var address = new ResourceAddress("hybridclr-test", HybridCLRManifest.ManifestPath);
            var request = firstLoader.StartModuleAsync(address, "YLFramework.HybridCLR.Tests.PlayMode", "YLFramework.Tests.Integrations.HybridCLR.HybridCLRWaitingModule");
            for (var i = 0; i < 240 && request.Module == null && !request.isDone; i++) yield return null;
            Assert.That(request.Module, Is.Not.Null); Assert.That(request.isDone, Is.False);
            yield return new ExitPlayMode();
            Assert.That(request.isDone, Is.True, "A stopped Core coroutine must not leave its accepted wrapper pending forever.");
            Assert.That(request.Exception, Is.TypeOf<InvalidOperationException>());
            yield return new EnterPlayMode();
            var next = Initialize(); firstLoader.Dispose(); firstLoader = null;
            Assert.That(next.IsAvailable, Is.True);
            using var stop = next.ShutdownAsync(); yield return stop; stop.TakeResult();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DisabledDomainReloadStartsANewResourceSessionAndRejectsTheOldPublicLoader()
        {
            PrepareOwnedScene();
            yield return new EnterPlayMode();
            var first = Initialize(); firstLoader = new HybridCLRLoader(first, "base-test");
            var address = new ResourceAddress("hybridclr-test", HybridCLRManifest.ManifestPath);
            var firstLoad = firstLoader.LoadAsync(address); yield return firstLoad;
            Assert.That(firstLoad.Exception, Is.Null, firstLoad.error); Assert.That(firstLoad.Result.Simulation, Is.True);
            Assert.That(first.GetDiagnosticsSnapshot().LeaseCount, Is.EqualTo(1));

            // 刻意不正常 Stop，以真实退出 Play 验证会话失效；不宣称托管程序集已经卸载。
            yield return new ExitPlayMode();
            yield return new EnterPlayMode();
            Assert.That(first.IsAvailable, Is.False);
            Assert.Throws<InvalidOperationException>(() => firstLoader.LoadAsync(address));
            var second = Initialize(); secondLoader = new HybridCLRLoader(second, "base-test");
            firstLoader.Dispose(); firstLoader = null;
            Assert.That(second.IsAvailable, Is.True, "Disposing a stale loader must not close the current authority.");
            var secondLoad = secondLoader.LoadAsync(address); yield return secondLoad;
            Assert.That(secondLoad.Exception, Is.Null, secondLoad.error); Assert.That(secondLoad.Result.Simulation, Is.True);
            Assert.That(second.GetDiagnosticsSnapshot().LeaseCount, Is.EqualTo(1));
            secondLoader.Dispose(); secondLoader = null;
            using var shutdown = second.ShutdownAsync(); yield return shutdown;
            Assert.That(shutdown.Status, Is.EqualTo(ResourceRequestStatus.Succeeded), shutdown.Error?.ToString());
            yield return new ExitPlayMode();
        }

        private void PrepareOwnedScene()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            originalScenes = EditorSceneManager.GetSceneManagerSetup();
            if (originalScenes.Any(s => string.IsNullOrEmpty(s.path)) || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save your own scenes explicitly before this real cross-Play test.");
            HybridCLRTestFixtures.Prepare();
            ownedFolder = "Assets/YLFramework/Tests/Fixtures/HybridCLRSession-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Fixtures", System.IO.Path.GetFileName(ownedFolder));
            var neutral = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(neutral, ownedFolder + "/Neutral.unity"), Is.True);
            oldEnabled = EditorSettings.enterPlayModeOptionsEnabled; oldOptions = EditorSettings.enterPlayModeOptions; changedOptions = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSettings.enterPlayModeOptionsEnabled = true;

        }

        private static ResourceSystem Initialize()
        {
            Assert.That(ResourceSystem.Current, Is.Null);
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "YLFramework.HybridCLR.Tests.PlayMode");
            var name = assembly.GetName().Name;
            var entries = new[]
            {
                new ResourceCatalogEntry("manifest", new ResourceAddress("hybridclr-test", HybridCLRManifest.ManifestPath),
                    new ResourceLocation(ResourceSourceId.Resources, "YLHybridCLRTests/Good", typeof(TextAsset))),
                new ResourceCatalogEntry("code", new ResourceAddress("hybridclr-test", HybridCLRManifest.PayloadAddress(name, false)),
                    new ResourceLocation(ResourceSourceId.Resources, "YLHybridCLRTests/Entry", typeof(TextAsset)))
            };
            using var request = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[] { new ResourceCatalog("hybridclr-test", entries) }));
            return request.TakeResult();
        }

        [UnityTearDown]
        public IEnumerator RestoreOnlyOwnedState()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            try { firstLoader?.Dispose(); secondLoader?.Dispose(); }
            finally
            {
                if (changedOptions)
                {
                    EditorSettings.enterPlayModeOptions = oldOptions;
                    EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
                }
                if (originalScenes != null && originalScenes.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
                if (ownedFolder != null && AssetDatabase.IsValidFolder(ownedFolder)) AssetDatabase.DeleteAsset(ownedFolder);
            }
        }

        private sealed class WaitingProvider : ICoreResourceProvider
        {
            public CoreResourceRequest<string> ReadTextAsync(string projectId, string address) => throw new NotSupportedException();
            public CorePrefabLease AcquirePrefab(string projectId, string address) => throw new NotSupportedException();
            public CoreResourceRequest<CorePrefabLease> AcquirePrefabAsync(string projectId, string address) => throw new NotSupportedException();
            public CoreResourceRequest<CoreSceneResult> LoadSceneAsync(string projectId, string address, LoadSceneMode mode) => new WaitingScene();
        }
        private sealed class WaitingScene : CoreResourceRequest<CoreSceneResult>
        {
            private bool disposed;
            public override CoreResourceStatus Status => IsDisposed ? CoreResourceStatus.Canceled : CoreResourceStatus.Pending;
            public override float Progress => 0;
            public override CoreResourceException Error => null;
            public override bool IsDisposed => disposed;
            public override event Action<CoreResourceRequest<CoreSceneResult>> Completed { add { } remove { } }
            public override CoreSceneResult TakeResult() => throw new InvalidOperationException("Deliberately pending test scene.");
            public override void Cancel() => Dispose();
            public override void Dispose() => disposed = true;
        }
    }
}
