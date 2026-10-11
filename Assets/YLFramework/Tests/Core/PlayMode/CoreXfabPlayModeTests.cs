#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XFABManager;
using YLFramework.Core.Pool;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Tests
{
    public class CoreXfabPlayModeTests
    {
        private const float Timeout = 10f;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private readonly List<GameObject> ownedObjects = new List<GameObject>();
        private readonly List<Object> ownedAssets = new List<Object>();
        private readonly List<Scene> ownedSceneInstances = new List<Scene>();
        private CoreClassPoolScope classPool;
        private CoreUpdateHostScope updateHosts;
        private readonly HashSet<int> prefabKeys = new HashSet<int>();
        private readonly HashSet<int> previousEventSystems = new HashSet<int>();
        private readonly Dictionary<YLFramework.Core.UIType, Canvas> ownedCanvases =
            new Dictionary<YLFramework.Core.UIType, Canvas>();
        private YLCoreSmokeModule module;
        private YLCoreSmokeRow row;
        private object moduleManager;
        private object previousCurrent;
        private object previousPanelManager;
        private object previousLoader;
        private GameObject previousManagerEventSystem;
        private Scene previousActive;
        private bool ownsScene;
        private bool singleSceneTest;
        private bool initialized;
        private bool timedOut;
        private Action<Canvas> canvasCallback;

        [SetUp]
        public void SetUp()
        {
            initialized = false;
            YLCoreSmokeRow.InitAction = null;
            YLCoreSmokeView.LoadingProbe = null;
            YLCoreSmokeView.ArgsProbe = null;
            YLCoreSmokePanel.ResetProbes();
            timedOut = false;
            ownsScene = singleSceneTest = false;
            ownedObjects.Clear();
            ownedAssets.Clear();
            ownedSceneInstances.Clear();
            classPool = new CoreClassPoolScope();
            updateHosts = new CoreUpdateHostScope();
            prefabKeys.Clear();
            previousEventSystems.Clear();
            ownedCanvases.Clear();
            module = null;
            row = null;
            canvasCallback = null;
            previousActive = SceneManager.GetActiveScene();
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                previousEventSystems.Add(system.gameObject.GetInstanceID());
            previousLoader = Singleton(typeof(GameObjectLoader));
            previousPanelManager = Singleton(typeof(YLFramework.Core.PanelManager));
            previousManagerEventSystem = previousPanelManager == null ? null :
                (GameObject)Field(previousPanelManager, "_eventSystem");
            moduleManager = typeof(YLFramework.Core.ModuleManager).GetProperty("Instance", StaticPrivate).GetValue(null);
            previousCurrent = Field(moduleManager, "current_module");
            initialized = true;

            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.Null,
                "Never replace an externally registered fixture module.");
            Assert.That(SceneManager.GetSceneByPath(YLCoreSmokeModule.ScenePath).isLoaded, Is.False,
                "The fixture scene must not already be loaded.");
            var profile = AssetBundleManager.GetProfile(YLCoreSmokeModule.Project);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.loadMode, Is.EqualTo(LoadMode.Assets), "These tests do not alter the selected XFAB Profile.");
            Assert.That(profile.updateModel, Is.EqualTo(UpdateMode.LOCAL));
            var project = AssetDatabase.LoadAssetAtPath<XFABProject>(YLCoreSmokeModule.FixtureRoot + "/YLCoreSmokeModule.asset");
            Assert.That(project, Is.Not.Null, "Run CoreSmokeAssetFixture.CreateOrValidate in Edit Mode first.");
            Assert.That(project.name, Is.EqualTo(YLCoreSmokeModule.Project));
            Assert.That(XFABProjectManager.Instance.GetProject(YLCoreSmokeModule.Project), Is.SameAs(project),
                "Create the fixture before entering Play Mode; do not refresh global project caches during a test.");
            foreach (string name in new[] { nameof(YLCoreSmokeView), nameof(YLCoreSmokePanel) })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(YLCoreSmokeModule.FixtureRoot + "/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                int key = prefab.GetHashCode();
                if (previousLoader != null)
                    Assert.That(((IDictionary)Field(previousLoader, "allPools")).Contains(key), Is.False,
                        "Fixture prefab already has a pool owned by another caller: " + name);
                prefabKeys.Add(key);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!initialized) yield break;
            YLCoreSmokeRow.InitAction = null;
            YLCoreSmokeView.LoadingProbe = null;
            YLCoreSmokeView.ArgsProbe = null;
            YLCoreSmokePanel.ResetProbes();
            // A timeout exits this isolated PlayMode session. Producers are not cancellable by Abort.
            if (timedOut) yield break;
            var failures = new List<Exception>();
            void Attempt(Action action)
            {
                try { action(); }
                catch (Exception error) { failures.Add(error); }
            }
            // Include cached instances created by a failed type-check request, not just returned Views.
            var loader = Singleton(typeof(GameObjectLoader));
            Attempt(() =>
            {
                if (loader == null) return;
                var pools = (IDictionary)Field(loader, "allPools");
                foreach (int key in prefabKeys)
                {
                    if (!pools.Contains(key)) continue;
                    foreach (object info in ((IDictionary)Field(pools[key], "allObjs")).Values)
                        Own((GameObject)info.GetType().GetProperty("Obj").GetValue(info));
                }
            });
            Attempt(() =>
            {
                if (module == null || !ReferenceEquals(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), module)) return;
                row = module.GetModel<YLCoreSmokeRow>(101);
                module.Close();
            });
            Attempt(() => typeof(YLFramework.Core.ModuleManager).GetField("current_module", InstancePrivate).SetValue(moduleManager, previousCurrent));
            if (canvasCallback != null) YLFramework.Core.PanelManager.onCreateCanvas -= canvasCallback;
            Attempt(CleanPanelManager);
            Attempt(() =>
            {
                if (loader == null) return;
                var pools = (IDictionary)Field(loader, "allPools");
                var mappings = (IDictionary)Field(loader, "allObjPoolMapping");
                var keys = new List<object>();
                foreach (DictionaryEntry mapping in mappings)
                    if (prefabKeys.Contains((int)mapping.Value)) keys.Add(mapping.Key);
                foreach (object key in keys) mappings.Remove(key);
                foreach (int key in prefabKeys) pools.Remove(key);
            });
            Attempt(() => classPool.Restore(new Module[] { module }));
            Attempt(updateHosts.ReleaseOwnedIdleHosts);
            foreach (var obj in ownedObjects) if (obj) Attempt(() => Object.Destroy(obj));
            foreach (var asset in ownedAssets) if (asset) Attempt(() => AssetBundleManager.UnloadAsset(asset));

            if (singleSceneTest && (!previousActive.IsValid() || !previousActive.isLoaded))
            {
                // Single is opt-in and only accepts this managed empty harness as the original scene.
                yield return AwaitUnity(EditorSceneManager.LoadSceneAsyncInPlayMode(YLCoreSmokeModule.HarnessPath,
                    new LoadSceneParameters(LoadSceneMode.Additive)), "restore fixture harness");
                previousActive = SceneManager.GetSceneByPath(YLCoreSmokeModule.HarnessPath);
            }
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            var scene = SceneManager.GetSceneByPath(YLCoreSmokeModule.ScenePath);
            if (ownsScene && scene.IsValid() && scene.isLoaded)
                yield return AwaitUnity(SceneManager.UnloadSceneAsync(scene), "unload fixture scene");
            foreach (var instance in ownedSceneInstances)
                if (instance.IsValid() && instance.isLoaded)
                    yield return AwaitUnity(SceneManager.UnloadSceneAsync(instance), "unload owned additive scene instance");
            yield return null;
            initialized = false;
            if (failures.Count > 0) throw new AggregateException("XFAB fixture cleanup failed.", failures);
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.Null);
            foreach (var obj in ownedObjects) Assert.That(obj == null, Is.True, "Fixture object survived cleanup.");

            // Framework hosts run shared pool/resource maintenance coroutines, even when first
            // created by this test. Keep those services alive; do not orphan or duplicate their checkers.
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedFixtureSessionsRemoveOwnedPoolsWithoutDestroyingMaintenanceHosts()
        {
            for (var cycle = 0; cycle < 2; cycle++)
            {
                yield return NamedPanelsUseRealManagerCanvasEventSystemAsyncAndCacheIndexes();
                var backendHost = Object.FindObjectsByType<XFABManager.CoroutineStarter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var foundationHost = Object.FindObjectsByType<YLFramework.Foundation.CoroutineStarter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                yield return TearDown();
                CollectionAssert.AreEquivalent(backendHost,
                    Object.FindObjectsByType<XFABManager.CoroutineStarter>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                CollectionAssert.AreEquivalent(foundationHost,
                    Object.FindObjectsByType<YLFramework.Foundation.CoroutineStarter>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                var loader = Singleton(typeof(GameObjectLoader));
                var pools = (IDictionary)Field(loader, "allPools");
                foreach (var key in prefabKeys) Assert.That(pools.Contains(key), Is.False);
                if (cycle == 0) SetUp();
            }
        }

        [UnityTest]
        public IEnumerator StartupDeserializesRealJsonIntoOwnedCoreModel()
        {
            module = new YLCoreSmokeModule();
            var startup = YLFramework.Core.ModuleManager.StartUpModule(module);
            Assert.That(startup.isDone, Is.False, "The real table request yields during startup.");
            Assert.That(module.State, Is.EqualTo(ModuleState.Starting));
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.SameAs(module));
            Assert.That(YLFramework.Core.ModuleManager.StartUpModule(module), Is.SameAs(startup));
            Assert.That(YLFramework.Core.ModuleManager.StartUpModule<YLCoreSmokeModule>(), Is.SameAs(startup));
            var rejected = new YLCoreSmokeModule();
            Assert.Throws<InvalidOperationException>(() => YLFramework.Core.ModuleManager.StartUpModule(rejected));
            Assert.Throws<InvalidOperationException>(() => module.Close());
            YLFramework.Foundation.CustomAsyncOperation<StartUpModuleRequest> foundation = startup;
            Assert.Throws<NotSupportedException>(() => foundation.Abort());
            Assert.That(startup.isDone, Is.False);
            var current = Field(moduleManager, "current_module");
            Assert.That(current, Is.SameAs(module));
            int callbacks = 0;
            startup.AddCompleteEvent(done =>
            {
                callbacks++;
                Assert.That(module.State, Is.EqualTo(ModuleState.Running));
                var repeated = YLFramework.Core.ModuleManager.StartUpModule(module);
                Assert.That(repeated.isDone, Is.True);
                Assert.That(repeated, Is.Not.SameAs(done));
                Assert.That(repeated.error, Is.Null.Or.Empty);
                Assert.That(Field(moduleManager, "current_module"), Is.SameAs(current));
            });
            startup.AddCompleteEvent(_ => throw new InvalidOperationException("YLCore completion subscriber failure"));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: YLCore completion subscriber failure"));
            yield return Await(() => startup.isDone, "Core startup with real table and duplicate request");
            Assert.That(startup.error, Is.Null.Or.Empty, "Subscriber failure is not startup failure.");
            Assert.That(module.State, Is.EqualTo(ModuleState.Running));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(rejected.State, Is.EqualTo(ModuleState.Created));
            Assert.That(rejected.StartCount, Is.Zero);
            Assert.That(module.ProjectName, Is.EqualTo(YLCoreSmokeModule.Project));
            Assert.That(module.RowReadyAtStart, Is.True);
            Assert.That(module.StartCount, Is.EqualTo(1));
            row = module.GetModel<YLCoreSmokeRow>(101);
            Assert.That(row, Is.InstanceOf<YLFramework.Core.Model>());
            Assert.That(row.Marker, Is.EqualTo("yl-smoke"));
            Assert.That(row.Module, Is.SameAs(module));
            Assert.That(row.InitCount, Is.EqualTo(1));
            Assert.That(module.GetModels<YLCoreSmokeRow>(), Is.EqualTo(new[] { row }));
            module.Close();
            Assert.That(row.ExitCount, Is.EqualTo(1));
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator StartupDistinguishesMissingBlankInvalidAndEmptyArrayTables()
        {
            foreach (string name in new[] { string.Empty, "YLCoreMissingRows", YLCoreSmokeModule.BlankTable, YLCoreSmokeModule.InvalidTable, YLCoreSmokeModule.EmptyTable })
            {
                module = new YLCoreSmokeModule { TableName = name };
                Assert.That(module.ProjectName, Is.EqualTo(YLCoreSmokeModule.Project), "Default identity exists before any resource request.");
                var request = YLFramework.Core.ModuleManager.StartUpModule(module);
                int callbacks = 0;
                request.AddCompleteEvent(done =>
                {
                    callbacks++;
                    Assert.That(module.State, Is.EqualTo(name == YLCoreSmokeModule.EmptyTable ? ModuleState.Running : ModuleState.Closed));
                    if (name != YLCoreSmokeModule.EmptyTable)
                        Assert.That(YLFramework.Core.ModuleManager.GetModule(module.ModuleName), Is.Null);
                });
                yield return Await(() => request.isDone, "Core startup table " + name);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(module.GetModels<Model>(), Is.Empty);
                if (name == YLCoreSmokeModule.EmptyTable)
                {
                    Assert.That(request.error, Is.Null.Or.Empty);
                    Assert.That(request.Exception, Is.Null);
                    Assert.That(module.StartCount, Is.EqualTo(1));
                    module.Close();
                }
                else
                {
                    Assert.That(request.error, Is.Not.Null.And.Not.Empty);
                    Assert.That(request.Exception, Is.Not.Null);
                    Assert.That(module.StartCount, Is.Zero);
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BaseStartupUsesItsExplicitResourceProjectInsteadOfCurrentModule()
        {
            var current = new YLCoreConsumerModule { ProjectName = "YLCoreUnrelatedCurrentProject" };
            var baseModule = new YLCoreResourceProjectModule { ProjectName = YLCoreSmokeModule.Project };
            try
            {
                Assert.That(YLFramework.Core.ModuleManager.StartUpModule(current).error, Is.Null.Or.Empty);
                var startup = YLFramework.Core.ModuleManager.StartUpModule(baseModule);
                yield return Await(() => startup.isDone, "base module explicit resource project");
                Assert.That(startup.error, Is.Null.Or.Empty);
                Assert.That(baseModule.State, Is.EqualTo(ModuleState.Running));
                Assert.That(baseModule.ProjectName, Is.EqualTo(YLCoreSmokeModule.Project));
                Assert.That(baseModule.ModuleName, Is.EqualTo(typeof(YLCoreResourceProjectModule).FullName));
                var loaded = baseModule.GetModel<YLCoreSmokeRow>(101);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.Module, Is.SameAs(baseModule));
                Assert.That(YLFramework.Core.ModuleManager.AllBaseModules[baseModule.ModuleName], Is.SameAs(baseModule));
                Assert.That(current.GetModels<Model>(), Is.Empty);
            }
            finally
            {
                // 仅fixture结束自己的基础模块，不对全工程调用Shutdown。
                baseModule.AllowClose = true;
                baseModule.Close();
                current.Close();
                classPool.Restore(new Module[] { baseModule, current });
            }
        }

        [UnityTest]
        public IEnumerator TemporaryTableCacheEntryIsReturnedBeforeModelInitializationAndOnInvalidJson()
        {
            // Assets模式不建立AB缓存；仅为自己的TextAsset登记可控条目，验证归还调用而非AB物理卸载。
            var addCache = typeof(AssetBundleManager).GetMethod("AddAssetCache", StaticPrivate, null,
                new[] { typeof(string), typeof(string), typeof(object) }, null);
            Assert.That(addCache, Is.Not.Null);
            var backend = Singleton(typeof(AssetBundleManager));
            var hashes = (IDictionary)Field(backend, "asset_hash_project_name");
            foreach (string name in new[] { YLCoreSmokeModule.Table, YLCoreSmokeModule.EmptyTable, YLCoreSmokeModule.BlankTable, YLCoreSmokeModule.InvalidTable })
            {
                var text = AssetDatabase.LoadAssetAtPath<TextAsset>(YLCoreSmokeModule.FixtureRoot + "/" + name + ".json");
                Assert.That(text, Is.Not.Null);
                Assert.That(hashes.Contains(text.GetHashCode()), Is.False, "Never assume ownership of an external cache entry.");
                bool projectExisted = AssetBundleManager.BundleLoadedAssets.TryGetValue(YLCoreSmokeModule.Project, out var buckets);
                bool bundleExisted = projectExisted && buckets.ContainsKey("text");
                var sentinel = new TextAsset("owned cache sentinel");
                try
                {
                    // sentinel只用于避免最后条目触发真实Bundle卸载；不是外部资源持有者。
                    addCache.Invoke(null, new object[] { YLCoreSmokeModule.Project, "text", sentinel });
                    addCache.Invoke(null, new object[] { YLCoreSmokeModule.Project, "text", text });
                    bool returnedAtInit = false;
                    YLCoreSmokeRow.InitAction = _ => returnedAtInit = !hashes.Contains(text.GetHashCode());
                    module = new YLCoreSmokeModule { TableName = name };
                    var request = YLFramework.Core.ModuleManager.StartUpModule(module);
                    yield return Await(() => request.isDone, "temporary table cache return " + name);
                    Assert.That(hashes.Contains(text.GetHashCode()), Is.False);
                    Assert.That(AssetBundleManager.BundleLoadedAssets[YLCoreSmokeModule.Project]["text"].ContainsKey(text.GetHashCode()), Is.False);
                    Assert.That(hashes.Contains(sentinel.GetHashCode()), Is.True, "Return only the obtained table, not unrelated entries.");
                    if (name == YLCoreSmokeModule.Table)
                        Assert.That(returnedAtInit, Is.True, "Model initialization occurs after asset return.");
                    if (name == YLCoreSmokeModule.Table || name == YLCoreSmokeModule.EmptyTable)
                        Assert.That(request.error, Is.Null.Or.Empty);
                    else Assert.That(request.error, Is.Not.Null.And.Not.Empty);
                }
                finally
                {
                    YLCoreSmokeRow.InitAction = null;
                    module?.Close();
                    hashes.Remove(text.GetHashCode());
                    hashes.Remove(sentinel.GetHashCode());
                    var ownBucket = AssetBundleManager.BundleLoadedAssets[YLCoreSmokeModule.Project]["text"];
                    ownBucket.Remove(text.GetHashCode());
                    ownBucket.Remove(sentinel.GetHashCode());
                    if (!bundleExisted && ownBucket.Count == 0) AssetBundleManager.BundleLoadedAssets[YLCoreSmokeModule.Project].Remove("text");
                    if (!projectExisted && AssetBundleManager.BundleLoadedAssets[YLCoreSmokeModule.Project].Count == 0)
                        AssetBundleManager.BundleLoadedAssets.Remove(YLCoreSmokeModule.Project);
                    Object.Destroy(sentinel);
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ReportedDefaultSceneFailureRollsBackInitializedModelsWithoutChangingScenes()
        {
            var before = new List<int>();
            for (int i = 0; i < SceneManager.sceneCount; i++) before.Add(SceneManager.GetSceneAt(i).handle);
            module = new YLCoreSmokeModule { SceneOverride = "YLCoreMissingScene_" + Guid.NewGuid().ToString("N") };
            YLCoreSmokeRow.InitAction = value => row = value;
            var startup = YLFramework.Core.ModuleManager.StartUpModule(module);
            yield return Await(() => startup.isDone, "reported default scene failure");
            YLCoreSmokeRow.InitAction = null;
            Assert.That(row, Is.Not.Null);
            Assert.That(startup.error, Does.Contain("Default scene load failed"));
            Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
            Assert.That(module.StartCount, Is.Zero);
            Assert.That(YLFramework.Core.ModuleManager.GetModule(module.ModuleName), Is.Null);
            Assert.That(row.Module, Is.Null);
            Assert.That(row.ExitCount, Is.EqualTo(1));
            var after = new List<int>();
            for (int i = 0; i < SceneManager.sceneCount; i++) after.Add(SceneManager.GetSceneAt(i).handle);
            Assert.That(after, Is.EqualTo(before));
            module.SceneOverride = null;
            var restarted = YLFramework.Core.ModuleManager.StartUpModule(module);
            yield return Await(() => restarted.isDone, "restart after scene failure");
            Assert.That(restarted.error, Is.Null.Or.Empty);
            Assert.That(module.State, Is.EqualTo(ModuleState.Running));
        }

        [UnityTest]
        public IEnumerator XfabLoadsRealPrefabAndTextSynchronouslyAndAsynchronously()
        {
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            Assert.That(prefab.GetComponent<YLCoreSmokeView>(), Is.Not.Null);
            var text = LoadFixture<TextAsset>(YLCoreSmokeModule.Table);
            Assert.That(text.text, Is.EqualTo(YLCoreSmokeModule.Json));
            foreach (string name in new[] { nameof(YLCoreSmokeView), YLCoreSmokeModule.Table })
            {
                var request = name == YLCoreSmokeModule.Table
                    ? AssetBundleManager.LoadAssetAsync<TextAsset>(YLCoreSmokeModule.Project, name)
                    : AssetBundleManager.LoadAssetAsync<GameObject>(YLCoreSmokeModule.Project, name);
                int callbacks = 0;
                bool callbackComplete = false;
                request.AddCompleteEvent(done =>
                {
                    callbacks++;
                    callbackComplete = done.isDone && !done.keepWaiting && done.progress == 1f && string.IsNullOrEmpty(done.error);
                });
                yield return Await(() => request.isDone, "XFAB LoadAssetAsync " + name);
                Assert.That(request.error, Is.Null.Or.Empty);
                Assert.That(request.asset, Is.SameAs(name == YLCoreSmokeModule.Table ? (Object)text : prefab));
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(callbackComplete, Is.True);
                OwnAsset(request.asset);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator NamedViewLoadAndAsyncInitializeArgsCallbacksIndexesAndReuseCache()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var parent = Own(new GameObject("YLCoreSmoke.ViewParent")).transform;
            var args = NewArgs();
            args.SetInt("token", 41);
            var view = Track(module.LoadView<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent, loadViewArgs: args));
            AssertView(view, parent, 41, 1);
            YLFramework.Core.EventManager.TriggerEvent(YLCoreSmokeView.ProbeEvent);
            Assert.That(view.EventCount, Is.EqualTo(1));
            view.Close();
            AssertRemoved(view, nameof(YLCoreSmokeView));
            var nextArgs = NewArgs();
            nextArgs.SetInt("token", 73);
            var request = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent, loadViewArgs: nextArgs);
            int callbacks = 0;
            bool completeState = false;
            request.AddCompleteEvent(done =>
            {
                callbacks++;
                completeState = done.isDone && !done.keepWaiting && done.progress == 1f && string.IsNullOrEmpty(done.error);
                Track(done.view);
            });
            yield return Await(() => request.isDone, "Core named LoadViewAsync");
            Assert.That(request.error, Is.Null.Or.Empty);
            Assert.That(request.view, Is.SameAs(view));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(completeState, Is.True);
            AssertView(request.view, parent, 73, 2);
            int late = 0;
            request.AddCompleteEvent(_ => late++);
            Assert.That(late, Is.EqualTo(1));
            yield return null;
            Assert.That(view.LoadedCount, Is.EqualTo(2), "View.Start must not initialize a loader-owned view again.");
            module.Close();
            Assert.That(view.RemovedCount, Is.EqualTo(2));
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ParallelNamedViewsCancelLateProducerWithoutInstanceOrRecycledArgsLeak()
        {
            yield return StartModule();
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var parent = Own(new GameObject("YLCore.AsyncParallelParent")).transform;
            var firstArgs = NewArgs();
            firstArgs.SetInt("token", 11);
            var first = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent, loadViewArgs: firstArgs);
            var canceledArgs = NewArgs();
            canceledArgs.SetObject("retained", module);
            var canceled = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent, loadViewArgs: canceledArgs);
            Assert.That(first.Status, Is.EqualTo(LoadViewStatus.Pending));
            Assert.That(canceled.Status, Is.EqualTo(LoadViewStatus.Pending));
            Assert.That(PendingViewCount(), Is.EqualTo(2));
            Assert.That(FixtureInstanceCount(prefab), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => canceledArgs.GetObject("retained"));
            Assert.Throws<InvalidOperationException>(() => module.LoadViewAsync<YLCoreSmokeView>(
                nameof(YLCoreSmokeView), parent, loadViewArgs: canceledArgs));
            Assert.That(PendingViewCount(), Is.EqualTo(2), "Duplicate Claim cannot disturb the original pending request.");
            int canceledCallbacks = 0;
            canceled.AddCompleteEvent(_ => canceledCallbacks++);
            YLFramework.Foundation.CustomAsyncOperation<LoadViewRequest<YLCoreSmokeView>> foundation = canceled;
            foundation.Abort();
            canceled.Abort();
            AssertTerminal(canceled, LoadViewStatus.Canceled);
            Assert.That(canceled.Exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(canceledCallbacks, Is.EqualTo(1));
            Assert.That(PendingViewCount(), Is.EqualTo(1));
            AssertReleasedArgs(canceledArgs);

            // 立即重新租用真实池对象，旧producer仍在等待同一资源，不能读取或归还新租用。
            var secondArgs = NewArgs();
            Assert.That(secondArgs, Is.SameAs(canceledArgs));
            secondArgs.SetInt("token", 73);
            secondArgs.SetObject("retained", parent);
            var observedTokens = new List<int>();
            YLCoreSmokeView.ArgsProbe = (view, args) => observedTokens.Add(args.GetInt("token"));
            var second = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent, loadViewArgs: secondArgs);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(PendingViewCount(), Is.EqualTo(2));
            yield return Await(() => first.isDone && second.isDone, "parallel named Views after cancellation");
            yield return null;
            AssertTerminal(first, LoadViewStatus.Succeeded);
            AssertTerminal(second, LoadViewStatus.Succeeded);
            AssertTerminal(canceled, LoadViewStatus.Canceled);
            var firstView = Track(first.view);
            var secondView = Track(second.view);
            Assert.That(firstView, Is.Not.SameAs(secondView));
            Assert.That(firstView.Token, Is.EqualTo(11));
            Assert.That(secondView.Token, Is.EqualTo(73));
            CollectionAssert.AreEquivalent(new[] { 11, 73 }, observedTokens);
            CollectionAssert.AreEquivalent(new[] { firstView, secondView }, module.GetViews<YLCoreSmokeView>());
            Assert.That(firstView.Module, Is.SameAs(module));
            Assert.That(secondView.Module, Is.SameAs(module));
            Assert.That(firstView.transform.parent, Is.SameAs(parent));
            Assert.That(secondView.transform.parent, Is.SameAs(parent));
            Assert.That(FixtureInstanceCount(prefab), Is.EqualTo(2), "Canceled producer must never acquire an instance.");
            Assert.That(canceledCallbacks, Is.EqualTo(1));
            Assert.That(PendingViewCount(), Is.Zero);
            AssertReleasedArgs(firstArgs);
            AssertReleasedArgs(secondArgs);
        }

        [UnityTest]
        public IEnumerator CloseInvalidatesPendingBeforeNotificationAndAllowsSameInstanceRestart()
        {
            yield return StartModule();
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var oldView = Track(module.LoadView<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: null));
            var oldRow = row;
            var args = NewArgs();
            args.SetObject("retained", module);
            var request = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
            var oldLifecycle = typeof(Module).GetField("ViewLifecycle", InstancePrivate).GetValue(module);
            Assert.That(request.Status, Is.EqualTo(LoadViewStatus.Pending));
            bool inputsReleasedBeforeViewExit = false;
            oldView.RemoveAction = () =>
            {
                inputsReleasedBeforeViewExit = module.State == ModuleState.Closing && !request.isDone &&
                    PendingViewCount() == 0 && Field(request, "owner") == null && Field(request, "args") == null;
                AssertReleasedArgs(args);
            };
            StartUpModuleRequest restart = null;
            LoadViewRequest<YLCoreSmokeView> fresh = null;
            int callbacks = 0;
            request.AddCompleteEvent(done =>
            {
                callbacks++;
                AssertTerminal(done, LoadViewStatus.Invalidated);
                Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
                Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.Null);
                Assert.That(oldRow.Module, Is.Null);
                Assert.That(oldRow.ExitCount, Is.EqualTo(1));
                Assert.That(PendingViewCount(), Is.Zero);
                AssertReleasedArgs(args);
                module.StartAction = () => fresh = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: null);
                restart = YLFramework.Core.ModuleManager.StartUpModule(module);
            });
            module.Close();
            Assert.That(inputsReleasedBeforeViewExit, Is.True, "Close releases pending inputs before running View cleanup.");
            oldView.RemoveAction = null;
            Assert.That(callbacks, Is.EqualTo(1), "Invalidation publishes synchronously after unregistering the owner.");
            Assert.That(restart, Is.Not.Null);
            yield return Await(() => restart.isDone && fresh != null && fresh.isDone, "restart same Module after pending invalidation");
            Assert.That(restart.error, Is.Null.Or.Empty);
            Assert.That(typeof(Module).GetField("ViewLifecycle", InstancePrivate).GetValue(module), Is.Not.SameAs(oldLifecycle));
            AssertTerminal(request, LoadViewStatus.Invalidated);
            AssertTerminal(fresh, LoadViewStatus.Succeeded);
            var view = Track(fresh.view);
            Assert.That(view, Is.SameAs(oldView));
            Assert.That(view.Module, Is.SameAs(module));
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { view }));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(PendingViewCount(), Is.Zero);
            Assert.That(FixtureInstanceCount(prefab), Is.EqualTo(1), "Old invalidated producer cannot create an extra instance in the restarted owner.");
        }

        [UnityTest]
        public IEnumerator OnStartPendingViewIsInvalidatedAfterStartupThrowRollback()
        {
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var args = NewArgs();
            var failure = new InvalidOperationException("YLCore OnStart pending View rollback");
            LoadViewRequest<YLCoreSmokeView> viewRequest = null;
            StartUpModuleRequest restart = null;
            int callbacks = 0;
            module = new YLCoreSmokeModule();
            args.SetObject("retained", module);
            module.StartAction = () =>
            {
                row = module.GetModel<YLCoreSmokeRow>(101);
                viewRequest = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
                Assert.That(viewRequest.Status, Is.EqualTo(LoadViewStatus.Pending));
                viewRequest.AddCompleteEvent(done =>
                {
                    callbacks++;
                    AssertTerminal(done, LoadViewStatus.Invalidated);
                    Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
                    Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.Null);
                    Assert.That(row.Module, Is.Null);
                    Assert.That(row.ExitCount, Is.EqualTo(1));
                    Assert.That(module.GetViews<View>(), Is.Empty);
                    Assert.That(PendingViewCount(), Is.Zero);
                    AssertReleasedArgs(args);
                    module.StartAction = null;
                    restart = YLFramework.Core.ModuleManager.StartUpModule(module);
                });
                throw failure;
            };
            var startup = YLFramework.Core.ModuleManager.StartUpModule(module);
            yield return Await(() => startup.isDone, "OnStart pending View rollback");
            Assert.That(startup.Exception, Is.SameAs(failure));
            Assert.That(callbacks, Is.EqualTo(1));
            AssertTerminal(viewRequest, LoadViewStatus.Invalidated);
            Assert.That(restart, Is.Not.Null, "Startup rollback removes its old startup record before pending callbacks can restart.");
            yield return Await(() => restart.isDone, "restart from startup rollback pending callback");
            Assert.That(restart.error, Is.Null.Or.Empty);
            Assert.That(module.State, Is.EqualTo(ModuleState.Running));
            yield return DrainFixtureAssetProducer();
            Assert.That(FixtureInstanceCount(prefab), Is.Zero);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PendingParentDestructionOrSessionEndInvalidatesButLiveSceneMigrationSucceeds()
        {
            yield return StartModule();
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var parent = Own(new GameObject("YLCore.PendingDestroyedParent"));
            var args = NewArgs();
            args.SetObject("retained", parent);
            var destroyed = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent.transform, loadViewArgs: args);
            Assert.That(destroyed.Status, Is.EqualTo(LoadViewStatus.Pending));
            Object.DestroyImmediate(parent);
            yield return Await(() => destroyed.isDone, "pending parent destruction");
            AssertTerminal(destroyed, LoadViewStatus.Invalidated);
            AssertReleasedArgs(args);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(FixtureInstanceCount(prefab), Is.Zero);

            var parentObject = Own(new GameObject("YLCore.PendingParentSession"));
            parentObject.SetActive(false);
            var parentView = parentObject.AddComponent<YLCoreSmokeView>();
            module.AttachView(parentView);
            var ended = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), parentView.transform, loadViewArgs: null);
            parentView.Close();
            module.AttachView(parentView);
            yield return Await(() => ended.isDone, "pending parent session replaced on live object");
            AssertTerminal(ended, LoadViewStatus.Invalidated);
            Assert.That(parentView.Module, Is.SameAs(module));
            Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { parentView }));
            Assert.That(FixtureInstanceCount(prefab), Is.Zero);
            parentView.Close();

            var destination = SceneManager.CreateScene("YLCore.PendingParentDestination");
            ownedSceneInstances.Add(destination);
            var moved = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), parentObject.transform, loadViewArgs: null);
            SceneManager.MoveGameObjectToScene(parentObject, destination);
            yield return Await(() => moved.isDone, "pending live parent scene migration");
            AssertTerminal(moved, LoadViewStatus.Succeeded);
            var view = Track(moved.view);
            Assert.That(view.transform.parent, Is.SameAs(parentObject.transform));
            Assert.That(view.gameObject.scene, Is.EqualTo(destination));
            Assert.That(view.Module, Is.SameAs(module));
            Assert.That(PendingViewCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ParentlessPendingViewKeepsCapturedSceneAndInvalidatesWhenItUnloads()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var target = SceneManager.CreateScene("YLCore.PendingCapturedScene");
            ownedSceneInstances.Add(target);
            SceneManager.SetActiveScene(target);
            var request = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: null);
            Assert.That(request.Status, Is.EqualTo(LoadViewStatus.Pending));
            SceneManager.SetActiveScene(previousActive);
            yield return Await(() => request.isDone, "parentless captured scene after active switch");
            AssertTerminal(request, LoadViewStatus.Succeeded);
            var view = Track(request.view);
            Assert.That(view.gameObject.scene, Is.EqualTo(target));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(previousActive));
            view.Close();

            var unloading = SceneManager.CreateScene("YLCore.PendingUnloadedScene");
            ownedSceneInstances.Add(unloading);
            SceneManager.SetActiveScene(unloading);
            var args = NewArgs();
            args.SetObject("retained", module);
            var invalidated = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
            Assert.That(invalidated.Status, Is.EqualTo(LoadViewStatus.Pending));
            SceneManager.SetActiveScene(previousActive);
            var unload = SceneManager.UnloadSceneAsync(unloading);
            yield return AwaitUnity(unload, "unload pending captured scene");
            yield return Await(() => invalidated.isDone, "pending captured scene invalidation");
            AssertTerminal(invalidated, LoadViewStatus.Invalidated);
            AssertReleasedArgs(args);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(PendingViewCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator RealMissingAndArgsInitializationFailureCompleteWithoutOwnerOrInputResidue()
        {
            yield return StartModule();
            var args = NewArgs();
            args.SetObject("retained", module);
            var missing = module.LoadViewAsync<YLCoreSmokeView>("YLCoreMissingView_" + Guid.NewGuid().ToString("N"), loadViewArgs: args);
            int callbacks = 0;
            missing.AddCompleteEvent(_ => callbacks++);
            yield return Await(() => missing.isDone, "real missing named prefab");
            AssertTerminal(missing, LoadViewStatus.Failed);
            Assert.That(missing.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(callbacks, Is.EqualTo(1));
            AssertReleasedArgs(args);
            Assert.That(module.GetViews<View>(), Is.Empty);

            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var failure = new InvalidOperationException("YLCore async args initialization failure");
            var cleanupFailure = new InvalidOperationException("YLCore async View rollback cleanup failure");
            YLCoreSmokeView failedView = null;
            LoadViewRequest<YLCoreSmokeView> failed = null;
            args = NewArgs();
            args.SetObject("retained", row);
            YLCoreSmokeView.ArgsProbe = (view, borrowed) =>
            {
                failedView = Track(view);
                view.BoundModel = (Model)borrowed.GetObject("retained");
                view.RemoveAction = () =>
                {
                    failed.Abort();
                    throw cleanupFailure;
                };
                throw failure;
            };
            failed = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
            callbacks = 0;
            failed.AddCompleteEvent(_ => callbacks++);
            yield return Await(() => failed.isDone, "real async hierarchy initialization failure");
            AssertTerminal(failed, LoadViewStatus.Failed);
            Assert.That(failed.Exception, Is.SameAs(failure));
            Assert.That(failed.Exception.Data["YLFramework.Core.CleanupException"], Is.SameAs(cleanupFailure));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(failedView, Is.Not.Null);
            Assert.That(failedView.Module, Is.Null);
            Assert.That(failedView.Model, Is.Null);
            Assert.That(failedView.RemovedCount, Is.EqualTo(1));
            Assert.That(failedView.ClosedCount, Is.Zero);
            Assert.That(row.Module, Is.SameAs(module));
            Assert.That(row.ExitCount, Is.Zero);
            AssertReleasedArgs(args);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(PendingViewCount(), Is.Zero);
            yield return null;
            Assert.That(failedView == null, Is.True, "Failed initialization discards the obtained instance.");
        }

        [UnityTest]
        public IEnumerator ArgsCallbackAbortDefersNotificationUntilRollbackThenAllowsReentrantLoad()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var args = NewArgs();
            args.SetInt("token", 61);
            args.SetObject("retained", row);
            LoadViewRequest<YLCoreSmokeView> request = null;
            LoadViewRequest<YLCoreSmokeView> next = null;
            YLCoreSmokeView canceledView = null;
            bool stillBorrowedAfterAbort = false;
            YLCoreSmokeView.ArgsProbe = (view, borrowed) =>
            {
                canceledView = Track(view);
                view.BoundModel = (Model)borrowed.GetObject("retained");
                request.Abort();
                request.Abort();
                stillBorrowedAfterAbort = !request.isDone && request.Status == LoadViewStatus.Pending &&
                    borrowed.GetInt("token") == 61 && PendingViewCount() == 1;
                YLCoreSmokeView.ArgsProbe = null;
            };
            request = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
            Assert.That(request.Status, Is.EqualTo(LoadViewStatus.Pending));
            int callbacks = 0;
            request.AddCompleteEvent(done =>
            {
                callbacks++;
                AssertTerminal(done, LoadViewStatus.Canceled);
                Assert.That(canceledView.Module, Is.Null);
                Assert.That(canceledView.Model, Is.Null);
                Assert.That(canceledView.IsLoaded, Is.False);
                Assert.That(canceledView.RemovedCount, Is.EqualTo(1));
                Assert.That(canceledView.ClosedCount, Is.Zero);
                Assert.That(module.GetViews<View>(), Is.Empty);
                Assert.That(PendingViewCount(), Is.Zero);
                AssertReleasedArgs(args);
                done.Abort();
                var nextArgs = NewArgs();
                nextArgs.SetInt("token", 62);
                next = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: nextArgs);
            });
            yield return Await(() => request.isDone, "args Abort rollback");
            Assert.That(next, Is.Not.Null, "Completion callback starts a new request after the rollback transaction ends.");
            yield return Await(() => next.isDone, "reentrant named load after args Abort");
            Assert.That(stillBorrowedAfterAbort, Is.True);
            Assert.That(callbacks, Is.EqualTo(1));
            AssertTerminal(request, LoadViewStatus.Canceled);
            Assert.That(request.Exception, Is.TypeOf<OperationCanceledException>());
            AssertTerminal(next, LoadViewStatus.Succeeded);
            var nextView = Track(next.view);
            Assert.That(nextView.Token, Is.EqualTo(62));
            Assert.That(nextView.Module, Is.SameAs(module));
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { nextView }));
            Assert.That(PendingViewCount(), Is.Zero);
            Assert.That(row.ExitCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ArgsModelRemovedWhilePendingOrReregisteredDuringInitializationCannotCommitBinding()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var model = module.LoadModel<YLCoreProbeModel>();
            for (int phase = 0; phase < 2; phase++)
            {
                var args = NewArgs();
                args.SetObject("retained", model);
                YLCoreSmokeView failedView = null;
                YLCoreSmokeView.ArgsProbe = (view, borrowed) =>
                {
                    failedView = Track(view);
                    view.BoundModel = (Model)borrowed.GetObject("retained");
                    if (phase == 1)
                    {
                        module.RemoveModel(model);
                        module.AddModel(model);
                        Assert.That(view.Model, Is.Null, "A new registration cannot revive the old binding token.");
                    }
                };
                var request = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
                Assert.That(request.Status, Is.EqualTo(LoadViewStatus.Pending));
                if (phase == 0) module.RemoveModel(model);
                yield return Await(() => request.isDone, "invalid args Model registration " + phase);
                AssertTerminal(request, LoadViewStatus.Failed);
                Assert.That(request.Exception, Is.TypeOf<InvalidOperationException>());
                Assert.That(failedView, Is.Not.Null);
                Assert.That(failedView.Module, Is.Null);
                Assert.That(failedView.Model, Is.Null);
                Assert.That(failedView.IsLoaded, Is.False);
                Assert.That(failedView.RemovedCount, Is.EqualTo(1));
                Assert.That(failedView.ClosedCount, Is.Zero);
                Assert.That(failedView.UnboundCount, Is.EqualTo(phase));
                AssertReleasedArgs(args);
                Assert.That(module.GetViews<View>(), Is.Empty);
                Assert.That(PendingViewCount(), Is.Zero);
                Assert.That(model.ExitCount, Is.EqualTo(phase + 1), "Only explicit business removal exits the Model.");
                if (phase == 0) module.AddModel(model);
                Assert.That(model.Module, Is.SameAs(module));
                yield return null;
                Assert.That(failedView == null, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator CompletionThrowAndRepeatedAbortDoNotRewriteSuccessAndArgsSelectCurrentModel()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var args = NewArgs();
            args.SetInt("token", 95);
            YLCoreSmokeView.ArgsProbe = (view, _) => view.BoundModel = module.GetModel<YLCoreSmokeRow>(101);
            var request = module.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
            Assert.That(request.Status, Is.EqualTo(LoadViewStatus.Pending));
            var oldRow = row;
            module.RemoveModel(oldRow);
            row = new YLCoreSmokeRow { Id = 101, Marker = "replacement" };
            module.AddModel(row);
            int callbacks = 0;
            request.AddCompleteEvent(done =>
            {
                callbacks++;
                done.Abort();
                throw new InvalidOperationException("YLCore async View completion subscriber failure");
            });
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: YLCore async View completion subscriber failure"));
            yield return Await(() => request.isDone, "async View success with throwing completion callback");
            AssertTerminal(request, LoadViewStatus.Succeeded);
            var view = Track(request.view);
            Assert.That(view.Model, Is.SameAs(row), "Args callback binds the valid registration at callback time, not the old request-time Model.");
            Assert.That(oldRow.Module, Is.Null);
            Assert.That(oldRow.ExitCount, Is.EqualTo(1));
            Assert.That(view.Token, Is.EqualTo(95));
            Assert.That(callbacks, Is.EqualTo(1));
            AssertReleasedArgs(args);
            request.Abort();
            request.Abort();
            int late = 0;
            request.AddCompleteEvent(_ => late++);
            yield return null;
            AssertTerminal(request, LoadViewStatus.Succeeded);
            Assert.That(request.view, Is.SameAs(view));
            Assert.That(view.IsLoaded, Is.True);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(late, Is.EqualTo(1));
            Assert.That(PendingViewCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator NamedSynchronousFailuresAndColdCacheReuseKeepArgsAndOwnerBoundaries()
        {
            yield return StartModule();
            var args = NewArgs();
            args.SetObject("retained", module);
            Assert.Throws<InvalidOperationException>(() => module.LoadView("YLCoreMissingView", loadViewArgs: args));
            Assert.Throws<ObjectDisposedException>(() => args.GetObject("retained"));
            Assert.That(module.GetViews<YLFramework.Core.View>(), Is.Empty);

            args = NewArgs();
            Assert.Throws<InvalidCastException>(() => module.LoadView<YLCoreSmokePanel>(nameof(YLCoreSmokeView), loadViewArgs: args));
            Assert.Throws<ObjectDisposedException>(() => args.GetInt("token"));
#pragma warning disable CS0618
            Assert.Throws<InvalidCastException>(() => module.LoadView<YLCoreSmokePanel>(nameof(YLCoreSmokeView), null, new object[0]));
#pragma warning restore CS0618
            Assert.That(module.GetViews<YLFramework.Core.View>(), Is.Empty);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(YLCoreSmokeModule.FixtureRoot + "/" + nameof(YLCoreSmokeView) + ".prefab");
            var loader = Singleton(typeof(GameObjectLoader));
            if (loader != null)
                Assert.That(((IDictionary)Field(loader, "allPools")).Contains(prefab.GetHashCode()), Is.False);

            args = NewArgs();
            args.SetInt("token", 52);
            var parent = Own(new GameObject("YLCoreSmoke.ColdSyncParent")).transform;
            // 不预调用LoadFixture，实际走命名资产取得、校验、实例化的冷入口。
            var view = Track(module.LoadView<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent, loadViewArgs: args));
            OwnAsset(prefab);
            AssertView(view, parent, 52, 1);
            Assert.Throws<ObjectDisposedException>(() => args.GetInt("token"));
            var model = module.LoadModel<YLCoreProbeModel>();
            view.BoundModel = model;
            var removed = 0;
            view.onViewRemove = () => removed++;
            view.Close();
#pragma warning disable CS0618
            var reused = Track(module.LoadView<YLCoreSmokeView>(nameof(YLCoreSmokeView), parent, new object[] { "new session" }));
#pragma warning restore CS0618
            Assert.That(reused, Is.SameAs(view));
            AssertView(reused, parent, 0, 2);
            Assert.That(reused.Model, Is.Null);
            Assert.That(reused.onViewRemove, Is.Null);
            Assert.That(reused.Parameter, Is.EqualTo("new session"));
            reused.Close();
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(model.Module, Is.SameAs(module));
            Assert.That(model.ExitCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NamedResourceFallbackUsesBaseProjectButKeepsConsumerOwnership()
        {
            yield return StartModule();
            var consumer = new YLCoreDirectViewModule { ProjectName = "YLCoreMissingViewProject" };
            var resourceModule = new YLCoreBaseProbeModule { ProjectName = YLCoreSmokeModule.Project };
            try
            {
                var startup = YLFramework.Core.ModuleManager.StartUpModule(resourceModule);
                yield return Await(() => startup.isDone, "View resource base module");
                Assert.That(startup.error, Is.Null.Or.Empty);
                startup = YLFramework.Core.ModuleManager.StartUpModule(consumer);
                yield return Await(() => startup.isDone, "View consumer module");
                Assert.That(startup.error, Is.Null.Or.Empty);
                var candidates = typeof(YLFramework.Core.ModuleManager).GetMethod("ViewResourceProjects", StaticPrivate);
                Assert.That(candidates.Invoke(null, new object[] { consumer }),
                    Is.EqualTo(new[] { consumer.ProjectName, YLCoreSmokeModule.Project }));
                consumer.ProjectName = YLCoreSmokeModule.Project;
                Assert.That(candidates.Invoke(null, new object[] { consumer }), Is.EqualTo(new[] { YLCoreSmokeModule.Project }));
                consumer.ProjectName = "YLCoreMissingViewProject";
                var args = NewArgs();
                args.SetInt("token", 84);
                var view = Track(consumer.LoadView<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args));
                OwnAsset(AssetDatabase.LoadAssetAtPath<GameObject>(YLCoreSmokeModule.FixtureRoot + "/" + nameof(YLCoreSmokeView) + ".prefab"));
                Assert.That(view.Module, Is.SameAs(consumer));
                Assert.That(view.Token, Is.EqualTo(84));
                Assert.That(view.IsLoaded, Is.True);
                Assert.That(consumer.GetView<YLCoreSmokeView>(), Is.SameAs(view));
                Assert.That(module.GetViews<YLFramework.Core.View>(), Is.Empty);
                Assert.That(resourceModule.GetViews<YLFramework.Core.View>(), Is.Empty);
                Assert.Throws<ObjectDisposedException>(() => args.GetInt("token"));
                view.Close();
                args = NewArgs();
                args.SetInt("token", 85);
                var asyncFallback = consumer.LoadViewAsync<YLCoreSmokeView>(nameof(YLCoreSmokeView), loadViewArgs: args);
                yield return Await(() => asyncFallback.isDone, "async View fallback across resource projects");
                AssertTerminal(asyncFallback, LoadViewStatus.Succeeded);
                Assert.That(asyncFallback.view, Is.SameAs(view));
                Assert.That(view.Module, Is.SameAs(consumer));
                Assert.That(view.Token, Is.EqualTo(85));
                Assert.That(resourceModule.GetViews<View>(), Is.Empty);
                Assert.That(module.GetViews<View>(), Is.Empty);
                AssertReleasedArgs(args);
                consumer.Close();
                Assert.That(view.Module, Is.Null);
                Assert.That(resourceModule.State, Is.EqualTo(YLFramework.Core.ModuleState.Running));
            }
            finally
            {
                consumer.Close();
                resourceModule.AllowClose = true;
                resourceModule.Close();
                classPool.Restore(new YLFramework.Core.Module[] { consumer, resourceModule });
            }
        }

        [UnityTest]
        public IEnumerator NamedPanelsUseRealManagerCanvasEventSystemAsyncAndCacheIndexes()
        {
            yield return StartModule();
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            Assert.That(prefab.GetComponent<Canvas>(), Is.Null);
            Assert.That(prefab.GetComponent<RectTransform>(), Is.Not.Null);
            Transform parent = PanelParent();
            int loadingProbes = 0;
            int argsProbes = 0;
            YLCoreSmokePanel.LoadingProbe = panel =>
            {
                loadingProbes++;
                AssertPreparedPanel(panel, UIType.UI);
            };
            YLCoreSmokePanel.ArgsProbe = (panel, _) =>
            {
                argsProbes++;
                AssertPreparedPanel(panel, UIType.UI);
            };
            var args = NewArgs();
            args.SetInt("token", 17);
            var first = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                YLFramework.Core.UIType.UI, parent, loadViewArgs: args));
            AssertPanel(first, 17, 1);
            first.SortingOrder += 37;
            Assert.That(PanelOrders(UIType.UI)[first], Is.EqualTo(first.SortingOrder));
            Assert.That(first.Canvas.sortingOrder, Is.EqualTo(first.DisplaySortingOrder));
            first.gameObject.SetActive(false);
            var siblingObject = Own(new GameObject("YLCoreSmoke.InactiveOrderSibling", typeof(RectTransform)));
            siblingObject.SetActive(false);
            siblingObject.transform.SetParent(first.transform.parent);
            var sibling = module.AttachView(siblingObject.AddComponent<YLCoreSmokePanel>());
            Assert.That(PanelOrders(UIType.UI).Contains(first), Is.True,
                "An inactive but registered Panel must not be pruned while calculating the next order.");
            Assert.That(sibling.SortingOrder, Is.GreaterThan(first.SortingOrder));
            sibling.Close();
            first.gameObject.SetActive(true);
            Assert.That(first.Canvas.overrideSorting, Is.True);
            Assert.That(first.Canvas.sortingOrder, Is.EqualTo(first.DisplaySortingOrder));
            if (previousPanelManager == null)
            {
                var canvas = YLFramework.Core.PanelManager.UICanvas[YLFramework.Core.UIType.UI];
                Assert.That(first.transform.parent, Is.SameAs(canvas.transform));
                Assert.That(canvas.name, Is.EqualTo(Resources.Load<GameObject>("YLFramework/Core/Canvas").name + "(Clone)"));
                Assert.That(ownedCanvases[YLFramework.Core.UIType.UI], Is.SameAs(canvas));
            }
            else Assert.That(first.transform.parent, Is.SameAs(parent));
            Assert.That(Singleton(typeof(YLFramework.Core.PanelManager)), Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);
            var managerSystem = (GameObject)Field(Singleton(typeof(YLFramework.Core.PanelManager)), "_eventSystem");
            if (managerSystem && !previousEventSystems.Contains(managerSystem.GetInstanceID()))
            {
                Assert.That(managerSystem.GetComponent<EventSystem>(), Is.SameAs(EventSystem.current));
                Assert.That(managerSystem.name, Is.EqualTo("EventSystem(Created By YLFramework.Core)"));
            }
            var canvasOnPanel = first.Canvas;
            first.Close();
            AssertRemoved(first, nameof(YLCoreSmokePanel));
            Assert.That(PanelOrders(UIType.UI).Contains(first), Is.False);
            var nextArgs = NewArgs();
            nextArgs.SetInt("token", 29);
            var request = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                YLFramework.Core.UIType.UI, parent, loadViewArgs: nextArgs);
            int callbacks = 0;
            bool completeState = false;
            request.AddCompleteEvent(done =>
            {
                callbacks++;
                completeState = done.isDone && done.progress == 1f && !done.keepWaiting && string.IsNullOrEmpty(done.error);
                Track(done.panel);
            });
            yield return Await(() => request.isDone, "Core named LoadPanelAsync");
            Assert.That(request.error, Is.Null.Or.Empty);
            AssertTerminal(request, LoadViewStatus.Succeeded);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(completeState, Is.True);
            Assert.That(request.panel, Is.SameAs(first));
            Assert.That(first.Canvas, Is.SameAs(canvasOnPanel));
            Assert.That(first.GetComponents<Canvas>(), Has.Length.EqualTo(1));
            AssertPanel(first, 29, 2);
            Assert.That(first.GetComponents<UnityEngine.UI.GraphicRaycaster>(), Has.Length.EqualTo(1));
            Assert.That(loadingProbes, Is.EqualTo(3));
            Assert.That(argsProbes, Is.EqualTo(3));
            module.Close();
            Assert.That(first.RemovedCount, Is.EqualTo(2));
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.Empty);
            Assert.That(PanelOrders(UIType.UI).Contains(first), Is.False);
        }

        [UnityTest]
        public IEnumerator OrdinaryViewPanelPreparationRetriesAndReselectsSharedCanvasOnReuse()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            Assert.That(PanelManager.UICanvas, Is.Not.Null);
            var invalidObject = Own(new GameObject("YLCore.PanelInvalidCanvas", typeof(RectTransform), typeof(Canvas)));
            invalidObject.SetActive(false);
            var invalid = invalidObject.AddComponent<YLCoreSmokePanel>();
            var args = NewArgs();
            args.SetObject("retained", module);
            module.UIStartSortOrder = short.MaxValue;
            Assert.Throws<ArgumentOutOfRangeException>(() => module.AttachView(invalid, args));
            module.UIStartSortOrder = 0;
            Assert.That(invalid.LoadedCount, Is.Zero);
            AssertReleasedArgs(args);
            AssertPanelReleased(invalid);
            YLCoreSmokePanel.LoadingProbe = panel => AssertPreparedPanel(panel, UIType.UI);
            module.AttachView(invalid);
            Assert.That(invalid.IsLoaded, Is.True);
            Assert.That(invalid.GetComponents<Canvas>(), Has.Length.EqualTo(1));
            invalid.Close();
            AssertPanelReleased(invalid);

            var root = Own(new GameObject("YLCore.SharedCanvasRoot", typeof(RectTransform)));
            var firstParent = Own(new GameObject("YLCore.SharedCanvasFirst", typeof(RectTransform), typeof(Canvas)));
            firstParent.transform.SetParent(root.transform);
            var secondParent = Own(new GameObject("YLCore.SharedCanvasSecond", typeof(RectTransform), typeof(Canvas)));
            secondParent.transform.SetParent(root.transform);
            YLCoreSmokePanel.LoadingProbe = null;
            module.AttachView(firstParent.AddComponent<YLCoreSmokePanel>());
            module.AttachView(secondParent.AddComponent<YLCoreSmokePanel>());
            int probes = 0;
            Canvas expected = firstParent.GetComponent<Canvas>();
            YLCoreSmokePanel.LoadingProbe = panel =>
            {
                probes++;
                Assert.That(panel.UIType, Is.EqualTo(UIType.UI));
                Assert.That(panel.Canvas, Is.SameAs(expected));
                Assert.That(panel.GetComponent<Canvas>(), Is.Null);
                Assert.That(panel.IsLoaded, Is.False);
                Assert.That(module.GetView<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), panel.Id), Is.SameAs(panel));
                Assert.That(PanelOrders(UIType.UI).Contains(panel), Is.False);
                Assert.That(PanelQueues().Contains(panel), Is.False);
            };
            var shared = Track(module.LoadView<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), firstParent.transform, loadViewArgs: null));
            shared.Close();
            AssertPanelReleased(shared);
            expected = secondParent.GetComponent<Canvas>();
            var reused = module.LoadViewAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), secondParent.transform, loadViewArgs: null);
            yield return Await(() => reused.isDone, "ordinary View Panel shared Canvas reload");
            AssertTerminal(reused, LoadViewStatus.Succeeded);
            Assert.That(reused.view, Is.SameAs(shared));
            Assert.That(probes, Is.EqualTo(2));
            shared.Close();
            AssertPanelReleased(shared);
            Assert.That(PendingViewCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator PanelResourceAndGenericFailuresPublishChildTerminalWithoutResidue()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var parent = PanelParent();
            foreach (string name in new[] { "YLCoreMissingPanel_" + Guid.NewGuid().ToString("N"), nameof(YLCoreSmokeView) })
            {
                var args = NewArgs();
                args.SetObject("retained", module);
                var request = module.LoadPanelAsync<YLCoreSmokePanel>(name, UIType.UI, parent, loadViewArgs: args);
                var child = (LoadViewRequest<YLCoreSmokePanel>)Field(request, "child");
                Assert.That(child, Is.Not.Null);
                int callbacks = 0;
                request.AddCompleteEvent(done =>
                {
                    callbacks++;
                    AssertTerminal(done, LoadViewStatus.Failed);
                    AssertTerminal(child, LoadViewStatus.Failed);
                    Assert.That(done.Exception, Is.SameAs(child.Exception));
                    Assert.That(module.GetViews<View>(), Is.Empty);
                    Assert.That(PendingViewCount(), Is.Zero);
                    AssertReleasedArgs(args);
                });
                yield return Await(() => request.isDone, "Panel failed child " + name);
                Assert.That(request.Exception, name == nameof(YLCoreSmokeView)
                    ? Is.TypeOf<InvalidCastException>() : Is.TypeOf<InvalidOperationException>());
                Assert.That(callbacks, Is.EqualTo(1));
                request.Abort();
                AssertTerminal(request, LoadViewStatus.Failed);
            }
#pragma warning disable CS0618
            Assert.Throws<InvalidCastException>(() => module.LoadPanel<YLCoreIncompatiblePanel>(nameof(YLCoreSmokePanel),
                UIType.UI, parent, new object[0]));
            var legacy = module.LoadPanelAsync<YLCoreIncompatiblePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, new object[0]);
#pragma warning restore CS0618
            yield return Await(() => legacy.isDone, "legacy Panel generic mismatch");
            AssertTerminal(legacy, LoadViewStatus.Failed);
            Assert.That(legacy.Exception, Is.TypeOf<InvalidCastException>());
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(PendingViewCount(), Is.Zero);
            Assert.That(FixtureInstanceCount(AssetDatabase.LoadAssetAtPath<GameObject>(
                YLCoreSmokeModule.FixtureRoot + "/" + nameof(YLCoreSmokePanel) + ".prefab")), Is.Zero);
        }

        [UnityTest]
        public IEnumerator PanelSortingAndBusinessFailuresRollbackBeforeNotificationAndPreserveRootCause()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            var parent = PanelParent();
            for (int phase = 0; phase < 5; phase++)
            {
                var failure = new InvalidOperationException("YLCore Panel transaction failure " + phase);
                var cleanupFailure = new InvalidOperationException("YLCore Panel rollback failure " + phase);
                var args = NewArgs();
                args.SetObject("retained", module);
                YLCoreSmokePanel failedPanel = null;
                LoadPanelRequest<YLCoreSmokePanel> request = null;
                void Fail(YLCoreSmokePanel panel)
                {
                    failedPanel = Track(panel);
                    panel.RemoveAction = () =>
                    {
                        request.Abort();
                        throw cleanupFailure;
                    };
                    throw failure;
                }
                YLCoreSmokePanel.SortingProbe = (panel, order) =>
                {
                    if (phase == 0 || (phase == 3 && order == 100) || (phase == 4 && module.UIStartSortOrder == 100)) Fail(panel);
                };
                YLCoreSmokePanel.LoadingProbe = panel =>
                {
                    if (phase == 1) Fail(panel);
                    if (phase == 3) panel.SortingOrder = 100;
                    if (phase == 4) module.UIStartSortOrder = 100;
                };
                YLCoreSmokePanel.ArgsProbe = phase == 2 ? (Action<YLCoreSmokePanel, LoadViewArgs>)((panel, _) => Fail(panel)) : null;
                request = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: args);
                var child = (LoadViewRequest<YLCoreSmokePanel>)Field(request, "child");
                int callbacks = 0;
                request.AddCompleteEvent(done =>
                {
                    callbacks++;
                    AssertTerminal(done, LoadViewStatus.Failed);
                    AssertTerminal(child, LoadViewStatus.Failed);
                    Assert.That(done.Exception, Is.SameAs(failure));
                    Assert.That(done.Exception.Data["YLFramework.Core.CleanupException"], Is.SameAs(cleanupFailure));
                    AssertPanelReleased(failedPanel);
                    Assert.That(failedPanel.IsLoaded, Is.False);
                    Assert.That(failedPanel.RemovedCount, Is.EqualTo(1));
                    Assert.That(failedPanel.ClosedCount, Is.Zero);
                    AssertReleasedArgs(args);
                    Assert.That(PendingViewCount(), Is.Zero);
                });
                yield return Await(() => request.isDone, "Panel transaction rollback " + phase);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(module.GetViews<View>(), Is.Empty);
                yield return null;
                Assert.That(failedPanel == null, Is.True, "Failed initialization discards the acquired instance.");
                YLCoreSmokePanel.ResetProbes();
                module.UIStartSortOrder = 0;
            }
        }

        [UnityTest]
        public IEnumerator AlwaysInFrontSortingSnapshotCannotChangeReusedPanelInAnotherUiContext()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            var parent = PanelParent();
            var first = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: null));
            var second = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: null));
            first.KeepInFront = second.KeepInFront = true;
            var ordered = new List<YLCoreSmokePanel>();
            foreach (DictionaryEntry entry in PanelOrders(UIType.UI)) ordered.Add((YLCoreSmokePanel)entry.Key);
            Assert.That(ordered, Has.Count.EqualTo(2));
            var trigger = ordered[0];
            var previous = ordered[1];
            var persistentParent = Own(new GameObject("YLCore.SortingReentryPersistentParent", typeof(RectTransform), typeof(Canvas))).transform;
            Object.DontDestroyOnLoad(persistentParent.gameObject);
            YLCoreSmokePanel fresh = null;
            bool reentered = false;
            YLCoreSmokePanel.SortingProbe = (panel, _) =>
            {
                if (panel != trigger || reentered) return;
                reentered = true;
                previous.Close();
                fresh = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.DontDestroyUI, persistentParent, loadViewArgs: null));
                Assert.That(fresh, Is.SameAs(previous), "Exercise a new business session on the same healthy cached component.");
            };
            var third = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: null));
            Assert.That(reentered, Is.True);
            Assert.That(fresh.UIType, Is.EqualTo(UIType.DontDestroyUI));
            Assert.That(fresh.IsLoaded, Is.True);
            Assert.That(fresh.SortingOrder, Is.GreaterThanOrEqualTo(10000), "The old UI sorting snapshot cannot write the new DDOL session.");
            Assert.That(PanelOrders(UIType.UI).Contains(fresh), Is.False);
            Assert.That(PanelOrders(UIType.DontDestroyUI)[fresh], Is.EqualTo(fresh.SortingOrder));
            Assert.That(fresh.Canvas.sortingOrder, Is.EqualTo(fresh.DisplaySortingOrder));
            Assert.That(fresh.Module, Is.SameAs(module));
            YLCoreSmokePanel.ResetProbes();
            third.Close();
            first.Close();
            second.Close();
            Assert.That(module.GetViews<Panel>(), Is.Empty);
            Assert.That(PanelOrders(UIType.UI), Is.Empty);
            Assert.That(PanelOrders(UIType.DontDestroyUI), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AlwaysInFrontObserverFailureIsReportedWithoutChangingSession()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            var panel = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, PanelParent(), loadViewArgs: null));
            panel.KeepInFront = true;
            var failure = new InvalidOperationException("AlwaysInFront sorting failure");
            YLCoreSmokePanel.SortingProbe = (_, _) => throw failure;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: AlwaysInFront sorting failure"));
            Assert.DoesNotThrow(() => panel.RefreshUIState());
            Assert.That(panel.IsLoaded, Is.True);
            Assert.That(panel.Canvas.sortingOrder, Is.EqualTo(panel.DisplaySortingOrder));
            YLCoreSmokePanel.ResetProbes();
            panel.Close();
            AssertPanelReleased(panel);
        }

        [UnityTest]
        public IEnumerator PanelVisibilityObserverFailureDoesNotRollbackUnrelatedLoad()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            var parent = PanelParent();
            YLCoreSmokePanel.ProbePanelType = PanelType.Panel;
            Assert.That(PanelManager.UICanvas, Is.Not.Null);
            Assert.That(PanelQueues(), Is.Empty, "Do not hide Panels belonging to an external fixture.");
            var previous = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: null));
            var failure = new InvalidOperationException("YLCore Panel Invisible preparation failure");
            int restored = 0;
            YLCoreSmokePanel.VisibleProbe = panel =>
            {
                Assert.That(panel, Is.SameAs(previous));
                restored++;
            };
            YLCoreSmokePanel failedPanel = null;
            YLCoreSmokePanel.SortingProbe = (panel, _) => failedPanel = Track(panel);
            YLCoreSmokePanel.InvisibleProbe = panel =>
            {
                Assert.That(panel, Is.SameAs(previous));
                throw failure;
            };
            var args = NewArgs();
            args.SetObject("retained", module);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: YLCore Panel Invisible preparation failure"));
            var request = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: args);
            yield return Await(() => request.isDone, "Panel queue preparation rollback");
            AssertTerminal(request, LoadViewStatus.Succeeded);
            Assert.That(request.Exception, Is.Null);
            Assert.That(failedPanel, Is.Not.Null);
            Assert.That(request.panel, Is.SameAs(failedPanel));
            Assert.That(failedPanel.LoadedCount, Is.EqualTo(1), "Queue activation runs after business initialization, within the final Loaded transaction.");
            Assert.That(failedPanel.ArgsCount, Is.EqualTo(1));
            Assert.That(failedPanel.IsLoaded, Is.True);
            Assert.That(previous.Module, Is.SameAs(module));
            Assert.That(previous.IsLoaded, Is.True);
            Assert.That(previous.Canvas.enabled, Is.False);
            Assert.That(restored, Is.Zero);
            Assert.That(PanelQueues().Contains(previous), Is.True);
            Assert.That(PanelManager.GetTopPanel(previous.RootCanvas), Is.SameAs(failedPanel));
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.EquivalentTo(new[] { previous, failedPanel }));
            AssertReleasedArgs(args);
            Assert.That(PendingViewCount(), Is.Zero);
            YLCoreSmokePanel.ResetProbes();
            YLCoreSmokePanel.VisibleProbe = panel => { Assert.That(panel, Is.SameAs(previous)); restored++; };
            failedPanel.Close();
            Assert.That(previous.Canvas.enabled, Is.True);
            Assert.That(restored, Is.EqualTo(1));
            AssertPanelReleased(failedPanel);
            YLCoreSmokePanel.ResetProbes();
            previous.Close();
            AssertPanelReleased(previous);
        }

        [UnityTest]
        public IEnumerator PanelWaitingAbortDuplicateClaimAndBorrowedArgsAbortKeepStableOwnership()
        {
            yield return StartModule();
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            var parent = PanelParent();
            var rejectedArgs = NewArgs();
            rejectedArgs.SetObject("retained", module);
            Assert.Throws<ArgumentException>(() => module.LoadPanelAsync<YLCoreSmokePanel>(string.Empty,
                UIType.UI, parent, loadViewArgs: rejectedArgs));
            AssertReleasedArgs(rejectedArgs);
            Assert.That(PendingViewCount(), Is.Zero);

            var args = NewArgs();
            args.SetObject("retained", module);
            var waiting = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: args);
            var waitingChild = (LoadViewRequest<YLCoreSmokePanel>)Field(waiting, "child");
            Assert.That(waiting.Status, Is.EqualTo(LoadViewStatus.Pending));
            Assert.Throws<InvalidOperationException>(() => module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                UIType.UI, parent, loadViewArgs: args));
            Assert.Throws<InvalidOperationException>(() => args.GetObject("retained"));
            Assert.That(PendingViewCount(), Is.EqualTo(1));
            int waitingCallbacks = 0;
            waiting.AddCompleteEvent(_ => waitingCallbacks++);
            YLFramework.Foundation.CustomAsyncOperation<LoadPanelRequest<YLCoreSmokePanel>> foundation = waiting;
            foundation.Abort();
            waitingChild.Abort();
            AssertTerminal(waiting, LoadViewStatus.Canceled);
            AssertTerminal(waitingChild, LoadViewStatus.Canceled);
            Assert.That(waiting.Exception, Is.SameAs(waitingChild.Exception));
            Assert.That(waitingCallbacks, Is.EqualTo(1));
            AssertReleasedArgs(args);
            Assert.That(PendingViewCount(), Is.Zero);
            Assert.That(FixtureInstanceCount(prefab), Is.Zero);

            args = NewArgs();
            args.SetInt("token", 61);
            args.SetObject("retained", module);
            LoadPanelRequest<YLCoreSmokePanel> borrowedRequest = null;
            LoadPanelRequest<YLCoreSmokePanel> next = null;
            YLCoreSmokePanel canceledPanel = null;
            bool borrowedAfterAbort = false;
            YLCoreSmokePanel.ArgsProbe = (panel, borrowed) =>
            {
                canceledPanel = Track(panel);
                AssertPreparedPanel(panel, UIType.UI);
                Assert.Throws<InvalidOperationException>(() => module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                    UIType.UI, parent, loadViewArgs: borrowed));
                borrowedRequest.Abort();
                borrowedRequest.Abort();
                borrowedAfterAbort = !borrowedRequest.isDone && borrowedRequest.Status == LoadViewStatus.Pending &&
                    borrowed.GetInt("token") == 61 && PendingViewCount() == 1;
                YLCoreSmokePanel.ArgsProbe = null;
            };
            borrowedRequest = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: args);
            int callbacks = 0;
            borrowedRequest.AddCompleteEvent(done =>
            {
                callbacks++;
                AssertTerminal(done, LoadViewStatus.Canceled);
                AssertPanelReleased(canceledPanel);
                Assert.That(canceledPanel.IsLoaded, Is.False);
                Assert.That(canceledPanel.RemovedCount, Is.EqualTo(1));
                Assert.That(canceledPanel.ClosedCount, Is.Zero);
                AssertReleasedArgs(args);
                Assert.That(PendingViewCount(), Is.Zero);
                var nextArgs = NewArgs();
                nextArgs.SetInt("token", 62);
                next = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), UIType.UI, parent, loadViewArgs: nextArgs);
            });
            yield return Await(() => borrowedRequest.isDone && next != null && next.isDone, "Panel borrowed Abort and reentrant load");
            Assert.That(borrowedAfterAbort, Is.True);
            Assert.That(callbacks, Is.EqualTo(1));
            AssertTerminal(borrowedRequest, LoadViewStatus.Canceled);
            Assert.That(borrowedRequest.Exception, Is.TypeOf<OperationCanceledException>());
            AssertTerminal(next, LoadViewStatus.Succeeded);
            var freshPanel = Track(next.panel);
            Assert.That(freshPanel.Token, Is.EqualTo(62));
            Assert.That(freshPanel.Module, Is.SameAs(module));
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.EqualTo(new[] { freshPanel }));
            Assert.That(waitingCallbacks, Is.EqualTo(1));
            Assert.That(PendingViewCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator DontDestroyPanelKeepsOwnerAndClosePublishesAfterClosedBeforeRestartedSession()
        {
            yield return StartModule();
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            var parentObject = Own(new GameObject("YLCore.DontDestroyPanelParent", typeof(RectTransform), typeof(Canvas)));
            Object.DontDestroyOnLoad(parentObject);
            var parent = parentObject.transform;
            int probes = 0;
            YLCoreSmokePanel.LoadingProbe = panel =>
            {
                probes++;
                AssertPreparedPanel(panel, UIType.DontDestroyUI);
            };
            var oldPanel = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                UIType.DontDestroyUI, parent, loadViewArgs: null));
            Assert.That(oldPanel.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            var consumer = new YLCoreConsumerModule();
            try
            {
                var consumerStartup = ModuleManager.StartUpModule(consumer);
                yield return Await(() => consumerStartup.isDone, "unrelated current Module for DDOL Panel");
                Assert.That(consumerStartup.error, Is.Null.Or.Empty);
                Assert.That(oldPanel.Module, Is.SameAs(module));
                Assert.That(consumer.GetViews<View>(), Is.Empty);
                var args = NewArgs();
                args.SetObject("retained", module);
                var pending = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                    UIType.DontDestroyUI, parent, loadViewArgs: args);
                var child = (LoadViewRequest<YLCoreSmokePanel>)Field(pending, "child");
                var lifecycle = typeof(Module).GetField("ViewLifecycle", InstancePrivate).GetValue(module);
                var oldRow = row;
                bool releasedBeforeExit = false;
                oldPanel.RemoveAction = () =>
                {
                    releasedBeforeExit = module.State == ModuleState.Closing && !pending.isDone &&
                        PendingViewCount() == 0 && Field(child, "owner") == null && Field(child, "args") == null;
                    AssertReleasedArgs(args);
                };
                StartUpModuleRequest restarted = null;
                LoadPanelRequest<YLCoreSmokePanel> fresh = null;
                int callbacks = 0;
                pending.AddCompleteEvent(done =>
                {
                    callbacks++;
                    AssertTerminal(done, LoadViewStatus.Invalidated);
                    AssertTerminal(child, LoadViewStatus.Invalidated);
                    Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
                    Assert.That(ModuleManager.GetModule<YLCoreSmokeModule>(), Is.Null);
                    Assert.That(oldRow.Module, Is.Null);
                    Assert.That(oldRow.ExitCount, Is.EqualTo(1));
                    AssertPanelReleased(oldPanel);
                    AssertReleasedArgs(args);
                    Assert.That(PendingViewCount(), Is.Zero);
                    module.StartAction = () => fresh = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                        UIType.DontDestroyUI, parent, loadViewArgs: null);
                    restarted = ModuleManager.StartUpModule(module);
                });
                module.Close();
                oldPanel.RemoveAction = null;
                Assert.That(releasedBeforeExit, Is.True);
                Assert.That(callbacks, Is.EqualTo(1));
                yield return Await(() => restarted != null && restarted.isDone && fresh != null && fresh.isDone,
                    "DDOL Panel restarted owner session");
                Assert.That(restarted.error, Is.Null.Or.Empty);
                Assert.That(typeof(Module).GetField("ViewLifecycle", InstancePrivate).GetValue(module), Is.Not.SameAs(lifecycle));
                AssertTerminal(pending, LoadViewStatus.Invalidated);
                AssertTerminal(fresh, LoadViewStatus.Succeeded);
                var panel = Track(fresh.panel);
                Assert.That(panel, Is.SameAs(oldPanel));
                Assert.That(panel.Module, Is.SameAs(module));
                Assert.That(panel.UIType, Is.EqualTo(UIType.DontDestroyUI));
                Assert.That(panel.GetComponents<Canvas>(), Has.Length.EqualTo(1));
                Assert.That(PanelOrders(UIType.DontDestroyUI)[panel], Is.EqualTo(panel.SortingOrder));
                Assert.That(PanelOrders(UIType.UI).Contains(panel), Is.False);
                Assert.That(probes, Is.EqualTo(2));
                Assert.That(PendingViewCount(), Is.Zero);
                Assert.That(consumer.GetViews<View>(), Is.Empty);
                yield return null;
                Assert.That(FixtureInstanceCount(prefab), Is.EqualTo(1), "Old producer must not acquire an instance in the new session.");
            }
            finally
            {
                oldPanel.RemoveAction = null;
                consumer.Close();
                classPool.Restore(new Module[] { consumer });
            }
        }

        [UnityTest]
        public IEnumerator PanelDestroyedParentAndThrowingCompletionKeepTerminalAndReleasedInputs()
        {
            yield return StartModule();
            var prefab = LoadFixture<GameObject>(nameof(YLCoreSmokePanel));
            var parent = Own(new GameObject("YLCore.PanelDestroyedParent", typeof(RectTransform)));
            var args = NewArgs();
            args.SetObject("retained", parent);
            var invalidated = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                UIType.UI, parent.transform, loadViewArgs: args);
            var child = (LoadViewRequest<YLCoreSmokePanel>)Field(invalidated, "child");
            Object.DestroyImmediate(parent);
            yield return Await(() => invalidated.isDone, "Panel destroyed pending parent");
            AssertTerminal(invalidated, LoadViewStatus.Invalidated);
            AssertTerminal(child, LoadViewStatus.Invalidated);
            Assert.That(invalidated.Exception, Is.SameAs(child.Exception));
            AssertReleasedArgs(args);
            Assert.That(module.GetViews<View>(), Is.Empty);
            Assert.That(PendingViewCount(), Is.Zero);
            Assert.That(FixtureInstanceCount(prefab), Is.Zero);

            args = NewArgs();
            args.SetInt("token", 95);
            var request = module.LoadPanelAsync<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                UIType.UI, PanelParent(), loadViewArgs: args);
            int callbacks = 0;
            request.AddCompleteEvent(done =>
            {
                callbacks++;
                AssertTerminal(done, LoadViewStatus.Succeeded);
                Assert.That(PendingViewCount(), Is.Zero);
                AssertReleasedArgs(args);
                done.Abort();
                throw new InvalidOperationException("YLCore Panel completion subscriber failure");
            });
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: YLCore Panel completion subscriber failure"));
            yield return Await(() => request.isDone, "Panel throwing completion subscriber");
            AssertTerminal(request, LoadViewStatus.Succeeded);
            var panel = Track(request.panel);
            AssertPanel(panel, 95, 1);
            request.Abort();
            int late = 0;
            request.AddCompleteEvent(_ => late++);
            yield return null;
            AssertTerminal(request, LoadViewStatus.Succeeded);
            Assert.That(request.panel, Is.SameAs(panel));
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(late, Is.EqualTo(1));
            Assert.That(panel.IsLoaded, Is.True);
            Assert.That(PendingViewCount(), Is.Zero);
            panel.Close();
            AssertPanelReleased(panel);
        }

        [UnityTest]
        public IEnumerator XfabAdditiveSceneRunsIndependentEntryWithoutReplacingActiveScene()
        {
            yield return StartModule();
            Scene active = SceneManager.GetActiveScene();
            ownsScene = true;
            var request = AssetBundleManager.LoadSceneAsynchrony(YLCoreSmokeModule.Project,
                YLCoreSmokeModule.SceneName, LoadSceneMode.Additive);
            int callbacks = 0;
            request.AddCompleteEvent(_ => callbacks++);
            yield return Await(() => request.isDone, "XFAB additive scene");
            Assert.That(request.error, Is.Null.Or.Empty);
            Assert.That(request.progress, Is.EqualTo(1f));
            Assert.That(request.keepWaiting, Is.False);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(request.Scene.IsValid() && request.Scene.isLoaded, Is.True);
            Assert.That(request.Scene.path, Is.EqualTo(YLCoreSmokeModule.ScenePath));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            yield return AssertSceneEntry(request.Scene);
        }

        [UnityTest]
        public IEnumerator IncompatibleExistingPrefabCompletesViewFailureCallbackWithoutRegisteringView()
        {
            yield return StartModule();
            LoadFixture<GameObject>(nameof(YLCoreSmokeView));
            var parent = Own(new GameObject("YLCoreSmoke.FailureParent")).transform;
            var args = NewArgs();
            args.SetObject("retained", module);
            var request = module.LoadViewAsync<YLCoreSmokePanel>(nameof(YLCoreSmokeView), parent, loadViewArgs: args);
            int callbacks = 0;
            string callbackError = null;
            request.AddCompleteEvent(done => { callbacks++; callbackError = done.error; });
            yield return Await(() => request.isDone, "incompatible View request");
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(callbackError, Is.Not.Null.And.Not.Empty);
            StringAssert.Contains("View", callbackError);
            Assert.That(request.error, Is.EqualTo(callbackError));
            Assert.That(request.progress, Is.EqualTo(1f));
            Assert.That(request.keepWaiting, Is.False);
            Assert.That(request.view, Is.Null);
            AssertTerminal(request, LoadViewStatus.Failed);
            Assert.That(request.Exception, Is.TypeOf<InvalidCastException>());
            AssertReleasedArgs(args);
            Assert.That(PendingViewCount(), Is.Zero);
            Assert.That(module.GetViews(nameof(YLCoreSmokeView)), Is.Empty);
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedAdditiveSceneAssetAttachesByHandleAndUnloadPreservesOtherInstance()
        {
            yield return StartModule();
            void Capture(Scene scene, LoadSceneMode mode)
            {
                if (scene.path == YLCoreSmokeModule.ScenePath) ownedSceneInstances.Add(scene);
            }
            SceneManager.sceneLoaded += Capture;
            try
            {
                for (int i = 0; i < 2; i++)
                    yield return AwaitUnity(EditorSceneManager.LoadSceneAsyncInPlayMode(YLCoreSmokeModule.ScenePath,
                        new LoadSceneParameters(LoadSceneMode.Additive)), "load repeated additive scene");
            }
            finally { SceneManager.sceneLoaded -= Capture; }
            Assert.That(ownedSceneInstances.Count, Is.EqualTo(2));
            var first = ownedSceneInstances[0];
            var second = ownedSceneInstances[1];
            Assert.That(first.name, Is.EqualTo(second.name));
            Assert.That(first.path, Is.EqualTo(second.path));
            Assert.That(first.handle, Is.Not.EqualTo(second.handle));
            var firstObject = Own(new GameObject("YLCore.FirstAdditiveView"));
            var secondObject = Own(new GameObject("YLCore.SecondAdditiveView"));
            firstObject.SetActive(false);
            secondObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(firstObject, first);
            SceneManager.MoveGameObjectToScene(secondObject, second);
            var firstView = firstObject.AddComponent<YLCoreSmokeView>();
            var secondView = secondObject.AddComponent<YLCoreSmokeView>();
            module.AttachViews(first);
            Assert.That(secondView.Module, Is.Null);
            module.AttachViews(second);
            yield return AwaitUnity(SceneManager.UnloadSceneAsync(first), "unload first additive instance");
            Assert.That(firstView == null, Is.True);
            Assert.That(firstView.Module, Is.Null);
            Assert.That(firstView.RemovedCount, Is.EqualTo(1));
            Assert.That(secondView.Module, Is.SameAs(module));
            Assert.That(secondView.IsLoaded, Is.True);
            Assert.That(secondView.RemovedCount, Is.Zero);
            Assert.That(module.GetViews<View>(), Is.EqualTo(new View[] { secondView }));
            Assert.That(second.isLoaded, Is.True);
        }

        [UnityTest]
        public IEnumerator DefaultStartupSceneUsesSingleAndRealFixtureEntry()
        {
            yield return PrepareDefaultSceneTest();
            YLCoreSmokeView sceneView = null;
            void InjectView(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != YLCoreSmokeModule.ScenePath) return;
                var obj = Own(new GameObject("YLCore.DefaultSceneView"));
                obj.SetActive(false);
                SceneManager.MoveGameObjectToScene(obj, scene);
                obj.transform.SetParent(scene.GetRootGameObjects()[0].transform);
                sceneView = obj.AddComponent<YLCoreSmokeView>();
            }
            SceneManager.sceneLoaded += InjectView;
            try
            {
                yield return StartModule(true, () =>
                {
                    Assert.That(sceneView.IsLoaded, Is.True, "Default scene attachment commits before Module.OnStart.");
                    Assert.That(sceneView.Module, Is.SameAs(module));
                    Assert.That(module.GetView<YLCoreSmokeView>(sceneView.Id), Is.SameAs(sceneView));
                    Assert.That(sceneView.AwakeCount, Is.Zero);
                });
            }
            finally { SceneManager.sceneLoaded -= InjectView; }
            var loaded = SceneManager.GetSceneByPath(YLCoreSmokeModule.ScenePath);
            Assert.That(loaded.IsValid() && loaded.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(loaded));
            Assert.That(SceneManager.GetSceneByPath(YLCoreSmokeModule.HarnessPath).isLoaded, Is.False);
            Assert.That(module.SceneReadyAtStart, Is.True, "Startup validates the loaded scene before OnStart.");
            Assert.That(module.RowReadyAtStart, Is.True);
            yield return AssertSceneEntry(loaded);
            module.Close();
            Assert.That(sceneView.Module, Is.Null);
            Assert.That(sceneView.RemovedCount, Is.EqualTo(1));
            Assert.That(sceneView, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator DefaultSceneAttachFailureRollsBackModuleViewsAndModelsBeforeCompletion()
        {
            yield return PrepareDefaultSceneTest();
            YLCoreSmokeView first = null;
            YLCoreSmokeView failing = null;
            YLCoreSmokeRow initializedRow = null;
            var failure = new InvalidOperationException("default scene attach failure");
            YLCoreSmokeRow.InitAction = value => initializedRow = value;
            void InjectViews(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != YLCoreSmokeModule.ScenePath) return;
                var root = Own(new GameObject("YLCore.DefaultFailureRoot"));
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                first = root.AddComponent<YLCoreSmokeView>();
                var child = Own(new GameObject("YLCore.DefaultFailureChild"));
                child.SetActive(false);
                child.transform.SetParent(root.transform);
                failing = child.AddComponent<YLCoreSmokeView>();
                failing.LoadedAction = () => throw failure;
            }
            SceneManager.sceneLoaded += InjectViews;
            try
            {
                module = new YLCoreSmokeModule { StartWithScene = true };
                var request = YLFramework.Core.ModuleManager.StartUpModule(module);
                yield return Await(() => request.isDone, "default scene attachment failure");
                Assert.That(request.Exception, Is.SameAs(failure));
                Assert.That(request.error, Is.Not.Null.And.Not.Empty);
                Assert.That(module.State, Is.EqualTo(ModuleState.Closed));
                Assert.That(module.StartCount, Is.Zero);
                Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.Null);
                Assert.That(module.GetViews<View>(), Is.Empty);
                Assert.That(first.Module, Is.Null);
                Assert.That(failing.Module, Is.Null);
                Assert.That(first.RemovedCount, Is.EqualTo(1));
                Assert.That(failing.RemovedCount, Is.EqualTo(1));
                Assert.That(first.ClosedCount, Is.Zero);
                Assert.That(first && failing, Is.True, "Startup owns business sessions, not the scene objects.");
                Assert.That(initializedRow.ExitCount, Is.EqualTo(1));
                Assert.That(initializedRow.Module, Is.Null);
                Assert.That(SceneManager.GetSceneByPath(YLCoreSmokeModule.ScenePath).isLoaded, Is.True,
                    "Logical startup rollback does not restore or unload the scene.");
            }
            finally { SceneManager.sceneLoaded -= InjectViews; }
        }

        private IEnumerator PrepareDefaultSceneTest()
        {
            Assert.That(YLFramework.Core.ModuleManager.AllModules, Is.Empty);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                Assert.That(scene.isDirty, Is.False);
                if (scene.path == YLCoreSmokeModule.HarnessPath)
                    Assert.That(scene.GetRootGameObjects(), Is.Empty);
                else
                {
                    // Unity Test Runner's own temporary scene is allowed, but not user objects.
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                        Assert.That(behaviours, Is.Not.Empty, "Unexpected non-runner root: " + root.name);
                        foreach (var behaviour in behaviours)
                            Assert.That(behaviour && behaviour.GetType().Assembly.GetName().Name == "UnityEngine.TestRunner",
                                Is.True, "Single would unload a non-runner object: " + root.name);
                    }
                    Assert.That(string.IsNullOrEmpty(scene.path) || scene.name.StartsWith("InitTestScene", StringComparison.Ordinal),
                        Is.True, "Single would unload a non-fixture scene: " + scene.path);
                }
            }
            if (previousActive.path != YLCoreSmokeModule.HarnessPath)
            {
                yield return AwaitUnity(EditorSceneManager.LoadSceneAsyncInPlayMode(YLCoreSmokeModule.HarnessPath,
                    new LoadSceneParameters(LoadSceneMode.Additive)), "load owned empty harness");
                previousActive = SceneManager.GetSceneByPath(YLCoreSmokeModule.HarnessPath);
                Assert.That(previousActive.GetRootGameObjects(), Is.Empty);
                SceneManager.SetActiveScene(previousActive);
            }
            singleSceneTest = ownsScene = true;
        }

        private IEnumerator StartModule(bool withScene = false, Action onStart = null)
        {
            module = new YLCoreSmokeModule { StartWithScene = withScene, StartAction = onStart };
            var request = YLFramework.Core.ModuleManager.StartUpModule(module);
            Assert.That(request, Is.Not.Null);
            yield return Await(() => request.isDone, "Core startup");
            Assert.That(request.error, Is.Null.Or.Empty);
            Assert.That(request.Module, Is.SameAs(module));
            Assert.That(request.progress, Is.EqualTo(1f));
            Assert.That(request.keepWaiting, Is.False);
            Assert.That(YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>(), Is.SameAs(module));
            row = module.GetModel<YLCoreSmokeRow>(101);
            Assert.That(row, Is.Not.Null, "Assets requests can complete without an asset; startup success alone is insufficient.");
            yield return null;
        }

        private T LoadFixture<T>(string name) where T : Object
        {
            var asset = AssetBundleManager.LoadAsset<T>(YLCoreSmokeModule.Project, name);
            Assert.That(asset, Is.Not.Null, name);
            string path = AssetDatabase.GetAssetPath(asset);
            Assert.That(path.StartsWith(YLCoreSmokeModule.FixtureRoot + "/", StringComparison.Ordinal), Is.True);
            OwnAsset(asset);
            return asset;
        }

        private Transform PanelParent()
        {
            if (previousPanelManager != null)
                return Own(new GameObject("YLCoreSmoke.PanelParent", typeof(RectTransform), typeof(Canvas))).transform;
            canvasCallback = canvas =>
            {
                var manager = Singleton(typeof(YLFramework.Core.PanelManager));
                foreach (var pair in (Dictionary<YLFramework.Core.UIType, Canvas>)Field(manager, "uiCanvas"))
                    if (ReferenceEquals(pair.Value, canvas)) ownedCanvases[pair.Key] = canvas;
                Own(canvas.gameObject);
            };
            YLFramework.Core.PanelManager.onCreateCanvas += canvasCallback;
            return null;
        }

        private void AssertView(YLCoreSmokeView view, Transform parent, int token, int loads)
        {
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Module, Is.SameAs(module));
            Assert.That(view.transform.parent, Is.SameAs(parent));
            Assert.That(view.Token, Is.EqualTo(token));
            Assert.That(view.LoadedCount, Is.EqualTo(loads));
            Assert.That(view.ArgsCount, Is.EqualTo(loads));
            Assert.That(module.GetView<YLCoreSmokeView>(nameof(YLCoreSmokeView), view.Id), Is.SameAs(view));
            Assert.That(module.GetViews<YLCoreSmokeView>(), Is.EqualTo(new[] { view }));
        }

        private void AssertPanel(YLCoreSmokePanel panel, int token, int loads)
        {
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Module, Is.SameAs(module));
            Assert.That(panel.Token, Is.EqualTo(token));
            Assert.That(panel.LoadedCount, Is.EqualTo(loads));
            Assert.That(panel.ArgsCount, Is.EqualTo(loads));
            Assert.That(panel.UIType, Is.EqualTo(YLFramework.Core.UIType.UI));
            Assert.That(panel.Canvas, Is.SameAs(panel.GetComponent<Canvas>()));
            Assert.That(panel.Canvas.overrideSorting, Is.True);
            Assert.That(panel.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(panel.Canvas.sortingOrder, Is.EqualTo(panel.SortingOrder + module.UIStartSortOrder));
            Assert.That(module.GetView<YLCoreSmokePanel>(nameof(YLCoreSmokePanel), panel.Id), Is.SameAs(panel));
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.EqualTo(new[] { panel }));
            var orders = PanelOrders(UIType.UI);
            Assert.That(orders.Contains(panel), Is.True);
            Assert.That(orders[panel], Is.EqualTo(panel.SortingOrder));
        }

        private static IDictionary PanelOrders(UIType type)
        {
            var panels = new List<Panel>();
            PanelManager.GetAllPanels(type, panels);
            var orders = new Dictionary<Panel, int>();
            foreach (var panel in panels) orders.Add(panel, panel.SortingOrder);
            return orders;
        }

        private static IList PanelQueues()
        {
            var panels = new List<Panel>();
            var persistent = new List<Panel>();
            PanelManager.GetAllPanels(UIType.UI, panels);
            PanelManager.GetAllPanels(UIType.DontDestroyUI, persistent);
            panels.AddRange(persistent);
            panels.RemoveAll(panel => !panel.IsLoaded || !panel.gameObject.activeInHierarchy || panel.PanelType != PanelType.Panel);
            return panels;
        }

        private void AssertPreparedPanel(YLCoreSmokePanel panel, UIType type)
        {
            Track(panel);
            Assert.That(panel.Module, Is.SameAs(module));
            Assert.That(panel.IsLoaded, Is.False, "Business callbacks run before the View transaction commits.");
            Assert.That(panel.UIType, Is.EqualTo(type));
            Assert.That(panel.Canvas, Is.Not.Null);
            Assert.That(panel.Canvas, Is.SameAs(panel.GetComponent<Canvas>()));
            if (panel.gameObject.activeInHierarchy) Assert.That(panel.Canvas.overrideSorting, Is.True);
            Assert.That(panel.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(panel.Canvas.sortingOrder, Is.EqualTo(panel.DisplaySortingOrder));
            Assert.That(module.GetView<YLCoreSmokePanel>(panel.Id), Is.SameAs(panel));
            Assert.That(((IDictionary)Field(module, "ownedViews"))[panel.Id], Is.SameAs(panel));
            Assert.That(PanelOrders(type)[panel], Is.EqualTo(panel.SortingOrder));
            Assert.That(PanelOrders(type == UIType.UI ? UIType.DontDestroyUI : UIType.UI).Contains(panel), Is.False);
            Assert.That(PanelQueues().Contains(panel), Is.False, "Visibility queue starts at Loaded activation, not during business initialization.");
        }

        private void AssertPanelReleased(YLCoreSmokePanel panel)
        {
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Module, Is.Null);
            Assert.That(module.GetView<YLCoreSmokePanel>(panel.Id), Is.Null);
            Assert.That(((IDictionary)Field(module, "ownedViews")).Contains(panel.Id), Is.False);
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Has.No.Member(panel));
            Assert.That(module.GetViews(nameof(YLCoreSmokePanel)), Has.No.Member(panel));
            Assert.That(PanelOrders(UIType.UI).Contains(panel), Is.False);
            Assert.That(PanelOrders(UIType.DontDestroyUI).Contains(panel), Is.False);
            Assert.That(PanelQueues().Contains(panel), Is.False);
            Assert.That(((IDictionary)Field(Singleton(typeof(PanelManager)), "states")).Contains(panel), Is.False,
                "退出不仅排除查询，还必须释放旧会话策略记录。");
        }

        private void AssertRemoved(YLFramework.Core.View view, string name)
        {
            Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(module.GetView(name, view.Id), Is.Null);
            Assert.That(module.GetViews(name), Is.Empty);
            var smokeView = view as YLCoreSmokeView;
            var smokePanel = view as YLCoreSmokePanel;
            Assert.That(smokeView ? smokeView.RemovedCount : smokePanel.RemovedCount, Is.EqualTo(1));
        }

        private IEnumerator AssertSceneEntry(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            Assert.That(roots, Has.Length.EqualTo(1));
            var entry = roots[0].GetComponent<YLCoreSmokeSceneEntry>();
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.ModuleAtAwake, Is.SameAs(module));
            Assert.That(entry.StateAtAwake, Is.EqualTo(singleSceneTest ? ModuleState.Starting : ModuleState.Running));
            yield return Await(() => entry && entry.StartCount == 1, "fixture scene entry Start");
            Assert.That(entry.ModuleAtStart, Is.SameAs(module));
        }

        private void CleanPanelManager()
        {
            var manager = Singleton(typeof(YLFramework.Core.PanelManager));
            if (manager == null) return;
            var canvases = (Dictionary<YLFramework.Core.UIType, Canvas>)Field(manager, "uiCanvas");
            foreach (var pair in ownedCanvases)
                if (canvases.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value)) canvases.Remove(pair.Key);
            var system = (GameObject)Field(manager, "_eventSystem");
            if (system && !previousEventSystems.Contains(system.GetInstanceID()))
            {
                Own(system);
                manager.GetType().GetField("_eventSystem", InstancePrivate).SetValue(manager, previousManagerEventSystem);
            }
            if (previousPanelManager != null) return;
            if (((IDictionary)Field(manager, "states")).Count != 0 || canvases.Count != 0 ||
                PanelQueues().Count != 0) return;
            var type = typeof(YLFramework.Core.PanelManager);
            SceneManager.sceneLoaded -= (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction<Scene, LoadSceneMode>), type.GetMethod("OnSceneLoaded", StaticPrivate));
            SceneManager.sceneUnloaded -= (UnityEngine.Events.UnityAction<Scene>)Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction<Scene>), type.GetMethod("OnSceneUnloaded", StaticPrivate));
            // 最后一个 Panel 的必要退出已释放该 manager 自己的 Late token；不清公共宿主。
            ResetNewSingleton(type);
        }

        private YLFramework.Core.LoadViewArgs NewArgs()
        {
            var args = YLFramework.Core.LoadViewArgs.New();
            classPool.Track(args);
            return args;
        }

        private int PendingViewCount()
        {
            var pending = typeof(Module).GetField("pendingViews", InstancePrivate).GetValue(module);
            return (int)pending.GetType().GetProperty("Count").GetValue(pending);
        }

        private static void AssertTerminal<T>(LoadViewRequest<T> request, LoadViewStatus status) where T : View
        {
            Assert.That(request.isDone, Is.True);
            Assert.That(request.keepWaiting, Is.False);
            Assert.That(request.progress, Is.EqualTo(1f));
            Assert.That(request.Status, Is.EqualTo(status));
            if (status == LoadViewStatus.Succeeded)
            {
                Assert.That(request.view, Is.Not.Null);
                Assert.That(request.Exception, Is.Null);
                Assert.That(request.error, Is.Null.Or.Empty);
            }
            else
            {
                Assert.That(request.view, Is.Null);
                Assert.That(request.Exception, Is.Not.Null);
                Assert.That(request.error, Is.Not.Null.And.Not.Empty);
            }
            foreach (var name in new[] { "owner", "lifecycle", "assetName", "projects", "parent", "args", "param" })
                Assert.That(Field(request, name), Is.Null, "Terminal request retains " + name);
            Assert.That((IList)Field(request, "parentSessions"), Is.Empty);
            Assert.That(((Scene)Field(request, "targetScene")).IsValid(), Is.False);
        }

        private static void AssertReleasedArgs(LoadViewArgs args)
        {
            Assert.Throws<ObjectDisposedException>(() => args.GetObject("retained"));
            Assert.That((IDictionary)Field(args, "objects"), Is.Empty);
            Assert.That((IDictionary)Field(args, "ints"), Is.Empty);
        }

        private static void AssertTerminal<T>(LoadPanelRequest<T> request, LoadViewStatus status) where T : Panel
        {
            Assert.That(request.isDone, Is.True);
            Assert.That(request.keepWaiting, Is.False);
            Assert.That(request.progress, Is.EqualTo(1f));
            Assert.That(request.Status, Is.EqualTo(status));
            Assert.That(Field(request, "child"), Is.Null, "Terminal Panel wrapper must release its child request.");
            if (status == LoadViewStatus.Succeeded)
            {
                Assert.That(request.panel, Is.Not.Null);
                Assert.That(request.Exception, Is.Null);
                Assert.That(request.error, Is.Null.Or.Empty);
            }
            else
            {
                Assert.That(request.panel, Is.Null);
                Assert.That(request.Exception, Is.Not.Null);
                Assert.That(request.error, Is.Not.Null.And.Not.Empty);
            }
        }

        private static int FixtureInstanceCount(GameObject prefab)
        {
            var loader = Singleton(typeof(GameObjectLoader));
            if (loader == null) return 0;
            var pools = (IDictionary)Field(loader, "allPools");
            return pools.Contains(prefab.GetHashCode()) ? ((IDictionary)Field(pools[prefab.GetHashCode()], "allObjs")).Count : 0;
        }

        private IEnumerator DrainFixtureAssetProducer()
        {
            // 等待同一真实后端的新请求结束，再留一帧让之前的Core producer退出；不使用假后端或改写注册。
            var backend = AssetBundleManager.LoadAssetAsync<GameObject>(YLCoreSmokeModule.Project, nameof(YLCoreSmokeView));
            yield return Await(() => backend.isDone, "drain real fixture asset producer");
            Assert.That(backend.error, Is.Null.Or.Empty);
            Assert.That(backend.asset, Is.Not.Null);
            OwnAsset(backend.asset);
            yield return null;
        }

        private T Track<T>(T view) where T : YLFramework.Core.View
        {
            if (view) Own(view.gameObject);
            return view;
        }

        private GameObject Own(GameObject obj)
        {
            if (obj && !ownedObjects.Contains(obj)) ownedObjects.Add(obj);
            return obj;
        }

        private void OwnAsset(Object asset)
        {
            if (asset && !ownedAssets.Contains(asset)) ownedAssets.Add(asset);
        }

        private IEnumerator Await(Func<bool> done, string label)
        {
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (!done() && Time.realtimeSinceStartup < deadline) yield return null;
            if (done()) yield break;
            // Abort only marks these operations complete. Stop the entire test session instead of
            // allowing a still-running producer to mutate the next test after ownership cleanup.
            timedOut = true;
            EditorApplication.isPlaying = false;
            Assert.Fail(label + " exceeded the ten-second realtime timeout; isolated PlayMode is stopping.");
        }

        private IEnumerator AwaitUnity(AsyncOperation operation, string label)
        {
            Assert.That(operation, Is.Not.Null, label);
            yield return Await(() => operation.isDone, label);
        }

        private static object Singleton(Type type) => type.GetField("_instance", StaticPrivate).GetValue(null);
        private static object Field(object target, string name) => target.GetType().GetField(name, InstancePrivate).GetValue(target);

        private static void ResetNewSingleton(Type type)
        {
            EditorApplication.playModeStateChanged -= (Action<PlayModeStateChange>)Delegate.CreateDelegate(
                typeof(Action<PlayModeStateChange>), type.GetMethod("EditorApplication_playModeStateChanged", StaticPrivate));
            type.GetField("_instance", StaticPrivate).SetValue(null, null);
        }

        private sealed class YLCoreIncompatiblePanel : Panel { }

    }
}
#endif
