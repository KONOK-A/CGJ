using System;
using System.Collections.Generic;

namespace YLFramework.Events
{
    /// <summary>
    /// 事件注销句柄：调用 <see cref="UnRegister"/> 注销对应的注册。
    /// 约定实现应尽量幂等，重复调用不产生副作用。
    /// </summary>
    public interface IUnRegister
    {
        /// <summary>
        /// 执行注销。重复调用应为安全空操作。
        /// </summary>
        void UnRegister();
    }

    /// <summary>
    /// 持有多个注销句柄的集合，可通过 RegisterListExtension 统一注销。
    /// </summary>
    public interface IUnRegisterList
    {
        /// <summary>
        /// 当前持有的注销句柄列表。
        /// </summary>
        List<IUnRegister> UnRegisterList { get; }
    }

    /// <summary>
    /// 基于委托的自定义注销句柄；成功后清空回调，同实例重入为空操作，失败后可重试。
    /// </summary>
    public class CustomUnRegister : IUnRegister
    {
        private CallbackUnRegister _callback;
        private bool _executing;

        /// <summary>
        /// 使用自定义注销回调创建句柄。
        /// </summary>
        /// <param name="onUnRegister">注销时执行的回调。</param>
        public CustomUnRegister(Action onUnRegister)
        {
            _callback = new CallbackUnRegister(onUnRegister);
        }

        /// <summary>
        /// 执行自定义注销回调；成功后重复调用为空操作，失败保留回调供重试。
        /// </summary>
        public void UnRegister()
        {
            if (_executing) return;
            _executing = true;
            try { _callback.UnRegister(); }
            finally { _executing = false; }
        }
    }
}
