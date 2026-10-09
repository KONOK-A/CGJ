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
        internal ulong AdmissionOrder;
        internal int ClipId;
        internal int Priority;
        internal AudioClip Clip => mClip;
        internal float TransitionGain { get; private set; } = 1f;
        internal bool CanAdvanceTransition => Active && mStarted && mEnabled && !Paused;
        internal bool IsFollowing => mFollowing;
        internal AudioSource Source { get; private set; }
        internal ulong Generation { get; private set; }
        internal bool Active { get; private set; }
        internal bool Paused { get; private set; }
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
        internal AudioVoice(AudioManager owner, Kind category) { mOwner = owner; Category = category; }
        internal bool IsValid(ulong generation) => Active && Generation == generation && mOwner && mOwner.Available;

        internal AudioHandle Begin(AudioClip clip, in AudioPlaybackOptions options, bool enabled, float sharedVolume, bool muted,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool spatial = false, Vector3 position = default, Transform target = null,
            bool startImmediately = true, float initialTransitionGain = 1f, bool paused = false)
        {
            Generation++;
            Active = true; Paused = paused; mStarted = false; mEnabled = enabled;
            mClip = clip; mOptions = options; mOnStarted = started; mOnFinished = finished;
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
            EnsureSource();
            ResetSource();
            Source.outputAudioMixerGroup = options.OutputRoute.Resolve(mOwner.GetOutputGroup(this));
            Source.clip = clip; Source.loop = options.Loop; Source.pitch = options.Pitch;
            Source.spatialBlend = spatial ? 1f : 0f;
            if (spatial)
            {
                Source.minDistance = options.MinDistance; Source.maxDistance = options.MaxDistance;
                Source.rolloffMode = options.RolloffMode;
            }
            if (spatial) Source.transform.position = position;
            SetVolume(sharedVolume);
            Source.mute = muted;
            Source.gameObject.SetActive(true);
            var handle = new AudioHandle(this);
            if (enabled && startImmediately) StartSource();
            return handle;
        }
        internal void SetVolume(float sharedVolume)
        {
            if (Active && Source) Source.volume = Mathf.Clamp01(sharedVolume * Options.Volume) * mFadeGain * TransitionGain;
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
            Source.mute = value;
            // Unmuting must restore the latest channel volume and request scale, not a stale Unity snapshot.
            SetVolume(mOwner.GetSharedVolume(this));
        }
        internal void SetInheritedOutputGroup(AudioMixerGroup group)
        {
            if (Active && Source && Options.OutputRoute.Mode == AudioOutputRoute.RouteMode.Inherit)
                Source.outputAudioMixerGroup = group ? group : null;
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
            if (Source) Source.pitch = pitch;
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
            if (!Active || !mStarted || Paused || !mEnabled) return;
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
            if (!Source || !mClip || mFollowing && (!mTarget || !mTarget.gameObject.activeInHierarchy)) { Stop(); return; }
            if (mFollowing) Source.transform.position = mTarget.position;
            else if (mSpatial) Source.transform.position = mPosition;
            Source.outputAudioMixerGroup = Options.OutputRoute.Resolve(mOwner.GetOutputGroup(this));
            if (mAutoFadeActive)
            {
                ResetAutoPosition();
                SetVolume(mOwner.GetSharedVolume(this));
            }
            var generation = Generation;
            var clip = mClip;
            var handle = Handle;
            var musicEpoch = Category == Kind.Music ? mOwner.MusicPlayback.Epoch : 0;
            Source.Play(); mStarted = true;
            var callback = mOnStarted; mOnStarted = null;
            try { callback?.Invoke(clip, handle); }
            catch
            {
                if (Category == Kind.Music) mOwner.MusicPlayback.StartFailed(handle, musicEpoch);
                else if (Active && Generation == generation) Stop();
                throw;
            }
        }
        internal void SetEnabled(bool value)
        {
            if (!Active || mEnabled == value) return;
            mEnabled = value;
            if (!value) { if (Source) Source.Stop(); mStarted = false; }
            else StartSource();
        }
        internal void Pause()
        {
            if (!Active || Paused) return;
            if (mAutoFadeActive) RefreshAutoFade();
            Paused = true;
            if (mStarted && Source) Source.Pause();
        }
        internal void Resume()
        {
            if (!Active || !Paused) return;
            Paused = false;
            if (!mEnabled) return;
            if (mStarted && Source) Source.UnPause();
            else StartSource();
        }
        internal void CheckFinished()
        {
            if (!Active) return;
            if (!Source || !mClip || mFollowing && (!mTarget || !mTarget.gameObject.activeInHierarchy)) { Stop(); return; }
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
            try { callback?.Invoke(clip, handle); }
            finally { if (Active && Generation == generation) Stop(); }
        }
        internal void Stop()
        {
            if (!Active) return;
            Active = false; Generation++;
            // Sound 先由 owner 摘除计数，再由池 reset 清租用数据；固定槽不进入 Sound 池。
            if (IsSound) mOwner.Recycle(this);
            else ResetPlayback();
        }
        internal void ResetPlayback()
        {
            Paused = false; mStarted = mEnabled = false;
            mOnStarted = mOnFinished = null; mClip = null; mTarget = null; mFollowing = false;
            mSpatial = false; mPosition = Vector3.zero;
            mOptions = default;
            ResetFade();
            ResetSource();
            if (Source) Source.gameObject.SetActive(false);
            ClipId = Priority = 0;
        }
        private void ResetSource()
        {
            if (!Source) return;
            Source.Stop(); Source.clip = null; Source.playOnAwake = false;
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
            Stop();
            ReleaseSource();
        }
        private void ReleaseSource()
        {
            var root = mSourceRoot;
            mSourceRoot = null;
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
