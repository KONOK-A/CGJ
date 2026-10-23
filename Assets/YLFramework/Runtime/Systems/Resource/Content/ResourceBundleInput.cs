using System;
using System.IO;
using System.Threading;

namespace YLFramework.Services.Resource
{
    /// <summary>由原 BundleRecord 持有；只有确认 native 卸载后才允许释放流和预算。</summary>
    internal sealed class ResourceBundleInput : IDisposable
    {
        private readonly object _gate = new();
        private Stream _stream;
        private IDisposable _reservation;
        private Exception _releaseFault;
        private bool _isStreamReleased;
        internal Stream Stream
        {
            get
            {
                lock (_gate)
                {
                    return _stream ?? throw new ObjectDisposedException(nameof(ResourceBundleInput));
                }
            }
        }
        internal bool IsEncrypted
        {
            get;
        }
        internal bool IsDisposed
        {
            get
            {
                lock (_gate)
                {
                    return _stream == null && _reservation == null;
                }
            }
        }
        internal long DataBufferBytes => IsEncrypted ? ResourceDecryptStream.DataBufferBytes : 0;
        internal long CngKeyObjectBytes
        {
            get
            {
                lock (_gate)
                {
                    return _stream is ResourceDecryptStream decrypt ? decrypt.CngKeyObjectBytes : 0;
                }
            }
        }
        internal Exception Fault
        {
            get
            {
                lock (_gate)
                {
                    return _stream is ResourceDecryptStream decrypt ? decrypt.Fault : null;
                }
            }
        }
        internal Exception ReleaseFault
        {
            get
            {
                lock (_gate)
                {
                    return _releaseFault ?? (_stream is ResourceDecryptStream decrypt ? decrypt.ReleaseFault : null);
                }
            }
        }

        private ResourceBundleInput(Stream stream, bool encrypted, IDisposable reservation)
        {
            this._stream = stream;
            this._reservation = reservation;
            IsEncrypted = encrypted;
        }

        internal void SetFaultNotification(Action<Exception> notification)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            lock (_gate)
            {
                if (_stream == null)
                {
                    throw new ObjectDisposedException(nameof(ResourceBundleInput));
                }

                if (_stream is ResourceDecryptStream decrypt)
                {
                    decrypt.SetFaultNotification(notification);
                }
            }
        }

        internal static ResourceBundleInput Open(FileStream verified, ResourceBundleContext context, byte[] key,
            IDisposable reservation = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            var header = ResourceBundleEnvelope.Inspect(verified, context);
            if (header == null)
            {
                verified.Position = 0;
                return new ResourceBundleInput(verified, false, reservation);
            }
            // 构造失败时 verified 和 reservation 均仍归 caller，不能提前归还共享图的预算。
            var decrypt = ResourceBundleEnvelope.OpenDecrypt(verified, context, key, false, token);
            return new ResourceBundleInput(decrypt, true, reservation);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_stream == null && _reservation == null)
                {
                    return;
                }

                try
                {
                    if (!_isStreamReleased)
                    {
                        _stream.Dispose();
                        _isStreamReleased = true;
                    }
                    if (_reservation != null)
                    {
                        _reservation.Dispose();
                        _reservation = null;
                    }
                    _stream = null;
                }
                catch (Exception error)
                {
                    _releaseFault = error;
                    throw;
                }
            }
        }
    }
}
