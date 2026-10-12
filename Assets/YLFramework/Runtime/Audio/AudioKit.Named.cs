using System;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    public static partial class AudioKit
    {
        private static int sClipCacheCapacity;
        private static double sClipCacheIdleSeconds;
        /// <summary>最大闲置 Clip 条目数，默认 0 禁缓存；不包含等待者、播放或 Source 池。</summary>
        public static int ClipCacheCapacity
        {
            get => sClipCacheCapacity;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (IsClosing) throw new InvalidOperationException("AudioKit is shutting down.");
                sClipCacheCapacity = value;
                if (AudioManager.TryGetInstance(out var host)) host.ClipCache.Tick(Time.realtimeSinceStartupAsDouble);
            }
        }
        /// <summary>闲置资源的 unscaled 存活秒数，默认 0 禁缓存。重新取得取消闲置到期；活跃责任不受影响。</summary>
        public static double ClipCacheIdleSeconds
        {
            get => sClipCacheIdleSeconds;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (IsClosing) throw new InvalidOperationException("AudioKit is shutting down.");
                sClipCacheIdleSeconds = value;
                if (AudioManager.TryGetInstance(out var host)) host.ClipCache.Tick(Time.realtimeSinceStartupAsDouble);
            }
        }
        /// <summary>只释放本 Audio 会话的闲置 Clip 缓存，不释放仍有等待者/播放/显式租约的资源。</summary>
        public static void ClearClipCache() { if (AudioManager.TryGetInstance(out var host)) host.ClipCache.ClearIdle(); }
        /// <summary>显式同步预加载/保活；调用方必须 Dispose 返回租约。不同后端不合并所有权。</summary>
        public static AudioClipLease AcquireClip(AudioClipAddress address)
        {
            address.Validate(); return AudioManager.Instance.ClipCache.Load(address);
        }
        internal static bool InvalidateNamed(AudioCategory category, AudioPlayEnd reason = AudioPlayEnd.Replaced)
        {
            if (!AudioManager.TryGetInstance(out var host)) return true;
            var version = host.PendingVersion(category, null);
            host.CancelPending(category, null, reason);
            return host && host.Available && host.PendingVersion(category, null) == version + 1;
        }
        private static AudioPlayRequest PlayNamed(AudioClipAddress address, AudioCategory category, AudioChannel channel,
            AudioSoundOptions options, Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool asynchronous, float crossfade = -1f)
        {
            address.Validate(); channel?.RequireValid();
            var snapshot = AudioPlaybackOptions.Capture(options, channel == null && category == AudioCategory.Music);
            if (category != AudioCategory.Sound) { snapshot.Priority = 0; snapshot.PlaySoundMode = null; }
            snapshot.Validate(false);
            var host = channel?.Owner ?? AudioManager.Instance;
            var request = new AudioPlayRequest(host, address, category, channel, snapshot, started, finished, asynchronous, crossfade);
            host.AddPending(request);
            request.Load(asynchronous);
            return request;
        }
        /// <summary>同步取得命名 Music 并提交到原双声部内核；省略 options 仍默认循环。资产加载同步不等于后台解码同步。</summary>
        public static AudioHandle PlayMusicNamed(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlayNamed(address, AudioCategory.Music, null, options, onStarted, onFinished, false).Handle;
        /// <summary>异步取得 Music，等待音频数据就绪后接纳；最新有效独占请求决定提交，失败不停止旧播放。</summary>
        public static AudioPlayRequest PlayMusicAsync(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlayNamed(address, AudioCategory.Music, null, options, onStarted, onFinished, true);
        /// <summary>同步取得 Voice，省略 options 不循环；资源由原请求全终态归还。</summary>
        public static AudioHandle PlayVoiceNamed(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlayNamed(address, AudioCategory.Voice, null, options, onStarted, onFinished, false).Handle;
        /// <summary>异步取得 Voice，加载中句柄无效；不物理取消共享 producer。</summary>
        public static AudioPlayRequest PlayVoiceAsync(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlayNamed(address, AudioCategory.Voice, null, options, onStarted, onFinished, true);
        /// <summary>同步取得 Sound，沿用 Sound 去重/容量/优先级接纳，不将 Clip 缓存混作 Source 预算。</summary>
        public static AudioHandle PlaySoundNamed(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onFinished = null, Action<AudioClip, AudioHandle> onStarted = null) =>
            PlayNamed(address, AudioCategory.Sound, null, options, onStarted, onFinished, false).Handle;
        /// <summary>异步 Sound 请求各自等待/取消，同完整 Provider key 合并在途取得，不合并播放生命周期。</summary>
        public static AudioPlayRequest PlaySoundAsync(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onFinished = null, Action<AudioClip, AudioHandle> onStarted = null) =>
            PlayNamed(address, AudioCategory.Sound, null, options, onStarted, onFinished, true);
        /// <summary>同步取得后使用原 Music crossfade；尾轨自己的后端租约保活到过渡终结。</summary>
        public static AudioHandle CrossFadeMusicNamed(AudioClipAddress address, float duration, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null)
        {
            ValidateNamedCrossfade(duration);
            return PlayNamed(address, AudioCategory.Music, null, options, onStarted, onFinished, false, duration).Handle;
        }
        /// <summary>异步取得后才开始原 Music crossfade；加载期间不截断旧曲或尾轨。</summary>
        public static AudioPlayRequest CrossFadeMusicAsync(AudioClipAddress address, float duration, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null)
        {
            ValidateNamedCrossfade(duration);
            return PlayNamed(address, AudioCategory.Music, null, options, onStarted, onFinished, true, duration);
        }
        private static void ValidateNamedCrossfade(float duration)
        {
            if (!AudioSoundOptions.IsFinite(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
        }
        internal static AudioPlayRequest PlayChannelNamed(AudioChannel channel, AudioClipAddress address, AudioSoundOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished, bool asynchronous) =>
            PlayNamed(address, channel.Category, channel, options, started, finished, asynchronous);
    }

    public sealed partial class AudioChannel
    {
        /// <summary>同步命名播放，复用本通道接纳/借用资源收尾。省略 options 为非循环 Defaults。</summary>
        public AudioHandle PlayNamed(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            AudioKit.PlayChannelNamed(this, address, options, onStarted, onFinished, false).Handle;
        /// <summary>异步命名播放；Exclusive 以最新有效请求提交，Overlap 保留各自等待者。</summary>
        public AudioPlayRequest PlayAsync(AudioClipAddress address, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            AudioKit.PlayChannelNamed(this, address, options, onStarted, onFinished, true);
    }
}
