// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/AsyncOperation/LoadPanelRequest.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections; 
using UnityEngine;


namespace YLFramework.Core
{

    public class LoadPanelRequest<T> : YLFramework.Foundation.CustomAsyncOperation<LoadPanelRequest<T>> where T : Panel
    {

        public T panel;

        internal IEnumerator LoadPanel(Module module,  string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param)
        {
            parent = PanelManager.GetParent(module, parent, type);

            if (parent == null)
            {
                Completed("parent is null!");
                yield break;
            }

            LoadViewRequest<T> request = ViewManager.LoadViewAsync<T>(module, asset_name, parent,type == UIType.UI, param);
            yield return request;
            if (!string.IsNullOrEmpty(request.error))
                yield break;

            panel = request.view;

            if (panel == null)
            {
                Completed(string.Format("加载panel失败!:{0}",request.error));
                yield break;
            }
            InitPanel(type);
        }

        internal IEnumerator LoadPanel(Module module, string asset_name, UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null)
        {
            parent = PanelManager.GetParent(module, parent, type);

            if (parent == null)
            {
                Completed("parent is null!");
                yield break;
            }

            LoadViewRequest<T> request = ViewManager.LoadViewAsync<T>(module, asset_name, parent, type == UIType.UI, loadViewArgs);
            yield return request;
            if (!string.IsNullOrEmpty(request.error))
                yield break;

            panel = request.view;

            if (panel == null)
            {
                Completed(string.Format("加载panel失败!:{0}", request.error));
                yield break;
            }
            InitPanel(type); 
        }

        private void InitPanel(UIType type) 
        {
            panel.transform.localPosition = Vector3.zero;
            // 设置层级
            panel.UIType = type;

            if (panel.IsMyselfAddCanvas)
            {
                panel.SortingOrder = PanelManager.GetUIOrder(type);
                PanelManager.AddOrder(type, panel, panel.SortingOrder);
            }

            PanelManager.AddPanelQueue(panel);

            Completed();
        }

    }
}

