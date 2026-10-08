// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ViewManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections.Generic;
using UnityEngine;
using GameObjectLoader = XFABManager.GameObjectLoader;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{

    internal class ViewManager
    {
        #region 添加 
        internal static void AddView(Module module, View view)
        {
            module.EnsureCanAddObjects();
            if (!module.views.ContainsKey(view.AssetName))
                module.views.Add(view.AssetName, new Dictionary<int, View>());

            if (!module.views[view.AssetName].ContainsKey(view.Id))
            {
                view.Module = module;
                module.views[view.AssetName].Add(view.Id, view);
            }
            else
                Debug.LogWarningFormat("view name:{0} id:{1} 已经存在,请勿重复添加!", view.AssetName, view.Id);


            // 同步 type 为 key
            Type t = view.GetType();

            if (!module.type_views.ContainsKey(t)) 
            {
                module.type_views.Add(t,new List<View>());
            }

            module.type_views[t].Add(view);
        }

        #endregion

        #region 移除

        internal static void RemoveView(Module module, View view)
        {
            // 如果不在运行中,则不需要移除
            if (!ApplicationTool.isPlaying)
                return;

            // 触发移除回调 
            view.Remove();

            // 清除引用(如果是直接拖到场景中的 没有保存引用 所以也不需要清空)
            if (module == null || view == null || string.IsNullOrEmpty(view.AssetName)) return;
            if (!module.views.ContainsKey(view.AssetName)) return;
            if (module.views[view.AssetName] == null) return;
            // 只是单纯的移除 
            if (!module.views[view.AssetName].ContainsKey(view.Id)) return;
             
            // 移除数据 
            if (view.Model != null)
                ModelManager.RemoveModel(module, view.Model);

            //view.Module = null;
            module.views[view.AssetName].Remove(view.Id);

            // 移除(type为key)
            Type t = view.GetType(); 
            if (module.type_views.ContainsKey(t))
            {
                module.type_views[t].Remove(view);
            }
             

            EventManager.TriggerEvent(XFGameFrameworkEvents.ON_REMOVE_VIEW, view); 
        }

        #endregion

        #region 加载

        internal static GameObject LoadGameObject(Module module, string asset_name, Transform parent = null)
        {
            module.EnsureCanAddObjects();
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
            module.EnsureCanAddObjects();
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
            module.EnsureCanAddObjects();
            LoadViewRequest<T> loadView = new LoadViewRequest<T>();
            CoroutineStarter.Start(loadView.LoadView(module, asset_name, parent, sameScene, param));
            return loadView;
        }


        internal static void InitView(Module module,View view,string prefabName,LoadViewArgs loadViewArgs, params object[] param) 
        {
            view.AssetName = prefabName;

            AddView(module, view);
            view.BeforeOnLoaded();
            view.loaded = true;
            view.OnLoaded(param);

            if (loadViewArgs == null)
                loadViewArgs = LoadViewArgs.New();

            view.OnLoaded(loadViewArgs);

            // 回调结束之后 立即回收
            LoadViewArgs.Recycle(loadViewArgs);
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
            module.EnsureCanAddObjects();
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
            module.EnsureCanAddObjects();
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
            List<View> views = ClassObjectPoolManager.Load<List<View>>();
            views.Clear();

            // 调用 关闭方法
            foreach (var item in module.views.Values)
                foreach (var view in item.Values)
                    views.Add(view);

            for (int i = 0; i < views.Count; i++)
                views[i].Close();


            ClassObjectPoolManager.Unload(views);
        }

        internal static void ClearView(Module module, string asset_name, bool clear_base_module)
        {

            List<View> views = ClassObjectPoolManager.Load<List<View>>();
            views.Clear();

            if (module != null && module.views.ContainsKey(asset_name))
            {
                foreach (var item in module.views[asset_name].Values)
                {
                    views.Add(item);
                }
            }

            if (clear_base_module) 
            {
                // 当前模块没有尝试去基础模块中查找
                foreach (var m in ModuleManager.AllBaseModules.Values)
                {
                    if (module == m) continue;

                    if (m != null && m.views.ContainsKey(asset_name))
                    {
                        foreach (var item in m.views[asset_name].Values)
                        {
                            views.Add(item);
                        }
                    }
                }
            }

              
            for (int i = 0; i < views.Count; i++)
                views[i].Close();

            ClassObjectPoolManager.Unload(views);
        }

        internal static void ClearView<T>(Module module,bool clear_base_module) where T : View
        {

            List<View> views = ClassObjectPoolManager.Load<List<View>>();
            views.Clear();


            foreach (var type in module.type_views.Keys)
            {
                if (type == typeof(T) || type.IsSubclassOf(typeof(T)))
                {
                    foreach (var item in module.type_views[type])
                    {
                        T t = item as T;
                        if (!t) continue;
                        views.Add(t);
                    }
                }
            }

            if (clear_base_module) 
            {
                // 基础模块中查找
                foreach (var m in ModuleManager.AllBaseModules.Values)
                {
                    if (m == module) continue;

                    foreach (var type in m.type_views.Keys)
                    {
                        if (type == typeof(T) || type.IsSubclassOf(typeof(T)))
                        {
                            foreach (var item in m.type_views[type])
                            {
                                T t = item as T;
                                if (!t) continue;
                                views.Add(t);
                            }
                        }
                    }
                }
            }



            for (int i = 0; i < views.Count; i++)
                views[i].Close();

            ClassObjectPoolManager.Unload(views);
        }

        #endregion

        #region 查询

        internal static View GetView(Module module, string assetName, int id)
        {
            if (module.views.ContainsKey(assetName) && module.views[assetName].ContainsKey(id))
                return module.views[assetName][id];

            // 当前模块没有尝试去基础模块中查找
            foreach (var m in ModuleManager.AllBaseModules.Values)
            {
                if (module == m) continue;
                if (m.views.ContainsKey(assetName) && m.views[assetName].ContainsKey(id))
                    return m.views[assetName][id];
            }

            return null;
        }

        internal static T GetView<T>(Module module, string assetName, int id) where T : View
        {
            return GetView(module, assetName, id) as T;
        }


        internal static View GetView(Module module, string assetName)
        {
            if (module.views.ContainsKey(assetName))
            {
                if (module.views[assetName].Count == 1)
                { 
                    foreach (var item in module.views[assetName].Values)
                    {
                        return item;
                    }
                }
                else if (module.views[assetName].Count > 1)
                {
                    Exception e = new Exception(string.Format("查询到当前模块中view:{0}有多个实例,请指定id进行查询!", assetName)); 
                    Debug.LogException(e);
                    return null;
                }
            }
              
            // 当前模块没有尝试去基础模块中查找
            foreach (var m in ModuleManager.AllBaseModules.Values)
            {
                if (module == m) continue;

                View view = GetView(m, assetName);

                if (view != null)
                    return view;
            }

            return null;
        }
         
        internal static T GetView<T>(Module module, string assetName ) where T : View
        {
            return GetView(module, assetName ) as T;
        }


        internal static T GetView<T>(Module module) where T : View
        {

            if (module.type_views.ContainsKey(typeof(T))) 
            {
                foreach (var view in module.type_views[typeof(T)])
                {
                    if (!view) continue;
                    return view as T;
                }
            }
              
            // 基础模块中查找
            foreach (var m in ModuleManager.AllBaseModules.Values)
            {
                if (m == module) continue;

                if (m.type_views.ContainsKey(typeof(T)))
                {
                    foreach (var view in m.type_views[typeof(T)])
                    {
                        if (!view) continue;
                        return view as T;
                    }
                }
            }

            return null;
        }

        internal static List<View> GetViews(Module module, string assetName)
        {
            List<View> views = new List<View>();

            if (module.views.ContainsKey(assetName))
            {
                foreach (var item in module.views[assetName].Values)
                    views.Add(item);

            }

            // 基础模块中查找
            foreach (var m in ModuleManager.AllBaseModules.Values)
            {
                if (m == module) continue;
                if (!m.views.ContainsKey(assetName)) continue;
                foreach (var item in m.views[assetName].Values)
                    views.Add(item);
            }

            return views;
        }

        internal static List<T> GetViews<T>(Module module, string assetName) where T : View
        {
            List<T> views = new List<T>();

            if (module.views.ContainsKey(assetName))
            {
                foreach (var item in module.views[assetName].Values)
                {
                    T t = item as T;
                    if (t != null) views.Add(t);
                }
            }

            // 基础模块中查找
            foreach (var m in ModuleManager.AllBaseModules.Values)
            {
                if (m == module) continue;
                if (!m.views.ContainsKey(assetName)) continue;
                foreach (var item in m.views[assetName].Values)
                {
                    T t = item as T;
                    if (t != null) views.Add(t);
                }
            }

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
                throw new Exception("results 不能为空!");

            results.Clear();

            foreach (var type in module.type_views.Keys)
            {
                if (type == typeof(T) || type.IsSubclassOf(typeof(T)))
                {
                    foreach (var item in module.type_views[type])
                    {
                        T t = item as T;
                        if (!t) continue;
                        results.Add(t);
                    }
                }
            }

            // 基础模块中查找
            foreach (var m in ModuleManager.AllBaseModules.Values)
            {
                if (m == module) continue;

                foreach (var type in m.type_views.Keys)
                {
                    if (type == typeof(T) || type.IsSubclassOf(typeof(T)))
                    {
                        foreach (var item in m.type_views[type])
                        {
                            T t = item as T;
                            if (!t) continue;
                            results.Add(t);
                        }
                    }
                }
            }
        }

        #endregion

    }
}
