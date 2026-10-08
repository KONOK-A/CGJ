// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/AsyncOperation/LoadViewRequest.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections; 
using UnityEngine;
using GameObjectLoader = XFABManager.GameObjectLoader;
using GameObjectLoadRequest = XFABManager.GameObjectLoadRequest;

namespace YLFramework.Core
{


    public class LoadViewRequest<T> : YLFramework.Foundation.CustomAsyncOperation<LoadViewRequest<T>> where T : View
    {

        public T view;

        internal IEnumerator LoadView(Module module , string asset_name, Transform parent = null,bool sameScene = false, params object[] param)
        {

            string project_name = module.ProjectName;

            GameObjectLoadRequest request = GameObjectLoader.LoadAsync(project_name, asset_name, parent);
            yield return request;
            if (!string.IsNullOrEmpty(request.error))
            {
                // 加载失败，尝试从基础模块中加载
                foreach (var item in ModuleManager.AllBaseModules.Values)
                {
                    project_name = module.ProjectName;
                    request = GameObjectLoader.LoadAsync(project_name, asset_name, parent, sameScene);
                    yield return request;
                    if (string.IsNullOrEmpty(request.error))
                        break;
                }

                if (!string.IsNullOrEmpty(request.error)) 
                {
                    Completed(request.error);
                    yield break;
                }
            }
            GameObject obj = request.Obj;

            if (obj == null)
            { 
                Completed(string.Format("资源加载失败 project_name:{0} asset_name:{1} 请检查资源是否存在!", project_name, asset_name));
                yield break;
            }

            view = obj.GetComponent<T>();

            if (view == null)
            {
                GameObjectLoader.UnLoad(obj);  
                Completed(string.Format("obj:{0} 未查询到View组件!", obj.name));
                yield break;
            }
            obj.transform.localPosition = Vector3.zero;
  
            ViewManager.InitView(module, view, asset_name, null, param);

            Completed();
        }

        internal IEnumerator LoadView(Module module, string asset_name, Transform parent = null, bool sameScene = false, LoadViewArgs loadViewArgs = null)
        {

            string project_name = module.ProjectName;

            GameObjectLoadRequest request = GameObjectLoader.LoadAsync(project_name, asset_name, parent);
            yield return request;
            if (!string.IsNullOrEmpty(request.error))
            {
                // 加载失败，尝试从基础模块中加载
                foreach (var item in ModuleManager.AllBaseModules.Values)
                {
                    project_name = module.ProjectName;
                    request = GameObjectLoader.LoadAsync(project_name, asset_name, parent, sameScene);
                    yield return request;
                    if (string.IsNullOrEmpty(request.error))
                        break;
                }

                if (!string.IsNullOrEmpty(request.error))
                {
                    Completed(request.error);
                    yield break;
                }
            }
            GameObject obj = request.Obj;

            if (obj == null)
            {
                Completed(string.Format("资源加载失败 project_name:{0} asset_name:{1} 请检查资源是否存在!", project_name, asset_name));
                yield break;
            }

            view = obj.GetComponent<T>();

            if (view == null)
            {
                GameObjectLoader.UnLoad(obj);
                Completed(string.Format("obj:{0} 未查询到View组件!", obj.name));
                yield break;
            }
            obj.transform.localPosition = Vector3.zero; 

            ViewManager.InitView(module, view, asset_name, loadViewArgs);

            Completed();
        }



    }
}
