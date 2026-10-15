using System;
using System.Linq;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    internal sealed class AssetBundleSource : IResourceSource
    {
        private readonly ResourceSystem system;
        public ResourceSourceId Id => ResourceSourceId.AssetBundle;
        public bool SupportsSynchronousLoad => true;
        internal AssetBundleSource(ResourceSystem system) => this.system = system;
        public void Validate(ResourceCatalogEntry entry)
        {
            if (entry.Snapshot == null || entry.Descriptor == null || entry.Descriptor.Kind == "Scene" || entry.Dependencies.Count != 0)
                throw new ArgumentException("AssetBundle sources require an explicit verified Catalog snapshot descriptor.");
        }
        public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type)
        {
            using var request = system.AcquireBundleRequest(entry.Address.ProjectId, entry.Descriptor.BundleId, true, false);
            var lease = request.TakeResult();
            try
            {
                var result = entry.Descriptor.Kind == "SubAsset" ? Select(entry, type, lease.Bundle.LoadAssetWithSubAssets(entry.Descriptor.LoadName, type)) :
                    lease.Bundle.LoadAsset(entry.Descriptor.LoadName, type);
                return new ResourceSourceResult(result, lease);
            }
            catch { lease.Dispose(); throw; }
        }
        public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type) => new Operation(system, entry, type);
        public void Release(UnityEngine.Object asset) { } // Bundle graph retention 才是物理资产释放权威。

        private static UnityEngine.Object Select(ResourceCatalogEntry entry, Type type, UnityEngine.Object[] assets)
        {
            var matches = assets.Where(x => x && type.IsInstanceOfType(x) && x.name == entry.Descriptor.SubAssetName).ToArray();
            if (matches.Length != 1) throw new ResourceException(ResourceError.LoadFailed,
                $"Subasset '{entry.Descriptor.SubAssetName}'/{type.FullName} has {matches.Length} native matches.", address: entry.Address, source: ResourceSourceId.AssetBundle);
            return matches[0];
        }

        private sealed class Operation : ResourceSourceOperation
        {
            private readonly ResourceCatalogEntry entry;
            private readonly Type type;
            private ResourceRequest<BundleLease> bundleRequest;
            private BundleLease retention;
            private AssetBundleRequest native;
            public override bool SupportsCancellation => native == null && !IsDone;
            public override float Progress => native != null ? 0.5f + native.progress * 0.5f : (bundleRequest?.Progress ?? 0) * 0.5f;
            internal Operation(ResourceSystem system, ResourceCatalogEntry entry, Type type)
            {
                this.entry = entry;
                this.type = type;
                bundleRequest = system.AcquireBundleRequest(entry.Address.ProjectId, entry.Descriptor.BundleId, false, false);
                bundleRequest.Completed += OnBundle;
            }
            private void OnBundle(ResourceRequest<BundleLease> request)
            {
                if (IsDone) return;
                Exception failure = null;
                try
                {
                    retention = request.TakeResult();
                    native = entry.Descriptor.Kind == "SubAsset" ? retention.Bundle.LoadAssetWithSubAssetsAsync(entry.Descriptor.LoadName, type) :
                        retention.Bundle.LoadAssetAsync(entry.Descriptor.LoadName, type);
                    if (native == null) throw new InvalidOperationException("Unity did not return an AssetBundleRequest.");
                    native.completed += OnAsset;
                    if (native.isDone) OnAsset(native);
                }
                catch (Exception error) { failure = error; }
                finally { request.Dispose(); bundleRequest = null; }
                if (failure != null) Finish(null, failure);
            }
            private void OnAsset(AsyncOperation operation)
            {
                if (IsDone) return;
                native.completed -= OnAsset;
                UnityEngine.Object asset = null;
                Exception error = null;
                try { asset = entry.Descriptor.Kind == "SubAsset" ? Select(entry, type, native.allAssets) : native.asset; }
                catch (Exception cause) { error = cause; }
                Finish(asset, error);
            }
            private void Finish(UnityEngine.Object asset, Exception error)
            {
                var owned = retention;
                retention = null;
                Complete(asset, error, owned);
            }
            public override void Cancel()
            {
                // 已启动的 AssetBundleRequest 无法撤销；保活必须持续到 OnAsset。
                if (native != null || IsDone) return;
                bundleRequest?.Cancel();
            }
            public override void Dispose() { base.Dispose(); native = null; }
        }
    }
}
