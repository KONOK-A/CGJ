using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
        private YLCoreDirectViewModule module;
        private CoreClassPoolScope classPool;

        [SetUp]
        public void SetUp()
        {
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
                try { classPool?.Restore(new Module[] { module }); }
                finally { ReleaseObjects(); }
            }
            yield return null;
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
                EventManager.TriggerEvent(EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
                Assert.That(view.EventCount, Is.EqualTo(before), "Framework subscriptions end before the unbind hook.");
            };
            XFEvent<View> broadcast = removed =>
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
            EventManager.AddEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast);
            try
            {
                var error = Assert.Throws<AggregateException>(() => view.Close());
                Assert.That(error.InnerExceptions, Is.EqualTo(new Exception[] { firstError, lastError }));
                Assert.That(events, Is.EqualTo(new[] { "first", "reentrant", "last", "remove", "unbind", "broadcast", "closed" }));
                Assert.That(view.RemovedCount, Is.EqualTo(1));
                Assert.That(view.UnboundCount, Is.EqualTo(1));
                Assert.That(view.ClosedCount, Is.EqualTo(1));
                Assert.That(model.Module, Is.SameAs(module));
                Assert.That(model.ExitCount, Is.Zero);
                view.Close();
                Assert.That(events, Has.Count.EqualTo(7));
            }
            finally
            {
                EventManager.RemoveEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast);
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
                XFEvent<View> broadcast = removed =>
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

            // 从未激活的对象不保证收到OnDestroy，查询必须依靠保存的注册身份收尾。
            var inactive = Template<YLCoreSmokeView>("YLCoreUnactivatedView").GetComponent<YLCoreSmokeView>();
            typeof(View).GetProperty("AssetName", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(inactive, "YLCoreUnactivatedView");
            InvokeViewManager("AddView", module, inactive);
            var session = ViewSession(inactive);
            id = inactive.Id;
            Object.Destroy(inactive.gameObject);
            yield return null;
            Assert.That(inactive == null, Is.True);
            Assert.That(ViewSession(inactive), Is.SameAs(session));
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
            EventManager.TriggerEvent(EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
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
                    orders.Add(previous, 10);
                    orders.Add(panel, 20);
                    persistentOrders.Add(panel, 20);
                    queue.Add(previous);
                    queue.Add(panel);
                    queue.Add(panel);
                    current.SetValue(manager, panel);
                    listener.SetValue(manager, phase == 1);
                    previous.Canvas.enabled = false;
                    var failure = new InvalidOperationException("global panel observer " + phase);
                    XFEvent<View> broadcast = removed =>
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
                    EventManager.AddEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast);
                    try
                    {
                        Assert.That(Assert.Throws<InvalidOperationException>(() => panel.Close()), Is.SameAs(failure));
                        Assert.That(panel.gameObject.activeSelf, Is.False);
                        Assert.That(panel.RemovedCount, Is.EqualTo(1));
                        Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.EqualTo(new[] { previous }));
                    }
                    finally { EventManager.RemoveEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, broadcast); }
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
            EventManager.TriggerEvent(EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
            Assert.That(view.EventCount, Is.EqualTo(1));
            view.gameObject.SetActive(false);
            EventManager.TriggerEvent(EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
            Assert.That(view.EventCount, Is.EqualTo(1));
            view.gameObject.SetActive(true);
            EventManager.TriggerEvent(EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
            Assert.That(view.EventCount, Is.EqualTo(2));
            view.Close();
            EventManager.TriggerEvent(EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
            Assert.That(view.EventCount, Is.EqualTo(2));
            Assert.That(Track(module.LoadView<YLCoreSmokeView>(prefab, loadViewArgs: null)), Is.SameAs(view));
            EventManager.TriggerEvent(EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
            Assert.That(view.EventCount, Is.EqualTo(3));
            Assert.That(view.EnabledCount, Is.EqualTo(3));
            Assert.That(view.DisabledCount, Is.EqualTo(2));
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
        public IEnumerator DirectPanelUsesInheritedViewPrefabContractAndInitializesCanvasOnlyOnce()
        {
            yield return StartModule();
            var prefab = Template<YLCoreSmokePanel>("YLCoreDirectPanelPrefab", true);
            var parent = new GameObject("YLCoreDirectPanelParent", typeof(RectTransform), typeof(Canvas));
            objects.Add(parent);
            var args = LoadViewArgs.New();
            classPool.Track(args);
            args.SetInt("token", 17);
            var panel = Track(module.LoadView<YLCoreSmokePanel>(prefab, parent.transform, loadViewArgs: args));
            Assert.That(panel.Canvas, Is.SameAs(panel.GetComponent<Canvas>()));
            Assert.That(panel.Canvas.overrideSorting, Is.True);
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
