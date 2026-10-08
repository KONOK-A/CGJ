using System;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// GameObject 预制加载器：内部持有一个 <see cref="ResLoader"/> 负责预制资产。
    /// <para>
    /// Ownership：Prefab asset 由内部 ResLoader 持有（<see cref="ReleaseAsset"/> / 回收时释放）；
    /// Instantiate 出来的实例属于调用方，本类不会自动 Destroy，也不会自动登记到
    /// <see cref="ResLoader.AddObjectToDestroyWhenRecycle"/>。
    /// </para>
    /// </summary>
    public class GameObjectLoader : DisposableObject,IPoolType,IOfPool
    {
        //不让外界通过new来创建对象
        [Obsolete("请使用 GameObjectLoader.Allocate() 获取 GameObjectLoader 对象", true)]
        public GameObjectLoader()
        {
        }
        
        private ResLoader _resLoader;
        
        /// <summary>同步加载并实例化预制；加载失败返回 null。</summary>
        public GameObject LoadSync(string assetName,Transform parent = null)
        {
            var goPre = _resLoader.LoadSync<GameObject>(assetName);
            if (goPre == null)
            {
                return null;
            }
            var go = goPre.InstantiateWithParent(parent);
            return go;
        }

        /// <summary>同步加载并实例化预制（指定 Bundle）；加载失败返回 null。</summary>
        public GameObject LoadSync(string bundleName, string assetName, Transform parent = null)
        {
            var goPre = _resLoader.LoadSync<GameObject>(bundleName,assetName);
            if (goPre == null)
            {
                return null;
            }
            var go = goPre.InstantiateWithParent(parent);
            return go;
        }

        /// <summary>同步加载、实例化并取组件；加载失败返回 null。</summary>
        public T LoadSync<T>(string assetName, Transform parent = null)
        {
            var go = LoadSync(assetName, parent);
            return go != null ? go.GetComponent<T>() : default;
        }

        /// <summary>同步加载、实例化并取组件（指定 Bundle）；加载失败返回 null。</summary>
        public T LoadSync<T>(string bundleName, string assetName, Transform parent = null) where T : Component
        {
            var go = LoadSync(bundleName, assetName, parent);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>
        /// 异步加载预制并实例化；<paramref name="callback"/> 的 result 为加载是否成功，
        /// 加载失败或资源不是 GameObject 时回调的 go 为 null。
        /// </summary>
        /// <param name="assetName">资源名（不指定 Bundle）。</param>
        /// <param name="callback">完成回调。</param>
        /// <param name="parent">实例的父节点。</param>
        public void LoadAsync(string assetName, Action<bool,GameObject> callback, Transform parent = null)
        {
            _resLoader.AddToPreLoad<GameObject>(assetName, (result, res) =>
            {
                GameObject go = null;
                if (result)
                {
                    if (res.Asset is GameObject goPre)
                    {
                        go = goPre.InstantiateWithParent(parent);
                    }
                }
                callback?.Invoke(result,go);
            });
            _resLoader.LoadAsync();
        }

        /// <summary>
        /// 异步加载预制并实例化（指定 Bundle）；<paramref name="callback"/> 的 result 为加载是否成功，
        /// 加载失败或资源不是 GameObject 时回调的 go 为 null。
        /// </summary>
        /// <param name="bundleName">所属 AssetBundle 名。</param>
        /// <param name="assetName">资源名。</param>
        /// <param name="callback">完成回调。</param>
        /// <param name="parent">实例的父节点。</param>
        public void LoadAsync(string bundleName, string assetName, Action<bool, GameObject> callback,
            Transform parent = null)
        {
            _resLoader.AddToPreLoad<GameObject>(bundleName, assetName, (result, res) =>
            {
                GameObject go = null;
                if (result)
                {
                    if (res.Asset is GameObject goPre)
                    {
                        go = goPre.InstantiateWithParent(parent);
                    }
                }

                callback?.Invoke(result, go);
            });
            _resLoader.LoadAsync();
        }

        /// <summary>
        /// 异步加载预制、实例化并取 T 组件；result 为加载是否成功，
        /// 加载失败、资源不是 GameObject 或实例上没有该组件时回调的 c 为 null。
        /// </summary>
        /// <param name="assetName">资源名（不指定 Bundle）。</param>
        /// <param name="callback">完成回调。</param>
        /// <param name="parent">实例的父节点。</param>
        public void LoadAsync<T>(string assetName, Action<bool, T> callback, Transform parent = null)
            where T : Component
        {
            _resLoader.AddToPreLoad<GameObject>(assetName, (result, res) =>
            {
                GameObject go = null;
                if (result)
                {
                    if (res.Asset is GameObject goPre)
                    {
                        go = goPre.InstantiateWithParent(parent);
                    }
                }
                var c = go?.GetComponent<T>();
                callback?.Invoke(result,c);
            });
            _resLoader.LoadAsync();
        }

        /// <summary>
        /// 异步加载预制、实例化并取 T 组件（指定 Bundle）；result 为加载是否成功，
        /// 加载失败、资源不是 GameObject 或实例上没有该组件时回调的 c 为 null。
        /// </summary>
        /// <param name="bundleName">所属 AssetBundle 名。</param>
        /// <param name="assetName">资源名。</param>
        /// <param name="callback">完成回调。</param>
        /// <param name="parent">实例的父节点。</param>
        public void LoadAsync<T>(string bundleName, string assetName, Action<bool, T> callback, Transform parent = null)
            where T : Component
        {
            _resLoader.AddToPreLoad<GameObject>(bundleName, assetName, (result, res) =>
            {
                GameObject go = null;
                if (result)
                {
                    if (res.Asset is GameObject goPre)
                    {
                        go = goPre.InstantiateWithParent(parent);
                    }
                }
                var c = go?.GetComponent<T>();
                callback?.Invoke(result, c);
            });
            _resLoader.LoadAsync();
        }
        
        /// <summary>释放内部 ResLoader（即释放预制资产引用）；实例不受影响。可重复调用。</summary>
        public void ReleaseAsset()
        {
            if(_resLoader == null) return;
            _resLoader.RecycleToCache();
            _resLoader = null;
        }
        
        /// <summary>重置内部 ResLoader：先释放旧引用，再分配一个新的。</summary>
        public void Init()
        {
            if (_resLoader != null)
            {
                ReleaseAsset();
            }
            _resLoader = ResLoader.Allocate();
        }


        #region 对象池相关

        /// <summary>从对象池分配并绑定一个新的 ResLoader。</summary>
        public static GameObjectLoader Allocate()
        {
            var goLoader = SafeObjectPool<GameObjectLoader>.Instance.Allocate();
            goLoader._resLoader = ResLoader.Allocate();
            return goLoader;
        }
        
        /// <summary>Dispose 等价于回收进对象池：先按回收流程释放内部 ResLoader，再执行基类释放。</summary>
        public override void Dispose()
        {
            RecycleToCache();
            base.Dispose();
        }

        public void RecycleToCache()
        {
            SafeObjectPool<GameObjectLoader>.Instance.Recycle(this);
        }

        /// <summary>
        /// 回池时释放内部 ResLoader 并断开引用；池对象复用时不会残留上一轮的 Loader / 资产引用。
        /// </summary>
        public void OnRecycle()
        {
            if (_resLoader != null)
            {
                _resLoader.RecycleToCache();
                _resLoader = null;
            }
        }

        public bool IsRecycled { get; set; }

        #endregion
        
    }
}