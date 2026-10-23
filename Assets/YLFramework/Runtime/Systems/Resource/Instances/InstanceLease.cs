using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>一次 checkout 的独立身份。归还后不可再使用对象；旧 Lease 不能操作下一次 checkout。</summary>
    public sealed class InstanceLease : IDisposable
    {
        private InstanceCache.Entry _entry;
        private readonly InstanceCache _cache;
        internal InstanceLease(InstanceCache cache, InstanceCache.Entry entry)
        {
            this._cache = cache;
            this._entry = entry;
            entry.Checkout = this;
        }

        public bool IsDisposed => _entry == null;
        public bool IsValid => _entry != null && ReferenceEquals(_entry.Checkout, this) && _entry.Object && _cache.Owner.IsSessionActive;

        /// <summary>只在当前 checkout 有效时借读；不得自行归还到其他池或在 Return 后保留使用权。</summary>
        public GameObject GameObject
        {
            get
            {
                _cache.Owner.CheckThread();
                if (_entry == null)
                {
                    throw new ObjectDisposedException(nameof(InstanceLease));
                }

                if (!IsValid)
                {
                    throw new ResourceException(ResourceError.SessionUnavailable, "The instance checkout is no longer valid.");
                }

                return _entry.Object;
            }
        }

        /// <summary>结束本次 checkout；正常实例回原桶，已退休桶或超容量实例销毁。</summary>
        public void Return() => Release(false);
        /// <summary>结束本次 checkout 并销毁损坏/失败实例，绝不进入空闲缓存。</summary>
        public void Discard() => Release(true);
        public void Dispose() => Return();

        private void Release(bool discard)
        {
            _cache.Owner.CheckThread();
            var owned = _entry;
            if (owned == null)
            {
                return;
            }

            _entry = null;
            if (!ReferenceEquals(owned.Checkout, this))
            {
                return;
            }

            owned.Checkout = null;
            _cache.Return(owned, discard);
        }
    }
}
