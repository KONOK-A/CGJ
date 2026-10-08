using System.Collections.Generic;
using YLFramework.Core.Pool;

namespace YLFramework.Events.Internal
{
    /// <summary>
    /// 全局事件系统共享的内部工具，不对外暴露。
    /// </summary>
    internal static class EventSystemInternal
    {
        /// <summary>
        /// 回收字典中全部池化事件并清空字典。
        /// 仅用于通过 <c>EasyEvent.Allocate</c> 创建的事件，非池化事件不应走该入口。
        /// </summary>
        public static void RecycleAllAndClear<TKey, TEvent>(Dictionary<TKey, TEvent> events)
            where TEvent : class, IOfPool, IPoolType
        {
            foreach (var e in events.Values)
            {
                e?.RecycleToCache();
            }

            events.Clear();
        }
    }
}
