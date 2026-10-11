using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    // Two fixed leases and an epoch keep callback reentry from controlling a later transition.
    internal sealed class AudioMusicPlayback
    {
        private readonly AudioManager mOwner;
        private readonly AudioVoice mFirst, mSecond;
        private AudioHandle mIncoming, mOutgoing;
        private double mElapsed, mDuration;
        private float mOutgoingGain;
        private bool mTransition, mIgnoreTimeScale;
        private ulong mControlVersion;
        private AudioCrossfadeCurve mCurve;
        internal AudioVoice Current { get; private set; }
        internal AudioHandle Outgoing => mOutgoing.IsValid ? mOutgoing : default;
        internal ulong Epoch { get; private set; }
        internal AudioCrossfadeCurve CurrentCurve => mCurve;

        internal AudioMusicPlayback(AudioManager owner)
        {
            mOwner = owner;
            mFirst = new AudioVoice(owner, AudioVoice.Kind.Music);
            mSecond = new AudioVoice(owner, AudioVoice.Kind.Music);
            Current = mFirst;
        }

        internal AudioHandle Play(AudioClip clip, in AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished, AudioResourceLease resource = null)
        {
            var epoch = Epoch;
            Stop(AudioRequestEnd.Replaced);
            if (!mOwner.Available || Epoch != epoch + 1) return default;
            var settings = AudioKit.Settings;
            return Current.Begin(clip, in options, settings.IsMusicOn.Value, settings.MusicVolume.Value,
                settings.IsMusicMuted.Value, started, finished, resource: resource);
        }

        internal AudioHandle CrossFade(AudioClip clip, float duration, in AudioPlaybackOptions options,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished, bool ignoreTimeScale,
            AudioCrossfadeCurve curve, AudioResourceLease resource = null)
        {
            var settings = AudioKit.Settings;
            if (!settings.IsMusicOn.Value) return Play(clip, in options, started, finished, resource);
            var paused = Current.Active && Current.Paused;
            if (duration == 0f)
            {
                var previousEpoch = Epoch;
                Stop(AudioRequestEnd.Replaced);
                if (!mOwner.Available || Epoch != previousEpoch + 1) return default;
                return Current.Begin(clip, in options, true, settings.MusicVolume.Value, settings.IsMusicMuted.Value,
                    started, finished, paused: paused, resource: resource);
            }
            var epoch = ++Epoch;
            if (mOutgoing.IsValid) mOutgoing.Voice.Stop(AudioRequestEnd.Replaced);
            if (!mOwner.Available || Epoch != epoch) return default;
            mOutgoing = Current.Handle;
            if (mOutgoing.IsValid) Current.SuppressCompletion();
            mOutgoingGain = Current.TransitionGain;
            Current = ReferenceEquals(Current, mFirst) ? mSecond : mFirst;
            mElapsed = 0d; mDuration = duration; mIgnoreTimeScale = ignoreTimeScale; mTransition = true; mCurve = curve;
            try
            {
                var incoming = Current;
                var result = incoming.Begin(clip, in options, true, settings.MusicVolume.Value, settings.IsMusicMuted.Value,
                    started, finished, startImmediately: false, initialTransitionGain: 0f, paused: paused, resource: resource);
                if (Epoch != epoch) return result;
                mIncoming = result;
                incoming.StartConfigured(result.Generation);
                return result;
            }
            catch
            {
                if (Epoch == epoch) Stop(AudioRequestEnd.Failed);
                throw;
            }
        }

        internal void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!mOwner.Available || !mTransition) return;
            var incoming = mIncoming.IsValid;
            var outgoing = mOutgoing.IsValid;
            if (!incoming && !outgoing) { CancelTransition(); return; }
            if (incoming && !mIncoming.Voice.CanAdvanceTransition || outgoing && !mOutgoing.Voice.CanAdvanceTransition) return;
            var delta = mIgnoreTimeScale ? unscaledDeltaTime : deltaTime;
            if (delta <= 0f) return;
            mElapsed = Math.Min(mDuration, mElapsed + delta);
            var progress = (float)(mElapsed / mDuration);
            var incomingGain = progress;
            var outgoingScale = 1f - progress;
            if (mCurve == AudioCrossfadeCurve.EqualPower)
            {
                var angle = progress * Mathf.PI * .5f;
                incomingGain = Mathf.Sin(angle);
                outgoingScale = Mathf.Cos(angle);
            }
            if (incoming) mIncoming.Voice.SetTransitionGain(incomingGain);
            if (outgoing) mOutgoing.Voice.SetTransitionGain(mOutgoingGain * outgoingScale);
            if (mElapsed >= mDuration) CancelTransition();
        }

        private void CancelTransition(AudioRequestEnd reason = AudioRequestEnd.Replaced)
        {
            Epoch++;
            var outgoing = mOutgoing;
            mOutgoing = default; mIncoming = default;
            mTransition = false; mElapsed = mDuration = 0d; mCurve = AudioCrossfadeCurve.Linear;
            if (outgoing.IsValid) outgoing.Voice.Stop(reason);
        }
        internal void Stop(AudioRequestEnd reason = AudioRequestEnd.Stopped)
        {
            var first = mFirst.Handle;
            var second = mSecond.Handle;
            CancelTransition(reason);
            if (first.IsValid) first.Voice.Stop(reason);
            if (second.IsValid) second.Voice.Stop(reason);
        }
        internal void Pause()
        {
            mControlVersion++;
            Current.Handle.Pause();
            mOutgoing.Pause();
        }
        internal void Resume()
        {
            var control = ++mControlVersion;
            var epoch = Epoch;
            var current = Current.Handle;
            var outgoing = mOutgoing;
            current.Resume();
            if (Epoch == epoch && mControlVersion == control) outgoing.Resume();
        }
        internal void StartFailed(AudioHandle failed, ulong epoch)
        {
            if (Epoch != epoch) return;
            if (ReferenceEquals(Current, failed.Voice) && failed.IsValid) Stop(AudioRequestEnd.Failed);
            else if (failed.IsValid) failed.Voice.Stop(AudioRequestEnd.Failed);
        }
        internal void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                var previousEpoch = Epoch;
                var disabling = Current.Handle;
                CancelTransition();
                if (!mOwner.Available || Epoch != previousEpoch + 1 || AudioKit.CurrentSettings.IsMusicOn.Value) return;
                if (disabling.IsValid)
                {
                    disabling.Voice.SetEnabled(false);
                    disabling.Voice.SetTransitionGain(1f);
                }
                return;
            }
            var control = ++mControlVersion;
            var epoch = Epoch;
            var current = Current.Handle;
            var outgoing = mOutgoing;
            if (current.IsValid) current.Voice.SetEnabled(true);
            if (Epoch == epoch && mControlVersion == control && outgoing.IsValid) outgoing.Voice.SetEnabled(true);
        }
        internal void SetVolume(float value)
        {
            Current.SetVolume(value);
            if (mOutgoing.IsValid) mOutgoing.Voice.SetVolume(value);
        }
        internal void SetMute(bool value)
        {
            Current.SetMute(value);
            if (mOutgoing.IsValid) mOutgoing.Voice.SetMute(value);
        }
        internal void SetInheritedOutputGroup(AudioMixerGroup group)
        {
            Current.SetInheritedOutputGroup(group);
            if (mOutgoing.IsValid) mOutgoing.Voice.SetInheritedOutputGroup(group);
        }
        internal void CopyTo(List<AudioHandle> handles)
        {
            if (Current.Active) handles.Add(Current.Handle);
            if (mOutgoing.IsValid) handles.Add(mOutgoing);
        }
        internal void DestroySources()
        {
            try { Stop(AudioRequestEnd.Shutdown); }
            finally
            {
                try { mFirst.DestroySource(); }
                finally { mSecond.DestroySource(); }
            }
        }
    }
}
