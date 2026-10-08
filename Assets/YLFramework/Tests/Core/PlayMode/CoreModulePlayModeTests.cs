using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace YLFramework.Core.Tests
{
    public class CoreModulePlayModeTests
    {
        private static readonly PropertyInfo ManagerInstance = typeof(YLFramework.Core.ModuleManager)
            .GetProperty("Instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentModule = typeof(YLFramework.Core.ModuleManager)
            .GetField("current_module", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<YLFramework.Core.Module> ownedModules = new List<YLFramework.Core.Module>();
        private object manager;
        private object previousCurrentModule;
        private CoreClassPoolScope classPool;

        [SetUp]
        public void SetUp()
        {
            ownedModules.Clear();
            Assert.That(ManagerInstance, Is.Not.Null);
            Assert.That(CurrentModule, Is.Not.Null);
            manager = ManagerInstance.GetValue(null);
            previousCurrentModule = CurrentModule.GetValue(manager);
            classPool = new CoreClassPoolScope();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var failures = new List<Exception>();
            try
            {
                for (int i = ownedModules.Count - 1; i >= 0; i--)
                {
                    var module = ownedModules[i];
                    if (!ReferenceEquals(YLFramework.Core.ModuleManager.GetModule(module.ModuleName), module)) continue;
                    var baseProbe = module as YLCoreBaseProbeModule;
                    if (baseProbe != null) baseProbe.AllowClose = true;
                    try { module.Close(); }
                    catch (Exception error) { failures.Add(error); }
                }
            }
            finally
            {
                // Do not clear shared dictionaries or replace the manager singleton.
                if (manager != null) CurrentModule.SetValue(manager, previousCurrentModule);
                try { classPool?.Restore(ownedModules); }
                finally { ownedModules.Clear(); }
            }
            yield return null;
            if (failures.Count > 0) throw new AggregateException("Owned Module cleanup failed.", failures);
        }

        [UnityTest]
        public IEnumerator EmptyModuleRegistersClosesAndRestarts()
        {
            var module = new YLCoreLifecycleModule();
            Assert.That(module.DefaultStartUpScene, Is.Empty);
            var first = StartOwned(module);
            yield return AwaitStartup(first);

            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreLifecycleModule>(), Is.SameAs(module));
            Assert.That(YLFramework.Core.ModuleManager.AllModules[module.ModuleName], Is.SameAs(module));
            Assert.That(YLFramework.Core.ModuleManager.AllBaseModules.ContainsKey(module.ModuleName), Is.False);
            Assert.That(module.ModuleName, Is.EqualTo(typeof(YLCoreLifecycleModule).FullName));
            Assert.That(module.RegisteredAtStart, Is.True);
            Assert.That(module.ProjectNameAtStart, Is.EqualTo(nameof(YLCoreLifecycleModule)));
            Assert.That(module.StartCount, Is.EqualTo(1));
            AssertIndexesEmpty(module);

            module.Close();
            Assert.That(YLFramework.Core.ModuleManager.GetModule(module.ModuleName), Is.Null);
            Assert.That(module.CloseCount, Is.EqualTo(1));
            Assert.That(module.RegisteredAtClose, Is.True);
            yield return AwaitStartup(StartOwned(module));
            Assert.That(module.StartCount, Is.EqualTo(2));
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreLifecycleModule>(), Is.SameAs(module));
            AssertIndexesEmpty(module);
            module.Close();
            Assert.That(module.CloseCount, Is.EqualTo(2));
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreLifecycleModule>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator RepeatedStartupOfSameInstanceKeepsOneRegistrationAndRunsOnStartAgain()
        {
            var module = new YLCoreLifecycleModule();
            yield return AwaitStartup(StartOwned(module));
            var count = YLFramework.Core.ModuleManager.AllModules.Count;
            yield return AwaitStartup(StartOwned(module));

            // Existing semantics rerun OnStart; this is not an idempotent-start assertion.
            Assert.That(module.StartCount, Is.EqualTo(2));
            Assert.That(YLFramework.Core.ModuleManager.AllModules.Count, Is.EqualTo(count));
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreLifecycleModule>(), Is.SameAs(module));
            module.Close();
            Assert.That(module.CloseCount, Is.EqualTo(1));
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreLifecycleModule>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator StartupRequestUsesFoundationCompletionProgressAndLateCallbacks()
        {
            var module = new YLCoreLifecycleModule();
            var request = StartOwned(module);
            YLFramework.Foundation.CustomAsyncOperation<YLFramework.Core.StartUpModuleRequest> foundation = request;
            int completionCount = 0;
            YLFramework.Core.StartUpModuleRequest completedRequest = null;
            bool callbackSawStartedModule = false;
            bool callbackSawCompletion = false;
            Action<YLFramework.Core.StartUpModuleRequest> callback = completed =>
            {
                completionCount++;
                completedRequest = completed;
                callbackSawStartedModule = module.StartCount == 1 && module.RegisteredAtStart;
                callbackSawCompletion = completed.isDone && !completed.keepWaiting &&
                    completed.progress == 1f && string.IsNullOrEmpty(completed.error);
            };
            foundation.AddCompleteEvent(callback);
            yield return AwaitStartup(request);

            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(completedRequest, Is.SameAs(request));
            Assert.That(callbackSawStartedModule, Is.True);
            Assert.That(callbackSawCompletion, Is.True);
            Assert.That(request.Module, Is.SameAs(module));
            Assert.That(request.Step, Is.EqualTo(YLFramework.Core.StartingModuleSteps.LoadScene));
            Assert.That(foundation.progress, Is.EqualTo(1f));
            Assert.That(foundation.keepWaiting, Is.False);
            Assert.That(foundation.error, Is.Null.Or.Empty);
            int lateCount = 0;
            foundation.AddCompleteEvent(completed =>
            {
                Assert.That(completed, Is.SameAs(request));
                lateCount++;
            });
            Assert.That(lateCount, Is.EqualTo(1), "A callback added after completion runs immediately.");
            foundation.RemoveCompleteEvent(callback);
            yield return null;
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(lateCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ModelsTrackOwnershipIdsMultipleInstancesQueriesAndRemoval()
        {
            var module = new YLCoreLifecycleModule();
            yield return AwaitStartup(StartOwned(module));
            var first = new YLCoreProbeModel { Id = 11 };
            var second = new YLCoreProbeModel { Id = 22 };
            var automatic = new YLCoreDerivedProbeModel();
            module.AddModel(first);
            module.AddModel(second);
            module.AddModel(automatic);

            foreach (var model in new YLCoreProbeModel[] { first, second, automatic })
            {
                Assert.That(model.Module, Is.SameAs(module));
                Assert.That(model.InitCount, Is.EqualTo(1));
                Assert.That(model.RegisteredAtInit, Is.True);
            }
            Assert.That(automatic.Id, Is.EqualTo((long)automatic.GetHashCode()));
            Assert.That(module.GetModel<YLCoreProbeModel>(11), Is.SameAs(first));
            Assert.That(module.GetModel(typeof(YLCoreProbeModel), 22), Is.SameAs(second));
            Assert.That(module.GetModel<YLCoreDerivedProbeModel>(), Is.SameAs(automatic));
            Assert.That(module.GetModels<YLCoreProbeModel>(), Is.EquivalentTo(new[] { first, second, automatic }));
            var ambiguous = Assert.Throws<Exception>(() => module.GetModel<YLCoreProbeModel>());
            StringAssert.Contains(typeof(YLCoreProbeModel).FullName, ambiguous.Message);

            var duplicate = new YLCoreProbeModel { Id = 11 };
            LogAssert.Expect(LogType.Warning, $"model name:{typeof(YLCoreProbeModel).FullName} id:11 已经存在,请勿重复添加!");
            module.AddModel(duplicate);
            Assert.That(duplicate.Module, Is.Null);
            Assert.That(duplicate.InitCount, Is.Zero);
            Assert.That(module.GetModel<YLCoreProbeModel>(11), Is.SameAs(first));
            module.RemoveModel(first);
            Assert.That(first.ExitCount, Is.EqualTo(1));
            Assert.That(first.RegisteredAtExit, Is.True);
            Assert.That(module.GetModel<YLCoreProbeModel>(11), Is.Null);
            Assert.That(module.GetModel<YLCoreProbeModel>(), Is.SameAs(second));
            module.ClearModel<YLCoreProbeModel>();
            Assert.That(module.GetModels<YLCoreProbeModel>(), Is.Empty);
            Assert.That(first.ExitCount, Is.EqualTo(1));
            Assert.That(second.ExitCount, Is.EqualTo(1));
            Assert.That(automatic.ExitCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BaseModelsAreReadableAcrossModulesWithLocalIdPrecedence()
        {
            var baseModule = new YLCoreBaseProbeModule();
            var consumer = new YLCoreConsumerModule();
            yield return AwaitStartup(StartOwned(baseModule));
            yield return AwaitStartup(StartOwned(consumer));
            var shared = new YLCoreProbeModel { Id = 31 };
            var derived = new YLCoreDerivedProbeModel { Id = 32 };
            var local = new YLCoreProbeModel { Id = 31 };
            baseModule.AddModel(shared);
            baseModule.AddModel(derived);

            Assert.That(YLFramework.Core.ModuleManager.AllBaseModules[baseModule.ModuleName], Is.SameAs(baseModule));
            // ID and list queries do not recursively search missing types in other base modules.
            Assert.That(consumer.GetModel<YLCoreProbeModel>(31), Is.SameAs(shared));
            Assert.That(consumer.GetModel<YLCoreDerivedProbeModel>(32), Is.SameAs(derived));
            Assert.That(consumer.GetModels<YLCoreProbeModel>(), Is.EquivalentTo(new[] { shared, derived }));
            consumer.AddModel(local);
            Assert.That(consumer.GetModel<YLCoreProbeModel>(31), Is.SameAs(local));
            Assert.That(local.Module, Is.SameAs(consumer));
            Assert.That(shared.Module, Is.SameAs(baseModule));
            Assert.That(consumer.GetModels<YLCoreProbeModel>(), Is.EquivalentTo(new[] { local, shared, derived }));

            consumer.Close();
            Assert.That(local.ExitCount, Is.EqualTo(1));
            Assert.That(shared.ExitCount, Is.Zero);
            Assert.That(derived.ExitCount, Is.Zero);
            Assert.That(baseModule.GetModel<YLCoreProbeModel>(31), Is.SameAs(shared));
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreBaseProbeModule>(), Is.SameAs(baseModule));
        }

        [UnityTest]
        public IEnumerator ControllerLoadReusesAcrossOrdinaryModulesButFalseIsolatesOwnershipAndExit()
        {
            var owner = new YLCoreLifecycleModule();
            var consumer = new YLCoreConsumerModule();
            yield return AwaitStartup(StartOwned(owner));
            yield return AwaitStartup(StartOwned(consumer));
            Assert.That(owner.BaseModule, Is.False);
            var shared = owner.LoadController<YLCoreProbeController>(false);
            Assert.That(owner.LoadController(typeof(YLCoreProbeController), false), Is.SameAs(shared));
            Assert.That(consumer.LoadController<YLCoreProbeController>(), Is.SameAs(shared));
            Assert.That(shared.Module, Is.SameAs(owner));
            Assert.That(shared.OwnerAtInit, Is.SameAs(owner));
            Assert.That(shared.InitCount, Is.EqualTo(1));

            var local = (YLCoreProbeController)consumer.LoadController(typeof(YLCoreProbeController), false);
            Assert.That(local, Is.Not.SameAs(shared));
            Assert.That(local.Module, Is.SameAs(consumer));
            Assert.That(local.OwnerAtInit, Is.SameAs(consumer));
            Assert.That(local.InitCount, Is.EqualTo(1));
            Assert.That(consumer.LoadController<YLCoreProbeController>(), Is.SameAs(local));
            consumer.RemoveController<YLCoreProbeController>();
            Assert.That(local.ExitCount, Is.EqualTo(1));
            Assert.That(local.OwnerAtExit, Is.SameAs(consumer));
            Assert.That(shared.ExitCount, Is.Zero);
            Assert.That(consumer.LoadController<YLCoreProbeController>(), Is.SameAs(shared));
            consumer.Close();
            Assert.That(local.ExitCount, Is.EqualTo(1));
            Assert.That(shared.ExitCount, Is.Zero, "Borrowed controllers are not owned by the consumer.");
            owner.Close();
            Assert.That(shared.ExitCount, Is.EqualTo(1));
            Assert.That(shared.OwnerAtExit, Is.SameAs(owner));
        }

        [UnityTest]
        public IEnumerator DuplicateCreateControllerReturnsUnregisteredInstanceAsLegacyBehavior()
        {
            var module = new YLCoreLifecycleModule();
            yield return AwaitStartup(StartOwned(module));
            var registered = module.CreateController<YLCoreProbeController>();
            LogAssert.Expect(LogType.Warning, $"controller name:{typeof(YLCoreProbeController)} 已经存在,请勿重复添加!");
            var duplicate = (YLCoreProbeController)module.CreateController(typeof(YLCoreProbeController));

            // Characterize the retained original defect, not an isolation guarantee for CreateController.
            Assert.That(duplicate, Is.Not.SameAs(registered));
            Assert.That(duplicate.Module, Is.Null);
            Assert.That(duplicate.InitCount, Is.Zero);
            Assert.That(module.LoadController<YLCoreProbeController>(false), Is.SameAs(registered));
            Assert.That(registered.InitCount, Is.EqualTo(1));
            module.Close();
            Assert.That(registered.ExitCount, Is.EqualTo(1));
            Assert.That(duplicate.ExitCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StartupAndCloseOrderClearAllMvcIndexesBeforeRestart()
        {
            var module = new YLCoreLifecycleModule { SeedMvc = true };
            var request = StartOwned(module);
            request.AddCompleteEvent(_ => module.Events.Add("request.complete"));
            yield return AwaitStartup(request);
            Assert.That(module.Events, Is.EqualTo(new[] { "module.start", "model.init", "controller.init", "request.complete" }));
            var oldModel = module.StartedModel;
            var oldController = module.StartedController;
            Assert.That(oldModel.Module, Is.SameAs(module));
            Assert.That(oldController.Module, Is.SameAs(module));
            module.Close();

            Assert.That(module.Events, Is.EqualTo(new[]
            {
                "module.start", "model.init", "controller.init", "request.complete",
                "model.exit", "controller.exit", "module.close"
            }));
            Assert.That(oldModel.ExitCount, Is.EqualTo(1), "Legacy OnExit and Exit must not notify a child twice.");
            Assert.That(oldController.ExitCount, Is.EqualTo(1));
            Assert.That(module.RegisteredAtClose, Is.True);
            Assert.That(YLFramework.Core.ModuleManager.GetModule(module.ModuleName), Is.Null);
            AssertIndexesEmpty(module);

            module.SeedMvc = false;
            yield return AwaitStartup(StartOwned(module));
            Assert.That(module.StartCount, Is.EqualTo(2));
            AssertIndexesEmpty(module);
            var newModel = new YLCoreProbeModel { Id = 101 };
            module.AddModel(newModel);
            var newController = module.LoadController<YLCoreProbeController>(false);
            Assert.That(module.GetModel<YLCoreProbeModel>(101), Is.SameAs(newModel));
            Assert.That(newController, Is.Not.SameAs(oldController));
            Assert.That(newController.InitCount, Is.EqualTo(1));
            module.Close();
            Assert.That(newModel.ExitCount, Is.EqualTo(1));
            Assert.That(newController.ExitCount, Is.EqualTo(1));
            Assert.That(oldModel.ExitCount, Is.EqualTo(1));
            Assert.That(oldController.ExitCount, Is.EqualTo(1));
            Assert.That(module.CloseCount, Is.EqualTo(2));
            AssertIndexesEmpty(module);
        }

        private YLFramework.Core.StartUpModuleRequest StartOwned(YLFramework.Core.Module module)
        {
            var registered = YLFramework.Core.ModuleManager.GetModule(module.ModuleName);
            Assert.That(registered == null || ReferenceEquals(registered, module), Is.True,
                "Never replace or close an externally registered fixture module.");
            Assert.That(!YLFramework.Core.ModuleManager.AllBaseModules.TryGetValue(module.ModuleName, out var baseModule) ||
                ReferenceEquals(baseModule, module), Is.True);
            if (!ownedModules.Contains(module)) ownedModules.Add(module);
            var request = YLFramework.Core.ModuleManager.StartUpModule(module);
            Assert.That(request, Is.Not.Null);
            return request;
        }

        private static IEnumerator AwaitStartup(YLFramework.Core.StartUpModuleRequest request)
        {
            Assert.That(request, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.isDone, Is.True, "Module startup exceeded the five-second timeout.");
            Assert.That(request.error, Is.Null.Or.Empty);
            // Let the backend's request-cache waiter release this key before a restart.
            yield return null;
        }

        private static void AssertIndexesEmpty(YLFramework.Core.Module module)
        {
            foreach (var name in new[] { "models", "controllers", "views", "type_views" })
            {
                var field = typeof(YLFramework.Core.Module).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, name);
                Assert.That(((IDictionary)field.GetValue(module)).Count, Is.Zero, name);
            }
        }

    }
}
