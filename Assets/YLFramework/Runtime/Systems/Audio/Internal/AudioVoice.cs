using System;
using UnityEngine;
using UnityEngine.Audio;

namespace YLFramework.Services.Audio
{
    internal sealed class AudioVoice
    {
        private readonly AudioManager _owner;
        internal enum Kind
        {
            Sound,
            Music,
            Voice
        }
        internal readonly Kind Category;
        internal bool IsSound => Category == Kind.Sound;
        internal readonly bool PooledSound;
        internal ulong AdmissionOrder;
        internal int ClipId;
        internal int Priority;
        internal AudioClip Clip => _clip;
        internal float TransitionGain { get; private set; } = 1f;
        internal bool CanAdvanceTransition => IsActive && _hasStarted && _isEnabled && !IsPaused && _clip && _clip.loadState == AudioDataLoadState.Loaded;
        internal bool IsFollowing => _isFollowing;
        internal AudioSource Source
        {
            get; private set;
        }
        internal ulong Generation
        {
            get; private set;
        }
        internal bool IsActive
        {
            get; private set;
        }
        internal bool IsPaused => _isManuallyPaused || _isAutomaticallyPaused || _isChannelPaused;
        private bool _isManuallyPaused;
        private bool _isAutomaticallyPaused;
        private bool _isChannelPaused;
        internal bool IsManuallyPaused => _isManuallyPaused;
        internal AudioChannel Channel
        {
            get; private set;
        }
        private AudioBorrowedSource _borrowed;
        internal float Duration => Source && _clip && Source.clip ? _clip.length : 0f;
        internal float PlaybackTime => _hasStarted && Source && _clip && Source.clip ? Mathf.Clamp(Source.time, 0f, Duration) : 0f;
        internal AudioHandle Handle => IsActive ? new AudioHandle(this) : default;
        private AudioPlaybackOptions _options;
        internal AudioPlaybackOptions Options => _options;
        internal AudioKit.PlaySoundModes Mode;
        private AudioClip _clip;
        private Transform _target;
        private Vector3 _position;
        private bool _spatial;
        private bool _isFollowing;
        private bool _isEnabled;
        private bool _hasStarted;
        private bool _ownsSource;
        private GameObject _sourceRoot;
        private float _fadeGain = 1f;
        private float _fadeFrom = 1f;
        private float _fadeTarget = 1f;
        private float _fadeDuration;
        private double _fadeElapsed;
        private bool _isFading;
        private bool _hasTouchedFade;
        private bool _fadeStopWhenDone;
        private bool _fadeIgnoreTimeScale = true;
        private bool _isAutoFadeActive;
        private bool _hasCompletedAutoLoop;
        private double _autoLength;
        private double _autoFadeIn;
        private double _autoFadeOut;
        private double _autoPreviousTime;
        private Action<AudioClip, AudioHandle> _onStarted;
        private Action<AudioClip, AudioHandle> _onFinished;
        private AudioResourceLease _resource;
        internal AudioRequestEnd EndReason
        {
            get; private set;
        }
        internal AudioVoice(AudioManager owner, Kind category, bool pooledSound = true)
        {
            _owner = owner;
            Category = category;
            PooledSound = pooledSound;
        }
        internal bool IsValid(ulong generation) => IsActive && Generation == generation && _owner && _owner.IsAvailable;

        internal AudioHandle Begin(AudioClip clip, in AudioPlaybackOptions options, bool enabled, float sharedVolume, bool muted,
            Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool spatial = false, Vector3 position = default, Transform target = null,
            bool startImmediately = true, float initialTransitionGain = 1f, bool paused = false,
            AudioResourceLease resource = null, AudioChannel channel = null, AudioSource borrowedSource = null)
        {
            if (!_owner.IsAvailable)
            {
                throw new InvalidOperationException("Audio host is unavailable.");
            }

            resource?.Bind();
            _resource = resource;
            Generation++;
            EndReason = AudioRequestEnd.None;
            IsActive = true;
            _isManuallyPaused = paused;
            _hasStarted = false;
            _isEnabled = enabled;
            Channel = channel;
            _isChannelPaused = channel != null && channel.IsPaused;
            _isAutomaticallyPaused = options.AutoPauseWhenTimeScaleZero && Time.timeScale == 0f;
            _clip = clip;
            _options = options;
            _onStarted = started;
            _onFinished = finished;
            var generation = Generation;
            try
            {
                ResetFade();
                TransitionGain = initialTransitionGain;
                _isAutoFadeActive = options.AutoFade.IsEnabled;
                if (_isAutoFadeActive)
                {
                    _autoLength = clip.samples / (double)clip.frequency;
                    options.AutoFade.Normalize(_autoLength, options.Loop, out _autoFadeIn, out _autoFadeOut);
                    ResetAutoPosition();
                    _hasTouchedFade = true;
                }
                _target = target;
                _isFollowing = target;
                _spatial = spatial;
                _position = position;
                if (borrowedSource)
                {
                    _owner.BorrowSource(borrowedSource, this);
                    _borrowed = new AudioBorrowedSource(borrowedSource, clip, in options);
                    Source = borrowedSource;
                }
                else
                {
                    EnsureSource();
                    ResetSource();
                    Source.clip = clip;
                    Source.loop = options.Loop;
                    Source.pitch = options.Pitch;
                    Source.spatialBlend = spatial ? 1f : 0f;
                }
                SetOutput(options.OutputRoute.Resolve(_owner.GetOutputGroup(this)));
                if (spatial && _borrowed == null)
                {
                    Source.minDistance = options.MinDistance;
                    Source.maxDistance = options.MaxDistance;
                    Source.rolloffMode = options.RolloffMode;
                }
                if (spatial)
                {
                    Source.transform.position = position;
                }

                SetVolume(sharedVolume);
                SetMute(muted);
                var handle = new AudioHandle(this);
                resource?.Accept(handle);
                if (_borrowed == null)
                {
                    Source.gameObject.SetActive(true);
                }

                if (IsValid(generation) && enabled && startImmediately)
                {
                    StartSource();
                }

                return handle;
            }
            catch
            {
                if (IsActive && Generation == generation)
                {
                    Stop(AudioRequestEnd.Failed);
                }

                throw;
            }
        }
        internal void SetVolume(float sharedVolume)
        {
            if (!IsActive || !Source)
            {
                return;
            }

            var value = Mathf.Clamp01(sharedVolume * Options.Volume) * _fadeGain * TransitionGain;
            if (_borrowed != null)
            {
                _borrowed.SetVolume(value);
            }
            else
            {
                Source.volume = value;
            }
        }
        internal void SetTransitionGain(float value)
        {
            TransitionGain = Mathf.Clamp01(value);
            SetVolume(_owner.GetSharedVolume(this));
        }
        internal void SuppressCompletion() => _onFinished = null;
        internal void StartConfigured(ulong generation)
        {
            if (IsValid(generation))
            {
                StartSource();
            }
        }
        internal void SetMute(bool value)
        {
            if (!IsActive || !Source)
            {
                return;
            }

            if (_borrowed != null)
            {
                _borrowed.SetMute(value);
            }
            else
            {
                Source.mute = value;
            }
            // Unmuting must restore the latest channel volume and request scale, not a stale Unity snapshot.
            SetVolume(_owner.GetSharedVolume(this));
        }
        internal void SetInheritedOutputGroup(AudioMixerGroup group)
        {
            if (IsActive && Source && Options.OutputRoute.Mode == AudioOutputRoute.RouteMode.Inherit)
            {
                SetOutput(group ? group : null);
            }
        }
        private void SetOutput(AudioMixerGroup group)
        {
            if (_borrowed != null)
            {
                _borrowed.SetOutput(group);
            }
            else if (Source)
            {
                Source.outputAudioMixerGroup = group;
            }
        }
        internal void SetVolumeScale(float volumeScale)
        {
            if (!AudioSoundOptions.IsFinite(volumeScale) || volumeScale < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(volumeScale));
            }

            _options.Volume = volumeScale;
            SetVolume(_owner.GetSharedVolume(this));
        }
        internal void SetPitch(float pitch)
        {
            if (!AudioSoundOptions.IsFinite(pitch) || pitch <= 0 || pitch > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(pitch));
            }

            _options.Pitch = pitch;
            if (_borrowed != null)
            {
                _borrowed.SetPitch(pitch);
            }
            else if (Source)
            {
                Source.pitch = pitch;
            }
        }
        internal void FadeIn(float duration, bool ignoreTimeScale) => BeginFade(1f, duration, false, ignoreTimeScale, true);
        internal void FadeOut(float duration, bool stopWhenDone, bool ignoreTimeScale) => BeginFade(0f, duration, stopWhenDone, ignoreTimeScale, false);
        private void BeginFade(float target, float duration, bool stopWhenDone, bool ignoreTimeScale, bool fromSilence)
        {
            if (!AudioSoundOptions.IsFinite(duration) || duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            if (_isAutoFadeActive)
            {
                RefreshAutoFade();
                _isAutoFadeActive = false;
                _hasTouchedFade = true;
            }
            if (fromSilence && !_hasTouchedFade && duration > 0f)
            {
                _fadeGain = 0f;
            }

            _hasTouchedFade = true;
            _fadeFrom = _fadeGain;
            _fadeTarget = target;
            _fadeDuration = duration;
            _fadeElapsed = 0d;
            _fadeStopWhenDone = stopWhenDone;
            _fadeIgnoreTimeScale = ignoreTimeScale;
            _isFading = duration > 0f;
            if (!_isFading)
            {
                _fadeGain = target;
            }

            SetVolume(_owner.GetSharedVolume(this));
            if (!_isFading && stopWhenDone)
            {
                Stop();
            }
        }
        internal void TickFade(float deltaTime, float unscaledDeltaTime)
        {
            if (!IsActive || !_hasStarted || IsPaused || !_isEnabled || !_clip || _clip.loadState != AudioDataLoadState.Loaded)
            {
                return;
            }

            if (_isAutoFadeActive)
            {
                RefreshAutoFade();
                return;
            }
            if (!_isFading)
            {
                return;
            }

            var delta = _fadeIgnoreTimeScale ? unscaledDeltaTime : deltaTime;
            if (delta <= 0f)
            {
                return;
            }

            _fadeElapsed = Math.Min(_fadeDuration, _fadeElapsed + delta);
            _fadeGain = Mathf.Lerp(_fadeFrom, _fadeTarget, (float)(_fadeElapsed / _fadeDuration));
            _isFading = _fadeElapsed < _fadeDuration;
            SetVolume(_owner.GetSharedVolume(this));
            if (!_isFading && _fadeStopWhenDone)
            {
                Stop();
            }
        }
        private void ResetFade()
        {
            _fadeGain = _fadeFrom = _fadeTarget = 1f;
            _fadeDuration = 0f;
            _fadeElapsed = 0d;
            _isFading = _hasTouchedFade = _fadeStopWhenDone = false;
            _fadeIgnoreTimeScale = true;
            _isAutoFadeActive = _hasCompletedAutoLoop = false;
            _autoLength = _autoFadeIn = _autoFadeOut = _autoPreviousTime = 0d;
            TransitionGain = 1f;
        }
        private void ResetAutoPosition()
        {
            _autoPreviousTime = 0d;
            _hasCompletedAutoLoop = false;
            _fadeGain = _autoFadeIn > 0d ? 0f : 1f;
        }
        private void RefreshAutoFade()
        {
            if (!IsActive || !_hasStarted || IsPaused || !_isEnabled || !Source || !_clip || !Source.isPlaying)
            {
                return;
            }

            if (Options.Loop && _hasCompletedAutoLoop)
            {
                return;
            }

            var position = Math.Max(0d, Math.Min(_autoLength, Source.timeSamples / (double)_clip.frequency));
            if (Options.Loop && (position >= _autoFadeIn || position < _autoPreviousTime))
            {
                _hasCompletedAutoLoop = true;
            }

            _autoPreviousTime = position;
            _fadeGain = _hasCompletedAutoLoop ? 1f : AudioAutoFade.Evaluate(position, _autoLength, _autoFadeIn, _autoFadeOut);
            SetVolume(_owner.GetSharedVolume(this));
        }
        private void StartSource()
        {
            if (!IsActive || IsPaused || !_isEnabled)
            {
                return;
            }

            if (!Source || !Source.isActiveAndEnabled || !_clip || _borrowed != null && !_borrowed.OwnsPlayback ||
                _isFollowing && (!_target || !_target.gameObject.activeInHierarchy))
            {
                Stop(AudioRequestEnd.Unavailable);
                return;
            }
            if (_clip.loadState == AudioDataLoadState.Failed)
            {
                Stop(AudioRequestEnd.Failed);
                return;
            }
            if (_isFollowing)
            {
                Source.transform.position = _target.position;
            }
            else if (_spatial)
            {
                Source.transform.position = _position;
            }

            SetOutput(Options.OutputRoute.Resolve(_owner.GetOutputGroup(this)));
            if (_isAutoFadeActive)
            {
                ResetAutoPosition();
                SetVolume(_owner.GetSharedVolume(this));
            }
            var generation = Generation;
            var clip = _clip;
            var handle = Handle;
            var musicEpoch = Category == Kind.Music && Channel == null ? _owner.MusicPlayback.Epoch : 0;
            Source.Play();
            _hasStarted = true;
            var callback = _onStarted;
            _onStarted = null;
            try
            {
                callback?.Invoke(clip, handle);
            }
            catch
            {
                if (Category == Kind.Music && Channel == null)
                {
                    _owner.MusicPlayback.StartFailed(handle, musicEpoch);
                }
                else if (IsActive && Generation == generation)
                {
                    Stop(AudioRequestEnd.Failed);
                }

                throw;
            }
        }
        internal void SetEnabled(bool value)
        {
            if (!IsActive || _isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            if (!value)
            {
                if (Source && (_borrowed == null || _borrowed.OwnsPlayback))
                {
                    Source.Stop();
                }
                _hasStarted = false;
            }
            else
            {
                StartSource();
            }
        }
        internal void Pause()
        {
            SetPauseReason(ref _isManuallyPaused, true);
        }
        internal void Resume()
        {
            SetPauseReason(ref _isManuallyPaused, false);
        }
        internal void SetChannelPaused(bool value) => SetPauseReason(ref _isChannelPaused, value);
        internal void RefreshAutomaticPause(bool timeScaleZero) => SetPauseReason(ref _isAutomaticallyPaused, Options.AutoPauseWhenTimeScaleZero && timeScaleZero);
        private void SetPauseReason(ref bool reason, bool value)
        {
            if (!IsActive || reason == value)
            {
                return;
            }

            var wasPaused = IsPaused;
            if (!wasPaused && value && _isAutoFadeActive)
            {
                RefreshAutoFade();
            }

            reason = value;
            if (wasPaused == IsPaused)
            {
                return;
            }

            if (IsPaused)
            {
                if (_hasStarted && Source && (_borrowed == null || _borrowed.OwnsPlayback))
                {
                    Source.Pause();
                }

                return;
            }
            if (!_isEnabled)
            {
                return;
            }

            if (_hasStarted && Source && (_borrowed == null || _borrowed.OwnsPlayback))
            {
                Source.UnPause();
            }
            else
            {
                StartSource();
            }
        }
        internal void CheckFinished()
        {
            if (!IsActive)
            {
                return;
            }

            if (!Source || !_clip || Source.clip != _clip || !Source.isActiveAndEnabled ||
                _isFollowing && (!_target || !_target.gameObject.activeInHierarchy))
            {
                Stop(AudioRequestEnd.Unavailable);
                return;
            }
            var loadState = _clip.loadState;
            if (loadState == AudioDataLoadState.Failed)
            {
                Stop(AudioRequestEnd.Failed);
                return;
            }
            if (_hasStarted && loadState == AudioDataLoadState.Unloaded)
            {
                Stop(AudioRequestEnd.Unavailable);
                return;
            }
            // Play 可由 Unity 延后到背景数据就绪；不能把等待中的 !isPlaying 当自然结束。
            if (loadState != AudioDataLoadState.Loaded)
            {
                return;
            }

            if (_isFollowing)
            {
                Source.transform.position = _target.position;
            }
            else if (_spatial)
            {
                Source.transform.position = _position;
            }

            if (!_hasStarted || IsPaused || !_isEnabled || Source.loop || Source.isPlaying)
            {
                return;
            }

            var generation = Generation;
            var clip = _clip;
            var handle = Handle;
            if (_isAutoFadeActive)
            {
                _fadeGain = AudioAutoFade.Evaluate(_autoLength, _autoLength, _autoFadeIn, _autoFadeOut);
                SetVolume(_owner.GetSharedVolume(this));
            }
            if (IsSound)
            {
                AudioKit.Channels.Finished(clip.GetInstanceID(), handle);
            }

            var callback = _onFinished;
            _onFinished = null;
            // 回调可停止并复用同一 Voice；旧资源保活到原自然通知返回，之后只释放局部责任。
            var resource = _resource;
            _resource = null;
            try
            {
                resource?.End(AudioRequestEnd.Completed);
                callback?.Invoke(clip, handle);
            }
            finally
            {
                try
                {
                    if (IsActive && Generation == generation)
                    {
                        Stop(AudioRequestEnd.Completed);
                    }
                }
                finally
                {
                    resource?.Dispose();
                }
            }
        }
        internal void Stop(AudioRequestEnd reason = AudioRequestEnd.Stopped)
        {
            if (!IsActive)
            {
                return;
            }

            EndReason = reason;
            IsActive = false;
            Generation++;
            var resource = _resource;
            _resource = null;
            var channel = Channel;
            channel?.Remove(this);
            // Sound 先由 owner 摘除计数，再由池 reset 清租用数据；固定槽不进入 Sound 池。
            try
            {
                if (IsSound)
                {
                    _owner.Recycle(this);
                }
                else if (channel != null)
                {
                    _owner.ReleaseChannelVoice(this);
                }
                else
                {
                    ResetPlayback();
                }
            }
            finally
            {
                resource?.End(reason);
                resource?.Dispose();
            }
        }
        internal void ResetPlayback()
        {
            _isManuallyPaused = _isAutomaticallyPaused = _isChannelPaused = false;
            _hasStarted = _isEnabled = false;
            _onStarted = _onFinished = null;
            _clip = null;
            _target = null;
            _isFollowing = false;
            _spatial = false;
            _position = Vector3.zero;
            _options = default;
            ResetFade();
            ClipId = Priority = 0;
            Channel = null;
            if (_borrowed != null)
            {
                var borrowed = _borrowed;
                _borrowed = null;
                Source = null;
                try
                {
                    borrowed.Release();
                }
                finally
                {
                    _owner.ReturnBorrowedSource(borrowed.Source, this);
                }
            }
            else
            {
                ResetSource();
                if (Source)
                {
                    Source.gameObject.SetActive(false);
                }
            }
        }
        private void ResetSource()
        {
            if (!Source)
            {
                return;
            }

            Source.Stop();
            Source.clip = null;
            Source.playOnAwake = false;
            Source.enabled = true;
            Source.outputAudioMixerGroup = null;
            Source.loop = false;
            Source.volume = 1f;
            Source.pitch = 1f;
            Source.mute = false;
            Source.spatialBlend = 0f;
            Source.minDistance = 1f;
            Source.maxDistance = 50f;
            Source.rolloffMode = AudioRolloffMode.Linear;
            Source.dopplerLevel = 0f;
            Source.transform.localPosition = Vector3.zero;
        }
        internal void EnsureSource()
        {
            if (Source)
            {
                return;
            }
            // The component may be destroyed independently; keep ownership of its GameObject until released.
            ReleaseSource();
            var root = new GameObject(IsSound ? "Sound" : "Slot");
            _sourceRoot = root;
            root.SetActive(false);
            try
            {
                root.transform.SetParent(_owner.transform, false);
                Source = root.AddComponent<AudioSource>();
                _ownsSource = true;
                _owner.RegisterOwnedSource(Source);
                if (IsSound)
                {
                    _owner.SoundSourceCreated();
                }

                ResetSource();
            }
            catch
            {
                ReleaseSource();
                throw;
            }
        }
        internal void DestroySource()
        {
            try
            {
                Stop(AudioRequestEnd.Shutdown);
            }
            finally
            {
                ReleaseSource();
            }
        }
        private void ReleaseSource()
        {
            var root = _sourceRoot;
            _sourceRoot = null;
            if (_ownsSource)
            {
                _owner.UnregisterOwnedSource(Source);
            }

            Source = null;
            if (_ownsSource)
            {
                _ownsSource = false;
                if (IsSound)
                {
                    _owner.SoundSourceReleased();
                }
            }
            if (!root)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
