using System;

namespace YLFramework.Timing.Internal
{
    // 普通 run 不池化；句柄保留历史终态，而终态清除调度归属及用户委托。
    // Generation 与执行租约独立于可复用存储；后续复用槽必须更换代次，不能重发旧句柄。
    internal sealed class TimerRun
    {
        internal TimerScheduler Owner;
        internal ulong Generation;
        internal ulong ExecutingGeneration;
        // 仅 Executing 的非末轮使用：请求回调退出后的暂停，不提前改写当前执行状态。
        internal bool PauseNext;
        internal TimerState State;
        internal TimerClock Clock;
        internal Action Callback;
        internal Action<long> TickCallback;
        internal Action<TimerCancellationReason> OnCancelled;
        internal long ExecutionCount;
        internal int? RemainingCount;
        internal double IntervalSeconds;
        internal long IntervalFrames;
        internal double DueSeconds;
        internal long DueFrame;
        internal double FrozenSeconds;
        internal long FrozenFrames;
        internal ulong Order;
        internal TimerHeap Heap;
        internal int HeapIndex;
        internal int PendingIndex = -1;
    }
}
