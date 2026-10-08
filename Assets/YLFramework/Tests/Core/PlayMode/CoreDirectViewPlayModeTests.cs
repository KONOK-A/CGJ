using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Tests
{
    public sealed class YLCoreDirectViewModule : YLFramework.Core.Module
    {
        public override void OnStart() { }
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
        public IEnumerator MvcLocalQueriesDoNotChangeBaseModuleViewLookup()
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
                Assert.That(module.GetView<YLCoreSmokeView>(prefab.name, view.Id), Is.SameAs(view));
                Assert.That(module.GetView(prefab.name), Is.SameAs(view));
                Assert.That(module.GetView<YLCoreSmokeView>(), Is.SameAs(view));
                Assert.That(module.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { view }));
                Assert.That(view.Module, Is.SameAs(baseModule));
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
            first.Close();
            Assert.That(removed, Is.EqualTo(1));
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
            first.Close();
            Assert.That(module.GetView<YLCoreSmokeView>(prefab.name, first.Id), Is.Null);
            Assert.That(module.GetView<YLCoreSmokeView>(prefab.name, second.Id), Is.SameAs(second));
            Assert.That(second.gameObject.activeSelf, Is.True);
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { second }));
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

        private IEnumerator StartModule()
        {
            var request = ModuleManager.StartUpModule(module);
            var deadline = Time.realtimeSinceStartup + 10;
            while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.isDone, Is.True);
            Assert.That(request.error, Is.Null.Or.Empty);
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
