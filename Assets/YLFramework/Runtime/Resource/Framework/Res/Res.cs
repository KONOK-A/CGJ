using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Pool;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源对象基类。
    /// <para>
    /// 生命周期：由 <see cref="ResFactory"/> 通过对象池创建 → Waiting → 同步/异步加载进入 Loading →
    /// 成功后 Ready 并通知加载回调；失败回到 Waiting 并以 false 通知。
    /// 引用计数归零（<see cref="OnZeroRef"/>）或 ServiceManager 判定零引用时调用 <see cref="ReleaseRes"/>
    /// 卸载缓存资产并回到 Waiting，随后由 ServiceManager 从资源表移除并回收本对象。
    /// </para>
    /// <para>
    /// 注意区分：<see cref="SimpleRc.Release"/> 只操作引用计数，<see cref="ReleaseRes"/> 负责卸载资产；
    /// <see cref="Asset"/> 是缓存资产本体，调用方 Instantiate 出来的实例由调用方 / Loader 负责销毁。
    /// </para>
    /// </summary>
    public class Res : SimpleRc, IRes, IOfPool, IPoolType
    {
        protected Res(string assetName)
        {
            IsRecycled = false;
            AssetName = assetName;
        }

        protected Res()
        {
            IsRecycled = false;
        }

        private ResState _resState = ResState.Waiting;
        private event Action<bool, IRes> _onResLoadDoneEvent;

        /// <inheritdoc />
        public Object Asset { get; protected set; }
        /// <inheritdoc />
        public string AssetName { get; protected set; }

        /// <inheritdoc />
        public Type AssetType { get; internal set; }
        /// <inheritdoc />
        public virtual string OwnerBundleName { get;internal set; }

        /// <inheritdoc />
        public ResState State
        {
            get => _resState;
            protected set
            {
                _resState = value;
                if (_resState == ResState.Ready)
                {
                    //通知资源加载完毕事件
                    NotifyResLoadDoneEvent(true);
                }
            }
        }

        /// <inheritdoc />
        public float LoadProgress
        {
            get
            {
                return _resState switch
                {
                    ResState.Loading => CalculateLoadProgress(),
                    ResState.Ready => 1,
                    _ => 0
                };
            }
        }

        /// <summary>
        /// 计算 Loading 期间的加载进度；默认返回 0，具体实现可按底层请求进度重写。
        /// </summary>
        protected virtual float CalculateLoadProgress()
        {
            return 0;
        }

        /// <summary>
        /// 通知加载完成并清空事件：每次加载结束只通知一次，失败用 false。
        /// </summary>
        private void NotifyResLoadDoneEvent(bool result)
        {
            if (_onResLoadDoneEvent != null)
            {
                _onResLoadDoneEvent.Invoke(result, this);
                //此次资源加载事件完成，回调清空
                _onResLoadDoneEvent = null;
            }
        }

        /// <inheritdoc />
        public void RegisterOnResLoadDoneEvent(Action<bool, IRes> listener)
        {
            if (listener == null) return;
            if (_resState == ResState.Ready)
            {
                listener(true, this);
                return;
            }

            _onResLoadDoneEvent += listener;
        }

        /// <inheritdoc />
        public void UnRegisterOnResLoadDoneEvent(Action<bool, IRes> listener)
        {
            if (listener == null) return;
            if (_onResLoadDoneEvent == null) return;
            _onResLoadDoneEvent -= listener;
        }

        /// <summary>
        /// 资源加载失败：回到 Waiting 并以 false 通知加载完成回调（不保留失败状态）。
        /// </summary>
        protected void OnResLoadFail()
        {
            //加载失败，重回等待加载事件
            _resState = ResState.Waiting;
            //通知加载完成事件，结果为失败
            NotifyResLoadDoneEvent(false);
        }

        /// <summary>
        /// 检测资源能否加载(加载中或加载完毕时不可再加载)
        /// </summary>
        protected bool CheckLoadAble()
        {
            return _resState == ResState.Waiting;
        }

        /// <summary>
        /// 引用该 Res 的依赖资源：加载期间对依赖 Bundle 各 Retain 一次，
        /// 加载结束后由 <see cref="UnHoldDependRes"/> 成对释放。
        /// </summary>
        protected void HoldDependRes()
        {
            var dependBundles = GetDependBundleList();
            if (dependBundles == null || dependBundles.Count == 0)
            {
                return;
            }

            for (var i = dependBundles.Count - 1; i >= 0; i--)
            {
                //拼接一个AssetBundle的资源Key
                var resKey = ResKey.Allocate(dependBundles[i], null, typeof(AssetBundle));
                var dependRes = ServiceManager.Instance.GetRes(resKey);
                resKey.RecycleToCache();
                if (dependRes != null)
                {
                    //该依赖项被当前Res引用（加载）了，引用计数加一
                    dependRes.Retain();
                }
            }
        }

        /// <summary>
        /// 释放该 Res 对依赖资源的引用（与 <see cref="HoldDependRes"/> 成对）。
        /// </summary>
        protected void UnHoldDependRes()
        {
            var dependBundles = GetDependBundleList();
            if (dependBundles == null || dependBundles.Count == 0)
            {
                return;
            }

            for (int i = dependBundles.Count - 1; i >= 0; i--)
            {
                var resKey = ResKey.Allocate(dependBundles[i], null, typeof(AssetBundle));
                var dependRes = ServiceManager.Instance.GetRes(resKey);
                resKey.RecycleToCache();
                //释放该Res对依赖资源的引用
                if (dependRes != null)
                {
                    dependRes.Release();
                }
            }
        }

        /// <inheritdoc />
        public virtual bool UnloadInstantiate(bool flag)
        {
            return false;
        }

        /// <inheritdoc />
        public virtual bool LoadSync()
        {
            return false;
        }

        /// <inheritdoc />
        public virtual void LoadAsync()
        {
        }

        /// <inheritdoc />
        public virtual List<string> GetDependBundleList()
        {
            return null;
        }

        /// <inheritdoc />
        public bool DependResLoadIsFinish()
        {
            var dependBundles = GetDependBundleList();
            if (dependBundles == null || dependBundles.Count == 0)
            {
                return true;
            }

            for (var i = dependBundles.Count - 1; i >= 0; i--)
            {
                var resKey = ResKey.Allocate(dependBundles[i], null, typeof(AssetBundle));
                var dependRes = ServiceManager.Instance.GetRes(resKey);
                resKey.RecycleToCache();
                if (dependRes is not { State: ResState.Ready })
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 由 <see cref="ResLoader"/> 在依赖 Bundle 加载失败时调用：按加载失败处理
        /// （回 Waiting 并通知一次 false），避免异步加载因依赖失败而永久挂起。
        /// 仅供同程序集的加载器调用。
        /// </summary>
        internal void NotifyDependBundleLoadFailed()
        {
            OnResLoadFail();
        }

        /// <inheritdoc />
        public bool ReleaseRes()
        {
            if (_resState == ResState.Loading)
            {
                return false;
            }

            if (_resState != ResState.Ready)
            {
                return true;
            }

            OnReleaseRes();
            //状态改回等待加载状态
            _resState = ResState.Waiting;
            _onResLoadDoneEvent = null;
            return true;
        }

        /// <summary>
        /// 卸载缓存资产的默认实现：非 GameObject 资源走 Resources.UnloadAsset；
        /// GameObject 不做 Resources.UnloadAsset（Unity 不允许）。
        /// <para>
        /// 注意：Asset 可能已经被所属 Bundle 的 Unload(true) 销毁（释放顺序为先 Bundle 后 Asset），
        /// 此时 Unity 的 == null 为真但字段仍引用已销毁对象；字段必须无条件清空，
        /// 否则对象池复用时会残留上一生命周期已销毁的 Asset 引用。
        /// </para>
        /// </summary>
        protected virtual void OnReleaseRes()
        {
            if (Asset != null)
            {
                //卸载资源
                ResUnloadHelper.UnLoadRes(Asset);
            }

            //无论资产是否已被销毁，都必须清空引用，避免池复用残留
            Asset = null;
        }

        /// <summary>
        /// 回收到对象池；基类为 no-op，具体实现必须重写并回收到对应池。
        /// 仅应在 ServiceManager 确认对象可回收后调用。
        /// </summary>
        public virtual void RecycleToCache()
        {
        }

        /// <summary>
        /// 对象池回收清理：清空资源名与加载事件；子类重写时应调用基类实现。
        /// </summary>
        public virtual void OnRecycle()
        {
            AssetName = null;
            _onResLoadDoneEvent = null;
        }

        /// <summary>是否已被对象池回收。</summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 引用计数归零：Ready 时立即卸载缓存资产；Loading 时不做处理，
        /// 由 ServiceManager 在加载完成后回收（见 RemoveUnusedRes）。
        /// </summary>
        protected override void OnZeroRef(Object refOwner = null)
        {
            if (_resState == ResState.Loading)
            {
                return;
            }

            //零引用了，直接释放资源引用
            ReleaseRes();
        }
        /// <summary>
        /// 实际进行异步加载资源的任务；基类为空实现，立即调用完成回调，具体实现重写。
        /// </summary>
        /// <param name="finishCallback">加载结束（无论成败）时调用一次。</param>
        public virtual IEnumerator DoLoadAsync(Action finishCallback)
        {
            finishCallback?.Invoke();
            yield break;
        }

        public override string ToString()
        {
            return $"Name:{AssetName}\t State:{State}\t RefCount:{RefCount}";
        }
    }
}
