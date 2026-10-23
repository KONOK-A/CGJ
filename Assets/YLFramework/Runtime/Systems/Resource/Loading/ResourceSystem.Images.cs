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
        private readonly ResourceCatalog _imageCatalog = new("runtime-images", Array.Empty<ResourceCatalogEntry>());
        private readonly LinkedList<AssetRecord> _idleImages = new();
        private long _idleImageBytes;
        private readonly LinkedList<AssetRecord> _imageLoadQueue = new();
        private int _runningImageLoads;
        private bool _isPumpingImageLoads;

        private void PumpImageLoads()
        {
            if (_isPumpingImageLoads)
            {
                return;
            }

            _isPumpingImageLoads = true;
            try
            {
                while (IsAvailable && _imageLoadQueue.First != null && _runningImageLoads < _configuration.Images.MaximumConcurrentLoads)
                {
                    var record = _imageLoadQueue.First.Value;
                    _imageLoadQueue.RemoveFirst();
                    record.ImageQueueNode = null;
                    record.IsImageLoadRunning = true;
                    _runningImageLoads++;
                    try
                    {
                        StartSourceOperation(record);
                    }
                    catch (Exception error)
                    {
                        ReleaseImageLoadSlot(record);
                        if (record.IsLoading)
                        {
                            CompleteRecord(record, default, error);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
            finally
            {
                _isPumpingImageLoads = false;
            }
        }

        private void ReleaseImageLoadSlot(AssetRecord record)
        {
            if (!record.IsImageLoadRunning)
            {
                return;
            }

            record.IsImageLoadRunning = false;
            _runningImageLoads--;
        }

        /// <summary>按完整 HTTP(S) URL 或绝对本地路径取得图片。每人独立请求/租约，同一原始定位共享生产。</summary>
        /// <remarks>刷新远端内容请使用显式版本 URL；不会偷偷改写大小写或增加 query。闲置缓存不拥有活跃消费者。</remarks>
        public ResourceRequest<AssetLease<Texture2D>> AcquireImageAsync(string location) => AcquireImageRequest(location, false);

        /// <summary>仅本地冷取得或已 Ready 的远端图片可同步取得；在途请求返回 InProgress。</summary>
        public AssetLease<Texture2D> AcquireImage(string location)
        {
            using var request = AcquireImageRequest(location, true);
            if (request.Status != ResourceRequestStatus.Succeeded)
            {
                throw request.Error;
            }

            return request.TakeResult();
        }

        private ResourceRequest<AssetLease<Texture2D>> AcquireImageRequest(string location, bool synchronous)
        {
            CheckThread();
            var request = new ResourceRequest<AssetLease<Texture2D>>(lease => lease.Dispose());
            if (!IsAvailable)
            {
                return Fail(request, ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            }

            ResourceCatalogEntry entry;
            try
            {
                if (string.IsNullOrWhiteSpace(location))
                {
                    throw new ArgumentException("An exact URL or fully qualified image file is required.");
                }

                var source = Path.IsPathFullyQualified(location) ? ResourceSourceId.LocalTexture : ResourceSourceId.UrlTexture;
                var id = ResourceCatalogCodec.Digest(Encoding.UTF8.GetBytes(source + "\n" + location));
                var address = new ResourceAddress(_imageCatalog.ProjectId, id);
                entry = new ResourceCatalogEntry(id, address, new ResourceLocation(source, location, typeof(Texture2D)));
                _sources[source].Validate(entry);
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is NotSupportedException)
            {
                return Fail(request, ResourceError.InvalidAddress, "Invalid image location.", cause: error);
            }
            TrimImageCache();
            return AcquireEntry<Texture2D>(_imageCatalog, entry, entry.Address, synchronous, request, true);
        }

        /// <summary>只释放闲置图片。活跃 Lease、其他来源、外部 Texture 均不受影响。</summary>
        public void ClearImageCache()
        {
            CheckThread();
            while (_idleImages.First != null)
            {
                ReleaseRecord(_idleImages.First.Value);
            }

            CheckDrain();
        }

        private bool TryCacheImage(AssetRecord record)
        {
            var options = _configuration.Images;
            if (!record.IsImageCacheEligible || !IsAvailable || !record.Asset || options.IdleCapacityBytes == 0 || options.IdleCapacity == 0 || options.IdleTimeToLive == TimeSpan.Zero)
            {
                return false;
            }

            var texture = (Texture2D)record.Asset;
            record.ImageBytes = (long)texture.width * texture.height * 4;
            if (record.ImageBytes > options.IdleCapacityBytes)
            {
                return false;
            }

            record.IdleSince = Time.realtimeSinceStartupAsDouble;
            record.ImageCacheNode = _idleImages.AddLast(record);
            _idleImageBytes += record.ImageBytes;
            InstallInstanceCleanup();
            TrimImageCache();
            return true;
        }

        private void RemoveIdleImage(AssetRecord record)
        {
            if (record.ImageCacheNode == null)
            {
                return;
            }

            _idleImages.Remove(record.ImageCacheNode);
            record.ImageCacheNode = null;
            _idleImageBytes -= record.ImageBytes;
        }

        private void TrimImageCache()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            while (_idleImages.First != null)
            {
                var record = _idleImages.First.Value;
                if (record.Asset && _idleImages.Count <= _configuration.Images.IdleCapacity && _idleImageBytes <= _configuration.Images.IdleCapacityBytes &&
                    now - record.IdleSince < _configuration.Images.IdleTimeToLive.TotalSeconds)
                {
                    break;
                }

                ReleaseRecord(record);
            }
        }
    }
}
