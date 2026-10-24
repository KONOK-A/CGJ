using System;
using System.Linq;
using UnityEngine;

namespace YLFramework.Resource
{
    internal sealed class AssetBundleSource : IResourceSource
    {
        private readonly ResourceSystem _system;
        public ResourceSourceId Id => ResourceSourceId.AssetBundle;
        public bool SupportsSynchronousLoad => true;
        internal AssetBundleSource(ResourceSystem system) => this._system = system;
        public void Validate(ResourceCatalogEntry entry)
        {
            if (entry.Snapshot == null || entry.Descriptor == null || entry.Descriptor.Kind == "Scene" || entry.Dependencies.Count != 0)
            {
                throw new ArgumentException("AssetBundle sources require an explicit verified Catalog snapshot descriptor.");
            }
        }
        public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type)
        {
            using var request = _system.AcquireBundleRequest(entry.Address.ProjectId, entry.Descriptor.BundleId, true, false, new BundleLeaseOrigin(entry.Address));
            var lease = request.TakeResult();
            try
            {
                var result = entry.Descriptor.Kind == "SubAsset" ? Select(entry, type, lease.Bundle.LoadAssetWithSubAssets(entry.Descriptor.LoadName, type)) :
                    lease.Bundle.LoadAsset(entry.Descriptor.LoadName, type);
                _system.RequireHealthyBundle(lease);
                return new ResourceSourceResult(result, lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
        public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type) => new Operation(_system, entry, type);
        public void Release(UnityEngine.Object asset)
        {
        } // Bundle graph retention 才是物理资产释放权威。

        private static UnityEngine.Object Select(ResourceCatalogEntry entry, Type type, UnityEngine.Object[] assets)
        {
            var matches = assets.Where(x => x && type.IsInstanceOfType(x) && x.name == entry.Descriptor.SubAssetName).ToArray();
            if (matches.Length != 1)
            {
                throw new ResourceException(ResourceError.LoadFailed,
                $"Subasset '{entry.Descriptor.SubAssetName}'/{type.FullName} has {matches.Length} native matches.", address: entry.Address, source: ResourceSourceId.AssetBundle);
            }

            return matches[0];
        }

        private sealed class Operation : ResourceSourceOperation
        {
            private readonly ResourceCatalogEntry _entry;
            private readonly Type _type;
            private ResourceRequest<BundleLease> _bundleRequest;
            private BundleLease _retention;
            private AssetBundleRequest _native;
            public override bool SupportsCancellation => _native == null && !IsDone;
            public override float Progress => _native != null ? 0.5f + _native.progress * 0.5f : (_bundleRequest?.Progress ?? 0) * 0.5f;
            internal Operation(ResourceSystem system, ResourceCatalogEntry entry, Type type)
            {
                this._entry = entry;
                this._type = type;
                _bundleRequest = system.AcquireBundleRequest(entry.Address.ProjectId, entry.Descriptor.BundleId, false, false, new BundleLeaseOrigin(entry.Address));
                _bundleRequest.Completed += OnBundle;
            }
            private void OnBundle(ResourceRequest<BundleLease> request)
            {
                if (IsDone)
                {
                    return;
                }

                Exception failure = null;
                try
                {
                    _retention = request.TakeResult();
                    _retention.Record.Owner.RequireHealthyBundle(_retention);
                    _native = _entry.Descriptor.Kind == "SubAsset" ? _retention.Bundle.LoadAssetWithSubAssetsAsync(_entry.Descriptor.LoadName, _type) :
                        _retention.Bundle.LoadAssetAsync(_entry.Descriptor.LoadName, _type);
                    if (_native == null)
                    {
                        throw new InvalidOperationException("Unity did not return an AssetBundleRequest.");
                    }

                    _native.completed += OnAsset;
                    if (_native.isDone)
                    {
                        OnAsset(_native);
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                }
                finally
                {
                    request.Dispose();
                    _bundleRequest = null;
                }
                if (failure != null)
                {
                    Finish(null, failure);
                }
            }
            private void OnAsset(AsyncOperation operation)
            {
                if (IsDone)
                {
                    return;
                }

                _native.completed -= OnAsset;
                UnityEngine.Object asset = null;
                Exception error = null;
                try
                {
                    asset = _entry.Descriptor.Kind == "SubAsset" ? Select(_entry, _type, _native.allAssets) : _native.asset;
                    _retention.Record.Owner.RequireHealthyBundle(_retention);
                }
                catch (Exception cause)
                {
                    error = cause;
                }
                Finish(asset, error);
            }
            private void Finish(UnityEngine.Object asset, Exception error)
            {
                var owned = _retention;
                _retention = null;
                Complete(asset, error, owned);
            }
            public override void Cancel()
            {
                // 已启动的 AssetBundleRequest 无法撤销；保活必须持续到 OnAsset。
                if (_native != null || IsDone)
                {
                    return;
                }

                _bundleRequest?.Cancel();
            }
            public override void Dispose()
            {
                base.Dispose();
                _native = null;
            }
        }
    }
}
