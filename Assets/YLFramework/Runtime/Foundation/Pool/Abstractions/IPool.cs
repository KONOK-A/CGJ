namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 对象池通用接口。
    /// </summary>
    /// <typeparam name="T">池化对象类型。</typeparam>
    public interface IPool<T>
    {
        /// <summary>
        /// 从池中分配一个对象；缓存为空时由实现决定新建或返回空。
        /// </summary>
        T Allocate();

        /// <summary>
        /// 回收对象。
        /// </summary>
        /// <param name="obj">待回收对象。</param>
        /// <returns>成功进入缓存时返回 true；被具体实现的回收策略或容量规则拒绝时返回 false。</returns>
        bool Recycle(T obj);
    }
}
