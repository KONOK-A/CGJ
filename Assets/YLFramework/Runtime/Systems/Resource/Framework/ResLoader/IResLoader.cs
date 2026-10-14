using System;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源加载器契约。
    /// <para>
    /// Ownership：调用方通过 <c>ResLoader.Allocate()</c> 获取实例（实现类从对象池分配），
    /// 使用 <see cref="ReleaseAllRes"/> 或回收（<c>RecycleToCache</c>）释放加载器持有的全部资源引用。
    /// Loader 对同一资源只持有一份引用，重复加载不会重复计数；重复 Release 安全。
    /// 传入的 <see cref="ResKey"/> 一律由调用方负责回收（Loader 不消费 key）。
    /// </para>
    /// </summary>
    public interface IResLoader
    {
        /// <summary>同步加载并返回资源对象；没有可用创建器时返回 null（key 由调用方回收）。</summary>
        IRes LoadResSync(ResKey resKey);
        /// <summary>同步加载并返回资产本体；失败返回 null（key 由调用方回收）。</summary>
        Object LoadAssetSync(ResKey resKey);

        /// <summary>
        /// 将加载请求加入预加载队列（异步加载使用）；同一资源重复添加只追加监听。
        /// 没有可用创建器时立即以 (false, null) 回调 listener。
        /// </summary>
        void AddToPreLoad(ResKey resKey, Action<bool, IRes> listener = null, bool lastOrder = true);
        /// <summary>执行全部预加载请求；listener 在全部完成后调用一次。</summary>
        void LoadAsync(Action listener = null);

        /// <summary>释放加载器持有的全部资源引用并清空内部状态；重复调用安全。</summary>
        void ReleaseAllRes();
        /// <summary>卸载全部"实例化对象"并释放对应持有；仅 UnloadInstantiate 返回 true 的资源会被处理。</summary>
        void UnloadAllInstantiateRes(bool flag);
    }
}
