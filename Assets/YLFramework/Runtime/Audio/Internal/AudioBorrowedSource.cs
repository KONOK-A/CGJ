using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    // 不触碰 caller 的层级、启用状态、空间设置或滤镜；每个字段只恢复仍等于本租约最后写入的值。
    internal sealed class AudioBorrowedSource
    {
        internal readonly AudioSource Source;
        private readonly AudioClip mOriginalClip, mClip;
        private readonly bool mOriginalLoop, mOriginalMute;
        private readonly float mOriginalVolume, mOriginalPitch;
        private readonly AudioMixerGroup mOriginalOutput;
        private float mVolume, mPitch;
        private bool mMute, mLoop;
        private AudioMixerGroup mOutput;
        internal bool OwnsPlayback => Source && Source.clip == mClip;
        internal AudioBorrowedSource(AudioSource source, AudioClip clip, in AudioPlaybackOptions options)
        {
            Source = source; mClip = clip; mOriginalClip = source.clip;
            mOriginalLoop = source.loop; mOriginalVolume = source.volume; mOriginalPitch = source.pitch;
            mOriginalMute = source.mute; mOriginalOutput = source.outputAudioMixerGroup;
            source.clip = clip; source.loop = mLoop = options.Loop;
            SetPitch(options.Pitch);
        }
        internal void SetVolume(float value) { if (OwnsPlayback) Source.volume = mVolume = value; }
        internal void SetPitch(float value) { if (OwnsPlayback) Source.pitch = mPitch = value; }
        internal void SetMute(bool value) { if (OwnsPlayback) Source.mute = mMute = value; }
        internal void SetOutput(AudioMixerGroup value) { if (OwnsPlayback) Source.outputAudioMixerGroup = mOutput = value; }
        internal void Release()
        {
            if (!OwnsPlayback) return;
            Source.Stop();
            if (Source.loop == mLoop) Source.loop = mOriginalLoop;
            if (Source.volume == mVolume) Source.volume = mOriginalVolume;
            if (Source.pitch == mPitch) Source.pitch = mOriginalPitch;
            if (Source.mute == mMute) Source.mute = mOriginalMute;
            if (Source.outputAudioMixerGroup == mOutput) Source.outputAudioMixerGroup = mOriginalOutput;
            Source.clip = mOriginalClip ? mOriginalClip : null;
        }
    }
}
