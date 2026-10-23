using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>Resources 为共享 borrowed 来源。释放逻辑记录不表示 native memory 已卸载。</summary>
    internal sealed class UnityResourcesSource : IResourceSource
    {
        public ResourceSourceId Id => ResourceSourceId.Resources;
        public bool SupportsSynchronousLoad => true;

        public void Validate(ResourceCatalogEntry entry)
        {
            var path = entry.Location.Locator;
            ResourceAddress.ValidatePath(path, nameof(entry.Location.Locator));
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || path.IndexOf(':') >= 0 ||
                path.LastIndexOf('.') > path.LastIndexOf('/'))
            {
                throw new ArgumentException($"Resources requires an extension-free path relative to Resources: '{path}'.");
            }

            if (entry.Address.SubAsset != null || entry.Dependencies.Count != 0)
            {
                throw new ArgumentException("Resources subasset selection and explicit dependency execution are not supported in 8.1.");
            }
        }

        public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type) => new(Resources.Load(entry.Location.Locator, type));
        public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type) => new Operation(entry.Location.Locator, type);

        // 外部借用者不可枚举，也不属于本系统；禁止单租约触发 UnloadAsset/Destroy/全局扫描。
        public void Release(UnityEngine.Object asset)
        {
        }

        private sealed class Operation : ResourceSourceOperation
        {
            private UnityEngine.ResourceRequest _native;
            public override float Progress => _native == null ? base.Progress : _native.progress;

            internal Operation(string path, Type type)
            {
                _native = Resources.LoadAsync(path, type);
                _native.completed += OnCompleted;
                if (_native.isDone)
                {
                    OnCompleted(_native);
                }
            }

            private void OnCompleted(AsyncOperation operation)
            {
                if (IsDone)
                {
                    return;
                }

                _native.completed -= OnCompleted;
                UnityEngine.Object result = null;
                Exception error = null;
                try
                {
                    result = _native.asset;
                }
                catch (Exception cause)
                {
                    error = cause;
                }
                Complete(result, error);
            }

            public override void Dispose()
            {
                base.Dispose();
                _native = null;
            }
        }
    }
}
