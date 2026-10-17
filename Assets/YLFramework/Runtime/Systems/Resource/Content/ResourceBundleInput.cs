using System;
using System.IO;
using System.Threading;

namespace YLFramework.Services.Resource
{
    /// <summary>由原 BundleRecord 持有；只有确认 native 卸载后才允许释放流和预算。</summary>
    internal sealed class ResourceBundleInput : IDisposable
    {
        private readonly object gate = new();
        private Stream stream;
        private IDisposable reservation;
        private Exception releaseFault;
        private bool streamReleased;
        internal Stream Stream { get { lock (gate) return stream ?? throw new ObjectDisposedException(nameof(ResourceBundleInput)); } }
        internal bool IsEncrypted { get; }
        internal bool IsDisposed { get { lock (gate) return stream == null && reservation == null; } }
        internal long DataBufferBytes => IsEncrypted ? ResourceDecryptStream.DataBufferBytes : 0;
        internal long CngKeyObjectBytes { get { lock (gate) return stream is ResourceDecryptStream decrypt ? decrypt.CngKeyObjectBytes : 0; } }
        internal Exception Fault { get { lock (gate) return stream is ResourceDecryptStream decrypt ? decrypt.Fault : null; } }
        internal Exception ReleaseFault { get { lock (gate) return releaseFault ?? (stream is ResourceDecryptStream decrypt ? decrypt.ReleaseFault : null); } }

        private ResourceBundleInput(Stream stream, bool encrypted, IDisposable reservation)
        {
            this.stream = stream;
            this.reservation = reservation;
            IsEncrypted = encrypted;
        }

        internal void SetFaultNotification(Action<Exception> notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            lock (gate)
            {
                if (stream == null) throw new ObjectDisposedException(nameof(ResourceBundleInput));
                if (stream is ResourceDecryptStream decrypt) decrypt.SetFaultNotification(notification);
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
            lock (gate)
            {
                if (stream == null && reservation == null) return;
                try
                {
                    if (!streamReleased) { stream.Dispose(); streamReleased = true; }
                    if (reservation != null) { reservation.Dispose(); reservation = null; }
                    stream = null;
                }
                catch (Exception error) { releaseFault = error; throw; }
            }
        }
    }
}
