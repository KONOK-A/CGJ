using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Core.Pool;
using System.Runtime.ExceptionServices;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;
using UpdateManager = YLFramework.Foundation.UpdateManager;
using YLFramework.Events;
using YLFramework.Core.Singleton;

namespace YLFramework.Core
{

    internal partial class ViewManager
    {
        #region 加载

        internal static View LoadView(Module module, string asset_name, Transform parent = null, params object[] param)
        {
            return LoadViewLegacy<View>(module, asset_name, parent, param);
        }

        internal static View LoadView(Module module, GameObject prefab, Transform parent = null, params object[] param)
        {
            return LoadViewLegacy<View>(module, prefab, parent, param);
        }

        internal static T LoadViewLegacy<T>(Module module, string assetName, Transform parent, object[] param) where T : View
            => LoadViewSync<T>(module, assetName, null, parent, null, param, true);

        internal static T LoadViewLegacy<T>(Module module, GameObject prefab, Transform parent, object[] param) where T : View
            => LoadViewSync<T>(module, null, prefab, parent, null, param, false);

        private static T LoadViewSync<T>(Module module, string assetName, GameObject prefab, Transform parent,
            LoadViewArgs args, object[] param, bool named) where T : View
        {
            // 接管失败不进入finally，不能归还另一笔操作正在使用的参数。
            if (args != null) args.Claim();
            GameObject instance = null;
            CorePrefabLease prefabLease = null;
            CoreInstanceLease instanceLease = null;
            bool transaction = false;
            try
            {
                EnsureOwner(module);
                if (!ReferenceEquals(parent, null) && !parent)
                    throw new ArgumentException("View parent has been destroyed.", nameof(parent));
                FindBackendRoot(module, parent);
                if (named)
                {
                    if (string.IsNullOrWhiteSpace(assetName)) throw new ArgumentException("View asset name is required.", nameof(assetName));
                    var provider = CoreResourceAccess.RequireProvider(CoreResourceAccess.Provider);
                    Exception resourceError = null;
                    foreach (var project in ModuleManager.ViewResourceProjects(module))
                    {
                        try { prefabLease = provider.AcquirePrefab(project, assetName); }
                        catch (Exception error) when (CoreResourceAccess.CanFallback(error)) { resourceError = error; }
                        if (prefabLease != null) { prefab = prefabLease.Prefab; break; }
                    }
                    if (!prefab) throw new InvalidOperationException($"View prefab not found: {assetName}.", resourceError);
                }
                else
                {
                    if (!prefab) throw new ArgumentNullException(nameof(prefab));
                    assetName = prefab.name;
                }
                var sourceView = ValidateView<T>(prefab);
                if (sourceView is Panel) throw new InvalidOperationException("根 View 是 Panel，请使用 Module.LoadPanel/LoadPanelAsync。");
                // 后端可能先触发Awake/OnEnable；取得实例也属于同步事务，不能在此关闭owner。
                module.viewTransactions++;
                transaction = true;
                var scene = parent ? parent.gameObject.scene : SceneManager.GetActiveScene();
                instanceLease = named ? prefabLease.Instantiate(parent, scene) : AcquireDirectInstance(prefab, parent, scene);
                instance = instanceLease.Instance;
                if (!instance) throw new InvalidOperationException($"View instance load failed: {assetName}.");
                var view = ValidateView<T>(instance);
                EnsureOwner(module);
                if (!ReferenceEquals(parent, null) && !parent)
                    throw new ArgumentException("View parent was destroyed during acquisition.", nameof(parent));
                instance.transform.localPosition = Vector3.zero;
                InitializeHierarchy(module, new List<View>(instance.GetComponentsInChildren<View>(true)), view, assetName, args, param, instanceLease: instanceLease);
                return view;
            }
            catch (Exception error)
            {
                if (instanceLease != null)
                {
                    try { instanceLease.Discard(); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
            finally
            {
                prefabLease?.Dispose();
                if (transaction) module.viewTransactions--;
                if (args != null) args.Release();
            }
        }

        private static T ValidateView<T>(GameObject obj) where T : View
        {
            var view = obj.GetComponent<View>();
            if (!view) throw new InvalidOperationException($"Object {obj.name} has no root View component.");
            if (view is T typed) return typed;
            throw new InvalidCastException($"View {view.GetType().FullName} cannot be loaded as {typeof(T).FullName}.");
        }

        private static void DiscardInstance(GameObject instance)
        {
            try { instance.SetActive(false); }
            finally { UnityEngine.Object.Destroy(instance); }
        }

        private static CoreInstanceLease AcquireDirectInstance(GameObject prefab, Transform parent, Scene scene, bool panelCheckout = false)
        {
            var pool = GameObjectPool.Instance;
            string key = "YL.Core.Direct/" + prefab.GetInstanceID();
            var instance = pool.Allocate(key, parent);
            if (panelCheckout && instance)
            {
                var previous = instance.GetComponent<View>();
                if (!previous || !previous.DirectSource || !ReferenceEquals(previous.DirectSource, prefab))
                { DiscardInstance(instance); instance = null; }
            }
            if (!instance) instance = UnityEngine.Object.Instantiate(prefab, parent);
            if (!instance) throw new InvalidOperationException("Direct Prefab checkout failed.");
            var lease = new DirectInstanceLease(pool, key, instance);
            try
            {
                if (!parent && instance.scene != scene) SceneManager.MoveGameObjectToScene(instance, scene);
                if (panelCheckout) instance.GetComponent<View>().DirectSource = prefab;
                instance.SetActive(true);
                return lease;
            }
            catch { lease.Discard(); throw; }
        }

        internal static bool IsDirectLease(CoreInstanceLease lease) => lease is DirectInstanceLease;

        private sealed class DirectInstanceLease : CoreInstanceLease
        {
            private readonly GameObjectPool pool;
            private readonly string key;
            private GameObject instance;
            internal DirectInstanceLease(GameObjectPool pool, string key, GameObject instance)
            { this.pool = pool; this.key = key; this.instance = instance; }
            public override GameObject Instance => instance;
            public override bool IsValid => instance;
            public override void Return()
            {
                var owned = instance;
                instance = null;
                if (!owned) return;
                if (pool) pool.Recycle(key, owned);
                else DiscardInstance(owned);
            }
            public override void Discard()
            {
                var owned = instance;
                instance = null;
                if (owned) DiscardInstance(owned);
            }
        }

        // 异步加载
        internal static LoadViewRequest<T> LoadViewAsync<T>(Module module, string asset_name, Transform parent = null,bool sameScene = false, params object[] param) where T : View
        {
            return StartAsync<T>(module, asset_name, parent, null, param);
        }

        internal static LoadViewRequest<T> StartAsync<T>(Module module, string assetName, Transform parent, LoadViewArgs args,
            object[] param, GameObject directPrefab = null) where T : View
        {
            if (args != null) args.Claim();
            LoadViewRequest<T> request;
            try
            {
                EnsureOwner(module);
                if (string.IsNullOrWhiteSpace(assetName)) throw new ArgumentException("View asset name is required.", nameof(assetName));
                if (!ReferenceEquals(parent, null) && !parent) throw new ArgumentException("View parent has been destroyed.", nameof(parent));
                FindBackendRoot(module, parent);
                request = new LoadViewRequest<T>(module, assetName, parent, args, param, directPrefab);
            }
            catch (Exception error)
            {
                if (args != null)
                {
                    try { args.Release(); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
            try
            {
                if (CoroutineStarter.Start(request.Run()) == null && !request.isDone)
                    request.FailToStart(new InvalidOperationException("View loading requires an active coroutine host."));
            }
            catch (Exception error) { request.FailToStart(error); }
            return request;
        }

        internal static T CompleteAsync<T>(Module module, GameObject prefab, string assetName, Transform parent, Scene scene,
            LoadViewArgs args, object[] param, Action validate, CorePrefabLease prefabLease = null, PanelManager.PanelOperation panelOperation = null) where T : View
        {
            var sourceView = ValidateView<T>(prefab);
            if (sourceView is Panel && panelOperation == null)
                throw new InvalidOperationException("根 View 是 Panel，请使用 Module.LoadPanel/LoadPanelAsync。");
            GameObject instance = null;
            CoreInstanceLease instanceLease = null;
            module.viewTransactions++;
            try
            {
                validate();
                FindBackendRoot(module, parent);
                instanceLease = prefabLease != null ? prefabLease.Instantiate(parent, scene) : AcquireDirectInstance(prefab, parent, scene, panelOperation != null);
                instance = instanceLease.Instance;
                if (!instance) throw new InvalidOperationException($"View instance load failed: {assetName}.");
                if (!instanceLease.IsValid) throw new InvalidOperationException("View 实例租约已失效。");
                validate();
                var view = ValidateView<T>(instance);
                panelOperation?.ValidateAcquired(view);
                if (!parent && instance.scene != scene) SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.localPosition = Vector3.zero;
                InitializeHierarchy(module, new List<View>(instance.GetComponentsInChildren<View>(true)), view, assetName, args, param, validate, instanceLease, panelOperation);
                return view;
            }
            catch (Exception error)
            {
                panelOperation?.MarkFailing();
                if (instanceLease != null)
                {
                    try { instanceLease.Discard(); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
            finally { module.viewTransactions--; }
        }


        internal static void InitView(Module module,View view,string prefabName,LoadViewArgs loadViewArgs, params object[] param) 
        {
            if (loadViewArgs != null) loadViewArgs.Claim();
            try
            {
                if (!view) throw new ArgumentNullException(nameof(view));
                if (view.Module != null || view.Session != null || view.IsRemoving || view.IsAttaching)
                    throw new InvalidOperationException("Cannot initialize an existing View session.");
                InitializeHierarchy(module, new List<View>(view.GetComponentsInChildren<View>(true)), view, prefabName, loadViewArgs, param);
            }
            finally { if (loadViewArgs != null) loadViewArgs.Release(); }
        }

        private static void InitializeView(Module module, View view, string prefabName, LoadViewArgs args, object[] param,
            View backendRoot = null, bool backend = false, Action validate = null, CoreInstanceLease instanceLease = null, PanelManager.PanelOperation panelOperation = null, Action onFailure = null)
        {
            if (!view) throw new ArgumentNullException(nameof(view));
            if (view.Module != null || view.Session != null || view.IsRemoving)
                throw new InvalidOperationException("Cannot initialize an existing View session.");
            object session = null;
            GameObject instance = view.gameObject;
            bool transaction = false;
            bool defaultArgs = false;
            var previousInitialization = InitializingView;
            try
            {
                EnsureOwner(module);
                view.AssetName = prefabName;
                AddView(module, view);
                session = view.Session;
                view.UsesBackend = backend;
                view.BackendInstance = backend ? instance : null;
                view.BackendLease = backend ? instanceLease : null;
                view.BackendRoot = backendRoot;
                view.BackendRootSession = backendRoot != null ? backendRoot.Session : null;
                view.IsInitializing = true;
                InitializingView = new KeyValuePair<View, object>(view, session);
                module.viewTransactions++;
                transaction = true;
                if (view is Panel panel)
                {
                    panel.DefersEvents = true;
                    panel.InitialOpen = panelOperation;
                    panel.Configure(panelOperation != null ? panelOperation.Root : PanelManager.RequireAttachedRoot(panel),
                        panelOperation != null ? panelOperation.Settings : panel.Defaults);
                }
                view.BeforeOnLoaded();
                validate?.Invoke();
                ValidateInitialization(module, view, session);
                if (view is Panel committing) committing.InitialOpen?.BeginCommit?.Invoke();
                view.OnLoaded(param ?? Array.Empty<object>());
                validate?.Invoke();
                ValidateInitialization(module, view, session);
                if (args == null)
                {
                    args = LoadViewArgs.New();
                    args.Claim();
                    defaultArgs = true;
                }
                args.BeginRead();
                try { view.OnLoaded(args); }
                finally { args.EndRead(); }
                validate?.Invoke();
                ValidateInitialization(module, view, session);
                view.loaded = true;
                if (view is Panel opened)
                {
                    opened.Open(args);
                    validate?.Invoke();
                    ValidateInitialization(module, view, session);
                    opened.DefersEvents = false;
                }
                view.ResumeEvents();
                validate?.Invoke();
                ValidateInitialization(module, view, session);
            }
            catch (Exception initializationError)
            {
                onFailure?.Invoke();
                view.IsInitializing = false;
                if (view is Panel ended) { ended.DefersEvents = false; ended.InitialOpen = null; }
                view.loaded = false;
                view.DiscardOnExit = true;
                try
                {
                    if (session != null) RemoveViewCore(module, view, session, !view, !backend, true);
                    else
                    {
                        view.AssetName = null;
                        if (backend && view) DiscardInstance(view.gameObject);
                    }
                }
                catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(initializationError, cleanupError); }
                // 组件可能已销毁而取得根对象仍存活；不能因fake-null跳过根实例处理。
                if (backend && !view && instance)
                {
                    try { DiscardInstance(instance); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(initializationError, cleanupError); }
                }
                throw;
            }
            finally
            {
                if (ReferenceEquals(InitializingView.Key, view) && ReferenceEquals(InitializingView.Value, session))
                    InitializingView = previousInitialization;
                view.IsInitializing = false;
                if (view is Panel completedPanel) { completedPanel.DefersEvents = false; completedPanel.InitialOpen = null; }
                if (transaction) module.viewTransactions--;
                if (defaultArgs) args.Release();
            }
        }

        private static void ValidateInitialization(Module module, View view, object session)
        {
            if (!view || !HasRegistration(module, view, session) || !view.HasValidModelBinding ||
                (view is Panel panel && (!panel.Canvas || !PanelManager.IsPreparedValid(panel))))
                throw new InvalidOperationException("View initialization invalidated its instance, registration, Model binding or Panel Canvas.");
        }

        internal static void AttachView(Module module, View view, LoadViewArgs args)
        {
            if (args != null) args.Claim();
            try { InitializeHierarchy(module, new List<View> { view }, null, null, args, null); }
            finally { if (args != null) args.Release(); }
        }

        internal static List<View> AttachViews(Module module, GameObject root)
        {
            EnsureOwner(module);
            if (!root) throw new ArgumentNullException(nameof(root));
            var views = new List<View>(root.GetComponentsInChildren<View>(true));
            InitializeHierarchy(module, views, null, null, null, null);
            return views;
        }

        internal static List<View> AttachViews(Module module, Scene scene)
        {
            EnsureOwner(module);
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("View scene must be loaded.", nameof(scene));
            var views = new List<View>();
            foreach (var root in scene.GetRootGameObjects()) views.AddRange(root.GetComponentsInChildren<View>(true));
            InitializeHierarchy(module, views, null, null, null, null);
            return views;
        }

        private static View FindBackendRoot(Module module, Transform target)
        {
            for (var parent = target; parent; parent = parent.parent)
            {
                var ancestor = parent.GetComponent<View>();
                if (!ancestor) continue;
                if (ancestor.IsRemoving || ancestor.IsAttaching || ancestor.IsInitializing)
                    throw new InvalidOperationException("Cannot attach under a View lifecycle transaction.");
                if (!ancestor.UsesBackend) continue;
                if (!ReferenceEquals(ancestor.Module, module)) throw new InvalidOperationException("Acquired hierarchy belongs to another Module.");
                return ancestor;
            }
            return null;
        }

        private static void InitializeHierarchy(Module module, List<View> views, View acquiredRoot, string assetName,
            LoadViewArgs args, object[] param, Action validate = null, CoreInstanceLease instanceLease = null, PanelManager.PanelOperation panelOperation = null)
        {
            var added = new List<KeyValuePair<View, object>>();
            var roots = new Dictionary<View, View>();
            View outerRoot = null;
            bool guarded = false;
            try
            {
                EnsureOwner(module);
                if (acquiredRoot != null) outerRoot = FindBackendRoot(module, acquiredRoot.transform);
                foreach (var view in views)
                {
                    if (!view) throw new ArgumentNullException(nameof(views));
                    if (!view.gameObject.scene.IsValid() || !view.gameObject.scene.isLoaded)
                        throw new ArgumentException("Only live scene instances can be attached.", nameof(views));
                    if (view.IsAttaching || view.IsInitializing || view.IsRemoving || view.IsUnbinding)
                        throw new InvalidOperationException("Cannot attach during a View lifecycle transaction.");
                    if (view is Panel attached)
                        PanelManager.RequireAttachedRoot(attached).GetLayer(ReferenceEquals(view, acquiredRoot) && panelOperation != null ?
                            panelOperation.Settings.Layer : attached.Session != null ? attached.EffectiveSettings.Layer : attached.Defaults.Layer);
                    if (view.Session != null || view.Module != null)
                    {
                        if (!ReferenceEquals(view.Module, module) || !view.IsLoaded || !HasRegistration(module, view, view.Session))
                            throw new InvalidOperationException("View already has a conflicting or incomplete registration.");
                    }
                    roots.Add(view, acquiredRoot != null ? acquiredRoot : FindBackendRoot(module, view.transform));
                }
                foreach (var view in views) view.IsAttaching = true;
                module.viewTransactions++;
                guarded = true;
                foreach (var view in views)
                {
                    if (view.Session != null) continue;
                    bool root = ReferenceEquals(view, acquiredRoot);
                    InitializeView(module, view, root ? assetName : null,
                        root || views.Count == 1 ? args : null, root ? param : null,
                          root ? outerRoot : roots[view], root, validate, root ? instanceLease : null, root ? panelOperation : null,
                          panelOperation != null ? (Action)panelOperation.MarkFailing : null);
                    added.Add(new KeyValuePair<View, object>(view, view.Session));
                }
                foreach (var entry in added) ValidateInitialization(module, entry.Key, entry.Value);
                validate?.Invoke();
            }
            catch (Exception error)
            {
                panelOperation?.MarkFailing();
                // 批次没有提交，不派发成功退出通知；已有绑定与activeSelf保持不变。
                foreach (var entry in added) entry.Key.loaded = false;
                if (acquiredRoot != null) acquiredRoot.DiscardOnExit = true;
                for (int i = added.Count - 1; i >= 0; i--)
                {
                    var entry = added[i];
                    try { RemoveViewCore(module, entry.Key, entry.Value, !entry.Key, !entry.Key.UsesBackend, true); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                if (acquiredRoot && acquiredRoot.Session == null)
                {
                    try { DiscardInstance(acquiredRoot.gameObject); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
            finally
            {
                if (guarded)
                {
                    foreach (var view in views) view.IsAttaching = false;
                    module.viewTransactions--;
                }
            }
        }

        #endregion

        #region 加载(With LoadViewArgs)

        // 加载
        internal static T LoadView<T>(Module module, string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return LoadViewSync<T>(module, asset_name, null, parent, loadViewArgs, null, true);
        }

        internal static T LoadView<T>(Module module, GameObject prefab, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return LoadViewSync<T>(module, null, prefab, parent, loadViewArgs, null, false);
        }

        // 异步加载
        internal static LoadViewRequest<T> LoadViewAsync<T>(Module module, string asset_name, Transform parent = null, bool sameScene = false, LoadViewArgs loadViewArgs = null) where T : View
        {
            return StartAsync<T>(module, asset_name, parent, loadViewArgs, null);
        }

        #endregion



    }
}
