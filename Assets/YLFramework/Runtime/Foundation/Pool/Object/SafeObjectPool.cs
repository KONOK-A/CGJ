using System;
using YLFramework.Singletons;

namespace YLFramework.Pooling
{
    /// <summary>
    /// IOfPool 的类型化单例入口，缓存与真实所有权由统一内核负责。
    /// IsRecycled 仅为领域可观察状态，不是池身份或租用代次。
    /// </summary>
    public class SafeObjectPool<T> : IPool<T>, IDisposable, ISingleton where T : class, IOfPool, new()
    {
        private readonly ObjectPool<T> _pool;
        protected IObjectFactory<T> ObjectFactory;
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
            if (original != null && original.TryGetTarget(out var pool))
            {
                pool.Dispose();
            }
        }

        void ISingleton.OnSingletonInit() => _sessionPool = new WeakReference<SafeObjectPool<T>>(this);

        public static SafeObjectPool<T> Instance => SingletonProperty<SafeObjectPool<T>>.Instance;

        protected SafeObjectPool()
        {
            ObjectFactory = new DefaultObjectFactory<T>();
            _pool = new ObjectPool<T>(Create, OnAllocate, Reset, OnDiscard);
        }

        /// <summary>当前空闲数量，不包含借出及转换期对象。</summary>
        public int CurStoreCount => _pool.CurStoreCount;

        /// <summary>空闲容量上限，默认 20，非正值不限；缩容不重复 OnRecycle。</summary>
        public int MaxPoolSize
        {
            get => _pool.MaxPoolSize; set => _pool.MaxPoolSize = value;
        }

        /// <summary>替换后续冷创建的工厂，不改变已借用对象的生命周期责任。</summary>
        public void SetObjectFactory(IObjectFactory<T> factory)
        {
            _pool.SetFactoryMethod(Create);
            ObjectFactory = factory;
        }

        /// <summary>使用委托配置后续冷创建。</summary>
        public void SetFactoryMethod(Func<T> factoryMethod) => SetObjectFactory(new CustomObjectFactory<T>(factoryMethod));

        protected virtual T Create()
        {
            if (ObjectFactory == null)
            {
                throw new InvalidOperationException($"[Pool] {typeof(T).FullName} 尚未设置对象工厂。");
            }

            return ObjectFactory.Create();
        }

        /// <summary>冷/热取得均执行 OnAllocate，成功后才交付对象。</summary>
        public virtual T Allocate() => _pool.Allocate();

        /// <summary>工厂返回 null 时为 false，工厂及回调异常仍传播。</summary>
        public bool TryAllocate(out T item) => _pool.TryAllocate(out item);

        /// <summary>
        /// 只归还本池借出对象。false 仅表示未缓存，不代表可继续使用或重试；
        /// foreign 抛错，重复/转换期归还拒绝，失败对象永久隔离。
        /// </summary>
        public virtual bool Recycle(T item) => _pool.Recycle(item);

        /// <summary>结束借用并执行 OnRecycle，但不缓存；不重复清理已归还对象。</summary>
        public void Discard(T item) => _pool.Discard(item);

        /// <summary>按目标空闲数量预热，默认执行 OnRecycle，返回实际新增缓存数。</summary>
        public int Prewarm(int count) => _pool.Prewarm(count);

        /// <summary>Raw 只登记及标记空闲，不执行 OnAllocate/OnRecycle。</summary>
        public int Prewarm(int count, PoolPrewarmMode mode) => _pool.Prewarm(count, mode, PrepareRaw);

        /// <summary>摘除空闲快照后逐项清理；附加回调错误聚合报告，不触碰借出对象。</summary>
        public virtual void Clear(Action<T> itemClearCallback = null) => _pool.Clear(itemClearCallback);

        /// <summary>初始化空闲容量及目标数预热，非正容量不限。</summary>
        public void Init(int maxSize, int initialCount)
        {
            MaxPoolSize = maxSize;
            Prewarm(initialCount);
        }

        protected virtual void PrepareRaw(T item) => item.IsRecycled = true;

        protected virtual void OnAllocate(T item)
        {
            item.IsRecycled = false;
            item.OnAllocate();
        }

        protected virtual void Reset(T item)
        {
            item.IsRecycled = true;
            try
            {
                item.OnRecycle();
            }
            finally
            {
                item.IsRecycled = true;
            }
        }

        protected virtual void OnDiscard(T item) => item.IsRecycled = true;

        /// <summary>
        /// 关闭原实例后，仅摘除仍指向自己的静态入口。旧引用不能关闭新单例；
        /// 旧借用须归还原池，只清理不缓存，新静态池不会接纳旧借用。
        /// </summary>
        public virtual void Dispose()
        {
            try
            {
                _pool.Dispose();
            }
            finally
            {
                ObjectFactory = null;
                if (_sessionPool != null && _sessionPool.TryGetTarget(out var original) && ReferenceEquals(original, this))
                {
                    _sessionPool = null;
                }

                if (SingletonProperty<SafeObjectPool<T>>.TryGetInstance(out var current) && ReferenceEquals(current, this))
                {
                    SingletonProperty<SafeObjectPool<T>>.Dispose();
                }
            }
        }
    }
}
