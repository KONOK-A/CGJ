using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>A playback lease. Controls on an expired or recycled lease are safe no-ops.</summary>
    public readonly struct AudioHandle
    {
        internal readonly AudioVoice Voice;
        internal readonly ulong Generation;
        internal AudioHandle(AudioVoice voice) { Voice = voice; Generation = voice.Generation; }
        /// <summary>该 lease 是否仍指向可用宿主上的有效请求；过期或已被回收的 lease 均为无效。</summary>
        public bool IsValid => Voice != null && Voice.IsValid(Generation);
        /// <summary>底层 AudioSource 当前是否正在播放。静音时仍为 true，因此不能作为能否听见的依据。</summary>
        public bool IsPlaying => IsValid && Voice.Source && Voice.Source.isPlaying;
        /// <summary>该请求是否被显式暂停。等待分类通道重新启用不计为暂停。</summary>
        public bool IsPaused => IsValid && Voice.Paused;
        /// <summary>Current clip position in seconds; zero for expired or not-yet-started requests. Pausing freezes it, muting does not.</summary>
        /// <remarks>Loops report the current cycle. Compressed audio inherits AudioSource.time packet precision; no completed history is retained.</remarks>
        public float Time => IsValid ? Voice.PlaybackTime : 0f;
        /// <summary>Clip length in seconds, including valid requests waiting for a disabled channel. Unavailable or expired requests return zero.</summary>
        public float Duration => IsValid ? Voice.Duration : 0f;
        /// <summary>Current-cycle position in [0,1], or zero for unavailable/expired requests. Natural completion callbacks do not guarantee a value of one.</summary>
        public float Progress
        {
            get
            {
                var duration = Duration;
                return duration > 0f ? Mathf.Clamp01(Time / duration) : 0f;
            }
        }
        /// <summary>暂停播放；会冻结 Time、淡变包络，以及该 lease 参与的音乐 Crossfade 进度。</summary>
        public void Pause() { if (IsValid) Voice.Pause(); }
        /// <summary>恢复播放。等待分类通道重新启用的请求会保持停止，直到该通道恢复启用。</summary>
        public void Resume() { if (IsValid) Voice.Resume(); }
        /// <summary>停止并释放该请求，不触发其自然完成回调。</summary>
        public void Stop() { if (IsValid) Voice.Stop(); }
        /// <summary>Change this request's volume multiplier without restarting or changing channel volume. Expired handles are ignored.</summary>
        /// <param name="volumeScale">A finite, nonnegative multiplier. Source volume is clamped after multiplying by current channel volume.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">The handle is valid and the multiplier is negative or nonfinite.</exception>
        public void SetVolumeScale(float volumeScale) { if (IsValid) Voice.SetVolumeScale(volumeScale); }
        /// <summary>Fade this request's independent envelope to full gain. If it has not faded before, a positive fade-in starts at silence; otherwise it continues from current gain.</summary>
        /// <param name="duration">Finite, nonnegative seconds. Zero restores full gain immediately. Expired handles are ignored.</param>
        /// <param name="ignoreTimeScale">Use unscaled time by default. Explicit pause and waiting for an enabled channel always freeze the fade; mute does not.</param>
        /// <remarks>Replaces this request's previous fade without changing Settings, request volume, clip, route or lease. This is frame-based, not DSP-scheduled.</remarks>
        public void FadeIn(float duration, bool ignoreTimeScale = true) { if (IsValid) Voice.FadeIn(duration, ignoreTimeScale); }
        /// <summary>Fade this request's envelope to silence, optionally stopping only this lease without invoking natural completion.</summary>
        /// <param name="duration">Finite, nonnegative seconds. Zero reaches silence immediately. Expired handles are ignored.</param>
        /// <param name="stopWhenDone">Stop and release this request after the fade; false keeps it active at zero envelope gain.</param>
        /// <param name="ignoreTimeScale">Use unscaled time by default. Explicit pause and waiting for an enabled channel always freeze the fade; mute does not.</param>
        /// <remarks>A new fade replaces the old fade and continues from current gain. Stop, replacement, natural completion and Shutdown discard all fade state.</remarks>
        public void FadeOut(float duration, bool stopWhenDone = true, bool ignoreTimeScale = true)
        {
            if (IsValid) Voice.FadeOut(duration, stopWhenDone, ignoreTimeScale);
        }
        /// <summary>Change this request's pitch without restarting or changing its paused state. Expired handles are ignored.</summary>
        /// <param name="pitch">A finite pitch in (0, 3].</param>
        /// <exception cref="System.ArgumentOutOfRangeException">The handle is valid and pitch is nonfinite or outside (0, 3].</exception>
        public void SetPitch(float pitch) { if (IsValid) Voice.SetPitch(pitch); }
    }
}
