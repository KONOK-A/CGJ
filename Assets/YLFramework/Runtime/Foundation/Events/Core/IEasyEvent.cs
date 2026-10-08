using System;
using YLFramework.Core.Pool;

namespace YLFramework.Events
{
    /// <summary>
    /// 轻量事件契约：提供类型擦除的无参注册入口与统一注销。
    /// 带参数的事件额外提供对应的强类型 Register / UnRegister / Trigger 方法（见各 EasyEvent 变体）。
    /// </summary>
    public interface IEasyEvent : IOfPool, IPoolType
    {
        /// <summary>
        /// 注册一个无参回调，返回用于注销本次注册的句柄；带参数事件的参数会被丢弃。
        /// </summary>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        IUnRegister Register(Action eventAction);

        /// <summary>
        /// 注销当前事件上的所有回调。
        /// </summary>
        void UnRegisterAll();
    }
}
