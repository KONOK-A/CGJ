/****************************************************************************
 * 动作回收逻辑提取自本地 ActionKit MonoActionQueue。
 * Copyright (c) liangxiegame UNDER MIT License
 * https://github.com/liangxiegame/QFramework
 ****************************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Actions
{
    // 回收回调只是"把对象还回池子"的延迟动作，本身不区分类型，因此用非泛型接口存放在同一队列。
    internal interface IActionQueueCallback
    {
        void Call();
    }

    // 延迟回收项：持有池与动作引用；Call 后立即清空引用，避免队列在清空之前长期持有对象。
    internal struct ActionQueueRecycleCallback<T> : IActionQueueCallback
    {
        public ActionPool<T> Pool;
        public T Action;

        public ActionQueueRecycleCallback(ActionPool<T> pool, T action)
        {
            Pool = pool;
            Action = action;
        }

        public void Call()
        {
            Pool.Recycle(Action);
            Pool = null;
            Action = default;
        }
    }

    // 延迟回收队列：动作清理时只入队，等执行栈完全退出后再真正还池，避免回调中立即复用同一个池对象。
    internal static class ActionQueue
    {
        private static readonly Queue<IActionQueueCallback> Callbacks = new Queue<IActionQueueCallback>();
        private static bool mDraining;

        internal static int PendingCount => Callbacks.Count;

        // 播放模式下确保回收驱动器存在（实际清空在其 Update 中执行）；编辑模式只入队，由测试或显式 Drain 清空。
        public static void AddCallback(IActionQueueCallback callback)
        {
            Callbacks.Enqueue(callback);
            if (Application.isPlaying)
            {
                ActionRecycleDriver.EnsureCreated();
            }
        }

        internal static void Drain()
        {
            if (mDraining) return;
            // 清空过程中加入的回收项留到下一轮，避免同一执行栈内立即复用对象。
            var count = Callbacks.Count;
            mDraining = true;
            try
            {
                for (var index = 0; index < count; index++)
                {
                    Callbacks.Dequeue().Call();
                }
            }
            finally
            {
                mDraining = false;
            }
        }
    }
}
