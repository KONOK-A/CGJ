using System;
using System.Threading;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public enum ResourceRequestStatus { Pending, Succeeded, Failed, Canceled, Invalidated }

    internal interface IResourceRequest : IDisposable
    {
        void PrepareInvalidation();
        void ReleaseInvalidatedResult();
        void NotifyInvalidation();
    }

    /// <summary>独立消费者。yield 只等待终态；TakeResult 恰一次转交结果，Dispose 只释放未领取结果。</summary>
    /// <remarks>所有操作在接受请求的 Unity 主线程执行；完成观察异常逐个报告，不改终态。</remarks>
    public sealed class ResourceRequest<T> : CustomYieldInstruction, IDisposable, IResourceRequest
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Action<T> releaseResult;
        private T result;
        private Action cancel;
        private Func<float> progressReader;
        private Action<ResourceRequest<T>> observers;
        private Action<ResourceRequest<T>> invalidationObservers;
        private bool claimed;
        private float progress;
        internal Action OwnershipEnded;
        internal Action Terminal;

        public ResourceRequestStatus Status { get; private set; }
        public ResourceException Error { get; private set; }
        public bool IsDone => Status != ResourceRequestStatus.Pending;
        public bool IsDisposed { get; private set; }
        public override bool keepWaiting => !IsDone;
        /// <summary>在途借读 producer 进度；成功为 1，其他终态冻结最后样本，不能代替 IsDone 判断。</summary>
        public float Progress
        {
            get
            {
                CheckThread();
                var value = progressReader == null ? progress : progressReader();
                return float.IsNaN(value) ? 0 : Mathf.Clamp01(value);
            }
        }

        internal ResourceRequest(Action<T> releaseResult = null) => this.releaseResult = releaseResult;

        /// <summary>终态后订阅即时回放。通知栈中的退订/Dispose 不跳过已捕获的其他观察者。</summary>
        public event Action<ResourceRequest<T>> Completed
        {
            add
            {
                CheckThread();
                if (IsDisposed) throw new ObjectDisposedException(nameof(ResourceRequest<T>));
                if (value == null) return;
                if (IsDone)
                    foreach (Action<ResourceRequest<T>> observer in value.GetInvocationList()) Invoke(observer);
                else observers += value;
            }
            remove { CheckThread(); observers -= value; }
        }

        internal void Attach(Action onCancel, Func<float> getProgress)
        {
            cancel = onCancel;
            progressReader = getProgress;
        }

        internal bool SetSuccess(T value)
        {
            if (IsDone || IsDisposed) { releaseResult?.Invoke(value); return false; }
            result = value;
            SetTerminal(ResourceRequestStatus.Succeeded, null);
            return true;
        }

        internal void SetFailure(ResourceRequestStatus status, ResourceException error)
        {
            if (IsDone) return;
            SetTerminal(status, error);
        }

        private void SetTerminal(ResourceRequestStatus status, ResourceException error)
        {
            progress = status == ResourceRequestStatus.Succeeded ? 1 : Progress;
            Status = status;
            Error = error;
            cancel = null;
            progressReader = null;
            var terminal = Terminal;
            Terminal = null;
            terminal?.Invoke();
        }

        internal void Notify()
        {
            var snapshot = observers;
            observers = null;
            NotifyObservers(snapshot);
        }

        private void NotifyObservers(Action<ResourceRequest<T>> snapshot)
        {
            if (snapshot == null) return;
            foreach (Action<ResourceRequest<T>> observer in snapshot.GetInvocationList()) Invoke(observer);
        }

        private void Invoke(Action<ResourceRequest<T>> observer)
        {
            try { observer(this); }
            catch (Exception error) { Debug.LogException(error); }
        }

        /// <summary>成功结果单次转移。失败/取消抛原资源错误；Dispose 后始终抛 ObjectDisposedException。</summary>
        public T TakeResult()
        {
            CheckThread();
            if (IsDisposed) throw new ObjectDisposedException(nameof(ResourceRequest<T>));
            if (Status != ResourceRequestStatus.Succeeded)
            {
                if (Error != null) throw Error;
                throw new InvalidOperationException("The resource request has not succeeded.");
            }
            if (claimed) throw new InvalidOperationException("The resource result was already taken.");
            claimed = true;
            var value = result;
            result = default;
            EndOwnership();
            return value;
        }

        /// <summary>只退出自己的等待；已完成时无副作用，不撤销共享原生生产。</summary>
        public void Cancel()
        {
            CheckThread();
            if (IsDone) return;
            var detach = cancel;
            SetTerminal(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "The resource consumer canceled its wait."));
            detach?.Invoke();
            Notify();
        }

        void IResourceRequest.PrepareInvalidation()
        {
            if (IsDisposed) return;
            if (!IsDone)
            {
                var detach = cancel;
                SetTerminal(ResourceRequestStatus.Invalidated, new ResourceException(ResourceError.SessionUnavailable, "The resource session ended."));
                detach?.Invoke();
            }
            IsDisposed = true;
            invalidationObservers = observers;
            observers = null;
        }

        void IResourceRequest.ReleaseInvalidatedResult()
        {
            try { ReleaseUnclaimedResult(); }
            finally { EndOwnership(); }
        }

        void IResourceRequest.NotifyInvalidation()
        {
            var snapshot = invalidationObservers;
            invalidationObservers = null;
            NotifyObservers(snapshot);
        }

        public void Dispose()
        {
            CheckThread();
            if (IsDisposed) return;
            IsDisposed = true;
            try
            {
                if (!IsDone) Cancel();
                else ReleaseUnclaimedResult();
            }
            finally { observers = null; EndOwnership(); }
        }

        private void ReleaseUnclaimedResult()
        {
            if (Status != ResourceRequestStatus.Succeeded || claimed) return;
            var value = result;
            result = default;
            claimed = true;
            releaseResult?.Invoke(value);
        }

        private void EndOwnership()
        {
            var ended = OwnershipEnded;
            OwnershipEnded = null;
            ended?.Invoke();
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("Resource requests must be used on their accepting Unity thread.");
        }
    }
}
