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
using YLFramework.Timing;
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

    public sealed class YLCoreSelectionInputProbe : MonoBehaviour, UnityEngine.EventSystems.ICancelHandler, UnityEngine.EventSystems.IDeselectHandler, UnityEngine.EventSystems.ISelectHandler
    {
        public int Cancels;
        public Action Deselected;
        public Action Selected;
        public void OnCancel(UnityEngine.EventSystems.BaseEventData data) => Cancels++;
        public void OnDeselect(UnityEngine.EventSystems.BaseEventData data) => Deselected?.Invoke();
        public void OnSelect(UnityEngine.EventSystems.BaseEventData data) => Selected?.Invoke();
    }

    public sealed class YLCoreOtherInputGate : MonoBehaviour
    {
        public bool Allowed;
        public int Reads;
        public bool IsInputAllowed { get { Reads++; return Allowed; } }
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    public sealed class YLCoreLegacyPointerInput : UnityEngine.EventSystems.BaseInput
    {
        private Vector2 position;
        private bool pressed;
        private int downFrame = -1, upFrame = -1;
        public void Set(Vector2 position, bool pressed)
        {
            this.position = position;
            if (pressed != this.pressed)
            {
                if (pressed) downFrame = Time.frameCount + 1;
                else upFrame = Time.frameCount + 1;
            }
            this.pressed = pressed;
        }
        public override bool mousePresent => true;
        public override Vector2 mousePosition => position;
        public override bool touchSupported => false;
        public override bool GetMouseButton(int button) => button == 0 && pressed;
        public override bool GetMouseButtonDown(int button) => button == 0 && Time.frameCount == downFrame;
        public override bool GetMouseButtonUp(int button) => button == 0 && Time.frameCount == upFrame;
        public override float GetAxisRaw(string axisName) => 0;
        public override bool GetButtonDown(string buttonName) => false;
    }
#endif

    public class CoreDirectViewPlayModeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<int> prefabKeys = new List<int>();
        private readonly List<int> instanceKeys = new List<int>();
        private readonly List<Scene> scenes = new List<Scene>();
        private YLCoreDirectViewModule module;
        private CoreClassPoolScope classPool;
        private CoreUpdateHostScope updateHosts;
        private readonly HashSet<GameObject> priorEventSystems = new HashSet<GameObject>();

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
            updateHosts = new CoreUpdateHostScope();
            priorEventSystems.Clear();
            foreach (var system in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                priorEventSystems.Add(system.gameObject);
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
                finally
                {
                    ReleaseObjects();
                    updateHosts?.ReleaseOwnedIdleHosts();
                }
            }
            yield return null;
            foreach (var scene in scenes)
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            scenes.Clear();
            Assert.That(ModuleManager.GetModule<YLCoreDirectViewModule>(), Is.Null);
        }

        private void ReleaseObjects()
        {
            var policy = typeof(PanelManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var inputHost = policy == null ? null : (GameObject)typeof(PanelManager).GetField("_eventSystem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(policy);
            if (inputHost && !priorEventSystems.Contains(inputHost)) objects.Add(inputHost);
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
            var firstBroadcastToken = EventManager.AddEvent<View>(CoreEvents.ON_REMOVE_VIEW, removed =>
            {
                if (!ReferenceEquals(removed, view)) return;
                events.Add("broadcast.throw");
                throw broadcastError;
            });
            var broadcastToken = EventManager.AddEvent(CoreEvents.ON_REMOVE_VIEW, broadcast);
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
                EventManager.AddEvent(CoreEvents.ON_REMOVE_VIEW, broadcast);
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
                    EventManager.RemoveEvent(CoreEvents.ON_REMOVE_VIEW, broadcast);
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
            var states = (IDictionary)type.GetField("states", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            List<Panel> Queue()
            {
                var panels = new List<Panel>();
                PanelManager.GetAllPanels(UIType.UI, panels);
                panels.RemoveAll(item => !item.IsLoaded || !item.gameObject.activeInHierarchy || item.PanelType != PanelType.Panel);
                return panels;
            }
            try
            {
                YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
                var root = PolicyRoot("YLCore.CleanupRoot");
                for (int phase = 0; phase < 2; phase++)
                {
                    var prefab = Template<YLCoreSmokePanel>("YLCorePanelCleanupPrefab" + phase, true);
                    var previous = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                    var panel = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                    Assert.That(states.Contains(previous), Is.True);
                    Assert.That(states.Contains(panel), Is.True);
                    previous.SortingOrder = 10;
                    panel.SortingOrder = 20;
                    PanelManager.MultiPanelListener = phase == 1;
                    var failure = new InvalidOperationException("global panel observer " + phase);
                    var followingCalls = 0;
                    Action<View> broadcast = removed =>
                    {
                        if (!ReferenceEquals(removed, panel)) return;
                        Assert.That(states.Contains(panel), Is.False);
                        Assert.That(Queue(), Is.EqualTo(new Panel[] { previous }));
                        Assert.That(PanelManager.GetTopPanel(previous.RootCanvas), Is.SameAs(previous));
                        Assert.That(previous.Canvas.enabled, Is.True);
                        Assert.That(panel.Module, Is.Null);
                        throw failure;
                    };
                    var broadcastToken = EventManager.AddEvent(CoreEvents.ON_REMOVE_VIEW, broadcast);
                    var followingToken = EventManager.AddEvent<View>(CoreEvents.ON_REMOVE_VIEW, removed =>
                    {
                        if (!ReferenceEquals(removed, panel)) return;
                        followingCalls++;
                        Assert.That(panel.IsLoaded, Is.False);
                        Assert.That(panel.Module, Is.Null);
                        Assert.That(panel.gameObject.activeSelf, Is.False);
                        Assert.That(states.Contains(panel), Is.False);
                        Assert.That(Queue(), Is.EqualTo(new Panel[] { previous }));
                        Assert.That(PanelManager.GetTopPanel(previous.RootCanvas), Is.SameAs(previous));
                        Assert.That(previous.Canvas.enabled, Is.True);
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
                    Assert.That(Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null)), Is.SameAs(panel));
                    panel.Close();
                    previous.Close();
                    Assert.That(states, Is.Empty);
                    Assert.That(Queue(), Is.Empty);
                    Assert.That(PanelManager.GetTopPanel(root.GetComponent<Canvas>()), Is.Null);
                }
            }
            finally
            {
                YLCoreSmokePanel.ResetProbes();
                try
                {
                    typeof(PanelManager).GetMethod("EndLateRound", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                    typeof(PanelManager).GetMethod("ReleaseOwnedInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                }
                finally { instanceField.SetValue(null, previousManager); }
            }
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
        public IEnumerator PanelSwitchDoesNotDuplicateTokenAndLastRootEndsInputLifetimeSubscription()
        {
            yield return StartModule();
            YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
            var stopper = YLFramework.Foundation.UpdateManager.AddLateUpdateEvent(() => PanelManager.MultiPanelListener = false);
            try
            {
                var prefab = Template<YLCoreSmokePanel>("YLCorePanelUpdateRound", true);
                var root = PolicyRoot("YLCore.LateRoundRoot");
                var previous = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                var current = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                current.enabled = false;
                current.gameObject.SetActive(false);
                var host = UnityEngine.Object.FindFirstObjectByType<YLFramework.Foundation.UpdateManager>();
                host.SendMessage("LateUpdate");
                Assert.That(previous.Canvas.enabled, Is.True, "关闭遮挡策略应立即恢复显示。");
                stopper.UnRegister();
                PanelManager.MultiPanelListener = true;
                PanelManager.MultiPanelListener = true;
                host.SendMessage("LateUpdate");
                Assert.That(previous.Canvas.enabled, Is.True);
                var channel = (EventChannel)typeof(YLFramework.Foundation.UpdateManager)
                    .GetField("_lateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
                Assert.That(channel.ListenerCount, Is.EqualTo(1), "同值启用不能重复建立连接。");
                current.Close(); previous.Close();
                Assert.That(channel.ListenerCount, Is.EqualTo(1), "窗口队列归零后仍服务有效输入根，不销毁其他 Module 可继续使用的输入。");
                Object.Destroy(root.gameObject);
                yield return null;
                yield return null;
                Assert.That(channel.ListenerCount, Is.Zero, "最后的 UI 根退出后释放自身输入寿命订阅，保留共享宿主。");
                Assert.That(host != null, Is.True);
            }
            finally
            {
                stopper.UnRegister();
                PanelManager.MultiPanelListener = true;
                YLCoreSmokePanel.ResetProbes();
            }
        }

        [UnityTest]
        public IEnumerator ViewScannerResetPreservesDomainOwnershipAndRejectsOldSnapshot()
        {
            yield return StartModule();
            var template = Template<YLCoreSmokeView>("YLCoreViewUpdateRound");
            YLCoreSmokeView first = null;
            YLCoreSmokeView replacement = null;
            object firstSession = null;
            IUnRegister resetter = null;
            resetter = YLFramework.Foundation.UpdateManager.AddUpdateEvent(() =>
            {
                resetter.UnRegister();
                InvokeViewManager("ResetSession");
                Assert.That(first.Module, Is.SameAs(module));
                Assert.That(ViewSession(first), Is.SameAs(firstSession), "执行会话重置不调用业务 Close 或清 Module 索引。");
                var obj = new GameObject("YLCoreNewInactiveViewRound");
                objects.Add(obj);
                obj.SetActive(false);
                replacement = obj.AddComponent<YLCoreSmokeView>();
                module.AttachView(replacement);
                UnityEngine.Object.DestroyImmediate(obj);
            });
            try
            {
                first = Track(module.LoadView<YLCoreSmokeView>(template, loadViewArgs: null));
                firstSession = ViewSession(first);
                var host = UnityEngine.Object.FindFirstObjectByType<YLFramework.Foundation.UpdateManager>();
                host.SendMessage("Update");
                Assert.That(ViewSession(replacement), Is.Not.Null, "旧扫描快照不能扫描新订阅轮次的索引。");
                yield return null;
                yield return null;
                Assert.That(ViewSession(replacement), Is.Null, "新轮次继续保留从未激活对象的必要扫描。");
                Assert.That(first.Module, Is.SameAs(module));
                first.Close();
            }
            finally { resetter.UnRegister(); }
        }

        [UnityTest]
        public IEnumerator DefaultPanelListenerRestoresPreviousPanelWhenDisabledComponentMissesDeactivation()
        {
            yield return StartModule();
            Assert.That(PanelManager.MultiPanelListener, Is.True);
            YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
            try
            {
                var prefab = Template<YLCoreSmokePanel>("YLCorePanelLateFallback", true);
                var root = PolicyRoot("YLCore.LateFallbackRoot");
                var previous = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                var current = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                Assert.That(previous.Canvas.enabled, Is.False);
                // 先停用组件时对象仍活跃，直接 Deactivate 不应释放队列。
                // 随后停用对象不会再收到第二次 OnDisable，需原有 Late 兜底。
                current.enabled = false;
                current.gameObject.SetActive(false);
                Assert.That(previous.Canvas.enabled, Is.False);
                yield return null;
                yield return null;
                Assert.That(previous.Canvas.enabled, Is.True);
                Assert.That(PanelManager.GetTopPanel(previous.RootCanvas), Is.SameAs(previous));
                current.Close(); previous.Close();
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator InactivePanelAttachmentKeepsVisiblePanelUntilActivationAndRetainsOrderOnDisable()
        {
            yield return StartModule();
            YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
            try
            {
                var prefab = Template<YLCoreSmokePanel>("YLCoreInactivePanelPrefab", true);
                var root = PolicyRoot("YLCore.InactiveRoot");
                var visible = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                var parent = new GameObject("YLCoreInactivePanelParent");
                objects.Add(parent);
                parent.transform.SetParent(root, false);
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
                var orders = (IDictionary)typeof(PanelManager).GetField("states", BindingFlags.Instance | BindingFlags.NonPublic)
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
                var root = PolicyRoot("YLCore.SharedEventRoot");
                var first = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                YLCoreSmokePanel.InvisibleProbe = panel =>
                {
                    Assert.That(panel, Is.SameAs(first));
                    var activating = module.GetViews<YLCoreSmokePanel>().Find(item => item != first);
                    Assert.That(activating, Is.Not.Null);
                    activating.gameObject.SetActive(false);
                };
                var second = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
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
            module.UIStartSortOrder = short.MaxValue;
            var args = NewArgs();
            Assert.Throws<ArgumentOutOfRangeException>(() => module.LoadView<YLCoreSmokePanel>(prefab, loadViewArgs: args));
            module.UIStartSortOrder = 0;
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

        [UnityTest]
        public IEnumerator ViewDisableKeepsExplicitTimerScopeAndCloseOnlyDisposesItsOwnSession()
        {
            var driverType = typeof(YLFramework.Timing.Timer).Assembly.GetType("YLFramework.Timing.Internal.TimerDriver", true);
            Assert.That(Object.FindObjectsByType(driverType, FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty,
                "不接管已有 TimerDriver。");
            yield return StartModule();
            using var scope = new TimerScope();
            Component ownedHost = null;
            TimerHandle global = default;
            try
            {
                var view = Template<YLCoreSmokeView>("YLTimer.ViewSession").GetComponent<YLCoreSmokeView>();
                module.AttachView(view);
                view.gameObject.SetActive(true);
                var cancelled = 0;
                view.RemoveAction = scope.Dispose;
                var scoped = scope.PostNamed("view", 60, onCancelled: _ => cancelled++);
                var completed = 0;
                global = YLFramework.Timing.Timer.DelayFrames(4, () => completed++);
                ownedHost = (Component)Object.FindObjectsByType(driverType, FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
                view.gameObject.SetActive(false);
                yield return null;
                Assert.That(view.IsLoaded, Is.True, "临时停用不是表现会话结束。");
                Assert.That(scoped.IsActive, Is.True);
                Assert.That(scope.IsDisposed, Is.False);
                Assert.That(cancelled, Is.Zero);
                view.gameObject.SetActive(true);
                view.Close();
                Assert.That(scoped.Status, Is.EqualTo(TimerState.Cancelled));
                Assert.That(scope.IsDisposed, Is.True);
                Assert.That(cancelled, Is.EqualTo(1));
                Assert.That(global.IsActive, Is.True, "View.Close 不清外部全局 Timer。");
                using var nextScope = new TimerScope();
                module.AttachView(view);
                view.gameObject.SetActive(true);
                view.RemoveAction = nextScope.Dispose;
                var next = nextScope.PostNamed("view", 60);
                Assert.That(next.IsActive, Is.True);
                module.Close();
                Assert.That(next.Status, Is.EqualTo(TimerState.Cancelled));
                Assert.That(cancelled, Is.EqualTo(1), "旧 Scope 不参与重启后的 View 会话。");
                for (var frame = 0; frame < 8 && completed == 0; frame++) yield return null;
                Assert.That(global.Status, Is.EqualTo(TimerState.Completed));
                Assert.That(completed, Is.EqualTo(1));
            }
            finally
            {
                global.Cancel();
                if (ownedHost) Object.Destroy(ownedHost.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PanelPolicyUsesEffectiveModuleOrderAndStableFrontPriority()
        {
            yield return StartModule();
            var other = new YLCoreConsumerModule();
            try
            {
                var request = ModuleManager.StartUpModule(other);
                while (!request.isDone) yield return null;
                Assert.That(request.error, Is.Null.Or.Empty);
                other.UIStartSortOrder = 200;
                var root = new GameObject("YLPolicy.SharedRoot", typeof(RectTransform), typeof(Canvas));
                objects.Add(root);
                var prefab = Template<YLCoreSmokePanel>("YLPolicy.Order", true);
                var high = Track(other.LoadView<YLCoreSmokePanel>(prefab, root.transform, loadViewArgs: null));
                var low = Track(module.LoadView<YLCoreSmokePanel>(prefab, root.transform, loadViewArgs: null));
                high.SortingOrder = 10;
                low.SortingOrder = 50;
                Assert.That(high.Canvas.sortingOrder, Is.GreaterThan(low.Canvas.sortingOrder));
                Assert.That(high.IsTop(), Is.True);
                Assert.That(low.IsTop(), Is.False);
                Assert.That(PanelManager.GetTopPanel(UIType.UI), Is.SameAs(high));
                high.TypeOverride = low.TypeOverride = PanelType.Panel;
                high.RefreshUIState();
                Assert.That(high.IsVisible, Is.True);
                Assert.That(low.IsVisible, Is.False, "A later lower effective order cannot hide the higher Module's page.");
                high.KeepInFront = true;
                var third = Track(module.LoadView<YLCoreSmokePanel>(prefab, root.transform, loadViewArgs: null));
                var stable = high.Canvas.sortingOrder;
                third.TypeOverride = PanelType.Panel;
                third.Close();
                int hiddenAtActivation = 0;
                YLCoreSmokePanel.InvisibleProbe = _ => hiddenAtActivation++;
                third = Track(module.LoadView<YLCoreSmokePanel>(prefab, root.transform, loadViewArgs: null));
                Assert.That(hiddenAtActivation, Is.EqualTo(1), "An initially occluded page must receive its Invisible transition.");
                YLCoreSmokePanel.InvisibleProbe = null;
                Assert.That(high.Canvas.sortingOrder, Is.EqualTo(stable), "AlwaysInFront must not drift with each registration.");
                low.TypeOverride = PanelType.Window;
                low.Modal = true;
                low.RefreshUIState();
                Assert.That(low.Canvas.sortingOrder, Is.GreaterThan(high.Canvas.sortingOrder), "Module offsets and AlwaysInFront cannot cross the modal priority band.");
                Assert.That(high.IsInputBlocked, Is.True);
                Assert.That(low.HasFocus, Is.True);
            }
            finally
            {
                YLCoreSmokePanel.ResetProbes();
                if (ReferenceEquals(ModuleManager.GetModule(other.ModuleName), other)) other.Close();
                classPool.Restore(new Module[] { other });
            }
        }

        [UnityTest]
        public IEnumerator PanelPolicyIsolatesIndependentRootsAndControlsNestedWindowCanvas()
        {
            yield return StartModule();
            YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
            try
            {
                var rootA = new GameObject("YLPolicy.RootA", typeof(RectTransform), typeof(Canvas));
                var rootB = new GameObject("YLPolicy.RootB", typeof(RectTransform), typeof(Canvas));
                objects.Add(rootA);
                objects.Add(rootB);
                var container = new GameObject("YLPolicy.Container", typeof(RectTransform));
                objects.Add(container);
                container.transform.SetParent(rootB.transform, false);
                var prefab = Template<YLCoreSmokePanel>("YLPolicy.RootWindow", true);
                var a = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootA.transform, loadViewArgs: null));
                var b = Track(module.LoadView<YLCoreSmokePanel>(prefab, container.transform, loadViewArgs: null));
                Assert.That(a.Canvas.enabled, Is.True, "A different effective root cannot occlude this Panel.");
                Assert.That(b.Canvas, Is.SameAs(b.GetComponent<Canvas>()), "A nested root container is not another Panel's Canvas.");
                Assert.That(rootA.GetComponent<Canvas>().enabled, Is.True);
                Assert.That(rootB.GetComponent<Canvas>().enabled, Is.True);
                Assert.That(a.IsTop(), Is.True);
                Assert.That(b.IsTop(), Is.True);
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator PanelPolicyAcceptsPresetCanvasAndRejectsOverflowBeforeMutation()
        {
            yield return StartModule();
            var root = new GameObject("YLPolicy.PresetRoot", typeof(RectTransform), typeof(Canvas));
            objects.Add(root);
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.Preset", true);
            prefab.AddComponent<Canvas>();
            var panel = Track(module.LoadView<YLCoreSmokePanel>(prefab, root.transform, loadViewArgs: null));
            var previous = panel.SortingOrder;
            var display = panel.Canvas.sortingOrder;
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.SortingOrder = int.MaxValue);
            Assert.That(panel.SortingOrder, Is.EqualTo(previous));
            Assert.That(panel.Canvas.sortingOrder, Is.EqualTo(display));
            Assert.Throws<ArgumentOutOfRangeException>(() => module.UIStartSortOrder = int.MaxValue);
            Assert.That(module.UIStartSortOrder, Is.Zero);
            Assert.That(panel.Canvas.sortingOrder, Is.EqualTo(display));
            var sibling = Track(module.LoadView<YLCoreSmokePanel>(prefab, root.transform, loadViewArgs: null));
            sibling.SortingOrder = previous;
            Assert.That(sibling.Canvas.sortingOrder, Is.GreaterThan(panel.Canvas.sortingOrder));
            Assert.That(sibling.IsTop(), Is.True, "Equal requested orders use stable registration order, not Dictionary enumeration.");
            Assert.Throws<ArgumentOutOfRangeException>(() => module.UIStartSortOrder = short.MaxValue);
            sibling.SortingOrder = short.MaxValue;
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.SortingOrder = short.MaxValue);
            Assert.That(panel.SortingOrder, Is.EqualTo(previous), "A rejected priority collision cannot partially change the order.");
            panel.Close();
            Assert.DoesNotThrow(() => panel.SortingOrder = int.MaxValue, "A retired session must not operate on a cached Canvas or dereference its old owner.");
            Assert.That(panel.SortingOrder, Is.EqualTo(previous));
            Assert.That(sibling.Canvas.sortingOrder, Is.EqualTo(32767));
        }

        [UnityTest]
        public IEnumerator PanelPolicyModalStackRestoresOnlyEffectiveFocusAndIgnoresOverlay()
        {
            yield return StartModule();
            var root = PolicyRoot("YLPolicy.ModalRoot");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.Modal", true);
            var content = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            var first = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            first.Modal = true;
            first.RefreshUIState();
            var second = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            second.Modal = true;
            second.RefreshUIState();
            var overlay = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            overlay.TypeOverride = PanelType.Overlay;
            overlay.RefreshUIState();
            Assert.That(content.IsVisible, Is.True, "Modal does not imply full-screen rendering occlusion.");
            Assert.That(content.IsInputBlocked, Is.True);
            Assert.That(first.IsInputBlocked, Is.True);
            Assert.That(second.HasFocus, Is.True);
            Assert.That(PanelManager.GetTopPanel(root.GetComponent<Canvas>()), Is.SameAs(second));
            second.gameObject.SetActive(false);
            Assert.That(first.HasFocus, Is.True);
            Assert.That(PanelManager.GetModalPanel(content.RootCanvas), Is.SameAs(first));
            first.Close();
            Assert.That(content.HasFocus, Is.True);
            Assert.That(content.IsInputBlocked, Is.False);
            second.gameObject.SetActive(true);
            Assert.That(second.HasFocus, Is.True);
            second.Close();
            Assert.That(content.HasFocus, Is.True);
            Assert.That(overlay.HasFocus, Is.False);
            PanelManager.MultiPanelListener = false;
            try
            {
                first = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                first.Modal = true;
                first.RefreshUIState();
                Assert.That(content.IsInputBlocked, Is.True);
                first.enabled = false;
                first.gameObject.SetActive(false);
                yield return null;
                yield return null;
                Assert.That(content.HasFocus, Is.True, "Occlusion switch cannot disable modal/focus lifecycle maintenance.");
                Assert.That(content.IsInputBlocked, Is.False);
            }
            finally { PanelManager.MultiPanelListener = true; }
        }

        [UnityTest]
        public IEnumerator PanelPolicyBorrowedCanvasInheritsOwnerWithoutHidingSharedRoot()
        {
            yield return StartModule();
            var root = PolicyRoot("YLPolicy.BorrowRoot");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.BorrowOwner", true);
            var owner = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            owner.TypeOverride = PanelType.Panel;
            owner.RefreshUIState();
            var childObject = new GameObject("YLPolicy.BorrowedChild", typeof(RectTransform));
            objects.Add(childObject);
            childObject.transform.SetParent(owner.transform, false);
            var child = module.AttachView(childObject.AddComponent<YLCoreSmokePanel>());
            Assert.That(child.Canvas, Is.SameAs(owner.Canvas));
            var before = owner.Canvas.sortingOrder;
            child.SortingOrder = int.MaxValue;
            Assert.That(owner.Canvas.sortingOrder, Is.EqualTo(before));
            var covering = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            covering.TypeOverride = PanelType.Panel;
            covering.RefreshUIState();
            Assert.That(owner.IsVisible, Is.False);
            Assert.That(child.IsVisible, Is.False, "Borrowed child must reflect the committed owner visibility.");
            Assert.That(child.IsInputBlocked, Is.True);
            child.Close();
            Assert.That(root.GetComponent<Canvas>().enabled, Is.True);
            Assert.That(covering.Canvas.enabled, Is.True);
            covering.Close();
            Assert.That(owner.Canvas.enabled, Is.True);
            Assert.That(owner.IsLoaded, Is.True);
        }

        [UnityTest]
        public IEnumerator PanelPolicyDefaultRootsAreSceneScopedAndPersistentRootIsIndependent()
        {
            yield return StartModule();
            var previousScene = SceneManager.GetActiveScene();
            var previousHook = PanelManager.onCreateCanvas;
            var sceneA = OwnScene("YLPolicy.SceneA");
            var sceneB = OwnScene("YLPolicy.SceneB");
            var system = new GameObject("YLPolicy.OwnEventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            objects.Add(system);
            var getParent = typeof(PanelManager).GetMethod("GetParent", BindingFlags.Static | BindingFlags.NonPublic);
            PanelManager.onCreateCanvas = canvas => objects.Add(canvas.gameObject);
            try
            {
                SceneManager.SetActiveScene(sceneA);
                var rootA = (Transform)getParent.Invoke(null, new object[] { module, null, UIType.UI });
                SceneManager.SetActiveScene(sceneB);
                var rootB = (Transform)getParent.Invoke(null, new object[] { module, null, UIType.UI });
                var persistent = (Transform)getParent.Invoke(null, new object[] { module, null, UIType.DontDestroyUI });
                Assert.That(rootA, Is.Not.SameAs(rootB));
                Assert.That(rootA.gameObject.scene, Is.EqualTo(sceneA));
                Assert.That(rootB.gameObject.scene, Is.EqualTo(sceneB));
                SceneManager.SetActiveScene(sceneA);
                Assert.That(getParent.Invoke(null, new object[] { module, null, UIType.UI }), Is.SameAs(rootA));
                var prefab = Template<YLCoreSmokePanel>("YLPolicy.ScenePanel", true);
                var a = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootA, loadViewArgs: null));
                var b = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootB, loadViewArgs: null));
                var d = Track(module.LoadView<YLCoreSmokePanel>(prefab, persistent, loadViewArgs: null));
                a.TypeOverride = b.TypeOverride = d.TypeOverride = PanelType.Panel;
                a.RefreshUIState();
                d.SortingOrder = 100;
                Assert.That(d.UIType, Is.EqualTo(UIType.DontDestroyUI));
                Assert.That(a.Canvas.enabled && b.Canvas.enabled && d.Canvas.enabled, Is.True);
                Assert.That(a.IsTop() && b.IsTop() && d.IsTop(), Is.True);
                SceneManager.SetActiveScene(previousScene);
                yield return SceneManager.UnloadSceneAsync(sceneA);
                Assert.That(a == null, Is.True);
                Assert.That(a.IsTop(), Is.False, "Unity fake-null cannot compare equal to an empty Top query.");
                Assert.That(a.HasFocus, Is.False);
                Assert.That(b.IsLoaded && b.IsVisible && d.IsLoaded && d.IsVisible, Is.True);
                Assert.That(b.Module, Is.SameAs(module));
                Assert.That(d.Module, Is.SameAs(module));
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                PanelManager.onCreateCanvas = previousHook;
            }
        }

        [UnityTest]
        public IEnumerator PanelPolicyReparentingOutOfDestroyedRootRetainsTheSameViewSession()
        {
            yield return StartModule();
            var rootA = PolicyRoot("YLPolicy.MovingRootA");
            var rootB = PolicyRoot("YLPolicy.MovingRootB");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.Moving", true);
            var moving = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootA, loadViewArgs: null));
            var other = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootB, loadViewArgs: null));
            moving.TypeOverride = other.TypeOverride = PanelType.Panel;
            moving.SortingOrder = 50;
            var session = ViewSession(moving);
            moving.transform.SetParent(rootB, false);
            Object.DestroyImmediate(rootA.gameObject);
            moving.RefreshUIState();
            Assert.That(moving.IsVisible, Is.True);
            Assert.That(other.IsVisible, Is.False);
            Assert.That(moving.RootCanvas, Is.SameAs(rootB.GetComponent<Canvas>()));
            Assert.That(ViewSession(moving), Is.SameAs(session));
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.EquivalentTo(new[] { moving, other }));
            Assert.That(moving.IsTop(), Is.True);
        }

        [UnityTest]
        public IEnumerator PanelPolicySortingFailureInsideOwnInitializationStillRollsBack()
        {
            yield return StartModule();
            var root = PolicyRoot("YLPolicy.InitializationRoot");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.Initialization", true);
            var failure = new InvalidOperationException("YLPolicy.InitializationSortingFailure");
            YLCoreSmokePanel prepared = null;
            var args = NewArgs();
            args.SetObject("retained", module);
            YLCoreSmokePanel.SortingProbe = (_, order) => { if (order == 100) throw failure; };
            YLCoreSmokePanel.LoadingProbe = panel => { prepared = Track(panel); panel.SortingOrder = 100; };
            try
            {
                Assert.That(Assert.Throws<InvalidOperationException>(() => module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: args)), Is.SameAs(failure));
                Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.Empty);
                Assert.That(prepared.IsLoaded, Is.False);
                Assert.That(prepared.RemovedCount, Is.EqualTo(1));
                AssertArgsReleased(args);
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator PanelPolicyNestedInitializationDoesNotTreatOuterSortingObserverAsChildFailure()
        {
            yield return StartModule();
            var root = PolicyRoot("YLPolicy.NestedInitRoot");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.NestedInit", true);
            YLCoreSmokePanel outer = null;
            YLCoreSmokePanel child = null;
            YLCoreSmokePanel.SortingProbe = (panel, order) =>
            {
                if (panel == outer && order >= 100) throw new InvalidOperationException("YLPolicy.OuterObserver");
            };
            YLCoreSmokePanel.LoadingProbe = panel =>
            {
                if (outer == null)
                {
                    outer = Track(panel);
                    outer.KeepInFront = true;
                    LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: YLPolicy.OuterObserver"));
                    child = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                }
                else panel.SortingOrder = 100;
            };
            try
            {
                var loaded = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                Assert.That(loaded, Is.SameAs(outer));
                Assert.That(outer.IsLoaded && child.IsLoaded, Is.True);
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator PanelPolicyMergedRootsRejectModuleOffsetAndOrderWithoutPartialCommit()
        {
            yield return StartModule();
            var rootA = PolicyRoot("YLPolicy.AtomicRootA");
            var rootB = PolicyRoot("YLPolicy.AtomicRootB");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.Atomic", true);
            var a = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootA, loadViewArgs: null));
            var b = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootB, loadViewArgs: null));
            a.SortingOrder = b.SortingOrder = 32766;
            b.transform.SetParent(rootA, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => module.UIStartSortOrder = 1);
            Assert.That(module.UIStartSortOrder, Is.Zero);
            Assert.That(a.Canvas.sortingOrder, Is.EqualTo(32766));
            Assert.That(b.SortingOrder, Is.EqualTo(32766));
            b.SortingOrder = 32767;
            Assert.Throws<ArgumentOutOfRangeException>(() => a.SortingOrder = 32767);
            Assert.That(a.SortingOrder, Is.EqualTo(32766));
            a.RefreshUIState();
            Assert.That(b.Canvas.sortingOrder, Is.EqualTo(32767));
        }

        [UnityTest]
        public IEnumerator PanelPolicyLiveBorrowedReparentInheritsNewOwnerCanvasAndModalState()
        {
            yield return StartModule();
            var rootA = PolicyRoot("YLPolicy.BorrowMovingA");
            var rootB = PolicyRoot("YLPolicy.BorrowMovingB");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.BorrowMoving", true);
            var a = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootA, loadViewArgs: null));
            var b = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootB, loadViewArgs: null));
            var obj = new GameObject("YLPolicy.LiveBorrowed", typeof(RectTransform));
            objects.Add(obj);
            obj.transform.SetParent(a.transform, false);
            var child = module.AttachView(obj.AddComponent<YLCoreSmokePanel>());
            var modal = Track(module.LoadView<YLCoreSmokePanel>(prefab, rootB, loadViewArgs: null));
            modal.Modal = true;
            modal.RefreshUIState();
            obj.transform.SetParent(b.transform, false);
            yield return null;
            yield return null;
            Assert.That(child.Canvas, Is.SameAs(b.Canvas));
            Assert.That(child.RootCanvas, Is.SameAs(b.RootCanvas));
            Assert.That(child.IsVisible, Is.True);
            Assert.That(child.IsInputBlocked, Is.True);
            Assert.That(a.Canvas.enabled && b.Canvas.enabled, Is.True);
            modal.Close();
            Assert.That(child.IsInputBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator PanelPolicyHighModalOrderDoesNotExhaustNormalWindowGroup()
        {
            yield return StartModule();
            var root = PolicyRoot("YLPolicy.PriorityGroupRoot");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.PriorityGroup", true);
            var content = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            var modal = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            modal.Modal = true;
            modal.SortingOrder = 32767;
            var next = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            Assert.That(next.SortingOrder, Is.EqualTo(20));
            Assert.That(modal.Canvas.sortingOrder, Is.EqualTo(32767));
            Assert.That(next.IsInputBlocked && content.IsInputBlocked, Is.True);
        }

        [UnityTest]
        public IEnumerator PanelPolicySortingLayerWinsOverOrderAndAllocatesOnlyWithinItsGroup()
        {
            yield return StartModule();
            var root = PolicyRoot("YLPolicy.SortingLayerRoot");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.DefaultLayer", true);
            var low = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            var layeredPrefab = Template<YLCoreSmokePanel>("YLPolicy.UpperLayer", true);
            var layer = SortingLayer.NameToID("Map");
            Assert.That(SortingLayer.GetLayerValueFromID(layer), Is.GreaterThan(SortingLayer.GetLayerValueFromID(0)),
                "使用工程已有 SortingLayer，不修改 TagManager。");
            layeredPrefab.AddComponent<Canvas>().sortingLayerID = layer;
            var high = Track(module.LoadView<YLCoreSmokePanel>(layeredPrefab, root, loadViewArgs: null));
            high.SortingOrder = -300;
            low.SortingOrder = 500;
            Assert.That(high.IsTop(), Is.True);
            low.TypeOverride = high.TypeOverride = PanelType.Panel;
            high.RefreshUIState();
            Assert.That(low.IsVisible, Is.False);
            Assert.That(high.IsVisible, Is.True);
            high.SortingOrder = 32767;
            var next = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            Assert.That(next.SortingOrder, Is.EqualTo(510), "Other SortingLayer maxima cannot exhaust this group's default order.");
            Assert.That(PanelManager.GetTopPanel(low.RootCanvas), Is.SameAs(high));
        }

        [UnityTest]
        public IEnumerator PanelPolicyExplicitNestedRootsRemainIndependentWithinOneOuterCanvas()
        {
            yield return StartModule();
            var assetCanvas = Resources.Load<GameObject>("YLFramework/Core/Canvas").GetComponent<Canvas>();
            Assert.Throws<ArgumentException>(() => PanelManager.RegisterRoot(assetCanvas), "A root asset is not a loaded Scene context.");
            var outer = PolicyRoot("YLPolicy.OuterRoot");
            var firstRoot = PolicyRoot("YLPolicy.ExplicitFirst");
            var secondRoot = PolicyRoot("YLPolicy.ExplicitSecond");
            firstRoot.SetParent(outer, false);
            secondRoot.SetParent(outer, false);
            PanelManager.RegisterRoot(firstRoot.GetComponent<Canvas>());
            PanelManager.RegisterRoot(secondRoot.GetComponent<Canvas>());
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.ExplicitRootPanel", true);
            var a = Track(module.LoadView<YLCoreSmokePanel>(prefab, firstRoot, loadViewArgs: null));
            var b = Track(module.LoadView<YLCoreSmokePanel>(prefab, secondRoot, loadViewArgs: null));
            a.TypeOverride = b.TypeOverride = PanelType.Panel;
            a.RefreshUIState();
            Assert.That(a.RootCanvas, Is.SameAs(firstRoot.GetComponent<Canvas>()));
            Assert.That(b.RootCanvas, Is.SameAs(secondRoot.GetComponent<Canvas>()));
            Assert.That(a.IsVisible && b.IsVisible && a.IsTop() && b.IsTop(), Is.True);
            Assert.That(outer.GetComponent<Canvas>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator PanelPolicySortingObserverFailureContinuesOtherNotificationsAfterCommit()
        {
            yield return StartModule();
            var root = PolicyRoot("YLPolicy.SortObserverRoot");
            var prefab = Template<YLCoreSmokePanel>("YLPolicy.SortObserver", true);
            var first = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            var second = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            first.KeepInFront = second.KeepInFront = true;
            int following = 0;
            YLCoreSmokePanel.SortingProbe = (panel, _) =>
            {
                if (panel == first)
                {
                    Assert.That(second.Canvas.sortingOrder, Is.EqualTo(20), "All Canvas orders must be committed before notifying business code.");
                    throw new InvalidOperationException("YLPolicy.SortObserverFailure");
                }
                if (panel == second) following++;
            };
            try
            {
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: YLPolicy.SortObserverFailure"));
                var third = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                Assert.That(following, Is.EqualTo(1));
                Assert.That(first.IsLoaded && second.IsLoaded && third.IsLoaded, Is.True);
                Assert.That(PanelManager.GetTopPanel(first.RootCanvas), Is.SameAs(second));
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator WindowEntryReopensUniqueSessionWithoutReloadAndCloseEndsIdentity()
        {
            yield return StartModule();
            var root = PolicyRoot("YLWindow.UniqueRoot");
            var prefab = Template<YLCoreSmokePanel>("YLWindow.Unique", true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("unique", prefab));
            var first = Track(catalog.Open<YLCoreSmokePanel>(module, "unique", root));
            Assert.That(first.gameObject.activeSelf && first.IsVisible, Is.True, "Open must show even an initially inactive prefab.");
            var session = ViewSession(first);
            var again = catalog.Open<YLCoreSmokePanel>(module, "unique", root);
            Assert.That(again, Is.SameAs(first));
            Assert.That(ViewSession(again), Is.SameAs(session));
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Has.Count.EqualTo(1));
            first.Close();
            Assert.That(first.Module, Is.Null);
            var reopened = Track(catalog.Open<YLCoreSmokePanel>(module, "unique", root));
            Assert.That(ViewSession(reopened), Is.Not.SameAs(session));
            Assert.That(reopened.Module, Is.SameAs(module));
        }

        [UnityTest]
        public IEnumerator WindowEntryAsyncRejectsPendingAndCancellationDoesNotOccupyKey()
        {
            yield return StartModule();
            var root = PolicyRoot("YLWindow.AsyncRoot");
            var prefab = Template<YLCoreSmokePanel>("YLWindow.Async", true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("unique", prefab));
            var request = catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", root);
            var rejectedArgs = LoadViewArgs.New();
            Assert.Throws<InvalidOperationException>(() => catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", root, rejectedArgs));
            Assert.Throws<ObjectDisposedException>(() => rejectedArgs.GetInt("token"));
            request.Abort();
            Assert.That(request.Status, Is.EqualTo(LoadViewStatus.Canceled));
            var next = catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", root);
            yield return next;
            Assert.That(next.Status, Is.EqualTo(LoadViewStatus.Succeeded), next.error);
            var panel = Track(next.panel);
            Assert.That(panel.gameObject.activeSelf && panel.IsVisible, Is.True);
            var session = ViewSession(panel);
            var again = catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", root);
            Assert.That(again.isDone, Is.True);
            Assert.That(again.panel, Is.SameAs(panel));
            Assert.That(ViewSession(panel), Is.SameAs(session));
            Assert.That(panel.LoadedCount, Is.EqualTo(1));
            panel.Close();
            var pending = catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", root);
            module.Close();
            Assert.That(pending.Status, Is.EqualTo(LoadViewStatus.Invalidated));
            yield return null;
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator WindowEntryInvalidatedPrefabDoesNotFallBackToNamedLoading()
        {
            yield return StartModule();
            var root = PolicyRoot("YLWindow.DeadPrefabRoot");
            var prefab = Template<YLCoreSmokePanel>("YLWindow.DeadPrefab", true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("unique", prefab));
            var request = catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", root);
            Object.DestroyImmediate(prefab);
            yield return request;
            Assert.That(request.Status, Is.EqualTo(LoadViewStatus.Invalidated));
            Assert.That(request.panel, Is.Null);
        }

        [UnityTest]
        public IEnumerator WindowEntryMultiInstanceAndNonReusableKeepOwnerAndDiscardContract()
        {
            yield return StartModule();
            var root = PolicyRoot("YLWindow.MultiRoot");
            var prefab = Template<YLCoreSmokePanel>("YLWindow.Multi", true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("multi", prefab) { Unique = false, Reusable = false });
            var first = Track(catalog.Open<YLCoreSmokePanel>(module, "multi", root));
            var second = Track(catalog.Open<YLCoreSmokePanel>(module, "multi", root));
            Assert.That(first, Is.Not.SameAs(second));
            first.Close();
            Assert.That(first.Module, Is.Null);
            Assert.That(second.Module, Is.SameAs(module));
            yield return null;
            Assert.That(first == null, Is.True, "Non-reusable acquired instance must be destroyed, not cached.");
        }

        [UnityTest]
        public IEnumerator TipsAreModuleOwnedAndFinishPausedWithoutLeakingActiveItems()
        {
            yield return StartModule();
            var root = PolicyRoot("YLTips.Root");
            var presenter = CreateTips(root);
            var oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                var handle = presenter.Show(module, "Paused tip", duration: 0.05f, fade: false);
                var item = handle.Item;
                Assert.That(item.Module, Is.SameAs(module));
                var deadline = Time.realtimeSinceStartup + 2;
                while (!handle.IsDone && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(handle.IsDone, Is.True);
                Assert.That(handle.Result, Is.EqualTo(UITipResult.Completed));
                Assert.That(item.Module, Is.Null);
                Assert.That(item.gameObject.activeSelf, Is.False);
                Assert.That(presenter.ActiveCount, Is.Zero);
            }
            finally { Time.timeScale = oldScale; }
        }

        [UnityTest]
        public IEnumerator TipsCancelReuseModuleExitAndHostEndReleaseOnlyTheirSessions()
        {
            yield return StartModule();
            var root = PolicyRoot("YLTips.CancelRoot");
            var presenter = CreateTips(root);
            var first = presenter.Show(module, "first");
            first.Dispose();
            Assert.That(first.IsDone, Is.True);
            var next = presenter.Show(module, "next");
            Assert.That(next.Item, Is.SameAs(first.Item));
            first.Dispose();
            Assert.That(next.IsDone, Is.False, "Old handle cannot close the reused item.");
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("tips", Template<YLCoreSmokePanel>("YLTips.RealWindow", true)));
            var window = Track(catalog.Open<YLCoreSmokePanel>(module, "tips", root));
            Assert.That(window.Module, Is.SameAs(module), "Tips presentation must not occupy the caller's window key namespace.");
            module.Close();
            Assert.That(next.IsDone, Is.True);
            Assert.That(presenter.ActiveCount, Is.Zero);
            yield return StartModule();
            var ending = presenter.Show(module, "host");
            presenter.enabled = false;
            Assert.That(ending.IsDone, Is.True);
            Assert.That(ending.Result, Is.EqualTo(UITipResult.HostEnded));
            Assert.That(ending.Item.Module, Is.Null);
            Assert.That(presenter.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TipsCompletionFailureStillReturnsItemAndAllowsReentrantReuse()
        {
            yield return StartModule();
            var presenter = CreateTips(PolicyRoot("YLTips.CallbackRoot"));
            UITipHandle reentrant = null;
            Assert.Throws<InvalidOperationException>(() => presenter.Show(module, "zero", duration: 0, completed: _ =>
            {
                reentrant = presenter.Show(module, "new session");
                throw new InvalidOperationException("YLTips.CallbackFailure");
            }));
            Assert.That(presenter.ActiveCount, Is.EqualTo(1));
            Assert.That(reentrant.IsDone, Is.False);
            Assert.That(reentrant.Item.IsLoaded, Is.True);
            reentrant.Dispose();
            Assert.That(presenter.ActiveCount, Is.Zero);
        }

        private UITipsPresenter CreateTips(Transform root)
        {
            var prefab = Template<UITipsItem>("YLTips.Template", true);
            var bg = prefab.AddComponent<UnityEngine.UI.Image>();
            var label = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(prefab.transform, false);
            var text = label.GetComponent<UnityEngine.UI.Text>();
            SetPrivate(prefab.GetComponent<UITipsItem>(), "bg", bg);
            SetPrivate(prefab.GetComponent<UITipsItem>(), "text", text);
            var presenter = root.gameObject.AddComponent<UITipsPresenter>();
            SetPrivate(presenter, "uiTipItemPrefab", prefab);
            SetPrivate(presenter, "uiTipItemPool", root);
            foreach (var field in new[] { "middleTipRoot", "leftTopTipRoot", "leftDownTipRoot", "rightTopTipRoot", "rightDownTipRoot" })
                SetPrivate(presenter, field, root);
            return presenter;
        }

        private static void SetPrivate(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [UnityTest]
        public IEnumerator FormalColorWindowUsesPanelPolicyAndDropsBlockedCapture()
        {
            yield return StartModule();
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YLFramework/Prefabs/ColorSelector.prefab");
            var root = PolicyRoot("YLColor.MixedGateRoot");
            for (int order = 0; order < 2; order++)
            {
                var prefab = Object.Instantiate(asset);
                prefab.SetActive(false);
                objects.Add(prefab);
                prefabKeys.Add(prefab.GetHashCode());
                var rootGate = prefab.AddComponent<YLCoreOtherInputGate>();
                rootGate.Allowed = false;
                if (order == 1)
                    while (UnityEditorInternal.ComponentUtility.MoveComponentUp(rootGate)) { }
                var child = prefab.GetComponentInChildren<ColorSelector>(true).gameObject;
                child.AddComponent<YLCoreOtherInputGate>().Allowed = true;
                prefab.SetActive(true);
                var unowned = prefab.GetComponent<ColorSelectorWindow>();
                var localArgs = ColorSelectorWindow.Arguments(Color.red);
                try
                {
                    unowned.OnLoaded(localArgs);
                    var unownedSelector = child.GetComponent<ColorSelector>();
                    var unownedRect = child.GetComponent<RectTransform>();
                    unownedSelector.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(null)
                    {
                        pointerId = 9,
                        position = RectTransformUtility.WorldToScreenPoint(null, unownedRect.TransformPoint(
                            unownedRect.rect.center + new Vector2(-.44f, .7621024f) * Mathf.Min(unownedRect.rect.width, unownedRect.rect.height) * .5f))
                    });
                    Assert.That(unowned.IsLoaded, Is.False);
                    Assert.That(unowned.CurrentColor, Is.EqualTo(Color.red), "A nearer allowing gate cannot bypass the unloaded formal Panel either.");
                }
                finally
                {
                    typeof(LoadViewArgs).GetMethod("Recycle", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { localArgs });
                    child.GetComponent<ColorSelector>().EndSession();
                    prefab.SetActive(false);
                }
                var catalog = new WindowCatalog();
                catalog.Register(new WindowDefinition("mixed-" + order, prefab));
                int changes = 0;
                var window = Track(catalog.Open<ColorSelectorWindow>(module, "mixed-" + order, root,
                    ColorSelectorWindow.Arguments(Color.red, _ => changes++)));
                window.gameObject.SetActive(true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var selector = window.GetComponentInChildren<ColorSelector>();
                var rect = selector.GetComponent<RectTransform>();
                Vector2 Point(Vector2 normalized) => RectTransformUtility.WorldToScreenPoint(null,
                    rect.TransformPoint(rect.rect.center + normalized * Mathf.Min(rect.rect.width, rect.rect.height) * .5f));
                var data = new UnityEngine.EventSystems.PointerEventData(null) { pointerId = 23,
                    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                    position = Point(new Vector2(-.44f, .7621024f)) };
                selector.OnPointerDown(data);
                Assert.That(window.CurrentColor.g, Is.EqualTo(1).Within(.001f), "An unrelated denying gate on the Panel must not change its original priority.");
                Assert.That(changes, Is.EqualTo(1));
                var modal = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLColor.MixedModal" + order, true), root, loadViewArgs: null));
                modal.gameObject.SetActive(true);
                modal.Modal = true;
                modal.RefreshUIState();
                Assert.That(window.IsInputBlocked, Is.True);
                data.position = Point(new Vector2(-.44f, -.7621024f));
                selector.OnDrag(data);
                Assert.That(window.CurrentColor.g, Is.EqualTo(1).Within(.001f), "A nearer allowing gate must not bypass the actual modal Panel.");
                Assert.That(changes, Is.EqualTo(1));
                modal.Close();
                selector.OnDrag(data);
                Assert.That(changes, Is.EqualTo(1), "Unblocking must not revive the old pointer capture.");
                selector.OnPointerDown(data);
                Assert.That(window.CurrentColor.b, Is.EqualTo(1).Within(.001f));
                Assert.That(changes, Is.EqualTo(2));
                foreach (var gate in window.GetComponentsInChildren<YLCoreOtherInputGate>(true))
                    Assert.That(gate.Reads, Is.Zero, "The explicit formal Panel owns this session, in either component order.");
                window.Close();
            }
#endif
        }

        [UnityTest]
        public IEnumerator FormalColorPrefabSupportsCameraCoordinatesCallbacksAndReuse()
        {
            yield return StartModule();
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YLFramework/Prefabs/ColorSelector.prefab");
            Assert.That(asset, Is.Not.Null);
            var prefab = Object.Instantiate(asset);
            prefab.SetActive(false);
            objects.Add(prefab);
            prefabKeys.Add(prefab.GetHashCode());
            var root = PolicyRoot("YLColor.CameraRoot");
            var cameraObject = new GameObject("YLColor.Camera", typeof(Camera));
            objects.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("color", prefab));
            int old = 0, current = 0;
            var first = Track(catalog.Open<ColorSelectorWindow>(module, "color", root,
                ColorSelectorWindow.Arguments(new Color(1, 0, 0, 0.35f), _ => old++)));
            first.gameObject.SetActive(true);
            first.GetComponent<RectTransform>().anchoredPosition = new Vector2(120, -80);
            first.transform.localScale = new Vector3(0.8f, 0.8f, 1);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var selector = first.GetComponentInChildren<YLFramework.Core.ColorSelector>();
            var wheel = selector.GetComponent<RectTransform>();
            var local = wheel.rect.center + new Vector2(-0.44f, 0.7621024f) * (wheel.rect.width * 0.5f);
            var data = new UnityEngine.EventSystems.PointerEventData(null)
            {
                pointerId = 3,
                position = RectTransformUtility.WorldToScreenPoint(camera, wheel.TransformPoint(local)),
                pointerPressRaycast = new UnityEngine.EventSystems.RaycastResult { module = first.GetComponent<UnityEngine.UI.GraphicRaycaster>() }
            };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty, "The actual Camera canvas must expose the wheel through UGUI raycasting.");
            Assert.That(hits[0].gameObject, Is.SameAs(selector.gameObject));
            Assert.That(UIPointer.CheckPositionOnUI(data.position), Is.True);
            Assert.That(UIPointer.CheckPositionOnUI(new Vector2(-1, -1)), Is.False);
            var system = UnityEngine.EventSystems.EventSystem.current;
            try
            {
                system.enabled = false;
                Assert.That(UIPointer.CheckPositionOnUI(data.position), Is.False);
            }
            finally { system.enabled = true; }
            Assert.That(UIPointer.CheckPositionOnUI(data.position), Is.True, "Cache must follow the actual current EventSystem.");
            data.pointerPressRaycast = hits[0];
            UnityEngine.EventSystems.ExecuteEvents.Execute(hits[0].gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            Assert.That(first.CurrentColor.g, Is.EqualTo(1).Within(0.001f));
            Assert.That(first.CurrentColor.r, Is.EqualTo(0).Within(0.001f));
            Assert.That(first.CurrentColor.a, Is.EqualTo(0.35f));
            Assert.That(old, Is.EqualTo(1));
            first.Close();
            var second = Track(catalog.Open<ColorSelectorWindow>(module, "color", root,
                ColorSelectorWindow.Arguments(Color.blue, _ => current++)));
            second.gameObject.SetActive(true);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.CurrentColor, Is.EqualTo(Color.blue));
            selector.OnDrag(data);
            Assert.That(current, Is.Zero, "Old pointer cannot change the next session.");
            data.position = RectTransformUtility.WorldToScreenPoint(camera, wheel.TransformPoint(local));
            selector.OnPointerDown(data);
            Assert.That(current, Is.EqualTo(1));
            Assert.That(old, Is.EqualTo(1));
            second.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(second.Module, Is.Null);
#endif
        }

        [UnityTest]
        public IEnumerator ModalBlocksActualRaycastsSelectionAndRestoresValidFocus()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.ModalRoot");
            var lower = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.Lower", true), root, loadViewArgs: null));
            lower.gameObject.SetActive(true);
            var buttonObject = new GameObject("LowerButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            buttonObject.transform.SetParent(lower.transform, false);
            var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(100, 100);
            buttonRect.anchoredPosition = new Vector2(-150, 0);
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            var system = UnityEngine.EventSystems.EventSystem.current;
            if (!system)
            {
                var input = new GameObject("YLInput.TestSystem", typeof(UnityEngine.EventSystems.EventSystem));
                objects.Add(input);
                system = input.GetComponent<UnityEngine.EventSystems.EventSystem>();
            }
            system.SetSelectedGameObject(buttonObject);
            var modal = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.Modal", true), root, loadViewArgs: null));
            modal.gameObject.SetActive(true);
            modal.Modal = true;
            modal.RefreshUIState();
            Canvas.ForceUpdateCanvases();
            Assert.That(button.IsInteractable(), Is.False, "Logical blocking must also constrain UGUI navigation and submit.");
            Assert.That(system.currentSelectedGameObject, Is.Not.SameAs(buttonObject));
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            system.RaycastAll(new UnityEngine.EventSystems.PointerEventData(system)
            { position = RectTransformUtility.WorldToScreenPoint(null, buttonRect.position) }, hits);
            Assert.That(hits.Exists(hit => hit.gameObject == buttonObject), Is.False);
            UnityEngine.EventSystems.ExecuteEvents.Execute(buttonObject, new UnityEngine.EventSystems.BaseEventData(system),
                UnityEngine.EventSystems.ExecuteEvents.submitHandler);
            Assert.That(clicks, Is.Zero);
            modal.Close();
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(system.currentSelectedGameObject, Is.SameAs(buttonObject));
            lower.Close();
            Assert.That(system.currentSelectedGameObject, Is.Null, "Closed sessions cannot remain selected.");
        }

        [UnityTest]
        public IEnumerator DeselectCloseDoesNotLetOldInputCoveragePolluteReusedPanel()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.ReentrantRoot");
            var prefab = Template<YLCoreSmokePanel>("YLInput.ReentrantLower", true);
            foreach (var name in new[] { "First", "Second" })
            {
                var child = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(YLCoreSelectionInputProbe));
                child.transform.SetParent(prefab.transform, false);
            }
            var lower = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            lower.gameObject.SetActive(true);
            var system = UnityEngine.EventSystems.EventSystem.current;
            var buttons = lower.GetComponentsInChildren<UnityEngine.UI.Button>();
            buttons[0].GetComponent<YLCoreSelectionInputProbe>().Deselected = lower.Close;
            system.SetSelectedGameObject(buttons[0].gameObject);
            var modal = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.ReentrantModal", true), root, loadViewArgs: null));
            modal.gameObject.SetActive(true);
            modal.Modal = true;
            modal.RefreshUIState();
            Assert.That(lower.Module, Is.Null);
            modal.Close();
            var next = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            Assert.That(next, Is.SameAs(lower));
            next.gameObject.SetActive(true);
            foreach (var button in next.GetComponentsInChildren<UnityEngine.UI.Button>())
                Assert.That(button.IsInteractable(), Is.True, "Old Apply must stop as soon as Deselect ends its session.");
            Assert.That(next.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator NestedIndependentModalSelectionIsNotOwnedByBlockedAncestor()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.NestedRoot");
            var lower = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.NestedLower", true), root, loadViewArgs: null));
            lower.gameObject.SetActive(true);
            var modalPrefab = Template<YLCoreSmokePanel>("YLInput.NestedModal", true);
            modalPrefab.AddComponent<Canvas>();
            var modal = Track(module.LoadView<YLCoreSmokePanel>(modalPrefab, lower.transform, loadViewArgs: null));
            var button = new GameObject("NestedModalButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(YLCoreSelectionInputProbe));
            button.transform.SetParent(modal.transform, false);
            int deselections = 0;
            button.GetComponent<YLCoreSelectionInputProbe>().Deselected = () => deselections++;
            modal.gameObject.SetActive(true);
            modal.Modal = true;
            modal.RefreshUIState();
            var system = UnityEngine.EventSystems.EventSystem.current;
            system.SetSelectedGameObject(button);
            yield return null;
            yield return null;
            Assert.That(system.currentSelectedGameObject, Is.SameAs(button));
            Assert.That(deselections, Is.Zero, "A blocked ancestor must not deselect an independent descendant modal each frame.");
        }

        [UnityTest]
        public IEnumerator OnSelectCloseDefersSelectionCleanupWithoutLosingPreviousFocus()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.SelectCloseRoot");
            var lower = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.SelectCloseLower", true), root, loadViewArgs: null));
            lower.gameObject.SetActive(true);
            var previous = new GameObject("Previous", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            previous.transform.SetParent(lower.transform, false);
            var system = UnityEngine.EventSystems.EventSystem.current;
            system.SetSelectedGameObject(previous);
            var modal = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.SelectCloseModal", true), root, loadViewArgs: null));
            var selected = new GameObject("CloseOnSelect", typeof(RectTransform), typeof(UnityEngine.UI.Button), typeof(YLCoreSelectionInputProbe));
            selected.transform.SetParent(modal.transform, false);
            selected.GetComponent<YLCoreSelectionInputProbe>().Selected = modal.Close;
            modal.Modal = true;
            modal.gameObject.SetActive(true);
            modal.RefreshUIState();
            yield return null;
            yield return null;
            Assert.That(modal.Module, Is.Null);
            Assert.That(system.currentSelectedGameObject, Is.SameAs(previous));
        }

        [UnityTest]
        public IEnumerator ReparentedControlsReleaseOldInputCoverageBeforeNewOwnerConfiguration()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.ControlMoveRoot");
            var lower = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.ControlMoveLower", true), root, loadViewArgs: null));
            lower.gameObject.SetActive(true);
            var child = new GameObject("MovedButton", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            child.transform.SetParent(lower.transform, false);
            var button = child.GetComponent<UnityEngine.UI.Button>();
            var modal = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.ControlMoveModal", true), root, loadViewArgs: null));
            modal.gameObject.SetActive(true);
            modal.Modal = true;
            modal.RefreshUIState();
            Assert.That(button.interactable, Is.False);
            child.transform.SetParent(modal.transform, false);
            yield return null;
            Assert.That(button.interactable, Is.True);
            button.interactable = false;
            lower.Close();
            Assert.That(button.interactable, Is.False, "The old state must not write attributes after ownership has moved.");
        }

        [UnityTest]
        public IEnumerator ActivePanelMovedToPreviouslyUnusedRootKeepsItsOwnedInput()
        {
            yield return StartModule();
            var first = PolicyRoot("YLInput.MoveRootA");
            var second = PolicyRoot("YLInput.MoveRootB");
            var panel = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.MovedPanel", true), first, loadViewArgs: null));
            panel.gameObject.SetActive(true);
            var system = UnityEngine.EventSystems.EventSystem.current;
            panel.transform.SetParent(second, false);
            yield return null;
            Object.Destroy(first.gameObject);
            yield return null;
            yield return null;
            Assert.That(panel.RootCanvas, Is.SameAs(second.GetComponent<Canvas>()));
            Assert.That(system && system.isActiveAndEnabled, Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.SameAs(system));
        }

#if ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
#if ENABLE_INPUT_SYSTEM
        [UnityTest]
        public IEnumerator InputSystemMouseDrivesFormalColorDragAndCloseButton()
            => DriveFormalColorWithInputSystem(false);

        [UnityTest]
        public IEnumerator InputSystemMouseDrivesCameraColorAfterResolutionChange()
            => DriveFormalColorWithInputSystem(true);
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        [UnityTest]
        public IEnumerator BorrowedStandaloneModuleDrivesFormalColorViaBaseInputOverride()
            => DriveFormalColorWithInputSystem(false, true);
#endif

        private IEnumerator DriveFormalColorWithInputSystem(bool cameraMode, bool legacy = false)
        {
            yield return StartModule();
#if UNITY_EDITOR
            var root = PolicyRoot("YLInput.NativeMouseRoot");
#if ENABLE_LEGACY_INPUT_MANAGER
            YLCoreLegacyPointerInput scriptedInput = null;
            if (legacy)
            {
                var external = new GameObject("YLInput.LegacySystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule), typeof(YLCoreLegacyPointerInput));
                objects.Add(external);
                scriptedInput = external.GetComponent<YLCoreLegacyPointerInput>();
                external.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>().inputOverride = scriptedInput;
            }
#endif
            root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            var context = root.gameObject.AddComponent<UIRootContext>();
            Camera camera = null;
            if (cameraMode)
            {
                var cameraObject = new GameObject("YLInput.NativeCamera", typeof(Camera));
                objects.Add(cameraObject);
                camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
            }
            context.ConfigureCanvas(cameraMode ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay, camera, 5);
            context.ConfigureResolution(new Vector2(800, 600));
            var catalog = new WindowCatalog();
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YLFramework/Prefabs/ColorSelector.prefab");
            var prefab = Object.Instantiate(asset);
            prefab.SetActive(false);
            objects.Add(prefab);
            prefabKeys.Add(prefab.GetHashCode());
            catalog.Register(new WindowDefinition("inputColor", prefab));
            int changes = 0;
            var window = Track(catalog.Open<ColorSelectorWindow>(module, "inputColor", root,
                ColorSelectorWindow.Arguments(new Color(1, 0, 0, 0.4f), _ => changes++)));
            window.gameObject.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var system = UnityEngine.EventSystems.EventSystem.current;
            Assert.That(system.currentInputModule.GetType().FullName, Is.EqualTo(legacy ? "UnityEngine.EventSystems.StandaloneInputModule" : "UnityEngine.InputSystem.UI.InputSystemUIInputModule"));
            var inputType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            var stateType = Type.GetType("UnityEngine.InputSystem.LowLevel.MouseState, Unity.InputSystem");
            var mouse = legacy ? null : inputType.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Mouse", "YLInput.OwnedMouse", null });
            Action<Vector2, bool> queue = (position, pressed) =>
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                if (scriptedInput) { scriptedInput.Set(position, pressed); return; }
#endif
                QueueMouse(inputType, stateType, mouse, position, pressed);
            };
            Action restoreDevices = null;
            try
            {
                if (mouse != null) restoreDevices = UseOwnedInputDevices(system.currentInputModule, mouse);
                var wheel = window.GetComponentInChildren<ColorSelector>().GetComponent<RectTransform>();
                var center = wheel.rect.center;
                float radius = Mathf.Min(wheel.rect.width, wheel.rect.height) * 0.5f;
                Vector2 green = RectTransformUtility.WorldToScreenPoint(camera, wheel.TransformPoint(center + new Vector2(-0.44f, 0.7621024f) * radius));
                queue(green, false);
                yield return null;
                queue(green, true);
                yield return null;
                string pointerEvidence = "Legacy override";
                if (mouse != null)
                {
                    var inputModule = system.currentInputModule;
                    var pointReference = inputModule.GetType().GetProperty("point").GetValue(inputModule);
                    var action = pointReference.GetType().GetProperty("action").GetValue(pointReference);
                    var value = action.GetType().GetMethod("ReadValueAsObject").Invoke(action, null);
                    int id = (int)mouse.GetType().GetProperty("deviceId").GetValue(mouse);
                    var hit = (UnityEngine.EventSystems.RaycastResult)inputModule.GetType().GetMethod("GetLastRaycastResult").Invoke(inputModule, new object[] { id });
                    pointerEvidence = $"focused={system.isFocused} requested={green} action={value} hit={hit.gameObject}";
                }
                Assert.That(window.CurrentColor.g, Is.EqualTo(1).Within(0.01f), pointerEvidence);
                Assert.That(changes, Is.EqualTo(1));
                context.ConfigureResolution(new Vector2(600, 800), false, 0.25f);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector2 blue = RectTransformUtility.WorldToScreenPoint(camera, wheel.TransformPoint(center + new Vector2(-0.44f, -0.7621024f) * radius));
                queue(blue, true);
                yield return null;
                Assert.That(window.CurrentColor.b, Is.EqualTo(1).Within(0.01f));
                Assert.That(window.CurrentColor.a, Is.EqualTo(0.4f));
                queue(blue, false);
                yield return null;
                var close = window.GetComponentInChildren<UnityEngine.UI.Button>();
                Vector2 closePoint = RectTransformUtility.WorldToScreenPoint(camera, close.transform.position);
                queue(closePoint, false);
                yield return null;
                queue(closePoint, true);
                yield return null;
                queue(closePoint, false);
                yield return null;
                Assert.That(window.Module, Is.Null, "Close must be dispatched by the input module, not invoked directly.");
            }
            finally
            {
                restoreDevices?.Invoke();
                if (mouse != null) inputType.GetMethod("RemoveDevice").Invoke(null, new[] { mouse });
            }
#endif
        }

        private static void QueueMouse(Type inputType, Type stateType, object mouse, Vector2 position, bool pressed, bool rightPressed = false)
        {
            var state = Activator.CreateInstance(stateType);
            stateType.GetField("position").SetValue(state, position);
            stateType.GetField("buttons").SetValue(state, (ushort)((pressed ? 1 : 0) | (rightPressed ? 2 : 0)));
            QueueInputState(inputType, stateType, mouse, state);
        }

        // 真实原生鼠标也会向默认 PassThrough actions 推送位置；仅限制本测试自建系统的 assets，不重置全局输入。
        private Action UseOwnedInputDevices(object module, params object[] devices)
        {
            var host = ((UnityEngine.EventSystems.BaseInputModule)module).gameObject;
            Assert.That(priorEventSystems.Contains(host), Is.False, "Never change a pre-existing EventSystem's device filter.");
            var manager = typeof(PanelManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var owned = manager == null ? null : typeof(PanelManager).GetField("_eventSystem", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            Assert.That(ReferenceEquals(owned, host) || objects.Contains(host), Is.True, "Only this fixture's input host may be filtered.");
            var asset = module.GetType().GetProperty("actionsAsset").GetValue(module);
#if UNITY_EDITOR
            Assert.That(UnityEditor.AssetDatabase.Contains((Object)asset), Is.False, "Only the owned runtime actions may be filtered, never persistent assets.");
#endif
            var property = asset.GetType().GetProperty("devices");
            var previous = property.GetValue(asset);
            var readonlyArray = property.PropertyType.GetGenericArguments()[0];
            var deviceType = readonlyArray.GetGenericArguments()[0];
            var array = Array.CreateInstance(deviceType, devices.Length);
            for (int i = 0; i < devices.Length; i++) array.SetValue(devices[i], i);
            property.SetValue(asset, Activator.CreateInstance(readonlyArray, new object[] { array }));
            return () => property.SetValue(asset, previous);
        }

        private static void QueueInputState(Type inputType, Type stateType, object device, object state)
        {
            foreach (var method in inputType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (method.Name == "QueueStateEvent" && method.IsGenericMethodDefinition)
                {
                    method.MakeGenericMethod(stateType).Invoke(null, new[] { device, state, (object)(-1d) });
                    return;
                }
            Assert.Fail("Installed Input System does not expose QueueStateEvent<TState>.");
        }

#if ENABLE_INPUT_SYSTEM
        private static void QueueKeyboard(Type inputType, object keyboard, string keyName = null)
        {
            var keyType = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");
            var stateType = Type.GetType("UnityEngine.InputSystem.LowLevel.KeyboardState, Unity.InputSystem");
            var keys = Array.CreateInstance(keyType, keyName == null ? 0 : 1);
            if (keyName != null) keys.SetValue(Enum.Parse(keyType, keyName), 0);
            var state = Activator.CreateInstance(stateType, new object[] { keys });
            QueueInputState(inputType, stateType, keyboard, state);
        }

#if !ENABLE_LEGACY_INPUT_MANAGER
        [UnityTest]
        public IEnumerator InputSystemOnlyDiagnosticShortcutUsesF1WithoutLegacyPolling()
        {
            var type = Type.GetType("YLFramework.Services.Audio.AudioKitDebugWindow, YLFramework.Runtime");
            Assert.That(type, Is.Not.Null);
            bool visible = (bool)type.GetProperty("IsVisible").GetValue(null);
            type.GetMethod("Hide").Invoke(null, null);
            var inputType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            var keyboard = inputType.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Keyboard", "YLInput.DiagnosticKeyboard", null });
            try
            {
                yield return null;
                QueueKeyboard(inputType, keyboard, "F1"); yield return null;
                QueueKeyboard(inputType, keyboard); yield return null;
                Assert.That((bool)type.GetProperty("IsVisible").GetValue(null), Is.True);
            }
            finally
            {
                type.GetMethod(visible ? "Show" : "Hide").Invoke(null, null);
                inputType.GetMethod("RemoveDevice").Invoke(null, new[] { keyboard });
            }
        }
#endif

        [UnityTest]
        public IEnumerator NewModalCancelsAlreadyCapturedScrollRectDrag()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.ScrollRoot");
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var lower = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.ScrollLower", true), root, loadViewArgs: null));
            lower.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 400);
            var scroll = lower.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            lower.gameObject.AddComponent<UnityEngine.UI.Image>();
            var content = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            content.transform.SetParent(lower.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(600, 600);
            scroll.content = contentRect;
            scroll.viewport = lower.GetComponent<RectTransform>();
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Unrestricted;
            scroll.inertia = false;
            lower.gameObject.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var inputType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            var mouseType = Type.GetType("UnityEngine.InputSystem.LowLevel.MouseState, Unity.InputSystem");
            var mouse = inputType.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Mouse", "YLInput.ScrollMouse", null });
            Action restoreDevices = null;
            try
            {
                restoreDevices = UseOwnedInputDevices(UnityEngine.EventSystems.EventSystem.current.currentInputModule, mouse);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, lower.transform.position);
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, true); yield return null;
                QueueMouse(inputType, mouseType, mouse, point + new Vector2(40, 40), true); yield return null;
                QueueMouse(inputType, mouseType, mouse, point + new Vector2(60, 60), true); yield return null;
                Assert.That(contentRect.anchoredPosition.sqrMagnitude, Is.GreaterThan(1));
                var modal = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.ScrollModal", true), root, loadViewArgs: null));
                modal.gameObject.SetActive(true);
                modal.Modal = true;
                modal.RefreshUIState();
                Vector2 stopped = contentRect.anchoredPosition;
                QueueMouse(inputType, mouseType, mouse, point + new Vector2(100, 100), true); yield return null;
                Assert.That(contentRect.anchoredPosition, Is.EqualTo(stopped));
                modal.Close();
                QueueMouse(inputType, mouseType, mouse, point + new Vector2(120, 120), true); yield return null;
                Assert.That(contentRect.anchoredPosition, Is.EqualTo(stopped), "An old capture must not resume after the modal ends.");
                Assert.That(scroll.enabled, Is.True);
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
            }
            finally { restoreDevices?.Invoke(); inputType.GetMethod("RemoveDevice").Invoke(null, new[] { mouse }); }
        }

        [UnityTest]
        public IEnumerator RemovingPressedDeviceReleasesInvalidControlCapture()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.RemovedDeviceRoot");
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var lower = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.RemovedDeviceLower", true), root, loadViewArgs: null));
            var sliderObject = new GameObject("CapturedSlider", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Slider));
            sliderObject.transform.SetParent(lower.transform, false);
            var slider = sliderObject.GetComponent<UnityEngine.UI.Slider>();
            lower.gameObject.SetActive(true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var inputType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            var stateType = Type.GetType("UnityEngine.InputSystem.LowLevel.MouseState, Unity.InputSystem");
            var mouse = inputType.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Mouse", "YLInput.RemovedMouse", null });
            var remove = inputType.GetMethod("RemoveDevice");
            Action restore = null;
            bool removed = false;
            try
            {
                restore = UseOwnedInputDevices(UnityEngine.EventSystems.EventSystem.current.currentInputModule, mouse);
                var point = RectTransformUtility.WorldToScreenPoint(null, slider.transform.position);
                QueueMouse(inputType, stateType, mouse, point, false); yield return null;
                QueueMouse(inputType, stateType, mouse, point, true); yield return null;
                var modal = Track(module.LoadView<YLCoreSmokePanel>(Template<YLCoreSmokePanel>("YLInput.RemovedDeviceModal", true), root, loadViewArgs: null));
                modal.gameObject.SetActive(true);
                modal.Modal = true;
                modal.RefreshUIState();
                Assert.That(slider.enabled, Is.False);
                remove.Invoke(null, new[] { mouse });
                removed = true;
                yield return null;
                modal.Close();
                yield return null;
                Assert.That(slider.enabled, Is.True, "Removed controls cannot retain an invalid capture or read device state.");
            }
            finally { restore?.Invoke(); if (!removed) remove.Invoke(null, new[] { mouse }); }
        }

        [UnityTest]
        public IEnumerator OldPointerPressCannotClickAReusedWindowSession()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.OldPressRoot");
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = Template<YLCoreSmokePanel>("YLInput.OldPressPanel", true);
            var child = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
            child.transform.SetParent(prefab.transform, false);
            var lower = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            lower.gameObject.SetActive(true);
            int clicks = 0;
            var button = lower.GetComponentInChildren<UnityEngine.UI.Button>();
            button.onClick.AddListener(() => clicks++);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var inputType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            var mouseType = Type.GetType("UnityEngine.InputSystem.LowLevel.MouseState, Unity.InputSystem");
            var mouse = inputType.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Mouse", "YLInput.OldPressMouse", null });
            Action restoreDevices = null;
            try
            {
                restoreDevices = UseOwnedInputDevices(UnityEngine.EventSystems.EventSystem.current.currentInputModule, mouse);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, true); yield return null;
                lower.Close();
                var next = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                Assert.That(next, Is.SameAs(lower));
                next.gameObject.SetActive(true);
                QueueMouse(inputType, mouseType, mouse, point, true, true); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, false, true); yield return null;
                Assert.That(clicks, Is.Zero, "Up from an old session cannot invoke a new session's Button.");
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, true); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                Assert.That(clicks, Is.EqualTo(1));
            }
            finally { restoreDevices?.Invoke(); inputType.GetMethod("RemoveDevice").Invoke(null, new[] { mouse }); }
        }

        [UnityTest]
        public IEnumerator NativeDownOnSelectReuseCannotAuthorizeOldPressForNewSession()
        {
            yield return StartModule();
            var root = PolicyRoot("YLInput.DownSelectRoot");
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = Template<YLCoreSmokePanel>("YLInput.DownSelectPanel", true);
            var child = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(YLCoreSelectionInputProbe));
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
            child.transform.SetParent(prefab.transform, false);
            var lower = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
            lower.gameObject.SetActive(true);
            int clicks = 0;
            var button = lower.GetComponentInChildren<UnityEngine.UI.Button>();
            button.onClick.AddListener(() => clicks++);
            YLCoreSmokePanel replacement = null;
            button.GetComponent<YLCoreSelectionInputProbe>().Selected = () =>
            {
                lower.Close();
                replacement = Track(module.LoadView<YLCoreSmokePanel>(prefab, root, loadViewArgs: null));
                replacement.GetComponentInChildren<YLCoreSelectionInputProbe>().Selected = null;
                replacement.gameObject.SetActive(true);
            };
            yield return null;
            Canvas.ForceUpdateCanvases();
            var inputType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            var mouseType = Type.GetType("UnityEngine.InputSystem.LowLevel.MouseState, Unity.InputSystem");
            var mouse = inputType.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) })
                .Invoke(null, new object[] { "Mouse", "YLInput.DownSelectMouse", null });
            Action restoreDevices = null;
            try
            {
                restoreDevices = UseOwnedInputDevices(UnityEngine.EventSystems.EventSystem.current.currentInputModule, mouse);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, true); yield return null;
                Assert.That(replacement, Is.SameAs(lower));
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                Assert.That(clicks, Is.Zero);
                QueueMouse(inputType, mouseType, mouse, point, true); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                Assert.That(clicks, Is.EqualTo(1));
            }
            finally { restoreDevices?.Invoke(); inputType.GetMethod("RemoveDevice").Invoke(null, new[] { mouse }); }
        }

        [UnityTest]
        public IEnumerator InputSystemModalMaskSubmitCancelAndSliderNavigationUseCurrentSession()
        {
            yield return StartModule();
#if UNITY_EDITOR
            var template = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YLFramework/Prefabs/YLFramework.prefab");
            var host = Object.Instantiate(template);
            objects.Add(host);
            var context = host.GetComponentInChildren<UIRootContext>();
            var catalog = new WindowCatalog();
            var prefab = Template<YLCoreSmokePanel>("YLInput.NavigationWindow", true);
            catalog.Register(new WindowDefinition("lowerInput", prefab) { Layer = 0 });
            catalog.Register(new WindowDefinition("modalInput", prefab) { Layer = 3, Modal = true });
            var lower = Track(catalog.Open<YLCoreSmokePanel>(module, "lowerInput", context.Canvas.transform));
            lower.gameObject.SetActive(true);
            var lowerButton = new GameObject("LowerButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            lowerButton.transform.SetParent(lower.transform, false);
            int lowerClicks = 0, modalClicks = 0;
            lowerButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => lowerClicks++);
            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Slider));
            sliderObject.transform.SetParent(lower.transform, false);
            var slider = sliderObject.GetComponent<UnityEngine.UI.Slider>();
            var system = UnityEngine.EventSystems.EventSystem.current;
            yield return null;
            system.SetSelectedGameObject(lowerButton);
            var modal = Track(catalog.Open<YLCoreSmokePanel>(module, "modalInput", context.Canvas.transform));
            var modalButton = new GameObject("ModalButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(YLCoreSelectionInputProbe));
            modalButton.transform.SetParent(modal.transform, false);
            modalButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => modalClicks++);
            modal.gameObject.SetActive(true);
            modal.RefreshUIState();
            yield return null;
            Assert.That(system.currentSelectedGameObject, Is.SameAs(modalButton));
            Assert.That(lowerButton.GetComponent<UnityEngine.UI.Button>().IsInteractable(), Is.False);
            var inputType = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem");
            var mouseType = Type.GetType("UnityEngine.InputSystem.LowLevel.MouseState, Unity.InputSystem");
            var add = inputType.GetMethod("AddDevice", new[] { typeof(string), typeof(string), typeof(string) });
            var mouse = add.Invoke(null, new object[] { "Mouse", "YLInput.MaskMouse", null });
            var keyboard = add.Invoke(null, new object[] { "Keyboard", "YLInput.NavigationKeyboard", null });
            Action restoreDevices = null;
            try
            {
                restoreDevices = UseOwnedInputDevices(system.currentInputModule, mouse, keyboard);
                Canvas.ForceUpdateCanvases();
                var point = new Vector2(10, 10);
                var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                system.RaycastAll(new UnityEngine.EventSystems.PointerEventData(system) { position = point }, hits);
                Assert.That(hits, Is.Not.Empty);
                Assert.That(hits[0].gameObject.name, Is.EqualTo("Mask"));
                Assert.That(UIPointer.CheckPositionOnUI(point), Is.True, "Mask is a world-input blocker, not an empty UI result.");
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, true); yield return null;
                QueueMouse(inputType, mouseType, mouse, point, false); yield return null;
                Assert.That(lowerClicks, Is.Zero);
                // 点击非 Selectable 遮罩可清空选择；显式选择模态按钮后验证真正后端的导航事件。
                system.SetSelectedGameObject(modalButton);
                QueueKeyboard(inputType, keyboard, "Enter"); yield return null;
                QueueKeyboard(inputType, keyboard); yield return null;
                Assert.That(modalClicks, Is.EqualTo(1));
                Assert.That(lowerClicks, Is.Zero);
                QueueKeyboard(inputType, keyboard, "Escape"); yield return null;
                QueueKeyboard(inputType, keyboard); yield return null;
                Assert.That(modalButton.GetComponent<YLCoreSelectionInputProbe>().Cancels, Is.EqualTo(1));
                modal.Close();
                Assert.That(system.currentSelectedGameObject, Is.SameAs(lowerButton));
                QueueKeyboard(inputType, keyboard, "Enter"); yield return null;
                QueueKeyboard(inputType, keyboard); yield return null;
                Assert.That(lowerClicks, Is.EqualTo(1));
                system.SetSelectedGameObject(sliderObject);
                float oldValue = slider.value;
                QueueKeyboard(inputType, keyboard, "RightArrow"); yield return null;
                QueueKeyboard(inputType, keyboard); yield return null;
                Assert.That(slider.value, Is.GreaterThan(oldValue));
                var tip = host.GetComponentInChildren<UITipsPresenter>().Show(module, "Adapted input", UITipPos.RightTop);
                Assert.That(((RectTransform)tip.Item.transform.parent).anchorMin, Is.EqualTo(Vector2.one));
                tip.Dispose();
            }
            finally
            {
                restoreDevices?.Invoke();
                var remove = inputType.GetMethod("RemoveDevice");
                remove.Invoke(null, new[] { mouse });
                remove.Invoke(null, new[] { keyboard });
            }
#endif
        }
#endif
#endif

        [UnityTest]
        public IEnumerator MigratedRootTemplateHasLayersMasksAndBoundTipsWithoutLegacyComponents()
        {
            yield return StartModule();
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YLFramework/Prefabs/YLFramework.prefab");
            var instance = Object.Instantiate(prefab);
            objects.Add(instance);
            var context = instance.GetComponentInChildren<UIRootContext>(true);
            var presenter = instance.GetComponentInChildren<UITipsPresenter>(true);
            Assert.That(context && presenter, Is.True);
            var panelPrefab = Template<YLCoreSmokePanel>("YLWindow.LayerTemplate", true);
            panelPrefab.AddComponent<UnityEngine.UI.Image>();
            panelPrefab.SetActive(true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("lower", panelPrefab) { Layer = 0 });
            catalog.Register(new WindowDefinition("modal", panelPrefab) { Layer = 3, Modal = true });
            var lower = Track(catalog.Open<YLCoreSmokePanel>(module, "lower", context.Canvas.transform));
            lower.SortingOrder = 300;
            var modal = Track(catalog.Open<YLCoreSmokePanel>(module, "modal", context.Canvas.transform));
            modal.SortingOrder = 0;
            Assert.That(modal.RootCanvas, Is.SameAs(lower.RootCanvas));
            Assert.That(modal.DisplaySortingOrder, Is.GreaterThan(lower.DisplaySortingOrder), "Module/raw order must not cross the configured layer priority.");
            Assert.That(lower.IsInputBlocked, Is.True);
            var masks = context.GetLayer(3).GetComponentsInChildren<UnityEngine.UI.Image>(true);
            UnityEngine.UI.Image mask = null;
            foreach (var image in masks) if (image.gameObject.name == "Mask") mask = image;
            Assert.That(mask, Is.Not.Null);
            Assert.That(mask.gameObject.activeSelf && mask.raycastTarget, Is.True);
            Assert.That(mask.GetComponent<Canvas>().sortingOrder, Is.EqualTo(modal.DisplaySortingOrder - 1));
            var placement = presenter.Show(module, "Right top", UITipPos.RightTop);
            Assert.That(((RectTransform)placement.Item.transform.parent).anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(placement.Item.DisplaySortingOrder, Is.GreaterThan(modal.DisplaySortingOrder), "Non-interactive Tips must render above the configured window layers.");
            placement.Dispose();
            var tip = presenter.Show(module, "Migrated template", UITipPos.RightTop, duration: 0);
            Assert.That(tip.IsDone, Is.True);
            Assert.That(presenter.ActiveCount, Is.Zero);
            modal.Close();
            Assert.That(mask.gameObject.activeSelf, Is.False);
            Assert.That(lower.IsInputBlocked, Is.False);
            lower.Close();
            modal = Track(catalog.Open<YLCoreSmokePanel>(module, "modal", context.Canvas.transform));
            int order = modal.SortingOrder;
            Assert.Throws<ArgumentOutOfRangeException>(() => modal.SortingOrder = short.MinValue);
            Assert.That(modal.SortingOrder, Is.EqualTo(order), "Mask range rejection must happen before committing the order.");
            modal.Close();
            foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Assert.That(component, Is.Not.Null, "Migrated template must import without missing scripts.");
                Assert.That(component.GetType().Namespace, Is.Not.EqualTo("YLFramework.Services.UI"));
            }
#endif
        }

        [UnityTest]
        public IEnumerator WindowActivationReentryNeverUpdatesTheReplacementSession()
        {
            yield return StartModule();
            var root = PolicyRoot("YLWindow.ActivationRoot");
            var prefab = Template<YLCoreSmokePanel>("YLWindow.Activation", true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("unique", prefab));
            var first = Track(catalog.Open<YLCoreSmokePanel>(module, "unique", root));
            first.gameObject.SetActive(false);
            YLCoreSmokePanel replacement = null;
            int updates = 0;
            YLCoreSmokePanel.UpdateProbe = (_, __) => updates++;
            YLCoreSmokePanel.EnabledProbe = panel =>
            {
                YLCoreSmokePanel.EnabledProbe = null;
                panel.Close();
                var next = LoadViewArgs.New();
                next.SetInt("token", 22);
                replacement = Track(catalog.Open<YLCoreSmokePanel>(module, "unique", root, next));
            };
            try
            {
                var old = LoadViewArgs.New();
                old.SetInt("token", 11);
                Assert.Throws<InvalidOperationException>(() => catalog.Open<YLCoreSmokePanel>(module, "unique", root, old));
                Assert.That(replacement, Is.SameAs(first));
                Assert.That(replacement.Token, Is.EqualTo(22));
                Assert.That(updates, Is.Zero);
                Assert.Throws<ObjectDisposedException>(() => old.GetInt("token"));
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        [UnityTest]
        public IEnumerator UniquePendingRootMigrationInvalidatesBeforeDuplicateCommit()
        {
            yield return StartModule();
            var a = PolicyRoot("YLWindow.PendingA");
            var b = PolicyRoot("YLWindow.PendingB");
            var parent = new GameObject("MovingParent", typeof(RectTransform)).transform;
            parent.SetParent(a, false);
            var prefab = Template<YLCoreSmokePanel>("YLWindow.PendingMigration", true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("unique", prefab));
            var pending = catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", parent);
            parent.SetParent(b, false);
            var current = Track(catalog.Open<YLCoreSmokePanel>(module, "unique", b));
            yield return pending;
            Assert.That(pending.Status, Is.EqualTo(LoadViewStatus.Invalidated));
            Assert.That(pending.panel, Is.Null);
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Has.Count.EqualTo(1));
            Assert.That(current.RootCanvas, Is.SameAs(b.GetComponent<Canvas>()));
        }

        [UnityTest]
        public IEnumerator UniqueReopenBringsToFrontUsingModuleOffsetAndTieOrder()
        {
            yield return StartModule();
            var other = new YLCoreBaseProbeModule();
            yield return ModuleManager.StartUpModule(other);
            try
            {
                other.UIStartSortOrder = 200;
                var root = PolicyRoot("YLWindow.OffsetRoot");
                var prefab = Template<YLCoreSmokePanel>("YLWindow.Offset", true);
                var catalog = new WindowCatalog();
                catalog.Register(new WindowDefinition("unique", prefab));
                var first = Track(catalog.Open<YLCoreSmokePanel>(module, "unique", root));
                var second = Track(catalog.Open<YLCoreSmokePanel>(other, "unique", root));
                first.gameObject.SetActive(true);
                second.gameObject.SetActive(true);
                first.SortingOrder = 20;
                second.SortingOrder = 10;
                Assert.That(second.IsTop(), Is.True);
                catalog.Open<YLCoreSmokePanel>(module, "unique", root);
                Assert.That(first.IsTop(), Is.True);
                other.UIStartSortOrder = 0;
                first.SortingOrder = second.SortingOrder = 20;
                Assert.That(second.IsTop(), Is.True);
                catalog.Open<YLCoreSmokePanel>(module, "unique", root);
                Assert.That(first.IsTop(), Is.True);
            }
            finally
            {
                other.AllowClose = true;
                other.Close();
                classPool.Restore(new Module[] { other });
            }
        }

        [UnityTest]
        public IEnumerator TipsHostReactivationDuringExitCannotBorrowFromTheRetiringPool()
        {
            yield return StartModule();
            var presenter = CreateTips(PolicyRoot("YLTips.Reactivation"));
            var handle = presenter.Show(module, "old host", completed: _ =>
            {
                presenter.enabled = true;
                Assert.Throws<InvalidOperationException>(() => presenter.Show(module, "nested host"));
            });
            presenter.enabled = false;
            Assert.That(handle.IsDone, Is.True);
            Assert.That(presenter.ActiveCount, Is.Zero);
            var next = presenter.Show(module, "new host");
            next.Dispose();
            Assert.That(next.IsDone, Is.True);
            Assert.That(presenter.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ReopenCallbackEndingItsSessionDoesNotPublishAClosedWindowAsSuccess()
        {
            yield return StartModule();
            var root = PolicyRoot("YLWindow.UpdateExitRoot");
            var prefab = Template<YLCoreSmokePanel>("YLWindow.UpdateExit", true);
            var catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("unique", prefab));
            var first = Track(catalog.Open<YLCoreSmokePanel>(module, "unique", root));
            YLCoreSmokePanel.UpdateProbe = (panel, _) => panel.Close();
            try
            {
                Assert.Throws<InvalidOperationException>(() => catalog.OpenAsync<YLCoreSmokePanel>(module, "unique", root));
                Assert.That(first.Module, Is.Null);
                Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.Empty);
            }
            finally { YLCoreSmokePanel.ResetProbes(); }
        }

        private Transform PolicyRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            objects.Add(root);
            return root.transform;
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
