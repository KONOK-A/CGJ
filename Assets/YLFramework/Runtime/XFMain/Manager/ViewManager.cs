using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameObjectLoader = XFABManager.GameObjectLoader;
using System.Runtime.ExceptionServices;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{

    internal class ViewManager
    {
        private static readonly HashSet<Module> sceneOwners = new HashSet<Module>();
        private static readonly List<Module> pruneOwners = new List<Module>();

        private static void TrackScene(Module module, View view)
        {
            view.SceneHandle = view.gameObject.scene.handle;
            if (sceneOwners.Count == 0)
            {
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                UpdateManager.AddUpdateEvent(PruneDestroyedSessions);
            }
            sceneOwners.Add(module);
        }

        private static void PruneDestroyedSessions()
        {
            // 从未激活的对象没有OnDestroy。仅扫描受管索引，正常帧不分配View快照。
            pruneOwners.Clear();
            pruneOwners.AddRange(sceneOwners);
            foreach (var module in pruneOwners)
            {
                if (module.viewTransactions != 0) continue;
                try { PruneDestroyed(module); }
                catch (Exception error) { Debug.LogException(error); }
            }
            pruneOwners.Clear();
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            var errors = new List<Exception>();
            foreach (var module in new List<Module>(sceneOwners))
                foreach (var entry in Snapshot(module))
                {
                    var view = entry.Key;
                    // 迁移及DDOL不改变owner；死对象只能使用保存的会话及实例身份。
                    if (view) view.SceneHandle = view.gameObject.scene.handle;
                    else
                    {
                        try { RemoveView(module, view, entry.Value, true); }
                        catch (Exception error) { errors.Add(error); }
                    }
                }
            if (errors.Count != 0)
                Debug.LogException(new AggregateException($"View cleanup after scene unload ({scene.handle}) failed.", errors).Flatten());
        }

        #region 添加 
        internal static void AddView(Module module, View view)
        {
            EnsureOwner(module);
            if (!view) throw new ArgumentNullException(nameof(view));
            if (view.IsRemoving || view.IsUnbinding) throw new InvalidOperationException("Cannot register a View during exit or unbinding.");
            if (view.Module != null || view.Session != null)
            {
                if (!ReferenceEquals(view.Module, module))
                    throw new InvalidOperationException("View is already owned by another Module.");
                if (view.IsInitializing || view.IsRemoving)
                    throw new InvalidOperationException("Cannot register a View during its lifecycle callback.");
                if (HasRegistration(module, view, view.Session)) return;
                throw new InvalidOperationException("View owner and registration do not match.");
            }
            int id = view.GetInstanceID();
            if (module.ownedViews.ContainsKey(id) ||
                (!string.IsNullOrEmpty(view.AssetName) && module.views.TryGetValue(view.AssetName, out var existing) && existing.ContainsKey(id)))
                throw new InvalidOperationException($"View ID {id} is already registered.");
            Type type = view.GetType();
            if (module.type_views.TryGetValue(type, out var typed) && typed.Exists(item => ReferenceEquals(item, view)))
                throw new InvalidOperationException("View type index already contains this instance.");
            view.RegisteredId = id;
            view.Session = new object();
            view.LastSession = view.Session;
            view.Module = module;
            module.ownedViews.Add(id, view);
            if (!string.IsNullOrEmpty(view.AssetName))
            {
                if (!module.views.TryGetValue(view.AssetName, out var named))
                    module.views.Add(view.AssetName, named = new Dictionary<int, View>());
                named.Add(id, view);
            }
            if (typed == null) module.type_views.Add(type, typed = new List<View>());
            typed.Add(view);
            TrackScene(module, view);
        }

        internal static void EnsureOwner(Module module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            module.EnsureCanAddObjects();
            if (!ReferenceEquals(ModuleManager.GetModule(module.ModuleName), module) ||
                (module.State != ModuleState.Starting && module.State != ModuleState.Running))
                throw new InvalidOperationException("View requires a registered Starting or Running Module.");
        }

        private static bool HasRegistration(Module module, View view, object session)
        {
            if (!(session != null && ReferenceEquals(view.Module, module) && ReferenceEquals(view.Session, session) &&
                module.ownedViews.TryGetValue(view.RegisteredId, out var registered) && ReferenceEquals(registered, view) &&
                (string.IsNullOrEmpty(view.AssetName) || (module.views.TryGetValue(view.AssetName, out var named) &&
                    named.TryGetValue(view.RegisteredId, out var byName) && ReferenceEquals(byName, view))) &&
                module.type_views.TryGetValue(view.GetType(), out var typed))) return false;
            int count = 0;
            foreach (var item in typed)
                if (ReferenceEquals(item, view)) count++;
            return count == 1;
        }

        #endregion

        #region 移除

        internal static void RemoveView(Module module, View view)
        {
            if (ReferenceEquals(view, null)) return;
            RemoveView(module, view, view.Session);
        }

        internal static void RemoveView(Module module, View view, object session, bool destroyed = false)
            => RemoveViewCore(module, view, session, destroyed, false, false);

        private static void RemoveViewCore(Module module, View view, object session, bool destroyed, bool preserveInstance, bool rollback)
        {
            if (module == null || ReferenceEquals(view, null) || session == null ||
                !ReferenceEquals(view.Module, module) || !ReferenceEquals(view.Session, session) ||
                !module.ownedViews.TryGetValue(view.RegisteredId, out var registered) || !ReferenceEquals(registered, view)) return;
            if (view.IsRemoving) return;
            if (view.IsInitializing || view.IsUnbinding || (view.IsAttaching && !rollback))
                throw new InvalidOperationException("Cannot close a View during initialization, attachment or unbinding.");
            bool notify = view.loaded;
            view.IsRemoving = true;
            view.loaded = false;
            module.viewTransactions++;
            var errors = new List<Exception>();
            bool damaged = view.DiscardOnExit;
            bool backend = view.UsesBackend;
            bool nestedBackend = backend && !ReferenceEquals(view.BackendRoot, null);
            GameObject instance = backend ? view.BackendInstance : (!destroyed && view ? view.gameObject : null);
            if (backend && destroyed) damaged = true;
            if (backend)
                foreach (var child in Snapshot(module))
                    if (!ReferenceEquals(child.Key, view) && ReferenceEquals(child.Key.BackendRoot, view) &&
                        ReferenceEquals(child.Key.BackendRootSession, session))
                    {
                        try { RemoveViewCore(module, child.Key, child.Value, !child.Key, !child.Key.UsesBackend, rollback); }
                        catch (Exception error) { errors.Add(error); }
                    }
            try { view.StopEvents(); }
            catch (Exception error) { errors.Add(error); damaged = true; }
            if (notify && view.onViewRemove != null)
                foreach (Action observer in view.onViewRemove.GetInvocationList())
                {
                    try { observer(); }
                    catch (Exception error) { errors.Add(error); }
                }
            try { view.OnRemove(); }
            catch (Exception error) { errors.Add(error); damaged = true; }
            try { view.DetachModel(); }
            catch (Exception error) { errors.Add(error); damaged = true; }
            try { PanelManager.ReleaseView(view); }
            catch (Exception error) { errors.Add(error); }
            damaged |= view.DiscardOnExit;
            if (damaged && !backend && !ReferenceEquals(view.BackendRoot, null) &&
                ReferenceEquals(view.BackendRoot.Session, view.BackendRootSession))
                view.BackendRoot.DiscardOnExit = true;

            // 撤销当前会话后仍保持Removing，直到实例处理结束，禁止OnDisable重入复活。
            DetachIndexes(module, view, session);
            try
            {
                if (instance && !preserveInstance)
                {
                    // 独立取得、挂在另一取得根下的实例仍独立归还，不能留在外层缓存子树中被再次纳管。
                    if (nestedBackend) instance.transform.SetParent(null, true);
                    if (backend && !damaged) GameObjectLoader.UnLoad(instance);
                    else
                    {
                        instance.SetActive(false);
                        if (backend && damaged) UnityEngine.Object.Destroy(instance);
                    }
                }
            }
            catch (Exception error)
            {
                errors.Add(error);
                if (backend && instance)
                {
                    try { UnityEngine.Object.Destroy(instance); }
                    catch (Exception discardError) { errors.Add(discardError); }
                }
            }
            view.IsRemoving = false;
            module.viewTransactions--;
            if (notify)
            {
                try { EventManager.TriggerEventAll(XFGameFrameworkEvents.ON_REMOVE_VIEW, view); }
                catch (Exception error) { errors.Add(error); }
                // 广播可以复用同一组件；不把旧完成钩子派发给已开启过的新会话。
                if (ReferenceEquals(view.LastSession, session))
                {
                    try { view.NotifyClosed(); }
                    catch (Exception error) { errors.Add(error); }
                }
            }
            ThrowCleanupErrors(errors);
        }

        private static void DetachIndexes(Module module, View view, object session)
        {
            if (!ReferenceEquals(view.Session, session) || !ReferenceEquals(view.Module, module)) return;
            if (module.ownedViews.TryGetValue(view.RegisteredId, out var owned) && ReferenceEquals(owned, view))
                module.ownedViews.Remove(view.RegisteredId);
            if (!string.IsNullOrEmpty(view.AssetName) && module.views.TryGetValue(view.AssetName, out var named))
            {
                if (named.TryGetValue(view.RegisteredId, out var registered) && ReferenceEquals(registered, view))
                    named.Remove(view.RegisteredId);
                if (named.Count == 0) module.views.Remove(view.AssetName);
            }
            Type type = view.GetType();
            if (module.type_views.TryGetValue(type, out var typed))
            {
                typed.RemoveAll(item => ReferenceEquals(item, view));
                if (typed.Count == 0) module.type_views.Remove(type);
            }
            view.onViewRemove = null;
            view.AssetName = null;
            view.UsesBackend = false;
            view.BackendInstance = null;
            view.BackendRoot = null;
            view.BackendRootSession = null;
            view.SceneHandle = 0;
            view.DiscardOnExit = false;
            view.Module = null;
            view.Session = null;
            if (module.ownedViews.Count == 0)
            {
                sceneOwners.Remove(module);
                if (sceneOwners.Count == 0)
                {
                    SceneManager.sceneUnloaded -= OnSceneUnloaded;
                    UpdateManager.RemoveUpdateEvent(PruneDestroyedSessions);
                }
            }
        }

        internal static void ThrowCleanupErrors(List<Exception> errors)
        {
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("View cleanup failed.", errors).Flatten();
        }

        #endregion

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
            LoadViewArgs args, object[] param, bool named, bool argsClaimed = false, UIType? panelType = null) where T : View
        {
            // 接管失败不进入finally，不能归还另一笔操作正在使用的参数。
            if (args != null && !argsClaimed) args.Claim();
            GameObject instance = null;
            bool initializing = false;
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
                    Exception resourceError = null;
                    foreach (var project in ModuleManager.ViewResourceProjects(module))
                    {
                        try { prefab = XFABManager.AssetBundleManager.LoadAsset<GameObject>(project, assetName); }
                        catch (Exception error) { resourceError = error; }
                        if (prefab) break;
                    }
                    if (!prefab) throw new InvalidOperationException($"View prefab not found: {assetName}.", resourceError);
                }
                else
                {
                    if (!prefab) throw new ArgumentNullException(nameof(prefab));
                    assetName = prefab.name;
                }
                ValidateView<T>(prefab);
                // 后端可能先触发Awake/OnEnable；取得实例也属于同步事务，不能在此关闭owner。
                module.viewTransactions++;
                transaction = true;
                instance = GameObjectLoader.Load(prefab, parent);
                if (!instance) throw new InvalidOperationException($"View instance load failed: {assetName}.");
                var view = ValidateView<T>(instance);
                EnsureOwner(module);
                if (!ReferenceEquals(parent, null) && !parent)
                    throw new ArgumentException("View parent was destroyed during acquisition.", nameof(parent));
                instance.transform.localPosition = Vector3.zero;
                initializing = true;
                InitializeHierarchy(module, new List<View>(instance.GetComponentsInChildren<View>(true)), view, assetName, args, param, panelType: panelType);
                return view;
            }
            catch (Exception error)
            {
                if (instance && !initializing)
                {
                    try { DiscardInstance(instance); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
            finally
            {
                if (transaction) module.viewTransactions--;
                if (args != null && !argsClaimed) args.Release();
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

        // 异步加载
        internal static LoadViewRequest<T> LoadViewAsync<T>(Module module, string asset_name, Transform parent = null,bool sameScene = false, params object[] param) where T : View
        {
            return StartAsync<T>(module, asset_name, parent, null, param);
        }

        internal static LoadViewRequest<T> StartAsync<T>(Module module, string assetName, Transform parent, LoadViewArgs args,
            object[] param, bool argsClaimed = false, UIType? panelType = null) where T : View
        {
            if (args != null && !argsClaimed) args.Claim();
            LoadViewRequest<T> request;
            try
            {
                EnsureOwner(module);
                if (string.IsNullOrWhiteSpace(assetName)) throw new ArgumentException("View asset name is required.", nameof(assetName));
                if (!ReferenceEquals(parent, null) && !parent) throw new ArgumentException("View parent has been destroyed.", nameof(parent));
                FindBackendRoot(module, parent);
                request = new LoadViewRequest<T>(module, assetName, parent, args, param, panelType);
            }
            catch (Exception error)
            {
                if (args != null && !argsClaimed)
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
            LoadViewArgs args, object[] param, Action validate, UIType? panelType = null) where T : View
        {
            ValidateView<T>(prefab);
            GameObject instance = null;
            bool initializing = false;
            module.viewTransactions++;
            try
            {
                validate();
                FindBackendRoot(module, parent);
                instance = GameObjectLoader.Load(prefab, parent);
                if (!instance) throw new InvalidOperationException($"View instance load failed: {assetName}.");
                validate();
                var view = ValidateView<T>(instance);
                if (!parent && instance.scene != scene) SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.localPosition = Vector3.zero;
                initializing = true;
                InitializeHierarchy(module, new List<View>(instance.GetComponentsInChildren<View>(true)), view, assetName, args, param, validate, panelType);
                return view;
            }
            catch (Exception error)
            {
                if (instance && !initializing)
                {
                    try { DiscardInstance(instance); }
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
            View backendRoot = null, bool backend = false, Action validate = null, UIType? panelType = null)
        {
            if (!view) throw new ArgumentNullException(nameof(view));
            if (view.Module != null || view.Session != null || view.IsRemoving)
                throw new InvalidOperationException("Cannot initialize an existing View session.");
            object session = null;
            GameObject instance = view.gameObject;
            bool transaction = false;
            bool defaultArgs = false;
            try
            {
                EnsureOwner(module);
                view.AssetName = prefabName;
                AddView(module, view);
                session = view.Session;
                view.UsesBackend = backend;
                view.BackendInstance = backend ? instance : null;
                view.BackendRoot = backendRoot;
                view.BackendRootSession = backendRoot != null ? backendRoot.Session : null;
                view.IsInitializing = true;
                module.viewTransactions++;
                transaction = true;
                if (view is Panel panel) panel.UIType = panelType ?? PanelManager.GetUIType(panel.transform.parent);
                view.BeforeOnLoaded();
                validate?.Invoke();
                ValidateInitialization(module, view, session);
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
                view.ResumeEvents();
                validate?.Invoke();
                ValidateInitialization(module, view, session);
            }
            catch (Exception initializationError)
            {
                view.IsInitializing = false;
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
                view.IsInitializing = false;
                if (transaction) module.viewTransactions--;
                if (defaultArgs) args.Release();
            }
        }

        private static void ValidateInitialization(Module module, View view, object session)
        {
            if (!view || !HasRegistration(module, view, session) || !view.HasValidModelBinding ||
                (view is Panel panel && !panel.Canvas))
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
            LoadViewArgs args, object[] param, Action validate = null, UIType? panelType = null)
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
                        root ? outerRoot : roots[view], root, validate, root ? panelType : null);
                    added.Add(new KeyValuePair<View, object>(view, view.Session));
                }
                foreach (var entry in added) ValidateInitialization(module, entry.Key, entry.Value);
                validate?.Invoke();
            }
            catch (Exception error)
            {
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

        // Panel入口在解析父节点前已接管参数，并负责自己的finally归还。
        internal static T LoadViewClaimed<T>(Module module, string assetName, Transform parent, LoadViewArgs args,
            object[] param = null, UIType? panelType = null) where T : View
            => LoadViewSync<T>(module, assetName, null, parent, args, param, true, true, panelType);

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


        #region 清空

        /// <summary>
        /// 清空某一个模块的所有View
        /// </summary>
        /// <param name="module"></param>
        internal static void ClearView(Module module)
        {
            ClearSessions(module, Snapshot(module));
        }

        internal static void ClearView(Module module, string asset_name)
        {
            if (asset_name == null) throw new ArgumentNullException(nameof(asset_name));
            var sessions = Snapshot(module);
            sessions.RemoveAll(item => item.Key.AssetName != asset_name);
            ClearSessions(module, sessions);
        }

        internal static void ClearView<T>(Module module) where T : View
        {
            var sessions = Snapshot(module);
            sessions.RemoveAll(item => !(item.Key is T));
            ClearSessions(module, sessions);
        }

        internal static List<KeyValuePair<View, object>> Snapshot(Module module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            var sessions = new List<KeyValuePair<View, object>>(module.ownedViews.Count);
            foreach (var view in module.ownedViews.Values)
                sessions.Add(new KeyValuePair<View, object>(view, view.Session));
            // 取得根先退出，子会话不单独改变缓存子树的activeSelf。
            sessions.Sort((left, right) => right.Key.UsesBackend.CompareTo(left.Key.UsesBackend));
            return sessions;
        }

        private static void ClearSessions(Module module, List<KeyValuePair<View, object>> sessions)
        {
            var errors = new List<Exception>();
            foreach (var session in sessions)
            {
                try { RemoveView(module, session.Key, session.Value, !session.Key); }
                catch (Exception error) { errors.Add(error); }
            }
            ThrowCleanupErrors(errors);
        }

        private static void PruneDestroyed(Module module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            List<KeyValuePair<View, object>> dead = null;
            foreach (var view in module.ownedViews.Values)
                if (!view)
                {
                    if (dead == null) dead = new List<KeyValuePair<View, object>>();
                    dead.Add(new KeyValuePair<View, object>(view, view.Session));
                }
            if (dead != null) ClearSessions(module, dead);
        }

        #endregion

        #region 查询

        internal static View GetView(Module module, string assetName, int id)
        {
            if (assetName == null) throw new ArgumentNullException(nameof(assetName));
            PruneDestroyed(module);
            return module.views.TryGetValue(assetName, out var named) && named.TryGetValue(id, out var view) ? view : null;
        }

        internal static T GetView<T>(Module module, string assetName, int id) where T : View
        {
            return Cast<T>(GetView(module, assetName, id));
        }

        internal static T GetView<T>(Module module, int id) where T : View
        {
            PruneDestroyed(module);
            return module.ownedViews.TryGetValue(id, out var view) ? Cast<T>(view) : null;
        }

        private static T Cast<T>(View view) where T : View
        {
            if (view == null) return null;
            if (view is T typed) return typed;
            throw new InvalidCastException($"View {view.GetType().FullName} cannot be queried as {typeof(T).FullName}.");
        }

        private static View Single(IEnumerable<View> views)
        {
            View result = null;
            foreach (var view in views)
            {
                if (result != null) throw new InvalidOperationException("Multiple Views match; specify an ID.");
                result = view;
            }
            return result;
        }


        internal static View GetView(Module module, string assetName)
        {
            if (assetName == null) throw new ArgumentNullException(nameof(assetName));
            PruneDestroyed(module);
            return module.views.TryGetValue(assetName, out var named) ? Single(named.Values) : null;
        }
         
        internal static T GetView<T>(Module module, string assetName ) where T : View
        {
            return Cast<T>(GetView(module, assetName));
        }


        internal static T GetView<T>(Module module) where T : View
        {
            PruneDestroyed(module);
            return module.type_views.TryGetValue(typeof(T), out var typed) ? (T)Single(typed) : null;
        }

        internal static List<View> GetViews(Module module, string assetName)
        {
            if (assetName == null) throw new ArgumentNullException(nameof(assetName));
            PruneDestroyed(module);
            return module.views.TryGetValue(assetName, out var named) ? new List<View>(named.Values) : new List<View>();
        }

        internal static List<T> GetViews<T>(Module module, string assetName) where T : View
        {
            var views = new List<T>();
            foreach (var view in GetViews(module, assetName))
                if (view is T typed) views.Add(typed);
            return views;
        }


        internal static List<T> GetViews<T>(Module module) where T : View
        {
            List<T> views = new List<T>(); 
            GetViewsNonAlloc<T>(module, views); 
            return views;
        }


        internal static void GetViewsNonAlloc<T>(Module module,List<T> results) where T : View 
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            PruneDestroyed(module);
            results.Clear();
            foreach (var view in module.ownedViews.Values)
                if (view is T typed) results.Add(typed);
        }

        #endregion

    }
}
