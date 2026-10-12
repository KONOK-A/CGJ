using System;
using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    internal sealed class AudioVoice
    {
        private readonly AudioManager mOwner;
        internal enum Kind { Sound, Music, Voice }
        internal readonly Kind Category;
        internal bool IsSound => Category == Kind.Sound;
        internal readonly bool PooledSound;
        internal ulong AdmissionOrder;
        internal int ClipId;
        internal int Priority;
        internal AudioClip Clip => mClip;
        internal float TransitionGain { get; private set; } = 1f;
        internal bool CanAdvanceTransition => Active && mStarted && mEnabled && !Paused && mClip && mClip.loadState == AudioDataLoadState.Loaded;
        internal bool IsFollowing => mFollowing;
        internal AudioSource Source { get; private set; }
        internal ulong Generation { get; private set; }
        internal bool Active { get; private set; }
        internal bool Paused => mManualPaused || mAutomaticPaused || mChannelPaused;
        private bool mManualPaused, mAutomaticPaused, mChannelPaused;
        internal bool ManuallyPaused => mManualPaused;
        internal AudioChannel Channel { get; private set; }
        private AudioBorrowedSource mBorrowed;
        internal float Duration => Source && mClip && Source.clip ? mClip.length : 0f;
        internal float PlaybackTime => mStarted && Source && mClip && Source.clip ? Mathf.Clamp(Source.time, 0f, Duration) : 0f;
        internal AudioHandle Handle => Active ? new AudioHandle(this) : default;
        private AudioPlaybackOptions mOptions;
        internal AudioPlaybackOptions Options => mOptions;
        internal AudioKit.PlaySoundModes Mode;
        private AudioClip mClip;
        private Transform mTarget;
        private Vector3 mPosition;
        private bool mSpatial, mFollowing, mEnabled, mStarted;
        private bool mOwnsSource;
        private GameObject mSourceRoot;
        private float mFadeGain = 1f, mFadeFrom = 1f, mFadeTarget = 1f, mFadeDuration;
        private double mFadeElapsed;
        private bool mFadeActive, mFadeTouched, mFadeStopWhenDone, mFadeIgnoreTimeScale = true;
        private bool mAutoFadeActive, mAutoLoopComplete;
        private double mAutoLength, mAutoFadeIn, mAutoFadeOut, mAutoPreviousTime;
        private Action<AudioClip, AudioHandle> mOnStarted, mOnFinished;
        private AudioResourceLease mResource;
        internal AudioRequestEnd EndReason { get; private set; }
        internal AudioVoice(AudioManager owner, Kind category, bool pooledSound = true) { mOwner = owner; Category = category; PooledSound = pooledSound; }
        internal bool IsValid(ulong generation) => Active && Generation == generation && mOwner && mOwner.Available;

        internal AudioHandle Begin(AudioClip clip, in AudioPlaybackOptions options, bool enabled, float sharedVolume, bool muted,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool spatial = false, Vector3 position = default, Transform target = null,
            bool startImmediately = true, float initialTransitionGain = 1f, bool paused = false,
            AudioResourceLease resource = null, AudioChannel channel = null, AudioSource borrowedSource = null)
        {
            if (!mOwner.Available) throw new InvalidOperationException("Audio host is unavailable.");
            resource?.Bind();
            mResource = resource;
            Generation++;
            EndReason = AudioRequestEnd.None;
            Active = true; mManualPaused = paused; mStarted = false; mEnabled = enabled;
            Channel = channel;
            mChannelPaused = channel != null && channel.IsPaused;
            mAutomaticPaused = options.AutoPauseWhenTimeScaleZero && Time.timeScale == 0f;
            mClip = clip; mOptions = options; mOnStarted = started; mOnFinished = finished;
            var generation = Generation;
            try
            {
                ResetFade();
                TransitionGain = initialTransitionGain;
                mAutoFadeActive = options.AutoFade.IsEnabled;
                if (mAutoFadeActive)
                {
                    mAutoLength = clip.samples / (double)clip.frequency;
                    options.AutoFade.Normalize(mAutoLength, options.Loop, out mAutoFadeIn, out mAutoFadeOut);
                    ResetAutoPosition();
                    mFadeTouched = true;
                }
                mTarget = target; mFollowing = target;
                mSpatial = spatial; mPosition = position;
                if (borrowedSource)
                {
                    mOwner.BorrowSource(borrowedSource, this);
                    mBorrowed = new AudioBorrowedSource(borrowedSource, clip, in options);
                    Source = borrowedSource;
                }
                else
                {
                    EnsureSource();
                    ResetSource();
                    Source.clip = clip; Source.loop = options.Loop; Source.pitch = options.Pitch;
                    Source.spatialBlend = spatial ? 1f : 0f;
                }
                SetOutput(options.OutputRoute.Resolve(mOwner.GetOutputGroup(this)));
                if (spatial && mBorrowed == null)
                {
                    Source.minDistance = options.MinDistance; Source.maxDistance = options.MaxDistance;
                    Source.rolloffMode = options.RolloffMode;
                }
                if (spatial) Source.transform.position = position;
                SetVolume(sharedVolume);
                SetMute(muted);
                var handle = new AudioHandle(this);
                if (mBorrowed == null) Source.gameObject.SetActive(true);
                if (IsValid(generation) && enabled && startImmediately) StartSource();
                return handle;
            }
            catch
            {
                if (Active && Generation == generation) Stop(AudioRequestEnd.Failed);
                throw;
            }
        }
        internal void SetVolume(float sharedVolume)
        {
            if (!Active || !Source) return;
            var value = Mathf.Clamp01(sharedVolume * Options.Volume) * mFadeGain * TransitionGain;
            if (mBorrowed != null) mBorrowed.SetVolume(value);
            else Source.volume = value;
        }
        internal void SetTransitionGain(float value)
        {
            TransitionGain = Mathf.Clamp01(value);
            SetVolume(mOwner.GetSharedVolume(this));
        }
        internal void SuppressCompletion() => mOnFinished = null;
        internal void StartConfigured(ulong generation) { if (IsValid(generation)) StartSource(); }
        internal void SetMute(bool value)
        {
            if (!Active || !Source) return;
            if (mBorrowed != null) mBorrowed.SetMute(value);
            else Source.mute = value;
            // Unmuting must restore the latest channel volume and request scale, not a stale Unity snapshot.
            SetVolume(mOwner.GetSharedVolume(this));
        }
        internal void SetInheritedOutputGroup(AudioMixerGroup group)
        {
            if (Active && Source && Options.OutputRoute.Mode == AudioOutputRoute.RouteMode.Inherit)
                SetOutput(group ? group : null);
        }
        private void SetOutput(AudioMixerGroup group)
        {
            if (mBorrowed != null) mBorrowed.SetOutput(group);
            else if (Source) Source.outputAudioMixerGroup = group;
        }
        internal void SetVolumeScale(float volumeScale)
        {
            if (!AudioSoundOptions.IsFinite(volumeScale) || volumeScale < 0) throw new ArgumentOutOfRangeException(nameof(volumeScale));
            mOptions.Volume = volumeScale;
            SetVolume(mOwner.GetSharedVolume(this));
        }
        internal void SetPitch(float pitch)
        {
            if (!AudioSoundOptions.IsFinite(pitch) || pitch <= 0 || pitch > 3) throw new ArgumentOutOfRangeException(nameof(pitch));
            mOptions.Pitch = pitch;
            if (mBorrowed != null) mBorrowed.SetPitch(pitch);
            else if (Source) Source.pitch = pitch;
        }
        internal void FadeIn(float duration, bool ignoreTimeScale) => BeginFade(1f, duration, false, ignoreTimeScale, true);
        internal void FadeOut(float duration, bool stopWhenDone, bool ignoreTimeScale) => BeginFade(0f, duration, stopWhenDone, ignoreTimeScale, false);
        private void BeginFade(float target, float duration, bool stopWhenDone, bool ignoreTimeScale, bool fromSilence)
        {
            if (!AudioSoundOptions.IsFinite(duration) || duration < 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            if (mAutoFadeActive)
            {
                RefreshAutoFade();
                mAutoFadeActive = false;
                mFadeTouched = true;
            }
            if (fromSilence && !mFadeTouched && duration > 0f) mFadeGain = 0f;
            mFadeTouched = true;
            mFadeFrom = mFadeGain; mFadeTarget = target; mFadeDuration = duration; mFadeElapsed = 0d;
            mFadeStopWhenDone = stopWhenDone; mFadeIgnoreTimeScale = ignoreTimeScale;
            mFadeActive = duration > 0f;
            if (!mFadeActive) mFadeGain = target;
            SetVolume(mOwner.GetSharedVolume(this));
            if (!mFadeActive && stopWhenDone) Stop();
        }
        internal void TickFade(float deltaTime, float unscaledDeltaTime)
        {
            if (!Active || !mStarted || Paused || !mEnabled || !mClip || mClip.loadState != AudioDataLoadState.Loaded) return;
            if (mAutoFadeActive) { RefreshAutoFade(); return; }
            if (!mFadeActive) return;
            var delta = mFadeIgnoreTimeScale ? unscaledDeltaTime : deltaTime;
            if (delta <= 0f) return;
            mFadeElapsed = Math.Min(mFadeDuration, mFadeElapsed + delta);
            mFadeGain = Mathf.Lerp(mFadeFrom, mFadeTarget, (float)(mFadeElapsed / mFadeDuration));
            mFadeActive = mFadeElapsed < mFadeDuration;
            SetVolume(mOwner.GetSharedVolume(this));
            if (!mFadeActive && mFadeStopWhenDone) Stop();
        }
        private void ResetFade()
        {
            mFadeGain = mFadeFrom = mFadeTarget = 1f; mFadeDuration = 0f; mFadeElapsed = 0d;
            mFadeActive = mFadeTouched = mFadeStopWhenDone = false; mFadeIgnoreTimeScale = true;
            mAutoFadeActive = mAutoLoopComplete = false;
            mAutoLength = mAutoFadeIn = mAutoFadeOut = mAutoPreviousTime = 0d;
            TransitionGain = 1f;
        }
        private void ResetAutoPosition()
        {
            mAutoPreviousTime = 0d;
            mAutoLoopComplete = false;
            mFadeGain = mAutoFadeIn > 0d ? 0f : 1f;
        }
        private void RefreshAutoFade()
        {
            if (!Active || !mStarted || Paused || !mEnabled || !Source || !mClip || !Source.isPlaying) return;
            if (Options.Loop && mAutoLoopComplete) return;
            var position = Math.Max(0d, Math.Min(mAutoLength, Source.timeSamples / (double)mClip.frequency));
            if (Options.Loop && (position >= mAutoFadeIn || position < mAutoPreviousTime)) mAutoLoopComplete = true;
            mAutoPreviousTime = position;
            mFadeGain = mAutoLoopComplete ? 1f : AudioAutoFade.Evaluate(position, mAutoLength, mAutoFadeIn, mAutoFadeOut);
            SetVolume(mOwner.GetSharedVolume(this));
        }
        private void StartSource()
        {
            if (!Active || Paused || !mEnabled) return;
            if (!Source || !Source.isActiveAndEnabled || !mClip || mBorrowed != null && !mBorrowed.OwnsPlayback ||
                mFollowing && (!mTarget || !mTarget.gameObject.activeInHierarchy)) { Stop(AudioRequestEnd.Unavailable); return; }
            if (mClip.loadState == AudioDataLoadState.Failed) { Stop(AudioRequestEnd.Failed); return; }
            if (mFollowing) Source.transform.position = mTarget.position;
            else if (mSpatial) Source.transform.position = mPosition;
            SetOutput(Options.OutputRoute.Resolve(mOwner.GetOutputGroup(this)));
            if (mAutoFadeActive)
            {
                ResetAutoPosition();
                SetVolume(mOwner.GetSharedVolume(this));
            }
            var generation = Generation;
            var clip = mClip;
            var handle = Handle;
            var musicEpoch = Category == Kind.Music && Channel == null ? mOwner.MusicPlayback.Epoch : 0;
            Source.Play(); mStarted = true;
            var callback = mOnStarted; mOnStarted = null;
            try { callback?.Invoke(clip, handle); }
            catch
            {
                if (Category == Kind.Music && Channel == null) mOwner.MusicPlayback.StartFailed(handle, musicEpoch);
                else if (Active && Generation == generation) Stop(AudioRequestEnd.Failed);
                throw;
            }
        }
        internal void SetEnabled(bool value)
        {
            if (!Active || mEnabled == value) return;
            mEnabled = value;
            if (!value) { if (Source && (mBorrowed == null || mBorrowed.OwnsPlayback)) Source.Stop(); mStarted = false; }
            else StartSource();
        }
        internal void Pause()
        {
            SetPauseReason(ref mManualPaused, true);
        }
        internal void Resume()
        {
            SetPauseReason(ref mManualPaused, false);
        }
        internal void SetChannelPaused(bool value) => SetPauseReason(ref mChannelPaused, value);
        internal void RefreshAutomaticPause(bool timeScaleZero) => SetPauseReason(ref mAutomaticPaused, Options.AutoPauseWhenTimeScaleZero && timeScaleZero);
        private void SetPauseReason(ref bool reason, bool value)
        {
            if (!Active || reason == value) return;
            var wasPaused = Paused;
            if (!wasPaused && value && mAutoFadeActive) RefreshAutoFade();
            reason = value;
            if (wasPaused == Paused) return;
            if (Paused)
            {
                if (mStarted && Source && (mBorrowed == null || mBorrowed.OwnsPlayback)) Source.Pause();
                return;
            }
            if (!mEnabled) return;
            if (mStarted && Source && (mBorrowed == null || mBorrowed.OwnsPlayback)) Source.UnPause();
            else StartSource();
        }
        internal void CheckFinished()
        {
            if (!Active) return;
            if (!Source || !mClip || Source.clip != mClip || !Source.isActiveAndEnabled ||
                mFollowing && (!mTarget || !mTarget.gameObject.activeInHierarchy)) { Stop(AudioRequestEnd.Unavailable); return; }
            var loadState = mClip.loadState;
            if (loadState == AudioDataLoadState.Failed) { Stop(AudioRequestEnd.Failed); return; }
            if (mStarted && loadState == AudioDataLoadState.Unloaded) { Stop(AudioRequestEnd.Unavailable); return; }
            // Play 可由 Unity 延后到背景数据就绪；不能把等待中的 !isPlaying 当自然结束。
            if (loadState != AudioDataLoadState.Loaded) return;
            if (mFollowing) Source.transform.position = mTarget.position;
            else if (mSpatial) Source.transform.position = mPosition;
            if (!mStarted || Paused || !mEnabled || Source.loop || Source.isPlaying) return;
            var generation = Generation;
            var clip = mClip;
            var handle = Handle;
            if (mAutoFadeActive)
            {
                mFadeGain = AudioAutoFade.Evaluate(mAutoLength, mAutoLength, mAutoFadeIn, mAutoFadeOut);
                SetVolume(mOwner.GetSharedVolume(this));
            }
            if (IsSound) AudioKit.Channels.Finished(clip.GetInstanceID(), handle);
            var callback = mOnFinished; mOnFinished = null;
            // 回调可停止并复用同一 Voice；旧资源保活到原自然通知返回，之后只释放局部责任。
            var resource = mResource; mResource = null;
            try { callback?.Invoke(clip, handle); }
            finally
            {
                try { if (Active && Generation == generation) Stop(AudioRequestEnd.Completed); }
                finally { resource?.Dispose(); }
            }
        }
        internal void Stop(AudioRequestEnd reason = AudioRequestEnd.Stopped)
        {
            if (!Active) return;
            EndReason = reason;
            Active = false; Generation++;
            var resource = mResource; mResource = null;
            var channel = Channel;
            channel?.Remove(this);
            // Sound 先由 owner 摘除计数，再由池 reset 清租用数据；固定槽不进入 Sound 池。
            try
            {
                if (IsSound) mOwner.Recycle(this);
                else if (channel != null) mOwner.ReleaseChannelVoice(this);
                else ResetPlayback();
            }
            finally { resource?.Dispose(); }
        }
        internal void ResetPlayback()
        {
            mManualPaused = mAutomaticPaused = mChannelPaused = false; mStarted = mEnabled = false;
            mOnStarted = mOnFinished = null; mClip = null; mTarget = null; mFollowing = false;
            mSpatial = false; mPosition = Vector3.zero;
            mOptions = default;
            ResetFade();
            ClipId = Priority = 0;
            Channel = null;
            if (mBorrowed != null)
            {
                var borrowed = mBorrowed; mBorrowed = null; Source = null;
                try { borrowed.Release(); }
                finally { mOwner.ReturnBorrowedSource(borrowed.Source, this); }
            }
            else
            {
                ResetSource();
                if (Source) Source.gameObject.SetActive(false);
            }
        }
        private void ResetSource()
        {
            if (!Source) return;
            Source.Stop(); Source.clip = null; Source.playOnAwake = false;
            Source.enabled = true;
            Source.outputAudioMixerGroup = null;
            Source.loop = false; Source.volume = 1f; Source.pitch = 1f; Source.mute = false;
            Source.spatialBlend = 0f; Source.minDistance = 1f; Source.maxDistance = 50f;
            Source.rolloffMode = AudioRolloffMode.Linear; Source.dopplerLevel = 0f;
            Source.transform.localPosition = Vector3.zero;
        }
        internal void EnsureSource()
        {
            if (Source) return;
            // The component may be destroyed independently; keep ownership of its GameObject until released.
            ReleaseSource();
            var root = new GameObject(IsSound ? "Sound" : "Slot");
            mSourceRoot = root;
            root.SetActive(false);
            try
            {
                root.transform.SetParent(mOwner.transform, false);
                Source = root.AddComponent<AudioSource>();
                mOwnsSource = true;
                mOwner.RegisterOwnedSource(Source);
                if (IsSound) mOwner.SoundSourceCreated();
                ResetSource();
            }
            catch
            {
                ReleaseSource();
                throw;
            }
        }
        internal void DestroySource()
        {
            try { Stop(AudioRequestEnd.Shutdown); }
            finally { ReleaseSource(); }
        }
        private void ReleaseSource()
        {
            var root = mSourceRoot;
            mSourceRoot = null;
            if (mOwnsSource) mOwner.UnregisterOwnedSource(Source);
            Source = null;
            if (mOwnsSource)
            {
                mOwnsSource = false;
                if (IsSound) mOwner.SoundSourceReleased();
            }
            if (!root) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
