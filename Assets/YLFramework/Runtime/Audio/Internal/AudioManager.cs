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
        private readonly Dictionary<int, int> mClipCounts = new Dictionary<int, int>();
        private readonly List<int> mClipChecks = new List<int>();
        private readonly List<AudioHandle> mChecks = new List<AudioHandle>();
        private readonly List<IUnRegister> mSubscriptions = new List<IUnRegister>(12);
        private bool mDisposing;
        private ulong mAdmissionOrder;
        private ulong mSoundSession;
        internal bool Available => !HostReleased && !mDisposing && !AudioKit.IsClosing;
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
                return GetInstance(true);
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
            mSubscriptions.Add(settings.IsVoiceOn.Register(Voice.SetEnabled));
            mSubscriptions.Add(settings.IsSoundOn.Register(value => { if (!value) StopAllSound(); }));
            mSubscriptions.Add(settings.MusicVolume.Register(RefreshMusicVolume));
            mSubscriptions.Add(settings.VoiceVolume.Register(Voice.SetVolume));
            mSubscriptions.Add(settings.SoundVolume.Register(SetSoundVolume));
            mSubscriptions.Add(settings.IsMusicMuted.Register(RefreshMusicMute));
            mSubscriptions.Add(settings.IsVoiceMuted.Register(Voice.SetMute));
            mSubscriptions.Add(settings.IsSoundMuted.Register(SetSoundMute));
            mSubscriptions.Add(settings.MusicOutputGroup.Register(RefreshMusicOutputGroup));
            mSubscriptions.Add(settings.VoiceOutputGroup.Register(RefreshVoiceOutputGroup));
            mSubscriptions.Add(settings.SoundOutputGroup.Register(RefreshSoundOutputGroup));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SceneManager.sceneLoaded += OnSceneLoaded;
            CheckListener();
#endif
        }
        internal AudioHandle PlaySlot(bool music, AudioClip clip, in AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool spatial = false, Vector3 position = default, Transform target = null)
        {
            if (music) return MusicPlayback.Play(clip, in options, started, finished);
            var slot = Voice;
            slot.Stop();
            var settings = AudioKit.Settings;
            return slot.Begin(clip, in options, settings.IsVoiceOn.Value, settings.VoiceVolume.Value,
                settings.IsVoiceMuted.Value, started, finished, spatial, position, target);
        }
        internal AudioHandle PlaySound(AudioClip clip, in AudioPlaybackOptions options, AudioKit.PlaySoundModes mode,
            Action<AudioClip, AudioHandle> finished, Action<AudioClip, AudioHandle> started, bool spatial, Vector3 position, Transform target)
        {
            var capacity = AudioKit.CurrentSoundConcurrency;
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
                victim.Stop();
                // Stop can synchronously change other counts; discard the cached budget lookup.
                clipCount = -1;
                AudioKit.SoundCounters.StolenCount++;
            }
            if (mPool.CurStoreCount > 0) AudioKit.SoundCounters.CacheHitCount++;
            else AudioKit.SoundCounters.CacheMissCount++;
            var voice = mPool.Allocate();
            mSounds.Add(voice);
            mClipCounts[clipId] = (clipCount >= 0 ? clipCount : GetSoundCount(clipId)) + 1;
            voice.ClipId = clipId;
            voice.Priority = options.Priority;
            voice.AdmissionOrder = ++mAdmissionOrder;
            voice.Mode = mode;
            var generation = voice.Generation + 1;
            var channels = AudioKit.Channels;
            AudioHandle handle = default;
            try
            {
                var settings = AudioKit.Settings;
                handle = voice.Begin(clip, in options, true, settings.SoundVolume.Value, settings.IsSoundMuted.Value,
                    started, finished, spatial, position, target, startImmediately: false);
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
                if (voice.Generation == generation) voice.Stop();
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
            if (mDisposing || AudioKit.SoundCacheCapacity == 0 || !voice.Source) mPool.Discard(voice);
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
            foreach (var voice in mSounds) voice.SetVolume(value);
        }
        private void SetSoundMute(bool value)
        {
            foreach (var voice in mSounds) voice.SetMute(value);
        }
        internal float GetSharedVolume(AudioVoice voice)
        {
            var settings = AudioKit.Settings;
            return voice.IsSound ? settings.SoundVolume.Value :
                voice.Category == AudioVoice.Kind.Music ? settings.MusicVolume.Value : settings.VoiceVolume.Value;
        }
        private void RefreshMusicEnabled(bool _) { if (Available) MusicPlayback.SetEnabled(AudioKit.CurrentSettings.IsMusicOn.Value); }
        private void RefreshMusicVolume(float _) { if (Available) MusicPlayback.SetVolume(AudioKit.CurrentSettings.MusicVolume.Value); }
        private void RefreshMusicMute(bool _) { if (Available) MusicPlayback.SetMute(AudioKit.CurrentSettings.IsMusicMuted.Value); }
        // A listener can synchronously change the property again or release this host before our turn.
        private void RefreshMusicOutputGroup(AudioMixerGroup _)
        {
            if (Available) MusicPlayback.SetInheritedOutputGroup(AudioKit.CurrentSettings.MusicOutputGroup.Value);
        }
        private void RefreshVoiceOutputGroup(AudioMixerGroup _)
        {
            if (Available) Voice.SetInheritedOutputGroup(AudioKit.CurrentSettings.VoiceOutputGroup.Value);
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
        internal void StopAllSound()
        {
            // Stop never calls user callbacks, so removing one entry at a time is safe and allocation-free.
            while (mSounds.Count != 0)
            {
                AudioVoice voice;
                using (var iterator = mSounds.GetEnumerator()) { iterator.MoveNext(); voice = iterator.Current; }
                voice.Stop();
            }
        }
        private void Update()
        {
            if (Music == null || !Available) return;
            mChecks.Clear();
            var musicEpoch = MusicPlayback.Epoch;
            MusicPlayback.CopyTo(mChecks);
            if (Voice.Active) mChecks.Add(Voice.Handle);
            CopySounds(mChecks);
            for (var i = 0; i < mChecks.Count; i++)
            {
                var handle = mChecks[i];
                if (!handle.IsValid) continue;
                try
                {
                    handle.Voice.CheckFinished();
                    if (handle.IsValid) handle.Voice.TickFade(Time.deltaTime, Time.unscaledDeltaTime);
                }
                catch (Exception exception) { Debug.LogException(exception); }
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
            Cleanup(StopAllSound);
            if (MusicPlayback != null) Cleanup(MusicPlayback.DestroySources);
            if (Voice != null) Cleanup(Voice.DestroySource);
            if (mPool != null) Cleanup(mPool.Dispose);
            // A callback may release this host while Update is iterating its snapshot.
            if (failures != null) throw new AggregateException("AudioKit source cleanup failed.", failures);
        }
        protected override void OnApplicationQuit() { base.OnApplicationQuit(); AudioKit.BeginQuit(); }
    }
}
