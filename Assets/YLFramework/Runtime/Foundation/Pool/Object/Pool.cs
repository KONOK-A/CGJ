using System;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 历史领域池基类。缓存、身份、容量与异常管理全部委托给 ObjectPool，
    /// 派生类型只提供创建及生命周期钩子，不再直接操作空闲容器。
    /// </summary>
    public abstract class Pool<T> : IPool<T>, IDisposable where T : class
    {
        private readonly ObjectPool<T> _pool;
        protected IObjectFactory<T> ObjectFactory;

        protected Pool() : this(PoolCacheOrder.Fifo) { }

        protected Pool(PoolCacheOrder cacheOrder)
        {
            _pool = new ObjectPool<T>(Create, OnAllocate, Reset, OnDiscard, cacheOrder: cacheOrder);
        }

        /// <summary>当前空闲数量，不包含借出及转换期对象。</summary>
        public int CurStoreCount => _pool.CurStoreCount;

        /// <summary>空闲容量上限，非正值不限；缩容执行永久丢弃，不重复 Reset。</summary>
        public int MaxPoolSize { get => _pool.MaxPoolSize; set => _pool.MaxPoolSize = value; }

        public void SetObjectFactory(IObjectFactory<T> factory)
        {
            // 由内核同时核验关闭状态；创建仍经过领域 Create 钩子。
            _pool.SetFactoryMethod(Create);
            ObjectFactory = factory;
        }

        public void SetFactoryMethod(Func<T> factoryMethod) => SetObjectFactory(new CustomObjectFactory<T>(factoryMethod));

        protected virtual T Create()
        {
            if (ObjectFactory == null)
                throw new InvalidOperationException($"[Pool] {typeof(T).FullName} 尚未设置对象工厂。");
            return ObjectFactory.Create();
        }

        protected virtual void OnAllocate(T item) { }
        protected virtual void Reset(T item) { }
        protected virtual void OnDiscard(T item) { }
        protected virtual void PrepareRaw(T item) { }

        public virtual T Allocate() => _pool.Allocate();
        public bool TryAllocate(out T item) => _pool.TryAllocate(out item);

        /// <summary>
        /// 只归还本池借出对象。false 仅表示未缓存，不代表可继续使用或重试；
        /// foreign 抛错，重复/转换期归还拒绝，失败对象永久隔离。
        /// </summary>
        public virtual bool Recycle(T item) => _pool.Recycle(item);

        /// <summary>结束借用并清理，但不缓存；不会重复清理已归还对象。</summary>
        public void Discard(T item) => _pool.Discard(item);

        public int Prewarm(int count) => _pool.Prewarm(count);
        public int Prewarm(int count, PoolPrewarmMode mode) => _pool.Prewarm(count, mode, PrepareRaw);
        public virtual void Clear(Action<T> itemClearCallback = null) => _pool.Clear(itemClearCallback);

        /// <summary>关闭本实例；不主动清理借出对象，晚归还仍清理但不缓存。</summary>
        public virtual void Dispose() => _pool.Dispose();
    }
}
