using System;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>Optional request envelope bound to clip position, not elapsed wall-clock time. The zero value disables it.</summary>
    [Serializable]
    public struct AudioAutoFade
    {
        /// <summary>Clip seconds for the initial fade-in. Short nonlooping clips proportionally compress both fade regions.</summary>
        public float FadeInSeconds;
        /// <summary>Clip seconds for the final fade-out before natural completion. Must be zero for looping playback.</summary>
        public float FadeOutSeconds;

        /// <summary>Store automatic fade durations. Playback validates them before changing an existing request.</summary>
        /// <param name="fadeInSeconds">Finite, nonnegative clip seconds for the initial fade-in.</param>
        /// <param name="fadeOutSeconds">Finite, nonnegative clip seconds for the final fade-out; zero when looping.</param>
        public AudioAutoFade(float fadeInSeconds, float fadeOutSeconds)
        {
            this.FadeInSeconds = fadeInSeconds;
            this.FadeOutSeconds = fadeOutSeconds;
        }

        internal bool IsEnabled => FadeInSeconds != 0f || FadeOutSeconds != 0f;
        internal void Validate(bool loop)
        {
            if (!AudioSoundOptions.IsFinite(FadeInSeconds) || FadeInSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(FadeInSeconds));
            }

            if (!AudioSoundOptions.IsFinite(FadeOutSeconds) || FadeOutSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(FadeOutSeconds));
            }

            if (loop && FadeOutSeconds > 0f)
            {
                throw new ArgumentException("Looping playback has no final fade-out. Use an initial fade-in and stop it explicitly.", nameof(FadeOutSeconds));
            }
        }

        internal void ValidateClip(AudioClip clip)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (!clip || clip.samples <= 0 || clip.frequency <= 0)
            {
                throw new ArgumentException("Automatic fading requires a live clip with a positive sample count and frequency.", nameof(clip));
            }
        }

        internal void Normalize(double length, bool loop, out double fadeIn, out double fadeOut)
        {
            Validate(loop);
            if (double.IsNaN(length) || double.IsInfinity(length) || length <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            fadeIn = FadeInSeconds;
            fadeOut = FadeOutSeconds;
            if (loop)
            {
                fadeIn = Math.Min(fadeIn, length);
                return;
            }
            var sum = fadeIn + fadeOut;
            if (sum > length)
            {
                var scale = length / sum;
                fadeIn *= scale;
                fadeOut *= scale;
            }
        }

        internal static float Evaluate(double time, double length, double fadeIn, double fadeOut)
        {
            time = Math.Max(0d, Math.Min(length, time));
            var startGain = fadeIn > 0d ? Math.Min(1d, time / fadeIn) : 1d;
            var endGain = fadeOut > 0d ? Math.Min(1d, (length - time) / fadeOut) : 1d;
            return (float)Math.Min(startGain, endGain);
        }
    }
}
