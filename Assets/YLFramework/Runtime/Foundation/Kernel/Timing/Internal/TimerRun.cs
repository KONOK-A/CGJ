using System;

namespace YLFramework.Timing.Internal
{
    // 普通 run 不池化；句柄保留历史终态，而终态清除调度归属及用户委托。
    // 显式复用槽更换代次；旧回调栈只允许收尾捕获的代次。
    internal sealed class TimerRun
    {
        internal TimerScheduler Owner;
        internal TimerScope Scope;
        internal TimerGameObjectBinding Binding;
        internal string Name;
        internal bool NeedsAnchor;
        internal long EarliestFrame;
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
