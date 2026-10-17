using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        // 仅作为直接图片请求的会话身份；不注册、修改或发布 Catalog v1。
        private readonly ResourceCatalog imageCatalog = new("runtime-images", Array.Empty<ResourceCatalogEntry>());
        private readonly LinkedList<AssetRecord> idleImages = new();
        private long idleImageBytes;
        private readonly LinkedList<AssetRecord> imageLoadQueue = new();
        private int runningImageLoads;
        private bool pumpingImageLoads;

        private void PumpImageLoads()
        {
            if (pumpingImageLoads) return;
            pumpingImageLoads = true;
            try
            {
                while (IsAvailable && imageLoadQueue.First != null && runningImageLoads < configuration.Images.MaximumConcurrentLoads)
                {
                    var record = imageLoadQueue.First.Value;
                    imageLoadQueue.RemoveFirst();
                    record.ImageQueueNode = null;
                    record.ImageLoadRunning = true;
                    runningImageLoads++;
                    try { StartSourceOperation(record); }
                    catch (Exception error)
                    {
                        ReleaseImageLoadSlot(record);
                        if (record.Loading) CompleteRecord(record, default, error);
                        else throw;
                    }
                }
            }
            finally { pumpingImageLoads = false; }
        }

        private void ReleaseImageLoadSlot(AssetRecord record)
        {
            if (!record.ImageLoadRunning) return;
            record.ImageLoadRunning = false;
            runningImageLoads--;
        }

        /// <summary>按完整 HTTP(S) URL 或绝对本地路径取得图片。每人独立请求/租约，同一原始定位共享生产。</summary>
        /// <remarks>刷新远端内容请使用显式版本 URL；不会偷偷改写大小写或增加 query。闲置缓存不拥有活跃消费者。</remarks>
        public ResourceRequest<AssetLease<Texture2D>> AcquireImageAsync(string location) => AcquireImageRequest(location, false);

        /// <summary>仅本地冷取得或已 Ready 的远端图片可同步取得；在途请求返回 InProgress。</summary>
        public AssetLease<Texture2D> AcquireImage(string location)
        {
            using var request = AcquireImageRequest(location, true);
            if (request.Status != ResourceRequestStatus.Succeeded) throw request.Error;
            return request.TakeResult();
        }

        private ResourceRequest<AssetLease<Texture2D>> AcquireImageRequest(string location, bool synchronous)
        {
            CheckThread();
            var request = new ResourceRequest<AssetLease<Texture2D>>(lease => lease.Dispose());
            if (!IsAvailable) return Fail(request, ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            ResourceCatalogEntry entry;
            try
            {
                if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("An exact URL or fully qualified image file is required.");
                var source = Path.IsPathFullyQualified(location) ? ResourceSourceId.LocalTexture : ResourceSourceId.UrlTexture;
                var id = ResourceCatalogCodec.Digest(Encoding.UTF8.GetBytes(source + "\n" + location));
                var address = new ResourceAddress(imageCatalog.ProjectId, id);
                entry = new ResourceCatalogEntry(id, address, new ResourceLocation(source, location, typeof(Texture2D)));
                sources[source].Validate(entry);
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is NotSupportedException)
            { return Fail(request, ResourceError.InvalidAddress, "Invalid image location.", cause: error); }
            TrimImageCache();
            return AcquireEntry<Texture2D>(imageCatalog, entry, entry.Address, synchronous, request, true);
        }

        /// <summary>只释放闲置图片。活跃 Lease、其他来源、外部 Texture 均不受影响。</summary>
        public void ClearImageCache()
        {
            CheckThread();
            while (idleImages.First != null) ReleaseRecord(idleImages.First.Value);
            CheckDrain();
        }

        private bool TryCacheImage(AssetRecord record)
        {
            var options = configuration.Images;
            if (!record.ImageCacheEligible || !IsAvailable || !record.Asset || options.IdleCapacityBytes == 0 || options.IdleCapacity == 0 || options.IdleTimeToLive == TimeSpan.Zero)
                return false;
            var texture = (Texture2D)record.Asset;
            record.ImageBytes = (long)texture.width * texture.height * 4;
            if (record.ImageBytes > options.IdleCapacityBytes) return false;
            record.IdleSince = Time.realtimeSinceStartupAsDouble;
            record.ImageCacheNode = idleImages.AddLast(record);
            idleImageBytes += record.ImageBytes;
            InstallInstanceCleanup();
            TrimImageCache();
            return true;
        }

        private void RemoveIdleImage(AssetRecord record)
        {
            if (record.ImageCacheNode == null) return;
            idleImages.Remove(record.ImageCacheNode);
            record.ImageCacheNode = null;
            idleImageBytes -= record.ImageBytes;
        }

        private void TrimImageCache()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            while (idleImages.First != null)
            {
                var record = idleImages.First.Value;
                if (record.Asset && idleImages.Count <= configuration.Images.IdleCapacity && idleImageBytes <= configuration.Images.IdleCapacityBytes &&
                    now - record.IdleSince < configuration.Images.IdleTimeToLive.TotalSeconds) break;
                ReleaseRecord(record);
            }
        }
    }
}
