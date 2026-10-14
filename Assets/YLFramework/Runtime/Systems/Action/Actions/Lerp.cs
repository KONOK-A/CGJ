/****************************************************************************
 * Copyright (c) 2016 - 2024 liangxiegame UNDER MIT License
 * 
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * https://gitee.com/liangxiegame/QFramework
 ****************************************************************************/

using System;
using UnityEngine;

namespace YLFramework.Actions
{
    /// <summary>
    /// 按执行传入的 dt，从 A 到 B 进行线性插值的池化动作。
    /// </summary>
    public class Lerp : IAction
    {
        private static readonly ActionPool<Lerp> mPool =
            new ActionPool<Lerp>(() => new Lerp(), null, 10);

        /// <summary>起始值，启动时先通知该值。</summary>
        public float A;

        /// <summary>目标值，到达时长后通知该值。</summary>
        public float B;

        /// <summary>插值时长（秒）；非正数在启动后的首次执行输出终点。</summary>
        public float Duration;

        /// <summary>接收起点、过程中间值及终点的回调。</summary>
        public Action<float> OnLerp;

        /// <summary>到达插值终点后调用；直接标记 Finish 不会触发。</summary>
        public Action OnLerpFinish;

        private float mCurrentTime = 0.0f;

        /// <summary>
        /// 分配并初始化线性插值动作。
        /// </summary>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">插值时长（秒）；非正数在启动后的首次执行输出终点。</param>
        /// <param name="onLerp">接收起点、过程中间值及终点的可选回调。</param>
        /// <param name="onLerpFinish">到达终点后调用的可选回调，强制 Finish 不触发。</param>
        /// <returns>尚未启动的插值动作，时间源由执行器选择。</returns>
        public static Lerp Allocate(float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            var retNode = mPool.Allocate();
            retNode.ActionID = ActionKit.ID_GENERATOR++;
            retNode.Deinited = false;
            retNode.Reset();
            retNode.A = a;
            retNode.B = b;
            retNode.Duration = duration;
            retNode.OnLerp = onLerp;
            retNode.OnLerpFinish = onLerpFinish;
            return retNode;
        }

        /// <inheritdoc />
        public bool Paused { get; set; }
        /// <summary>清零累计时间并重置执行阶段和暂停状态，保留插值配置与回调。</summary>
        public void Reset()
        {
            Status = ActionStatus.NotStart;
            Paused = false;
            mCurrentTime = 0.0f;
        }

        /// <summary>清除插值回调并安排延迟回收，不触发插值结束回调。</summary>
        public void Deinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                OnLerp = null;
                OnLerpFinish = null;
                ActionQueue.AddCallback(new ActionQueueRecycleCallback<Lerp>(mPool, this));
            }
        }

        /// <inheritdoc />
        public ulong ActionID { get; set; }
        /// <inheritdoc />
        public ActionStatus Status { get; set; }
        /// <summary>清零累计时间并输出起点，不在启动阶段输出终点。</summary>
        public void OnStart()
        {
            mCurrentTime = 0.0f;
            OnLerp?.Invoke(Mathf.Lerp(A, B, 0));
        }

        /// <summary>累计时间并输出插值结果；到达时长后标记完成并调用插值结束回调。</summary>
        /// <param name="dt">本次累计的时间增量，单位为秒。</param>
        public void OnExecute(float dt)
        {
            mCurrentTime += dt;
            if (mCurrentTime < Duration)
            {
                OnLerp?.Invoke(Mathf.Lerp(A, B, mCurrentTime / Duration));
            }
            else
            {
                this.Finish();
                OnLerp?.Invoke(Mathf.Lerp(A, B, 1.0f));
                OnLerpFinish?.Invoke();
            }
        }

        /// <inheritdoc />
        public void OnFinish()
        {
        }

        /// <inheritdoc />
        public bool Deinited { get; set; }
    }

    /// <summary>
    /// 向组合追加线性插值动作的链式扩展。
    /// </summary>
    public static class LerpExtension
    {
        /// <summary>
        /// 追加从 a 到 b 的线性插值，启动时输出起点，随后按 dt 推进至终点。
        /// </summary>
        /// <param name="self">要追加插值动作的父组合。</param>
        /// <param name="a">起始值。</param>
        /// <param name="b">目标值。</param>
        /// <param name="duration">插值时长（秒）；非正数在启动后的首次执行输出终点。</param>
        /// <param name="onLerp">接收起点、过程中间值及终点的可选回调。</param>
        /// <param name="onLerpFinish">到达终点后调用的可选回调，强制 Finish 不触发。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Lerp(this ISequence self, float a, float b, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            return self.Append(global::YLFramework.Actions.Lerp.Allocate(a, b, duration, onLerp, onLerpFinish));
        }

        /// <summary>
        /// 追加从 0 到 1 的线性插值，启动时输出 0，到达时长后输出 1。
        /// </summary>
        /// <param name="self">要追加插值动作的父组合。</param>
        /// <param name="duration">插值时长（秒）；非正数在启动后的首次执行输出终点。</param>
        /// <param name="onLerp">接收起点、过程中间值及终点的可选回调。</param>
        /// <param name="onLerpFinish">到达终点后调用的可选回调，强制 Finish 不触发。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Lerp01(this ISequence self, float duration, Action<float> onLerp = null, Action onLerpFinish = null)
        {
            return self.Append(global::YLFramework.Actions.Lerp.Allocate(0, 1, duration, onLerp, onLerpFinish));
        }
    }
}
