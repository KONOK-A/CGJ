using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Core;
using YLFramework.Integrations.HybridCLR;
using YLFramework.Services.Resource;
using Module = YLFramework.Core.Module;

namespace YLFramework.Tests.Integrations.HybridCLR
{
    public sealed class HybridCLRProbeModule : Module
    {
        public static Action Starting;
        public override string ProjectName { get => "hybridclr-test"; set { } }
        public override void OnStart() => Starting?.Invoke();
    }

    public sealed class HybridCLRWaitingModule : Module
    {
        public override string ProjectName { get => "hybridclr-test"; set { } }
        public override string DefaultStartUpScene => "owned-pending-scene";
        public override void OnStart() { }
    }

    public class HybridCLRModuleTests
    {
        private HybridCLRLoaderTests fixture;
        private ICoreResourceProvider previousProvider;
        private ICoreResourceProvider ownedProvider;
        private PendingScene pendingScene;
        private Scene ownedScene;
        private HybridCLRModuleRequest ownedRequest;
        private int nativeCalls;
        private string Entry => typeof(HybridCLRProbeModule).Assembly.GetName().Name;

        [SetUp]
        public void SetUp()
        {
            previousProvider = CoreResourceAccess.Provider;
            ownedProvider = null; pendingScene = null; ownedScene = default; ownedRequest = null; nativeCalls = 0;
            fixture = new HybridCLRLoaderTests(); fixture.Initialize(); HybridCLRProbeModule.Starting = null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // RED 断言也要放行自己的场景等待，不遗留无法 Close 的 Starting Module。
            pendingScene?.Succeed(ownedScene);
            for (var i = 0; i < 240 && ownedRequest != null && !ownedRequest.isDone; i++) yield return null;
            var waiting = ModuleManager.GetModule(typeof(HybridCLRWaitingModule));
            if (waiting != null && ReferenceEquals(waiting, ownedRequest?.Module)) waiting.Close();
            var module = ModuleManager.GetModule(typeof(HybridCLRProbeModule));
            if (module != null) module.Close();
            HybridCLRProbeModule.Starting = null;
            CoreResourceAccess.Unbind(ownedProvider);
            yield return fixture.TearDown();
            if (ownedScene.IsValid() && ownedScene.isLoaded) yield return SceneManager.UnloadSceneAsync(ownedScene);
            if (ownedProvider != null && previousProvider != null && CoreResourceAccess.Provider == null)
                CoreResourceAccess.Bind(previousProvider);
        }

        [UnityTest]
        public IEnumerator AThrowingSharedLoadObserverCannotStrandModuleStartup()
        {
            var load = fixture.Loader.LoadAsync(HybridCLRLoaderTests.Address);
            Assert.That(load.isDone, Is.False, "This regression needs a real pending resource load.");
            LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("Intentional shared-load observer"));
            load.AddCompleteEvent(_ => throw new InvalidOperationException("Intentional shared-load observer"));
            var request = fixture.Loader.StartModuleAsync(HybridCLRLoaderTests.Address, Entry, typeof(HybridCLRProbeModule).FullName);
            for (var i = 0; i < 30 && !request.isDone; i++) yield return null;
            Assert.That(request.isDone, Is.True); Assert.That(request.Exception, Is.Null, request.error);
            Assert.That(request.Module.State, Is.EqualTo(ModuleState.Running));
        }

        [UnityTest]
        public IEnumerator RemovingPublicLoadObserversCannotRemoveInternalModuleContinuation()
        {
            var load = fixture.Loader.LoadAsync(HybridCLRLoaderTests.Address);
            Assert.That(load.isDone, Is.False);
            var request = fixture.Loader.StartModuleAsync(HybridCLRLoaderTests.Address, Entry, typeof(HybridCLRProbeModule).FullName);
            load.RemoveAllCompleteEvent();
            for (var i = 0; i < 30 && !request.isDone; i++) yield return null;
            Assert.That(request.isDone, Is.True); Assert.That(request.Exception, Is.Null, request.error);
        }

        [UnityTest]
        public IEnumerator ExactEntryTypeUsesCoreLifecycleAndBlocksDisposalUntilModuleIsClosed()
        {
            var request = fixture.Loader.StartModuleAsync(HybridCLRLoaderTests.Address, Entry, typeof(HybridCLRProbeModule).FullName);
            Assert.Throws<NotSupportedException>(() => request.Abort()); yield return request;
            Assert.That(request.Exception, Is.Null, request.error);
            Assert.That(request.Module.State, Is.EqualTo(ModuleState.Running));
            Assert.Throws<InvalidOperationException>(() => fixture.Loader.Dispose());
            request.Module.Close(); fixture.Loader.Dispose();
            Assert.That(fixture.ResourcesSystem.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator ModuleFailureKeepsTheOriginalExceptionAndCanRetryTheSameLoadedCode()
        {
            var failure = new InvalidOperationException("Intentional isolated Module failure.");
            HybridCLRProbeModule.Starting = () => throw failure;
            var failed = fixture.Loader.StartModuleAsync(HybridCLRLoaderTests.Address, Entry, typeof(HybridCLRProbeModule).FullName);
            yield return failed;
            Assert.That(failed.Exception, Is.SameAs(failure)); Assert.That(failed.RequiresRestart, Is.False);
            Assert.That(ModuleManager.GetModule(typeof(HybridCLRProbeModule)), Is.Null);
            HybridCLRProbeModule.Starting = null;
            var retry = fixture.Loader.StartModuleAsync(HybridCLRLoaderTests.Address, Entry, typeof(HybridCLRProbeModule).FullName); yield return retry;
            Assert.That(retry.Exception, Is.Null, retry.error);
            Assert.That(retry.Code, Is.SameAs(failed.Code));
        }

        [UnityTest]
        public IEnumerator MissingModuleTypeFinishesWithoutScanningOtherAssemblies()
        {
            var request = fixture.Loader.StartModuleAsync(HybridCLRLoaderTests.Address, Entry, "Definitely.Not.A.Module");
            yield return request; Assert.That(request.Exception, Is.TypeOf<TypeLoadException>());
            Assert.That(request.Module, Is.Null); Assert.That(request.RequiresRestart, Is.False);
        }

        [UnityTest]
        public IEnumerator PendingModuleReportsMonotonicCodeAndCoreSceneProgressBeforeCompletion()
        {
            yield return fixture.TearDown();
            fixture.Initialize("Metadata");
            ownedScene = SceneManager.CreateScene("YLHybridCLR.Progress." + Guid.NewGuid().ToString("N"));
            pendingScene = new PendingScene { PendingProgress = 0.2f };
            ownedProvider = new PendingSceneProvider(pendingScene);
            CoreResourceAccess.Bind(ownedProvider);
            var load = fixture.Loader.LoadAsync(HybridCLRLoaderTests.Address);
            Assert.That(load.isDone, Is.False, "必须经过真实 pending Resource 加载，不能只验证同步完成。");
            ownedRequest = fixture.Loader.StartModuleAsync(HybridCLRLoaderTests.Address, Entry, typeof(HybridCLRWaitingModule).FullName);
            YLFramework.Foundation.CustomAsyncOperation<HybridCLRModuleRequest> observed = ownedRequest;
            var samples = new List<float>();
            var codeStage = ownedRequest.Stage;
            var sawCodeProgress = false;
            void Sample()
            {
                samples.Add(observed.progress);
                if (!load.isDone && observed.progress > 0 && observed.progress < 1) sawCodeProgress = true;
            }
            Sample();
            for (var i = 0; i < 240 && ownedRequest.Module == null && !ownedRequest.isDone; i++)
            {
                yield return null;
                Sample();
            }
            Assert.That(ownedRequest.Module, Is.Not.Null, ownedRequest.error);
            var core = ModuleManager.StartUpModule(typeof(HybridCLRWaitingModule));
            Assert.That(core.isDone, Is.False, "真实 Core 启动应等待受控场景请求。");
            Assert.That(core.Module, Is.SameAs(ownedRequest.Module));
            var sceneStage = ownedRequest.Stage;
            for (var i = 0; i < 4; i++) { yield return null; Sample(); }
            var low = observed.progress;
            pendingScene.PendingProgress = 0.8f;
            for (var i = 0; i < 4; i++) { yield return null; Sample(); }
            var high = observed.progress;
            Assert.That(ownedRequest.isDone, Is.False);
            Assert.That(core.progress, Is.GreaterThan(0.5f).And.LessThan(1));
            pendingScene.Succeed(ownedScene);
            for (var i = 0; i < 30 && !ownedRequest.isDone; i++) { yield return null; Sample(); }

            Assert.That(ownedRequest.isDone, Is.True);
            Assert.That(ownedRequest.Exception, Is.Null, ownedRequest.error);
            Assert.That(sawCodeProgress, Is.True, "代码资源尚未完成时必须转发中间进度。");
            Assert.That(low, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(high, Is.GreaterThan(low).And.LessThan(1), "Core 场景仍在等待时，外层进度必须随 Core 前进。");
            Assert.That(sceneStage, Is.Not.EqualTo(codeStage), "代码加载与 Core 启动必须可区分。");
            for (var i = 0; i < samples.Count; i++)
            {
                Assert.That(samples[i], Is.InRange(0f, 1f));
                if (i > 0) Assert.That(samples[i], Is.GreaterThanOrEqualTo(samples[i - 1]), "切换阶段时不能倒退。");
            }
            Assert.That(observed.progress, Is.EqualTo(1));
            Assert.That(ownedRequest.Module.State, Is.EqualTo(ModuleState.Running));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator EditorFastStartsWithoutCodeResourcesAndRetainsActiveModuleOwnership()
        {
            yield return InitializeWithoutCodeResources();
            var starts = 0;
            HybridCLRProbeModule.Starting = () => starts++;
            ownedRequest = StartEditorModule(Entry, typeof(HybridCLRProbeModule).FullName);
            for (var i = 0; i < 30 && !ownedRequest.isDone; i++) yield return null;
            Assert.That(ownedRequest.isDone, Is.True);
            Assert.That(ownedRequest.Exception, Is.Null, ownedRequest.error);
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(ownedRequest.Module, Is.SameAs(ModuleManager.GetModule(typeof(HybridCLRProbeModule))));
            Assert.That(ownedRequest.Module.State, Is.EqualTo(ModuleState.Running));
            Assert.That(ownedRequest.Code, Is.Not.Null);
            var mode = typeof(HybridCLRLoadResult).GetProperty("Mode", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(mode, Is.Not.Null, "必须明确区分 EditorFast、EditorSimulation 和 Native。");
            Assert.That(mode.PropertyType.IsEnum, Is.True);
            Assert.That(mode.PropertyType.FullName, Is.EqualTo("YLFramework.Integrations.HybridCLR.HybridCLRLoadMode"));
            Assert.That(mode.GetValue(ownedRequest.Code).ToString(), Is.EqualTo("EditorFast"));
            Assert.That(ownedRequest.Code.CodePackageId, Is.Null.Or.Empty, "未经 manifest 校验不能伪造 CodePackageId。");
            Assert.That(ownedRequest.Code.Assemblies[Entry], Is.SameAs(typeof(HybridCLRProbeModule).Assembly));
            Assert.That(nativeCalls, Is.Zero);
            Assert.That(fixture.Process.Projects, Is.Empty);
            Assert.That(fixture.Process.Assemblies, Is.Empty);
            Assert.That(fixture.Process.ActiveJob, Is.Null);
            Assert.That(fixture.ResourcesSystem.GetDiagnosticsReport().History, Is.Empty, "快路径不能尝试读取 manifest、DLL 或 metadata。");
            Assert.That(fixture.ResourcesSystem.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => fixture.Loader.Dispose());
            ownedRequest.Module.Close(); fixture.Loader.Dispose();
            Assert.That(fixture.ResourcesSystem.IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator EditorFastMissingEntryFinishesDiagnosticallyWithoutLoadingNativeCode()
        {
            yield return InitializeWithoutCodeResources();
            const string missing = "Missing.EditorFast.Entry";
            ownedRequest = StartEditorModule(missing, typeof(HybridCLRProbeModule).FullName);
            for (var i = 0; i < 30 && !ownedRequest.isDone; i++) yield return null;
            Assert.That(ownedRequest.isDone, Is.True, "已接受的缺失 entry 请求必须进入失败终态。");
            Assert.That(ownedRequest.Exception, Is.Not.Null);
            Assert.That(ownedRequest.error, Does.Contain(missing));
            Assert.That(ownedRequest.Module, Is.Null);
            Assert.That(ownedRequest.RequiresRestart, Is.False);
            Assert.That(ModuleManager.GetModule(typeof(HybridCLRProbeModule)), Is.Null);
            Assert.That(nativeCalls, Is.Zero);
            Assert.That(fixture.Process.Projects, Is.Empty);
            Assert.That(fixture.Process.Assemblies, Is.Empty);
            Assert.That(fixture.ResourcesSystem.GetDiagnosticsReport().History, Is.Empty);
            fixture.Loader.Dispose();
        }

        [UnityTest]
        public IEnumerator EditorFastKeepsModuleOwnershipAfterTheBorrowedResourceSystemShutsDown()
        {
            yield return InitializeWithoutCodeResources();
            ownedRequest = StartEditorModule(Entry, typeof(HybridCLRProbeModule).FullName);
            yield return ownedRequest;
            Assert.That(ownedRequest.Exception, Is.Null);
            Assert.That(ownedRequest.Module.State, Is.EqualTo(ModuleState.Running));
            using (var shutdown = fixture.ResourcesSystem.ShutdownAsync())
            { yield return shutdown; Assert.That(shutdown.Status, Is.EqualTo(ResourceRequestStatus.Succeeded), shutdown.Error?.ToString()); }
            Assert.That(fixture.ResourcesSystem.IsAvailable, Is.False);
            Assert.That(ownedRequest.Module.State, Is.EqualTo(ModuleState.Running));
            Assert.Throws<InvalidOperationException>(() => fixture.Loader.Dispose(), "Resource shutdown is not Module shutdown.");
            ownedRequest.Module.Close();
            Assert.DoesNotThrow(() => fixture.Loader.Dispose());
        }

        private IEnumerator InitializeWithoutCodeResources()
        {
            yield return fixture.TearDown();
            var catalog = new ResourceCatalog("hybridclr-test", Array.Empty<ResourceCatalogEntry>());
            using var init = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[] { catalog }));
            Assert.That(init.Status, Is.EqualTo(ResourceRequestStatus.Succeeded), init.Error?.ToString());
            fixture.ResourcesSystem = init.TakeResult();
            fixture.ResourcesSystem.ConfigureDiagnosticsHistory(8);
            fixture.Calls.LoadMetadata = _ => { nativeCalls++; throw new InvalidOperationException("EditorFast must not load metadata."); };
            fixture.Calls.LoadAssembly = _ => { nativeCalls++; throw new InvalidOperationException("EditorFast must not load DLL bytes."); };
            fixture.Loader = new HybridCLRLoader(fixture.ResourcesSystem, "base-test", fixture.Calls, fixture.Process);
            ownedProvider = fixture.ResourcesSystem.BindCoreResourceProvider();
        }

        private HybridCLRModuleRequest StartEditorModule(string entryAssemblyName, string moduleTypeFullName)
        {
            // 缺失新 API 时以普通断言 RED，而不是让测试程序集编译失败。
            var method = typeof(HybridCLRLoader).GetMethod("StartEditorModuleAsync", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(string), typeof(string), typeof(string) }, null);
            Assert.That(method, Is.Not.Null, "缺少 public StartEditorModuleAsync(string, string, string)。");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(HybridCLRModuleRequest)));
            return (HybridCLRModuleRequest)method.Invoke(fixture.Loader, new object[] { "hybridclr-test", entryAssemblyName, moduleTypeFullName });
        }
#endif

        private sealed class PendingSceneProvider : ICoreResourceProvider
        {
            private readonly PendingScene request;
            internal PendingSceneProvider(PendingScene request) => this.request = request;
            public CoreResourceRequest<string> ReadTextAsync(string projectId, string address) => throw new NotSupportedException();
            public CorePrefabLease AcquirePrefab(string projectId, string address) => throw new NotSupportedException();
            public CoreResourceRequest<CorePrefabLease> AcquirePrefabAsync(string projectId, string address) => throw new NotSupportedException();
            public CoreResourceRequest<CoreSceneResult> LoadSceneAsync(string projectId, string address, LoadSceneMode mode)
            {
                Assert.That(projectId, Is.EqualTo("hybridclr-test"));
                Assert.That(address, Is.EqualTo("owned-pending-scene"));
                Assert.That(mode, Is.EqualTo(LoadSceneMode.Single));
                return request;
            }
        }

        private sealed class PendingScene : CoreResourceRequest<CoreSceneResult>
        {
            private CoreResourceStatus status;
            private CoreSceneResult result;
            private bool taken;
            internal float PendingProgress;
            public override CoreResourceStatus Status => status;
            public override float Progress => status == CoreResourceStatus.Succeeded ? 1 : PendingProgress;
            public override CoreResourceException Error => null;
            private bool disposed;
            public override bool IsDisposed => disposed;
            public override event Action<CoreResourceRequest<CoreSceneResult>> Completed;
            internal void Succeed(Scene scene)
            {
                if (status != CoreResourceStatus.Pending || disposed) return;
                result = new CoreSceneResult(scene, false); status = CoreResourceStatus.Succeeded;
                var notify = Completed; Completed = null; notify?.Invoke(this);
            }
            public override CoreSceneResult TakeResult()
            {
                if (disposed) throw new ObjectDisposedException(nameof(PendingScene));
                if (taken || status != CoreResourceStatus.Succeeded) throw new InvalidOperationException("No transferable scene result.");
                taken = true; return result;
            }
            public override void Cancel()
            {
                if (status != CoreResourceStatus.Pending) return;
                status = CoreResourceStatus.Canceled;
                var notify = Completed; Completed = null; notify?.Invoke(this);
            }
            public override void Dispose() { if (disposed) return; Cancel(); disposed = true; Completed = null; }
        }
    }
}
