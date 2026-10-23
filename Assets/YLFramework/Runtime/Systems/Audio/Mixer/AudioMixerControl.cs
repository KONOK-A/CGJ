using System;
using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    /// <summary>Explicit native Mixer control without AudioKit initialization, implicit targets or transition queues.</summary>
    /// <remarks>
    /// Call on Unity's main thread. Call SetFloat/SetVolume in Start or later, not Awake, OnEnable or
    /// AfterSceneLoad initialization callbacks. Parameter writes override snapshot control until explicitly cleared.
    /// </remarks>
    public static class AudioMixerControl
    {
        private const float MinimumDecibels = -80f;

        /// <summary>Sets an exposed parameter. Returns the native result; invalid arguments throw.</summary>
        public static bool TrySetFloat(AudioMixer mixer, string parameterName, float value)
        {
            ValidateParameter(mixer, parameterName);
            ValidateFinite(value, nameof(value));
            return mixer.SetFloat(parameterName, value);
        }

        /// <summary>Reads an exposed parameter. A native failure returns false and sets value to zero.</summary>
        public static bool TryGetFloat(AudioMixer mixer, string parameterName, out float value)
        {
            ValidateParameter(mixer, parameterName);
            if (mixer.GetFloat(parameterName, out value))
            {
                return true;
            }

            value = 0f;
            return false;
        }

        /// <summary>Releases explicit control of an exposed parameter without changing output routing.</summary>
        public static bool TryClearFloat(AudioMixer mixer, string parameterName)
        {
            ValidateParameter(mixer, parameterName);
            return mixer.ClearFloat(parameterName);
        }

        /// <summary>Writes normalized [0,1] volume as dB to the specified exposed parameter, without changing Settings.</summary>
        public static bool TrySetVolume(AudioMixer mixer, string parameterName, float linearVolume)
        {
            return TrySetFloat(mixer, parameterName, LinearVolumeToDecibels(linearVolume));
        }

        /// <summary>Reads a dB parameter as normalized volume. A native failure returns false and sets linearVolume to zero.</summary>
        public static bool TryGetVolume(AudioMixer mixer, string parameterName, out float linearVolume)
        {
            linearVolume = 0f;
            if (!TryGetFloat(mixer, parameterName, out var value))
            {
                return false;
            }

            linearVolume = DecibelsToLinearVolume(value);
            return true;
        }

        /// <summary>Converts finite [0,1] volume to dB, with a -80 dB floor. The floor is not an absolute silence guarantee.</summary>
        public static float LinearVolumeToDecibels(float linearVolume)
        {
            ValidateFinite(linearVolume, nameof(linearVolume));
            if (linearVolume < 0f || linearVolume > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(linearVolume), "Volume must be in [0,1].");
            }

            return linearVolume == 0f ? MinimumDecibels : Mathf.Max(MinimumDecibels, 20f * Mathf.Log10(linearVolume));
        }

        /// <summary>Converts finite dB to normalized [0,1] volume; values at or below -80 dB map to zero.</summary>
        public static float DecibelsToLinearVolume(float decibels)
        {
            ValidateFinite(decibels, nameof(decibels));
            return decibels <= MinimumDecibels ? 0f : Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
        }

        /// <summary>Transitions immediately through the snapshot's native Mixer; no scheduling or parameter release is performed.</summary>
        public static void TransitionToSnapshot(AudioMixerSnapshot snapshot, float duration)
        {
            if (!snapshot)
            {
                throw new ArgumentException("A live snapshot is required.", nameof(snapshot));
            }

            ValidateMixer(snapshot.audioMixer, nameof(snapshot));
            ValidateDuration(duration);
            snapshot.TransitionTo(duration);
        }

        /// <summary>Transitions to a weighted mixture of snapshots belonging to mixer. Arrays are forwarded unchanged.</summary>
        public static void TransitionToSnapshots(AudioMixer mixer, AudioMixerSnapshot[] snapshots, float[] weights, float duration)
        {
            ValidateMixer(mixer, nameof(mixer));
            ValidateDuration(duration);
            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            if (snapshots.Length == 0)
            {
                throw new ArgumentException("At least one snapshot is required.", nameof(snapshots));
            }

            if (snapshots.Length != weights.Length)
            {
                throw new ArgumentException("Snapshot and weight arrays must have the same length.", nameof(weights));
            }

            double totalWeight = 0d;
            for (var i = 0; i < snapshots.Length; i++)
            {
                if (!snapshots[i])
                {
                    throw new ArgumentException($"Snapshot at index {i} must be live.", nameof(snapshots));
                }

                if (snapshots[i].audioMixer != mixer)
                {
                    throw new ArgumentException($"Snapshot at index {i} must belong to the supplied Mixer.", nameof(snapshots));
                }

                ValidateFinite(weights[i], nameof(weights));
                if (weights[i] < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be non-negative.");
                }

                totalWeight += weights[i];
            }
            if (totalWeight <= 0d)
            {
                throw new ArgumentException("Weights must have a positive total.", nameof(weights));
            }

            mixer.TransitionToSnapshots(snapshots, weights, duration);
        }

        private static void ValidateMixer(AudioMixer mixer, string parameterName)
        {
            if (!mixer)
            {
                throw new ArgumentException("A live Mixer is required.", parameterName);
            }
        }

        private static void ValidateParameter(AudioMixer mixer, string parameterName)
        {
            ValidateMixer(mixer, nameof(mixer));
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("An exposed parameter name is required.", nameof(parameterName));
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "The value must be finite.");
            }
        }

        private static void ValidateDuration(float duration)
        {
            ValidateFinite(duration, nameof(duration));
            if (duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be non-negative.");
            }
        }
    }
}
