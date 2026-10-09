using System;
using YLFramework.Core.Singleton;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// IOfPool 的类型化单例入口，缓存与真实所有权由统一内核负责。
    /// IsRecycled 仅为领域可观察状态，不是池身份或租用代次。
    /// </summary>
    public class SafeObjectPool<T> : Pool<T>, ISingleton where T : class, IOfPool, new()
    {
        private static WeakReference<SafeObjectPool<T>> _sessionPool;

        static SafeObjectPool()
        {
            SingletonSessionState.RegisterSessionReset(ResetSession);
        }

        private static void ResetSession()
        {
            // HasInstance 可先触发 SingletonProperty 注册；关闭不依赖其静态引用尚未摘除。
            var original = _sessionPool;
            _sessionPool = null;
            if (original != null && original.TryGetTarget(out var pool)) pool.Dispose();
        }

        void ISingleton.OnSingletonInit() => _sessionPool = new WeakReference<SafeObjectPool<T>>(this);

        public static SafeObjectPool<T> Instance => SingletonProperty<SafeObjectPool<T>>.Instance;

        protected SafeObjectPool() => SetObjectFactory(new DefaultObjectFactory<T>());

        /// <summary>初始化空闲容量及目标数预热，非正容量不限。</summary>
        public void Init(int maxSize, int initialCount)
        {
            MaxPoolSize = maxSize;
            Prewarm(initialCount);
        }

        protected override void PrepareRaw(T item) => item.IsRecycled = true;

        protected override void OnAllocate(T item)
        {
            item.IsRecycled = false;
            item.OnAllocate();
        }

        protected override void Reset(T item)
        {
            item.IsRecycled = true;
            try { item.OnRecycle(); }
            finally { item.IsRecycled = true; }
        }

        protected override void OnDiscard(T item) => item.IsRecycled = true;

        /// <summary>
        /// 关闭原实例后，仅摘除仍指向自己的静态入口。旧引用不能关闭新单例；
        /// 旧借用须归还原池，只清理不缓存，新静态池不会接纳旧借用。
        /// </summary>
        public override void Dispose()
        {
            try { base.Dispose(); }
            finally
            {
                if (_sessionPool != null && _sessionPool.TryGetTarget(out var original) && ReferenceEquals(original, this))
                    _sessionPool = null;
                if (SingletonProperty<SafeObjectPool<T>>.TryGetInstance(out var current) && ReferenceEquals(current, this))
                    SingletonProperty<SafeObjectPool<T>>.Dispose();
            }
        }
    }
}
