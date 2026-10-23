using System;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    // Request-owned value snapshot; Unity resources remain borrowed references.
    internal struct AudioPlaybackOptions
    {
        internal bool Loop;
        internal float Volume;
        internal float Pitch;
        internal AudioKit.PlaySoundModes? PlaySoundMode;
        internal float MinDistance;
        internal float MaxDistance;
        internal AudioRolloffMode RolloffMode;
        internal AudioOutputRoute OutputRoute;
        internal AudioAutoFade AutoFade;
        internal int Priority;
        internal bool AutoPauseWhenTimeScaleZero;

        // The zero value is only a cleared record, never valid playback defaults.
        internal static AudioPlaybackOptions Defaults => new AudioPlaybackOptions
        {
            Volume = 1f,
            Pitch = 1f,
            MinDistance = 1f,
            MaxDistance = 50f,
            RolloffMode = AudioRolloffMode.Linear
        };
        internal static AudioPlaybackOptions MusicDefaults
        {
            get
            {
                var options = Defaults;
                options.Loop = true;
                return options;
            }
        }
        internal static AudioPlaybackOptions Capture(AudioSoundOptions options, bool music = false)
        {
            if (options == null)
            {
                return music ? MusicDefaults : Defaults;
            }

            return new AudioPlaybackOptions
            {
                Loop = options.Loop,
                Volume = options.Volume,
                Pitch = options.Pitch,
                PlaySoundMode = options.PlaySoundMode,
                MinDistance = options.MinDistance,
                MaxDistance = options.MaxDistance,
                RolloffMode = options.RolloffMode,
                OutputRoute = options.OutputRoute,
                AutoFade = options.AutoFade,
                Priority = options.Priority,
                AutoPauseWhenTimeScaleZero = options.AutoPauseWhenTimeScaleZero
            };
        }
        internal void Validate(bool spatial)
        {
            OutputRoute.Validate();
            AutoFade.Validate(Loop);
            if (Priority < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Priority));
            }

            if (!AudioSoundOptions.IsFinite(Volume) || Volume < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Volume));
            }

            if (!AudioSoundOptions.IsFinite(Pitch) || Pitch <= 0 || Pitch > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(Pitch));
            }

            if (PlaySoundMode.HasValue && (uint)PlaySoundMode.Value > (uint)AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames)
            {
                throw new ArgumentOutOfRangeException(nameof(PlaySoundMode));
            }

            if (!spatial)
            {
                return;
            }

            if (!AudioSoundOptions.IsFinite(MinDistance) || MinDistance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(MinDistance));
            }

            if (!AudioSoundOptions.IsFinite(MaxDistance) || MaxDistance <= 0 || MaxDistance < MinDistance)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxDistance));
            }

            if (RolloffMode != AudioRolloffMode.Linear && RolloffMode != AudioRolloffMode.Logarithmic)
            {
                throw new ArgumentOutOfRangeException(nameof(RolloffMode));
            }
        }
        internal void ValidateClip(AudioClip clip) => AutoFade.ValidateClip(clip);
    }
}
