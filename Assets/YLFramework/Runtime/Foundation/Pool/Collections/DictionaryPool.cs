using System;
using System.Collections.Generic;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// Dictionary&lt;TKey, TValue&gt; 的静态对象池。
    /// 回收时只清空元素、保留容量。
    /// </summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">值类型。</typeparam>
    public static class DictionaryPool<TKey, TValue>
    {
        /// <summary>
        /// 从池中获取字典。
        /// </summary>
        public static Dictionary<TKey, TValue> Allocate()
        {
            return Allocate(0);
        }

        /// <summary>
        /// 从池中获取字典，池为空时按 capacity 预分配容量。
        /// 注意：池中已有缓存时返回现有字典，不保证容量不小于 capacity
        /// （Dictionary 没有直接扩容 API，与 ListPool 的容量保证不同）。
        /// </summary>
        /// <param name="capacity">池为空时的初始容量。</param>
        public static Dictionary<TKey, TValue> Allocate(int capacity)
        {
            return StaticStackPool<Dictionary<TKey, TValue>>.TryPop(out var dict)
                ? dict
                : new Dictionary<TKey, TValue>(capacity);
        }

        /// <summary>
        /// 回收字典：先清空元素再入池。
        /// </summary>
        /// <param name="toRecycle">待回收字典，null 会被忽略。</param>
        /// <exception cref="InvalidOperationException">同一字典被重复回收。</exception>
        public static void Recycle(Dictionary<TKey, TValue> toRecycle)
        {
            if (toRecycle == null)
            {
                return;
            }

            if (StaticStackPool<Dictionary<TKey, TValue>>.Contains(toRecycle))
            {
                throw new InvalidOperationException("重复回收 Dictionary");
            }

            toRecycle.Clear();
            StaticStackPool<Dictionary<TKey, TValue>>.Push(toRecycle);
        }

        /// <summary>
        /// 丢弃所有缓存字典，释放其占用的内存。
        /// </summary>
        public static void Clear()
        {
            StaticStackPool<Dictionary<TKey, TValue>>.Clear();
        }
    }

    /// <summary>
    /// <see cref="DictionaryPool{TKey,TValue}"/> 的扩展入口。
    /// </summary>
    public static class DictionaryPoolExtensions
    {
        /// <summary>
        /// 将字典回收进 DictionaryPool。
        /// </summary>
        public static void RecycleToPool<TKey, TValue>(this Dictionary<TKey, TValue> self)
        {
            DictionaryPool<TKey, TValue>.Recycle(self);
        }
    }
}
