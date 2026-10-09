using System;
using System.Collections.Generic;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 对象池抽象基类：共享工厂、空闲缓存、容量和预热，子类只提供回收策略。
    /// </summary>
    /// <typeparam name="T">池化对象类型。</typeparam>
    public abstract class Pool<T> : IPool<T>
    {
        /// <summary>
        /// 空闲缓存上限；小于等于 0 表示不限制。修改请优先使用 <see cref="MaxPoolSize"/>，以便同步收缩已有缓存。
        /// </summary>
        protected int maxPoolSize = 20;

        /// <summary>
        /// FIFO 模式的空闲对象缓存队列；LIFO 模式下该队列为空（实际使用内部栈）。
        /// </summary>
        protected readonly Queue<T> StoreContainer = new();
        private readonly Stack<T> _stack;

        /// <summary>
        /// 创建 FIFO 缓存顺序的对象池。
        /// </summary>
        protected Pool() : this(PoolCacheOrder.Fifo) { }

        /// <summary>
        /// 创建指定缓存顺序的对象池。
        /// </summary>
        /// <param name="cacheOrder">空闲对象的分配顺序。</param>
        protected Pool(PoolCacheOrder cacheOrder)
        {
            if (cacheOrder == PoolCacheOrder.Lifo) _stack = new Stack<T>();
        }

        /// <summary>
        /// 对象创建工厂；未设置时从空缓存分配会抛出 InvalidOperationException。
        /// 请通过 <see cref="SetObjectFactory"/> 或 <see cref="SetFactoryMethod"/> 配置。
        /// </summary>
        protected IObjectFactory<T> ObjectFactory;

        /// <summary>
        /// 当前缓存中的空闲对象数量（不包含已分配出去的对象）。
        /// </summary>
        public int CurStoreCount => _stack == null ? StoreContainer.Count : _stack.Count;

        /// <summary>空闲缓存上限；非正值不限。缩容时按分配顺序直接丢弃多余对象（不执行任何清理回调）。</summary>
        public int MaxPoolSize
        {
            get => maxPoolSize;
            set
            {
                maxPoolSize = value;
                if (value <= 0) return;
                while (CurStoreCount > value) TakeCached();
            }
        }

        private T TakeCached() => _stack == null ? StoreContainer.Dequeue() : _stack.Pop();

        private void Cache(T item)
        {
            if (_stack == null) StoreContainer.Enqueue(item);
            else _stack.Push(item);
        }

        private T Create()
        {
            if (ObjectFactory == null)
                throw new InvalidOperationException(
                    $"[Pool] {typeof(T).FullName} 尚未设置对象工厂，无法创建新对象。");
            return ObjectFactory.Create();
        }

        /// <summary>
        /// 设置对象创建工厂。传入 null 后，在缓存为空时分配对象会抛出异常。
        /// </summary>
        public void SetObjectFactory(IObjectFactory<T> factory)
        {
            ObjectFactory = factory;
        }

        /// <summary>
        /// 用委托创建 CustomObjectFactory 并设置。
        /// </summary>
        /// <param name="factoryMethod">对象创建委托。</param>
        public void SetFactoryMethod(Func<T> factoryMethod)
        {
            ObjectFactory = new CustomObjectFactory<T>(factoryMethod);
        }

        /// <summary>
        /// 从缓存分配一个对象；缓存为空时通过对象工厂创建。
        /// 未设置对象工厂时抛出 InvalidOperationException；工厂返回 null 时结果为 null。
        /// </summary>
        public virtual T Allocate()
        {
            return CurStoreCount > 0 ? TakeCached() : Create();
        }

        /// <summary>
        /// 容量在清理前确定；接受并成功清理后才入缓存，异常原样传播。
        /// </summary>
        public virtual bool Recycle(T obj)
        {
            var willCache = maxPoolSize <= 0 || CurStoreCount < maxPoolSize;
            if (!PrepareRecycle(ref obj, willCache) || !willCache) return false;
            Cache(obj);
            return true;
        }

        /// <summary>检查并原位清理待缓存值；willCache 为 false 时仍可清理。</summary>
        protected virtual bool PrepareRecycle(ref T item, bool willCache) => true;

        /// <summary>预热走回收流程前的领域准备；默认直接回收。</summary>
        protected virtual bool RecyclePrewarmed(T item) => Recycle(item);

        /// <summary>
        /// 按目标空闲数量补齐（走回收流程），返回实际进入缓存的数量。
        /// </summary>
        public int Prewarm(int count) => Prewarm(count, PoolPrewarmMode.Recycle);

        /// <summary>
        /// 按目标空闲数量补齐并返回实际进入缓存的数量；受正容量上限裁剪。
        /// 工厂返回 null 或被回收策略拒收时只跳过该项，不会无限重试。
        /// </summary>
        /// <param name="count">目标空闲数量。</param>
        /// <param name="mode">预热方式。</param>
        public int Prewarm(int count, PoolPrewarmMode mode)
        {
            if (maxPoolSize > 0) count = Math.Min(count, maxPoolSize);
            if (count <= CurStoreCount) return 0;
            var missing = count - CurStoreCount;
            var cached = 0;
            for (var i = 0; i < missing; i++)
            {
                var item = Create();
                if (mode == PoolPrewarmMode.Recycle)
                {
                    if (RecyclePrewarmed(item)) cached++;
                }
                else if (maxPoolSize <= 0 || CurStoreCount < maxPoolSize)
                {
                    Cache(item);
                    cached++;
                }
            }
            return cached;
        }

        /// <summary>
        /// 清空缓存中的空闲对象。只丢弃缓存并执行回调，不会再次触发 IOfPool.OnRecycle；
        /// 被丢弃对象上的回收标记保持不变。
        /// 实现先清空缓存再执行回调，因此回调中操作池不会破坏枚举状态。
        /// </summary>
        /// <param name="itemClearCallback">对每个被丢弃的缓存对象执行的回调，可为 null。</param>
        public virtual void Clear(Action<T> itemClearCallback = null)
        {
            if (CurStoreCount == 0) return;

            // 先快照并清空，回调中 Allocate/Recycle 不会影响遍历
            var snapshot = new T[CurStoreCount];
            if (_stack == null)
            {
                StoreContainer.CopyTo(snapshot, 0);
                StoreContainer.Clear();
            }
            else
            {
                _stack.CopyTo(snapshot, 0);
                _stack.Clear();
            }

            if (itemClearCallback == null) return;
            foreach (var item in snapshot)
            {
                itemClearCallback.Invoke(item);
            }
        }
    }
}
