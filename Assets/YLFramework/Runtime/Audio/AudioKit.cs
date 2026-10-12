using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>Clip-only music, voice and pooled sound service. The caller owns every supplied clip.</summary>
    /// <remarks>Playback callbacks receive the actual clip and original lease. Start callbacks may run before Play returns.
    /// Natural completion callbacks run before lease cleanup; Stop, replacement and Shutdown can invalidate it within the callback.
    /// Stop, rejection, replacement, disabling and target loss do not invoke natural completion callbacks.</remarks>
    public static class AudioKit
    {
        private static AudioKitSettings sSettings;
        private static SoundDeduplicator sChannels;
        private static AudioSoundConcurrency sSoundConcurrency;
        private static int sSoundCacheCapacity = 10;
        private static AudioSoundCounters sSoundCounters;
        private static ulong sSoundSession;
        private static Dictionary<int, ClipSoundRule> sClipSoundLimits;
        private static List<int> sExpiredClipRules;
        private readonly struct ClipSoundRule
        {
            internal readonly AudioClip Clip;
            internal readonly int Limit;
            internal ClipSoundRule(AudioClip clip, int limit) { Clip = clip; Limit = limit; }
        }
        private static bool sShuttingDown, sQuitting;
        internal static bool IsClosing => sShuttingDown || sQuitting ||
            Application.isPlaying && !YLFramework.Core.Singleton.SingletonSessionState.CanRunExecution;
        internal static AudioKitSettings CurrentSettings => sSettings;
        internal static SoundDeduplicator Channels { get { Initialize(); return sChannels; } }
        /// <summary>持久化的分类通道设置。首次访问会初始化，但不会创建播放宿主。</summary>
        public static AudioKitSettings Settings { get { Initialize(); return sSettings; } }
        /// <summary>最新 Music 请求的句柄；没有请求时返回无效句柄。读取不会创建宿主。</summary>
        public static AudioHandle Music => AudioManager.TryGetInstance(out var host) ? host.Music?.Handle ?? default : default;
        /// <summary>最新 Voice 请求的句柄；没有请求时返回无效句柄。读取不会创建宿主。</summary>
        public static AudioHandle Voice => AudioManager.TryGetInstance(out var host) ? host.Voice?.Handle ?? default : default;
        /// <summary>登记独立命名通道。名称与显式持久化身份冲突均拒绝；省略 persistenceId 不读写通道偏好键。</summary>
        /// <remarks>区分大小写，身份限 ASCII 字母/数字及 . _ - /。登记创建同一 Audio 宿主但不播放；Dispose/Shutdown 后身份失效。</remarks>
        public static AudioChannel RegisterChannel(string name, AudioCategory category = AudioCategory.Music,
            AudioChannelMode mode = AudioChannelMode.Exclusive, string persistenceId = null)
        {
            AudioChannel.ValidateIdentity(name, nameof(name));
            if (persistenceId != null) AudioChannel.ValidateIdentity(persistenceId, nameof(persistenceId));
            if ((uint)category > (uint)AudioCategory.Sound) throw new ArgumentOutOfRangeException(nameof(category));
            if ((uint)mode > (uint)AudioChannelMode.Overlap) throw new ArgumentOutOfRangeException(nameof(mode));
            return AudioManager.Instance.RegisterChannel(name, category, mode, persistenceId);
        }
        /// <summary>当前有效的 Sound 请求数，包含暂停、静音和循环中的请求。读取不会创建宿主。</summary>
        public static int SoundCount => AudioManager.TryGetInstance(out var host) ? host.SoundCount : 0;
        internal static AudioSoundConcurrency CurrentSoundConcurrency => sSoundConcurrency;
        internal static ref AudioSoundCounters SoundCounters => ref sSoundCounters;
        internal static ulong SoundSession => sSoundSession;
        /// <summary>Independent idle Sound cache budget. Defaults to 10; zero disables caching. Does not initialize settings or a host.</summary>
        /// <remarks>Changing this value never prewarms or stops active requests. Excess idle sources are released immediately.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">新容量为负数。</exception>
        /// <exception cref="InvalidOperationException">AudioKit 正在关闭。</exception>
        public static int SoundCacheCapacity
        {
            get => sSoundCacheCapacity;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                if (IsClosing) throw new InvalidOperationException("AudioKit is shutting down.");
                sSoundCacheCapacity = value;
                if (AudioManager.TryGetInstance(out var host)) host.ResizeSoundCache(value);
            }
        }
        /// <summary>Prepare a target number of distinct idle Sound AudioSources without playback, callbacks, deduplication or active quota.</summary>
        /// <param name="cachedCount">Target idle count, between zero and SoundCacheCapacity. Zero does not create a host.</param>
        /// <returns>The number of additional cached voices, not the target or an additional-count request.</returns>
        /// <remarks>Explicit positive prewarming may create the host. Active plus idle sources may exceed active Sound limits; no listener is created.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">cachedCount 为负数或大于 SoundCacheCapacity。</exception>
        public static int PrewarmSounds(int cachedCount)
        {
            if (cachedCount < 0 || cachedCount > sSoundCacheCapacity) throw new ArgumentOutOfRangeException(nameof(cachedCount));
            return cachedCount == 0 ? 0 : AudioManager.Instance.PrewarmSounds(cachedCount);
        }
        /// <summary>Read a lightweight Sound session snapshot without initializing settings, a host or collections.</summary>
        public static AudioSoundStats SoundStats => new AudioSoundStats(sSoundCounters, SoundCount,
            AudioManager.TryGetInstance(out var host) ? host.CachedSoundCount : 0);
        /// <summary>Clear cumulative Sound statistics and reset the peak to current active count. Does not change playback, rules, deduplication or resource gauges.</summary>
        public static void ResetSoundStats()
        {
            var owned = sSoundCounters.OwnedSourceCount;
            sSoundCounters = new AudioSoundCounters { PeakActiveCount = SoundCount, OwnedSourceCount = owned };
        }
        /// <summary>Runtime Sound request capacity. Zero is unlimited; paused, muted and looping requests count. Reducing the limit immediately stops excess requests.</summary>
        /// <exception cref="ArgumentOutOfRangeException">某字段为负数，或 OverflowPolicy 不在已定义取值内。</exception>
        public static AudioSoundConcurrency SoundConcurrency
        {
            get { Initialize(); return sSoundConcurrency; }
            set
            {
                value.Validate();
                Initialize();
                PruneClipSoundLimits();
                sSoundConcurrency = value;
                if (AudioManager.TryGetInstance(out var host)) host.TrimSounds(value);
            }
        }

        /// <summary>Set a runtime override for a live Clip instance and immediately trim excess requests. Zero bypasses the default Clip limit, not global capacity.</summary>
        /// <param name="clip">Borrowed Clip; remove its rule before unloading it. No asset ownership is transferred.</param>
        /// <param name="limit">Nonnegative maximum effective requests for this Clip; zero means unlimited.</param>
        /// <remarks>Does not initialize settings or create a playback host.</remarks>
        /// <exception cref="ArgumentNullException">clip 为 null 或已销毁。</exception>
        /// <exception cref="ArgumentOutOfRangeException">limit 为负数。</exception>
        /// <exception cref="InvalidOperationException">AudioKit 正在关闭。</exception>
        public static void SetClipSoundLimit(AudioClip clip, int limit)
        {
            if (!clip) throw new ArgumentNullException(nameof(clip));
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            if (IsClosing) throw new InvalidOperationException("AudioKit is shutting down.");
            PruneClipSoundLimits();
            if (sClipSoundLimits == null) sClipSoundLimits = new Dictionary<int, ClipSoundRule>();
            var id = clip.GetInstanceID();
            sClipSoundLimits[id] = new ClipSoundRule(clip, limit);
            if (AudioManager.TryGetInstance(out var host)) host.TrimClipSounds(id);
        }
        /// <summary>Remove the exact Clip instance override, including a destroyed managed reference, then reconcile the restored default limit. Does not initialize a session.</summary>
        /// <param name="clip">Original borrowed instance reference; reused instance IDs cannot remove another Clip's rule.</param>
        /// <returns>Whether this instance had an override.</returns>
        public static bool RemoveClipSoundLimit(AudioClip clip)
        {
            if (ReferenceEquals(clip, null) || sClipSoundLimits == null) return false;
            var id = clip.GetInstanceID();
            var removed = sClipSoundLimits.TryGetValue(id, out var rule) && ReferenceEquals(rule.Clip, clip) && sClipSoundLimits.Remove(id);
            PruneClipSoundLimits();
            if (removed && AudioManager.TryGetInstance(out var host)) host.TrimClipSounds(id);
            return removed;
        }
        /// <summary>Remove all runtime Clip overrides and immediately reconcile default and global limits. Does not initialize settings or a host.</summary>
        public static void ClearClipSoundLimits()
        {
            if (sClipSoundLimits == null || sClipSoundLimits.Count == 0) return;
            sClipSoundLimits.Clear();
            if (AudioManager.TryGetInstance(out var host)) host.TrimSounds(sSoundConcurrency);
        }
        /// <summary>Passively read the effective limit for a live Clip instance; invalid Clips return zero. Does not initialize settings, a host or collections.</summary>
        /// <param name="clip">Borrowed Clip instance, not a filename or candidate-list entry.</param>
        /// <returns>The instance override if present, otherwise DefaultClipLimit. Zero means unlimited.</returns>
        public static int GetEffectiveClipSoundLimit(AudioClip clip)
        {
            if (!clip) return 0;
            return sClipSoundLimits != null && sClipSoundLimits.TryGetValue(clip.GetInstanceID(), out var rule) && ReferenceEquals(rule.Clip, clip)
                ? rule.Limit : sSoundConcurrency.DefaultClipLimit;
        }
        /// <summary>Passively read effective Sound requests for a live Clip instance, including paused, muted and looping requests. Invalid Clips return zero.</summary>
        /// <param name="clip">Borrowed Clip instance; same-named instances have separate counts.</param>
        /// <returns>The current request count without initializing settings, a host or collections.</returns>
        public static int GetSoundCount(AudioClip clip) => clip && AudioManager.TryGetInstance(out var host) ? host.GetSoundCount(clip.GetInstanceID()) : 0;
        private static void PruneClipSoundLimits()
        {
            if (sClipSoundLimits == null || sClipSoundLimits.Count == 0) return;
            if (sExpiredClipRules == null) sExpiredClipRules = new List<int>();
            foreach (var pair in sClipSoundLimits) if (!pair.Value.Clip) sExpiredClipRules.Add(pair.Key);
            foreach (var id in sExpiredClipRules) sClipSoundLimits.Remove(id);
            sExpiredClipRules.Clear();
        }

        /// <summary>Sound 请求在容量接纳之前应用的帧窗口去重策略；Music 和 Voice 不适用。</summary>
        public enum PlaySoundModes
        {
            /// <summary>允许所有请求，不做去重。默认值。</summary>
            EveryOne,
            /// <summary>拒绝在共享全局帧窗口内已接纳过的同一 Clip 实例的请求；窗口由第一个落在窗口外的接纳重新开始计算。</summary>
            IgnoreSameSoundInGlobalFrames,
            /// <summary>拒绝在该 Clip 实例自身帧窗口内已接纳过的同一实例的请求。</summary>
            IgnoreSameSoundInSoundFrames
        }
        /// <summary>Sound 请求的默认去重模式；单个请求可通过自己的 PlaySoundMode 覆盖。读取会初始化 Settings。</summary>
        /// <exception cref="ArgumentOutOfRangeException">赋值为未定义的枚举值。</exception>
        public static PlaySoundModes PlaySoundMode
        {
            get => Channels.DefaultPlaySoundMode;
            set { if ((uint)value > (uint)PlaySoundModes.IgnoreSameSoundInSoundFrames) throw new ArgumentOutOfRangeException(nameof(value)); Channels.DefaultPlaySoundMode = value; }
        }
        /// <summary>Inclusive global deduplication window. Defaults to five frames; used only by its matching PlaySoundMode. Zero means same frame only.</summary>
        public static int GlobalFrameCountForIgnoreSameSound
        {
            get => Channels.GlobalFrameCountForIgnoreSameSound;
            set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Channels.GlobalFrameCountForIgnoreSameSound = value; }
        }
        /// <summary>Inclusive per-Clip deduplication window. Defaults to five frames; used only by its matching PlaySoundMode. Zero means same frame only.</summary>
        public static int SoundFrameCountForIgnoreSameSound
        {
            get => Channels.SoundFrameCountForIgnoreSameSound;
            set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Channels.SoundFrameCountForIgnoreSameSound = value; }
        }

        /// <summary>Initialize persistent settings without creating a host, source or listener.</summary>
        public static void Initialize()
        {
            if (IsClosing) throw new InvalidOperationException("AudioKit is shutting down.");
            if (sSettings != null) return;
            var settings = new AudioKitSettings();
            try
            {
                sChannels = new SoundDeduplicator();
                sSettings = settings;
                Application.quitting -= BeginQuit;
                Application.quitting += BeginQuit;
            }
            catch { settings.Dispose(); throw; }
        }

        /// <summary>Release owned sources and subscriptions, but never unload borrowed clips.</summary>
        public static void Shutdown()
        {
            if (sShuttingDown) return;
            sShuttingDown = true;
            try { AudioManager.Shutdown(); }
            finally
            {
                try { sSettings?.Dispose(); }
                finally
                {
                    sSettings = null; sChannels = null; sSoundConcurrency = default;
                    sSoundCacheCapacity = 10; sSoundCounters = default; sSoundSession++;
                    sClipSoundLimits = null; sExpiredClipRules = null;
                    sShuttingDown = false;
                }
            }
        }
        internal static void BeginQuit() { sQuitting = true; Shutdown(); }
        // 每次进入播放都会执行（包括关闭 Domain Reload 的会话），避免静态状态在会话之间泄漏。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { sQuitting = false; Shutdown(); }

        /// <summary>Copy validated runtime mixer defaults without creating a playback host or modifying mixer parameters.</summary>
        /// <remarks>The caller owns the config and mixer resources. Asset edits require another explicit Apply.</remarks>
        public static void ApplyMixerConfig(AudioMixerConfig config)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            config.Validate();
            var music = config.MusicGroup;
            var voice = config.VoiceGroup;
            var sound = config.SoundGroup;
            var settings = Settings;
            // 路由写入会同步触发监听器；监听器可能关闭当前会话，因此检测到 Settings 已被移除后停止继续写入。
            settings.MusicOutputGroup.Value = music ? music : null;
            if (IsClosing || !ReferenceEquals(sSettings, settings)) return;
            settings.VoiceOutputGroup.Value = voice ? voice : null;
            if (IsClosing || !ReferenceEquals(sSettings, settings)) return;
            settings.SoundOutputGroup.Value = sound ? sound : null;
        }

        /// <summary>Return inherited requests to direct output. Does not stop overrides or clear shared mixer parameters.</summary>
        public static void ClearMixerRouting()
        {
            var settings = sSettings;
            if (settings == null) return;
            // 路由写入会同步触发监听器；监听器可能关闭当前会话，因此检测到 Settings 已被移除后停止继续写入。
            settings.MusicOutputGroup.Value = null;
            if (IsClosing || !ReferenceEquals(sSettings, settings)) return;
            settings.VoiceOutputGroup.Value = null;
            if (IsClosing || !ReferenceEquals(sSettings, settings)) return;
            settings.SoundOutputGroup.Value = null;
        }

        /// <summary>Replace the music request. A disabled channel retains this request for replay.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="loop">Whether to repeat the clip.</param>
        /// <param name="volume">Request volume multiplier.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; may run before this method returns.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup. Not invoked by Stop or replacement.</param>
        /// <param name="outputRoute">Optional inherited, explicit mixer group or direct output routing.</param>
        public static AudioHandle PlayMusic(AudioClip clip, bool loop = true, float volume = 1f,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null,
            AudioOutputRoute outputRoute = default) => PlaySlot(clip, true, loop, volume, onStarted, onFinished, outputRoute);
        /// <summary>Replace the voice request independently of music and sounds.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="loop">Whether to repeat the clip.</param>
        /// <param name="volume">Request volume multiplier.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; may run before this method returns.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup.</param>
        /// <param name="outputRoute">Optional inherited, explicit mixer group or direct output routing.</param>
        public static AudioHandle PlayVoice(AudioClip clip, bool loop = false, float volume = 1f,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null,
            AudioOutputRoute outputRoute = default) => PlaySlot(clip, false, loop, volume, onStarted, onFinished, outputRoute);
        /// <summary>Replace music using complete 2D request options, including pitch and optional automatic fading.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="options">Copied request configuration; null uses MusicDefaults. Explicit Defaults do not loop. Sound deduplication, Priority and spatial fields are ignored.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; automatic initial gain is set before playback.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup. Automatic fading does not stop early.</param>
        public static AudioHandle PlayMusicWithOptions(AudioClip clip, AudioSoundOptions options,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlaySlotWithOptions(clip, true, options, onStarted, onFinished);
        /// <summary>Replace voice using complete 2D request options, including pitch and optional automatic fading.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="options">Copied request configuration; null uses Defaults. Sound deduplication, Priority and spatial fields are ignored.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; automatic initial gain is set before playback.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup. Automatic fading does not stop early.</param>
        public static AudioHandle PlayVoiceWithOptions(AudioClip clip, AudioSoundOptions options,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlaySlotWithOptions(clip, false, options, onStarted, onFinished);
        private static AudioHandle PlaySlotWithOptions(AudioClip clip, bool music, AudioSoundOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished) =>
            PlaySlotWithSnapshot(clip, music, AudioPlaybackOptions.Capture(options, music), started, finished);
        internal static AudioHandle PlaySlotWithSnapshot(AudioClip clip, bool music, AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished, AudioResourceLease resource = null)
        {
            try
            {
                resource?.Validate();
                if (!clip) return default;
                options.PlaySoundMode = null;
                options.Priority = 0;
                options.Validate(false);
                options.ValidateClip(clip);
                return AudioManager.Instance.PlaySlot(music, clip, in options, started, finished, resource: resource);
            }
            finally { if (resource != null && !resource.IsBound) resource.Dispose(); }
        }
        /// <summary>Replace the voice request with a 3D voice at a fixed world position. Sound deduplication options do not apply.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="position">Fixed world position.</param>
        /// <param name="options">Request parameters; omitted values use Defaults. Sound Priority is ignored.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; may run before this method returns.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup.</param>
        /// <exception cref="ArgumentOutOfRangeException">position 包含非有限分量。</exception>
        public static AudioHandle PlayVoiceAt(AudioClip clip, Vector3 position, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null)
        {
            ValidatePosition(position);
            return PlaySpatialVoice(clip, position, null, options, onStarted, onFinished);
        }
        /// <summary>Replace the voice request with a 3D voice following a live, active target. Losing the target stops the request.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="target">Live, active transform to follow.</param>
        /// <param name="options">Request parameters; omitted values use Defaults. Sound Priority is ignored.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; may run before this method returns.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup. Target loss does not invoke it.</param>
        /// <exception cref="ArgumentOutOfRangeException">target.position 包含非有限分量。</exception>
        public static AudioHandle PlayVoiceFollowing(AudioClip clip, Transform target, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null)
        {
            if (!target || !target.gameObject.activeInHierarchy) return default;
            var position = target.position;
            ValidatePosition(position);
            return PlaySpatialVoice(clip, position, target, options, onStarted, onFinished);
        }
        private static AudioHandle PlaySpatialVoice(AudioClip clip, Vector3 position, Transform target,
            AudioSoundOptions options, Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished)
        {
            if (!clip) return default;
            var resolved = AudioPlaybackOptions.Capture(options);
            resolved.PlaySoundMode = null;
            resolved.Priority = 0;
            resolved.Validate(true);
            resolved.ValidateClip(clip);
            return AudioManager.Instance.PlaySlot(false, clip, in resolved, started, finished, true, position, target);
        }
        private static AudioHandle PlaySlot(AudioClip clip, bool music, bool loop, float volume,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished, AudioOutputRoute outputRoute)
        {
            if (!clip) return default;
            var options = AudioPlaybackOptions.Defaults;
            options.Loop = loop; options.Volume = volume; options.OutputRoute = outputRoute; options.Validate(false);
            var host = AudioManager.Instance;
            return host.PlaySlot(music, clip, in options, started, finished);
        }
        /// <summary>Overlap at most two music requests using an independent transition envelope. Latest request wins during repeated transitions.</summary>
        /// <param name="clip">Caller-owned target clip. Missing clips do not replace current music.</param>
        /// <param name="duration">Finite, nonnegative transition seconds. Zero immediately replaces all old music.</param>
        /// <param name="options">Complete 2D options. Omitted options loop; explicit Defaults do not. Sound deduplication, Priority and spatial fields are ignored.</param>
        /// <param name="onStarted">Actual target clip and original lease after first start, possibly before return. Transition gain is initialized before notification.</param>
        /// <param name="onFinished">Target natural completion only. Outgoing requests are replaced, not naturally completed.</param>
        /// <param name="ignoreTimeScale">Use unscaled time by default. Paused participating requests always freeze the common clock.</param>
        /// <param name="curve">Defaults to EqualPower for uncorrelated background tracks, closer to constant power but not equal loudness. Use Linear explicitly for legacy gain mapping.</param>
        public static AudioHandle CrossFadeMusic(AudioClip clip, float duration, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null,
            bool ignoreTimeScale = true, AudioCrossfadeCurve curve = AudioCrossfadeCurve.EqualPower) =>
            CrossFadeMusicWithSnapshot(clip, duration, AudioPlaybackOptions.Capture(options, true),
                onStarted, onFinished, ignoreTimeScale, curve);
        internal static AudioHandle CrossFadeMusicWithSnapshot(AudioClip clip, float duration, AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null,
            bool ignoreTimeScale = true, AudioCrossfadeCurve curve = AudioCrossfadeCurve.EqualPower, AudioResourceLease resource = null)
        {
            try
            {
                resource?.Validate();
                if (!AudioSoundOptions.IsFinite(duration) || duration < 0f) throw new ArgumentOutOfRangeException(nameof(duration));
                if ((uint)curve > (uint)AudioCrossfadeCurve.EqualPower) throw new ArgumentOutOfRangeException(nameof(curve));
                if (!clip) return default;
                options.PlaySoundMode = null;
                options.Priority = 0;
                options.Validate(false);
                options.ValidateClip(clip);
                return AudioManager.Instance.MusicPlayback.CrossFade(clip, duration, in options, onStarted, onFinished, ignoreTimeScale, curve, resource);
            }
            finally { if (resource != null && !resource.IsBound) resource.Dispose(); }
        }
        /// <summary>Stop current music, its outgoing tail and any transition. Does not create a host.</summary>
        public static void StopMusic() { if (AudioManager.TryGetInstance(out var host)) host.MusicPlayback.Stop(); }
        /// <summary>Pause both music requests and freeze their common transition clock.</summary>
        public static void PauseMusic() { if (AudioManager.TryGetInstance(out var host)) host.MusicPlayback.Pause(); }
        /// <summary>Resume both music requests and the remaining transition.</summary>
        public static void ResumeMusic() { if (AudioManager.TryGetInstance(out var host)) host.MusicPlayback.Resume(); }
        /// <summary>停止当前 Voice 请求，不触发其自然完成回调。没有宿主或请求时安全 no-op。</summary>
        public static void StopVoice() => Voice.Stop();
        /// <summary>暂停当前 Voice 请求。没有宿主或请求时安全 no-op。</summary>
        public static void PauseVoice() => Voice.Pause();
        /// <summary>恢复当前 Voice 请求；等待 Voice 通道重新启用的请求会保持停止，直到该通道恢复启用。</summary>
        public static void ResumeVoice() => Voice.Resume();

        /// <summary>Play a 2D sound, returning a generation-safe control handle.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="options">Request parameters; omitted values use Defaults.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup. Stop and rejection do not invoke it.</param>
        /// <param name="onStarted">Actual clip and original lease after playback starts, before return. Capacity and deduplication are already committed.</param>
        public static AudioHandle PlaySound(AudioClip clip, AudioSoundOptions options = null, Action<AudioClip, AudioHandle> onFinished = null,
            Action<AudioClip, AudioHandle> onStarted = null) =>
            PlaySoundInternal(clip, options, onFinished, onStarted, false, Vector3.zero, null);
        /// <summary>Play a 3D sound at a fixed world position.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="position">Fixed world position.</param>
        /// <param name="options">Request parameters; omitted values use Defaults.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup.</param>
        /// <param name="onStarted">Actual clip and original lease after fully configured playback starts, before return.</param>
        /// <exception cref="ArgumentOutOfRangeException">position 包含非有限分量。</exception>
        public static AudioHandle PlaySoundAt(AudioClip clip, Vector3 position, AudioSoundOptions options = null, Action<AudioClip, AudioHandle> onFinished = null,
            Action<AudioClip, AudioHandle> onStarted = null)
        {
            ValidatePosition(position);
            return PlaySoundInternal(clip, options, onFinished, onStarted, true, position, null);
        }
        private static void ValidatePosition(Vector3 position)
        {
            if (!AudioSoundOptions.IsFinite(position.x) || !AudioSoundOptions.IsFinite(position.y) || !AudioSoundOptions.IsFinite(position.z))
                throw new ArgumentOutOfRangeException(nameof(position));
        }
        /// <summary>Follow a live, active target; losing the target stops rather than completes playback.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for this request.</param>
        /// <param name="target">Live, active transform to follow.</param>
        /// <param name="options">Request parameters; omitted values use Defaults.</param>
        /// <param name="onFinished">Actual clip and original lease on natural completion, before cleanup. Target loss does not invoke it.</param>
        /// <param name="onStarted">Actual clip and original lease after fully configured playback starts, before return.</param>
        /// <exception cref="ArgumentOutOfRangeException">target.position 包含非有限分量。</exception>
        public static AudioHandle PlaySoundFollowing(AudioClip clip, Transform target, AudioSoundOptions options = null, Action<AudioClip, AudioHandle> onFinished = null,
            Action<AudioClip, AudioHandle> onStarted = null)
        {
            if (!target || !target.gameObject.activeInHierarchy) return default;
            var position = target.position;
            ValidatePosition(position);
            return PlaySoundInternal(clip, options, onFinished, onStarted, true, position, target);
        }
        private static AudioHandle PlaySoundInternal(AudioClip clip, AudioSoundOptions options,
            Action<AudioClip, AudioHandle> finished, Action<AudioClip, AudioHandle> started, bool spatial, Vector3 position, Transform target) =>
            PlaySoundWithSnapshot(clip, AudioPlaybackOptions.Capture(options), finished, started, spatial, position, target);
        internal static AudioHandle PlaySoundWithSnapshot(AudioClip clip, AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> onFinished = null, Action<AudioClip, AudioHandle> onStarted = null,
            bool spatial = false, Vector3 position = default, Transform target = null, AudioResourceLease resource = null)
        {
            try
            {
                resource?.Validate();
                if (spatial)
                {
                    if (!ReferenceEquals(target, null))
                    {
                        if (!target || !target.gameObject.activeInHierarchy) return default;
                        position = target.position;
                    }
                    ValidatePosition(position);
                }
                if (!clip) return default;
                options.Validate(spatial);
                options.ValidateClip(clip);
                if (!Settings.IsSoundOn.Value) return default;
                var mode = options.PlaySoundMode ?? PlaySoundMode;
                if (!Channels.CanPlay(clip.GetInstanceID(), mode, Time.frameCount))
                {
                    sSoundCounters.DedupRejectedCount++;
                    return default;
                }
                return AudioManager.Instance.PlaySound(clip, in options, mode, onFinished, onStarted, spatial, position, target, resource);
            }
            finally { if (resource != null && !resource.IsBound) resource.Dispose(); }
        }
        /// <summary>Select uniformly among valid clips without allocating a filtered collection.</summary>
        /// <param name="clips">Caller-owned candidates; null and destroyed entries are ignored.</param>
        /// <param name="options">Request parameters; omitted values use Defaults.</param>
        /// <param name="onFinished">Actual selected clip and original lease on natural completion, before cleanup.</param>
        /// <param name="onStarted">Actual selected clip and original lease after accepted playback starts. Rejection does not notify or reselect.</param>
        public static AudioHandle PlayRandomSound(IReadOnlyList<AudioClip> clips, AudioSoundOptions options = null, Action<AudioClip, AudioHandle> onFinished = null,
            Action<AudioClip, AudioHandle> onStarted = null) =>
            TrySelectRandomClip(clips, out var clip) ? PlaySound(clip, options, onFinished, onStarted) : default;
        /// <summary>Select one weighted clip and play it as a 2D sound. Rejection does not select another clip.</summary>
        /// <param name="clips">Caller-owned candidates; every weight must be finite and nonnegative.</param>
        /// <param name="options">Request parameters and output routing; omitted values use Defaults.</param>
        /// <param name="onFinished">Actual selected clip and original lease on natural completion, before cleanup.</param>
        /// <param name="onStarted">Actual selected clip and original lease after accepted playback starts. Rejection does not notify or reselect.</param>
        public static AudioHandle PlayWeightedRandomSound(IReadOnlyList<AudioWeightedClip> clips, AudioSoundOptions options = null, Action<AudioClip, AudioHandle> onFinished = null,
            Action<AudioClip, AudioHandle> onStarted = null) =>
            TrySelectWeightedRandomClip(clips, out var clip) ? PlaySound(clip, options, onFinished, onStarted) : default;

        /// <summary>Select by relative weight without initializing settings or creating a playback host.</summary>
        /// <param name="clips">Caller-owned entries, unchanged by selection. All weights are validated, including entries without a live clip.</param>
        /// <param name="clip">Selected live clip, or null when no positive-weight live entry exists.</param>
        /// <returns>Whether a live clip was selected. Null and empty collections return false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">An entry has a negative or nonfinite weight.</exception>
        public static bool TrySelectWeightedRandomClip(IReadOnlyList<AudioWeightedClip> clips, out AudioClip clip)
        {
            clip = null;
            if (clips == null) return false;
            double total = 0;
            AudioClip lastValid = null;
            for (var i = 0; i < clips.Count; i++)
            {
                var entry = clips[i];
                if (!AudioSoundOptions.IsFinite(entry.Weight) || entry.Weight < 0f)
                    throw new ArgumentOutOfRangeException(nameof(clips), "Every weight must be finite and nonnegative.");
                if (!entry.Clip || entry.Weight == 0f) continue;
                total += entry.Weight;
                lastValid = entry.Clip;
            }
            if (!lastValid) return false;
            var selected = UnityEngine.Random.value * total;
            double cumulative = 0;
            for (var i = 0; i < clips.Count; i++)
            {
                var entry = clips[i];
                if (!entry.Clip || entry.Weight == 0f) continue;
                cumulative += entry.Weight;
                if (selected < cumulative) { clip = entry.Clip; return true; }
            }
            // Random.value includes one, so the upper endpoint belongs to the last valid entry.
            clip = lastValid;
            return true;
        }

        internal static bool TrySelectRandomClip(IReadOnlyList<AudioClip> clips, out AudioClip clip)
        {
            clip = null;
            if (clips == null) return false;
            var count = 0;
            for (var i = 0; i < clips.Count; i++) if (clips[i]) count++;
            if (count == 0) return false;
            var selected = UnityEngine.Random.Range(0, count);
            for (var i = 0; i < clips.Count; i++)
                if (clips[i] && selected-- == 0) { clip = clips[i]; return true; }
            return false;
        }
        /// <summary>停止所有有效 Sound 请求，不触发自然完成回调。不影响 Music 和 Voice；没有宿主时安全 no-op。</summary>
        public static void StopAllSound() { if (AudioManager.TryGetInstance(out var host)) host.StopAllSound(); }
        /// <summary>A snapshot of active requests, including paused sounds. Reading does not create the host.</summary>
        public static AudioHandle[] GetPlayingSounds()
        {
            if (!AudioManager.TryGetInstance(out var host)) return Array.Empty<AudioHandle>();
            var result = new List<AudioHandle>(host.SoundCount);
            host.CopySounds(result);
            return result.ToArray();
        }
        /// <summary>Replace a caller-owned list with current sound handles without a snapshot array.</summary>
        public static void CopyPlayingSounds(List<AudioHandle> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            if (AudioManager.TryGetInstance(out var host)) host.CopySounds(destination);
        }
    }
}
