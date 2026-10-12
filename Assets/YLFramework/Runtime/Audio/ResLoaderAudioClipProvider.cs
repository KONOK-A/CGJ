using System;
using System.Collections;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Services.Audio
{
    /// <summary>以 YL ResLoader 持有 AudioClip 及其依赖的窄后端；物理释放完全交给 Resource。</summary>
    /// <remarks>key 使用 ResLoader 完整定位（resources://路径或资源名）；AB 必须显式指定 ownerBundleName，不能当作 XFAB ProjectName。
    /// 不注册全局后端，不关闭 Resource 服务。异步复用其主线程加载，取消等待者不回收仍在生产的 Loader。</remarks>
    public sealed class ResLoaderAudioClipProvider : IAudioClipProvider
    {
        /// <summary>Resources 专用实例；同实例与完整 key 可以合并在途加载。</summary>
        public static ResLoaderAudioClipProvider Resources { get; } = new ResLoaderAudioClipProvider();
        /// <summary>显式 AB 所属 Bundle；null 仅允许 Resources 定位。</summary>
        public string OwnerBundleName { get; }
        /// <summary>Loader 确实持有资产与依赖，允许 Audio 闲置缓存。</summary>
        public bool CanCache => true;
        private sealed class LoaderOwner : IDisposable
        {
            private ResLoader mLoader;
            internal LoaderOwner(ResLoader loader) { mLoader = loader; }
            public void Dispose()
            {
                var loader = mLoader; mLoader = null;
                loader?.RecycleToCache();
            }
        }
        public ResLoaderAudioClipProvider(string ownerBundleName = null)
        {
            if (ownerBundleName != null && string.IsNullOrWhiteSpace(ownerBundleName)) throw new ArgumentException("A complete Bundle identity is required.", nameof(ownerBundleName));
            OwnerBundleName = ownerBundleName;
        }
        private ResKey CreateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A complete resource key is required.", nameof(key));
            var resources = key.StartsWith("resources/", StringComparison.OrdinalIgnoreCase) || key.StartsWith("resources://", StringComparison.OrdinalIgnoreCase);
            if (resources && OwnerBundleName != null || !resources && OwnerBundleName == null)
                throw new ArgumentException("Use a Resources address without a Bundle, or an asset address with an explicit owner Bundle.", nameof(key));
            return ResKey.Allocate(key, OwnerBundleName, typeof(AudioClip));
        }
        /// <inheritdoc />
        public AudioClipLease Load(string key)
        {
            var resKey = CreateKey(key);
            var loader = ResLoader.Allocate();
            try
            {
                var clip = loader.LoadAssetSync(resKey) as AudioClip;
                if (!clip) throw new InvalidOperationException("ResLoader did not load AudioClip: " + key);
                return new AudioClipLease(clip, new LoaderOwner(loader));
            }
            catch { loader.RecycleToCache(); throw; }
            finally { resKey.RecycleToCache(); }
        }
        /// <inheritdoc />
        public void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            var resKey = CreateKey(key);
            var loader = ResLoader.Allocate();
            IRes res;
            var done = false;
            try
            {
                // 不使用 AddToPreLoad 的逐项同步通知：Ready 通知可能早于该 Loader 的 Retain。
                loader.AddToPreLoad(resKey);
                res = ServiceManager.Instance.GetRes(resKey, false);
                if (res == null) throw new InvalidOperationException("ResLoader has no matching resource: " + key);
                loader.LoadAsync(() => done = true);
            }
            catch (Exception exception)
            {
                loader.RecycleToCache(); completed(null, exception); return;
            }
            finally { resKey.RecycleToCache(); }
            // 总回调只设置完成标记；下一次协程推进才交付，避免 Loader 在自己的通知栈中回收后被重入复用。
            ServiceManager.Instance.StartCoroutine(Deliver(loader, res, () => done, completed, progress, key));
        }
        private static IEnumerator Deliver(ResLoader loader, IRes res, Func<bool> done,
            Action<AudioClipLease, Exception> completed, Action<float> progress, string key)
        {
            var handedOff = false;
            try
            {
                while (!done())
                {
                    progress?.Invoke(res.LoadProgress);
                    yield return null;
                }
                var clip = res.State == ResState.Ready ? res.Asset as AudioClip : null;
                if (!clip) { completed(null, new InvalidOperationException("ResLoader failed to load AudioClip: " + key)); yield break; }
                var lease = new AudioClipLease(clip, new LoaderOwner(loader));
                handedOff = true;
                try { completed(lease, null); }
                catch { lease.ReleaseSafely(); throw; }
            }
            finally { if (!handedOff) loader.RecycleToCache(); }
        }
    }
}
