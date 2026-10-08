using System.Collections.Generic;
using YLFramework.Core.Singleton;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 集合池共用的静态栈缓存。
    /// 每个封闭集合类型独立缓存，并在进入新的 Play 会话时自动清空，
    /// 避免 Domain Reload 关闭时静态缓存跨会话持有残留引用。
    /// </summary>
    /// <typeparam name="T">缓存的集合类型。</typeparam>
    internal static class StaticStackPool<T> where T : class
    {
        private static readonly Stack<T> Stack = new(8);

        static StaticStackPool()
        {
            // 会话重置时丢弃缓存，防止静态缓存跨 Play 会话保留旧引用
            SingletonSessionState.RegisterSessionReset(Clear);
        }

        /// <summary>
        /// 尝试从缓存取出一个集合。
        /// </summary>
        internal static bool TryPop(out T item)
        {
            if (Stack.Count == 0)
            {
                item = null;
                return false;
            }

            item = Stack.Pop();
            return true;
        }

        /// <summary>
        /// 判断集合是否已在缓存中（用于重复回收检测）。注意：栈枚举为 O(n)。
        /// </summary>
        internal static bool Contains(T item)
        {
            return Stack.Contains(item);
        }

        /// <summary>
        /// 将集合放入缓存。
        /// </summary>
        internal static void Push(T item)
        {
            Stack.Push(item);
        }

        /// <summary>
        /// 清空缓存并释放内部数组，供会话重置与手动清理使用。
        /// </summary>
        internal static void Clear()
        {
            Stack.Clear();
            Stack.TrimExcess();
        }
    }
}
