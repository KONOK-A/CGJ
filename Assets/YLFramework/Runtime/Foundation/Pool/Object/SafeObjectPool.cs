using System;
using YLFramework.Core.Singleton;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 带回收状态保护的通用对象池单例，池化对象需满足 class、IOfPool、new() 约束。
    /// 通过 <see cref="Instance"/> 按封闭泛型类型访问；调用 <see cref="Dispose"/> 后再次访问会创建全新池，
    /// 旧缓存对象随旧实例一起丢弃。
    /// </summary>
    /// <typeparam name="T">池化对象类型。</typeparam>
    public class SafeObjectPool<T> : Pool<T>, ISingleton where T : class, IOfPool, new()
    {
        #region 单例相关

        void ISingleton.OnSingletonInit()
        {
        }

        /// <summary>
        /// 当前泛型类型对应的池单例。
        /// </summary>
        public static SafeObjectPool<T> Instance => SingletonProperty<SafeObjectPool<T>>.Instance;

        /// <summary>
        /// 释放当前单例对象池，下次访问 Instance 时重新创建（缓存对象不触发回收回调，随旧实例丢弃）。
        /// </summary>
        public void Dispose()
        {
            SingletonProperty<SafeObjectPool<T>>.Dispose();
        }

        #endregion

        /// <summary>
        /// 创建对象池：默认使用无参构造工厂，可通过 <see cref="Pool{T}.SetObjectFactory"/> / <see cref="Pool{T}.SetFactoryMethod"/> 替换。
        /// </summary>
        protected SafeObjectPool()
        {
            ObjectFactory = new DefaultObjectFactory<T>();
        }

        /// <summary>
        /// 初始化缓存上限并预热对象。
        /// </summary>
        /// <param name="maxSize">缓存上限，小于等于 0 表示不限制。</param>
        /// <param name="initialCount">预热数量；maxSize 大于 0 时会被裁剪到不超过 maxSize。</param>
        public void Init(int maxSize, int initialCount)
        {
            MaxPoolSize = maxSize;
            Prewarm(initialCount);
        }

        /// <summary>
        /// 预热对象先解除回收标记，再走正常回收流程；忽略 null。
        /// </summary>
        protected override bool RecyclePrewarmed(T obj)
        {
            if (obj == null) return false;
            obj.IsRecycled = false;
            return Recycle(obj);
        }

        /// <summary>
        /// 分配对象：先清除回收标记再调用 OnAllocate，保证回调中看到的是"可用"状态。
        /// 对象工厂返回 null 时返回 null，不抛异常。
        /// </summary>
        public override T Allocate()
        {
            var value = base.Allocate();
            if (value == null)
            {
                return null;
            }

            value.IsRecycled = false;
            value.OnAllocate();
            return value;
        }

        /// <summary>
        /// 尝试分配对象；对象工厂无法产生对象时返回 false（不吞对象工厂自身抛出的异常）。
        /// </summary>
        public bool TryAllocate(out T obj)
        {
            obj = Allocate();
            return obj != null;
        }

        /// <summary>
        /// 回收对象。空对象与重复回收会被拒绝；回收开始时即标记 IsRecycled，
        /// 因此 OnRecycle 中再次回收同一对象会被拒绝，不会无限递归；
        /// 缓存已满时对象不入池，但仍会执行一次 OnRecycle 并保持已回收标记，
        /// 避免调用方忽略返回值后重复触发回收清理。OnRecycle 抛出异常时回滚标记并向上抛出。
        /// </summary>
        /// <param name="obj">待回收对象。</param>
        /// <param name="willCache">按当前容量判断该对象是否会被放入缓存；为 false 时对象不入池，但仍会执行一次 OnRecycle。</param>
        /// <returns>对象成功进入缓存时返回 true。</returns>
        protected override bool PrepareRecycle(ref T obj, bool willCache)
        {
            if (obj == null || obj.IsRecycled)
            {
                return false;
            }

            // 先标记再回调：OnRecycle 中的重入回收会被拒绝；失败时回滚
            obj.IsRecycled = true;
            try
            {
                obj.OnRecycle();
                return true;
            }
            catch
            {
                // 回调失败：对象未入池，恢复未回收状态以便重试
                obj.IsRecycled = false;
                throw;
            }
        }
    }
}
