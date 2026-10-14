namespace YLFramework.UI
{
    /// <summary>宿主的即时输入资格；不拥有窗口、输入路由或生命周期。</summary>
    public interface IUIInputGate
    {
        /// <summary>当前宿主是否允许子控件接收输入，每次输入事件读取，不缓存状态。</summary>
        bool IsInputAllowed { get; }
    }
}
