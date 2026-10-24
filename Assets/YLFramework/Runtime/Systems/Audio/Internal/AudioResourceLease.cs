using System;
using UnityEngine;

namespace YLFramework.Audio
{
    internal enum AudioRequestEnd
    {
        None,
        Completed,
        Stopped,
        Replaced,
        Unavailable,
        Failed,
        Shutdown
    }

    // 仅取得自有资源的内部入口使用；直接 Clip 播放不创建租约，也不释放 Clip。
    internal sealed class AudioResourceLease : IDisposable
    {
        internal readonly ulong Session = AudioKit.SoundSession;
        internal bool IsBound
        {
            get; private set;
        }
        internal bool IsReleased
        {
            get; private set;
        }
        private IDisposable _resource;
        private Action<AudioRequestEnd> _ended;
        private Action<AudioHandle> _accepted;
        internal bool CanBind => !IsBound && !IsReleased && Session == AudioKit.SoundSession;

        internal AudioResourceLease(IDisposable resource, Action<AudioRequestEnd> ended = null, Action<AudioHandle> accepted = null)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _ended = ended;
            _accepted = accepted;
        }

        internal void Accept(AudioHandle handle)
        {
            var accepted = _accepted;
            _accepted = null;
            accepted?.Invoke(handle);
        }

        internal void End(AudioRequestEnd reason)
        {
            var ended = _ended;
            _ended = null;
            ended?.Invoke(reason);
        }

        internal void Bind()
        {
            Validate();
            IsBound = true;
        }

        internal void Validate()
        {
            if (IsBound || IsReleased || Session != AudioKit.SoundSession)
            {
                throw new InvalidOperationException("Audio resource lease is no longer available.");
            }
        }

        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            _accepted = null;
            var resource = _resource;
            _resource = null;
            try
            {
                resource.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
