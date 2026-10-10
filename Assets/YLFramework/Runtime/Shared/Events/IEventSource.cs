using System;

namespace YLFramework.Events
{
    /// <summary>不依赖池或 Unity owner 的无参观察入口；泛型事件按注册顺序丢弃 payload。</summary>
    public interface IEventSource
    {
        /// <summary>注册观察者，返回本次注册的精确注销身份；null 返回空句柄。</summary>
        IUnRegister Register(Action callback);
    }

    internal sealed class EmptyUnRegister : IUnRegister
    {
        internal static readonly EmptyUnRegister Instance = new();
        private EmptyUnRegister() { }
        public void UnRegister() { }
    }
}
