using System;
using UnityEngine;
using XFABManager;
using YLFramework.Services.Audio;

namespace YLFramework.Integrations.XFAB.Audio
{
    /// <summary>显式可选 XFAB Audio 接入。ProjectName 是 XFAB 项目，不是 YL Bundle 名；key 为项目内 AudioClip 资产名。</summary>
    /// <remarks>仅借用共享缓存，必须由外部资源 owner 保活。Audio 本地归零/取消/Shutdown 不调用 UnloadAsset、Abort 或删除共享缓存。
    /// 不自动注册，不随默认框架导出；主线程使用。底层永久 pending 时仅能结束 Audio 等待者，不能承诺 producer 物理终止。</remarks>
    public sealed class XfabAudioClipProvider : IAudioClipProvider
    {
        private sealed class Borrow : IDisposable
        {
            // 没有独立 XFAB 租约，释放只结束本地观察，物理资产责任仍属于外部 owner。
            public void Dispose() { }
        }
        /// <summary>明确的 XFAB 项目身份。</summary>
        public string ProjectName { get; }
        /// <summary>无全局租约，不提供拥有型闲置缓存。</summary>
        public bool CanCache => false;
        public XfabAudioClipProvider(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName)) throw new ArgumentException("An explicit XFAB project is required.", nameof(projectName));
            ProjectName = projectName;
        }
        /// <inheritdoc />
        public AudioClipLease Load(string key)
        {
            ValidateKey(key);
            var clip = AssetBundleManager.LoadAsset<AudioClip>(ProjectName, key);
            if (!clip) throw new InvalidOperationException("XFAB did not load AudioClip: " + ProjectName + "/" + key);
            return new AudioClipLease(clip, new Borrow());
        }
        /// <inheritdoc />
        public void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress)
        {
            ValidateKey(key);
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            var operation = AssetBundleManager.LoadAssetAsync<AudioClip>(ProjectName, key);
            if (operation == null) { completed(null, new InvalidOperationException("XFAB asynchronous loading is unavailable outside Play Mode.")); return; }
            progress?.Invoke(operation.progress);
            Action<LoadAssetRequest> observe = null;
            observe = result =>
            {
                // 仅摘自己的 observer，不 Abort/RemoveAllCompleteEvent 干扰其他共享等待者。
                operation.RemoveCompleteEvent(observe);
                var clip = result.asset as AudioClip;
                if (!string.IsNullOrEmpty(result.error) || !clip)
                { completed(null, new InvalidOperationException(result.error ?? "XFAB returned no AudioClip.")); return; }
                progress?.Invoke(1);
                completed(new AudioClipLease(clip, new Borrow()), null);
            };
            operation.AddCompleteEvent(observe);
        }
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An XFAB AudioClip asset name is required.", nameof(key));
        }
    }
}
