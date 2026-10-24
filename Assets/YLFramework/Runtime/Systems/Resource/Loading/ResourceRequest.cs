using System;
using System.Threading;
using UnityEngine;

namespace YLFramework.Resource
{
    public enum ResourceRequestStatus
    {
        Pending,
        Succeeded,
        Failed,
        Canceled,
        Invalidated
    }

    internal interface IResourceRequest : IDisposable
    {
        void PrepareInvalidation();
        void ReleaseInvalidatedResult();
        void NotifyInvalidation();
    }

    /// <summary>独立消费者。yield 只等待终态；TakeResult 恰一次转交结果，Dispose 只释放未领取结果。</summary>
    /// <remarks>所有操作在接受请求的 Unity 主线程执行；完成观察异常逐个报告，不改终态。</remarks>
    public class ResourceRequest<T> : CustomYieldInstruction, IDisposable, IResourceRequest
    {
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Action<T> _releaseResult;
        private T _result;
        private Action _cancel;
        private Func<float> _progressReader;
        private Action<ResourceRequest<T>> _observers;
        private Action<ResourceRequest<T>> _invalidationObservers;
        private Action _invalidationDetach;
        private bool _isClaimed;
        private float _progress;
        internal Action OwnershipEnded;
        internal Action Terminal;

        public ResourceRequestStatus Status
        {
            get; private set;
        }
        public ResourceException Error
        {
            get; private set;
        }
        public bool IsDone => Status != ResourceRequestStatus.Pending;
        public bool IsDisposed
        {
            get; private set;
        }
        public override bool keepWaiting => !IsDone;
        /// <summary>在途借读 producer 进度；成功为 1，其他终态冻结最后样本，不能代替 IsDone 判断。</summary>
        public float Progress
        {
            get
            {
                CheckThread();
                var value = _progressReader == null ? _progress : _progressReader();
                return float.IsNaN(value) ? 0 : Mathf.Clamp01(value);
            }
        }

        internal ResourceRequest(Action<T> releaseResult = null) => this._releaseResult = releaseResult;

        /// <summary>终态后订阅即时回放。通知栈中的退订/Dispose 不跳过已捕获的其他观察者。</summary>
        public event Action<ResourceRequest<T>> Completed
        {
            add
            {
                CheckThread();
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(nameof(ResourceRequest<T>));
                }

                if (value == null)
                {
                    return;
                }

                if (IsDone)
                {
                    foreach (Action<ResourceRequest<T>> observer in value.GetInvocationList())
                    {
                        Invoke(observer);
                    }
                }
                else
                {
                    _observers += value;
                }
            }
            remove
            {
                CheckThread();
                _observers -= value;
            }
        }

        internal void Attach(Action onCancel, Func<float> getProgress)
        {
            _cancel = onCancel;
            _progressReader = getProgress;
        }

        internal bool SetSuccess(T value)
        {
            if (IsDone || IsDisposed)
            {
                _releaseResult?.Invoke(value);
                return false;
            }
            _result = value;
            SetTerminal(ResourceRequestStatus.Succeeded, null);
            return true;
        }

        internal void SetFailure(ResourceRequestStatus status, ResourceException error)
        {
            if (IsDone)
            {
                return;
            }

            SetTerminal(status, error);
        }

        private void SetTerminal(ResourceRequestStatus status, ResourceException error)
        {
            _progress = status == ResourceRequestStatus.Succeeded ? 1 : Progress;
            Status = status;
            Error = error;
            _cancel = null;
            _progressReader = null;
            var terminal = Terminal;
            Terminal = null;
            terminal?.Invoke();
        }

        internal void Notify()
        {
            var snapshot = _observers;
            _observers = null;
            NotifyObservers(snapshot);
        }

        private void NotifyObservers(Action<ResourceRequest<T>> snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (Action<ResourceRequest<T>> observer in snapshot.GetInvocationList())
            {
                Invoke(observer);
            }
        }

        private void Invoke(Action<ResourceRequest<T>> observer)
        {
            try
            {
                observer(this);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        /// <summary>成功结果单次转移。失败/取消抛原资源错误；Dispose 后始终抛 ObjectDisposedException。</summary>
        public T TakeResult()
        {
            CheckThread();
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ResourceRequest<T>));
            }

            if (Status != ResourceRequestStatus.Succeeded)
            {
                if (Error != null)
                {
                    throw Error;
                }

                throw new InvalidOperationException("The resource request has not succeeded.");
            }
            if (_isClaimed)
            {
                throw new InvalidOperationException("The resource result was already taken.");
            }

            _isClaimed = true;
            var value = _result;
            _result = default;
            EndOwnership();
            return value;
        }

        /// <summary>只退出自己的等待；已完成时无副作用，存在其他消费者时不撤销共享生产。</summary>
        public void Cancel()
        {
            CheckThread();
            if (IsDone)
            {
                return;
            }

            var detach = _cancel;
            SetTerminal(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "The resource consumer canceled its wait."));
            detach?.Invoke();
            Notify();
        }

        void IResourceRequest.PrepareInvalidation()
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsDone)
            {
                _invalidationDetach = _cancel;
                SetTerminal(ResourceRequestStatus.Invalidated, new ResourceException(ResourceError.SessionUnavailable, "The resource session ended."));
            }
            IsDisposed = true;
            _invalidationObservers = _observers;
            _observers = null;
        }

        void IResourceRequest.ReleaseInvalidatedResult()
        {
            try
            {
                var detach = _invalidationDetach;
                _invalidationDetach = null;
                detach?.Invoke();
                ReleaseUnclaimedResult();
            }
            finally
            {
                EndOwnership();
            }
        }

        void IResourceRequest.NotifyInvalidation()
        {
            var snapshot = _invalidationObservers;
            _invalidationObservers = null;
            NotifyObservers(snapshot);
        }

        public void Dispose()
        {
            CheckThread();
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            try
            {
                if (!IsDone)
                {
                    Cancel();
                }
                else
                {
                    ReleaseUnclaimedResult();
                }
            }
            finally
            {
                _observers = null;
                EndOwnership();
            }
        }

        private void ReleaseUnclaimedResult()
        {
            if (Status != ResourceRequestStatus.Succeeded || _isClaimed)
            {
                return;
            }

            var value = _result;
            _result = default;
            _isClaimed = true;
            _releaseResult?.Invoke(value);
        }

        private void EndOwnership()
        {
            var ended = OwnershipEnded;
            OwnershipEnded = null;
            ended?.Invoke();
        }

        private void CheckThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _threadId)
            {
                throw new InvalidOperationException("Resource requests must be used on their accepting Unity thread.");
            }
        }
    }
}
