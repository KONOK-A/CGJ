/****************************************************************************
 * Copyright (c) 2015 - 2024 liangxiegame UNDER MIT License
 * 
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * https://gitee.com/liangxiegame/QFramework
 ****************************************************************************/

using System;
using UnityEngine;

namespace YLFramework.Actions
{
    // 以 Time.frameCount 的全局帧号计时，而不是累计 dt 或执行器更新次数；暂停期间帧号仍在推进。
    internal class DelayFrame : IAction
    {
        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus Status { get; set; }

        private static ActionPool<DelayFrame> mPool =
            new ActionPool<DelayFrame>(() => new DelayFrame(), null, 10);

        private Action mOnDelayFinish;

        public static DelayFrame Allocate(int frameCount, Action onDelayFinish = null)
        {
            var delayFrame = mPool.Allocate();
            delayFrame.ActionID = ActionKit.ID_GENERATOR++;
            delayFrame.Reset();
            delayFrame.Deinited = false;
            delayFrame.mDelayedFrameCount = frameCount;
            delayFrame.mOnDelayFinish = onDelayFinish;

            return delayFrame;
        }

        private int mStartFrameCount;
        private int mDelayedFrameCount;

        public void OnStart()
        {
            mStartFrameCount = Time.frameCount;
        }

        public void OnExecute(float dt)
        {
            if (Time.frameCount >= mStartFrameCount + mDelayedFrameCount)
            {
                mOnDelayFinish?.Invoke();
                this.Finish();
            }
        }

        public void OnFinish()
        {
        }

        public void Deinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mOnDelayFinish = null;
                ActionQueue.AddCallback(new ActionQueueRecycleCallback<DelayFrame>(mPool, this));
            }
        }

        public void Reset()
        {
            Status = ActionStatus.NotStart;
            Paused = false;
            mStartFrameCount = 0;
        }
    }

    /// <summary>
    /// 向组合追加按 Unity 全局帧数等待的动作，不按 dt 或执行次数计数。
    /// </summary>
    /// <remarks>暂停动作不冻结全局帧计数，恢复执行时按已过去的帧数判断。</remarks>
    public static class DelayFrameExtension
    {
        /// <summary>
        /// 追加等待指定 Unity 帧数的动作。
        /// </summary>
        /// <param name="self">要追加帧等待动作的父组合。</param>
        /// <param name="frameCount">相对启动帧等待的帧数；非正数在启动后的首次执行完成。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence DelayFrame(this ISequence self, int frameCount)
        {
            return self.Append(global::YLFramework.Actions.DelayFrame.Allocate(frameCount));
        }

        /// <summary>
        /// 追加从动作启动帧起等待一个 Unity 帧的动作。
        /// </summary>
        /// <param name="self">要追加帧等待动作的父组合。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence NextFrame(this ISequence self)
        {
            return self.Append(global::YLFramework.Actions.DelayFrame.Allocate(1));
        }
    }
}
