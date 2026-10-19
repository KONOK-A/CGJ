using System;
using UnityEngine;

namespace YLFramework.Core
{
    /// <summary>独立 Panel 消费者的结果。进度转发真实资源解析，终态进度为 1 不等于成功，必须检查 Status。</summary>
    public sealed class LoadPanelRequest<T> : YLFramework.Foundation.CustomAsyncOperation<LoadPanelRequest<T>> where T : Panel
    {
        /// <summary>本次成功交付的引用；不延长 Session 寿命，关闭或复用后不会撤回已完成的终态。</summary>
        public T panel { get; private set; }
        /// <summary>独立消费者终态，不能由进度或面板引用推断。</summary>
        public LoadViewStatus Status { get; private set; }
        /// <summary>原始失败原因；清理错误附在既有 Core CleanupException Data 中。</summary>
        public Exception Exception { get; private set; }
        private PanelManager.PanelOperation operation;
        internal LoadPanelRequest(PanelManager.PanelOperation operation)
        {
            this.operation = operation;
            operation.ProgressChanged = value => progress = value;
            operation.Completed = Complete;
        }
        /// <summary>仅提交前取消本消费者。进入首个业务初始化回调后不再接受主动取消。</summary>
        public override void Abort() => operation?.Abort();
        private void Complete(Panel result, Exception error, LoadViewStatus status)
        {
            panel = (T)result;
            Status = status;
            Exception = error;
            operation = null;
            try { Completed(error?.ToString()); }
            catch (Exception notificationError) { Debug.LogException(notificationError); }
        }
    }
}
