using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using YLFramework.Events;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Tests
{
    public sealed class YLCoreDirectViewModule : YLFramework.Core.Module
    {
        public Action StartAction;
        public Action CloseAction;
        public override void OnStart() => StartAction?.Invoke();
        protected override void OnClose() => CloseAction?.Invoke();
    }

    public class CoreDirectViewPlayModeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<int> prefabKeys = new List<int>();
        private readonly List<int> instanceKeys = new List<int>();
        private readonly List<Scene> scenes = new List<Scene>();
        private YLCoreDirectViewModule module;
        private CoreClassPoolScope classPool;

        [SetUp]
        public void SetUp()
        {
            YLCoreSmokeView.LoadingProbe = null;
            YLCoreSmokeView.ArgsProbe = null;
            YLCoreSmokeView.EnabledProbe = null;
            YLCoreSmokeView.DisabledProbe = null;
            YLCoreSmokeView.SharedEventHandler = null;
            YLCoreSmokeView.DuplicateProbeHandler = false;
            YLCoreSmokeView.SignatureConflictEvent = null;
            Assert.That(ModuleManager.AllModules, Is.Empty, "Use the independent test scene, not a running business module.");
            classPool = new CoreClassPoolScope();
            module = new YLCoreDirectViewModule();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (ReferenceEquals(ModuleManager.GetModule<YLCoreDirectViewModule>(), module)) module.Close();
            }
            finally
            {
                YLCoreSmokeView.LoadingProbe = null;
                YLCoreSmokeView.ArgsProbe = null;
                YLCoreSmokeView.EnabledProbe = null;
                YLCoreSmokeView.DisabledProbe = null;
                YLCoreSmokeView.SharedEventHandler = null;
                YLCoreSmokeView.DuplicateProbeHandler = false;
                YLCoreSmokeView.SignatureConflictEvent = null;
                try { classPool?.Restore(new Module[] { module }); }
                finally { ReleaseObjects(); }
            }
            yield return null;
            foreach (var scene in scenes)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
            Assert.That(ModuleManager.GetModule<YLCoreDirectViewModule>(), Is.Null);
        }

        private void ReleaseObjects()
        {
            foreach (var obj in objects) if (obj) Object.Destroy(obj);
            // The backend has no public per-prefab disposal. Remove only our unique pool/mapping entries.
            var loader = typeof(XFABManager.GameObjectLoader).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (loader != null)
            {
                var pools = (IDictionary)loader.GetType().GetField("allPools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
                var mappings = (IDictionary)loader.GetType().GetField("allObjPoolMapping", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
                foreach (var key in prefabKeys) pools.Remove(key);
                foreach (var key in instanceKeys) mappings.Remove(key);
            }
            objects.Clear();
            prefabKeys.Clear();
            instanceKeys.Clear();
        }

        [UnityTest]
        public IEnumerator ViewQueriesAndClearStayLocalAndDoNotBorrowBaseModuleViews()
        {
            yield return StartModule();
            var baseModule = new YLCoreBaseProbeModule();
            try
            {
                var request = ModuleManager.StartUpModule(baseModule);
                var deadline = Time.realtimeSinceStartup + 10;
                while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(request.isDone, Is.True);
                Assert.That(request.error, Is.Null.Or.Empty);
                var model = baseModule.LoadModel<YLCoreProbeModel>();
                var controller = baseModule.LoadController<YLCoreProbeController>();
                var prefab = Template<YLCoreSmokeView>("YLCoreBaseLookupPrefab");
                var view = Track(baseModule.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));

                Assert.That(module.GetModel<YLCoreProbeModel>(), Is.Null);
                Assert.That(module.GetController<YLCoreProbeController>(), Is.Null);
                Assert.That(module.GetView<YLCoreSmokeView>(prefab.name, view.Id), Is.Null);
                Assert.That(module.GetView<YLCoreSmokeView>(view.Id), Is.Null);
                Assert.That(module.GetView(prefab.name), Is.Null);
                Assert.That(module.GetView<YLCoreSmokeView>(prefab.name), Is.Null);
                Assert.That(module.GetView<YLCoreSmokeView>(), Is.Null);
                Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
                Assert.That(module.GetViews(prefab.name), Is.Empty);
                Assert.That(module.GetViews<YLCoreSmokeView>(prefab.name), Is.Empty);
                var results = new List<View> { view };
                module.GetViewsNonAlloc(results);
                Assert.That(results, Is.Empty);
                Assert.That(view.Module, Is.SameAs(baseModule));
                var local = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                Assert.That(module.GetView<YLCoreSmokeView>(), Is.SameAs(local));
                module.ClearView(prefab.name);
                Assert.That(local.Module, Is.Null);
                local = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                module.ClearView<View>();
                Assert.That(local.Module, Is.Null);
                module.ClearView();
                module.Close();
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(view.RemovedCount, Is.Zero);
                Assert.That(model.ExitCount, Is.Zero);
                Assert.That(controller.ExitCount, Is.Zero);
            }
            finally
            {
                baseModule.AllowClose = true;
                try
                {
                    if (ReferenceEquals(ModuleManager.GetModule(baseModule.ModuleName), baseModule)) baseModule.Close();
                }
                finally { classPool.Restore(new Module[] { baseModule }); }
            }
        }

        [UnityTest]
        public IEnumerator ViewCloseUnbindsBorrowedAndLocalModelsButOnlyExplicitRemovalExitsThem()
        {
            yield return StartModule();
            var owner = new YLCoreLifecycleModule();
            try
            {
                var request = ModuleManager.StartUpModule(owner);
                var deadline = Time.realtimeSinceStartup + 10;
                while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(request.isDone, Is.True);
                Assert.That(request.error, Is.Null.Or.Empty);
                var shared = new YLCoreProbeModel { Id = 41 };
                var local = new YLCoreProbeModel { Id = 41 };
                owner.AddModel(shared);
                module.AddModel(local);
                var prefab = Template<YLCoreSmokeView>("YLCoreModelRemovalPrefab");
                var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                view.BoundModel = shared;
                view.Close();
                Assert.That(view.Model, Is.Null);
                Assert.That(view.UnboundCount, Is.EqualTo(1));
                Assert.That(shared.Module, Is.SameAs(owner));
                Assert.That(shared.ExitCount, Is.Zero);
                Assert.That(module.GetModel<YLCoreProbeModel>(41), Is.SameAs(local));
                view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                Assert.Throws<InvalidOperationException>(() => view.BoundModel = new YLCoreProbeModel { Id = 41 });
                Assert.That(view.Model, Is.Null);
                view.Close();
                Assert.That(local.ExitCount, Is.Zero);
                Assert.That(module.GetModel<YLCoreProbeModel>(41), Is.SameAs(local));
                view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                view.BoundModel = local;
                view.Close();
                Assert.That(local.Module, Is.SameAs(module));
                Assert.That(local.ExitCount, Is.Zero);
                Assert.That(module.GetModel<YLCoreProbeModel>(41), Is.SameAs(local));
                module.RemoveModel(local);
                module.RemoveModel(local);
                Assert.That(local.Module, Is.Null);
                Assert.That(local.ExitCount, Is.EqualTo(1));
                view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                Assert.Throws<InvalidOperationException>(() => view.BoundModel = local);
                view.Close();
                Assert.That(local.ExitCount, Is.EqualTo(1));
                Assert.That(shared.ExitCount, Is.Zero);
            }
            finally
            {
                try
                {
                    if (ReferenceEquals(ModuleManager.GetModule(owner.ModuleName), owner)) owner.Close();
                }
                finally { classPool.Restore(new Module[] { owner }); }
            }
        }

        [UnityTest]
        public IEnumerator RegistrationRequiresExactRegisteredOwnerAndAllowsStartingOwner()
        {
            Assert.That(typeof(View).GetMethod(nameof(View.Close)).IsVirtual, Is.False);
            var modelProperty = typeof(View).GetProperty(nameof(View.Model));
            Assert.That(modelProperty.GetMethod.IsVirtual, Is.False);
            Assert.That(modelProperty.SetMethod, Is.Null);
            var prefab = Template<YLCoreSmokeView>("YLCoreOwnerStatePrefab");
            var unattached = prefab.GetComponent<YLCoreSmokeView>();
            typeof(View).GetProperty("AssetName", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unattached, prefab.name);
            Assert.Throws<InvalidOperationException>(() => module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            Assert.Throws<InvalidOperationException>(() => InvokeViewManager("AddView", module, unattached));
            Assert.Throws<InvalidOperationException>(() => unattached.BoundModel = new YLCoreProbeModel());
            unattached.Close();
            Assert.That(unattached.RemovedCount, Is.Zero);
            Assert.That(module.GetViews<View>(), Is.Empty);
            YLCoreSmokeView loaded = null;
            module.StartAction = () =>
            {
                Assert.That(module.State, Is.EqualTo(ModuleState.Starting));
                Assert.That(ModuleManager.GetModule(module.ModuleName), Is.SameAs(module));
                loaded = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            };
            yield return StartModule();
            Assert.That(loaded.IsLoaded, Is.True);
            var unregistered = new YLCoreDirectViewModule();
            try
            {
                Assert.Throws<InvalidOperationException>(() => unregistered.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                Assert.Throws<InvalidOperationException>(() => InvokeViewManager("AddView", unregistered, loaded));
                Assert.That(loaded.Module, Is.SameAs(module));
                Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { loaded }));
                Assert.That(unregistered.GetViews<View>(), Is.Empty);
            }
            finally { classPool.Restore(new Module[] { unregistered }); }
        }

        [UnityTest]
        public IEnumerator DuplicateRegistrationIsIdempotentAndForeignOwnerCannotAddOrRemoveView()
        {
            yield return StartModule();
            var other = new YLCoreConsumerModule();
            try
            {
                var request = ModuleManager.StartUpModule(other);
                var deadline = Time.realtimeSinceStartup + 10;
                while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(request.isDone, Is.True);
                Assert.That(request.error, Is.Null.Or.Empty);
                var prefab = Template<YLCoreSmokeView>("YLCoreExactOwnerPrefab");
                var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                var model = module.LoadModel<YLCoreProbeModel>();
                view.BoundModel = model;
                var session = ViewSession(view);
                InvokeViewManager("AddView", module, view);
                InvokeViewManager("AddView", module, view);
                Assert.That(ViewSession(view), Is.SameAs(session));
                Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { view }));
                Assert.That(module.GetViews(prefab.name), Is.EqualTo(new View[] { view }));
                Assert.That(view.LoadedCount, Is.EqualTo(1));
                Assert.That(view.Model, Is.SameAs(model));
                Assert.Throws<InvalidOperationException>(() => InvokeViewManager("AddView", other, view));
                InvokeViewManager("RemoveView", other, view);
                Assert.That(other.GetViews<View>(), Is.Empty);
                Assert.That(view.Module, Is.SameAs(module));
                Assert.That(view.RemovedCount, Is.Zero);
                Assert.That(view.gameObject.activeSelf, Is.True);

                var assetName = typeof(View).GetProperty("AssetName", BindingFlags.Instance | BindingFlags.NonPublic);
                assetName.SetValue(view, prefab.name + "Conflict");
                try
                {
                    Assert.Throws<InvalidOperationException>(() => InvokeViewManager("AddView", module, view));
                    Assert.That(module.GetView(prefab.name, view.Id), Is.SameAs(view));
                    Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { view }));
                    Assert.That(module.GetViews(prefab.name + "Conflict"), Is.Empty);
                }
                finally { assetName.SetValue(view, prefab.name); }

                InvokeViewManager("RemoveView", module, view);
                InvokeViewManager("RemoveView", module, view);
                Assert.That(module.GetViews<View>(), Is.Empty);
                Assert.That(view.Module, Is.Null);
                Assert.That(view.Model, Is.Null);
                Assert.That(view.RemovedCount, Is.EqualTo(1));
                Assert.That(view.UnboundCount, Is.EqualTo(1));
                Assert.That(view.ClosedCount, Is.EqualTo(1));
                Assert.That(model.ExitCount, Is.Zero);
            }
            finally
            {
                try { other.Close(); }
                finally { classPool.Restore(new Module[] { other }); }
            }
        }

        [UnityTest]
        public IEnumerator SharedModelBindingsDoNotOwnModelAndOldRegistrationCannotRevive()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            var prefab = Template<YLCoreSmokeView>("YLCoreSharedBindingPrefab");
            var first = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            var second = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            first.BoundModel = model;
            first.BoundModel = model;
            second.BoundModel = model;
            first.Close();
            Assert.That(first.UnboundCount, Is.EqualTo(1));
            Assert.That(first.Model, Is.Null);
            Assert.That(second.Model, Is.SameAs(model));
            Assert.That(model.ExitCount, Is.Zero);
            first = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            first.BoundModel = model;
            module.RemoveModel(model);
            Assert.That(first.Model, Is.Null);
            Assert.That(second.Model, Is.Null);
            Assert.That(model.ExitCount, Is.EqualTo(1));
            module.AddModel(model);
            Assert.That(model.InitCount, Is.EqualTo(2));
            Assert.That(first.Model, Is.Null, "The same Model instance has a new registration token.");
            Assert.That(second.Model, Is.Null);
            first.BoundModel = null;
            first.BoundModel = model;
            Assert.That(first.Model, Is.SameAs(model));
            Assert.That(second.Model, Is.Null);
            second.Close();
            Assert.That(second.UnboundCount, Is.EqualTo(1));
            Assert.That(first.Model, Is.SameAs(model));
            Assert.That(model.ExitCount, Is.EqualTo(1));
            first.Close();
            Assert.That(model.Module, Is.SameAs(module));
            Assert.That(model.ExitCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplicitUnbindFailureClearsSlotPreservesModelAndMarksInstanceForDiscard()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            var prefab = Template<YLCoreSmokeView>("YLCoreExplicitUnbindPrefab");
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            view.BoundModel = model;
            var failure = new InvalidOperationException("explicit unbind");
            view.UnboundAction = previous =>
            {
                Assert.That(previous, Is.SameAs(model));
                Assert.That(view.Model, Is.Null);
                Assert.Throws<InvalidOperationException>(() => view.BoundModel = model);
                Assert.Throws<InvalidOperationException>(() => view.Close());
                Assert.Throws<InvalidOperationException>(() => InvokeViewManager("AddView", module, view));
                Assert.Throws<InvalidOperationException>(() => module.Close());
                view.BoundModel = null;
                throw failure;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => view.BoundModel = null), Is.SameAs(failure));
            Assert.That(view.UnboundCount, Is.EqualTo(1));
            Assert.That(view.Model, Is.Null);
            Assert.That(view.Module, Is.SameAs(module));
            Assert.That(view.IsLoaded, Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(model.ExitCount, Is.Zero);
            Assert.That(module.GetModel<YLCoreProbeModel>(model.Id), Is.SameAs(model));
            view.UnboundAction = null;
            view.BoundModel = model;
            view.Close();
            Assert.That(view.UnboundCount, Is.EqualTo(2));
            Assert.That(model.ExitCount, Is.Zero);
            yield return null;
            Assert.That(view == null, Is.True, "A caught explicit unbind failure still contaminates the pooled instance.");
            var fresh = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            Assert.That(fresh, Is.Not.SameAs(view));
            Assert.That(fresh.LoadedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CloseIsReentrantAndEveryObserverRunsBeforeNecessaryCleanupAndCompletion()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            var prefab = Template<YLCoreSmokeView>("YLCoreExitObserversPrefab");
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            view.BoundModel = model;
            var events = new List<string>();
            var firstError = new InvalidOperationException("first observer");
            var lastError = new ArgumentException("last observer");
            var broadcastError = new InvalidOperationException("first broadcast observer");
            view.onViewRemove = () => { events.Add("first"); throw firstError; };
            view.onViewRemove += () =>
            {
                events.Add("reentrant");
                Assert.That(view.IsLoaded, Is.False);
                view.Close();
                Assert.Throws<InvalidOperationException>(() => InvokeViewManager("AddView", module, view));
                Assert.Throws<InvalidOperationException>(() => view.BoundModel = model);
            };
            view.onViewRemove += () => { events.Add("last"); throw lastError; };
            view.RemoveAction = () => { events.Add("remove"); view.Close(); };
            view.UnboundAction = previous =>
            {
                events.Add("unbind");
                Assert.That(previous, Is.SameAs(model));
                Assert.That(view.Model, Is.Null);
                int before = view.EventCount;
                EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
                Assert.That(view.EventCount, Is.EqualTo(before), "Framework subscriptions end before the unbind hook.");
            };
            Action<View> broadcast = removed =>
            {
                if (!ReferenceEquals(removed, view)) return;
                events.Add("broadcast");
                Assert.That(view.Module, Is.Null);
                Assert.That(view.Model, Is.Null);
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(module.GetViews<View>(), Is.Empty);
            };
            view.AfterClose = () =>
            {
                events.Add("closed");
                Assert.That(view.Module, Is.Null);
                Assert.That(view.gameObject.activeSelf, Is.False);
                view.Close();
            };
            var firstBroadcastToken = EventManager.AddEvent<View>(XFGameFrameworkEvents.ON_REMOVE_VIEW, removed =>
            {
                if (!ReferenceEquals(removed, view)) return;
                events.Add("broadcast.throw");
                throw broadcastError;
            });
            var broadcastToken = EventManager.AddEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast);
            try
            {
                var error = Assert.Throws<AggregateException>(() => view.Close());
                Assert.That(error.InnerExceptions, Is.EqualTo(new Exception[] { firstError, lastError, broadcastError }));
                Assert.That(events, Is.EqualTo(new[] { "first", "reentrant", "last", "remove", "unbind", "broadcast.throw", "broadcast", "closed" }));
                Assert.That(view.RemovedCount, Is.EqualTo(1));
                Assert.That(view.UnboundCount, Is.EqualTo(1));
                Assert.That(view.ClosedCount, Is.EqualTo(1));
                Assert.That(model.Module, Is.SameAs(module));
                Assert.That(model.ExitCount, Is.Zero);
                view.Close();
                Assert.That(events, Has.Count.EqualTo(8));
            }
            finally
            {
                firstBroadcastToken.UnRegister();
                broadcastToken.UnRegister();
                view.onViewRemove = null;
                view.RemoveAction = null;
                view.UnboundAction = null;
                view.AfterClose = null;
            }
            Assert.That(Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null)), Is.SameAs(view), "Observer errors do not contaminate an otherwise healthy pooled instance.");
            var completionError = new InvalidOperationException("completion observer");
            view.AfterClose = () => throw completionError;
            try
            {
                Assert.That(Assert.Throws<InvalidOperationException>(() => view.Close()), Is.SameAs(completionError));
                Assert.That(view.ClosedCount, Is.EqualTo(2));
                Assert.That(view.Module, Is.Null);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally { view.AfterClose = null; }
            Assert.That(Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null)), Is.SameAs(view));
        }

        [UnityTest]
        public IEnumerator GlobalRemovalObserverReplacementSessionDoesNotReceiveOldCompletionHook()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            for (int phase = 0; phase < 2; phase++)
            {
                var prefab = Template<YLCoreSmokeView>("YLCoreBroadcastReplacementPrefab" + phase);
                var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                view.BoundModel = model;
                var oldSession = ViewSession(view);
                YLCoreSmokeView replacement = null;
                var completed = 0;
                view.AfterClose = () =>
                {
                    completed++;
                    view.BoundModel = null;
                    view.Close();
                };
                Action<View> broadcast = removed =>
                {
                    if (!ReferenceEquals(removed, view) || !ReferenceEquals(replacement, null)) return;
                    replacement = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                    replacement.BoundModel = model;
                    Assert.That(ViewSession(replacement), Is.Not.SameAs(oldSession));
                    if (phase == 1) replacement.Close();
                };
                EventManager.AddEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast);
                try
                {
                    view.Close();
                    Assert.That(replacement, Is.SameAs(view));
                    Assert.That(completed, Is.EqualTo(phase), "Only a newly closed replacement session may invoke its own completion hook.");
                    Assert.That(view.ClosedCount, Is.EqualTo(phase));
                    Assert.That(view.RemovedCount, Is.EqualTo(phase + 1));
                    Assert.That(view.IsLoaded, Is.EqualTo(phase == 0));
                    if (phase == 0)
                    {
                        Assert.That(view.Model, Is.SameAs(model));
                        Assert.That(view.Module, Is.SameAs(module));
                        Assert.That(module.GetView<YLCoreSmokeView>(view.Id), Is.SameAs(view));
                        Assert.That(view.gameObject.activeSelf, Is.True);
                    }
                    else
                    {
                        Assert.That(view.Model, Is.Null);
                        Assert.That(view.Module, Is.Null);
                        Assert.That(module.GetViews<View>(), Is.Empty);
                    }
                    Assert.That(model.ExitCount, Is.Zero);
                }
                finally
                {
                    EventManager.RemoveEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast);
                    view.AfterClose = null;
                    view.Close();
                }
            }
        }

        [UnityTest]
        public IEnumerator NecessaryCleanupFailuresFinishExitButDestroyInstanceRatherThanPoolIt()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            for (int phase = 0; phase < 3; phase++)
            {
                var prefab = Template<YLCoreSmokeView>("YLCoreNecessaryFailurePrefab" + phase);
                var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                view.BoundModel = model;
                var failure = new InvalidOperationException(phase == 0 ? "OnRemove failure" : "unbind failure");
                if (phase == 0) view.RemoveAction = () => throw failure;
                if (phase == 2) view.onViewRemove = () => view.BoundModel = null;
                view.UnboundAction = previous =>
                {
                    Assert.That(view.Model, Is.Null);
                    Assert.That(previous, Is.SameAs(model));
                    if (phase != 0) throw failure;
                };
                var closed = 0;
                view.AfterClose = () =>
                {
                    closed++;
                    Assert.That(view.Module, Is.Null);
                    Assert.That(view.Model, Is.Null);
                    Assert.That(view.gameObject.activeSelf, Is.False);
                };
                Assert.That(Assert.Throws<InvalidOperationException>(() => view.Close()), Is.SameAs(failure));
                Assert.That(view.RemovedCount, Is.EqualTo(1));
                Assert.That(view.UnboundCount, Is.EqualTo(1));
                Assert.That(closed, Is.EqualTo(1));
                Assert.That(module.GetViews<View>(), Is.Empty);
                Assert.That(model.Module, Is.SameAs(module));
                Assert.That(model.ExitCount, Is.Zero);
                view.Close();
                yield return null;
                Assert.That(view == null, Is.True, "Necessary cleanup failure must never re-enter the backend's reusable pool.");
                var fresh = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                Assert.That(fresh, Is.Not.SameAs(view));
                Assert.That(fresh.LoadedCount, Is.EqualTo(1));
                fresh.Close();
            }
        }

        [UnityTest]
        public IEnumerator StaleSessionRemovalCannotCloseReusedViewSession()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreStaleSessionPrefab");
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            var previousSession = ViewSession(view);
            var id = view.Id;
            view.Close();
            var reused = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            Assert.That(reused, Is.SameAs(view));
            Assert.That(reused.Id, Is.EqualTo(id));
            Assert.That(ViewSession(reused), Is.Not.SameAs(previousSession));
            var model = module.LoadModel<YLCoreProbeModel>();
            reused.BoundModel = model;
            InvokeViewManager("RemoveView", module, reused, previousSession, false);
            Assert.That(reused.Module, Is.SameAs(module));
            Assert.That(reused.Model, Is.SameAs(model));
            Assert.That(reused.IsLoaded, Is.True);
            Assert.That(reused.gameObject.activeSelf, Is.True);
            Assert.That(module.GetView<YLCoreSmokeView>(id), Is.SameAs(reused));
            Assert.That(reused.RemovedCount, Is.EqualTo(1));
            Assert.That(reused.ClosedCount, Is.EqualTo(1));
            InvokeViewManager("RemoveView", module, reused);
            Assert.That(reused.RemovedCount, Is.EqualTo(2));
            Assert.That(reused.Model, Is.Null);
            Assert.That(model.ExitCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DestroyedViewsEndBindingsAndUnactivatedWrappersArePrunedBySavedIdentity()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            var prefab = Template<YLCoreSmokeView>("YLCoreDestroyedViewPrefab");
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            view.BoundModel = model;
            var id = view.Id;
            Object.Destroy(view.gameObject);
            yield return null;
            Assert.That(view == null, Is.True);
            Assert.That(view.Module, Is.Null);
            Assert.That(view.Model, Is.Null);
            Assert.That(view.RemovedCount, Is.EqualTo(1));
            Assert.That(view.UnboundCount, Is.EqualTo(1));
            Assert.That(module.GetView<View>(id), Is.Null);
            Assert.That(module.GetViews(prefab.name), Is.Empty);
            Assert.That(model.Module, Is.SameAs(module));
            Assert.That(model.ExitCount, Is.Zero);

            // 从未激活的对象不保证收到OnDestroy，索引扫描和查询均依靠保存身份收尾。
            var inactive = Template<YLCoreSmokeView>("YLCoreUnactivatedView").GetComponent<YLCoreSmokeView>();
            typeof(View).GetProperty("AssetName", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inactive, "YLCoreUnactivatedView");
            InvokeViewManager("AddView", module, inactive);
            id = inactive.Id;
            Object.Destroy(inactive.gameObject);
            yield return null;
            yield return null;
            Assert.That(inactive == null, Is.True);
            Assert.That(ViewSession(inactive), Is.Null, "Destruction cleanup does not require a business query.");
            Assert.That(module.GetView<View>(id), Is.Null);
            Assert.That(inactive.Module, Is.Null);
            Assert.That(ViewSession(inactive), Is.Null);
            Assert.That(inactive.RemovedCount, Is.EqualTo(1));
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(module.GetViews("YLCoreUnactivatedView"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AllClearOverloadsContinueAfterSiblingFailureAndPreserveModelRegistration()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            for (int phase = 0; phase < 3; phase++)
            {
                var prefab = Template<YLCoreSmokeView>("YLCoreClearFailurePrefab" + phase);
                var first = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                var sibling = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                first.BoundModel = model;
                sibling.BoundModel = model;
                var failure = new InvalidOperationException("clear observer " + phase);
                first.onViewRemove = () => throw failure;
                try
                {
                    var error = Assert.Throws<InvalidOperationException>(() =>
                    {
                        if (phase == 0) module.ClearView();
                        else if (phase == 1) module.ClearView<View>();
                        else module.ClearView(prefab.name);
                    });
                    Assert.That(error, Is.SameAs(failure));
                    Assert.That(first.Module, Is.Null);
                    Assert.That(sibling.Module, Is.Null);
                    Assert.That(first.Model, Is.Null);
                    Assert.That(sibling.Model, Is.Null);
                    Assert.That(first.RemovedCount, Is.EqualTo(1));
                    Assert.That(sibling.RemovedCount, Is.EqualTo(1));
                    Assert.That(sibling.ClosedCount, Is.EqualTo(1));
                    Assert.That(sibling.gameObject.activeSelf, Is.False);
                    Assert.That(module.GetViews<View>(), Is.Empty);
                    Assert.That(module.State, Is.EqualTo(ModuleState.Running));
                    Assert.That(model.Module, Is.SameAs(module));
                    Assert.That(model.ExitCount, Is.Zero);
                }
                finally { first.onViewRemove = null; }
            }
        }

        [UnityTest]
        public IEnumerator InitializationPublishesLoadedOnlyAfterCallbacksAndRejectsCloseAndDuplicateAdd()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreInitializingPrefab");
            var view = prefab.GetComponent<YLCoreSmokeView>();
            prefab.SetActive(true);
            yield return null;
            Assert.That(view.Module, Is.Null, "Start must not guess the current Module for a scene View.");
            Assert.That(view.IsLoaded, Is.False);
            Assert.That(view.LoadedCount, Is.Zero);
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.Zero);
            view.LoadedAction = () =>
            {
                Assert.That(view.IsLoaded, Is.False);
                Assert.That(view.Module, Is.SameAs(module));
                Assert.Throws<InvalidOperationException>(() => view.Close());
                Assert.Throws<InvalidOperationException>(() => InvokeViewManager("AddView", module, view));
            };
            InvokeViewManager("InitView", module, view, prefab.name, null, new object[0]);
            Assert.That(view.IsLoaded, Is.True);
            Assert.That(view.LoadedCount, Is.EqualTo(1));
            Assert.That(view.ArgsCount, Is.EqualTo(1));
            Assert.That(view.RemovedCount, Is.Zero);
            view.Close();
            Assert.That(view.IsLoaded, Is.False);
            Assert.That(view.Module, Is.Null);
        }

        [UnityTest]
        public IEnumerator FailedInitializationRollsBackRegistrationAndBindingWithoutExitingModel()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            var prefab = Template<YLCoreSmokeView>("YLCoreInitializationRollbackPrefab");
            var view = prefab.GetComponent<YLCoreSmokeView>();
            var failure = new InvalidOperationException("view initialization");
            view.LoadedAction = () =>
            {
                view.BoundModel = model;
                throw failure;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => InvokeViewManager("InitView", module, view, prefab.name, null, new object[0])), Is.SameAs(failure));
            Assert.That(view.Module, Is.Null);
            Assert.That(view.Model, Is.Null);
            Assert.That(view.IsLoaded, Is.False);
            Assert.That(view.UnboundCount, Is.EqualTo(1));
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(model.Module, Is.SameAs(module));
            Assert.That(model.ExitCount, Is.Zero);
            yield return null;
            Assert.That(view == null, Is.True);
        }

        [UnityTest]
        public IEnumerator PanelRegistrationCleanupDoesNotDependOnListenerSwitchOrSuccessfulGlobalBroadcast()
        {
            yield return StartModule();
            var type = typeof(PanelManager);
            var instanceField = type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousManager = instanceField.GetValue(null);
            // 独立构造不经过Instance属性，不注册场景或编辑器事件，也不污染已有manager。
            var manager = Activator.CreateInstance(type, true);
            instanceField.SetValue(null, manager);
            var orders = (IDictionary)type.GetField("uiOrders", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var persistentOrders = (IDictionary)type.GetField("dontDestroyUIOrders", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var queue = (IList)type.GetField("panelQueues", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            var current = type.GetField("_currentPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            var listener = type.GetField("multiPanelListener", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                for (int phase = 0; phase < 2; phase++)
                {
                    var prefab = Template<YLCoreSmokePanel>("YLCorePanelCleanupPrefab" + phase, true);
                    var previous = Track(module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: null));
                    var panel = Track(module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: null));
                    Assert.That(orders.Contains(previous), Is.True);
                    Assert.That(orders.Contains(panel), Is.True);
                    previous.SortingOrder = 10;
                    panel.SortingOrder = 20;
                    persistentOrders.Add(panel, 20);
                    queue.Add(previous);
                    queue.Add(panel);
                    queue.Add(panel);
                    current.SetValue(manager, panel);
                    listener.SetValue(manager, phase == 1);
                    previous.Canvas.enabled = false;
                    var failure = new InvalidOperationException("global panel observer " + phase);
                    var followingCalls = 0;
                    Action<View> broadcast = removed =>
                    {
                        if (!ReferenceEquals(removed, panel)) return;
                        Assert.That(orders.Contains(panel), Is.False);
                        Assert.That(persistentOrders.Contains(panel), Is.False);
                        Assert.That(queue, Is.EqualTo(new Panel[] { previous }));
                        Assert.That(current.GetValue(manager), Is.SameAs(previous));
                        Assert.That(previous.Canvas.enabled, Is.EqualTo(phase == 1));
                        Assert.That(panel.Module, Is.Null);
                        throw failure;
                    };
                    var broadcastToken = EventManager.AddEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast);
                    var followingToken = EventManager.AddEvent<View>(XFGameFrameworkEvents.ON_REMOVE_VIEW, removed =>
                    {
                        if (!ReferenceEquals(removed, panel)) return;
                        followingCalls++;
                        Assert.That(panel.IsLoaded, Is.False);
                        Assert.That(panel.Module, Is.Null);
                        Assert.That(panel.gameObject.activeSelf, Is.False);
                        Assert.That(orders.Contains(panel), Is.False);
                        Assert.That(persistentOrders.Contains(panel), Is.False);
                        Assert.That(queue, Is.EqualTo(new Panel[] { previous }));
                        Assert.That(current.GetValue(manager), Is.SameAs(previous));
                        Assert.That(previous.Canvas.enabled, Is.EqualTo(phase == 1));
                    });
                    try
                    {
                        Assert.That(Assert.Throws<InvalidOperationException>(() => panel.Close()), Is.SameAs(failure));
                        Assert.That(panel.gameObject.activeSelf, Is.False);
                        Assert.That(panel.RemovedCount, Is.EqualTo(1));
                        Assert.That(panel.ClosedCount, Is.EqualTo(1));
                        Assert.That(followingCalls, Is.EqualTo(1), "A failed removal observer must not skip the next observer.");
                        Assert.That(module.State, Is.EqualTo(ModuleState.Running));
                        Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.EqualTo(new[] { previous }));
                    }
                    finally
                    {
                        broadcastToken.UnRegister();
                        followingToken.UnRegister();
                    }
                    Assert.That(Track(module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: null)), Is.SameAs(panel));
                    panel.Close();
                    previous.Close();
                    Assert.That(orders, Is.Empty);
                    Assert.That(persistentOrders, Is.Empty);
                    Assert.That(queue, Is.Empty);
                    Assert.That(current.GetValue(manager), Is.Null);
                }
            }
            finally { instanceField.SetValue(null, previousManager); }
        }

        [UnityTest]
        public IEnumerator DirectPrefabInitializesOncePerLoadAndCloseReturnsSameObjectToBackendCache()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreDirectViewPrefab");
            var args = LoadViewArgs.New();
            classPool.Track(args);
            args.SetInt("token", 41);
            var first = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: args));
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Module, Is.SameAs(module));
            Assert.That(first.LoadedCount, Is.EqualTo(1));
            Assert.That(first.IsLoaded, Is.True);
            Assert.That(first.ArgsCount, Is.EqualTo(1));
            Assert.That(first.Token, Is.EqualTo(41));
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(module.GetView<YLCoreSmokeView>(prefab.name, first.Id), Is.SameAs(first));
            yield return null;
            Assert.That(first.LoadedCount, Is.EqualTo(1), "Start must not initialize an already loaded View again.");
            var removed = 0;
            first.onViewRemove += () => removed++;
            first.Close();
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(first != null, Is.True, "Close is cache return, not destruction or asset release.");
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
            Assert.That(module.GetViews(prefab.name), Is.Empty);
            Assert.That(first.RemovedCount, Is.EqualTo(1));
            Assert.That(first.Module, Is.Null);
            Assert.That(first.IsLoaded, Is.False);
            Assert.That(first.ClosedCount, Is.EqualTo(1));
            first.Close();
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(first.ClosedCount, Is.EqualTo(1));
            var second = Track(module.LoadView<YLCoreSmokeView>(prefab, null, new object[] { "reused" }));
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.LoadedCount, Is.EqualTo(2));
            Assert.That(second.Parameter, Is.EqualTo("reused"));
            Assert.That(second.Token, Is.Zero, "Recycled load arguments must not carry previous values.");
            Assert.That(module.GetViews<YLCoreSmokeView>(), Has.Count.EqualTo(1));
            module.Close();
            Assert.That(second.gameObject.activeSelf, Is.False);
            Assert.That(second.RemovedCount, Is.EqualTo(2));
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator EnableDisableAndCacheReuseSubscribeExactlyOnceAndUnsubscribe()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreDirectEventsPrefab");
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.EqualTo(1));
            view.gameObject.SetActive(false);
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.EqualTo(1));
            view.gameObject.SetActive(true);
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.EqualTo(2));
            view.Close();
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.EqualTo(2));
            Assert.That(Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null)), Is.SameAs(view));
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.EqualTo(3));
            Assert.That(view.EnabledCount, Is.EqualTo(3));
            Assert.That(view.DisabledCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator OrdinaryBusSnapshotCannotEnterReopenedCachedViewSession()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreBusSnapshotSessionPrefab");
            YLCoreSmokeView view = null;
            YLCoreSmokeView replacement = null;
            var reopen = true;
            var token = EventBus.Default.Register(YLCoreSmokeView.ProbeEvent, () =>
            {
                if (!reopen) return;
                reopen = false;
                view.Close();
                replacement = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            });
            try
            {
                view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                var session = ViewSession(view);
                EventBus.Default.Send(YLCoreSmokeView.ProbeEvent);
                Assert.That(replacement, Is.SameAs(view));
                Assert.That(ViewSession(replacement), Is.Not.SameAs(session));
                Assert.That(replacement.EventCount, Is.Zero, "The old ordinary snapshot must not enter the replacement session.");
                Assert.That(replacement.IsLoaded, Is.True);
                Assert.That(replacement.Module, Is.SameAs(module));
                Assert.That(replacement.RemovedCount, Is.EqualTo(1));
                Assert.That(replacement.LoadedCount, Is.EqualTo(2));
                EventBus.Default.Send(YLCoreSmokeView.ProbeEvent);
                Assert.That(replacement.EventCount, Is.EqualTo(1), "The replacement subscription receives the next round exactly once.");
            }
            finally { token.UnRegister(); }
        }

        [UnityTest]
        public IEnumerator ViewCallbackCloseOrDisableEnableSkipsRemainingSameRoundCallbacks()
        {
            yield return StartModule();
            YLCoreSmokeView.DuplicateProbeHandler = true;
            try
            {
                for (int phase = 0; phase < 2; phase++)
                {
                    var prefab = Template<YLCoreSmokeView>("YLCoreReentrantEventRoundPrefab" + phase);
                    var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                    var session = ViewSession(view);
                    YLCoreSmokeView replacement = null;
                    view.EventAction = () =>
                    {
                        view.EventAction = null;
                        if (phase == 0)
                        {
                            view.Close();
                            replacement = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                        }
                        else
                        {
                            view.gameObject.SetActive(false);
                            view.gameObject.SetActive(true);
                            replacement = view;
                        }
                    };
                    EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
                    Assert.That(replacement, Is.SameAs(view));
                    Assert.That(ViewSession(replacement), phase == 0 ? Is.Not.SameAs(session) : Is.SameAs(session));
                    Assert.That(replacement.EventCount, Is.EqualTo(1), "Remaining callbacks belong to the old session or subscription epoch.");
                    Assert.That(replacement.IsLoaded, Is.True);
                    Assert.That(replacement.Module, Is.SameAs(module));
                    EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
                    Assert.That(replacement.EventCount, Is.EqualTo(3), "Both handlers remain registered exactly once in the new round.");
                    replacement.Close();
                }
            }
            finally { YLCoreSmokeView.DuplicateProbeHandler = false; }
        }

        [UnityTest]
        public IEnumerator PartialEventRegistrationSignatureFailureRollsBackOnlyItsOwnTokens()
        {
            yield return StartModule();
            var conflict = "YLCore.PartialRegistration." + Guid.NewGuid().ToString("N");
            var sharedCalls = 0;
            var conflictCalls = 0;
            Action shared = () => sharedCalls++;
            IUnRegister sharedToken = null;
            IUnRegister conflictToken = null;
            YLCoreSmokeView failed = null;
            try
            {
                YLCoreSmokeView.SharedEventHandler = shared;
                var prefab = Template<YLCoreSmokeView>("YLCorePartialEventRegistrationPrefab");
                var sibling = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                sharedToken = EventBus.Default.Register(YLCoreSmokeView.ProbeEvent, shared);
                conflictToken = EventManager.AddEvent(conflict, () => conflictCalls++);
                YLCoreSmokeView.SignatureConflictEvent = conflict;
                YLCoreSmokeView.LoadingProbe = view => failed = Track(view);
                Assert.Throws<InvalidOperationException>(() => module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                Assert.That(failed, Is.Not.Null);
                Assert.That(failed.Module, Is.Null);
                Assert.That(failed.IsLoaded, Is.False);
                Assert.That(failed.RemovedCount, Is.EqualTo(1));
                Assert.That(failed.ClosedCount, Is.Zero);
                Assert.That(module.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { sibling }));
                Assert.That(sibling.IsLoaded, Is.True);
                Assert.That(sibling.RemovedCount, Is.Zero);
                Assert.That(module.State, Is.EqualTo(ModuleState.Running));
                Assert.That(EventBus.Default.ListenerCount(YLCoreSmokeView.ProbeEvent), Is.EqualTo(2));
                EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
                Assert.That(sharedCalls, Is.EqualTo(2), "Rollback preserves both the sibling View and the equal direct Default callback.");
                EventManager.TriggerEvent(conflict);
                Assert.That(conflictCalls, Is.EqualTo(1));
                sibling.Close();
                EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
                Assert.That(sharedCalls, Is.EqualTo(3), "Closing the sibling removes only its own subscription token.");
            }
            finally
            {
                YLCoreSmokeView.SignatureConflictEvent = null;
                YLCoreSmokeView.SharedEventHandler = null;
                YLCoreSmokeView.LoadingProbe = null;
                sharedToken?.UnRegister();
                conflictToken?.UnRegister();
                EventBus.Default.RemoveKey(conflict);
            }
            yield return null;
            Assert.That(failed == null, Is.True, "An activation failure discards the acquired instance rather than pooling it.");
        }

        [UnityTest]
        public IEnumerator MultipleDirectViewsHaveIndependentIdsAndClosingOneKeepsOtherRegistered()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreDirectMultiPrefab");
            var first = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            var second = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(module.GetViews<YLCoreSmokeView>(), Has.Count.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => module.GetView<YLCoreSmokeView>());
            Assert.Throws<InvalidOperationException>(() => module.GetView(prefab.name));
            Assert.Throws<InvalidOperationException>(() => module.GetView<YLCoreSmokeView>(prefab.name));
            Assert.That(module.GetView<View>(), Is.Null, "Type-only lookup uses the exact runtime type.");
            Assert.That(module.GetView<YLCoreSmokeView>(first.Id), Is.SameAs(first), "The prefab name is deliberately not the component type name.");
            Assert.That(module.GetView<View>(second.Id), Is.SameAs(second));
            Assert.Throws<InvalidCastException>(() => module.GetView<YLCoreSmokePanel>(prefab.name, first.Id));
            first.Close();
            Assert.That(module.GetView<YLCoreSmokeView>(prefab.name, first.Id), Is.Null);
            Assert.That(module.GetView<YLCoreSmokeView>(prefab.name, second.Id), Is.SameAs(second));
            Assert.That(second.gameObject.activeSelf, Is.True);
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { second }));
            Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { second }));
            Assert.That(module.GetView<YLCoreSmokeView>(prefab.name), Is.SameAs(second));
            Assert.Throws<InvalidCastException>(() => module.GetView<YLCoreSmokePanel>(prefab.name));
        }

        [UnityTest]
        public IEnumerator InactivePanelAttachmentKeepsVisiblePanelUntilActivationAndRetainsOrderOnDisable()
        {
            yield return StartModule();
            YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
            try
            {
                var prefab = Template<YLCoreSmokePanel>("YLCoreInactivePanelPrefab", true);
                var visible = Track(module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: null));
                var parent = new GameObject("YLCoreInactivePanelParent");
                objects.Add(parent);
                parent.SetActive(false);
                var obj = new GameObject("YLCoreInactiveScenePanel", typeof(RectTransform));
                objects.Add(obj);
                obj.transform.SetParent(parent.transform);
                var inactive = obj.AddComponent<YLCoreSmokePanel>();
                module.AttachView(inactive);
                Assert.That(inactive.IsLoaded, Is.True);
                Assert.That(inactive.gameObject.activeSelf, Is.True);
                Assert.That(inactive.gameObject.activeInHierarchy, Is.False);
                Assert.That(visible.Canvas.enabled, Is.True, "Inactive preparation cannot hide the current Panel.");
                var orders = (IDictionary)typeof(PanelManager).GetField("uiOrders", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(typeof(PanelManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
                Assert.That(orders.Contains(inactive), Is.True);
                module.UIStartSortOrder = 91;
                Assert.That(inactive.Canvas.sortingOrder, Is.EqualTo(inactive.SortingOrder + 91), "Unnamed attached Panels receive owner sorting offsets.");
                parent.SetActive(true);
                Assert.That(visible.Canvas.enabled, Is.False);
                Assert.That(inactive.Canvas.enabled, Is.True);
                if (!inactive.Canvas.isRootCanvas) Assert.That(inactive.Canvas.overrideSorting, Is.True);
                Assert.That(inactive.Canvas.sortingOrder, Is.EqualTo(inactive.DisplaySortingOrder), "First native Canvas activation must restore the prepared order.");
                YLCoreSmokePanel.VisibleProbe = panel => EventManager.TriggerEvent(YLCoreSmokePanel.ProbeEvent);
                parent.SetActive(false);
                Assert.That(inactive.EventCount, Is.Zero, "The disabled Panel must unsubscribe before restoring the previous Panel's business callback.");
                Assert.That(visible.EventCount, Is.EqualTo(1));
                Assert.That(visible.Canvas.enabled, Is.True);
                Assert.That(inactive.IsLoaded, Is.True);
                Assert.That(inactive.Module, Is.SameAs(module));
                Assert.That(orders.Contains(inactive), Is.True, "Disable pauses display, not business registration or sorting ownership.");
                inactive.Close();
                Assert.That(orders.Contains(inactive), Is.False);
                visible.Close();
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator PanelActivationCallbackCannotRemoveAnotherPanelsSharedEventSubscription()
        {
            yield return StartModule();
            YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
            YLCoreSmokePanel.UseSharedHandler = true;
            try
            {
                var prefab = Template<YLCoreSmokePanel>("YLCoreSharedPanelEventPrefab", true);
                var first = Track(module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: null));
                YLCoreSmokePanel.InvisibleProbe = panel =>
                {
                    Assert.That(panel, Is.SameAs(first));
                    var activating = module.GetViews<YLCoreSmokePanel>().Find(item => item != first);
                    Assert.That(activating, Is.Not.Null);
                    activating.gameObject.SetActive(false);
                };
                var second = Track(module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: null));
                Assert.That(second.IsLoaded, Is.True);
                Assert.That(second.gameObject.activeSelf, Is.False);
                Assert.That(first.Canvas.enabled, Is.True);
                EventManager.TriggerEvent(YLCoreSmokePanel.ProbeEvent);
                Assert.That(YLCoreSmokePanel.SharedEventCount, Is.EqualTo(1), "Rollback of an activating Panel cannot remove the other Panel's equal delegate.");
                first.Close();
                second.Close();
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator DirectPanelUsesInheritedViewPrefabContractAndInitializesCanvasOnlyOnce()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokePanel>("YLCoreDirectPanelPrefab", true);
            var parent = new GameObject("YLCoreDirectPanelParent", typeof(RectTransform), typeof(Canvas));
            objects.Add(parent);
            parent.SetActive(false);
            var args = LoadViewArgs.New();
            classPool.Track(args);
            args.SetInt("token", 17);
            var panel = Track(module.LoadView<YLCoreSmokePanel>(prefab, parent.transform, loadViewArgs: args));
            Assert.That(panel.IsLoaded, Is.True);
            Assert.That(panel.gameObject.activeInHierarchy, Is.False);
            parent.SetActive(true);
            Assert.That(panel.Canvas, Is.SameAs(panel.GetComponent<Canvas>()));
            Assert.That(panel.Canvas.overrideSorting, Is.True);
            Assert.That(panel.Canvas.sortingOrder, Is.EqualTo(panel.DisplaySortingOrder));
            Assert.That(panel.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(panel.Token, Is.EqualTo(17));
            panel.SortingOrder = 30;
            Assert.That(panel.Canvas.sortingOrder, Is.EqualTo(30 + module.UIStartSortOrder));
            var canvas = panel.Canvas;
            panel.Close();
            Assert.That(panel.RemovedCount, Is.EqualTo(1));
            var reused = Track(module.LoadView<YLCoreSmokePanel>(prefab, parent.transform, loadViewArgs: null));
            Assert.That(reused, Is.SameAs(panel));
            Assert.That(reused.Canvas, Is.SameAs(canvas));
            Assert.That(reused.GetComponents<Canvas>(), Has.Length.EqualTo(1));
            Assert.That(reused.LoadedCount, Is.EqualTo(2));
            Assert.That(reused.ArgsCount, Is.EqualTo(2));
            yield return null;
            Assert.That(reused.LoadedCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ModuleCloseOrdersControllerViewAndRemainingModelsAndDetachesViewOwner()
        {
            yield return StartModule();
            var events = new List<string>();
            var bound = new YLCoreProbeModel { Id = 1, ExitAction = _ => events.Add("bound.exit") };
            var remaining = new YLCoreProbeModel { Id = 2, ExitAction = _ => events.Add("remaining.exit") };
            module.AddModel(bound);
            module.AddModel(remaining);
            var controller = module.LoadController<YLCoreProbeController>();
            controller.ExitAction = _ => events.Add("controller.exit");
            var prefab = Template<YLCoreSmokeView>("YLCoreCloseOrderPrefab");
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            view.BoundModel = bound;
            view.onViewRemove = () => events.Add("view.remove");
            view.RemoveAction = () => events.Add("view.onRemove");
            view.UnboundAction = previous =>
            {
                Assert.That(previous, Is.SameAs(bound));
                Assert.That(view.Model, Is.Null);
                Assert.That(bound.ExitCount, Is.Zero);
                events.Add("view.unbound");
            };
            view.AfterClose = () =>
            {
                Assert.That(view.Module, Is.Null);
                Assert.That(view.Model, Is.Null);
                Assert.That(view.IsLoaded, Is.False);
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(module.GetViews<View>(), Is.Empty);
                events.Add("view.closed");
            };
            module.CloseAction = () =>
            {
                events.Add("module.close");
                Assert.That(module.GetView(prefab.name), Is.SameAs(view));
                Assert.That(controller.ExitCount, Is.Zero);
                Assert.That(bound.ExitCount, Is.Zero);
            };
            module.Close();
            Assert.That(events, Is.EqualTo(new[] { "module.close", "controller.exit", "view.remove", "view.onRemove", "view.unbound", "view.closed", "bound.exit", "remaining.exit" }));
            Assert.That(bound.ExitCount, Is.EqualTo(1));
            Assert.That(remaining.ExitCount, Is.EqualTo(1));
            Assert.That(view.Module, Is.Null);
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ViewsRemovedInsideCloseHooksStillLoseOwnerAtModuleClose()
        {
            for (int phase = 0; phase < 2; phase++)
            {
                yield return StartModule();
                var prefab = Template<YLCoreSmokeView>("YLCoreHookClosePrefab" + phase);
                var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                var controller = module.LoadController<YLCoreProbeController>();
                module.CloseAction = phase == 0 ? (Action)(() => view.Close()) : null;
                controller.ExitAction = _ => view.Close();
                view.onViewRemove = () => Assert.That(view.Module, Is.SameAs(module));
                module.Close();
                Assert.That(view.RemovedCount, Is.EqualTo(1));
                Assert.That(view.Module, Is.Null);
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator ViewReloadedByAnotherModuleDuringCloseKeepsNewOwnerAndIndexes()
        {
            yield return StartModule();
            var other = new YLCoreConsumerModule();
            YLCoreSmokeView view = null;
            try
            {
                var request = ModuleManager.StartUpModule(other);
                var deadline = Time.realtimeSinceStartup + 10;
                while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(request.isDone, Is.True);
                Assert.That(request.error, Is.Null.Or.Empty);
                var prefab = Template<YLCoreSmokeView>("YLCoreTransferOnClosePrefab");
                view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                var sibling = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                var bound = module.LoadModel<YLCoreProbeModel>();
                view.BoundModel = bound;
                YLCoreSmokeView reloaded = null;
                YLCoreSmokeView reloadedSibling = null;
                view.AfterClose = () =>
                {
                    Assert.That(view.Module, Is.Null);
                    Assert.That(view.Model, Is.Null);
                    Assert.That(view.gameObject.activeSelf, Is.False);
                    Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { sibling }));
                    sibling.Close();
                    reloaded = Track(other.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                    reloadedSibling = Track(other.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                };
                module.Close();
                view.AfterClose = null;
                Assert.That(reloaded, Is.SameAs(view));
                Assert.That(reloadedSibling, Is.SameAs(sibling));
                Assert.That(view.Module, Is.SameAs(other));
                Assert.That(sibling.Module, Is.SameAs(other));
                Assert.That(sibling.RemovedCount, Is.EqualTo(1), "The old Module snapshot must not close the sibling's replacement session.");
                Assert.That(bound.ExitCount, Is.EqualTo(1));
                Assert.That(view.ClosedCount, Is.EqualTo(1));
                Assert.That(other.GetView(prefab.name, view.Id), Is.SameAs(view));
                Assert.That(other.GetView(prefab.name, sibling.Id), Is.SameAs(sibling));
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
                view.Close();
                Assert.That(other.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { sibling }));
                Assert.That(view.RemovedCount, Is.EqualTo(2));
                sibling.Close();
                Assert.That(other.GetViews<YLCoreSmokeView>(), Is.Empty);
                Assert.That(sibling.RemovedCount, Is.EqualTo(2));
            }
            finally
            {
                if (view) view.AfterClose = null;
                other.Close();
                classPool.Restore(new Module[] { other });
            }
        }

        [UnityTest]
        public IEnumerator ViewCleanupErrorDoesNotBlockSiblingViewsOrRemainingModelCleanup()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            var prefab = Template<YLCoreSmokeView>("YLCoreCloseFailurePrefab");
            var first = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            var sibling = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            first.BoundModel = model;
            var viewError = new InvalidOperationException("view cleanup");
            first.onViewRemove = () => throw viewError;
            var error = Assert.Throws<AggregateException>(() => module.Close());
            Assert.That(error.InnerExceptions, Is.EqualTo(new[] { viewError }));
            Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
            Assert.That(ModuleManager.GetModule(module.ModuleName), Is.Null);
            Assert.That(model.ExitCount, Is.EqualTo(1));
            Assert.That(model.Module, Is.Null);
            Assert.That(first.Module, Is.Null);
            Assert.That(sibling.Module, Is.Null);
            Assert.That(sibling.RemovedCount, Is.EqualTo(1));
            Assert.That(sibling.gameObject.activeSelf, Is.False);
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
            first.Close();
            module.Close();
            Assert.That(model.ExitCount, Is.EqualTo(1));
            first.onViewRemove = null;
        }

        [UnityTest]
        public IEnumerator ModuleCloseFlattensViewErrorsAndContinuesCompletionHooksAndRemainingCleanup()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreAggregatedExitPrefab");
            var first = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            var sibling = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            var controller = module.LoadController<YLCoreProbeController>();
            var model = module.LoadModel<YLCoreProbeModel>();
            first.BoundModel = model;
            var controllerError = new InvalidOperationException("controller exit");
            var observerError = new ArgumentException("view observer");
            var removeError = new InvalidOperationException("view OnRemove");
            var unbindError = new InvalidOperationException("view unbind");
            var completedError = new ArgumentException("view OnClosed");
            var siblingError = new InvalidOperationException("sibling observer");
            var modelError = new InvalidOperationException("model exit");
            controller.ExitAction = _ => throw controllerError;
            first.onViewRemove = () => throw observerError;
            first.RemoveAction = () => throw removeError;
            first.UnboundAction = previous =>
            {
                Assert.That(first.Model, Is.Null);
                throw unbindError;
            };
            first.AfterClose = () =>
            {
                Assert.That(first.Module, Is.Null);
                Assert.That(first.gameObject.activeSelf, Is.False);
                throw completedError;
            };
            sibling.onViewRemove = () => throw siblingError;
            model.ExitAction = _ => throw modelError;
            var error = Assert.Throws<AggregateException>(() => module.Close());
            Assert.That(error.InnerExceptions, Is.EquivalentTo(new Exception[]
            {
                controllerError, observerError, removeError, unbindError, completedError, siblingError, modelError
            }));
            Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
            Assert.That(ModuleManager.GetModule(module.ModuleName), Is.Null);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(first.Model, Is.Null);
            Assert.That(first.UnboundCount, Is.EqualTo(1));
            Assert.That(first.ClosedCount, Is.EqualTo(1));
            Assert.That(sibling.RemovedCount, Is.EqualTo(1));
            Assert.That(sibling.ClosedCount, Is.EqualTo(1));
            Assert.That(controller.ExitCount, Is.EqualTo(1));
            Assert.That(model.ExitCount, Is.EqualTo(1));
            Assert.That(controller.Module, Is.Null);
            Assert.That(model.Module, Is.Null);
            first.Close();
            module.Close();
            yield return null;
            Assert.That(first == null, Is.True);
            Assert.That(sibling != null, Is.True);
        }

        [UnityTest]
        public IEnumerator LegacyGenericMismatchIsRejectedBeforeInitialization()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreLegacyMismatchPrefab");
#pragma warning disable CS0618
            Assert.Throws<InvalidCastException>(() => module.LoadView<YLCoreSmokePanel>(prefab, null, new object[0]));
#pragma warning restore CS0618
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(prefab.GetComponent<YLCoreSmokeView>().LoadedCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RejectedSynchronousInputsConsumeArgumentsWithoutCreatingViewsOrPools()
        {
            var prefab = Template<YLCoreSmokeView>("YLCoreInvalidInputsPrefab");
            var args = NewArgs();
            args.SetObject("retained", prefab);
            Assert.Throws<InvalidOperationException>(() => module.LoadView(prefab, loadViewArgs: args));
            AssertArgsReleased(args);
            args = NewArgs();
            Assert.Throws<InvalidOperationException>(() => module.LoadPanel("unused", loadViewArgs: args));
            AssertArgsReleased(args);
            yield return StartModule();
            args = NewArgs();
            Assert.Throws<ArgumentNullException>(() => module.LoadView((GameObject)null, loadViewArgs: args));
            AssertArgsReleased(args);
            args = NewArgs();
            Assert.Throws<ArgumentException>(() => module.LoadView(" ", loadViewArgs: args));
            AssertArgsReleased(args);
            args = NewArgs();
            Assert.Throws<InvalidCastException>(() => module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: args));
            AssertArgsReleased(args);
            var noView = new GameObject("YLCoreNoViewPrefab");
            objects.Add(noView);
            args = NewArgs();
            Assert.Throws<InvalidOperationException>(() => module.LoadView(noView, loadViewArgs: args));
            AssertArgsReleased(args);
            var deadParent = new GameObject("YLCoreDeadParent").transform;
            Object.Destroy(deadParent.gameObject);
            yield return null;
            args = NewArgs();
            Assert.Throws<ArgumentException>(() => module.LoadView(prefab, deadParent, loadViewArgs: args));
            AssertArgsReleased(args);
            Assert.That(module.GetViews<View>(), Is.Empty);
            var loader = typeof(XFABManager.GameObjectLoader).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (loader != null)
            {
                var pools = (IDictionary)loader.GetType().GetField("allPools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
                Assert.That(pools.Contains(prefab.GetHashCode()), Is.False, "Prefab validation precedes backend acquisition.");
            }
        }

        [UnityTest]
        public IEnumerator InitializationIsTemporarilyQueryableAndArgsCannotBeReenteredOrRetained()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreLoadTransactionPrefab");
            var siblingPrefab = Template<YLCoreSmokePanel>("YLCoreNestedInitializationPrefab", true);
            var args = NewArgs();
            args.SetInt("token", 91);
            YLCoreSmokeView.LoadingProbe = view =>
            {
                Track(view);
                Assert.That(view.IsLoaded, Is.False);
                Assert.That(module.GetView(prefab.name, view.Id), Is.SameAs(view));
                Assert.That(module.GetView<YLCoreSmokeView>(view.Id), Is.SameAs(view));
                Assert.Throws<InvalidOperationException>(() => InvokeViewManager("InitView", module, view, prefab.name, null, new object[0]));
                Assert.Throws<InvalidOperationException>(() => view.Close());
                Assert.Throws<InvalidOperationException>(() => module.Close());
                Assert.Throws<InvalidOperationException>(() => ModuleManager.Shutdown());
                Assert.Throws<InvalidOperationException>(() => args.GetInt("token"));
                Assert.Throws<InvalidOperationException>(() => module.LoadView(prefab, loadViewArgs: args));
                EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
                Assert.That(view.EventCount, Is.Zero);
                var sibling = Track(module.LoadView<YLCoreSmokePanel>(siblingPrefab, loadViewArgs: null));
                Assert.That(sibling.IsLoaded, Is.True, "Other valid objects may initialize inside the transaction.");
            };
            YLCoreSmokeView.ArgsProbe = (view, borrowed) =>
            {
                Assert.That(borrowed, Is.SameAs(args));
                Assert.That(borrowed.GetInt("token"), Is.EqualTo(91));
                Assert.Throws<InvalidOperationException>(() => borrowed.SetInt("token", 2));
                Assert.Throws<InvalidOperationException>(() => module.LoadView(prefab, loadViewArgs: borrowed));
                Assert.That(borrowed.GetInt("token"), Is.EqualTo(91), "Rejected second takeover cannot recycle the first operation's input.");
                Assert.That(view.IsLoaded, Is.False);
            };
            YLCoreSmokeView.EnabledProbe = view =>
            {
                Assert.That(view.IsLoaded, Is.True);
                Assert.Throws<InvalidOperationException>(() => args.GetInt("token"), "The borrowing window ended before event activation.");
            };
            var loaded = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: args));
            Assert.That(loaded.Token, Is.EqualTo(91));
            AssertArgsReleased(args);
            Assert.That(loaded.LoadedCount, Is.EqualTo(1));
            Assert.That(loaded.ArgsCount, Is.EqualTo(1));
            Assert.That(module.GetViews<View>(), Has.Count.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => InvokeViewManager("InitView", module, loaded, prefab.name, null, new object[0]));
            Assert.That(loaded.LoadedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BothBusinessLoadHooksAndEventActivationFailureRollbackWithoutReusingDamagedInstance()
        {
            yield return StartModule();
            var model = module.LoadModel<YLCoreProbeModel>();
            for (int phase = 0; phase < 3; phase++)
            {
                var prefab = Template<YLCoreSmokeView>("YLCoreLoadFailurePrefab" + phase);
                var args = NewArgs();
                args.SetObject("model", model);
                var failure = new InvalidOperationException("load failure " + phase);
                var cleanupError = new ArgumentException("load rollback cleanup");
                YLCoreSmokeView failed = null;
                YLCoreSmokeView.LoadingProbe = view =>
                {
                    failed = Track(view);
                    view.BoundModel = model;
                    view.onViewRemove = () => Assert.Fail("Failed initialization must not publish normal removal notifications.");
                    if (phase == 0)
                    {
                        view.RemoveAction = () => throw cleanupError;
                        throw failure;
                    }
                };
                YLCoreSmokeView.ArgsProbe = (view, borrowed) => { if (phase == 1) throw failure; };
                YLCoreSmokeView.EnabledProbe = view => { if (phase == 2) throw failure; };
                Assert.That(Assert.Throws<InvalidOperationException>(() => module.LoadView(prefab, loadViewArgs: args)), Is.SameAs(failure));
                AssertArgsReleased(args);
                Assert.That(failed.Module, Is.Null);
                Assert.That(failed.Model, Is.Null);
                Assert.That(failed.IsLoaded, Is.False);
                Assert.That(failed.RemovedCount, Is.EqualTo(1));
                Assert.That(failed.UnboundCount, Is.EqualTo(1));
                Assert.That(failed.ClosedCount, Is.Zero);
                Assert.That(failed.onViewRemove, Is.Null);
                Assert.That(module.GetViews<View>(), Is.Empty);
                Assert.That(model.ExitCount, Is.Zero);
                Assert.That(module.GetModel<YLCoreProbeModel>(), Is.SameAs(model));
                Assert.That(failed.gameObject.activeSelf, Is.False);
                Assert.That(BackendInUse(prefab, failed.gameObject), Is.True, "Do not expose a pending Destroy as idle in the pool.");
                if (phase == 0) Assert.That(failure.Data["YLFramework.Core.CleanupException"], Is.SameAs(cleanupError));
                YLCoreSmokeView.LoadingProbe = null;
                YLCoreSmokeView.ArgsProbe = null;
                YLCoreSmokeView.EnabledProbe = null;
                var replacement = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                Assert.That(replacement, Is.Not.SameAs(failed), "Same-frame load cannot reuse a damaged instance.");
                Assert.That(replacement.IsLoaded, Is.True);
                yield return null;
                Assert.That(failed == null, Is.True);
                replacement.Close();
            }
        }

        [UnityTest]
        public IEnumerator FrameworkPreparationFailureReleasesArgsAndDoesNotLeaveTemporaryRegistration()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokePanel>("YLCoreInvalidCanvasPrefab", true);
            prefab.AddComponent<Canvas>();
            var args = NewArgs();
            Assert.Throws<Exception>(() => module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: args));
            AssertArgsReleased(args);
            Assert.That(module.GetViews<View>(), Is.Empty);
            var loader = typeof(XFABManager.GameObjectLoader).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var pools = (IDictionary)loader.GetType().GetField("allPools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
            var all = (IDictionary)pools[prefab.GetHashCode()].GetType().GetField("allObjs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pools[prefab.GetHashCode()]);
            Assert.That(all.Count, Is.EqualTo(1));
            foreach (var info in all.Values)
            {
                var obj = (GameObject)info.GetType().GetProperty("Obj").GetValue(info);
                var view = Track(obj.GetComponent<YLCoreSmokePanel>());
                Assert.That(view.Module, Is.Null);
                Assert.That(view.LoadedCount, Is.Zero);
                Assert.That(view.ArgsCount, Is.Zero);
                Assert.That(view.RemovedCount, Is.EqualTo(1));
                Assert.That(BackendInUse(prefab, obj), Is.True);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InactiveParentInitializesBeforeAwakeAndNewCacheSessionStartsWithoutOldBindings()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreInactiveParentPrefab");
            var parent = new GameObject("YLCoreInactiveParent");
            parent.SetActive(false);
            objects.Add(parent);
            var model = module.LoadModel<YLCoreProbeModel>();
            var args = NewArgs();
            args.SetInt("token", 51);
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, parent.transform, loadViewArgs: args));
            Assert.That(view.IsLoaded, Is.True);
            Assert.That(view.AwakeCount, Is.Zero);
            Assert.That(view.EnabledCount, Is.Zero);
            Assert.That(view.Token, Is.EqualTo(51));
            view.BoundModel = model;
            view.onViewRemove = () => { };
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.Zero);
            parent.SetActive(true);
            Assert.That(view.AwakeCount, Is.EqualTo(1));
            Assert.That(view.EnabledCount, Is.EqualTo(1));
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.EqualTo(1));
            parent.SetActive(false);
            Assert.That(view.Module, Is.SameAs(module));
            Assert.That(view.Model, Is.SameAs(model));
            view.Close();
            var session = ViewSession(view);
            Assert.That(session, Is.Null);
            var reused = Track(module.LoadView<YLCoreSmokeView>(prefab, parent.transform, loadViewArgs: null));
            Assert.That(reused, Is.SameAs(view));
            Assert.That(reused.Model, Is.Null);
            Assert.That(reused.onViewRemove, Is.Null);
            Assert.That(reused.Token, Is.Zero);
            Assert.That(reused.LoadedCount, Is.EqualTo(2));
            Assert.That(reused.AwakeCount, Is.EqualTo(1));
            Assert.That(reused.EnabledCount, Is.EqualTo(1));
            parent.SetActive(true);
            parent.SetActive(true);
            Assert.That(reused.EnabledCount, Is.EqualTo(2));
            EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(reused.EventCount, Is.EqualTo(2));
            Assert.That(model.ExitCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CachedActualInstanceIsRevalidatedWhenItsViewComponentWasRemoved()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreMutatedCachePrefab");
            var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            var instance = view.gameObject;
            view.Close();
            Object.DestroyImmediate(view);
            var args = NewArgs();
            Assert.Throws<InvalidOperationException>(() => module.LoadView(prefab, loadViewArgs: args));
            AssertArgsReleased(args);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(BackendInUse(prefab, instance), Is.True);
            Assert.That(instance.activeSelf, Is.False);
            var fresh = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            Assert.That(fresh.gameObject, Is.Not.SameAs(instance));
            yield return null;
            Assert.That(instance == null, Is.True);
        }

        [UnityTest]
        public IEnumerator OldEnableDisableHookErrorsCannotAffectReplacementSessionOrSubscriptionRound()
        {
            yield return StartModule();
            for (int phase = 0; phase < 3; phase++)
            {
                var prefab = Template<YLCoreSmokeView>("YLCoreEventReplacementPrefab" + phase);
                var view = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                var session = ViewSession(view);
                var failure = new InvalidOperationException("old event hook " + phase);
                YLCoreSmokeView replacement = null;
                bool once = true;
                view.AfterClose = () =>
                {
                    view.AfterClose = null;
                    replacement = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
                };
                Action<YLCoreSmokeView> hook = current =>
                {
                    if (!once) return;
                    once = false;
                    if (phase == 2)
                    {
                        InvokeViewInstance("StopEvents", current);
                        InvokeViewInstance("ResumeEvents", current);
                        replacement = current;
                    }
                    else current.Close();
                    throw failure;
                };
                if (phase != 1)
                {
                    InvokeViewInstance("StopEvents", view);
                    YLCoreSmokeView.EnabledProbe = hook;
                }
                else YLCoreSmokeView.DisabledProbe = hook;
                var error = Assert.Throws<InvalidOperationException>(() => InvokeViewInstance(phase == 1 ? "StopEvents" : "ResumeEvents", view));
                Assert.That(error, Is.SameAs(failure));
                Assert.That(replacement, Is.SameAs(view));
                Assert.That(ViewSession(replacement), phase == 2 ? Is.SameAs(session) : Is.Not.SameAs(session));
                Assert.That(replacement.IsLoaded, Is.True);
                Assert.That(replacement.Module, Is.SameAs(module));
                var count = replacement.EventCount;
                EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
                Assert.That(replacement.EventCount, Is.EqualTo(count + 1), "Old hook cleanup must not stop the replacement's listeners.");
                YLCoreSmokeView.EnabledProbe = null;
                YLCoreSmokeView.DisabledProbe = null;
                replacement.AfterClose = null;
                replacement.Close();
                Assert.That(Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null)), Is.SameAs(replacement), "The replacement stays healthy and reusable.");
                replacement.Close();
            }
        }

        [UnityTest]
        public IEnumerator FirstEventHookDestroyedComponentFailsLoadAndDiscardsAcquiredRoot()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreDestroyDuringEventOpenPrefab");
            var model = module.LoadModel<YLCoreProbeModel>();
            var args = NewArgs();
            YLCoreSmokeView destroyed = null;
            GameObject root = null;
            YLCoreSmokeView.LoadingProbe = view => view.BoundModel = model;
            YLCoreSmokeView.EnabledProbe = view =>
            {
                destroyed = Track(view);
                root = view.gameObject;
                Object.DestroyImmediate(view);
            };
            Assert.Throws<InvalidOperationException>(() => module.LoadView(prefab, loadViewArgs: args));
            AssertArgsReleased(args);
            Assert.That(destroyed == null, Is.True);
            Assert.That(destroyed.Module, Is.Null);
            Assert.That(destroyed.Model, Is.Null);
            Assert.That(destroyed.IsLoaded, Is.False);
            Assert.That(destroyed.RemovedCount, Is.EqualTo(1));
            Assert.That(destroyed.UnboundCount, Is.EqualTo(1));
            Assert.That(destroyed.ClosedCount, Is.Zero);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(model.Module, Is.SameAs(module));
            Assert.That(model.ExitCount, Is.Zero);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(BackendInUse(prefab, root), Is.True);
            yield return null;
            Assert.That(root == null, Is.True);
        }

        [UnityTest]
        public IEnumerator ModelRegistrationChangedDuringLoadCannotCommitTheOldBinding()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCoreModelChangesDuringLoadPrefab");
            var model = module.LoadModel<YLCoreProbeModel>();
            var args = NewArgs();
            YLCoreSmokeView failed = null;
            YLCoreSmokeView.LoadingProbe = view =>
            {
                failed = Track(view);
                view.BoundModel = model;
                module.RemoveModel(model);
                module.AddModel(model);
                Assert.That(view.Model, Is.Null);
            };
            Assert.Throws<InvalidOperationException>(() => module.LoadView(prefab, loadViewArgs: args));
            AssertArgsReleased(args);
            Assert.That(failed.Module, Is.Null);
            Assert.That(failed.Model, Is.Null);
            Assert.That(failed.IsLoaded, Is.False);
            Assert.That(failed.UnboundCount, Is.EqualTo(1));
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(module.GetModel<YLCoreProbeModel>(), Is.SameAs(model));
            Assert.That(model.ExitCount, Is.EqualTo(1), "Only the explicit business removal ends a registration.");
            yield return null;
            Assert.That(failed == null, Is.True);
        }

        [UnityTest]
        public IEnumerator ExplicitAttachInitializesInactiveInstanceOnceAndConsumesDuplicateArgs()
        {
            yield return StartModule();
            var view = Template<YLCoreSmokeView>("YLCore.SceneOwned").GetComponent<YLCoreSmokeView>();
            var args = NewArgs();
            args.SetInt("token", 23);
            Assert.That(module.AttachView(view, args), Is.SameAs(view));
            AssertArgsReleased(args);
            Assert.That(view.IsLoaded, Is.True);
            Assert.That(view.Module, Is.SameAs(module));
            Assert.That(view.Token, Is.EqualTo(23));
            Assert.That(view.AwakeCount, Is.Zero);
            Assert.That(view.EnabledCount, Is.Zero);
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(module.GetView<YLCoreSmokeView>(view.Id), Is.SameAs(view));
            Assert.That(module.GetViews(view.gameObject.name), Is.Empty, "Scene names are not fabricated resource keys.");
            args = NewArgs();
            module.AttachView(view, args);
            AssertArgsReleased(args);
            module.AttachViews(view.gameObject);
            Assert.That(view.LoadedCount, Is.EqualTo(1));
            view.gameObject.SetActive(true);
            Assert.That(view.AwakeCount, Is.EqualTo(1));
            Assert.That(view.EnabledCount, Is.EqualTo(1));
            view.Close();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(view.Module, Is.Null);
            module.AttachView(view);
            Assert.That(view.LoadedCount, Is.EqualTo(2));
            module.Close();
            Assert.That(view.RemovedCount, Is.EqualTo(2));
            Assert.That(view, Is.Not.Null, "Caller instances are never discarded or pooled by Core.");
        }

        [UnityTest]
        public IEnumerator HierarchyAttachFailureRollsBackOnlyNewSessionsWithoutHidingExistingViews()
        {
            yield return StartModule();
            var root = Template<YLCoreSmokeView>("YLCore.AttachRoot").GetComponent<YLCoreSmokeView>();
            var existing = Template<YLCoreSmokeView>("YLCore.ExistingChild").GetComponent<YLCoreSmokeView>();
            existing.transform.SetParent(root.transform);
            existing.gameObject.SetActive(true);
            module.AttachView(existing);
            var oldSession = ViewSession(existing);
            var failing = Template<YLCoreSmokeView>("YLCore.FailingChild").GetComponent<YLCoreSmokeView>();
            failing.transform.SetParent(root.transform);
            var failure = new InvalidOperationException("hierarchy attach");
            failing.LoadedAction = () => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => module.AttachViews(root.gameObject)), Is.SameAs(failure));
            Assert.That(root.Module, Is.Null);
            Assert.That(failing.Module, Is.Null);
            Assert.That(root.RemovedCount, Is.EqualTo(1));
            Assert.That(failing.RemovedCount, Is.EqualTo(1));
            Assert.That(root.ClosedCount, Is.Zero);
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(existing.gameObject.activeSelf, Is.True);
            Assert.That(ViewSession(existing), Is.SameAs(oldSession));
            Assert.That(existing.IsLoaded, Is.True);
            Assert.That(existing.RemovedCount, Is.Zero);
            Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { existing }));
            failing.LoadedAction = null;
            Assert.That(module.AttachViews(root.gameObject).Count, Is.EqualTo(3));
            Assert.That(existing.LoadedCount, Is.EqualTo(1));
            Assert.That(root.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator HierarchyOwnerConflictIsRejectedBeforeInitializingAnyNewView()
        {
            yield return StartModule();
            var other = new YLCoreConsumerModule();
            try
            {
                var request = ModuleManager.StartUpModule(other);
                while (!request.isDone) yield return null;
                Assert.That(request.error, Is.Null.Or.Empty);
                var root = Template<YLCoreSmokeView>("YLCore.ConflictRoot").GetComponent<YLCoreSmokeView>();
                var child = Template<YLCoreSmokeView>("YLCore.ForeignChild").GetComponent<YLCoreSmokeView>();
                child.transform.SetParent(root.transform);
                other.AttachView(child);
                Assert.Throws<InvalidOperationException>(() => module.AttachView(child));
                Assert.Throws<InvalidOperationException>(() => module.AttachViews(root.gameObject));
                Assert.That(root.LoadedCount, Is.Zero);
                Assert.That(root.Module, Is.Null);
                Assert.That(child.Module, Is.SameAs(other));
                Assert.That(child.RemovedCount, Is.Zero);
                Assert.That(module.GetViews<View>(), Is.Empty);
            }
            finally
            {
                if (ReferenceEquals(ModuleManager.GetModule(other.ModuleName), other)) other.Close();
                classPool.Restore(new Module[] { other });
            }
        }

        [UnityTest]
        public IEnumerator LoadedHierarchyIncludesInactiveChildrenAndOnlyRootReturnsToBackend()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCore.NestedPrefab");
            var active = Template<YLCoreSmokeView>("activeChild");
            var inactive = Template<YLCoreSmokeView>("inactiveChild");
            active.transform.SetParent(prefab.transform);
            inactive.transform.SetParent(prefab.transform);
            active.SetActive(true);
            var args = NewArgs();
            args.SetInt("token", 47);
            var root = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: args));
            var child = root.transform.Find("activeChild").GetComponent<YLCoreSmokeView>();
            var dormant = root.transform.Find("inactiveChild").GetComponent<YLCoreSmokeView>();
            Assert.That(module.GetViews<View>().Count, Is.EqualTo(3));
            Assert.That(root.Token, Is.EqualTo(47));
            Assert.That(child.Token, Is.Zero);
            Assert.That(dormant.Token, Is.Zero);
            Assert.That(dormant.IsLoaded, Is.True);
            Assert.That(dormant.AwakeCount, Is.Zero);
            Assert.That(module.GetViews(prefab.name), Is.EqualTo(new View[] { root }));
            child.Close();
            Assert.That(root.IsLoaded, Is.True);
            Assert.That(dormant.IsLoaded, Is.True);
            Assert.That(BackendInUse(prefab, root.gameObject), Is.True);
            Assert.That(child.gameObject.activeSelf, Is.False);
            module.AttachView(child);
            child.gameObject.SetActive(true);
            child.AfterClose = () => Assert.Throws<InvalidOperationException>(() => module.AttachView(child));
            module.ClearView();
            child.AfterClose = null;
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(child.RemovedCount, Is.EqualTo(2));
            Assert.That(dormant.RemovedCount, Is.EqualTo(1));
            Assert.That(child.gameObject.activeSelf, Is.True, "Batch root return preserves nested activeSelf.");
            Assert.That(dormant.gameObject.activeSelf, Is.False);
            Assert.That(BackendInUse(prefab, root.gameObject), Is.False);
            Assert.That(Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null)), Is.SameAs(root));
            Assert.That(child.LoadedCount, Is.EqualTo(3));
            Assert.That(dormant.LoadedCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator NestedInitializationOrCleanupFailureDiscardsOnlyAcquiredRootAndEndsAllSessions()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokeView>("YLCore.BadNestedPrefab");
            var childTemplate = Template<YLCoreSmokeView>("badChild");
            childTemplate.transform.SetParent(prefab.transform);
            var failure = new InvalidOperationException("nested initialization");
            YLCoreSmokeView failedRoot = null;
            YLCoreSmokeView.LoadingProbe = view =>
            {
                if (!view.transform.parent) failedRoot = Track(view);
                else throw failure;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null)), Is.SameAs(failure));
            YLCoreSmokeView.LoadingProbe = null;
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(failedRoot.Module, Is.Null);
            Assert.That(failedRoot.ClosedCount, Is.Zero);
            Assert.That(BackendInUse(prefab, failedRoot.gameObject), Is.True);
            var root = Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null));
            Assert.That(root, Is.Not.SameAs(failedRoot));
            var child = root.transform.Find("badChild").GetComponent<YLCoreSmokeView>();
            child.RemoveAction = () => throw failure;
            Assert.That(Assert.Throws<InvalidOperationException>(() => child.Close()), Is.SameAs(failure));
            Assert.That(root.IsLoaded, Is.True);
            root.Close();
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(BackendInUse(prefab, root.gameObject), Is.True);
            yield return null;
            Assert.That(root == null && failedRoot == null, Is.True);
        }

        [UnityTest]
        public IEnumerator IndependentlyLoadedNestedRootReturnsSeparatelyWithoutBecomingOuterCachedChild()
        {
            yield return StartModule();
            var outerPrefab = Template<YLCoreSmokeView>("YLCore.OuterPrefab");
            var innerPrefab = Template<YLCoreSmokeView>("YLCore.InnerPrefab");
            var outer = Track(module.LoadView<YLCoreSmokeView>(outerPrefab, loadViewArgs: null));
            var inner = Track(module.LoadView<YLCoreSmokeView>(innerPrefab, outer.transform, loadViewArgs: null));
            outer.Close();
            Assert.That(inner.Module, Is.Null);
            Assert.That(inner.RemovedCount, Is.EqualTo(1));
            Assert.That(inner.transform.parent, Is.Null);
            Assert.That(BackendInUse(innerPrefab, inner.gameObject), Is.False);
            Assert.That(BackendInUse(outerPrefab, outer.gameObject), Is.False);
            Assert.That(Track(module.LoadView<YLCoreSmokeView>(outerPrefab, loadViewArgs: null)), Is.SameAs(outer));
            Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { outer }));
            Assert.That(Track(module.LoadView<YLCoreSmokeView>(innerPrefab, loadViewArgs: null)), Is.SameAs(inner));
        }

        [UnityTest]
        public IEnumerator SameNameSceneUnloadCleansOnlyDeadSessionsIncludingNeverActivatedViews()
        {
            yield return StartModule();
            var first = OwnScene("YLCore.SameName");
            var second = OwnScene("YLCore.SecondName");
            second.name = first.name;
            Assert.That(first.handle, Is.Not.EqualTo(second.handle));
            var dead = Template<YLCoreSmokeView>("YLCore.DeadInactive").GetComponent<YLCoreSmokeView>();
            var survivor = Template<YLCoreSmokeView>("YLCore.Survivor").GetComponent<YLCoreSmokeView>();
            SceneManager.MoveGameObjectToScene(dead.gameObject, first);
            SceneManager.MoveGameObjectToScene(survivor.gameObject, second);
            module.AttachViews(first);
            module.AttachViews(second);
            Assert.That(dead.AwakeCount, Is.Zero);
            yield return SceneManager.UnloadSceneAsync(first);
            Assert.That(dead == null, Is.True);
            Assert.That(dead.Module, Is.Null, "sceneUnloaded cleans inactive wrappers without query-side pruning.");
            Assert.That(dead.RemovedCount, Is.EqualTo(1));
            Assert.That(survivor.Module, Is.SameAs(module));
            Assert.That(survivor.IsLoaded, Is.True);
            Assert.That(survivor.RemovedCount, Is.Zero);
            Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { survivor }));
            Assert.Throws<ArgumentException>(() => module.AttachViews(first));
        }

        [UnityTest]
        public IEnumerator SceneMigrationAndDdolKeepOwnerUntilActualDestructionOrModuleClose()
        {
            yield return StartModule();
            var original = OwnScene("YLCore.Original");
            var destination = OwnScene("YLCore.Destination");
            var migrated = Template<YLCoreSmokeView>("YLCore.Migrated").GetComponent<YLCoreSmokeView>();
            var persistent = Template<YLCoreSmokeView>("YLCore.Persistent").GetComponent<YLCoreSmokeView>();
            SceneManager.MoveGameObjectToScene(migrated.gameObject, original);
            SceneManager.MoveGameObjectToScene(persistent.gameObject, original);
            module.AttachViews(original);
            SceneManager.MoveGameObjectToScene(migrated.gameObject, destination);
            Object.DontDestroyOnLoad(persistent.gameObject);
            yield return SceneManager.UnloadSceneAsync(original);
            Assert.That(migrated.IsLoaded && persistent.IsLoaded, Is.True);
            Assert.That(migrated.Module, Is.SameAs(module));
            Assert.That(persistent.Module, Is.SameAs(module));
            Assert.That(migrated.RemovedCount + persistent.RemovedCount, Is.Zero);
            yield return SceneManager.UnloadSceneAsync(destination);
            Assert.That(migrated.Module, Is.Null);
            Assert.That(migrated.RemovedCount, Is.EqualTo(1));
            Assert.That(persistent.IsLoaded, Is.True);
            module.Close();
            Assert.That(persistent.Module, Is.Null);
            Assert.That(persistent.RemovedCount, Is.EqualTo(1));
            Assert.That(persistent, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ExternalDestroyEndsActiveSessionAndQueriesPruneNeverActivatedDestroyedInstance()
        {
            yield return StartModule();
            var active = Template<YLCoreSmokeView>("YLCore.DestroyActive").GetComponent<YLCoreSmokeView>();
            var dormant = Template<YLCoreSmokeView>("YLCore.DestroyInactive").GetComponent<YLCoreSmokeView>();
            module.AttachView(active);
            module.AttachView(dormant);
            active.gameObject.SetActive(true);
            int dormantId = dormant.Id;
            var model = module.LoadModel<YLCoreProbeModel>();
            active.BoundModel = model;
            Object.Destroy(active.gameObject);
            Object.Destroy(dormant.gameObject);
            yield return null;
            yield return null;
            Assert.That(active.Module, Is.Null);
            Assert.That(active.RemovedCount, Is.EqualTo(1));
            Assert.That(dormant.Module, Is.Null, "Never-activated external destruction is pruned without a business query.");
            Assert.That(module.GetView<YLCoreSmokeView>(dormantId), Is.Null);
            Assert.That(dormant.Module, Is.Null);
            Assert.That(dormant.RemovedCount, Is.EqualTo(1));
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(model.ExitCount, Is.Zero);
            Assert.That(model.Module, Is.SameAs(module));
            active.Close();
            dormant.Close();
            Assert.That(active.RemovedCount + dormant.RemovedCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator StartingAttachFailureUsesModuleRollbackWithoutClosingSuccessfulNestedOwner()
        {
            var inner = new YLCoreConsumerModule();
            var outerView = Template<YLCoreSmokeView>("YLCore.OuterSceneView").GetComponent<YLCoreSmokeView>();
            var innerView = Template<YLCoreSmokeView>("YLCore.InnerSceneView").GetComponent<YLCoreSmokeView>();
            var failure = new InvalidOperationException("starting attach");
            Module nestedOwner = null;
            module.StartAction = () =>
            {
                var innerStart = ModuleManager.StartUpModule(inner);
                Assert.That(innerStart.isDone, Is.True);
                Assert.That(innerStart.error, Is.Null.Or.Empty);
                inner.AttachView(innerView);
                nestedOwner = innerView.Module;
                module.AttachView(outerView);
                var failing = Template<YLCoreSmokeView>("YLCore.StartingFailView").GetComponent<YLCoreSmokeView>();
                failing.LoadedAction = () => throw failure;
                module.AttachView(failing);
            };
            try
            {
                var request = ModuleManager.StartUpModule(module);
                while (!request.isDone) yield return null;
                Assert.That(request.Exception, Is.SameAs(failure));
                Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
                Assert.That(module.GetViews<View>(), Is.Empty);
                Assert.That(outerView.Module, Is.Null);
                Assert.That(outerView.RemovedCount, Is.EqualTo(1));
                Assert.That(innerView.Module, Is.SameAs(nestedOwner));
                Assert.That(innerView.IsLoaded, Is.True);
                Assert.That(innerView.RemovedCount, Is.Zero);
                Assert.That(inner.State, Is.EqualTo(ModuleState.Running));
            }
            finally
            {
                if (ReferenceEquals(ModuleManager.GetModule(inner.ModuleName), inner)) inner.Close();
                classPool.Restore(new Module[] { inner });
            }
        }

        private Scene OwnScene(string name)
        {
            var scene = SceneManager.CreateScene(name);
            scenes.Add(scene);
            return scene;
        }

        private static void InvokeViewInstance(string name, View view)
        {
            try { typeof(View).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        private LoadViewArgs NewArgs()
        {
            var args = LoadViewArgs.New();
            classPool.Track(args);
            return args;
        }

        private static void AssertArgsReleased(LoadViewArgs args)
        {
            Assert.Throws<ObjectDisposedException>(() => args.GetObject("retained"));
            Assert.Throws<ObjectDisposedException>(() => args.SetInt("token", 1));
            foreach (var field in typeof(LoadViewArgs).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (field.GetValue(args) is IDictionary values) Assert.That(values.Count, Is.Zero, field.Name);
        }

        private static bool BackendInUse(GameObject prefab, GameObject instance)
        {
            var loader = typeof(XFABManager.GameObjectLoader).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var pools = (IDictionary)loader.GetType().GetField("allPools", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loader);
            var pool = pools[prefab.GetHashCode()];
            var all = (IDictionary)pool.GetType().GetField("allObjs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
            return (bool)all[instance.GetHashCode()].GetType().GetProperty("IsInUse").GetValue(all[instance.GetHashCode()]);
        }

        private IEnumerator StartModule()
        {
            var request = ModuleManager.StartUpModule(module);
            var deadline = Time.realtimeSinceStartup + 10;
            while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.isDone, Is.True);
            Assert.That(request.error, Is.Null.Or.Empty);
        }

        private static object ViewSession(View view) => typeof(View).GetField("Session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

        private static object InvokeViewManager(string methodName, params object[] arguments)
        {
            var type = typeof(View).Assembly.GetType("YLFramework.Core.ViewManager", true);
            MethodInfo method = null;
            foreach (var candidate in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (candidate.Name != methodName || candidate.IsGenericMethod || candidate.GetParameters().Length != arguments.Length) continue;
                method = candidate;
                break;
            }
            Assert.That(method, Is.Not.Null, methodName + " overload must exist.");
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        private GameObject Template<T>(string name, bool rect = false) where T : View
        {
            var prefab = rect ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            prefab.SetActive(false);
            prefab.AddComponent<T>();
            objects.Add(prefab);
            prefabKeys.Add(prefab.GetHashCode());
            return prefab;
        }

        private T Track<T>(T view) where T : View
        {
            if (view && !objects.Contains(view.gameObject))
            {
                objects.Add(view.gameObject);
                instanceKeys.Add(view.gameObject.GetHashCode());
            }
            return view;
        }

    }
}
