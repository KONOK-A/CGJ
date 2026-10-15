using System;
using System.Collections.Generic;
using System.Threading;

namespace YLFramework.Services.Resource
{
    /// <summary>typed 批量/Group 的拥有型结果。每个成员是独立正式 Lease，Dispose 只释放本批责任；Errors 保留可选成员失败。</summary>
    public sealed class AssetBatchLease<T> : IDisposable where T : UnityEngine.Object
    {
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        public IReadOnlyList<AssetLease<T>> Items { get; }
        public IReadOnlyList<ResourceException> Errors { get; }
        public bool IsDisposed { get; private set; }
        internal AssetBatchLease(IEnumerable<AssetLease<T>> items, IEnumerable<ResourceException> errors)
        { Items = ResourceCatalog.Copy(items); Errors = ResourceCatalog.Copy(errors); }
        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId) throw new InvalidOperationException("Resource batches must be used on their accepting Unity thread.");
            if (IsDisposed) return;
            IsDisposed = true;
            List<Exception> errors = null;
            foreach (var item in Items)
                try { item.Dispose(); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            if (errors != null) throw new AggregateException("Resource batch cleanup failed.", errors);
        }
    }
}
