using System;
using UnityEngine;

namespace YLFramework.Audio
{
    /// <summary>命名请求阶段；Accepted 不保证启播，Started 表示 Source.Play 已提交而非可听见。</summary>
    public enum AudioPlayState
    {
        Loading,
        Ready,
        Accepted,
        Started,
        Ended
    }
    /// <summary>命名请求的稳定终态；只有 Completed 表示自然完成。</summary>
    public enum AudioPlayEnd
    {
        None,
        Completed,
        Stopped,
        Replaced,
        Unavailable,
        Failed,
        Shutdown,
        Canceled,
        Rejected
    }

    /// <summary>一次命名播放的稳定身份。加载中 Handle 无效；Cancel/Dispose 只结束本请求，不物理取消共享加载。</summary>
    /// <remarks>Unity 主线程使用。Error/Handle/HasStarted 在终态仍保留原请求证据；Stop 等可在开始回调中同步终结。</remarks>
    public sealed class AudioPlayRequest : IDisposable
    {
        private readonly AudioManager _owner;
        internal readonly AudioCategory Category;
        internal readonly AudioChannel Channel;
        private readonly AudioPlaybackOptions _options;
        private readonly Action<AudioClip, AudioHandle> _started;
        private readonly Action<AudioClip, AudioHandle> _finished;
        private readonly float _crossfade;
        private readonly bool _waitForData;
        private readonly AudioSource _borrowedSource;
        private readonly bool _isBorrowed;
        private readonly ulong _session;
        private IDisposable _waiting;
        private AudioClipLease _clip;
        private AudioResourceLease _resource;
        private bool _hasRequestedData;
        private bool _isSubmitting;
        /// <summary>完整资源定位。</summary>
        public AudioClipAddress Address
        {
            get;
        }
        /// <summary>当前阶段；终结后不会恢复。</summary>
        public AudioPlayState State { get; private set; } = AudioPlayState.Loading;
        /// <summary>终态是否已确定，不等于底层 producer 已停止。</summary>
        public bool IsDone => State == AudioPlayState.Ended;
        /// <summary>稳定终态原因。</summary>
        public AudioPlayEnd EndReason
        {
            get; private set;
        }
        /// <summary>后端或提交错误；取消/拒播没有伪造加载错误。</summary>
        public Exception Error
        {
            get; private set;
        }
        /// <summary>资产加载进度；不声称包含全部解码/Streaming 数据。</summary>
        public float Progress
        {
            get; private set;
        }
        /// <summary>资产曾由后端取得。</summary>
        public bool IsAssetReady
        {
            get; private set;
        }
        /// <summary>音频数据曾达到 Loaded；不表示 Streaming 全文件常驻内存。</summary>
        public bool IsDataReady
        {
            get; private set;
        }
        /// <summary>是否曾提交 Source.Play。</summary>
        public bool HasStarted
        {
            get; private set;
        }
        /// <summary>接纳/开始时的原播放句柄；加载中无效，终态保留原身份但不可控制新代次。</summary>
        public AudioHandle Handle
        {
            get; private set;
        }
        internal bool IsPending => State == AudioPlayState.Loading || State == AudioPlayState.Ready;

        internal AudioPlayRequest(AudioManager owner, AudioClipAddress address, AudioCategory category, AudioChannel channel,
            AudioPlaybackOptions options, Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool waitForData, float crossfade, AudioSource source = null, bool borrowed = false)
        {
            _owner = owner;
            Address = address;
            Category = category;
            Channel = channel;
            _options = options;
            _started = started;
            _finished = finished;
            _waitForData = waitForData;
            _crossfade = crossfade;
            _borrowedSource = source;
            _isBorrowed = borrowed;
            _session = AudioKit.SoundSession;
        }
        internal void Load(bool asynchronous)
        {
            if (IsDone)
            {
                return;
            }

            if (!asynchronous)
            {
                try
                {
                    Receive(_owner.ClipCache.Load(Address), null);
                }
                catch (Exception exception)
                {
                    Fail(exception);
                    throw;
                }
                return;
            }
            var waiting = _owner.ClipCache.LoadAsync(Address, Receive, value =>
            {
                if (!IsDone)
                {
                    Progress = value;
                }
            });
            if (IsPending && !IsAssetReady)
            {
                _waiting = waiting;
            }
            else
            {
                waiting.Dispose();
            }
        }
        private bool Available => _owner && _owner.IsAvailable && _session == AudioKit.SoundSession && (Channel == null || Channel.IsValid);
        private void Receive(AudioClipLease lease, Exception error)
        {
            if (IsDone || !Available)
            {
                lease?.ReleaseSafely();
                return;
            }
            _waiting = null;
            if (error != null)
            {
                lease?.ReleaseSafely();
                Fail(error);
                return;
            }
            _clip = lease;
            IsAssetReady = true;
            Progress = 1;
            State = AudioPlayState.Ready;
            try
            {
                Tick();
            }
            catch (Exception exception)
            {
                Fail(exception);
                if (!_waitForData)
                {
                    throw;
                }
            }
        }
        internal void Tick()
        {
            if (IsDone || !IsPending || _isSubmitting)
            {
                return;
            }

            if (!Available)
            {
                End(AudioPlayEnd.Unavailable);
                return;
            }
            // 等待期间仅观察存活，不占用 Source；真正借用仍由提交时的原 Voice 完成。
            if (_isBorrowed && (!_borrowedSource || !_borrowedSource.isActiveAndEnabled))
            {
                End(AudioPlayEnd.Unavailable);
                return;
            }
            if (!IsAssetReady)
            {
                return;
            }

            var clip = _clip.Clip;
            if (!clip)
            {
                End(AudioPlayEnd.Unavailable);
                return;
            }
            if (clip.loadState == AudioDataLoadState.Failed)
            {
                Fail(new InvalidOperationException("Audio data failed to load: " + Address.Key));
                return;
            }
            if (_waitForData && clip.loadState != AudioDataLoadState.Loaded)
            {
                if (!_hasRequestedData && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    _hasRequestedData = true;
                    if (!clip.LoadAudioData())
                    {
                        Fail(new InvalidOperationException("Audio data load was rejected: " + Address.Key));
                        return;
                    }
                }
                if (clip.loadState != AudioDataLoadState.Loaded)
                {
                    return;
                }
            }
            IsDataReady = clip.loadState == AudioDataLoadState.Loaded;
            _options.ValidateClip(clip);
            var acquired = _clip;
            _clip = null;
            _resource = new AudioResourceLease(acquired, reason => End((AudioPlayEnd)reason, stopPlayback: false), handle =>
            {
                Handle = handle;
                State = AudioPlayState.Accepted;
                _owner.RemovePending(this);
            });
            // 替换旧轨/抢占释放仍可重入取消；直到原 Voice 已绑定并交付句柄才撤销 pending。
            _isSubmitting = true;
            Action<AudioClip, AudioHandle> started = (actual, handle) =>
            {
                Handle = handle;
                HasStarted = true;
                if (!IsDone)
                {
                    State = AudioPlayState.Started;
                }

                _started?.Invoke(actual, handle);
            };
            AudioHandle result;
            if (Channel != null)
            {
                result = Channel.PlayWithSnapshot(clip, _options, started, _finished, _resource,
                source: _borrowedSource, borrowed: _isBorrowed, namedCommit: true);
            }
            else if (Category == AudioCategory.Sound)
            {
                result = AudioKit.PlaySoundWithSnapshot(clip, _options, _finished, started, resource: _resource);
            }
            else if (_crossfade >= 0f)
            {
                result = AudioKit.CrossFadeMusicWithSnapshot(clip, _crossfade, _options, started, _finished, resource: _resource, namedCommit: true);
            }
            else
            {
                result = AudioKit.PlaySlotWithSnapshot(clip, Category == AudioCategory.Music, _options, started, _finished, _resource, namedCommit: true);
            }

            if (!HasStarted)
            {
                Handle = result;
            }

            if (!_resource.IsBound && !IsDone)
            {
                End(AudioPlayEnd.Rejected);
            }
        }
        internal void Fail(Exception exception)
        {
            Error = exception;
            End(AudioPlayEnd.Failed);
        }
        internal void End(AudioPlayEnd reason, bool stopPlayback = true)
        {
            if (IsDone)
            {
                return;
            }

            State = AudioPlayState.Ended;
            EndReason = reason;
            _owner.RemovePending(this);
            var waiting = _waiting;
            _waiting = null;
            var clip = _clip;
            _clip = null;
            try
            {
                waiting?.Dispose();
            }
            finally
            {
                clip?.ReleaseSafely();
                if (_resource != null && !_resource.IsBound)
                {
                    _resource.Dispose();
                }

                if (stopPlayback)
                {
                    Handle.Stop();
                }
            }
        }
        /// <summary>逻辑取消等待或停止原播放。共享 producer 继续；迟到结果按原责任归还。</summary>
        public void Cancel()
        {
            if (IsDone)
            {
                return;
            }

            End(AudioPlayEnd.Canceled);
        }
        /// <summary>等价于 Cancel，不影响其他等待者。</summary>
        public void Dispose() => Cancel();
    }
}
