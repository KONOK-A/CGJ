using System;

namespace YLFramework.Events
{
    /// <summary>
    /// 引用类型的一次性注销句柄：调用（含重入调用）时先消费回调再执行，
    /// 回调抛异常后也不会再次执行；<see cref="Dispose"/> 等价于 <see cref="UnRegister"/>。
    /// </summary>
    public class SingleUseUnRegister : IUnRegister, IDisposable
    {
        private CallbackUnRegister _callback;

        /// <summary>
        /// 创建一次性注销句柄。
        /// </summary>
        /// <param name="callback">注销时执行的回调，可为 null。</param>
        public SingleUseUnRegister(Action callback)
        {
            _callback = new CallbackUnRegister(callback, CallbackReleaseMode.BeforeInvoke);
        }

        /// <inheritdoc />
        public void UnRegister() => _callback.UnRegister();

        /// <inheritdoc />
        public void Dispose() => UnRegister();
    }
}
