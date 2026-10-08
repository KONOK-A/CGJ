/****************************************************************************
 * Copyright (c) 2015 - 2024 liangxiegame UNDER MIT License
 * 
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * https://gitee.com/liangxiegame/QFramework
 ****************************************************************************/

using System;
using System.Collections;

namespace YLFramework.Actions
{
    // 把 Unity 协程包装为动作：在模块全局宿主上运行，协程自然结束时标记完成，取消或 Reset 时停止协程。
    internal class CoroutineAction : IAction
    {
        private static ActionPool<CoroutineAction> mPool =
            new ActionPool<CoroutineAction>(() => new CoroutineAction(), null, 10);

        private Func<IEnumerator> mCoroutineGetter = null;
        private CompletionSlot mCompletion;
        private ActionGlobalHost mHost;
        private UnityEngine.Coroutine mCoroutine;

        // 完成与取消都通过独立的槽对象转发：槽先断开与动作的引用，晚到的协程完成就不会影响已复用或已取消的动作。
        private sealed class CompletionSlot
        {
            public CoroutineAction Target;

            public void Finish()
            {
                var target = Target;
                if (target == null) return;
                if (target.mHost) target.mHost.UnRegisterCoroutineCancellation(Cancel);
                Target = null;
                target.mCompletion = null;
                target.mHost = null;
                target.mCoroutine = null;
                target.Status = ActionStatus.Finished;
            }

            public void Cancel() => Target?.Deinit();
        }

        private CoroutineAction()
        {
        }

        public static CoroutineAction Allocate(Func<IEnumerator> coroutineGetter)
        {
            var coroutineAction = mPool.Allocate();
            coroutineAction.ActionID = ActionKit.ID_GENERATOR++;
            coroutineAction.Deinited = false;
            coroutineAction.Reset();
            coroutineAction.mCoroutineGetter = coroutineGetter;
            return coroutineAction;
        }

        public bool Paused { get; set; }
        public void Deinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                StopRunningCoroutine();
                mCoroutineGetter = null;

                ActionQueue.AddCallback(new ActionQueueRecycleCallback<CoroutineAction>(mPool, this));
            }
        }

        public void Reset()
        {
            StopRunningCoroutine();
            Paused = false;
            Status = ActionStatus.NotStart;
        }

        private void StopRunningCoroutine()
        {
            // 先断开旧完成回调，再停止本动作的协程，避免晚到的完成影响复用对象。
            if (mCompletion != null)
            {
                if (mHost) mHost.UnRegisterCoroutineCancellation(mCompletion.Cancel);
                mCompletion.Target = null;
                mCompletion = null;
            }
            var host = mHost;
            var coroutine = mCoroutine;
            mHost = null;
            mCoroutine = null;
            if (host && coroutine != null) host.StopCoroutine(coroutine);
        }

        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus Status { get; set; }
        public void OnStart()
        {
            var completion = new CompletionSlot { Target = this };
            mCompletion = completion;
            var host = ActionGlobalHost.Instance;
            mHost = host;
            try
            {
                if (!host) throw new InvalidOperationException("ActionKit coroutine host is unavailable.");
                // 先登记取消再启动协程，宿主释放时才能停止尚未结束的协程并清理动作。
                host.RegisterCoroutineCancellation(completion.Cancel);
                var routine = mCoroutineGetter();
                if (completion.Target == null) return;
                var coroutine = host.StartActionCoroutine(routine, completion.Finish);
                if (completion.Target != null)
                {
                    mCoroutine = coroutine;
                }
                else if (host && coroutine != null)
                {
                    // 首步执行可能已取消动作，此时 StartCoroutine 才返回句柄，仍需停止该协程。
                    host.StopCoroutine(coroutine);
                }
            }
            catch
            {
                completion.Target?.Deinit();
                throw;
            }
        }

        public void OnExecute(float dt)
        {
        }

        public void OnFinish()
        {
        }
    }

    /// <summary>
    /// 将协程工厂或已有枚举器接入动作组合。
    /// </summary>
    /// <remarks>取消或 Reset 会停止当前协程；暂停动作不会暂停协程。</remarks>
    public static class CoroutineExtension
    {
        /// <summary>
        /// 追加协程动作，在每次启动时调用工厂创建枚举器。
        /// </summary>
        /// <param name="self">要追加协程动作的父组合。</param>
        /// <param name="coroutineGetter">每次启动时调用的协程工厂。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Coroutine(this ISequence self, Func<IEnumerator> coroutineGetter)
        {
            return self.Append(CoroutineAction.Allocate(coroutineGetter));
        }

        /// <summary>
        /// 包装已有枚举器，不创建新枚举器；Reset 不会使枚举器回到起点。
        /// </summary>
        /// <param name="self">被包装的原枚举器。</param>
        /// <returns>使用原枚举器的协程动作。</returns>
        public static IAction ToAction(this IEnumerator self)
        {
            return CoroutineAction.Allocate(() => self);
        }
    }
}
