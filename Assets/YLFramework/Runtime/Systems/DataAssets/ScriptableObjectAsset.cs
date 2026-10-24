using System;
using UnityEngine;
using YLFramework.Resource;

namespace YLFramework.DataAssets
{
    /// <summary>owner持有至使用结束。Dispose取消本请求、移除本登记并归还根Lease；Asset及子条目均为借读。</summary>
    public sealed class ScriptableObjectAsset<T> : CustomYieldInstruction, IDisposable where T : ScriptableObject
    {
        private ResourceRequest<AssetLease<T>> _request;
        private AssetLease<T> _lease;
        private Action<T> _loaded;
        private Action _released;

        public ResourceRequestStatus Status { get; private set; } = ResourceRequestStatus.Pending;
        public Exception Error
        {
            get; private set;
        }
        public bool IsDisposed
        {
            get; private set;
        }
        public bool IsDone => Status != ResourceRequestStatus.Pending;
        public bool IsValid => !IsDisposed && _lease != null && _lease.IsValid;
        public override bool keepWaiting => !IsDone;
        public T Asset
        {
            get
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                if (Status != ResourceRequestStatus.Succeeded)
                {
                    throw new InvalidOperationException("The ScriptableObject load has not succeeded.", Error);
                }

                return _lease.Asset;
            }
        }

        internal ScriptableObjectAsset()
        {
        }

        internal void Start(ResourceSystem resource, ResourceAddress address, Action<T> onLoaded = null, Action onReleased = null)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            _loaded = onLoaded;
            _released = onReleased;
            DataAssetLifetime.Track(this);
            try
            {
                var pending = resource.AcquireAsync<T>(address);
                // 资源加载触发SO.OnEnable时，owner可能已重入关闭；不能再订阅并遗留未领取结果。
                if (IsDisposed)
                {
                    pending.Dispose();
                    return;
                }
                _request = pending;
                _request.Completed += Complete;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Complete(ResourceRequest<AssetLease<T>> completed)
        {
            if (IsDisposed)
            {
                return;
            }

            Status = completed.Status;
            Error = completed.Error;
            try
            {
                if (Status == ResourceRequestStatus.Succeeded)
                {
                    _lease = completed.TakeResult();
                    _loaded?.Invoke(_lease.Asset);
                }
            }
            catch (Exception error)
            {
                // 登记校验失败仍须归还已领取的Lease，不能留半成功缓存。
                Error = error;
                Status = ResourceRequestStatus.Failed;
                _lease?.Dispose();
                _lease = null;
            }
            finally
            {
                _loaded = null;
                completed.Completed -= Complete;
                completed.Dispose();
                _request = null;
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            if (!IsDone)
            {
                Status = ResourceRequestStatus.Canceled;
            }

            var pending = _request;
            _request = null;
            var onReleased = _released;
            _released = null;
            _loaded = null;
            try
            {
                if (pending != null)
                {
                    pending.Completed -= Complete;
                    pending.Dispose();
                }
                onReleased?.Invoke();
            }
            finally
            {
                _lease?.Dispose();
                _lease = null;
                DataAssetLifetime.Untrack(this);
            }
        }
    }
}
