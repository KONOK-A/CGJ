using System;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.DataAssets
{
    /// <summary>owner持有至使用结束。Dispose取消本请求、移除本登记并归还根Lease；Asset及子条目均为借读。</summary>
    public sealed class ScriptableObjectAsset<T> : CustomYieldInstruction, IDisposable where T : ScriptableObject
    {
        private ResourceRequest<AssetLease<T>> request;
        private AssetLease<T> lease;
        private Action<T> loaded;
        private Action released;

        public ResourceRequestStatus Status { get; private set; } = ResourceRequestStatus.Pending;
        public Exception Error { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool IsDone => Status != ResourceRequestStatus.Pending;
        public bool IsValid => !IsDisposed && lease != null && lease.IsValid;
        public override bool keepWaiting => !IsDone;
        public T Asset
        {
            get
            {
                if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
                if (Status != ResourceRequestStatus.Succeeded) throw new InvalidOperationException("The ScriptableObject load has not succeeded.", Error);
                return lease.Asset;
            }
        }

        internal ScriptableObjectAsset() { }

        internal void Start(ResourceSystem resource, ResourceAddress address, Action<T> onLoaded = null, Action onReleased = null)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            loaded = onLoaded;
            released = onReleased;
            DataAssetLifetime.Track(this);
            try
            {
                var pending = resource.AcquireAsync<T>(address);
                // 资源加载触发SO.OnEnable时，owner可能已重入关闭；不能再订阅并遗留未领取结果。
                if (IsDisposed) { pending.Dispose(); return; }
                request = pending;
                request.Completed += Complete;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Complete(ResourceRequest<AssetLease<T>> completed)
        {
            if (IsDisposed) return;
            Status = completed.Status;
            Error = completed.Error;
            try
            {
                if (Status == ResourceRequestStatus.Succeeded)
                {
                    lease = completed.TakeResult();
                    loaded?.Invoke(lease.Asset);
                }
            }
            catch (Exception error)
            {
                // 登记校验失败仍须归还已领取的Lease，不能留半成功缓存。
                Error = error;
                Status = ResourceRequestStatus.Failed;
                lease?.Dispose();
                lease = null;
            }
            finally
            {
                loaded = null;
                completed.Completed -= Complete;
                completed.Dispose();
                request = null;
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (!IsDone) Status = ResourceRequestStatus.Canceled;
            var pending = request;
            request = null;
            var onReleased = released;
            released = null;
            loaded = null;
            try
            {
                if (pending != null) { pending.Completed -= Complete; pending.Dispose(); }
                onReleased?.Invoke();
            }
            finally
            {
                lease?.Dispose();
                lease = null;
                DataAssetLifetime.Untrack(this);
            }
        }
    }
}
