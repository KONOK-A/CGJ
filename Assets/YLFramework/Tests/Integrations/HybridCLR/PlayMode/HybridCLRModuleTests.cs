using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using YLFramework.Core;

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
        private string Entry => typeof(HybridCLRProbeModule).Assembly.GetName().Name;

        [SetUp]
        public void SetUp() { fixture = new HybridCLRLoaderTests(); fixture.Initialize(); HybridCLRProbeModule.Starting = null; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var module = ModuleManager.GetModule(typeof(HybridCLRProbeModule));
            if (module != null) module.Close();
            HybridCLRProbeModule.Starting = null;
            yield return fixture.TearDown();
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
    }
}
