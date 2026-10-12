using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;
using YLFramework.Events;

namespace YLFramework.Services.Audio
{
    internal sealed class AudioManager : MonoSingletonHost<AudioManager>
    {
        private ObjectPool<AudioVoice> mPool;
        private readonly HashSet<AudioVoice> mSounds = new HashSet<AudioVoice>();
        private readonly HashSet<AudioVoice> mChannelVoices = new HashSet<AudioVoice>();
        private readonly Dictionary<string, AudioChannel> mNamedChannels = new Dictionary<string, AudioChannel>(StringComparer.Ordinal);
        private readonly Dictionary<AudioSource, AudioVoice> mBorrowedSources = new Dictionary<AudioSource, AudioVoice>();
        private readonly HashSet<AudioSource> mOwnedSources = new HashSet<AudioSource>();
        private readonly Dictionary<int, int> mClipCounts = new Dictionary<int, int>();
        private readonly List<int> mClipChecks = new List<int>();
        private readonly List<AudioHandle> mChecks = new List<AudioHandle>();
        private readonly List<IUnRegister> mSubscriptions = new List<IUnRegister>(12);
        private bool mDisposing;
        private ulong mAdmissionOrder;
        private ulong mSoundSession;
        internal bool Available => !HostReleased && !mDisposing && !AudioKit.IsClosing && IsCurrentSoundSession;
        internal AudioMusicPlayback MusicPlayback { get; private set; }
        internal AudioVoice Music => MusicPlayback?.Current;
        internal AudioVoice Voice { get; private set; }
        internal int SoundCount => mSounds.Count;
        internal int CachedSoundCount => mPool?.CurStoreCount ?? 0;
        internal int GetSoundCount(int clipId) => mClipCounts.TryGetValue(clipId, out var count) ? count : 0;
        private bool IsCurrentSoundSession => mSoundSession == AudioKit.SoundSession;
        protected override Transform HostParent => Application.isPlaying ? YLFrameworkRoot.Instance.GetModuleRoot("Audio") : null;

        internal new static AudioManager Instance
        {
            get
            {
                if (AudioKit.IsClosing || !CanCreateHost(true)) throw new InvalidOperationException("Audio playback is unavailable during cleanup.");
                AudioKit.Initialize();
                try { return GetInstance(true); }
                catch
                {
                    // 基类只回滚初始化标记；Audio 必须撤销部分订阅、池和 Source 后才允许重试。
                    try { AudioKit.Shutdown(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                    throw;
                }
            }
        }
        public override void OnSingletonInit()
        {
            mSoundSession = AudioKit.SoundSession;
            mPool = new ObjectPool<AudioVoice>(() => new AudioVoice(this, AudioVoice.Kind.Sound),
                reset: voice => voice.ResetPlayback(), onDiscard: voice => voice.DestroySource(),
                maxPoolSize: Math.Max(1, AudioKit.SoundCacheCapacity), cacheOrder: PoolCacheOrder.Lifo);
            MusicPlayback = new AudioMusicPlayback(this); Voice = new AudioVoice(this, AudioVoice.Kind.Voice);
            var settings = AudioKit.Settings;
            mSubscriptions.Add(settings.IsMusicOn.Register(RefreshMusicEnabled));
            mSubscriptions.Add(settings.IsVoiceOn.Register(RefreshVoiceEnabled));
            mSubscriptions.Add(settings.IsSoundOn.Register(value => { if (!value) StopAllSound(); }));
            mSubscriptions.Add(settings.MusicVolume.Register(RefreshMusicVolume));
            mSubscriptions.Add(settings.VoiceVolume.Register(value => { Voice.SetVolume(value); RefreshChannelVolume(AudioCategory.Voice); }));
            mSubscriptions.Add(settings.SoundVolume.Register(SetSoundVolume));
            mSubscriptions.Add(settings.IsMusicMuted.Register(RefreshMusicMute));
            mSubscriptions.Add(settings.IsVoiceMuted.Register(value => { Voice.SetMute(value); RefreshChannelMute(AudioCategory.Voice); }));
            mSubscriptions.Add(settings.IsSoundMuted.Register(SetSoundMute));
            mSubscriptions.Add(settings.MusicOutputGroup.Register(RefreshMusicOutputGroup));
            mSubscriptions.Add(settings.VoiceOutputGroup.Register(RefreshVoiceOutputGroup));
            mSubscriptions.Add(settings.SoundOutputGroup.Register(RefreshSoundOutputGroup));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded += OnSceneLoaded;
            CheckListener();
#endif
        }
        internal AudioChannel RegisterChannel(string name, AudioCategory category, AudioChannelMode mode, string persistenceId)
        {
            if (mNamedChannels.ContainsKey(name)) throw new InvalidOperationException("Audio channel name is already registered: " + name);
            if (persistenceId != null)
                foreach (var channel in mNamedChannels.Values)
                    if (channel.PersistenceId == persistenceId) throw new InvalidOperationException("Audio channel storage identity is already registered: " + persistenceId);
            var result = new AudioChannel(this, name, category, mode, persistenceId);
            mNamedChannels.Add(name, result);
            return result;
        }
        internal void UnregisterChannel(AudioChannel channel)
        {
            if (mNamedChannels.TryGetValue(channel.Name, out var registered) && ReferenceEquals(channel, registered)) mNamedChannels.Remove(channel.Name);
        }
        internal void RegisterOwnedSource(AudioSource source) => mOwnedSources.Add(source);
        internal void UnregisterOwnedSource(AudioSource source) => mOwnedSources.Remove(source);
        internal void ValidateBorrowedSource(AudioSource source)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            if (!source.isActiveAndEnabled) throw new ArgumentException("Borrowed Source must be active and enabled.", nameof(source));
            if (mOwnedSources.Contains(source) || mBorrowedSources.ContainsKey(source) || source.isPlaying)
                throw new InvalidOperationException("Source is owned, already borrowed or still playing. Explicitly stop and relinquish it first.");
        }
        internal void BorrowSource(AudioSource source, AudioVoice voice)
        {
            ValidateBorrowedSource(source);
            mBorrowedSources.Add(source, voice);
        }
        internal void ReturnBorrowedSource(AudioSource source, AudioVoice voice)
        {
            if (mBorrowedSources.TryGetValue(source, out var current) && ReferenceEquals(current, voice)) mBorrowedSources.Remove(source);
        }
        internal AudioHandle PlayChannel(AudioChannel channel, AudioClip clip, in AudioPlaybackOptions options,
            AudioKit.PlaySoundModes mode, Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            AudioResourceLease resource, AudioSource source)
        {
            if (channel.Category == AudioCategory.Sound)
                return PlaySound(clip, in options, mode, finished, started, false, default, null, resource, channel, source);
            if (!ReferenceEquals(source, null)) ValidateBorrowedSource(source);
            var voice = new AudioVoice(this, channel.Category == AudioCategory.Music ? AudioVoice.Kind.Music : AudioVoice.Kind.Voice);
            channel.Add(voice); mChannelVoices.Add(voice);
            return voice.Begin(clip, in options, GetEnabled(voice.Category), GetCategoryVolume(voice.Category) * channel.Volume,
                GetCategoryMuted(voice.Category) || channel.Muted, started, finished, resource: resource, channel: channel, borrowedSource: source);
        }
        internal void ReleaseChannelVoice(AudioVoice voice)
        {
            mChannelVoices.Remove(voice);
            try { voice.ResetPlayback(); }
            finally { voice.DestroySource(); }
        }
        internal AudioHandle PlaySlot(bool music, AudioClip clip, in AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool spatial = false, Vector3 position = default, Transform target = null, AudioResourceLease resource = null)
        {
            if (music) return MusicPlayback.Play(clip, in options, started, finished, resource);
            var slot = Voice;
            slot.Stop(AudioRequestEnd.Replaced);
            if (!Available || slot.Active) return default;
            var settings = AudioKit.Settings;
            return slot.Begin(clip, in options, settings.IsVoiceOn.Value, settings.VoiceVolume.Value,
                settings.IsVoiceMuted.Value, started, finished, spatial, position, target, resource: resource);
        }
        internal AudioHandle PlaySound(AudioClip clip, in AudioPlaybackOptions options, AudioKit.PlaySoundModes mode,
            Action<AudioClip, AudioHandle> finished, Action<AudioClip, AudioHandle> started, bool spatial, Vector3 position, Transform target,
            AudioResourceLease resource = null, AudioChannel channel = null, AudioSource borrowedSource = null)
        {
            var capacity = AudioKit.CurrentSoundConcurrency;
            var channelEpoch = channel?.Epoch ?? 0;
            var clipId = clip.GetInstanceID();
            var clipLimit = AudioKit.GetEffectiveClipSoundLimit(clip);
            var clipCount = clipLimit > 0 ? GetSoundCount(clipId) : -1;
            var clipFull = clipLimit > 0 && clipCount >= clipLimit;
            var globalFull = capacity.Limit > 0 && mSounds.Count >= capacity.Limit;
            if (clipFull || globalFull)
            {
                if (capacity.OverflowPolicy == SoundOverflowPolicy.RejectNew)
                {
                    if (clipFull) AudioKit.SoundCounters.ClipRejectedCount++;
                    else AudioKit.SoundCounters.GlobalRejectedCount++;
                    return default;
                }
                // A full Clip budget restricts the victim set even when global capacity is also full.
                var victim = FindSoundVictim(capacity.OverflowPolicy, clipFull ? (int?)clipId : null);
                if (capacity.OverflowPolicy == SoundOverflowPolicy.StopLowestPriority && options.Priority < victim.Priority)
                {
                    AudioKit.SoundCounters.PriorityRejectedCount++;
                    return default;
                }
                var order = mAdmissionOrder;
                victim.Stop(AudioRequestEnd.Replaced);
                if (!Available || mAdmissionOrder != order || !AudioKit.CurrentSettings.IsSoundOn.Value ||
                    channel != null && (!channel.IsValid || channel.Epoch != channelEpoch)) return default;
                // Stop can synchronously change other counts; discard the cached budget lookup.
                clipCount = -1;
                AudioKit.SoundCounters.StolenCount++;
            }
            if (!ReferenceEquals(borrowedSource, null)) ValidateBorrowedSource(borrowedSource);
            if (!borrowedSource)
            {
                if (mPool.CurStoreCount > 0) AudioKit.SoundCounters.CacheHitCount++;
                else AudioKit.SoundCounters.CacheMissCount++;
            }
            var voice = borrowedSource ? new AudioVoice(this, AudioVoice.Kind.Sound, false) : mPool.Allocate();
            mSounds.Add(voice);
            mClipCounts[clipId] = (clipCount >= 0 ? clipCount : GetSoundCount(clipId)) + 1;
            voice.ClipId = clipId;
            voice.Priority = options.Priority;
            voice.AdmissionOrder = ++mAdmissionOrder;
            voice.Mode = mode;
            channel?.Add(voice);
            var generation = voice.Generation + 1;
            var channels = AudioKit.Channels;
            AudioHandle handle = default;
            try
            {
                var settings = AudioKit.Settings;
                handle = voice.Begin(clip, in options, true, settings.SoundVolume.Value * (channel?.Volume ?? 1f),
                    settings.IsSoundMuted.Value || (channel?.Muted ?? false),
                    started, finished, spatial, position, target, startImmediately: false, resource: resource, channel: channel, borrowedSource: borrowedSource);
                if (!handle.IsValid) return handle;
                channels.Record(clipId, mode, Time.frameCount, handle);
                AudioKit.SoundCounters.AdmittedCount++;
                AudioKit.SoundCounters.PeakActiveCount = Math.Max(AudioKit.SoundCounters.PeakActiveCount, mSounds.Count);
                voice.StartConfigured(handle.Generation);
                return handle;
            }
            catch
            {
                if (IsCurrentSoundSession) AudioKit.SoundCounters.StartFailureCount++;
                if (!ReferenceEquals(handle.Voice, null)) channels.Rollback(clipId, mode, handle);
                if (voice.Generation == generation) voice.Stop(AudioRequestEnd.Failed);
                else if (!voice.Active && mSounds.Contains(voice)) Recycle(voice);
                throw;
            }
        }
        private AudioVoice FindSoundVictim(SoundOverflowPolicy policy, int? clipId = null)
        {
            AudioVoice candidate = null;
            foreach (var sound in mSounds)
            {
                if (clipId.HasValue && sound.ClipId != clipId.Value) continue;
                if (candidate == null ||
                    (policy == SoundOverflowPolicy.StopLowestPriority && sound.Priority < candidate.Priority) ||
                    (policy != SoundOverflowPolicy.StopLowestPriority || sound.Priority == candidate.Priority) &&
                    (policy == SoundOverflowPolicy.RejectNew ? sound.AdmissionOrder > candidate.AdmissionOrder : sound.AdmissionOrder < candidate.AdmissionOrder))
                    candidate = sound;
            }
            return candidate;
        }
        internal void TrimSounds(AudioSoundConcurrency capacity)
        {
            if (!Available) return;
            // Reconcile local budgets first so a global trim never stops an unrelated extra request.
            mClipChecks.Clear();
            foreach (var id in mClipCounts.Keys) mClipChecks.Add(id);
            foreach (var id in mClipChecks) TrimClipBudget(id, capacity.OverflowPolicy);
            mClipChecks.Clear();
            TrimGlobalBudget(capacity);
        }
        internal void TrimClipSounds(int clipId)
        {
            if (!Available) return;
            var capacity = AudioKit.CurrentSoundConcurrency;
            TrimClipBudget(clipId, capacity.OverflowPolicy);
            TrimGlobalBudget(capacity);
        }
        private void TrimClipBudget(int clipId, SoundOverflowPolicy policy)
        {
            var candidate = FindSoundVictim(policy, clipId);
            if (candidate == null) return;
            var limit = AudioKit.GetEffectiveClipSoundLimit(candidate.Clip);
            if (limit == 0) return;
            while (GetSoundCount(clipId) > limit)
            {
                FindSoundVictim(policy, clipId).Stop();
                AudioKit.SoundCounters.TrimmedCount++;
            }
        }
        private void TrimGlobalBudget(AudioSoundConcurrency capacity)
        {
            if (capacity.Limit == 0) return;
            while (mSounds.Count > capacity.Limit)
            {
                FindSoundVictim(capacity.OverflowPolicy).Stop();
                AudioKit.SoundCounters.TrimmedCount++;
            }
        }
        internal void Recycle(AudioVoice voice)
        {
            if (!mSounds.Remove(voice)) return;
            var count = GetSoundCount(voice.ClipId);
            if (count == 1) mClipCounts.Remove(voice.ClipId);
            else mClipCounts[voice.ClipId] = count - 1;
            ReturnIdle(voice);
        }
        private void ReturnIdle(AudioVoice voice)
        {
            if (!voice.PooledSound)
            {
                try { voice.ResetPlayback(); }
                finally { voice.DestroySource(); }
            }
            else if (mDisposing || AudioKit.SoundCacheCapacity == 0 || !voice.Source) mPool.Discard(voice);
            else
            {
                mPool.Recycle(voice);
                // Source 的 OnDisable 可在 Reset 中改为禁缓存；提交后复核且不重复 Reset。
                if (AudioKit.SoundCacheCapacity == 0) mPool.Clear();
            }
        }
        internal void ResizeSoundCache(int capacity)
        {
            // A scene host may be adopted by Awake before its lazy playback initialization.
            if (!Available || mPool == null) return;
            if (capacity == 0) mPool.Clear();
            // 公共池的非正容量表示不限；Audio 的 0 由 Clear/Discard 明确禁缓存。
            mPool.MaxPoolSize = Math.Max(1, capacity);
        }
        internal int PrewarmSounds(int count)
        {
            var original = mPool.CurStoreCount;
            PrepareSoundCapacity(checked(mSounds.Count + count));
            var voices = new List<AudioVoice>(count);
            try
            {
                // Hold all allocations until initialization finishes so LIFO cannot return the same voice repeatedly.
                for (var i = 0; i < count; i++)
                {
                    var voice = mPool.Allocate();
                    voices.Add(voice);
                    voice.EnsureSource();
                }
            }
            finally { foreach (var voice in voices) ReturnIdle(voice); }
            return Math.Max(0, mPool.CurStoreCount - original);
        }
        private void PrepareSoundCapacity(int count)
        {
            mSounds.EnsureCapacity(count);
            mClipCounts.EnsureCapacity(count);
            if (mClipChecks.Capacity < count) mClipChecks.Capacity = count;
            if (mChecks.Capacity < checked(count + 3)) mChecks.Capacity = checked(count + 3);
        }
        internal void SoundSourceCreated()
        {
            if (!IsCurrentSoundSession) return;
            AudioKit.SoundCounters.OwnedSourceCount++;
            AudioKit.SoundCounters.SourceCreatedCount++;
        }
        internal void SoundSourceReleased()
        {
            if (!IsCurrentSoundSession) return;
            AudioKit.SoundCounters.OwnedSourceCount--;
            AudioKit.SoundCounters.SourceReleasedCount++;
        }
        private void SetSoundVolume(float value)
        {
            foreach (var voice in mSounds) voice.SetVolume(value * (voice.Channel?.Volume ?? 1f));
        }
        private void SetSoundMute(bool value)
        {
            foreach (var voice in mSounds) voice.SetMute(value || (voice.Channel?.Muted ?? false));
        }
        internal float GetSharedVolume(AudioVoice voice)
        {
            return GetCategoryVolume(voice.Category) * (voice.Channel?.Volume ?? 1f);
        }
        internal bool GetMuted(AudioVoice voice) => GetCategoryMuted(voice.Category) || (voice.Channel?.Muted ?? false);
        private float GetCategoryVolume(AudioVoice.Kind category)
        {
            var settings = AudioKit.Settings;
            return category == AudioVoice.Kind.Sound ? settings.SoundVolume.Value : category == AudioVoice.Kind.Music ? settings.MusicVolume.Value : settings.VoiceVolume.Value;
        }
        private bool GetCategoryMuted(AudioVoice.Kind category)
        {
            var settings = AudioKit.Settings;
            return category == AudioVoice.Kind.Sound ? settings.IsSoundMuted.Value : category == AudioVoice.Kind.Music ? settings.IsMusicMuted.Value : settings.IsVoiceMuted.Value;
        }
        private bool GetEnabled(AudioVoice.Kind category)
        {
            var settings = AudioKit.Settings;
            return category == AudioVoice.Kind.Music ? settings.IsMusicOn.Value : category == AudioVoice.Kind.Voice ? settings.IsVoiceOn.Value : settings.IsSoundOn.Value;
        }
        private void RefreshEnabled(AudioCategory category)
        {
            if (!Available) return;
            var snapshot = new List<AudioHandle>(mChannelVoices.Count);
            foreach (var voice in mChannelVoices) if (voice.Channel.Category == category) snapshot.Add(voice.Handle);
            List<Exception> failures = null;
            try
            {
                if (category == AudioCategory.Music) MusicPlayback.SetEnabled(AudioKit.CurrentSettings.IsMusicOn.Value);
                else Voice.SetEnabled(AudioKit.CurrentSettings.IsVoiceOn.Value);
            }
            catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            foreach (var handle in snapshot)
                try { if (handle.IsValid) handle.Voice.SetEnabled(GetEnabled(handle.Voice.Category)); }
                catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            if (failures != null) throw new AggregateException("Audio category enabled refresh failed.", failures);
        }
        private void RefreshChannelVolume(AudioCategory category)
        {
            if (!Available) return;
            foreach (var voice in mChannelVoices) if (voice.Channel.Category == category) voice.SetVolume(GetSharedVolume(voice));
        }
        private void RefreshChannelMute(AudioCategory category)
        {
            if (!Available) return;
            foreach (var voice in mChannelVoices) if (voice.Channel.Category == category) voice.SetMute(GetMuted(voice));
        }
        private void RefreshMusicEnabled(bool _) => RefreshEnabled(AudioCategory.Music);
        private void RefreshVoiceEnabled(bool _) => RefreshEnabled(AudioCategory.Voice);
        private void RefreshMusicVolume(float _) { if (Available) { MusicPlayback.SetVolume(AudioKit.CurrentSettings.MusicVolume.Value); RefreshChannelVolume(AudioCategory.Music); } }
        private void RefreshMusicMute(bool _) { if (Available) { MusicPlayback.SetMute(AudioKit.CurrentSettings.IsMusicMuted.Value); RefreshChannelMute(AudioCategory.Music); } }
        // A listener can synchronously change the property again or release this host before our turn.
        private void RefreshMusicOutputGroup(AudioMixerGroup _)
        {
            if (Available) MusicPlayback.SetInheritedOutputGroup(AudioKit.CurrentSettings.MusicOutputGroup.Value);
            RefreshChannelOutput(AudioCategory.Music);
        }
        private void RefreshVoiceOutputGroup(AudioMixerGroup _)
        {
            if (Available) Voice.SetInheritedOutputGroup(AudioKit.CurrentSettings.VoiceOutputGroup.Value);
            RefreshChannelOutput(AudioCategory.Voice);
        }
        private void RefreshChannelOutput(AudioCategory category)
        {
            if (!Available) return;
            foreach (var voice in mChannelVoices) if (voice.Channel.Category == category) voice.SetInheritedOutputGroup(GetOutputGroup(voice));
        }
        private void RefreshSoundOutputGroup(AudioMixerGroup _)
        {
            if (!Available) return;
            var group = AudioKit.CurrentSettings.SoundOutputGroup.Value;
            foreach (var voice in mSounds) voice.SetInheritedOutputGroup(group);
        }
        internal AudioMixerGroup GetOutputGroup(AudioVoice voice)
        {
            var settings = AudioKit.Settings;
            return voice.IsSound ? settings.SoundOutputGroup.Value :
                voice.Category == AudioVoice.Kind.Music ? settings.MusicOutputGroup.Value : settings.VoiceOutputGroup.Value;
        }
        internal void CopySounds(List<AudioHandle> destination)
        {
            foreach (var voice in mSounds) destination.Add(voice.Handle);
        }
        internal void StopAllSound(AudioRequestEnd reason = AudioRequestEnd.Stopped)
        {
            if (mSounds.Count == 0) return;
            // 释放责任可重入播放；只终结本次调用进入时的租约，不能误停新代次。
            var snapshot = new List<AudioHandle>(mSounds.Count);
            CopySounds(snapshot);
            List<Exception> failures = null;
            foreach (var handle in snapshot)
                try { if (handle.Voice.Active && handle.Voice.Generation == handle.Generation) handle.Voice.Stop(reason); }
                catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            if (failures != null) throw new AggregateException("AudioKit sound cleanup failed.", failures);
        }
        private void Update()
        {
            if (Music == null || !Available) return;
            mChecks.Clear();
            var musicEpoch = MusicPlayback.Epoch;
            MusicPlayback.CopyTo(mChecks);
            if (Voice.Active) mChecks.Add(Voice.Handle);
            CopySounds(mChecks);
            foreach (var voice in mChannelVoices) if (voice.Active) mChecks.Add(voice.Handle);
            for (var i = 0; i < mChecks.Count; i++)
            {
                var handle = mChecks[i];
                if (!handle.IsValid) continue;
                try
                {
                    handle.Voice.CheckFinished();
                    if (handle.IsValid) handle.Voice.RefreshAutomaticPause(Time.timeScale == 0f);
                    if (handle.IsValid) handle.Voice.TickFade(Time.deltaTime, Time.unscaledDeltaTime);
                }
                catch (Exception exception)
                {
                    try { if (handle.IsValid) handle.Voice.Stop(AudioRequestEnd.Failed); }
                    catch (Exception cleanup) { Debug.LogException(cleanup); }
                    Debug.LogException(exception);
                }
            }
            mChecks.Clear();
            if (Available && musicEpoch == MusicPlayback.Epoch) MusicPlayback.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            if (Available) AudioKit.Channels.Prune(Time.frameCount);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CheckListener();
        private void CheckListener()
        {
            if (!Application.isPlaying) return;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) return;
            Debug.LogWarning("AudioKit: no active scene AudioListener. Configure a listener on the camera or player; AudioKit does not create one.", this);
        }
#endif
        internal static void Shutdown()
        {
            List<Exception> failures = null;
            void Release(AudioManager host)
            {
                try { host.ReleaseHost(); }
                catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            }
            if (!ReferenceEquals(CachedHost, null)) Release(CachedHost);
            foreach (var host in FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Release(host);
            ResetHostReference();
            if (failures != null) throw new AggregateException("AudioKit host cleanup failed.", failures);
        }
        protected override void OnHostRelease()
        {
            if (mDisposing) return;
            mDisposing = true;
            List<Exception> failures = null;
            void Cleanup(Action operation)
            {
                try { operation(); }
                catch (Exception exception) { (failures ?? (failures = new List<Exception>())).Add(exception); }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded -= OnSceneLoaded;
#endif
            foreach (var subscription in mSubscriptions) Cleanup(subscription.UnRegister);
            mSubscriptions.Clear();
            foreach (var channel in new List<AudioChannel>(mNamedChannels.Values)) Cleanup(() => channel.Release(AudioRequestEnd.Shutdown));
            mNamedChannels.Clear();
            Cleanup(() => StopAllSound(AudioRequestEnd.Shutdown));
            if (MusicPlayback != null) Cleanup(MusicPlayback.DestroySources);
            if (Voice != null) Cleanup(Voice.DestroySource);
            if (mPool != null) Cleanup(mPool.Dispose);
            // A callback may release this host while Update is iterating its snapshot.
            if (failures != null) throw new AggregateException("AudioKit source cleanup failed.", failures);
        }
        protected override void OnApplicationQuit()
        {
            // 已释放或旧会话宿主的迟到消息不能关闭新会话，亦不能重置公共宿主 gate。
            if (HostReleased || !IsCurrentSoundSession) return;
            base.OnApplicationQuit();
            AudioKit.BeginQuit();
        }
    }
}
