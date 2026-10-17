using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>一次取得对应一次独立责任；只释放自己，不池化公开身份，不使用 finalizer。</summary>
    public sealed class AssetLease<T> : IDisposable, IAssetLeaseDiagnostics where T : UnityEngine.Object
    {
        private ResourceSystem.AssetRecord record;
        private readonly bool instancePin;
        private string ownerLabel;
        private IAssetLeaseDiagnostics previous;
        private IAssetLeaseDiagnostics next;
        internal ResourceSystem.AssetRecord Record => record;
        public ResourceAddress Address { get; }
        public bool IsDisposed => record == null;
        public bool IsValid => record != null && record.Owner.IsSessionActive && record.Asset;

        /// <summary>调用方显式填写的真实责任说明；未填写时诊断保持未标注，不参与 retain/release。</summary>
        public string OwnerLabel
        {
            get => ownerLabel;
            set
            {
                if (record == null) throw new ObjectDisposedException(nameof(AssetLease<T>));
                record.Owner.CheckThread();
                ownerLabel = value;
            }
        }

        IAssetLeaseDiagnostics IAssetLeaseDiagnostics.Previous { get => previous; set => previous = value; }
        IAssetLeaseDiagnostics IAssetLeaseDiagnostics.Next { get => next; set => next = value; }
        bool IAssetLeaseDiagnostics.IsInstancePin => instancePin;

        /// <summary>借读对象，仅在本 Lease 有效期间使用；外部销毁和会话退出均明确拒绝。</summary>
        public T Asset
        {
            get
            {
                if (record == null) throw new ObjectDisposedException(nameof(AssetLease<T>));
                record.Owner.CheckThread();
                if (!record.Owner.IsSessionActive)
                    throw new ResourceException(ResourceError.SessionUnavailable, "The asset lease belongs to an ended session.", address: Address);
                if (!record.Asset)
                    throw new ResourceException(ResourceError.LoadFailed, "The leased Unity object was destroyed externally.", address: Address);
                return (T)record.Asset;
            }
        }

        internal AssetLease(ResourceSystem.AssetRecord record, ResourceAddress address, bool instancePin = false)
        {
            this.record = record;
            this.instancePin = instancePin;
            Address = address;
            record.LeaseCount++;
            if (instancePin) record.InstancePins++;
            next = record.LeaseHead;
            if (next != null) next.Previous = this;
            record.LeaseHead = this;
        }

        public void Dispose()
        {
            if (record == null) return;
            record.Owner.CheckThread();
            var owned = record;
            if (previous == null) owned.LeaseHead = next;
            else previous.Next = next;
            if (next != null) next.Previous = previous;
            previous = null;
            next = null;
            record = null;
            owned.Owner.ReleaseLease(owned, instancePin);
        }
    }
}
