using System;
using UnityEngine;

namespace YLFramework.Actions
{
    // 按执行器传入的 dt 累计等待时长；完成检测在累加之前，因此跨越阈值的下一次执行才会完成。
    internal class Delay : IAction
    {
        public float DelayTime;

        public Func<float> DelayTimeFactory = null;

        public System.Action OnDelayFinish { get; set; }

        public float CurrentSeconds { get; set; }

        private Delay()
        {
        }

        private static readonly ActionPool<Delay> mPool =
            new ActionPool<Delay>(() => new Delay(), null, 10);

        public static Delay Allocate(float delayTime, System.Action onDelayFinish = null)
        {
            var retNode = mPool.Allocate();
            retNode.ActionID = ActionKit.ID_GENERATOR++;
            retNode.Deinited = false;
            retNode.Reset();
            retNode.DelayTime = delayTime;
            retNode.DelayTimeFactory = null;
            retNode.OnDelayFinish = onDelayFinish;
            retNode.CurrentSeconds = 0.0f;
            return retNode;
        }

        public static Delay Allocate(Func<float> delayTimeFactory, System.Action onDelayFinish = null)
        {
            var retNode = mPool.Allocate();
            retNode.ActionID = ActionKit.ID_GENERATOR++;
            retNode.Deinited = false;
            retNode.Reset();
            retNode.DelayTimeFactory = delayTimeFactory;
            retNode.OnDelayFinish = onDelayFinish;
            retNode.CurrentSeconds = 0.0f;
            return retNode;
        }

        public ulong ActionID { get; set; }
        public ActionStatus Status { get; set; }

        public void OnStart()
        {
            // 动态时长在每次启动时求值一次，Reset 后再次启动会重新求值。
            if (DelayTimeFactory != null)
            {
                DelayTime = DelayTimeFactory();
            }
        }

        public void OnExecute(float dt)
        {
            // 先检测已有累计时间，再累加本次 dt；本次跨过阈值后要到下次执行才检测完成。
            if (CurrentSeconds >= DelayTime)
            {
                this.Finish();
                OnDelayFinish?.Invoke();
            }

            CurrentSeconds += dt;
        }

        public void OnFinish()
        {
        }

        public void Reset()
        {
            Status = ActionStatus.NotStart;
            Paused = false;
            CurrentSeconds = 0.0f;
        }

        public bool Paused { get; set; }

        public void Deinit()
        {
            if (!Deinited)
            {
                OnDelayFinish = null;
                DelayTimeFactory = null;
                Deinited = true;
                ActionQueue.AddCallback(new ActionQueueRecycleCallback<Delay>(mPool, this));
            }
        }

        public bool Deinited { get; set; }
    }

    /// <summary>
    /// 向组合追加按累计时间等待的动作。
    /// </summary>
    /// <remarks>先比较已有累计时间再加入当前 dt，跨过时长后在下一次执行检测到完成。</remarks>
    public static class DelayExtension
    {
        /// <summary>
        /// 追加等待指定秒数的动作，时间由执行传入的 dt 累计。
        /// </summary>
        /// <param name="self">要追加等待动作的父组合。</param>
        /// <param name="seconds">等待秒数；非正数在启动后的首次执行完成。</param>
        /// <param name="onDelayFinish">检测到等待结束时调用的可选回调，取消不触发。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Delay(this ISequence self, float seconds, Action onDelayFinish = null)
        {
            return self.Append(global::YLFramework.Actions.Delay.Allocate(seconds, onDelayFinish));
        }

        /// <summary>
        /// 追加动态时长等待动作，每次启动时才求值时长，而非追加时求值。
        /// </summary>
        /// <param name="self">要追加等待动作的父组合。</param>
        /// <param name="delayTimeFactory">每次启动时调用一次的时长工厂，返回秒数。</param>
        /// <param name="onDelayFinish">检测到等待结束时调用的可选回调，取消不触发。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Delay(this ISequence self, Func<float> delayTimeFactory, Action onDelayFinish = null)
        {
            return self.Append(global::YLFramework.Actions.Delay.Allocate(delayTimeFactory, onDelayFinish));
        }
    }
}
