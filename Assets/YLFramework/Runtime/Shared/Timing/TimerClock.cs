namespace YLFramework.Timing
{
    /// <summary>计时域；帧域仅代表 Unity rendered frame，不代表 FixedUpdate 或 Action 步数。</summary>
    public enum TimerClock
    {
        Scaled,
        Unscaled,
        RenderedFrame
    }
}
