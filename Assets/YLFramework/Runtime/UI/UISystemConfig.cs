using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Utilities.Extensions;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Services.UI
{
    
    public class UISystemConfig
    {
        //默认使用内置的
        public IWindowLoaderPool WindowLoaderPool { get; private set; } = new WindowLoaderPoolBaseOnRes();
        /// <summary>
        /// 更改UI系统使用的UI窗口加载器
        /// </summary>
        public void WithWindowLoaderPool(IWindowLoaderPool windowLoaderPool)
        {
            WindowLoaderPool = windowLoaderPool;
        }
        
        public UIWindowBase LoadWindow(string assetBundleName, string assetName)
        {
            var windowLoader = WindowLoaderPool.AllocateLoader();
            var windowPrefab = windowLoader.LoadWindowPrefab(assetBundleName, assetName);
            var newWindow = Object.Instantiate(windowPrefab);
            var uiComponent = newWindow.GetComponent<UIWindowBase>();
            uiComponent.MyWindowLoader = windowLoader;
            return uiComponent;
        }
        public void LoadWindowAsync(string assetBundleName, string assetName, Action<UIWindowBase> onLoaded)
        {
            var windowLoader = WindowLoaderPool.AllocateLoader();
            windowLoader.LoadWindowPrefabAsync(assetBundleName, assetName, windowPrefab =>
            {
                var newWindow = Object.Instantiate(windowPrefab);
                var uiComponent = newWindow.GetComponent<UIWindowBase>();
                uiComponent.MyWindowLoader = windowLoader;
                onLoaded?.Invoke(uiComponent);
            });
        }
        
        public static void SetDefaultSizeOfWindow(UIWindowBase window)
        {
            var windowRect = window.transform as RectTransform;
            if (!windowRect) return;
            windowRect.offsetMin = Vector2.zero;
            windowRect.offsetMax = Vector2.zero;
            windowRect.anchoredPosition3D = Vector3.zero;
            windowRect.anchorMin = Vector2.zero;
            windowRect.anchorMax = Vector2.one;
            windowRect.localScale = Vector3.one;
        }
        
    }

    public interface IWindowLoader
    {
        GameObject LoadWindowPrefab(string assetBundleName,string assetName);
        void LoadWindowPrefabAsync(string assetBundleName, string assetName,Action<GameObject> callback);
        void UnLoad();
    }

    public interface IWindowLoaderPool
    {
        IWindowLoader AllocateLoader();
        void RecycleLoader(IWindowLoader loader);
    }

    public abstract class WindowLoaderPoolBase : IWindowLoaderPool
    {
        private readonly Stack<IWindowLoader> _pool = new(20);
        public IWindowLoader AllocateLoader()
        {
            return _pool.Count > 0 ? _pool.Pop() : CreateWindowLoader();
        }
        
        protected abstract IWindowLoader CreateWindowLoader();

        public void RecycleLoader(IWindowLoader loader)
        {
            _pool.Push(loader);
        }
    }
    /// <summary>
    /// 框架内置的UI窗口加载器，其基于框架的资源管理服务
    /// </summary>
    public class WindowLoaderPoolBaseOnRes : WindowLoaderPoolBase
    {
        public class WindowLoaderBaseOnRes : IWindowLoader
        {
            private ResLoader _resLoader;

            public GameObject LoadWindowPrefab(string assetBundleName, string assetName)
            {
                _resLoader ??= ResLoader.Allocate();
                return assetBundleName.IsNotNullAndEmpty()
                    ? _resLoader.LoadSync<GameObject>(assetBundleName, assetName)
                    : _resLoader.LoadSync<GameObject>(assetName);
            }

            public void LoadWindowPrefabAsync(string assetBundleName, string assetName, Action<GameObject> onloaded)
            {
                _resLoader ??= ResLoader.Allocate();
                //资源包名称存在
                if (assetBundleName.IsNotNullAndEmpty())
                {
                    _resLoader.AddToPreLoad<GameObject>(assetBundleName, assetName, (success, res) =>
                    {
                        if (success)
                        {
                            onloaded?.Invoke(res.Asset as GameObject);
                        }
                    });
                    _resLoader.LoadAsync();
                    return;
                }
                //资源包名称不存在
                _resLoader.AddToPreLoad<GameObject>(assetName, (success, res) =>
                {
                    if (success)
                    {
                        onloaded?.Invoke(res.Asset as GameObject);
                    }
                });
                _resLoader.LoadAsync();
            }

            public void UnLoad()
            {
                _resLoader?.RecycleToCache();
                _resLoader = null;
            }
        }

        protected override IWindowLoader CreateWindowLoader()
        {
            return new WindowLoaderBaseOnRes();
        }
    }


    
}