using System;

namespace YLFramework.Resource
{
    /// <summary>图片的有界输入与闲置策略。缓存预算是无 mip RGBA 像素估算，不是 Profiler native memory。</summary>
    public sealed class ResourceImageOptions
    {
        internal static readonly ResourceImageOptions Default = new();
        public int MaximumEncodedBytes
        {
            get;
        }
        public int MaximumPixels
        {
            get;
        }
        public long IdleCapacityBytes
        {
            get;
        }
        public int IdleCapacity
        {
            get;
        }
        public TimeSpan IdleTimeToLive
        {
            get;
        }
        /// <summary>本地/URL图片异步producer的并发上限；同图等待者不额外占槽。</summary>
        public int MaximumConcurrentLoads
        {
            get;
        }

        public ResourceImageOptions(int maximumEncodedBytes = 32 * 1024 * 1024, int maximumPixels = 16 * 1024 * 1024,
            long idleCapacityBytes = 32 * 1024 * 1024, TimeSpan? idleTimeToLive = null, int idleCapacity = 128, int maximumConcurrentLoads = 4)
        {
            var ttl = idleTimeToLive ?? TimeSpan.FromSeconds(30);
            if (maximumEncodedBytes < 24 || maximumEncodedBytes > 128 * 1024 * 1024)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEncodedBytes));
            }

            if (maximumPixels <= 0 || maximumPixels > 64 * 1024 * 1024)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumPixels));
            }

            if (idleCapacityBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(idleCapacityBytes));
            }

            if (idleCapacity < 0 || idleCapacity > 16384)
            {
                throw new ArgumentOutOfRangeException(nameof(idleCapacity));
            }

            if (ttl < TimeSpan.Zero || ttl > TimeSpan.FromDays(1))
            {
                throw new ArgumentOutOfRangeException(nameof(idleTimeToLive));
            }

            if (maximumConcurrentLoads < 1 || maximumConcurrentLoads > 32)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumConcurrentLoads));
            }

            MaximumEncodedBytes = maximumEncodedBytes;
            MaximumPixels = maximumPixels;
            IdleCapacityBytes = idleCapacityBytes;
            IdleCapacity = idleCapacity;
            IdleTimeToLive = ttl;
            MaximumConcurrentLoads = maximumConcurrentLoads;
        }
    }
}
