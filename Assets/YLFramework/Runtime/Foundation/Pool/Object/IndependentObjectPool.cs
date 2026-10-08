using System;
using System.Collections.Generic;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 让类型自持静态对象池的抽象基类。
    /// 每个具体封闭类型拥有独立缓存；<see cref="Clear"/> 会清空该类型的全部缓存。
    /// </summary>
    /// <typeparam name="T">池化类型自身。</typeparam>
    public abstract class IndependentObjectPool<T> where T : IndependentObjectPool<T>, new()
    {
        private static readonly Queue<T> Pool = new();

        /// <summary>
        /// 当前实例是否位于缓存中，用于拒绝重复回收。
        /// </summary>
        protected bool IsInPool;

        /// <summary>
        /// 从缓存分配一个实例；缓存为空时创建新实例。
        /// </summary>
        public T Allocate()
        {
            var obj = Pool.Count > 0 ? Pool.Dequeue() : new T();
            obj.IsInPool = false;
            return obj;
        }

        /// <summary>
        /// 回收当前实例：重复回收会被忽略；OnRecycle 抛出异常时对象不会入池且可再次回收。
        /// </summary>
        public void RecycleToCache()
        {
            if (IsInPool) return;

            // 先设置标记，防止 OnRecycle 内部再次调用 RecycleToCache 造成重复入池
            IsInPool = true;
            try
            {
                OnRecycle();
            }
            catch
            {
                IsInPool = false;
                throw;
            }

            Pool.Enqueue(this as T);
        }

        /// <summary>
        /// 回收时调用，用于重置实例状态。
        /// </summary>
        protected abstract void OnRecycle();

        /// <summary>
        /// 清空该类型的全部缓存。缓存中对象先解除池标记再执行回调，
        /// 因此被外部继续持有的实例之后仍可正常回收。
        /// </summary>
        /// <param name="onItemClear">对每个被移除实例执行的回调，可为 null。</param>
        public void Clear(Action<T> onItemClear)
        {
            if (Pool.Count == 0) return;

            // 先快照再清空，避免回调中操作池导致枚举异常
            var snapshot = new T[Pool.Count];
            Pool.CopyTo(snapshot, 0);
            Pool.Clear();

            foreach (var item in snapshot)
            {
                item.IsInPool = false;
                onItemClear?.Invoke(item);
            }
        }
    }
}
