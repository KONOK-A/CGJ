using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YLFramework.Core.Pool;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源加载器：业务使用资源服务的主要入口。
    /// <para>
    /// Ownership 契约：
    /// 1. Loader 持有 <c>_recordedRes</c> 中每个资源的一次引用（同一资源重复加载只追加监听，不重复计数）；
    ///    未 Ready 的资源同时进入预加载队列并计入加载计数。
    /// 2. <see cref="ReleaseRes(string)"/> / <see cref="ReleaseAllRes"/> 释放 Loader 的持有，重复释放安全（未持有则忽略）。
    /// 3. <see cref="RecycleToCache"/> 通过池身份校验后，由 <see cref="OnRecycle"/> 先销毁
    ///    <see cref="AddObjectToDestroyWhenRecycle"/> 登记的对象，再调用 <see cref="ReleaseAllRes"/>；
    ///    成功清理后才可复用，失败对象由池隔离，不自动重试释放。
    /// </para>
    /// </summary>
    public class ResLoader : DisposableObject,IResLoader,IPoolType,IOfPool
    {
        //不让外界通过new来创建对象
        [Obsolete("请使用 ResLoader.Allocate() 获取 ResLoader 对象", true)]
        public ResLoader()
        {
        }
        
        private readonly List<IRes> _recordedRes = new();
        /// <summary>
        /// 等待加载的资源列表
        /// </summary>
        private readonly LinkedList<IRes> _waitToLoadResList = new();
        private Action _listener;
        private int _loadingCount;
        private LinkedList<CallBackWrap> _callBackWraps = new();
        /// <summary>
        /// 记录外界希望在加载器回收时同步回收的对象
        /// </summary>
        private List<Object> _objectsPreToUnload;
        private readonly Dictionary<string,Sprite> _cachedSpriteDict = new();

        /// <summary>整体加载进度 0~1（按记录资源平均，含正在加载资源的自身进度）。</summary>
        public float Progress
        {
            get
            {
                if (_waitToLoadResList.Count == 0)
                {
                    return 1;
                }
                //计算每一份资源的加载进度占总进度的比率
                var unit = 1.0f / _recordedRes.Count;
                var currentValue = unit * (_recordedRes.Count - _loadingCount); 
                var currentLoadingRes = _waitToLoadResList.First;
                while (currentLoadingRes != null)
                {
                    currentValue += unit * currentLoadingRes.Value.LoadProgress;
                    currentLoadingRes = currentLoadingRes.Next;
                }
                return currentValue;
            }
        }


        #region 创建与回收

        /// <summary>
        /// 从对象池获取一个资源加载器。
        /// </summary>
        public static ResLoader Allocate()
        {
            return SafeObjectPool<ResLoader>.Instance.Allocate();
        }

        /// <summary>
        /// 回收本池借出的加载器：身份校验后进入回收转换态，再销毁登记对象并释放资源引用。
        /// 重复或转换期回收不会再次清理；不属于当前池的对象被拒绝且不产生清理副作用。
        /// </summary>
        public void RecycleToCache()
        {
            SafeObjectPool<ResLoader>.Instance.Recycle(this);
        }

        /// <summary>Dispose 等价于回收进对象池。</summary>
        public override void Dispose()
        {
            RecycleToCache();
            base.Dispose();
        }

        #endregion

        #region 同步加载方式

        /// <summary>
        /// 同步加载并返回资源对象。加载失败时返回的 res 可能为 Waiting / Asset 为 null；
        /// 没有可用创建器时返回 null。不回收传入的 key（调用方持有）。
        /// </summary>
        public IRes LoadResSync(ResKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key), "ResLoader.LoadResSync 的 key 不能为 null。");
            }

            AddToPreLoad(key);
            LoadSync();
            var res = ServiceManager.Instance.GetRes(key,false);
            if (res != null) return res;
            Debug.LogError("#YLFramework: Fail To Load Res:" + key);
            return null;
        }
        private void LoadSync()
        {
            //同步加载所有预加载资源
            while (_waitToLoadResList.Count > 0)
            {
                var first = _waitToLoadResList.First.Value;
                if (first == null) { return; }
                //调用Res自己的同步加载方法
                if (first.LoadSync()) { }
                //移除待加载项
                _loadingCount -= 1;
                _waitToLoadResList.RemoveFirst();
            }
        }

        /// <summary>
        /// 同步加载并返回资产本体；加载失败或没有可用创建器时返回 null。
        /// 不回收传入的 ResKey（调用方负责回收）。
        /// </summary>
        public Object LoadAssetSync(ResKey resKey)
        {
            Object asset;
            if (resKey.AssetType == typeof(Sprite))
            {
                if (AssetBundleHelper.SimulationMode)
                {
                    if (_cachedSpriteDict.TryGetValue(resKey.AssetName, out var sprite))
                    {
                        return sprite;
                    }
                    resKey.AssetType = typeof(Texture2D);
                    var texture = LoadResSync(resKey)?.Asset as Texture2D;
                    resKey.AssetType = typeof(Sprite);

                    Sprite newSprite = null;
                    if (texture != null)
                    {
#if UNITY_EDITOR
                        //模拟模式下按 TextureImporter 的轴心与像素密度创建 Sprite，避免与真机模式下表现不一致
                        var assetPath = UnityEditor.AssetDatabase.GetAssetPath(texture);
                        var importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;
                        newSprite = importer != null
                            ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                importer.spritePivot, importer.spritePixelsPerUnit)
                            : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                Vector2.one * 0.5f);
#else
                        newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                            Vector2.one * 0.5f);
#endif
                    }
                    _cachedSpriteDict.Add(resKey.AssetName, newSprite);
                    return newSprite;
                }
            }
            //没有可用创建器（或加载失败）时 LoadResSync 返回 null，这里保持返回 null 而不是抛异常
            asset = LoadResSync(resKey)?.Asset;
            return asset;
        }
        
        #endregion

        #region 异步加载方式
        
        /// <summary>
        /// 异步加载资源(使用前通过AddToPreLoad方法将加载请求加到预加载队列)。
        /// </summary>
        /// <param name="listener">全部预加载资源完成后的总回调，只会调用一次。</param>
        public void LoadAsync(Action listener = null)
        {
            _listener = listener;
            DoLoadAsync();
            
        }
        //实际进行加载逻辑
        private void DoLoadAsync()
        {
            if (_loadingCount == 0)
            {
                if (_listener != null)
                {
                    var callBack = _listener;
                    _listener = null;
                    callBack?.Invoke();
                }
            }
            var nextNode = _waitToLoadResList.First;
            while (nextNode != null)
            {
                var currentNode = nextNode;
                var res = currentNode.Value;
                nextNode = currentNode.Next;
                if (res.DependResLoadIsFinish())
                {
                    //开始加载了就移除
                    _waitToLoadResList.Remove(currentNode);
                    if (res.State != ResState.Ready)
                    {
                        //每个资源注册加载完成事件
                        res.RegisterOnResLoadDoneEvent(OnResLoadFinish);
                        //资源自身进行异步加载
                        res.LoadAsync();
                    }
                    else
                    {
                        _loadingCount -= 1;
                    }
                }
                else if (IsDependResLoadFailed(res))
                {
                    //依赖已失败：不再无限等待，移除并按加载失败向上级联。
                    //重入流程可能已处理过该节点（已从链表移除），此时直接跳过。
                    if (currentNode.List == null)
                    {
                        continue;
                    }
                    _waitToLoadResList.Remove(currentNode);
                    if (_loadingCount > 0)
                    {
                        _loadingCount -= 1;
                    }
                    if (res is Res concreteRes)
                    {
                        concreteRes.NotifyDependBundleLoadFailed();
                    }
                }
            }
        }

        /// <summary>
        /// 判断资源是否存在已失败的依赖 Bundle：依赖为 Waiting 且不在本 Loader 的等待队列中，
        /// 说明它已经尝试加载并失败；依赖无法创建（Creator 缺失等）同样视为失败。
        /// 由 <see cref="IRes.DependResLoadIsFinish"/> 为 false 时进一步判断。
        /// </summary>
        private bool IsDependResLoadFailed(IRes res)
        {
            var depends = res.GetDependBundleList();
            if (depends == null || depends.Count == 0)
            {
                return false;
            }

            for (var i = depends.Count - 1; i >= 0; i--)
            {
                var key = ResKey.Allocate(depends[i], null, typeof(AssetBundle));
                var dependRes = ServiceManager.Instance.GetRes(key);
                key.RecycleToCache();
                if (dependRes == null)
                {
                    return true;
                }

                if (dependRes.State == ResState.Waiting && !_waitToLoadResList.Contains(dependRes))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnResLoadFinish(bool result, IRes res)
        {
            if (_loadingCount > 0)
            {
                _loadingCount -= 1;
            }
            DoLoadAsync();
            if (_loadingCount == 0)
            {
                RemoveAllCallbacks(false);
                _listener?.Invoke();
            }
        }
        
        #endregion

        #region 释放资源方法

        /// <summary>
        /// 按资源名释放加载器的持有（同名不同资源时释放查询命中的第一个）；重复释放安全。
        /// </summary>
        public void ReleaseRes(string resName)
        {
            ReleaseResInternal(null, resName);
        }

        /// <summary>
        /// 按 Bundle + 资源名释放加载器的持有；重复释放安全。
        /// </summary>
        public void ReleaseRes(string bundleName, string resName)
        {
            ReleaseResInternal(bundleName, resName);
        }

        /// <summary>
        /// 释放逻辑：清理模拟模式 Sprite 缓存、移出预加载队列、解除注册并释放引用。
        /// 只有确实记录在本 Loader 中的资源才会被 Release，因此重复调用不会二次扣减引用计数。
        /// </summary>
        private void ReleaseResInternal(string bundleName, string resName)
        {
            if (string.IsNullOrEmpty(resName))
            {
                return;
            }

            if (AssetBundleHelper.SimulationMode)
            {
                if (_cachedSpriteDict.TryGetValue(resName, out var sprite))
                {
                    Object.Destroy(sprite);
                    _cachedSpriteDict.Remove(resName);
                }
            }
            var resKey = ResKey.Allocate(resName, bundleName);
            var res = ServiceManager.Instance.GetRes(resKey);
            resKey.RecycleToCache();
            if(res == null) return;
            if (_waitToLoadResList.Remove(res))
            {
                _loadingCount -= 1;
                if (_loadingCount == 0)
                {
                    _listener = null;
                }
            }
            if (_recordedRes.Remove(res))
            {
                res.UnRegisterOnResLoadDoneEvent(OnResLoadFinish);
                res.Release();
                ServiceManager.Instance.ClearOnUpdate();
            }
        }

        /// <summary>
        /// 批量按资源名释放。
        /// </summary>
        public void ReleaseRes(params string[] resNames)
        {
            if (resNames == null || resNames.Length == 0)
            {
                return;
            }

            for (var i = resNames.Length - 1; i >= 0; i--)
            {
                ReleaseRes(resNames[i]);
            }
        }

        /// <summary>
        /// 释放该加载器持有的全部资源引用并清空内部状态；重复调用安全。
        /// </summary>
        public void ReleaseAllRes()
        {
            if (AssetBundleHelper.SimulationMode)
            {
                foreach (var sprite in _cachedSpriteDict.Values)
                {
                    Object.Destroy(sprite);
                }
                _cachedSpriteDict.Clear();
            }

            _listener = null;
            _loadingCount = 0;
            _waitToLoadResList.Clear();
            if (_recordedRes.Count > 0)
            {
                //确保首先删除的是AB，这样能对Asset的卸载做优化
                _recordedRes.Reverse();
                for (var i = _recordedRes.Count - 1; i >= 0; i--)
                {
                    _recordedRes[i].UnRegisterOnResLoadDoneEvent(OnResLoadFinish);
                    _recordedRes[i].Release();
                }
                _recordedRes.Clear();
                if (!ServiceManager.IsApplicationQuit)
                {
                    ServiceManager.Instance.ClearOnUpdate();
                }
            }
            RemoveAllCallbacks(true);
        }

        /// <summary>
        /// 卸载全部"实例化对象"，并释放加载器对这些资源的持有。
        /// 仅 <see cref="IRes.UnloadInstantiate"/> 返回 true 的资源会被释放；
        /// 未加载完成的资源同时移出等待队列并修正加载计数；重复调用安全。
        /// </summary>
        /// <param name="flag">传给 <see cref="IRes.UnloadInstantiate"/> 的参数（对 AssetBundle 资源即 Unload 的 flag）。</param>
        public void UnloadAllInstantiateRes(bool flag)
        {
            if (_recordedRes.Count == 0)
            {
                return;
            }

            //倒序遍历按索引删除；先 Remove 再按旧索引访问会错位/越界。
            for (var i = _recordedRes.Count - 1; i >= 0; --i)
            {
                var res = _recordedRes[i];
                if (!res.UnloadInstantiate(flag))
                {
                    continue;
                }

                _recordedRes.RemoveAt(i);
                if (res.State != ResState.Ready)
                {
                    //非 Ready 的资源必然计入 _loadingCount：Waiting 在等待队列，Loading 已在加载中。
                    _waitToLoadResList.Remove(res);
                    _loadingCount -= 1;
                }

                RemoveCallBack(res, true);
                res.UnRegisterOnResLoadDoneEvent(OnResLoadFinish);
                res.Release();
            }
            ServiceManager.Instance.ClearOnUpdate();
        }

        #endregion

        /// <summary>
        /// 登记一个随加载器回收而销毁的实例对象（通常是 Instantiate 出来的 GameObject）。
        /// null 会被忽略，同一对象重复登记只销毁一次；对象被外部提前销毁不影响回收。
        /// 注意：<see cref="ReleaseAllRes"/> 不会销毁该列表，由池回收清理 <see cref="OnRecycle"/> 处理。
        /// </summary>
        public void AddObjectToDestroyWhenRecycle(Object obj)
        {
            //Unity 的 == 会同时过滤 null 与已销毁对象
            if (obj == null)
            {
                return;
            }

            _objectsPreToUnload ??= new List<Object>();
            for (var i = 0; i < _objectsPreToUnload.Count; i++)
            {
                if (_objectsPreToUnload[i] == obj)
                {
                    return;
                }
            }
            _objectsPreToUnload.Add(obj);
        }

        /// <summary>
        /// 添加一个资源加载请求到预加载资源列表，同时会将所有依赖项加到预加载列表。
        /// 同一资源重复添加只补充监听，不会重复引用计数。
        /// 没有可用创建器时立即以 (false, null) 回调 listener，避免异步调用方永久等待。
        /// </summary>
        /// <param name="resKey">资源查询键；调用方负责回收。</param>
        /// <param name="listener">单个资源的加载完成回调，可为 null。</param>
        /// <param name="lastOrder">true 追加到等待队列尾部，false 插入头部（更早被调度）。</param>
        public void AddToPreLoad(ResKey resKey, Action<bool, IRes> listener = null,bool lastOrder = true)
        {
            if (resKey == null)
            {
                throw new ArgumentNullException(nameof(resKey), "ResLoader.AddToPreLoad 的 resKey 不能为 null。");
            }

            var res = FindResInArray(_recordedRes, resKey);
            if (res != null)
            {
                if (listener != null)
                {
                    AddResLoadListenerRecord(res,listener);
                    res.RegisterOnResLoadDoneEvent(listener);
                }
                return;
            }
            res = ServiceManager.Instance.GetRes(resKey,true);
            if (res == null)
            {
                //创建失败：直接通知失败，调用方不会收到"数据永远不完整"的等待。
                listener?.Invoke(false, null);
                return;
            }
            if (listener != null)
            {
                AddResLoadListenerRecord(res,listener);
                res.RegisterOnResLoadDoneEvent(listener);
            }
            //无论该资源是否加载完成，都需要添加对该资源的依赖项的引用
            var depends = res.GetDependBundleList();
            if (depends != null)
            {
                foreach (var key in depends.Select(depend => ResKey.Allocate(depend, null, typeof(AssetBundle))))
                {
                    AddToPreLoad(key);
                    key.RecycleToCache();
                }
            }

            AddResToArray(res, lastOrder);
        }

        /// <summary>
        /// 登记资源并 Retain 一次；未 Ready 的资源进入等待队列并计入加载计数。
        /// 已登记过的资源不会重复添加 / 重复 Retain。
        /// </summary>
        private void AddResToArray(IRes res, bool lastOrder)
        {
            var key = ResKey.Allocate(res.AssetName, res.OwnerBundleName, res.AssetType);
            //再次确认当前加载器保存的Res中没有该资源
            var oldRes = FindResInArray(_recordedRes, key);
            key.RecycleToCache();
            if (oldRes != null)
            {
                //已经有了，无需再添加
                return;
            }
            _recordedRes.Add(res);
            //初次添加到一个加载器的资源，进行一次引用记录
            res.Retain();
            //资源还未加载完毕，记录一下
            if (res.State != ResState.Ready)
            {
                _loadingCount += 1;
                if (lastOrder)
                {
                    _waitToLoadResList.AddLast(res);
                }
                else
                {
                    _waitToLoadResList.AddFirst(res);
                }
            }
        }

        #region 事件注册相关

        private void AddResLoadListenerRecord(IRes res, Action<bool, IRes> listener)
        {
            _callBackWraps ??= new LinkedList<CallBackWrap>();
            _callBackWraps.AddLast(new CallBackWrap(listener, res));
        }

        private void RemoveCallBack(IRes res, bool release)
        {
            if (_callBackWraps != null)
            {
                var current = _callBackWraps.First;
                LinkedListNode<CallBackWrap> next = null;
                while (current != null)
                {
                    next = current.Next;
                    if (current.Value.IsRes(res))
                    {
                        if (release)
                        {
                            current.Value.Release();
                        }
                        _callBackWraps.Remove(current);
                    }
                    current = next;
                }
            }
        }
        private void RemoveAllCallbacks(bool release)
        {
            if (_callBackWraps != null)
            {
                var count = _callBackWraps.Count;
                while (count > 0)
                {
                    count -= 1;
                    if (release)
                    {
                        _callBackWraps.Last.Value.Release();
                    }
                    _callBackWraps.RemoveLast();
                }
            }
        }
        
        #endregion
        
        #region 工具方法

        private static IRes FindResInArray(List<IRes> list, ResKey resKey)
        {
            if (list == null)
            {
                return null;
            }

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (resKey.Match(list[i]))
                {
                    return list[i];
                }
            }
            //没有找到匹配项
            return null;
        }

        #endregion

        #region 内部类

        /// <summary>
        /// 记录注册到某个 Res 上的一次加载回调（listener + 目标 res），
        /// 释放时按目标 res 反注册对应 listener。
        /// </summary>
        public class CallBackWrap
        {
            private readonly Action<bool, IRes> _listener;
            private readonly IRes _res;

            public CallBackWrap(Action<bool, IRes> listener, IRes res)
            {
                _listener = listener;
                _res = res;
            }

            public void Release()
            {
                _res.UnRegisterOnResLoadDoneEvent(_listener);
            }

            public bool IsRes(IRes res)
            {
                return res.AssetName == _res.AssetName;
            }
        }

        #endregion

        /// <summary>
        /// 对象池回收回调：先销毁登记的实例对象，再释放全部资源引用并清空内部状态。
        /// </summary>
        public void OnRecycle()
        {
            // 清理只在池的 Returning 门禁内执行；先摘列表，避免清理回调重复遍历。
            var objectsToUnload = _objectsPreToUnload;
            _objectsPreToUnload = null;
            if (objectsToUnload != null)
            {
                foreach (var obj in objectsToUnload)
                {
                    //Unity 对象可能在登记后被外部销毁，这里用 Unity 的 null 语义过滤
                    if (obj != null)
                    {
                        ResUnloadHelper.DestroyObject(obj);
                    }
                }
                objectsToUnload.Clear();
            }

            ReleaseAllRes();
        }

        /// <summary>是否已被对象池回收。</summary>
        public bool IsRecycled { get; set; }
    }
}
