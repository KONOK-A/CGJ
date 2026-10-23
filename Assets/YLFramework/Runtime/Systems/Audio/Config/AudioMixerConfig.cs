using System;
using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    /// <summary>Optional category routing configuration. Apply explicitly; editing this asset does not update active routing.</summary>
    [CreateAssetMenu(fileName = "AudioMixerConfig", menuName = "YLFramework/Audio/Mixer Config")]
    public sealed class AudioMixerConfig : ScriptableObject
    {
        [Tooltip("Required when any output group is assigned. Does not bind exposed parameters automatically.")]
        [SerializeField] private AudioMixer mixer;
        [Tooltip("Leave empty for direct output. Assigned groups must belong to this configuration's Mixer.")]
        [SerializeField] private AudioMixerGroup musicGroup;
        [Tooltip("Leave empty for direct output. Assigned groups must belong to this configuration's Mixer.")]
        [SerializeField] private AudioMixerGroup voiceGroup;
        [Tooltip("Leave empty for direct output. Assigned groups must belong to this configuration's Mixer.")]
        [SerializeField] private AudioMixerGroup soundGroup;

        /// <summary>The native Mixer, which may be empty when all category groups are empty.</summary>
        public AudioMixer Mixer => mixer;
        /// <summary>The default music output group; null means direct output.</summary>
        public AudioMixerGroup MusicGroup => musicGroup;
        /// <summary>The default voice output group; null means direct output.</summary>
        public AudioMixerGroup VoiceGroup => voiceGroup;
        /// <summary>The default sound output group; null means direct output.</summary>
        public AudioMixerGroup SoundGroup => soundGroup;

        internal void Validate()
        {
            if (AudioOutputRoute.HasReference(mixer) && !mixer)
            {
                throw new ArgumentException("The configured Mixer has been destroyed.", nameof(mixer));
            }

            ValidateGroup(musicGroup, nameof(musicGroup));
            ValidateGroup(voiceGroup, nameof(voiceGroup));
            ValidateGroup(soundGroup, nameof(soundGroup));
        }

        private void ValidateGroup(AudioMixerGroup group, string parameterName)
        {
            if (!AudioOutputRoute.HasReference(group))
            {
                return;
            }

            if (!group)
            {
                throw new ArgumentException("The configured output group has been destroyed.", parameterName);
            }

            if (!mixer)
            {
                throw new ArgumentException("An output group requires a configured Mixer.", parameterName);
            }

            if (group.audioMixer != mixer)
            {
                throw new ArgumentException("The output group must belong to the configured Mixer.", parameterName);
            }
        }
    }
}
