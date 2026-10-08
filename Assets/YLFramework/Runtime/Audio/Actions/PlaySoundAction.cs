// Copyright (c) 2015 - 2025 liangxiegame, MIT License.
// https://github.com/liangxiegame/QFramework
using System;
using UnityEngine;
using YLFramework.Actions;

namespace YLFramework.Services.Audio
{
    /// <summary>Play a borrowed clip and wait for its request to end. Cancellation stops only this lease.</summary>
    public sealed class PlaySoundAction : AbstractAction<PlaySoundAction>, IActionExecutionVersion
    {
        private AudioClip mClip;
        private AudioHandle mHandle;
        private AudioPlaybackOptions mOptions;
        private Action mOnFinish;
        private Action<AudioClip, AudioHandle> mOnStarted;
        private ulong mVersion;
        ulong IActionExecutionVersion.ExecutionVersion => mVersion;
        private bool mNotified;
        /// <summary>Allocate a sound action using default request options and optional output routing.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for playback.</param>
        /// <param name="onFinish">Action completion notification; cancellation does not invoke it.</param>
        /// <param name="outputRoute">Optional inherited, explicit mixer group or direct output routing.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; cancellation and Reset are safe before Play returns.</param>
        public static PlaySoundAction Allocate(AudioClip clip, Action onFinish = null, AudioOutputRoute outputRoute = default,
            Action<AudioClip, AudioHandle> onStarted = null)
        {
            outputRoute.Validate();
            var options = AudioPlaybackOptions.Defaults;
            options.OutputRoute = outputRoute;
            var action = Allocate(); action.mClip = clip; action.mOnFinish = onFinish; action.mOptions = options; action.mOnStarted = onStarted; return action;
        }
        /// <summary>Allocate a sound action with complete request options, including automatic clip-time fades.</summary>
        /// <param name="clip">Caller-owned clip that must remain alive for playback; a missing clip is rejected when the action starts.</param>
        /// <param name="options">Configuration copied at allocation; null uses Defaults.</param>
        /// <param name="onFinish">Action completion notification; cancellation does not invoke it.</param>
        /// <param name="onStarted">Actual clip and original lease after first start; cancellation and Reset are safe before Play returns.</param>
        public static PlaySoundAction AllocateWithOptions(AudioClip clip, AudioSoundOptions options, Action onFinish = null,
            Action<AudioClip, AudioHandle> onStarted = null)
        {
            var captured = AudioPlaybackOptions.Capture(options);
            captured.Validate(false);
            if (clip) captured.ValidateClip(clip);
            var action = Allocate(); action.mClip = clip; action.mOnFinish = onFinish; action.mOptions = captured; action.mOnStarted = onStarted; return action;
        }
        public override void OnStart()
        {
            var id = ActionID;
            var version = mVersion;
            var onStarted = mOnStarted;
            var handle = AudioKit.PlaySoundWithSnapshot(mClip, mOptions, onFinished: (clip, finished) =>
            {
                if (Deinited || ActionID != id || mVersion != version) return;
                mHandle = default;
                this.Finish();
            }, onStarted: onStarted == null ? null : (clip, started) =>
            {
                if (Deinited || ActionID != id || mVersion != version) { started.Stop(); return; }
                mHandle = started;
                // A direct recursive Execute must not start this execution twice; Reset still starts a new version.
                Status = ActionStatus.Started;
                try { onStarted(clip, started); }
                catch
                {
                    started.Stop();
                    if (!Deinited && ActionID == id && mVersion == version &&
                        ReferenceEquals(mHandle.Voice, started.Voice) && mHandle.Generation == started.Generation)
                    {
                        mHandle = default;
                        Status = ActionStatus.NotStart;
                    }
                    throw;
                }
                if (Deinited || ActionID != id || mVersion != version) started.Stop();
            });
            if (Deinited || ActionID != id || mVersion != version) { handle.Stop(); return; }
            mHandle = handle;
            CheckPlayback();
        }
        public override void OnExecute(float dt) => CheckPlayback();
        private void CheckPlayback() { if (!mHandle.IsValid) this.Finish(); }
        public override void OnFinish()
        {
            if (mNotified) return;
            mNotified = true;
            mOnFinish?.Invoke();
        }
        protected override void OnReset() { mVersion++; mNotified = false; mHandle.Stop(); mHandle = default; }
        protected override void OnDeinit() { OnReset(); mClip = null; mOnFinish = null; mOnStarted = null; mOptions = default; }
    }
    /// <summary>Append a Clip-only sound request to an ActionKit sequence.</summary>
    public static class PlaySoundExtension
    {
        /// <summary>Append a sound action using default request options and optional output routing.</summary>
        /// <param name="self">Sequence receiving the sound action.</param>
        /// <param name="clip">Caller-owned clip that must remain alive for playback.</param>
        /// <param name="outputRoute">Optional inherited, explicit mixer group or direct output routing.</param>
        /// <param name="onStarted">Actual clip and original lease after first start.</param>
        public static ISequence PlaySound(this ISequence self, AudioClip clip, AudioOutputRoute outputRoute = default,
            Action<AudioClip, AudioHandle> onStarted = null) =>
            self.Append(PlaySoundAction.Allocate(clip, outputRoute: outputRoute, onStarted: onStarted));
        /// <summary>Append a sound action with complete request options, including automatic clip-time fades.</summary>
        /// <param name="self">Sequence receiving the sound action.</param>
        /// <param name="clip">Caller-owned clip that must remain alive for playback.</param>
        /// <param name="options">Configuration copied at allocation; null uses Defaults.</param>
        /// <param name="onStarted">Actual clip and original lease after first start.</param>
        public static ISequence PlaySoundWithOptions(this ISequence self, AudioClip clip, AudioSoundOptions options,
            Action<AudioClip, AudioHandle> onStarted = null) =>
            self.Append(PlaySoundAction.AllocateWithOptions(clip, options, onStarted: onStarted));
    }
}
