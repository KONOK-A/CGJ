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
        private ObjectPool<AudioVoice> _pool;
        private readonly HashSet<AudioVoice> _sounds = new HashSet<AudioVoice>();
        private readonly HashSet<AudioVoice> _channelVoices = new HashSet<AudioVoice>();
        private readonly Dictionary<string, AudioChannel> _namedChannels = new Dictionary<string, AudioChannel>(StringComparer.Ordinal);
        private readonly Dictionary<AudioSource, AudioVoice> _borrowedSources = new Dictionary<AudioSource, AudioVoice>();
        private readonly HashSet<AudioSource> _ownedSources = new HashSet<AudioSource>();
        private readonly Dictionary<int, int> _clipCounts = new Dictionary<int, int>();
        private readonly List<int> _clipChecks = new List<int>();
        private readonly List<AudioHandle> _checks = new List<AudioHandle>();
        private readonly List<IUnregister> _subscriptions = new List<IUnregister>(12);
        private bool _isDisposing;
        private ulong _admissionOrder;
        private ulong _soundSession;
        private readonly List<AudioPlayRequest> _pending = new List<AudioPlayRequest>();
        private readonly ulong[] _pendingVersions = new ulong[3];
        internal AudioClipCache ClipCache { get; } = new AudioClipCache();
        internal bool IsAvailable => !IsHostReleased && !_isDisposing && !AudioKit.IsClosing && IsCurrentSoundSession;
        internal AudioMusicPlayback MusicPlayback
        {
            get; private set;
        }
        internal AudioVoice Music => MusicPlayback?.Current;
        internal AudioVoice Voice
        {
            get; private set;
        }
        internal int SoundCount => _sounds.Count;
        internal int CachedSoundCount => _pool?.CurStoreCount ?? 0;
        internal int GetSoundCount(int clipId) => _clipCounts.TryGetValue(clipId, out var count) ? count : 0;
        private bool IsCurrentSoundSession => _soundSession == AudioKit.SoundSession;
        protected override Transform HostParent => Application.isPlaying ? YLFrameworkRoot.Instance.GetModuleRoot("Audio") : null;

        internal new static AudioManager Instance
        {
            get
            {
                if (AudioKit.IsClosing || !CanCreateHost(true))
                {
                    throw new InvalidOperationException("Audio playback is unavailable during cleanup.");
                }

                AudioKit.Initialize();
                try
                {
                    return GetInstance(true);
                }
                catch
                {
                    // 基类只回滚初始化标记；Audio 必须撤销部分订阅、池和 Source 后才允许重试。
                    try
                    {
                        AudioKit.Shutdown();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                    throw;
                }
            }
        }
        public override void OnSingletonInit()
        {
            _soundSession = AudioKit.SoundSession;
            _pool = new ObjectPool<AudioVoice>(() => new AudioVoice(this, AudioVoice.Kind.Sound),
                reset: voice => voice.ResetPlayback(), onDiscard: voice => voice.DestroySource(),
                maxPoolSize: Math.Max(1, AudioKit.SoundCacheCapacity), cacheOrder: PoolCacheOrder.Lifo);
            MusicPlayback = new AudioMusicPlayback(this);
            Voice = new AudioVoice(this, AudioVoice.Kind.Voice);
            var settings = AudioKit.Settings;
            _subscriptions.Add(settings.IsMusicOn.Register(RefreshMusicEnabled));
            _subscriptions.Add(settings.IsVoiceOn.Register(RefreshVoiceEnabled));
            _subscriptions.Add(settings.IsSoundOn.Register(value =>
            {
                if (!value)
                {
                    StopSoundsAndPending();
                }
            }));
            _subscriptions.Add(settings.MusicVolume.Register(RefreshMusicVolume));
            _subscriptions.Add(settings.VoiceVolume.Register(value =>
            {
                Voice.SetVolume(value);
                RefreshChannelVolume(AudioCategory.Voice);
            }));
            _subscriptions.Add(settings.SoundVolume.Register(SetSoundVolume));
            _subscriptions.Add(settings.IsMusicMuted.Register(RefreshMusicMute));
            _subscriptions.Add(settings.IsVoiceMuted.Register(value =>
            {
                Voice.SetMute(value);
                RefreshChannelMute(AudioCategory.Voice);
            }));
            _subscriptions.Add(settings.IsSoundMuted.Register(SetSoundMute));
            _subscriptions.Add(settings.MusicOutputGroup.Register(RefreshMusicOutputGroup));
            _subscriptions.Add(settings.VoiceOutputGroup.Register(RefreshVoiceOutputGroup));
            _subscriptions.Add(settings.SoundOutputGroup.Register(RefreshSoundOutputGroup));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded += OnSceneLoaded;
            CheckListener();
#endif
        }
        internal AudioChannel RegisterChannel(string name, AudioCategory category, AudioChannelMode mode, string persistenceId)
        {
            if (_namedChannels.ContainsKey(name))
            {
                throw new InvalidOperationException("Audio channel name is already registered: " + name);
            }

            if (persistenceId != null)
            {
                foreach (var channel in _namedChannels.Values)
                {
                    if (channel.PersistenceId == persistenceId)
                    {
                        throw new InvalidOperationException("Audio channel storage identity is already registered: " + persistenceId);
                    }
                }
            }

            var result = new AudioChannel(this, name, category, mode, persistenceId);
            _namedChannels.Add(name, result);
            return result;
        }
        internal void AddPending(AudioPlayRequest request)
        {
            if (request.Category == AudioCategory.Sound && !AudioKit.Settings.IsSoundOn.Value)
            {
                request.End(AudioPlayEnd.Rejected);
                return;
            }
            var version = AdvancePendingVersion(request.Category, request.Channel);
            if (request.Channel?.Mode != AudioChannelMode.Overlap && (request.Channel != null || request.Category != AudioCategory.Sound))
            {
                CancelPending(request.Category, request.Channel, AudioPlayEnd.Replaced, incrementVersion: false);
            }

            if (!IsAvailable || version != PendingVersion(request.Category, request.Channel) || request.Channel != null && !request.Channel.IsValid)
            {
                request.End(AudioPlayEnd.Unavailable);
                return;
            }
            _pending.Add(request);
        }
        internal void RemovePending(AudioPlayRequest request) => _pending.Remove(request);
        internal ulong PendingVersion(AudioCategory category, AudioChannel channel) => channel?.PendingVersion ?? _pendingVersions[(int)category];
        internal ulong AdvancePendingVersion(AudioCategory category, AudioChannel channel) => channel != null ? ++channel.PendingVersion : ++_pendingVersions[(int)category];
        internal AudioPlayRequest[] PendingSnapshot(AudioCategory category, AudioChannel channel, bool allChannels = false)
        {
            if (_pending.Count == 0)
            {
                return Array.Empty<AudioPlayRequest>();
            }

            var result = new List<AudioPlayRequest>();
            foreach (var request in _pending)
            {
                if (request.Category == category && (allChannels || ReferenceEquals(request.Channel, channel)))
                {
                    result.Add(request);
                }
            }

            return result.ToArray();
        }
        internal static void EndPending(AudioPlayRequest[] snapshot, AudioPlayEnd reason)
        {
            List<Exception> failures = null;
            foreach (var request in snapshot)
            {
                try
                {
                    request.End(reason);
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }
            }

            if (failures != null)
            {
                throw new AggregateException("Audio pending cleanup failed.", failures);
            }
        }
        internal void CancelPending(AudioCategory category, AudioChannel channel, AudioPlayEnd reason, bool allChannels = false, bool incrementVersion = true)
        {
            if (incrementVersion)
            {
                AdvancePendingVersion(category, channel);
            }

            if (_pending.Count == 0)
            {
                return;
            }

            EndPending(PendingSnapshot(category, channel, allChannels), reason);
        }
        internal void StopSoundsAndPending()
        {
            AdvancePendingVersion(AudioCategory.Sound, null);
            var pending = PendingSnapshot(AudioCategory.Sound, null, true);
            try
            {
                StopAllSound();
            }
            finally
            {
                EndPending(pending, AudioPlayEnd.Stopped);
            }
        }
        internal void UnregisterChannel(AudioChannel channel)
        {
            if (_namedChannels.TryGetValue(channel.Name, out var registered) && ReferenceEquals(channel, registered))
            {
                _namedChannels.Remove(channel.Name);
            }
        }
        internal void RegisterOwnedSource(AudioSource source) => _ownedSources.Add(source);
        internal void UnregisterOwnedSource(AudioSource source) => _ownedSources.Remove(source);
        internal void ValidateBorrowedSource(AudioSource source)
        {
            if (!source)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!source.isActiveAndEnabled)
            {
                throw new ArgumentException("Borrowed Source must be active and enabled.", nameof(source));
            }

            if (_ownedSources.Contains(source) || _borrowedSources.ContainsKey(source) || source.isPlaying)
            {
                throw new InvalidOperationException("Source is owned, already borrowed or still playing. Explicitly stop and relinquish it first.");
            }
        }
        internal void BorrowSource(AudioSource source, AudioVoice voice)
        {
            ValidateBorrowedSource(source);
            _borrowedSources.Add(source, voice);
        }
        internal void ReturnBorrowedSource(AudioSource source, AudioVoice voice)
        {
            if (_borrowedSources.TryGetValue(source, out var current) && ReferenceEquals(current, voice))
            {
                _borrowedSources.Remove(source);
            }
        }
        internal AudioHandle PlayChannel(AudioChannel channel, AudioClip clip, in AudioPlaybackOptions options,
            AudioKit.PlaySoundModes mode, Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            AudioResourceLease resource, AudioSource source)
        {
            if (resource != null && !resource.CanBind)
            {
                return default;
            }

            if (channel.Category == AudioCategory.Sound)
            {
                return PlaySound(clip, in options, mode, finished, started, false, default, null, resource, channel, source);
            }

            if (!ReferenceEquals(source, null))
            {
                ValidateBorrowedSource(source);
            }

            var voice = new AudioVoice(this, channel.Category == AudioCategory.Music ? AudioVoice.Kind.Music : AudioVoice.Kind.Voice);
            channel.Add(voice);
            _channelVoices.Add(voice);
            return voice.Begin(clip, in options, GetEnabled(voice.Category), GetCategoryVolume(voice.Category) * channel.Volume,
                GetCategoryMuted(voice.Category) || channel.IsMuted, started, finished, resource: resource, channel: channel, borrowedSource: source);
        }
        internal void ReleaseChannelVoice(AudioVoice voice)
        {
            _channelVoices.Remove(voice);
            try
            {
                voice.ResetPlayback();
            }
            finally
            {
                voice.DestroySource();
            }
        }
        internal AudioHandle PlaySlot(bool music, AudioClip clip, in AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool spatial = false, Vector3 position = default, Transform target = null, AudioResourceLease resource = null)
        {
            if (music)
            {
                return MusicPlayback.Play(clip, in options, started, finished, resource);
            }

            var slot = Voice;
            slot.Stop(AudioRequestEnd.Replaced);
            if (!IsAvailable || slot.IsActive || resource != null && !resource.CanBind)
            {
                return default;
            }

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
            var globalFull = capacity.Limit > 0 && _sounds.Count >= capacity.Limit;
            if (clipFull || globalFull)
            {
                if (capacity.OverflowPolicy == SoundOverflowPolicy.RejectNew)
                {
                    if (clipFull)
                    {
                        AudioKit.SoundCounters.ClipRejectedCount++;
                    }
                    else
                    {
                        AudioKit.SoundCounters.GlobalRejectedCount++;
                    }

                    return default;
                }
                // A full Clip budget restricts the victim set even when global capacity is also full.
                var victim = FindSoundVictim(capacity.OverflowPolicy, clipFull ? (int?)clipId : null);
                if (capacity.OverflowPolicy == SoundOverflowPolicy.StopLowestPriority && options.Priority < victim.Priority)
                {
                    AudioKit.SoundCounters.PriorityRejectedCount++;
                    return default;
                }
                var order = _admissionOrder;
                victim.Stop(AudioRequestEnd.Replaced);
                if (!IsAvailable || _admissionOrder != order || !AudioKit.CurrentSettings.IsSoundOn.Value || resource != null && !resource.CanBind ||
                    channel != null && (!channel.IsValid || channel.Epoch != channelEpoch))
                {
                    return default;
                }
                // Stop can synchronously change other counts; discard the cached budget lookup.
                clipCount = -1;
                AudioKit.SoundCounters.StolenCount++;
            }
            if (resource != null && !resource.CanBind)
            {
                return default;
            }

            if (!ReferenceEquals(borrowedSource, null))
            {
                ValidateBorrowedSource(borrowedSource);
            }

            if (!borrowedSource)
            {
                if (_pool.CurStoreCount > 0)
                {
                    AudioKit.SoundCounters.CacheHitCount++;
                }
                else
                {
                    AudioKit.SoundCounters.CacheMissCount++;
                }
            }
            var voice = borrowedSource ? new AudioVoice(this, AudioVoice.Kind.Sound, false) : _pool.Allocate();
            _sounds.Add(voice);
            _clipCounts[clipId] = (clipCount >= 0 ? clipCount : GetSoundCount(clipId)) + 1;
            voice.ClipId = clipId;
            voice.Priority = options.Priority;
            voice.AdmissionOrder = ++_admissionOrder;
            voice.Mode = mode;
            channel?.Add(voice);
            var generation = voice.Generation + 1;
            var channels = AudioKit.Channels;
            AudioHandle handle = default;
            try
            {
                var settings = AudioKit.Settings;
                handle = voice.Begin(clip, in options, true, settings.SoundVolume.Value * (channel?.Volume ?? 1f),
                    settings.IsSoundMuted.Value || (channel?.IsMuted ?? false),
                    started, finished, spatial, position, target, startImmediately: false, resource: resource, channel: channel, borrowedSource: borrowedSource);
                if (!handle.IsValid)
                {
                    return handle;
                }

                channels.Record(clipId, mode, Time.frameCount, handle);
                AudioKit.SoundCounters.AdmittedCount++;
                AudioKit.SoundCounters.PeakActiveCount = Math.Max(AudioKit.SoundCounters.PeakActiveCount, _sounds.Count);
                voice.StartConfigured(handle.Generation);
                return handle;
            }
            catch
            {
                if (IsCurrentSoundSession)
                {
                    AudioKit.SoundCounters.StartFailureCount++;
                }

                if (!ReferenceEquals(handle.Voice, null))
                {
                    channels.Rollback(clipId, mode, handle);
                }

                if (voice.Generation == generation)
                {
                    voice.Stop(AudioRequestEnd.Failed);
                }
                else if (!voice.IsActive && _sounds.Contains(voice))
                {
                    Recycle(voice);
                }

                throw;
            }
        }
        private AudioVoice FindSoundVictim(SoundOverflowPolicy policy, int? clipId = null)
        {
            AudioVoice candidate = null;
            foreach (var sound in _sounds)
            {
                if (clipId.HasValue && sound.ClipId != clipId.Value)
                {
                    continue;
                }

                if (candidate == null ||
                    (policy == SoundOverflowPolicy.StopLowestPriority && sound.Priority < candidate.Priority) ||
                    (policy != SoundOverflowPolicy.StopLowestPriority || sound.Priority == candidate.Priority) &&
                    (policy == SoundOverflowPolicy.RejectNew ? sound.AdmissionOrder > candidate.AdmissionOrder : sound.AdmissionOrder < candidate.AdmissionOrder))
                {
                    candidate = sound;
                }
            }
            return candidate;
        }
        internal void TrimSounds(AudioSoundConcurrency capacity)
        {
            if (!IsAvailable)
            {
                return;
            }
            // Reconcile local budgets first so a global trim never stops an unrelated extra request.
            _clipChecks.Clear();
            foreach (var id in _clipCounts.Keys)
            {
                _clipChecks.Add(id);
            }

            foreach (var id in _clipChecks)
            {
                TrimClipBudget(id, capacity.OverflowPolicy);
            }

            _clipChecks.Clear();
            TrimGlobalBudget(capacity);
        }
        internal void TrimClipSounds(int clipId)
        {
            if (!IsAvailable)
            {
                return;
            }

            var capacity = AudioKit.CurrentSoundConcurrency;
            TrimClipBudget(clipId, capacity.OverflowPolicy);
            TrimGlobalBudget(capacity);
        }
        private void TrimClipBudget(int clipId, SoundOverflowPolicy policy)
        {
            var candidate = FindSoundVictim(policy, clipId);
            if (candidate == null)
            {
                return;
            }

            var limit = AudioKit.GetEffectiveClipSoundLimit(candidate.Clip);
            if (limit == 0)
            {
                return;
            }

            while (GetSoundCount(clipId) > limit)
            {
                FindSoundVictim(policy, clipId).Stop();
                AudioKit.SoundCounters.TrimmedCount++;
            }
        }
        private void TrimGlobalBudget(AudioSoundConcurrency capacity)
        {
            if (capacity.Limit == 0)
            {
                return;
            }

            while (_sounds.Count > capacity.Limit)
            {
                FindSoundVictim(capacity.OverflowPolicy).Stop();
                AudioKit.SoundCounters.TrimmedCount++;
            }
        }
        internal void Recycle(AudioVoice voice)
        {
            if (!_sounds.Remove(voice))
            {
                return;
            }

            var count = GetSoundCount(voice.ClipId);
            if (count == 1)
            {
                _clipCounts.Remove(voice.ClipId);
            }
            else
            {
                _clipCounts[voice.ClipId] = count - 1;
            }

            ReturnIdle(voice);
        }
        private void ReturnIdle(AudioVoice voice)
        {
            if (!voice.PooledSound)
            {
                try
                {
                    voice.ResetPlayback();
                }
                finally
                {
                    voice.DestroySource();
                }
            }
            else if (_isDisposing || AudioKit.SoundCacheCapacity == 0 || !voice.Source)
            {
                _pool.Discard(voice);
            }
            else
            {
                _pool.Recycle(voice);
                // Source 的 OnDisable 可在 Reset 中改为禁缓存；提交后复核且不重复 Reset。
                if (AudioKit.SoundCacheCapacity == 0)
                {
                    _pool.Clear();
                }
            }
        }
        internal void ResizeSoundCache(int capacity)
        {
            // A scene host may be adopted by Awake before its lazy playback initialization.
            if (!IsAvailable || _pool == null)
            {
                return;
            }

            if (capacity == 0)
            {
                _pool.Clear();
            }
            // 公共池的非正容量表示不限；Audio 的 0 由 Clear/Discard 明确禁缓存。
            _pool.MaxPoolSize = Math.Max(1, capacity);
        }
        internal int PrewarmSounds(int count)
        {
            var original = _pool.CurStoreCount;
            PrepareSoundCapacity(checked(_sounds.Count + count));
            var voices = new List<AudioVoice>(count);
            try
            {
                // Hold all allocations until initialization finishes so LIFO cannot return the same voice repeatedly.
                for (var i = 0; i < count; i++)
                {
                    var voice = _pool.Allocate();
                    voices.Add(voice);
                    voice.EnsureSource();
                }
            }
            finally
            {
                foreach (var voice in voices)
                {
                    ReturnIdle(voice);
                }
            }
            return Math.Max(0, _pool.CurStoreCount - original);
        }
        private void PrepareSoundCapacity(int count)
        {
            _sounds.EnsureCapacity(count);
            _clipCounts.EnsureCapacity(count);
            if (_clipChecks.Capacity < count)
            {
                _clipChecks.Capacity = count;
            }

            if (_checks.Capacity < checked(count + 3))
            {
                _checks.Capacity = checked(count + 3);
            }
        }
        internal void SoundSourceCreated()
        {
            if (!IsCurrentSoundSession)
            {
                return;
            }

            AudioKit.SoundCounters.OwnedSourceCount++;
            AudioKit.SoundCounters.SourceCreatedCount++;
        }
        internal void SoundSourceReleased()
        {
            if (!IsCurrentSoundSession)
            {
                return;
            }

            AudioKit.SoundCounters.OwnedSourceCount--;
            AudioKit.SoundCounters.SourceReleasedCount++;
        }
        private void SetSoundVolume(float value)
        {
            foreach (var voice in _sounds)
            {
                voice.SetVolume(value * (voice.Channel?.Volume ?? 1f));
            }
        }
        private void SetSoundMute(bool value)
        {
            foreach (var voice in _sounds)
            {
                voice.SetMute(value || (voice.Channel?.IsMuted ?? false));
            }
        }
        internal float GetSharedVolume(AudioVoice voice)
        {
            return GetCategoryVolume(voice.Category) * (voice.Channel?.Volume ?? 1f);
        }
        internal bool GetMuted(AudioVoice voice) => GetCategoryMuted(voice.Category) || (voice.Channel?.IsMuted ?? false);
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
            if (!IsAvailable)
            {
                return;
            }

            var snapshot = new List<AudioHandle>(_channelVoices.Count);
            foreach (var voice in _channelVoices)
            {
                if (voice.Channel.Category == category)
                {
                    snapshot.Add(voice.Handle);
                }
            }

            List<Exception> failures = null;
            try
            {
                if (category == AudioCategory.Music)
                {
                    MusicPlayback.SetEnabled(AudioKit.CurrentSettings.IsMusicOn.Value);
                }
                else
                {
                    Voice.SetEnabled(AudioKit.CurrentSettings.IsVoiceOn.Value);
                }
            }
            catch (Exception exception)
            {
                (failures ?? (failures = new List<Exception>())).Add(exception);
            }
            foreach (var handle in snapshot)
            {
                try
                {
                    if (handle.IsValid)
                    {
                        handle.Voice.SetEnabled(GetEnabled(handle.Voice.Category));
                    }
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }
            }

            if (failures != null)
            {
                throw new AggregateException("Audio category enabled refresh failed.", failures);
            }
        }
        private void RefreshChannelVolume(AudioCategory category)
        {
            if (!IsAvailable)
            {
                return;
            }

            foreach (var voice in _channelVoices)
            {
                if (voice.Channel.Category == category)
                {
                    voice.SetVolume(GetSharedVolume(voice));
                }
            }
        }
        private void RefreshChannelMute(AudioCategory category)
        {
            if (!IsAvailable)
            {
                return;
            }

            foreach (var voice in _channelVoices)
            {
                if (voice.Channel.Category == category)
                {
                    voice.SetMute(GetMuted(voice));
                }
            }
        }
        private void RefreshMusicEnabled(bool _) => RefreshEnabled(AudioCategory.Music);
        private void RefreshVoiceEnabled(bool _) => RefreshEnabled(AudioCategory.Voice);
        private void RefreshMusicVolume(float _)
        {
            if (IsAvailable)
            {
                MusicPlayback.SetVolume(AudioKit.CurrentSettings.MusicVolume.Value);
                RefreshChannelVolume(AudioCategory.Music);
            }
        }
        private void RefreshMusicMute(bool _)
        {
            if (IsAvailable)
            {
                MusicPlayback.SetMute(AudioKit.CurrentSettings.IsMusicMuted.Value);
                RefreshChannelMute(AudioCategory.Music);
            }
        }
        // A listener can synchronously change the property again or release this host before our turn.
        private void RefreshMusicOutputGroup(AudioMixerGroup _)
        {
            if (IsAvailable)
            {
                MusicPlayback.SetInheritedOutputGroup(AudioKit.CurrentSettings.MusicOutputGroup.Value);
            }

            RefreshChannelOutput(AudioCategory.Music);
        }
        private void RefreshVoiceOutputGroup(AudioMixerGroup _)
        {
            if (IsAvailable)
            {
                Voice.SetInheritedOutputGroup(AudioKit.CurrentSettings.VoiceOutputGroup.Value);
            }

            RefreshChannelOutput(AudioCategory.Voice);
        }
        private void RefreshChannelOutput(AudioCategory category)
        {
            if (!IsAvailable)
            {
                return;
            }

            foreach (var voice in _channelVoices)
            {
                if (voice.Channel.Category == category)
                {
                    voice.SetInheritedOutputGroup(GetOutputGroup(voice));
                }
            }
        }
        private void RefreshSoundOutputGroup(AudioMixerGroup _)
        {
            if (!IsAvailable)
            {
                return;
            }

            var group = AudioKit.CurrentSettings.SoundOutputGroup.Value;
            foreach (var voice in _sounds)
            {
                voice.SetInheritedOutputGroup(group);
            }
        }
        internal AudioMixerGroup GetOutputGroup(AudioVoice voice)
        {
            var settings = AudioKit.Settings;
            return voice.IsSound ? settings.SoundOutputGroup.Value :
                voice.Category == AudioVoice.Kind.Music ? settings.MusicOutputGroup.Value : settings.VoiceOutputGroup.Value;
        }
        internal void CopySounds(List<AudioHandle> destination)
        {
            foreach (var voice in _sounds)
            {
                destination.Add(voice.Handle);
            }
        }
        internal void StopAllSound(AudioRequestEnd reason = AudioRequestEnd.Stopped)
        {
            if (_sounds.Count == 0)
            {
                return;
            }
            // 释放责任可重入播放；只终结本次调用进入时的租约，不能误停新代次。
            var snapshot = new List<AudioHandle>(_sounds.Count);
            CopySounds(snapshot);
            List<Exception> failures = null;
            foreach (var handle in snapshot)
            {
                try
                {
                    if (handle.Voice.IsActive && handle.Voice.Generation == handle.Generation)
                    {
                        handle.Voice.Stop(reason);
                    }
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }
            }

            if (failures != null)
            {
                throw new AggregateException("AudioKit sound cleanup failed.", failures);
            }
        }
        private void Update()
        {
            if (Music == null || !IsAvailable)
            {
                return;
            }

            if (_pending.Count > 0)
            {
                foreach (var request in _pending.ToArray())
                {
                    try
                    {
                        request.Tick();
                    }
                    catch (Exception exception)
                    {
                        request.Fail(exception);
                    }
                }
            }

            ClipCache.Tick(Time.realtimeSinceStartupAsDouble);
            _checks.Clear();
            var musicEpoch = MusicPlayback.Epoch;
            MusicPlayback.CopyTo(_checks);
            if (Voice.IsActive)
            {
                _checks.Add(Voice.Handle);
            }

            CopySounds(_checks);
            foreach (var voice in _channelVoices)
            {
                if (voice.IsActive)
                {
                    _checks.Add(voice.Handle);
                }
            }

            for (var i = 0; i < _checks.Count; i++)
            {
                var handle = _checks[i];
                if (!handle.IsValid)
                {
                    continue;
                }

                try
                {
                    handle.Voice.CheckFinished();
                    if (handle.IsValid)
                    {
                        handle.Voice.RefreshAutomaticPause(Time.timeScale == 0f);
                    }

                    if (handle.IsValid)
                    {
                        handle.Voice.TickFade(Time.deltaTime, Time.unscaledDeltaTime);
                    }
                }
                catch (Exception exception)
                {
                    try
                    {
                        if (handle.IsValid)
                        {
                            handle.Voice.Stop(AudioRequestEnd.Failed);
                        }
                    }
                    catch (Exception cleanup)
                    {
                        Debug.LogException(cleanup);
                    }
                    Debug.LogException(exception);
                }
            }
            _checks.Clear();
            if (IsAvailable && musicEpoch == MusicPlayback.Epoch)
            {
                MusicPlayback.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            }

            if (IsAvailable)
            {
                AudioKit.Channels.Prune(Time.frameCount);
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => CheckListener();
        private void CheckListener()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener.isActiveAndEnabled)
                {
                    return;
                }
            }

            Debug.LogWarning("AudioKit: no active scene AudioListener. Configure a listener on the camera or player; AudioKit does not create one.", this);
        }
#endif
        internal static void Shutdown()
        {
            List<Exception> failures = null;
            void Release(AudioManager host)
            {
                try
                {
                    host.ReleaseHost();
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }
            }
            if (!ReferenceEquals(CachedHost, null))
            {
                Release(CachedHost);
            }

            foreach (var host in FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Release(host);
            }

            ResetHostReference();
            if (failures != null)
            {
                throw new AggregateException("AudioKit host cleanup failed.", failures);
            }
        }
        protected override void OnHostRelease()
        {
            if (_isDisposing)
            {
                return;
            }

            _isDisposing = true;
            List<Exception> failures = null;
            void Cleanup(Action operation)
            {
                try
                {
                    operation();
                }
                catch (Exception exception)
                {
                    (failures ?? (failures = new List<Exception>())).Add(exception);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded -= OnSceneLoaded;
#endif
            foreach (var subscription in _subscriptions)
            {
                Cleanup(subscription.Unregister);
            }

            _subscriptions.Clear();
            foreach (var request in _pending.ToArray())
            {
                Cleanup(() => request.End(AudioPlayEnd.Shutdown));
            }

            _pending.Clear();
            foreach (var channel in new List<AudioChannel>(_namedChannels.Values))
            {
                Cleanup(() => channel.Release(AudioRequestEnd.Shutdown));
            }

            _namedChannels.Clear();
            Cleanup(() => StopAllSound(AudioRequestEnd.Shutdown));
            if (MusicPlayback != null)
            {
                Cleanup(MusicPlayback.DestroySources);
            }

            if (Voice != null)
            {
                Cleanup(Voice.DestroySource);
            }

            if (_pool != null)
            {
                Cleanup(_pool.Dispose);
            }

            Cleanup(ClipCache.Dispose);
            // A callback may release this host while Update is iterating its snapshot.
            if (failures != null)
            {
                throw new AggregateException("AudioKit source cleanup failed.", failures);
            }
        }
        protected override void OnApplicationQuit()
        {
            // 已释放或旧会话宿主的迟到消息不能关闭新会话，亦不能重置公共宿主 gate。
            if (IsHostReleased || !IsCurrentSoundSession)
            {
                return;
            }

            base.OnApplicationQuit();
            AudioKit.BeginQuit();
        }
    }
}
