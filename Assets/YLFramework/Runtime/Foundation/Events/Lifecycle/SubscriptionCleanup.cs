using System;
using System.Collections.Generic;

namespace YLFramework.Events
{
    /// <summary>
    /// 订阅集合的所有权清理工具：释放动作与异常、生命周期策略由调用方决定。
    /// </summary>
    public static class SubscriptionCleanup
    {
        /// <summary>
        /// 逐个执行释放回调，全部成功返回后再清空集合；
        /// 某个回调抛异常时集合保持原样（包含尚未处理的元素），异常向上传播，便于调用方重试或排查。
        /// </summary>
        /// <param name="items">要释放的集合。</param>
        /// <param name="release">对每个元素执行的释放回调。</param>
        public static void ReleaseAfterSuccess<T>(ICollection<T> items, Action<T> release)
        {
            foreach (var item in items) release(item);
            items.Clear();
        }

        /// <summary>
        /// 返回集合快照并立即清空原集合；之后在遍历快照时增删原集合不会破坏遍历。
        /// </summary>
        /// <param name="items">要分离的集合。</param>
        public static T[] DetachAll<T>(ICollection<T> items)
        {
            var snapshot = new T[items.Count];
            items.CopyTo(snapshot, 0);
            items.Clear();
            return snapshot;
        }
    }

    /// <summary>
    /// 去重的订阅所有权集合（不依赖 Unity 组件）：同一元素只记录一次，用于托管需要批量注销的句柄。
    /// 只负责持有与释放，不解释元素语义。
    /// </summary>
    public sealed class SubscriptionSet<T>
    {
        private readonly HashSet<T> _items = new();

        /// <summary>
        /// 当前托管的元素数量。
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 加入元素；已存在时不重复添加。
        /// </summary>
        public void Add(T item) => _items.Add(item);

        /// <summary>
        /// 移除元素（仅解除托管，不执行释放）。
        /// </summary>
        public void Remove(T item) => _items.Remove(item);

        /// <summary>
        /// 逐个执行释放回调，全部成功后再清空；异常时保留集合内容。
        /// </summary>
        public void ReleaseAfterSuccess(Action<T> release) => SubscriptionCleanup.ReleaseAfterSuccess(_items, release);

        /// <summary>
        /// 返回快照并清空集合。
        /// </summary>
        public T[] DetachAll() => SubscriptionCleanup.DetachAll(_items);
    }
}
