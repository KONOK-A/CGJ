using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源加载状态。
    /// <para>
    /// 生命周期：Waiting（等待加载 / 上一次加载失败）→ Loading（加载中）→ Ready（可用）；
    /// 加载失败回到 Waiting 并以 false 通知加载完成回调；Loading / Ready 期间不会重复发起加载。
    /// </para>
    /// </summary>
    public enum ResState
    {
        /// <summary>等待加载；也可能是上一次加载失败后的状态。</summary>
        Waiting = 0,
        /// <summary>加载中。</summary>
        Loading,
        /// <summary>加载完成，资源可用。</summary>
        Ready
    }

    /// <summary>
    /// 资源数据（<c>AssetData.AssetResType</c>）的加载方式常量。
    /// 与 AssetBundle 配置文件的序列化字段一一对应，历史编号保持不变，不要重排。
    /// </summary>
    public static class ResLoadType
    {
        /// <summary>AssetBundle 文件本身。</summary>
        public const short AssetBundle   = 0;
        /// <summary>Bundle 内的资产（Prefab / Texture / AudioClip 等）。</summary>
        public const short ABAsset  = 1;
        /// <summary>Bundle 内的场景。</summary>
        public const short ABScene  = 2;
        public const short Internal      = 3;
        /// <summary>网络图片（NetImageRes）。</summary>
        public const short NetImageRes   = 4;
        /// <summary>本地图片（LocalImageRes）。</summary>
        public const short LocalImageRes = 5;
    }

    /// <summary>
    /// 资源对象契约：一个 IRes 对应一份缓存资产（<see cref="Asset"/>）。
    /// 引用计数来自 <see cref="IRefCounter"/>，按帧异步加载来自 <see cref="IEnumeratorTask"/>。
    /// </summary>
    public interface IRes : IRefCounter,IEnumeratorTask
    {
        #region 属性

        /// <summary>缓存资产本体；卸载后为 null。不代表调用方 Instantiate 出来的实例。</summary>
        public Object Asset { get;}
        /// <summary>小写化后的资源名（创建后不变）。</summary>
        public string AssetName { get;}
        /// <summary>资源类型。</summary>
        public Type AssetType { get;}
        /// <summary>所属 AssetBundle 名；非 AB 资源为 null。</summary>
        public string OwnerBundleName { get;}
        /// <summary>当前加载状态。</summary>
        public ResState State { get; }
        /// <summary>加载进度 0~1：Waiting 为 0，Ready 为 1。</summary>
        float LoadProgress { get; }

        #endregion
        
        #region 方法
        
        /// <summary>
        /// 注册加载完成回调：Ready 时立即以 true 回调，否则在加载结束（成功 true / 失败 false）时回调一次。
        /// </summary>
        void RegisterOnResLoadDoneEvent(Action<bool,IRes> listener);
        /// <summary>注销加载完成回调。</summary>
        void UnRegisterOnResLoadDoneEvent(Action<bool,IRes> listener);
        /// <summary>卸载"实例对象"的扩展点；默认返回 false，表示没有需要处理的实例。</summary>
        bool UnloadInstantiate(bool flag);
        /// <summary>同步加载；成功进入 Ready，失败回到 Waiting 并返回 false。</summary>
        public bool LoadSync();
        /// <summary>发起异步加载（经 ServiceManager 协程队列调度）。</summary>
        public void LoadAsync();

        /// <summary>依赖的 AssetBundle 名列表；无依赖返回 null 或空集合。</summary>
        List<string> GetDependBundleList();
        /// <summary>依赖的 AssetBundle 是否全部 Ready；无依赖时返回 true。</summary>
        bool DependResLoadIsFinish();
        /// <summary>
        /// 卸载缓存资产并回到 Waiting（幂等）；Loading 阶段返回 false 表示暂不能卸载。
        /// 不会销毁调用方 Instantiate 出来的对象。
        /// </summary>
        bool ReleaseRes();
        /// <summary>回收本对象到其对应的对象池（具体池由实现决定）。</summary>
        void RecycleToCache();
        
        #endregion
        
    }
}
