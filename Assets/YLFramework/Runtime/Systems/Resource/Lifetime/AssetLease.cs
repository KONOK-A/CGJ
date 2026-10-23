using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>一次取得对应一次独立责任；只释放自己，不池化公开身份，不使用 finalizer。</summary>
    public sealed class AssetLease<T> : IDisposable, IAssetLeaseDiagnostics where T : UnityEngine.Object
    {
        private ResourceSystem.AssetRecord _record;
        private readonly bool _instancePin;
        private string _ownerLabel;
        private IAssetLeaseDiagnostics _previous;
        private IAssetLeaseDiagnostics _next;
        internal ResourceSystem.AssetRecord Record => _record;
        public ResourceAddress Address
        {
            get;
        }
        public bool IsDisposed => _record == null;
        public bool IsValid => _record != null && _record.Owner.IsSessionActive && _record.Asset;

        /// <summary>调用方显式填写的真实责任说明；未填写时诊断保持未标注，不参与 retain/release。</summary>
        public string OwnerLabel
        {
            get => _ownerLabel;
            set
            {
                if (_record == null)
                {
                    throw new ObjectDisposedException(nameof(AssetLease<T>));
                }

                _record.Owner.CheckThread();
                _ownerLabel = value;
            }
        }

        IAssetLeaseDiagnostics IAssetLeaseDiagnostics.Previous
        {
            get => _previous; set => _previous = value;
        }
        IAssetLeaseDiagnostics IAssetLeaseDiagnostics.Next
        {
            get => _next; set => _next = value;
        }
        bool IAssetLeaseDiagnostics.IsInstancePin => _instancePin;

        /// <summary>借读对象，仅在本 Lease 有效期间使用；外部销毁和会话退出均明确拒绝。</summary>
        public T Asset
        {
            get
            {
                if (_record == null)
                {
                    throw new ObjectDisposedException(nameof(AssetLease<T>));
                }

                _record.Owner.CheckThread();
                if (!_record.Owner.IsSessionActive)
                {
                    throw new ResourceException(ResourceError.SessionUnavailable, "The asset lease belongs to an ended session.", address: Address);
                }

                if (!_record.Asset)
                {
                    throw new ResourceException(ResourceError.LoadFailed, "The leased Unity object was destroyed externally.", address: Address);
                }

                return (T)_record.Asset;
            }
        }

        internal AssetLease(ResourceSystem.AssetRecord record, ResourceAddress address, bool instancePin = false)
        {
            this._record = record;
            this._instancePin = instancePin;
            Address = address;
            record.LeaseCount++;
            if (instancePin)
            {
                record.InstancePins++;
            }

            _next = record.LeaseHead;
            if (_next != null)
            {
                _next.Previous = this;
            }

            record.LeaseHead = this;
        }

        public void Dispose()
        {
            if (_record == null)
            {
                return;
            }

            _record.Owner.CheckThread();
            var owned = _record;
            if (_previous == null)
            {
                owned.LeaseHead = _next;
            }
            else
            {
                _previous.Next = _next;
            }

            if (_next != null)
            {
                _next.Previous = _previous;
            }

            _previous = null;
            _next = null;
            _record = null;
            owned.Owner.ReleaseLease(owned, _instancePin);
        }
    }
}
