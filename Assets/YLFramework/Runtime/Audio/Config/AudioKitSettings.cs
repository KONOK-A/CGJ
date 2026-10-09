using System;
using UnityEngine.Audio;
using YLFramework.Binding;

namespace YLFramework.Services.Audio
{
    /// <summary>Persistent channel switches and volume, plus runtime-only mute and mixer routing. Keeps the six existing save keys.</summary>
    public sealed class AudioKitSettings : IDisposable
    {
        /// <summary>持久化的 Sound 通道开关。关闭时停止所有有效声音并拒绝新请求，直到重新启用。</summary>
        public PlayerPrefsBoolProperty IsSoundOn { get; } = new PlayerPrefsBoolProperty("KEY_AUDIO_MANAGER_SOUND_ON", true);
        /// <summary>持久化的 Music 通道开关。关闭时停止 Source 但保留请求；重新启用后从头重播。</summary>
        public PlayerPrefsBoolProperty IsMusicOn { get; } = new PlayerPrefsBoolProperty("KEY_AUDIO_MANAGER_MUSIC_ON", true);
        /// <summary>持久化的 Voice 通道开关。关闭时停止 Source 但保留请求；重新启用后从头重播。</summary>
        public PlayerPrefsBoolProperty IsVoiceOn { get; } = new PlayerPrefsBoolProperty("KEY_AUDIO_MANAGER_VOICE_ON", true);
        /// <summary>Persistent nonnegative, finite Sound multiplier. Defaults to one; zero is silent and values above one are allowed.</summary>
        public PlayerPrefsFloatProperty SoundVolume { get; } = new AudioVolumeProperty("KEY_AUDIO_MANAGER_SOUND_VOLUME");
        /// <summary>Persistent nonnegative, finite Music multiplier. Invalid stored values fall back to one without rewriting storage.</summary>
        public PlayerPrefsFloatProperty MusicVolume { get; } = new AudioVolumeProperty("KEY_AUDIO_MANAGER_MUSIC_VOLUME");
        /// <summary>Persistent nonnegative, finite Voice multiplier. Invalid writes throw before persistence or notification.</summary>
        public PlayerPrefsFloatProperty VoiceVolume { get; } = new AudioVolumeProperty("KEY_AUDIO_MANAGER_VOICE_VOLUME");
        /// <summary>Mute sounds without stopping, rejecting requests or changing their volume. Not persisted; reset on Shutdown.</summary>
        public BindableProperty<bool> IsSoundMuted { get; } = new BindableProperty<bool>(false).WithComparer((a, b) => a == b);
        /// <summary>Mute music without pausing, restarting or changing its volume. Not persisted; reset on Shutdown.</summary>
        public BindableProperty<bool> IsMusicMuted { get; } = new BindableProperty<bool>(false).WithComparer((a, b) => a == b);
        /// <summary>Mute voice without pausing, restarting or changing its volume. Not persisted; reset on Shutdown.</summary>
        public BindableProperty<bool> IsVoiceMuted { get; } = new BindableProperty<bool>(false).WithComparer((a, b) => a == b);
        /// <summary>Default music route. Null means direct output. Changes update inherited requests without restarting.</summary>
        public BindableProperty<AudioMixerGroup> MusicOutputGroup { get; } = new BindableProperty<AudioMixerGroup>().WithComparer((a, b) => a == b);
        /// <summary>Default voice route. Runtime-only; callers keep the group and its mixer alive.</summary>
        public BindableProperty<AudioMixerGroup> VoiceOutputGroup { get; } = new BindableProperty<AudioMixerGroup>().WithComparer((a, b) => a == b);
        /// <summary>Default sound route. Explicit request overrides and direct routes are unaffected.</summary>
        public BindableProperty<AudioMixerGroup> SoundOutputGroup { get; } = new BindableProperty<AudioMixerGroup>().WithComparer((a, b) => a == b);
        /// <summary>仅当三个通道开关都为 true 时为 true；赋值会同时修改三者。</summary>
        public bool IsOn
        {
            get => IsMusicOn.Value && IsVoiceOn.Value && IsSoundOn.Value;
            set { IsMusicOn.Value = value; IsVoiceOn.Value = value; IsSoundOn.Value = value; }
        }
        internal AudioKitSettings()
        {
            SoundVolume.WithComparer((a, b) => a == b);
            MusicVolume.WithComparer((a, b) => a == b);
            VoiceVolume.WithComparer((a, b) => a == b);
        }
        /// <summary>注销全部监听器并清除运行时输出组引用。由 AudioKit.Shutdown 调用。</summary>
        public void Dispose()
        {
            IsSoundOn.UnRegisterAll(); IsMusicOn.UnRegisterAll(); IsVoiceOn.UnRegisterAll();
            SoundVolume.UnRegisterAll(); MusicVolume.UnRegisterAll(); VoiceVolume.UnRegisterAll();
            IsSoundMuted.UnRegisterAll(); IsMusicMuted.UnRegisterAll(); IsVoiceMuted.UnRegisterAll();
            MusicOutputGroup.UnRegisterAll(); VoiceOutputGroup.UnRegisterAll(); SoundOutputGroup.UnRegisterAll();
            MusicOutputGroup.SetValueWithoutCallback(null); VoiceOutputGroup.SetValueWithoutCallback(null); SoundOutputGroup.SetValueWithoutCallback(null);
        }
    }

    internal sealed class AudioVolumeProperty : PlayerPrefsFloatProperty
    {
        internal AudioVolumeProperty(string key) : base(key, 1f) { }

        protected override float Load(float defaultValue)
        {
            var value = base.Load(defaultValue);
            return AudioSoundOptions.IsFinite(value) && value >= 0f ? value : defaultValue;
        }

        protected override void SetValue(float value)
        {
            Validate(value);
            base.SetValue(value);
        }

        public override bool TrySetValue(float value)
        {
            // Validate before a caller-supplied comparer can suppress the write.
            Validate(value);
            return base.TrySetValue(value);
        }

        private static void Validate(float value)
        {
            if (!AudioSoundOptions.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Audio volume must be finite and nonnegative.");
        }
    }
}
