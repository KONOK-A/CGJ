// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ViewManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections.Generic;
using UnityEngine;
using GameObjectLoader = XFABManager.GameObjectLoader;
using System.Runtime.ExceptionServices;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{

    internal class ViewManager
    {
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
        {
            if (module == null || ReferenceEquals(view, null) || session == null ||
                !ReferenceEquals(view.Module, module) || !ReferenceEquals(view.Session, session) ||
                !module.ownedViews.TryGetValue(view.RegisteredId, out var registered) || !ReferenceEquals(registered, view)) return;
            if (view.IsRemoving) return;
            if (view.IsInitializing || view.IsUnbinding) throw new InvalidOperationException("Cannot close a View during initialization or unbinding.");
            bool notify = view.loaded;
            view.IsRemoving = true;
            view.loaded = false;
            module.viewTransactions++;
            var errors = new List<Exception>();
            bool damaged = view.DiscardOnExit;
            GameObject instance = !destroyed && view ? view.gameObject : null;
            bool backend = view.UsesBackend;
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

            // 撤销当前会话后仍保持Removing，直到实例处理结束，禁止OnDisable重入复活。
            DetachIndexes(module, view, session);
            try
            {
                if (instance)
                {
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
                try { EventManager.TriggerEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, view); }
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
            view.DiscardOnExit = false;
            view.Module = null;
            view.Session = null;
        }

        internal static void ThrowCleanupErrors(List<Exception> errors)
        {
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("View cleanup failed.", errors).Flatten();
        }

        #endregion

        #region 加载

        internal static GameObject LoadGameObject(Module module, string asset_name, Transform parent = null)
        {
            EnsureOwner(module);
            string project_name = module.ProjectName;
            GameObject obj = GameObjectLoader.Load(project_name, asset_name, parent);
            if (obj != null)
            {
                obj.transform.localPosition = Vector3.zero;
                return obj;
            }
            else
            {
                // 尝试从基础模块中加载
                foreach (var item in ModuleManager.AllBaseModules.Values)
                { 
                    obj = GameObjectLoader.Load(item.ProjectName, asset_name, parent);
                    if (obj != null)
                    {
                        obj.transform.localPosition = Vector3.zero;
                        return obj;
                    }
                }
            }

            return null;
        }

        // 加载
        internal static View LoadView(Module module, string asset_name, Transform parent = null, params object[] param)
        {
            GameObject obj = LoadGameObject(module, asset_name, parent);

            if (obj == null) 
            {
                Exception e = new Exception(string.Format("View加载失败,请检查资源名称:{0}是否正确,或资源是否添加到资源模块!", asset_name));
                Debug.LogException(e);
                return null;
            }

            View view = obj.GetComponent<View>();
              
            if (view == null)
            {
                GameObjectLoader.UnLoad(obj); 
                Exception e = new Exception(string.Format("view:{0}加载失败,未查询到View组件!", asset_name));
                Debug.LogException(e);
                return null;
            }
             
            InitView(module,view,asset_name,null,param);

            return view;
        }

        internal static View LoadView(Module module, GameObject prefab, Transform parent = null, params object[] param)
        {
            EnsureOwner(module);
            GameObject obj = GameObjectLoader.Load(prefab, parent);

            if (!obj) 
            {
                Exception e = new Exception(string.Format("View加载失败,请检查资源名称:{0}是否正确,或资源是否添加到资源模块!", prefab.name)); 
                Debug.LogException(e); 
                return null; 
            }

            View view = obj.GetComponent<View>();

            if (view == null)
            {
                GameObjectLoader.UnLoad(obj); 
                Exception e = new Exception(string.Format("view:{0}加载失败,未查询到View组件!", prefab.name));
                Debug.Log(e); 
                return null;
            }
              
            InitView(module, view, prefab.name, null, param);

            return view;
        }

        // 异步加载
        internal static LoadViewRequest<T> LoadViewAsync<T>(Module module, string asset_name, Transform parent = null,bool sameScene = false, params object[] param) where T : View
        {
            EnsureOwner(module);
            LoadViewRequest<T> loadView = new LoadViewRequest<T>();
            CoroutineStarter.Start(loadView.LoadView(module, asset_name, parent, sameScene, param));
            return loadView;
        }


        internal static void InitView(Module module,View view,string prefabName,LoadViewArgs loadViewArgs, params object[] param) 
        {
            if (view.Module != null || view.Session != null || view.IsRemoving)
                throw new InvalidOperationException("Cannot initialize an existing View session.");
            view.AssetName = prefabName;
            AddView(module, view);
            view.UsesBackend = true;
            view.IsInitializing = true;
            module.viewTransactions++;
            try
            {
                view.BeforeOnLoaded();
                view.OnLoaded(param);
                if (loadViewArgs == null) loadViewArgs = LoadViewArgs.New();
                view.OnLoaded(loadViewArgs);
                view.loaded = true;
                view.ResumeEvents();
            }
            catch (Exception initializationError)
            {
                view.IsInitializing = false;
                view.loaded = false;
                view.DiscardOnExit = true;
                try { RemoveView(module, view); }
                catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(initializationError, cleanupError); }
                throw;
            }
            finally
            {
                view.IsInitializing = false;
                module.viewTransactions--;
                if (loadViewArgs != null) LoadViewArgs.Recycle(loadViewArgs);
            }
        }

        #endregion

        #region 加载(With LoadViewArgs)

        // 加载
        internal static T LoadView<T>(Module module, string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            GameObject obj = LoadGameObject(module, asset_name, parent);

            if (obj == null)
            {
                Exception e = new Exception(string.Format("View加载失败,请检查资源名称:{0}是否正确,或资源是否添加到资源模块!", asset_name));
                Debug.LogException(e);
                return null;
            }

            T view = obj.GetComponent<T>();

            if (view == null)
            {
                GameObjectLoader.UnLoad(obj);
                Exception e = new Exception(string.Format("view:{0}加载失败,未查询到View组件!", asset_name));
                Debug.LogException(e);
                return null;
            }
 
            InitView(module, view, asset_name, loadViewArgs);

            return view;
        }

        internal static T LoadView<T>(Module module, GameObject prefab, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            EnsureOwner(module);
            GameObject obj = GameObjectLoader.Load(prefab, parent);

            if (!obj)
            {
                Exception e = new Exception(string.Format("View加载失败,请检查资源名称:{0}是否正确,或资源是否添加到资源模块!", prefab.name));
                Debug.LogException(e);
                return null;
            }

            T view = obj.GetComponent<T>();

            if (view == null)
            {
                GameObjectLoader.UnLoad(obj);
                Exception e = new Exception(string.Format("view:{0}加载失败,未查询到View组件!", prefab.name));
                Debug.Log(e);
                return null;
            }
  
            InitView(module, view, prefab.name, loadViewArgs);

            return view;
        }

        // 异步加载
        internal static LoadViewRequest<T> LoadViewAsync<T>(Module module, string asset_name, Transform parent = null, bool sameScene = false, LoadViewArgs loadViewArgs = null) where T : View
        {
            EnsureOwner(module);
            LoadViewRequest<T> loadView = new LoadViewRequest<T>();
            CoroutineStarter.Start(loadView.LoadView(module, asset_name, parent, sameScene, loadViewArgs));
            return loadView;
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
