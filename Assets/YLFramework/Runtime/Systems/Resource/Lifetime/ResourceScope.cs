using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YLFramework.Resource
{
    /// <summary>只聚合自己接管的请求与租约，不建立缓存或第二份资源加载权威。</summary>
    public sealed class ResourceScope : IDisposable
    {
        private readonly ResourceSystem _system;
        private readonly HashSet<IDisposable> _requests = new(ReferenceComparer.Instance);
        private readonly HashSet<IDisposable> _leases = new(ReferenceComparer.Instance);
        public bool IsDisposed
        {
            get; private set;
        }
        internal ResourceScope(ResourceSystem system) => this._system = system;

        private sealed class ReferenceComparer : IEqualityComparer<IDisposable>
        {
            internal static readonly ReferenceComparer Instance = new();
            public bool Equals(IDisposable left, IDisposable right) => ReferenceEquals(left, right);
            public int GetHashCode(IDisposable value) => RuntimeHelpers.GetHashCode(value);
        }

        public AssetLease<T> Acquire<T>(ResourceAddress address) where T : UnityEngine.Object
        {
            CheckAvailable();
            var lease = _system.Acquire<T>(address);
            if (IsDisposed)
            {
                lease.Dispose();
                throw new ObjectDisposedException(nameof(ResourceScope));
            }
            return Own(lease);
        }

        public ResourceRequest<AssetLease<T>> AcquireAsync<T>(ResourceAddress address) where T : UnityEngine.Object
        {
            CheckAvailable();
            var request = _system.AcquireAsync<T>(address);
            if (IsDisposed)
            {
                request.Dispose();
                throw new ObjectDisposedException(nameof(ResourceScope));
            }
            return Track(request);
        }

        /// <summary>显式接管一个等待/未领取结果。失败时不接管传入对象。</summary>
        public ResourceRequest<T> Track<T>(ResourceRequest<T> request)
        {
            CheckAvailable();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _requests.Add(request);
            return request;
        }

        /// <summary>调用方将自己拥有的 Lease 交给 Scope；不是新增 retain。</summary>
        public T Own<T>(T lease) where T : IDisposable
        {
            CheckAvailable();
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            _leases.Add(lease);
            return lease;
        }

        /// <summary>领取已登记请求的结果并立即纳入本域；随后关闭 Request 不释放已接管的结果。</summary>
        public T TakeResult<T>(ResourceRequest<T> request) where T : IDisposable
        {
            CheckAvailable();
            if (request == null || !_requests.Contains(request))
            {
                throw new ArgumentException("The request is not owned by this scope.", nameof(request));
            }

            var lease = request.TakeResult();
            try
            {
                _leases.Add(lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
            _requests.Remove(request);
            request.Dispose();
            return lease;
        }

        private void CheckAvailable()
        {
            _system.CheckThread();
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ResourceScope));
            }
        }

        public void Dispose()
        {
            _system.CheckThread();
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            var pending = new List<IDisposable>(_requests);
            var held = new List<IDisposable>(_leases);
            _requests.Clear();
            _leases.Clear();
            List<Exception> errors = null;
            foreach (var request in pending)
            {
                Release(request, ref errors);
            }

            foreach (var lease in held)
            {
                Release(lease, ref errors);
            }

            if (errors != null)
            {
                throw new AggregateException("Resource scope cleanup failed.", errors);
            }
        }

        private static void Release(IDisposable value, ref List<Exception> errors)
        {
            try
            {
                value.Dispose();
            }
            catch (Exception error)
            {
                (errors ??= new List<Exception>()).Add(error);
            }
        }
    }
}
