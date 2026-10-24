using System;
using UnityEngine;

namespace YLFramework.Async
{
    public class CustomAsyncOperation<T> : CustomYieldInstruction where T : CustomAsyncOperation<T>
    {
        [Obsolete("已过时,请使用AddCompleteEvent代替!")]
        public event Action<T> Completed = null;

        private Action<T> _completed;
        private bool _isCompleted = false;
        private string _error = string.Empty;
        private const string CheckingError = "正在检测中...";

        public bool IsDone => IsCompleted;

        protected bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                _isCompleted = value;
                if (_isCompleted)
                {
                    Completed?.Invoke(this as T);
                    _completed?.Invoke(this as T);
                    OnCompleted();
                }
            }
        }

        public float Progress
        {
            get; protected set;
        }

        public string Error
        {
            get
            {
                if (!IsDone)
                {
                    return CheckingError;
                }

                return _error;
            }
            private set => _error = value;
        }

        public override bool keepWaiting => !IsDone;

        protected virtual void OnCompleted()
        {
        }

        protected void Complete(string error = null)
        {
            Error = error;
            Complete();
        }

        private void Complete()
        {
            Progress = 1;
            if (IsCompleted)
            {
                return;
            }

            IsCompleted = true;
        }

        protected void CompleteWithoutNotify(string error = null)
        {
            Error = error;
            CompleteWithoutNotify();
        }

        private void CompleteWithoutNotify()
        {
            Progress = 1;
            if (IsCompleted)
            {
                return;
            }

            _isCompleted = true;
            OnCompleted();
        }

        public void AddCompleteEvent(Action<T> action)
        {
            _completed += action;
            // 已完成的请求对迟到订阅者同步通知，异常仍由调用方观察。
            if (IsDone)
            {
                action?.Invoke(this as T);
            }
        }

        public void RemoveCompleteEvent(Action<T> action)
        {
            _completed -= action;
        }

        public void RemoveAllCompleteEvent()
        {
            _completed = null;
        }

        public virtual void Abort()
        {
            Complete("request abort!");
            OnAbort();
        }

        protected virtual void OnAbort()
        {
        }
    }
}
