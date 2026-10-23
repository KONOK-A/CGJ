using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    // 仅描述创建这一次真实租约的调用来源；不参与图引用、生产或释放判断。
    internal readonly struct BundleLeaseOrigin
    {
        internal readonly ResourceAddress? Address;
        internal readonly string GroupId;
        internal readonly long? SceneSequence;
        internal BundleLeaseOrigin(ResourceAddress? address = null, string groupId = null, long? sceneSequence = null)
        {
            Address = address;
            GroupId = groupId;
            SceneSequence = sceneSequence;
        }
    }

    /// <summary>显式 Bundle 图根责任。native 包仅供在有效 Lease 内借读，不得自行 Unload；资产消费者优先使用 AssetLease。</summary>
    /// <remarks>调用方直接启动的 native 操作不进入 Resource 的内部操作账本，必须自行持有本 Lease 到操作及使用结束。</remarks>
    public sealed class BundleLease : IDisposable
    {
        private ResourceSystem.BundleRecord _record;
        internal readonly bool IsPublic;
        internal BundleLease Previous;
        internal BundleLease Next;
        internal ResourceSystem.BundleRecord Record => _record;
        internal readonly BundleLeaseOrigin Origin;
        private string _ownerLabel;
        public string ProjectId
        {
            get;
        }
        public string BundleId
        {
            get;
        }
        public bool IsDisposed => _record == null;
        public bool IsValid => _record != null && _record.Owner.IsSessionActive && _record.Bundle;
        /// <summary>真实持有者的可读说明，不影响 Bundle 图根计数；null 表示未标注。</summary>
        public string OwnerLabel
        {
            get => _ownerLabel;
            set
            {
                if (_record == null)
                {
                    throw new ObjectDisposedException(nameof(BundleLease));
                }

                _record.Owner.CheckThread();
                _ownerLabel = value;
            }
        }
        public AssetBundle Bundle
        {
            get
            {
                if (_record == null)
                {
                    throw new ObjectDisposedException(nameof(BundleLease));
                }

                _record.Owner.CheckThread();
                if (!_record.Owner.IsSessionActive)
                {
                    throw new ResourceException(ResourceError.SessionUnavailable, "The Bundle lease belongs to an ended session.");
                }

                if (!_record.Bundle)
                {
                    throw new ResourceException(ResourceError.LoadFailed, "The leased Bundle is not ready or was unloaded externally.");
                }

                return _record.Bundle;
            }
        }
        internal BundleLease(ResourceSystem.BundleRecord record, bool publicLease, BundleLeaseOrigin origin = default)
        {
            this._record = record;
            IsPublic = publicLease;
            Origin = origin;
            ProjectId = record.Snapshot.Document.ProjectId;
            BundleId = record.Descriptor.LogicalId;
            record.References++;
            record.RootPins++;
            record.Owner.RegisterBundleDiagnostics(this);
        }
        public void Dispose()
        {
            if (_record == null)
            {
                return;
            }

            _record.Owner.CheckThread();
            var owned = _record;
            owned.Owner.UnregisterBundleDiagnostics(this);
            _record = null;
            owned.Owner.ReleaseBundle(this, owned);
        }
    }
}
