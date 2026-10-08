using System;
using System.Collections.Generic;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// List&lt;T&gt; 的静态对象池。
    /// 回收时只清空元素、保留容量；池为空时按请求容量创建新列表。
    /// </summary>
    /// <typeparam name="T">列表元素类型。</typeparam>
    public static class ListPool<T>
    {
        /// <summary>
        /// 从池中获取列表，默认容量为 8。
        /// </summary>
        public static List<T> Allocate()
        {
            return Allocate(8);
        }

        /// <summary>
        /// 从池中获取列表，并保证其 Capacity 不小于 capacity。
        /// </summary>
        /// <param name="capacity">期望的最小容量。</param>
        public static List<T> Allocate(int capacity)
        {
            if (!StaticStackPool<List<T>>.TryPop(out var list))
            {
                return new List<T>(capacity);
            }

            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }

            return list;
        }

        /// <summary>
        /// 回收列表：先清空元素再入池。
        /// </summary>
        /// <param name="toRecycle">待回收列表，null 会被忽略。</param>
        /// <exception cref="InvalidOperationException">同一列表被重复回收。</exception>
        public static void Recycle(List<T> toRecycle)
        {
            if (toRecycle == null)
            {
                return;
            }

            if (StaticStackPool<List<T>>.Contains(toRecycle))
            {
                throw new InvalidOperationException("重复回收 List");
            }

            toRecycle.Clear();
            StaticStackPool<List<T>>.Push(toRecycle);
        }

        /// <summary>
        /// 丢弃所有缓存列表，释放其占用的内存。
        /// </summary>
        public static void Clear()
        {
            StaticStackPool<List<T>>.Clear();
        }
    }

    /// <summary>
    /// <see cref="ListPool{T}"/> 的扩展入口。
    /// </summary>
    public static class ListPoolExtensions
    {
        /// <summary>
        /// 将列表回收进 ListPool。
        /// </summary>
        public static void RecycleToPool<T>(this List<T> self)
        {
            ListPool<T>.Recycle(self);
        }
    }
}
