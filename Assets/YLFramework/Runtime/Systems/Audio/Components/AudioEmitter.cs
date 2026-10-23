using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace YLFramework.Services.Audio
{
    /// <summary>序列化的音效所有者：从候选 Clip 中选取一个（均匀或加权随机）并通过 AudioKit 播放。</summary>
    /// <remarks>组件停用时，循环和跟随播放的请求会被停止，2D 与固定位置的 one-shot 继续播放。选曲与校验会在替换自有循环播放之前完成。</remarks>
    public class AudioEmitter : MonoBehaviour
    {
        /// <summary>每次播放的声音在空间中的定位方式。</summary>
        public enum PositionMode
        {
            /// <summary>2D 播放；忽略空间距离和跟随目标。</summary>
            TwoDimensional,
            /// <summary>3D 播放，位置取每次 Play 时该组件 Transform 的世界坐标。</summary>
            FixedPosition,
            /// <summary>3D 播放，跟随 followTarget；followTarget 为空时跟随该组件的 Transform。</summary>
            FollowTransform
        }

        [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
        [SerializeField] private bool useWeightedRandom = false;
        [SerializeField] private List<AudioWeightedClip> weightedClips = new List<AudioWeightedClip>();
        [Tooltip("Request volume multiplier: 0 is silent, 1 is unchanged; values above 1 are allowed.")]
        [SerializeField] private float volume = 1f;
        [SerializeField] private float pitch = 1f;
        [Tooltip("Sound admission priority: 0 is ordinary; higher values are more important. Not Unity AudioSource.priority.")]
        [SerializeField, Min(0)] private int priority = 0;
        [SerializeField] private bool loop;
        [Tooltip("Play once in the first Start, not on every enable. Disabling stops owned loops and following sounds, but not fixed/2D one-shots.")]
        [SerializeField] private bool playOnAwake;
        [Tooltip("Inherit AudioKit.PlaySoundMode. Reset enables this for newly added Editor components; missing fields in old assets keep the explicit mode.")]
        [SerializeField] private bool useGlobalPlaySoundMode = false;
        [SerializeField] private AudioKit.PlaySoundModes playSoundMode = AudioKit.PlaySoundModes.EveryOne;
        [SerializeField] private PositionMode positionMode;
        [Tooltip("Leave empty to follow this component's Transform.")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 50f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
        [Tooltip("Inherit the Sound default, override with a mixer group, or use Direct output. Only MixerGroup accepts a Group.")]
        [SerializeField] private AudioOutputRoute outputRoute;
        [Tooltip("Fade durations use clip seconds. Short clips compress both fades proportionally. Looping clips cannot use a tail fade.")]
        [SerializeField] private AudioAutoFade autoFade;
        [SerializeField] private UnityEvent onStarted = new UnityEvent();

        /// <summary>Invoked once after each accepted sound starts and its lease is tracked by this emitter.</summary>
        public UnityEvent Started => onStarted;

        private struct Playback
        {
            public AudioHandle Handle;
            public bool StopOnDisable;
        }

        private readonly List<Playback> _playbacks = new List<Playback>();
        private Action<AudioClip, AudioHandle> _startedCallback;

        private void Reset()
        {
            useGlobalPlaySoundMode = true;
        }

        private void Start()
        {
            if (playOnAwake)
            {
                Play();
            }
        }

        /// <summary>按该组件的配置选曲并播放。选曲与校验会在替换自有循环播放之前完成。</summary>
        public void Play()
        {
            var options = AudioPlaybackOptions.Defaults;
            options.Loop = loop;
            options.Volume = volume;
            options.Pitch = pitch;
            options.Priority = priority;
            options.PlaySoundMode = useGlobalPlaySoundMode ? (AudioKit.PlaySoundModes?)null : playSoundMode;
            options.MinDistance = minDistance;
            options.MaxDistance = maxDistance;
            options.RolloffMode = rolloffMode;
            options.OutputRoute = outputRoute;
            options.AutoFade = autoFade;
            if ((uint)positionMode > (uint)PositionMode.FollowTransform)
            {
                throw new ArgumentOutOfRangeException(nameof(positionMode));
            }

            options.Validate(positionMode != PositionMode.TwoDimensional);

            // Choose once and validate all input before replacing an owned loop.
            AudioClip selectedClip;
            if (!(useWeightedRandom
                    ? AudioKit.TrySelectWeightedRandomClip(weightedClips, out selectedClip)
                    : AudioKit.TrySelectRandomClip(clips, out selectedClip)))
            {
                return;
            }

            options.ValidateClip(selectedClip);
            var target = AudioOutputRoute.HasReference(followTarget) ? followTarget : transform;
            if (positionMode == PositionMode.FollowTransform && (!target || !target.gameObject.activeInHierarchy))
            {
                return;
            }

            var position = positionMode == PositionMode.FollowTransform ? target.position : transform.position;
            if (positionMode != PositionMode.TwoDimensional &&
                (!AudioSoundOptions.IsFinite(position.x) || !AudioSoundOptions.IsFinite(position.y) || !AudioSoundOptions.IsFinite(position.z)))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            if (loop)
            {
                Stop();
            }

            for (var i = _playbacks.Count - 1; i >= 0; i--)
            {
                if (!_playbacks[i].Handle.IsValid)
                {
                    _playbacks.RemoveAt(i);
                }
            }
            if (_startedCallback == null)
            {
                _startedCallback = OnPlaybackStarted;
            }

            _ = AudioKit.PlaySoundWithSnapshot(selectedClip, options, onStarted: _startedCallback,
                spatial: positionMode != PositionMode.TwoDimensional,
                position: positionMode == PositionMode.TwoDimensional ? Vector3.zero : position,
                target: positionMode == PositionMode.FollowTransform ? target : null);
        }

        private void OnPlaybackStarted(AudioClip clip, AudioHandle handle)
        {
            _playbacks.Add(new Playback
            {
                Handle = handle,
                StopOnDisable = handle.Voice.Options.Loop || handle.Voice.IsFollowing
            });
            try
            {
                onStarted.Invoke();
            }
            catch
            {
                handle.Stop();
                RemovePlayback(handle);
                throw;
            }
            if (!handle.IsValid)
            {
                RemovePlayback(handle);
            }
        }

        private void RemovePlayback(AudioHandle handle)
        {
            for (var i = _playbacks.Count - 1; i >= 0; i--)
            {
                var owned = _playbacks[i].Handle;
                if (ReferenceEquals(owned.Voice, handle.Voice) && owned.Generation == handle.Generation)
                {
                    _playbacks.RemoveAt(i);
                }
            }
        }

        /// <summary>停止该组件仍在追踪的所有声音，并清空追踪列表。</summary>
        public void Stop()
        {
            foreach (var playback in _playbacks)
            {
                playback.Handle.Stop();
            }

            _playbacks.Clear();
        }

        private void OnDisable()
        {
            // UI and fixed-position one-shots outlive the object that triggered them.
            for (var i = _playbacks.Count - 1; i >= 0; i--)
            {
                var playback = _playbacks[i];
                if (playback.StopOnDisable)
                {
                    playback.Handle.Stop();
                }

                if (playback.StopOnDisable || !playback.Handle.IsValid)
                {
                    _playbacks.RemoveAt(i);
                }
            }
        }
    }
}
