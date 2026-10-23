using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    // 不触碰 caller 的层级、启用状态、空间设置或滤镜；每个字段只恢复仍等于本租约最后写入的值。
    internal sealed class AudioBorrowedSource
    {
        internal readonly AudioSource Source;
        private readonly AudioClip _originalClip;
        private readonly AudioClip _clip;
        private readonly bool _originalLoop;
        private readonly bool _originalMute;
        private readonly float _originalVolume;
        private readonly float _originalPitch;
        private readonly AudioMixerGroup _originalOutput;
        private float _volume;
        private float _pitch;
        private bool _mute;
        private bool _loop;
        private AudioMixerGroup _output;
        internal bool OwnsPlayback => Source && Source.clip == _clip;
        internal AudioBorrowedSource(AudioSource source, AudioClip clip, in AudioPlaybackOptions options)
        {
            Source = source;
            _clip = clip;
            _originalClip = source.clip;
            _originalLoop = source.loop;
            _originalVolume = source.volume;
            _originalPitch = source.pitch;
            _originalMute = source.mute;
            _originalOutput = source.outputAudioMixerGroup;
            source.clip = clip;
            source.loop = _loop = options.Loop;
            SetPitch(options.Pitch);
        }
        internal void SetVolume(float value)
        {
            if (OwnsPlayback)
            {
                Source.volume = _volume = value;
            }
        }
        internal void SetPitch(float value)
        {
            if (OwnsPlayback)
            {
                Source.pitch = _pitch = value;
            }
        }
        internal void SetMute(bool value)
        {
            if (OwnsPlayback)
            {
                Source.mute = _mute = value;
            }
        }
        internal void SetOutput(AudioMixerGroup value)
        {
            if (OwnsPlayback)
            {
                Source.outputAudioMixerGroup = _output = value;
            }
        }
        internal void Release()
        {
            if (!OwnsPlayback)
            {
                return;
            }

            Source.Stop();
            if (Source.loop == _loop)
            {
                Source.loop = _originalLoop;
            }

            if (Source.volume == _volume)
            {
                Source.volume = _originalVolume;
            }

            if (Source.pitch == _pitch)
            {
                Source.pitch = _originalPitch;
            }

            if (Source.mute == _mute)
            {
                Source.mute = _originalMute;
            }

            if (Source.outputAudioMixerGroup == _output)
            {
                Source.outputAudioMixerGroup = _originalOutput;
            }

            Source.clip = _originalClip ? _originalClip : null;
        }
    }
}
