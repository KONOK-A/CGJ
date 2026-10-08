using System;
using YLFramework.Core.Pool;

namespace YLFramework.Actions
{
    // YL 负责通用缓存，Action 只保留回收重置策略与静态池所有权。
    internal sealed class ActionPool<T>
    {
        private readonly ResetPool mPool;

        // LIFO 空闲缓存；每次回收都调用可选重置回调；容量不设上限（MaxPoolSize=0 表示无限）；
        // 预热使用 Raw 模式跳过重置回调，因为新建实例无需重置。
        private sealed class ResetPool : Pool<T>
        {
            private readonly Action<T> mReset;

            internal ResetPool(Func<T> factory, Action<T> reset) : base(PoolCacheOrder.Lifo)
            {
                SetFactoryMethod(factory);
                mReset = reset;
                MaxPoolSize = 0;
            }

            protected override bool PrepareRecycle(ref T item, bool willCache)
            {
                mReset?.Invoke(item);
                return true;
            }
        }

        internal ActionPool(Func<T> factory, Action<T> reset = null, int initCount = 0)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            mPool = new ResetPool(factory, reset);
            mPool.Prewarm(initCount, PoolPrewarmMode.Raw);
        }

        internal T Allocate() => mPool.Allocate();

        internal bool Recycle(T item) => mPool.Recycle(item);
    }
}
