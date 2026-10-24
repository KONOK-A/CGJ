using System;
using System.Collections.Generic;
using System.Threading;

namespace YLFramework.Resource
{
    public enum ResourceGroupItemKind
    {
        Asset,
        SceneBundle,
        ScenePreview
    }

    /// <summary>SceneBundle成功只表示图已保活；ScenePreview只验证Editor路径，没有native Bundle或Scene租约。两者均不加载场景。</summary>
    public sealed class ResourceGroupItemResult
    {
        public string AssetId
        {
            get;
        }
        public ResourceAddress? Address
        {
            get;
        }
        public bool Required
        {
            get;
        }
        public ResourceGroupItemKind Kind
        {
            get;
        }
        public string BundleId
        {
            get;
        }
        public ResourceException Error
        {
            get;
        }
        public bool HasSucceeded => Error == null;

        internal ResourceGroupItemResult(string assetId, ResourceAddress? address, bool required,
            ResourceGroupItemKind kind, string bundleId, ResourceException error)
        {
            AssetId = assetId;
            Address = address;
            Required = required;
            Kind = kind;
            BundleId = bundleId;
            Error = error;
        }
    }

    /// <summary>必需成员失败时保留逐项结果，外层 ResourceException 仍保留标准资源域错误合同。</summary>
    public sealed class ResourceGroupFailure : AggregateException
    {
        public IReadOnlyList<ResourceGroupItemResult> Items
        {
            get;
        }

        internal ResourceGroupFailure(IEnumerable<ResourceGroupItemResult> items, IEnumerable<Exception> causes)
            : base("Required group preload member failed; batch ownership was released.", causes)
            => Items = ResourceCatalog.Copy(items);
    }

    /// <summary>同一 ResourceSystem 的正式 Asset/Bundle 租约聚合，不缓存对象、不拥有第二份资源状态。</summary>
    public sealed class ResourceGroupLease : IDisposable
    {
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
        public string ProjectId
        {
            get;
        }
        public string GroupId
        {
            get;
        }
        public IReadOnlyList<ResourceGroupItemResult> Items
        {
            get;
        }
        public IReadOnlyList<AssetLease<UnityEngine.Object>> Assets
        {
            get;
        }
        public IReadOnlyList<BundleLease> SceneBundles
        {
            get;
        }
        public bool IsDisposed
        {
            get; private set;
        }

        internal ResourceGroupLease(string projectId, string groupId, IEnumerable<ResourceGroupItemResult> items,
            IEnumerable<AssetLease<UnityEngine.Object>> assets, IEnumerable<BundleLease> sceneBundles)
        {
            ProjectId = projectId;
            GroupId = groupId;
            Items = ResourceCatalog.Copy(items);
            Assets = ResourceCatalog.Copy(assets);
            SceneBundles = ResourceCatalog.Copy(sceneBundles);
        }

        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != _threadId)
            {
                throw new InvalidOperationException("Resource groups must be released on their accepting Unity thread.");
            }

            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            List<Exception> errors = null;
            foreach (var lease in Assets)
            {
                try
                {
                    lease.Dispose();
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
            }

            foreach (var lease in SceneBundles)
            {
                try
                {
                    lease.Dispose();
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
            }

            if (errors != null)
            {
                throw new AggregateException("Group preload cleanup failed.", errors);
            }
        }
    }
}
