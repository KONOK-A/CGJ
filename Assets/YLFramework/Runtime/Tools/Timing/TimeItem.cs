/****************************************************************************
 * Copyright (c) 2017 snowcold
 * Copyright (c) 2017 liangxie
 ****************************************************************************/
using System;
using YLFramework.Core.Pool;

namespace YLFramework.Utilities.Timing
{
    /// <summary>
    /// 计时任务句柄：承载回调、重复次数与运行状态，由 <see cref="Timer"/> 创建并调度。
    /// 实例在释放后不复用，避免旧持有者通过旧句柄影响新任务；终态在清理后仍保留在 <see cref="State"/> 中。
    /// </summary>
    public class TimeItem : IBinaryHeapElement, IOfPool, IPoolType
    {
        private float mDelayTime;
        private bool mIsEnabled = true;
        private int mRepeatCount;
        private Action<int> mCallback;
        private int mCallbackTick;
        internal Timer Owner;
        internal BinaryHeap<TimeItem> Heap;
        internal TimerScope Scope;
        internal string Flag;
        // 调度租约版本：每次投递或重新配置时递增，使旧出堆收尾能识别回调内的重投递。
        internal uint ScheduleVersion;
        // 当前正在执行的租约版本；仅执行期间非零，取消执行中任务时用来区分是否需要等待回调结束。
        internal uint ExecutingVersion;
        internal bool IgnoreTimeScale;
        internal bool PauseNext;
        internal float FrozenRemaining;

        /// <summary>任务状态；回调释放后仍保留 Completed、Cancelled 或 Faulted。</summary>
        public TimerState State { get; internal set; } = TimerState.Created;

        /// <summary>是否仍被调度（含暂停与执行中）；所属宿主被销毁后立即变为 false。</summary>
        public bool IsActive => HasActiveState && !ReferenceEquals(Owner, null) && Owner;

        // 只看状态本身，不检查宿主存活；IsActive 在此基础上叠加宿主存活检查。
        internal bool HasActiveState => State == TimerState.Scheduled || State == TimerState.Executing || State == TimerState.Paused;

        /// <summary>当前一轮剩余秒数：等待中按所属时钟计算且不低于零，暂停时取冻结值，执行中和终态为零。</summary>
        public float RemainingSeconds => !IsActive ? 0 : State == TimerState.Paused ? FrozenRemaining
            : State == TimerState.Scheduled
                ? Math.Max(0, SortScore - Owner.ReadClock(IgnoreTimeScale)) : 0;

        /// <summary>创建并初始化计时任务。</summary>
        /// <param name="callback">到期回调，参数为从 1 开始的触发次数；可为 null。</param>
        /// <param name="delayTime">首次及每轮间隔的秒数；不能为 NaN 或 Infinity。</param>
        /// <param name="repeatCount">执行次数；负数表示无限重复直到取消。</param>
        /// <returns>Created 状态的任务，交给 Timer 的 Post 系列入口调度。</returns>
        public static TimeItem Allocate(Action<int> callback, float delayTime, int repeatCount = 1)
        {
            // 实例会作为取消句柄暴露，不复用以免旧持有者取消新任务。
            var item = new TimeItem();
            item.Set(callback, delayTime, repeatCount);
            return item;
        }

        /// <summary>重新配置回调与计时参数，并把任务恢复到 Created 状态；仅适用于未被宿主拥有的任务。</summary>
        /// <exception cref="ArgumentOutOfRangeException">delayTime 为 NaN 或 Infinity 时抛出。</exception>
        /// <exception cref="InvalidOperationException">任务仍被宿主拥有或仍在堆中时抛出。</exception>
        public void Set(Action<int> callback, float delayTime, int repeatCount)
        {
            if (float.IsNaN(delayTime) || float.IsInfinity(delayTime)) throw new ArgumentOutOfRangeException(nameof(delayTime));
            if (!ReferenceEquals(Owner, null) || HeapIndex != 0) throw new InvalidOperationException("Cannot configure an owned TimeItem.");
            mCallbackTick = 0;
            mCallback = callback;
            mDelayTime = delayTime;
            mRepeatCount = repeatCount;
            mIsEnabled = true;
            IsRecycled = false;
            State = TimerState.Created;
            PauseNext = false;
            FrozenRemaining = 0;
            ScheduleVersion++;
        }

        /// <summary>由调度方（Timer）在任务到期时调用：扣减剩余次数并执行一次回调，传入从 1 开始的触发次数。</summary>
        public void OnTimeTick()
        {
            if (!mIsEnabled || IsRecycled) return;
            if (mRepeatCount > 0) --mRepeatCount;
            // 最后一轮在进入用户回调前释放名字，允许同名新任务；仍由当前租约负责收尾。
            if (!ReferenceEquals(Owner, null) && State == TimerState.Executing && !NeedRepeat()) Scope.ReleaseName(this);
            mCallback?.Invoke(++mCallbackTick);
        }

        /// <summary>当前回调；取消或回收后为 null。</summary>
        public Action<int> callback => mCallback;
        /// <summary>任务的到期时间（调度时的当前时钟 + 延迟），同时作为堆排序分数；调度期间不要直接改写。</summary>
        public float SortScore { get; set; }
        /// <summary>任务在堆中的位置，从 1 开始，零表示未入堆；由堆维护。</summary>
        public int HeapIndex { get; set; }
        /// <summary>是否允许继续执行；取消或回收后为 false，重新 Set 后恢复。</summary>
        public bool IsEnabled => mIsEnabled;
        /// <summary>任务是否已经释放。</summary>
        public bool IsRecycled { get; set; }

        /// <summary>取消任务并释放回调引用；执行中的任务不打断当前回调，但阻止后续重复，已完成 / 取消 / 异常的任务保持原终态。</summary>
        public void Cancel()
        {
            if (!ReferenceEquals(Owner, null)) { Owner.CancelItem(this); return; }
            if (State == TimerState.Completed || State == TimerState.Cancelled || State == TimerState.Faulted) return;
            State = TimerState.Cancelled;
            mIsEnabled = false;
            mCallback = null;
        }

        /// <summary>暂停当前等待；在循环回调内调用表示暂停下一轮完整周期，单次或最后一轮已开始执行时返回 false。</summary>
        public bool Pause() => !ReferenceEquals(Owner, null) && Owner.PauseItem(this);

        /// <summary>恢复原时间源上的等待；循环回调中可撤销暂停下一轮的请求。</summary>
        public bool Resume() => !ReferenceEquals(Owner, null) && Owner.ResumeItem(this);

        // 取消执行中任务或宿主关闭时调用：立即释放回调引用，任务本身由出堆租约在回调结束后收尾。
        internal void ClearCallback()
        {
            mIsEnabled = false;
            mCallback = null;
        }

        /// <summary>是否还有后续触发：正数为剩余次数，负数（无限重复）始终为 true。</summary>
        public bool NeedRepeat() => mRepeatCount != 0;
        /// <summary>读取每次触发之间配置的延迟秒数。</summary>
        public float DelayTime() => mDelayTime;
        /// <summary>任务分数变化后修复所在堆。</summary>
        public void RebuildHeap<T>(BinaryHeap<T> heap) where T : IBinaryHeapElement => heap.RebuildAtIndex(HeapIndex);

        /// <summary>清除已出堆任务的回调及计数状态，保留终态；Created 状态按取消处理。</summary>
        /// <exception cref="InvalidOperationException">任务仍被宿主拥有或仍在堆中时抛出。</exception>
        public void OnRecycled()
        {
            if (!ReferenceEquals(Owner, null) || HeapIndex != 0) throw new InvalidOperationException("Cannot recycle an owned TimeItem.");
            mCallbackTick = 0;
            mCallback = null;
            mIsEnabled = false;
            mRepeatCount = 0;
            SortScore = 0;
            if (State == TimerState.Created) State = TimerState.Cancelled;
        }

        /// <summary>释放任务；保留旧方法名，但不进入共享对象池，重复调用无副作用。</summary>
        /// <exception cref="InvalidOperationException">任务仍被宿主拥有或仍在堆中时抛出。</exception>
        public void Recycle2Cache()
        {
            if (IsRecycled) return;
            OnRecycled();
            IsRecycled = true;
        }

        /// <summary>YL 回收契约入口，清除已出堆任务的数据，不复用公开句柄。</summary>
        public void OnRecycle() => OnRecycled();

        /// <summary>YL 自管理释放入口；释放回调但不加入共享对象池。</summary>
        public void RecycleToCache() => Recycle2Cache();
    }
}
