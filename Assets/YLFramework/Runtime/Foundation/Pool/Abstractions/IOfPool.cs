namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 可由对象池安全管理的对象契约。
    /// 约定：分配时先清除回收标记再调用 <see cref="OnAllocate"/>；
    /// 回收时先标记 <see cref="IsRecycled"/>（使回调中的重入回收被拒绝），再调用 <see cref="OnRecycle"/>，
    /// 回调失败时池会回滚标记并向上抛出异常。
    /// </summary>
    public interface IOfPool
    {
        /// <summary>
        /// 对象被分配出池时调用。默认空实现，可按需覆盖。
        /// </summary>
        public virtual void OnAllocate()
        {
        }

        /// <summary>
        /// 对象被回收时调用，用于清理状态以便复用。
        /// 注意：缓存已满导致回收失败时该方法也会被调用一次，用于释放对象持有的外部资源；
        /// 调用后对象会被标记为已回收，不会再次触发。
        /// </summary>
        void OnRecycle();

        /// <summary>
        /// 是否已被回收。为 true 的对象会被池拒绝再次回收。
        /// </summary>
        bool IsRecycled { get; set; }
    }
}
