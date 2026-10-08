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
        public override void OnStart()
        {
            AddModel(new YLCoreFailureProbeModel { Id = 1 });
            if (FailStart) throw new InvalidOperationException("YLCoreSmoke startup failure");
        }
        protected override void OnClose()
        {
            if (FailClose) throw new InvalidOperationException("YLCoreSmoke close failure");
        }
    }

    public sealed class YLCoreFailureProbeModel : YLFramework.Core.Model
    {
        public int ExitCount;
        public override void OnInit() { }
        public override void OnExit() => ExitCount++;
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
        public IEnumerator FixtureCleanupDetachesOnlyOwnedRegistrationAfterCloseFailure()
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
                Assert.Throws<InvalidOperationException>(() => module.Close());
                Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.SameAs(module));
                classPool.Restore(new Module[] { module });
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
        public IEnumerator CloseFailureRetainsRegistrationAndRejectsRestartUntilCleanupSucceeds()
        {
            var request = ModuleManager.StartUpModule(module);
            var deadline = Time.realtimeSinceStartup + 5;
            while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.isDone, Is.True);
            Assert.That(request.error, Is.Null.Or.Empty);
            yield return null;
            var model = module.GetModel<YLCoreFailureProbeModel>(1);
            module.FailClose = true;
            var error = Assert.Throws<InvalidOperationException>(() => module.Close());
            Assert.That(error.Message, Is.EqualTo("YLCoreSmoke close failure"));
            Assert.That(model.ExitCount, Is.EqualTo(1));
            Assert.That(module.GetModels<YLCoreFailureProbeModel>(), Is.Empty);
            Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.SameAs(module),
                "Known upstream behavior: OnClose failure prevents removal from the registry.");
            Assert.That(module.State, Is.EqualTo(ModuleState.CloseFailed));
            Assert.Throws<InvalidOperationException>(() => ModuleManager.StartUpModule(module));
            module.FailClose = false;
            module.Close();
            Assert.That(model.ExitCount, Is.EqualTo(1));
            Assert.That(ModuleManager.GetModule<YLCoreFailureProbeModule>(), Is.Null);
            Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
        }

    }
}
