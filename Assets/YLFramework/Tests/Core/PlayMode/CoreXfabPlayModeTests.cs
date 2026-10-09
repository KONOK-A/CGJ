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
        private CoreClassPoolScope classPool;
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
            timedOut = false;
            ownsScene = singleSceneTest = false;
            ownedObjects.Clear();
            ownedAssets.Clear();
            classPool = new CoreClassPoolScope();
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
            YLFramework.Core.EventManager.TriggerEvent(YLFramework.Core.EventManager.StringToHash(YLCoreSmokeView.ProbeEvent));
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
            var args = NewArgs();
            args.SetInt("token", 17);
            var first = Track(module.LoadPanel<YLCoreSmokePanel>(nameof(YLCoreSmokePanel),
                YLFramework.Core.UIType.UI, parent, loadViewArgs: args));
            AssertPanel(first, 17, 1);
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
            Assert.That(((IDictionary)Field(Singleton(typeof(YLFramework.Core.PanelManager)), "uiOrders")).Contains(first), Is.False);
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
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(completeState, Is.True);
            Assert.That(request.panel, Is.SameAs(first));
            Assert.That(first.Canvas, Is.SameAs(canvasOnPanel));
            Assert.That(first.GetComponents<Canvas>(), Has.Length.EqualTo(1));
            AssertPanel(first, 29, 2);
            module.Close();
            Assert.That(first.RemovedCount, Is.EqualTo(2));
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.Empty);
            Assert.That(((IDictionary)Field(Singleton(typeof(YLFramework.Core.PanelManager)), "uiOrders")).Contains(first), Is.False);
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
            // Missing names hit the upstream null-prefab/hanging Assets branch. A valid prefab with
            // the wrong component exercises the public failure contract without an unhandled coroutine exception.
            var request = module.LoadViewAsync<YLCoreSmokePanel>(nameof(YLCoreSmokeView), parent, loadViewArgs: null);
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
            Assert.That(module.GetViews(nameof(YLCoreSmokeView)), Is.Empty);
            Assert.That(module.GetViews<YLCoreSmokePanel>(), Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultStartupSceneUsesSingleAndRealFixtureEntry()
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
            yield return StartModule(true);
            var loaded = SceneManager.GetSceneByPath(YLCoreSmokeModule.ScenePath);
            Assert.That(loaded.IsValid() && loaded.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(loaded));
            Assert.That(SceneManager.GetSceneByPath(YLCoreSmokeModule.HarnessPath).isLoaded, Is.False);
            Assert.That(module.SceneReadyAtStart, Is.True, "Startup validates the loaded scene before OnStart.");
            Assert.That(module.RowReadyAtStart, Is.True);
            yield return AssertSceneEntry(loaded);
        }

        private IEnumerator StartModule(bool withScene = false)
        {
            module = new YLCoreSmokeModule { StartWithScene = withScene };
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
            var orders = (IDictionary)Field(Singleton(typeof(YLFramework.Core.PanelManager)), "uiOrders");
            Assert.That(orders.Contains(panel), Is.True);
            Assert.That(orders[panel], Is.EqualTo(panel.SortingOrder));
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
            if (((IDictionary)Field(manager, "uiOrders")).Count != 0 ||
                ((IDictionary)Field(manager, "dontDestroyUIOrders")).Count != 0 || canvases.Count != 0 ||
                ((IList)Field(manager, "panelQueues")).Count != 0) return;
            var type = typeof(YLFramework.Core.PanelManager);
            SceneManager.sceneLoaded -= (UnityEngine.Events.UnityAction<Scene, LoadSceneMode>)Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction<Scene, LoadSceneMode>), type.GetMethod("OnSceneLoaded", StaticPrivate));
            // Instance never registers LateUpdate; this fixture never toggles MultiPanelListener.
            ResetNewSingleton(type);
        }

        private YLFramework.Core.LoadViewArgs NewArgs()
        {
            var args = YLFramework.Core.LoadViewArgs.New();
            classPool.Track(args);
            return args;
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

    }
}
#endif
