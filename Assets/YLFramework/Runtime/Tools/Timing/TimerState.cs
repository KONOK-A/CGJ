namespace YLFramework.Utilities.Timing
{
    /// <summary>一次计时任务的生命周期；终态保留正常结束、取消与异常的区别。</summary>
    public enum TimerState
    {
        /// <summary>已配置，尚未交给宿主调度。</summary>
        Created,
        /// <summary>已入堆，等待当前一轮到期。</summary>
        Scheduled,
        /// <summary>当前回调正在执行。</summary>
        Executing,
        /// <summary>已暂停，冻结本轮剩余时间。</summary>
        Paused,
        /// <summary>正常完成全部次数。</summary>
        Completed,
        /// <summary>主动取消或随所属宿主关闭。</summary>
        Cancelled,
        /// <summary>回调执行抛出异常；任务按异常终态清理，异常继续向上传播。</summary>
        Faulted
    }
}
