namespace YLFramework.Timing
{
    /// <summary>一次 run 的状态；Executing 尚未完成，末轮执行中也不再接受控制。</summary>
    public enum TimerState
    {
        Invalidated,
        Scheduled,
        Paused,
        Executing,
        Completed,
        Cancelled,
        Faulted
    }

    /// <summary>取消通知的原因；Faulted 与正常完成不会触发取消通知。</summary>
    public enum TimerCancellationReason
    {
        Cancelled,
        SchedulerDisposed
    }
}
