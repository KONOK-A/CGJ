using System;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 委托工厂及 Reset 的历史入口，默认无限 FIFO，复用统一 ObjectPool 内核。
    /// 只接纳本池借出对象，重复归还拒绝，Reset 失败对象不可重试或复用。
    /// </summary>
    public class SimpleObjectPool<T> : Pool<T> where T : class
    {
        private readonly Action<T> _resetMethod;

        public SimpleObjectPool(Func<T> factoryMethod, Action<T> resetMethod = null, int initialSize = 0)
            : this(factoryMethod, resetMethod, initialSize, PoolCacheOrder.Fifo) { }

        /// <summary>按指定 FIFO/LIFO 顺序创建；预热执行 Reset，默认空闲容量无限。</summary>
        public SimpleObjectPool(Func<T> factoryMethod, Action<T> resetMethod, int initialSize, PoolCacheOrder cacheOrder)
            : base(cacheOrder)
        {
            SetFactoryMethod(factoryMethod);
            _resetMethod = resetMethod;
            MaxPoolSize = 0;
            Prewarm(initialSize);
        }

        protected sealed override void Reset(T item) => _resetMethod?.Invoke(item);
    }
}
