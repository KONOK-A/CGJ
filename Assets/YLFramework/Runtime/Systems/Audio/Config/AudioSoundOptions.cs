using System;
using UnityEngine;

namespace YLFramework.Audio
{
    /// <summary>Mutable playback configuration with valid constructor defaults. Playback captures a value snapshot; Sound deduplication applies only to sounds.</summary>
    [Serializable]
    public sealed class AudioSoundOptions
    {
        /// <summary>Clip 结束后是否重复播放；请求会持续到被停止或替换。</summary>
        public bool Loop;
        /// <summary>Nonnegative request multiplier: zero is silent, one is unchanged, values above one are allowed.</summary>
        public float Volume;
        /// <summary>Playback pitch in (0,3]; omitted values use one. Explicit zero is invalid.</summary>
        public float Pitch;
        /// <summary>可选的单请求 Sound 去重覆盖；null 表示继承 AudioKit.PlaySoundMode。Music 和 Voice 会忽略该字段。</summary>
        public AudioKit.PlaySoundModes? PlaySoundMode;
        /// <summary>3D 衰减起始距离；仅空间播放的请求会校验该值。</summary>
        public float MinDistance;
        /// <summary>3D 衰减结束距离；必须为正数且不小于 MinDistance，仅空间播放的请求会校验该值。</summary>
        public float MaxDistance;
        /// <summary>3D 衰减模式；空间播放仅接受 Linear 和 Logarithmic。</summary>
        public AudioRolloffMode RolloffMode;
        /// <summary>Inherit the category default, override it with a group, or bypass mixer routing.</summary>
        public AudioOutputRoute OutputRoute;
        /// <summary>Optional clip-position envelope; default zero durations preserve existing playback behavior.</summary>
        public AudioAutoFade AutoFade;
        /// <summary>Sound admission priority: nonnegative, with larger values more important. Ignored by Music and Voice; not Unity AudioSource.priority.</summary>
        public int Priority;
        /// <summary>显式启用 timeScale == 0 时的局部自动暂停。默认 false；解除自动暂停不解除手动或通道暂停。</summary>
        public bool AutoPauseWhenTimeScaleZero;
        /// <summary>Create a nonlooping configuration with valid playback defaults.</summary>
        public AudioSoundOptions()
        {
            var defaults = AudioPlaybackOptions.Defaults;
            Loop = defaults.Loop;
            Volume = defaults.Volume;
            Pitch = defaults.Pitch;
            PlaySoundMode = defaults.PlaySoundMode;
            MinDistance = defaults.MinDistance;
            MaxDistance = defaults.MaxDistance;
            RolloffMode = defaults.RolloffMode;
            OutputRoute = defaults.OutputRoute;
            AutoFade = defaults.AutoFade;
            Priority = defaults.Priority;
        }
        /// <summary>A fresh nonlooping configuration with valid playback defaults.</summary>
        public static AudioSoundOptions Defaults => new AudioSoundOptions();
        /// <summary>A fresh looping configuration for background music.</summary>
        public static AudioSoundOptions MusicDefaults => new AudioSoundOptions { Loop = true };
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal void Validate(bool spatial) => AudioPlaybackOptions.Capture(this).Validate(spatial);
        internal void ValidateClip(AudioClip clip) => AutoFade.ValidateClip(clip);
    }
}
