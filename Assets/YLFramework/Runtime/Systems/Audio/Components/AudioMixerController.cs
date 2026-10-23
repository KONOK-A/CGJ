using System;
using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    /// <summary>Inspector and UnityEvent bridge for explicit Mixer routing, parameter writes and snapshot transitions.</summary>
    /// <remarks>Awake only applies routing. Disabling or destroying this component does not clear global routing or Mixer state.</remarks>
    public sealed class AudioMixerController : MonoBehaviour
    {
        [Tooltip("Routing configuration and Mixer used for explicit parameter and snapshot operations.")]
        [SerializeField] private AudioMixerConfig config;
        [Tooltip("Explicitly enable to apply routing in Awake. New Editor components leave global routing unchanged. Does not write parameters or transition snapshots.")]
        [SerializeField] private bool applyOnAwake = true;
        [Tooltip("Explicit exposed parameter name; no automatic binding. Invoke SetParameter/SetVolume in Start or later, not Awake, OnEnable or AfterSceneLoad.")]
        [SerializeField] private string parameterName;
        [Tooltip("Used only by the explicit TransitionToSnapshot event. Must belong to the configured Mixer.")]
        [SerializeField] private AudioMixerSnapshot snapshot;
        [Tooltip("Finite, non-negative transition seconds. New Editor components use 0.5 seconds; zero is an immediate switch. No transition starts automatically.")]
        [SerializeField] private float transitionDuration;
        [Tooltip("Used only by the explicit TransitionToSnapshots event. All snapshots must belong to the configured Mixer.")]
        [SerializeField] private AudioMixerSnapshot[] snapshots;
        [Tooltip("One finite, non-negative weight per snapshot, with a positive total. Forwarded to Unity without normalization.")]
        [SerializeField] private float[] weights;

        private void Reset()
        {
            applyOnAwake = false;
            transitionDuration = .5f;
        }

        private void Awake()
        {
            if (applyOnAwake && config)
            {
                Apply();
            }
        }

        /// <summary>Applies the complete configuration to category routing without writing Mixer parameters.</summary>
        public void Apply()
        {
            if (!config)
            {
                LogError(nameof(Apply), "A live AudioMixerConfig is required.");
                return;
            }
            try
            {
                AudioKit.ApplyMixerConfig(config);
            }
            catch (ArgumentException exception)
            {
                LogError(nameof(Apply), exception.Message);
            }
        }

        /// <summary>Clears category default routing only; explicit playback routes and Mixer parameters remain unchanged.</summary>
        public void ClearRouting()
        {
            AudioKit.ClearMixerRouting();
        }

        /// <summary>Writes the configured exposed parameter. Invoke on the main thread in Start or later.</summary>
        public void SetParameter(float value)
        {
            var mixer = GetMixer(nameof(SetParameter));
            if (!mixer)
            {
                return;
            }

            try
            {
                if (!AudioMixerControl.TrySetFloat(mixer, parameterName, value))
                {
                    LogError(nameof(SetParameter), $"Mixer rejected parameter '{parameterName}' (missing parameter or snapshots being edited).");
                }
            }
            catch (ArgumentException exception)
            {
                LogError(nameof(SetParameter), exception.Message);
            }
        }

        /// <summary>Writes normalized [0,1] volume to the configured dB parameter in Start or later, independently of Settings.</summary>
        public void SetVolume(float linearVolume)
        {
            var mixer = GetMixer(nameof(SetVolume));
            if (!mixer)
            {
                return;
            }

            try
            {
                if (!AudioMixerControl.TrySetVolume(mixer, parameterName, linearVolume))
                {
                    LogError(nameof(SetVolume), $"Mixer rejected parameter '{parameterName}' (missing parameter or snapshots being edited).");
                }
            }
            catch (ArgumentException exception)
            {
                LogError(nameof(SetVolume), exception.Message);
            }
        }

        /// <summary>Releases explicit control of the configured parameter without clearing routing.</summary>
        public void ClearParameter()
        {
            var mixer = GetMixer(nameof(ClearParameter));
            if (!mixer)
            {
                return;
            }

            try
            {
                if (!AudioMixerControl.TryClearFloat(mixer, parameterName))
                {
                    LogError(nameof(ClearParameter), $"Mixer could not clear parameter '{parameterName}'.");
                }
            }
            catch (ArgumentException exception)
            {
                LogError(nameof(ClearParameter), exception.Message);
            }
        }

        /// <summary>Explicitly transitions to the configured snapshot without releasing parameter overrides.</summary>
        public void TransitionToSnapshot()
        {
            var mixer = GetMixer(nameof(TransitionToSnapshot));
            if (!mixer)
            {
                return;
            }

            try
            {
                if (snapshot && snapshot.audioMixer != mixer)
                {
                    throw new ArgumentException("The snapshot must belong to the configured Mixer.", nameof(snapshot));
                }

                AudioMixerControl.TransitionToSnapshot(snapshot, transitionDuration);
            }
            catch (ArgumentException exception)
            {
                LogError(nameof(TransitionToSnapshot), exception.Message);
            }
        }

        /// <summary>Explicitly transitions to the configured snapshot mixture without scheduling or changing the arrays.</summary>
        public void TransitionToSnapshots()
        {
            var mixer = GetMixer(nameof(TransitionToSnapshots));
            if (!mixer)
            {
                return;
            }

            try
            {
                AudioMixerControl.TransitionToSnapshots(mixer, snapshots, weights, transitionDuration);
            }
            catch (ArgumentException exception)
            {
                LogError(nameof(TransitionToSnapshots), exception.Message);
            }
        }

        private AudioMixer GetMixer(string operation)
        {
            if (!config)
            {
                LogError(operation, "A live AudioMixerConfig is required.");
                return null;
            }
            var mixer = config.Mixer;
            if (!mixer)
            {
                LogError(operation, "The configuration must provide a live Mixer for this operation.");
                return null;
            }
            return mixer;
        }

        private void LogError(string operation, string message)
        {
            Debug.LogError($"{nameof(AudioMixerController)}.{operation}: {message}", this);
        }
    }
}
