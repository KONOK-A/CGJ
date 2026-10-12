using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>请求使用的既有分类设置与 Sound 预算；不决定独占或重叠拓扑。</summary>
    public enum AudioCategory
    {
        /// <summary>Music 分类设置；命名请求不占缺省双声部。</summary>
        Music,
        /// <summary>Voice 分类设置；命名请求不占缺省独占槽。</summary>
        Voice,
        /// <summary>Sound 分类设置、帧去重及全局/Clip 并发预算。</summary>
        Sound
    }
    /// <summary>Exclusive 替换本通道请求；Overlap 为每次播放分配独立 Source。</summary>
    public enum AudioChannelMode
    {
        /// <summary>新请求替换本通道原请求，支持受控 Source 借用。</summary>
        Exclusive,
        /// <summary>每个请求独立播放和控制，不支持借用同一 Source。</summary>
        Overlap
    }

    /// <summary>当前 Audio 会话内的命名控制身份。Dispose 只撤销自己的登记与请求；旧身份不能控制同名新通道。</summary>
    /// <remarks>所有操作须在 Unity 主线程调用。分类音量与本通道音量相乘，静音取 OR；分类 On/Off 语义保持不变。</remarks>
    public sealed partial class AudioChannel : IDisposable
    {
        internal readonly AudioManager Owner;
        private readonly HashSet<AudioVoice> mRequests = new HashSet<AudioVoice>();
        private bool mDisposed, mPaused, mMuted;
        private float mVolume = 1f;
        private ulong mPauseVersion;
        private ulong mVolumeVersion, mMuteVersion;
        internal ulong Epoch { get; private set; }
        internal ulong PendingVersion;
        /// <summary>区分大小写的登记名，不用作隐式存储身份。</summary>
        public string Name { get; }
        /// <summary>显式稳定持久化身份；null 表示不读写任何通道偏好键。</summary>
        public string PersistenceId { get; }
        /// <summary>通道使用的既有分类设置。</summary>
        public AudioCategory Category { get; }
        /// <summary>固定的独占/重叠拓扑。</summary>
        public AudioChannelMode Mode { get; }
        /// <summary>登记是否仍属于可用的原 Audio 会话。</summary>
        public bool IsValid => !mDisposed && Owner && Owner.Available;
        /// <summary>本通道是否显式暂停，不代表每个请求的其他暂停原因。</summary>
        public bool IsPaused => IsValid && mPaused;
        /// <summary>有效请求数，包含暂停和等待分类启用的请求。</summary>
        public int RequestCount => IsValid ? mRequests.Count : 0;
        /// <summary>局部音量实际变化且已更新原请求后通知；同值不通知。Dispose 撤销订阅，不投递初始值。</summary>
        public event Action<float> VolumeChanged;
        /// <summary>局部静音实际变化且已更新原请求后通知；不表示分类静音变化。</summary>
        public event Action<bool> MutedChanged;
        /// <summary>局部 [0,1] 音量，不回写缺省分类设置。</summary>
        public float Volume
        {
            get => mVolume;
            set
            {
                RequireValid();
                if (!AudioSoundOptions.IsFinite(value) || value < 0f || value > 1f) throw new ArgumentOutOfRangeException(nameof(value));
                if (PersistenceId != null) PlayerPrefs.SetFloat(StorageKey("Volume"), value);
                if (mVolume == value) return;
                mVolume = value;
                var version = ++mVolumeVersion;
                foreach (var request in mRequests) request.SetVolume(Owner.GetSharedVolume(request));
                if (VolumeChanged != null) Notify(VolumeChanged, value, () => IsValid && mVolumeVersion == version);
            }
        }
        /// <summary>局部静音，不停止、重播或暂停请求；仅显式 PersistenceId 才持久化。</summary>
        public bool Muted
        {
            get => mMuted;
            set
            {
                RequireValid();
                if (PersistenceId != null) PlayerPrefs.SetInt(StorageKey("Mute"), value ? 1 : 0);
                if (mMuted == value) return;
                mMuted = value;
                var version = ++mMuteVersion;
                foreach (var request in mRequests) request.SetMute(Owner.GetMuted(request));
                if (MutedChanged != null) Notify(MutedChanged, value, () => IsValid && mMuteVersion == version);
            }
        }
        internal AudioChannel(AudioManager owner, string name, AudioCategory category, AudioChannelMode mode, string persistenceId)
        {
            Owner = owner; Name = name; Category = category; Mode = mode; PersistenceId = persistenceId;
            if (persistenceId == null) return;
            var volume = PlayerPrefs.GetFloat(StorageKey("Volume"), 1f);
            mVolume = AudioSoundOptions.IsFinite(volume) && volume >= 0f && volume <= 1f ? volume : 1f;
            mMuted = PlayerPrefs.GetInt(StorageKey("Mute"), 0) != 0;
        }
        private string StorageKey(string setting) => "YLFramework.Audio.Channel:" + PersistenceId + ":" + setting;
        private static void Notify<T>(Action<T> listeners, T value, Func<bool> current)
        {
            if (listeners == null) return;
            List<Exception> failures = null;
            foreach (Action<T> listener in listeners.GetInvocationList())
            {
                if (!current()) break;
                try { listener(value); }
                catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            }
            if (failures != null) throw new AggregateException("Audio channel setting notification failed.", failures);
        }
        internal void RequireValid() { if (!IsValid) throw new InvalidOperationException("Audio channel has expired."); }
        internal static void ValidateIdentity(string identity, string parameter)
        {
            if (string.IsNullOrWhiteSpace(identity) || identity != identity.Trim()) throw new ArgumentException("A nonempty, unpadded identity is required.", parameter);
            foreach (var c in identity)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-' || c == '/'))
                    throw new ArgumentException("Use stable ASCII letters, digits, '.', '_', '-' or '/'.", parameter);
        }
        /// <summary>播放调用方保活的 Clip。null 选项为普通非循环 Defaults；非法配置不会替换旧请求。</summary>
        public AudioHandle Play(AudioClip clip, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlayWithSnapshot(clip, AudioPlaybackOptions.Capture(options), onStarted, onFinished);
        /// <summary>受控借用有效、已停止且调用方让渡独占控制的 Source；重复借用和框架自有 Source 明确拒绝。</summary>
        /// <remarks>仅 Exclusive 支持。不调用 PlayOneShot，不移动/停用/销毁对象，不改空间配置或曲线。
        /// 只配置 clip/loop/pitch/volume/mute/输出路由；Inherit 使用分类路由。释放比较最后写入值再恢复，不重建先前播放历史。
        /// 调用方在租约期间不得直接 Play/Pause/Stop（同 Clip 的外部重播无法可靠识别）；外部换 Clip 会放弃归属及恢复。</remarks>
        public AudioHandle PlayBorrowed(AudioSource source, AudioClip clip, AudioSoundOptions options = null,
            Action<AudioClip, AudioHandle> onStarted = null, Action<AudioClip, AudioHandle> onFinished = null) =>
            PlayWithSnapshot(clip, AudioPlaybackOptions.Capture(options), onStarted, onFinished, source: source, borrowed: true);
        internal AudioHandle PlayWithSnapshot(AudioClip clip, AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> started = null, Action<AudioClip, AudioHandle> finished = null,
            AudioResourceLease resource = null, AudioSource source = null, bool borrowed = false, bool namedCommit = false)
        {
            try
            {
                RequireValid(); resource?.Validate();
                if (borrowed)
                {
                    if (Mode != AudioChannelMode.Exclusive) throw new InvalidOperationException("Borrowing requires an exclusive channel.");
                    Owner.ValidateBorrowedSource(source);
                }
                if (!clip) return default;
                if (Category != AudioCategory.Sound) { options.Priority = 0; options.PlaySoundMode = null; }
                options.Validate(false); options.ValidateClip(clip);
                if (Category == AudioCategory.Sound && !AudioKit.Settings.IsSoundOn.Value) return default;
                var mode = options.PlaySoundMode ?? AudioKit.PlaySoundMode;
                if (Category == AudioCategory.Sound && !AudioKit.Channels.CanPlay(clip.GetInstanceID(), mode, Time.frameCount))
                { AudioKit.SoundCounters.DedupRejectedCount++; return default; }
                var epoch = ++Epoch;
                if (!namedCommit && Mode == AudioChannelMode.Exclusive) Owner.CancelPending(Category, this, AudioPlayEnd.Replaced);
                if (!IsValid || Epoch != epoch || resource != null && !resource.CanBind) return default;
                if (Mode == AudioChannelMode.Exclusive) StopRequests(AudioRequestEnd.Replaced);
                if (!IsValid || Epoch != epoch) return default;
                if (Category == AudioCategory.Sound)
                {
                    if (!AudioKit.CurrentSettings.IsSoundOn.Value) return default;
                    if (!AudioKit.Channels.CanPlay(clip.GetInstanceID(), mode, Time.frameCount))
                    { AudioKit.SoundCounters.DedupRejectedCount++; return default; }
                }
                if (borrowed) Owner.ValidateBorrowedSource(source);
                return Owner.PlayChannel(this, clip, in options, mode, started, finished, resource, source);
            }
            finally { if (resource != null && !resource.IsBound) resource.Dispose(); }
        }
        internal void Add(AudioVoice voice) => mRequests.Add(voice);
        internal void Remove(AudioVoice voice) => mRequests.Remove(voice);
        internal void CopyTo(List<AudioHandle> destination) { foreach (var voice in mRequests) if (voice.Active) destination.Add(voice.Handle); }
        private void StopRequests(AudioRequestEnd reason)
        {
            var snapshot = new List<AudioHandle>(mRequests.Count);
            CopyTo(snapshot);
            List<Exception> failures = null;
            foreach (var handle in snapshot)
                try { if (handle.Voice.Active && handle.Voice.Generation == handle.Generation) handle.Voice.Stop(reason); }
                catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            if (failures != null) throw new AggregateException("Audio channel cleanup failed.", failures);
        }
        /// <summary>停止调用进入时的请求，不停其他通道或清理重入中创建的新代次。</summary>
        public void Stop()
        {
            if (!IsValid) return;
            Epoch++; PendingVersion++;
            var pending = Owner.PendingSnapshot(Category, this);
            try { StopRequests(AudioRequestEnd.Stopped); }
            finally { AudioManager.EndPending(pending, AudioPlayEnd.Stopped); }
        }
        /// <summary>局部暂停也应用于后续本通道请求；不会覆盖逐请求手动暂停。</summary>
        public void Pause() { if (!IsValid) return; mPauseVersion++; mPaused = true; foreach (var voice in mRequests) voice.SetChannelPaused(true); }
        /// <summary>只解除本通道暂停，不解除请求的手动或自动暂停。</summary>
        public void Resume()
        {
            if (!IsValid) return;
            var pauseVersion = ++mPauseVersion;
            mPaused = false;
            var snapshot = new List<AudioHandle>(mRequests.Count); CopyTo(snapshot);
            List<Exception> failures = null;
            foreach (var handle in snapshot)
            {
                if (!IsValid || mPauseVersion != pauseVersion || mPaused) break;
                try { if (handle.IsValid) handle.Voice.SetChannelPaused(false); }
                catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            }
            if (failures != null) throw new AggregateException("Audio channel resume failed.", failures);
        }
        /// <summary>撤销登记后再结束原请求；不删除持久化值。重复调用安全。</summary>
        public void Dispose() => Release(AudioRequestEnd.Stopped);
        internal void Release(AudioRequestEnd reason)
        {
            if (mDisposed) return;
            mDisposed = true; Epoch++;
            VolumeChanged = null; MutedChanged = null;
            Owner.UnregisterChannel(this);
            Owner.CancelPending(Category, this, (AudioPlayEnd)reason);
            StopRequests(reason);
        }
    }
}
