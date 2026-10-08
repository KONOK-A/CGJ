using System;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 轻量对象池：默认 FIFO、无缓存上限，不维护回收状态或检测重复回收，由调用方保证生命周期正确。
    /// 适合单一所有者管理对象生命周期的场景（例如 UIManager 的提示对象）。
    /// </summary>
    /// <typeparam name="T">池化对象类型，不要求实现 IOfPool。</typeparam>
    public class SimpleObjectPool<T> : Pool<T> where T : class
    {
        private readonly Action<T> _resetMethod;

        /// <summary>
        /// 创建简单对象池。
        /// </summary>
        /// <param name="factoryMethod">对象创建委托，不能为 null。</param>
        /// <param name="resetMethod">回收时的重置回调，可为 null。</param>
        /// <param name="initialSize">创建时预热放入缓存的对象数量。</param>
        public SimpleObjectPool(Func<T> factoryMethod, Action<T> resetMethod = null, int initialSize = 0)
            : this(factoryMethod, resetMethod, initialSize, PoolCacheOrder.Fifo) { }

        /// <summary>创建指定复用顺序的简单对象池；默认无容量上限，不检测重复归还。</summary>
        /// <param name="factoryMethod">对象创建委托，不能为 null。</param>
        /// <param name="resetMethod">回收时的重置回调，满池时也执行，可为 null。</param>
        /// <param name="initialSize">按回收流程预热的空闲数量。</param>
        /// <param name="cacheOrder">空闲对象的分配顺序。</param>
        public SimpleObjectPool(Func<T> factoryMethod, Action<T> resetMethod, int initialSize, PoolCacheOrder cacheOrder)
            : base(cacheOrder)
        {
            ObjectFactory = new CustomObjectFactory<T>(factoryMethod);
            _resetMethod = resetMethod;
            maxPoolSize = 0;
            Prewarm(initialSize);
        }

        /// <summary>
        /// 回收对象：先执行重置回调，容量允许时放入缓存。同一对象重复回收不会被检测，可能同时存在多份引用。
        /// </summary>
        protected sealed override bool PrepareRecycle(ref T obj, bool willCache)
        {
            if (obj == null)
            {
                return false;
            }

            _resetMethod?.Invoke(obj);
            return true;
        }
    }
}
