using System;
using UnityEngine;
using YLFramework.Actions;

namespace YLFramework.Audio
{
    /// <summary>Play a borrowed clip and wait for its request to end. Cancellation stops only this lease.</summary>
    public sealed class PlaySoundAction : AbstractAction<PlaySoundAction>, IActionExecutionVersion
    {
        private AudioClip _clip;
        private AudioHandle _handle;
        private AudioPlaybackOptions _options;
        private Action _onFinish;
        private Action<AudioClip, AudioHandle> _onStarted;
        private ulong _version;
        ulong IActionExecutionVersion.ExecutionVersion => _version;
        private bool _hasNotified;
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
            var action = Allocate();
            action._clip = clip;
            action._onFinish = onFinish;
            action._options = options;
            action._onStarted = onStarted;
            return action;
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
            if (clip)
            {
                captured.ValidateClip(clip);
            }

            var action = Allocate();
            action._clip = clip;
            action._onFinish = onFinish;
            action._options = captured;
            action._onStarted = onStarted;
            return action;
        }
        public override void OnStart()
        {
            var id = ActionId;
            var version = _version;
            var onStarted = _onStarted;
            var handle = AudioKit.PlaySoundWithSnapshot(_clip, _options, onFinished: (clip, finished) =>
            {
                if (IsDeinitialized || ActionId != id || _version != version)
                {
                    return;
                }

                _handle = default;
                this.Finish();
            }, onStarted: onStarted == null ? null : (clip, started) =>
            {
                if (IsDeinitialized || ActionId != id || _version != version)
                {
                    started.Stop();
                    return;
                }
                _handle = started;
                // A direct recursive Execute must not start this execution twice; Reset still starts a new version.
                Status = ActionStatus.Started;
                try
                {
                    onStarted(clip, started);
                }
                catch
                {
                    started.Stop();
                    if (!IsDeinitialized && ActionId == id && _version == version &&
                        ReferenceEquals(_handle.Voice, started.Voice) && _handle.Generation == started.Generation)
                    {
                        _handle = default;
                        Status = ActionStatus.NotStart;
                    }
                    throw;
                }
                if (IsDeinitialized || ActionId != id || _version != version)
                {
                    started.Stop();
                }
            });
            if (IsDeinitialized || ActionId != id || _version != version)
            {
                handle.Stop();
                return;
            }
            _handle = handle;
            CheckPlayback();
        }
        public override void OnExecute(float dt) => CheckPlayback();
        private void CheckPlayback()
        {
            if (!_handle.IsValid)
            {
                this.Finish();
            }
        }
        public override void OnFinish()
        {
            if (_hasNotified)
            {
                return;
            }

            _hasNotified = true;
            _onFinish?.Invoke();
        }
        protected override void OnReset()
        {
            _version++;
            _hasNotified = false;
            _handle.Stop();
            _handle = default;
        }
        protected override void OnDeinit()
        {
            OnReset();
            _clip = null;
            _onFinish = null;
            _onStarted = null;
            _options = default;
        }
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
