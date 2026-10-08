using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace YLFramework.Core.Tests
{
    public sealed class YLCoreFailureProbeModule : YLFramework.Core.Module
    {
        public bool FailStart;
        public bool FailClose;
        public int CloseCount;
        public override void OnStart()
        {
            AddModel(new YLCoreFailureProbeModel { Id = 1 });
            if (FailStart) throw new InvalidOperationException("YLCoreSmoke startup failure");
        }
        protected override void OnClose()
        {
            CloseCount++;
            if (FailClose) throw new InvalidOperationException("YLCoreSmoke close failure");
        }
    }

    public sealed class YLCoreFailureProbeModel : YLFramework.Core.Model
    {
        public int ExitCount;
        public override void OnInit() { }
        public override void OnExit() => ExitCount++;
    }

    public sealed class YLCoreShutdownProbeModule<T> : SingletonModule<YLCoreShutdownProbeModule<T>>
    {
        public bool IsBase;
        public bool FailBaseGetter;
        public bool EqualModules;
        public Action StartAction;
        public Action CloseAction;
        public int CloseCount;
        public override bool BaseModule => FailBaseGetter ? throw new InvalidOperationException("base classification failure") : IsBase;
        public override bool Equals(object obj) => ReferenceEquals(this, obj) || (EqualModules && obj is Module);
        public override int GetHashCode() => EqualModules ? 1 : base.GetHashCode();
        public override void OnStart() => StartAction?.Invoke();
        protected override void OnClose()
        {
            CloseCount++;
            CloseAction?.Invoke();
        }
    }

    public class CoreFailurePathPlayModeTests
    {
        private YLCoreFailureProbeModule module;
        private CoreClassPoolScope classPool;

        [SetUp]
        public void SetUp()
        {
            Assert.That(ModuleManager.AllModules, Is.Empty, "Characterize failures only in the independent test scene.");
            classPool = new CoreClassPoolScope();
            module = new YLCoreFailureProbeModule();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            module.FailClose = false;
            try
            {
                if (ReferenceEquals(ModuleManager.GetModule<YLCoreFailureProbeModule>(), module)) module.Close();
            }
            finally
            {
                classPool.Restore(new Module[] { module });
            }
            yield return null;
            Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator StartupFailureRetainsOwnedObjectsRequiresCleanupAndDoesNotPolluteCurrent()
        {
            var other = new YLCoreDirectViewModule();
            try
            {
                var otherRequest = ModuleManager.StartUpModule(other);
                Assert.That(otherRequest.error, Is.Null.Or.Empty);
                module.FailStart = true;
                var request = ModuleManager.StartUpModule(module);
                var deadline = Time.realtimeSinceStartup + 5;
                while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(request.isDone, Is.True);
                Assert.That(request.error, Does.Contain("YLCoreSmoke startup failure"));
                Assert.That(request.progress, Is.EqualTo(1));
                Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.SameAs(module),
                    "Complete startup rollback belongs to stage E, not C.");
                Assert.That(module.State, Is.EqualTo(ModuleState.StartFailed));
                Assert.That(module.GetModel<YLCoreFailureProbeModel>(1).Module, Is.SameAs(module));
                var manager = typeof(ModuleManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(typeof(ModuleManager).GetField("current_module", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager), Is.SameAs(other));
                Assert.Throws<InvalidOperationException>(() => ModuleManager.StartUpModule(module));
                Assert.Throws<InvalidOperationException>(() => ModuleManager.StartUpModule<YLCoreFailureProbeModule>());
                module.Close();
                module.FailStart = false;
                var restarted = ModuleManager.StartUpModule(module);
                Assert.That(restarted.isDone, Is.True);
                Assert.That(restarted.error, Is.Null.Or.Empty);
                Assert.That(module.State, Is.EqualTo(ModuleState.Running));
                Assert.That(request.error, Does.Contain("YLCoreSmoke startup failure"));
            }
            finally { other.Close(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseFailureDetachesOnlyOwnedRegistrationAndKeepsOtherCurrent()
        {
            var startup = ModuleManager.StartUpModule(module);
            var deadline = Time.realtimeSinceStartup + 5;
            while (!startup.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(startup.isDone, Is.True);
            Assert.That(startup.error, Is.Null.Or.Empty);
            var other = new YLCoreDirectViewModule();
            try
            {
                var otherStartup = ModuleManager.StartUpModule(other);
                deadline = Time.realtimeSinceStartup + 5;
                while (!otherStartup.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(otherStartup.isDone, Is.True);
                Assert.That(otherStartup.error, Is.Null.Or.Empty);
                yield return null;
                module.FailClose = true;
                Assert.Throws<AggregateException>(() => module.Close());
                Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
                Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.Null);
                Assert.That(ModuleManager.GetModule<YLCoreDirectViewModule>(), Is.SameAs(other));
                var manager = typeof(ModuleManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Assert.That(typeof(ModuleManager).GetField("current_module", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager), Is.SameAs(other));
            }
            finally
            {
                if (ReferenceEquals(ModuleManager.GetModule<YLCoreDirectViewModule>(), other)) other.Close();
                classPool.Restore(new Module[] { module, other });
            }
        }

        [UnityTest]
        public IEnumerator CloseFailureEndsOwnershipBeforeReportingAndAllowsExplicitRestart()
        {
            var request = ModuleManager.StartUpModule(module);
            var deadline = Time.realtimeSinceStartup + 5;
            while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.isDone, Is.True);
            Assert.That(request.error, Is.Null.Or.Empty);
            yield return null;
            var model = module.GetModel<YLCoreFailureProbeModel>(1);
            module.FailClose = true;
            var error = Assert.Throws<AggregateException>(() => module.Close());
            Assert.That(error.InnerExceptions[0].Message, Is.EqualTo("YLCoreSmoke close failure"));
            Assert.That(model.ExitCount, Is.EqualTo(1));
            Assert.That(module.GetModels<YLCoreFailureProbeModel>(), Is.Empty);
            Assert.That(model.Module, Is.Null);
            Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.Null);
            Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
            module.Close();
            Assert.That(module.CloseCount, Is.EqualTo(1));
            Assert.That(model.ExitCount, Is.EqualTo(1));
            Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.Null);
            Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
            module.FailClose = false;
            Assert.That(ModuleManager.StartUpModule(module).error, Is.Null.Or.Empty);
        }

        [UnityTest]
        public IEnumerator ShutdownClosesOrdinaryThenBaseInReverseRegistrationOrderAndContinuesAfterErrors()
        {
            var events = new List<string>();
            var baseFirst = new YLCoreShutdownProbeModule<YLCoreProbeModel> { IsBase = true };
            var ordinaryFirst = new YLCoreShutdownProbeModule<YLCoreProbeController>();
            var baseLast = new YLCoreShutdownProbeModule<YLCoreFailureProbeModel> { IsBase = true };
            var ordinaryLast = new YLCoreShutdownProbeModule<YLCoreFailureProbeModule>();
            var owned = new Module[] { baseFirst, ordinaryFirst, baseLast, ordinaryLast };
            try
            {
                foreach (var item in owned)
                {
                    var request = ModuleManager.StartUpModule(item);
                    Assert.That(request.isDone, Is.True);
                    Assert.That(request.error, Is.Null.Or.Empty);
                }
                Assert.Throws<InvalidOperationException>(() => baseFirst.Close());
                Assert.That(baseFirst.State, Is.EqualTo(ModuleState.Running));
                Assert.That(baseFirst.CloseCount, Is.Zero);
                var model = baseFirst.LoadModel<YLCoreProbeModel>();
                var closeError = new InvalidOperationException("ordinary close");
                ordinaryLast.CloseAction = () =>
                {
                    events.Add("ordinary.last");
                    Assert.That(baseFirst.GetModel<YLCoreProbeModel>(), Is.SameAs(model));
                    Assert.That(baseFirst.State, Is.EqualTo(ModuleState.Running));
                    Assert.Throws<InvalidOperationException>(() => ModuleManager.StartUpModule(module));
                    Assert.Throws<InvalidOperationException>(() => ordinaryFirst.Close());
                    ModuleManager.Shutdown();
                    throw closeError;
                };
                ordinaryFirst.CloseAction = () => events.Add("ordinary.first");
                baseLast.CloseAction = () => events.Add("base.last");
                baseFirst.CloseAction = () => events.Add("base.first");
                var error = Assert.Throws<AggregateException>(() => ModuleManager.Shutdown());
                Assert.That(error.InnerExceptions, Is.EqualTo(new[] { closeError }));
                Assert.That(events, Is.EqualTo(new[] { "ordinary.last", "ordinary.first", "base.last", "base.first" }));
                Assert.That(ModuleManager.AllModules, Is.Empty);
                Assert.That(ModuleManager.AllBaseModules, Is.Empty);
                Assert.That(model.Module, Is.Null);
                Assert.That(model.ExitCount, Is.EqualTo(1));
                Assert.That(YLCoreShutdownProbeModule<YLCoreProbeModel>.Instance, Is.Null);
                foreach (var item in owned) Assert.That(item.State, Is.EqualTo(ModuleState.Closed));
                ModuleManager.Shutdown();
                Assert.That(ordinaryLast.CloseCount, Is.EqualTo(1));
                Assert.That(baseFirst.CloseCount, Is.EqualTo(1));
                ordinaryLast.CloseAction = null;
                Assert.That(ModuleManager.StartUpModule(ordinaryLast).error, Is.Null.Or.Empty);
            }
            finally
            {
                ordinaryLast.CloseAction = null;
                ModuleManager.Shutdown();
                classPool.Restore(owned);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedBaseStartupStillNeedsExplicitShutdownAndThenSingletonCanRestart()
        {
            var failed = new YLCoreShutdownProbeModule<YLCoreProbeModel> { IsBase = true };
            try
            {
                failed.StartAction = () =>
                {
                    failed.AddModel(new YLCoreProbeModel());
                    throw new InvalidOperationException("base startup failure");
                };
                var request = ModuleManager.StartUpModule(failed);
                Assert.That(request.isDone, Is.True);
                Assert.That(request.error, Does.Contain("base startup failure"));
                Assert.That(failed.State, Is.EqualTo(ModuleState.StartFailed));
                Assert.That(YLCoreShutdownProbeModule<YLCoreProbeModel>.Instance, Is.SameAs(failed));
                var model = failed.GetModel<YLCoreProbeModel>();
                Assert.Throws<InvalidOperationException>(() => failed.Close());
                failed.CloseAction = () =>
                {
                    Assert.That(model.ExitCount, Is.Zero);
                    Assert.That(failed.GetModel<YLCoreProbeModel>(), Is.SameAs(model));
                };
                ModuleManager.Shutdown();
                Assert.That(failed.State, Is.EqualTo(ModuleState.Closed));
                Assert.That(model.ExitCount, Is.EqualTo(1));
                Assert.That(model.Module, Is.Null);
                Assert.That(YLCoreShutdownProbeModule<YLCoreProbeModel>.Instance, Is.Null);
                failed.StartAction = null;
                failed.CloseAction = null;
                Assert.That(ModuleManager.StartUpModule(failed).error, Is.Null.Or.Empty);
                Assert.That(failed.State, Is.EqualTo(ModuleState.Running));
                Assert.That(failed.GetModels<Model>(), Is.Empty);
            }
            finally
            {
                failed.CloseAction = null;
                ModuleManager.Shutdown();
                classPool.Restore(new Module[] { failed });
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShutdownTracksReferenceIdentityAndPartiallyRegisteredStartupFailure()
        {
            var first = new YLCoreShutdownProbeModule<YLCoreProbeModel> { EqualModules = true };
            var second = new YLCoreShutdownProbeModule<YLCoreProbeController> { EqualModules = true };
            var failed = new YLCoreShutdownProbeModule<YLCoreFailureProbeModel> { FailBaseGetter = true };
            var owned = new Module[] { first, second, failed };
            try
            {
                Assert.That(ModuleManager.StartUpModule(first).error, Is.Null.Or.Empty);
                Assert.That(ModuleManager.StartUpModule(second).error, Is.Null.Or.Empty);
                second.Close();
                var request = ModuleManager.StartUpModule(failed);
                Assert.That(request.isDone, Is.True);
                Assert.That(request.error, Does.Contain("base classification failure"));
                Assert.That(failed.State, Is.EqualTo(ModuleState.StartFailed));
                Assert.That(ModuleManager.GetModule(failed.ModuleName), Is.SameAs(failed));
                ModuleManager.Shutdown();
                Assert.That(ModuleManager.AllModules, Is.Empty);
                Assert.That(first.CloseCount, Is.EqualTo(1));
                Assert.That(second.CloseCount, Is.EqualTo(1));
                Assert.That(failed.CloseCount, Is.EqualTo(1));
                Assert.That(first.State, Is.EqualTo(ModuleState.Closed));
                Assert.That(failed.State, Is.EqualTo(ModuleState.Closed));
            }
            finally
            {
                ModuleManager.Shutdown();
                classPool.Restore(owned);
            }
            yield return null;
        }

    }
}
