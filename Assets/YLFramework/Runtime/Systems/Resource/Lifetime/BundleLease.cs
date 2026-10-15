using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>显式 Bundle 图根责任。native 包仅供在有效 Lease 内借读，不得自行 Unload；资产消费者优先使用 AssetLease。</summary>
    /// <remarks>调用方直接启动的 native 操作不进入 Resource 的内部操作账本，必须自行持有本 Lease 到操作及使用结束。</remarks>
    public sealed class BundleLease : IDisposable
    {
        private ResourceSystem.BundleRecord record;
        internal readonly bool Public;
        public string ProjectId { get; }
        public string BundleId { get; }
        public bool IsDisposed => record == null;
        public bool IsValid => record != null && record.Owner.IsSessionActive && record.Bundle;
        public AssetBundle Bundle
        {
            get
            {
                if (record == null) throw new ObjectDisposedException(nameof(BundleLease));
                record.Owner.CheckThread();
                if (!record.Owner.IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "The Bundle lease belongs to an ended session.");
                if (!record.Bundle) throw new ResourceException(ResourceError.LoadFailed, "The leased Bundle is not ready or was unloaded externally.");
                return record.Bundle;
            }
        }
        internal BundleLease(ResourceSystem.BundleRecord record, bool publicLease)
        {
            this.record = record;
            Public = publicLease;
            ProjectId = record.Snapshot.Document.ProjectId;
            BundleId = record.Descriptor.LogicalId;
            record.References++;
            record.RootPins++;
        }
        public void Dispose()
        {
            if (record == null) return;
            record.Owner.CheckThread();
            var owned = record;
            record = null;
            owned.Owner.ReleaseBundle(this, owned);
        }
    }
}
