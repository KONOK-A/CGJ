using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace YLFramework.Services.Resource
{
    /// <summary>每段先认证再交付的独占可定位视图；native 卸载前不能释放。</summary>
    internal sealed class ResourceDecryptStream : Stream
    {
        internal const long DataBufferBytes = ResourceBundleEnvelope.DataBufferBytes;
        internal const int NativeReadBufferSize = ResourceBundleEnvelope.NativeReadBufferSize;
        private readonly object gate = new();
        private readonly Stream input;
        private readonly bool leaveOpen;
        private readonly ResourceBundleEnvelopeHeader envelope;
        private readonly long deliveryLength;
        private readonly byte[] tinkHeader = new byte[40];
        private readonly byte[] nonce = new byte[12];
        private readonly byte[] cipher = new byte[ResourceBundleEnvelope.SegmentSize];
        private readonly byte[][] plain = { new byte[ResourceBundleEnvelope.PlainCapacity], new byte[ResourceBundleEnvelope.PlainCapacity] };
        private readonly long[] indices = { -1, -1 };
        private readonly int[] lengths = new int[2];
        private CngSegmentAead aead;
        private long position;
        private int nextVictim;
        private Exception fault;
        private Exception releaseFault;
        private Action<Exception> faultNotification;
        private bool faultNotified;
        private bool disposed;
        private bool inputReleased;
        internal Exception Fault { get { lock (gate) return fault; } }
        internal Exception ReleaseFault { get { lock (gate) return releaseFault; } }
        internal long CngKeyObjectBytes { get { lock (gate) return aead?.KeyObjectBytes ?? 0; } }
        internal bool IsCleanupComplete { get { lock (gate) return disposed && aead == null && (leaveOpen || inputReleased); } }

        // sink 仅由创建此输入的主体安装，只做有界入队，不得等待主线程或访问 Unity/provider。
        internal void SetFaultNotification(Action<Exception> notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(ResourceDecryptStream));
                if (faultNotification != null) throw new InvalidOperationException("The content input already has a fault notification owner.");
                faultNotification = notification;
                NotifyFault();
            }
        }

        internal ResourceDecryptStream(Stream input, ResourceBundleEnvelopeHeader envelope, byte[] key,
            bool leaveOpen, CancellationToken token)
        {
            this.input = input;
            this.envelope = envelope;
            this.leaveOpen = leaveOpen;
            deliveryLength = input.Length;
            byte[] fileKey = null;
            try
            {
                token.ThrowIfCancellationRequested();
                input.Position = envelope.EncodedPrefix.Length;
                ResourceBundleEnvelope.ReadExact(input, tinkHeader, 0, tinkHeader.Length, token);
                if (tinkHeader[0] != 40) throw new InvalidDataException("Expected AES256 RAW header length 40.");
                fileKey = ResourceBundleEnvelope.DeriveKey(key, envelope.EncodedPrefix, tinkHeader);
                aead = new CngSegmentAead(fileKey);
                Array.Clear(fileKey, 0, fileKey.Length);
                GetSegment(0, token);
                if (envelope.SegmentCount > 1) GetSegment(envelope.SegmentCount - 1, token);
                CheckLength();
                token.ThrowIfCancellationRequested();
            }
            catch (Exception error)
            {
                ClearData();
                if (aead != null)
                {
                    try { aead.Dispose(); aead = null; }
                    catch (Exception cleanup) { throw new IOException("Content open and native cleanup failed.", new AggregateException(error, cleanup)); }
                }
                if (error is OperationCanceledException) throw;
                throw new IOException("Resource content open/authentication failed.", error);
            }
            finally { if (fileKey != null) Array.Clear(fileKey, 0, fileKey.Length); }
        }

        public override bool CanRead { get { lock (gate) return !disposed && fault == null; } }
        public override bool CanSeek { get { lock (gate) return !disposed && fault == null; } }
        public override bool CanWrite => false;
        public override long Length { get { lock (gate) { RequireUsable(); return envelope.PlainLength; } } }
        public override long Position
        {
            get { lock (gate) { RequireUsable(); return position; } }
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (gate)
            {
                RequireUsable();
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0 || count < 0) throw new ArgumentOutOfRangeException();
                if (offset > buffer.Length - count) throw new ArgumentException("Read outside destination bounds.");
                if (count == 0) return 0;
                try
                {
                    CheckLength();
                    int total = 0;
                    while (total < count && position < envelope.PlainLength)
                    {
                        long index = (position + 40) / ResourceBundleEnvelope.PlainCapacity;
                        long start = index == 0 ? 0 : index * ResourceBundleEnvelope.PlainCapacity - 40;
                        int within = (int)(position - start);
                        int slot = GetSegment(index);
                        int take = Math.Min(count - total, lengths[slot] - within);
                        if (take <= 0) throw new InvalidDataException("Invalid authenticated segment mapping.");
                        // 槽的 index 只在完整 tag 成功后发布；坏段的任何字节均不复制给 caller。
                        Buffer.BlockCopy(plain[slot], within, buffer, offset + total, take);
                        position += take;
                        total += take;
                    }
                    return total;
                }
                catch (Exception error) { throw Latch(error); }
            }
        }

        public override int ReadByte()
        {
            lock (gate)
            {
                RequireUsable();
                try
                {
                    CheckLength();
                    if (position >= envelope.PlainLength) return -1;
                    long index = (position + 40) / ResourceBundleEnvelope.PlainCapacity;
                    long start = index == 0 ? 0 : index * ResourceBundleEnvelope.PlainCapacity - 40;
                    int slot = GetSegment(index);
                    int value = plain[slot][(int)(position - start)];
                    ++position;
                    return value;
                }
                catch (Exception error) { throw Latch(error); }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (gate)
            {
                RequireUsable();
                long basis;
                switch (origin)
                {
                    case SeekOrigin.Begin: basis = 0; break;
                    case SeekOrigin.Current: basis = position; break;
                    case SeekOrigin.End: basis = envelope.PlainLength; break;
                    default: throw new ArgumentException("Invalid seek origin.", nameof(origin));
                }
                long target;
                try { target = checked(basis + offset); }
                catch (OverflowException error) { throw new IOException("Seek offset overflow.", error); }
                if (target < 0) throw new IOException("Cannot seek before the beginning.");
                position = target;
                return target;
            }
        }

        internal void ValidateAll(CancellationToken token)
        {
            lock (gate)
            {
                RequireUsable();
                try
                {
                    using var hash = SHA256.Create();
                    long count = 0;
                    for (long index = 0; index < envelope.SegmentCount; ++index)
                    {
                        token.ThrowIfCancellationRequested();
                        CheckLength();
                        int slot = GetSegment(index, token);
                        hash.TransformBlock(plain[slot], 0, lengths[slot], plain[slot], 0);
                        count = checked(count + lengths[slot]);
                    }
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    if (count != envelope.PlainLength || ResourceBundleEnvelope.Digest(hash.Hash) != envelope.PlainSha256)
                        throw new InvalidDataException("Authenticated plaintext length/SHA-256 is inconsistent with its envelope.");
                    CheckLength();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) { throw Latch(error); }
            }
        }

        private int GetSegment(long index, CancellationToken token = default)
        {
            for (int i = 0; i < indices.Length; ++i)
                if (indices[i] == index) return i;
            if (index < 0 || index >= envelope.SegmentCount) throw new InvalidDataException("Invalid segment index.");
            int slot = nextVictim;
            nextVictim = (nextVictim + 1) % plain.Length;
            indices[slot] = -1;
            lengths[slot] = 0;
            Array.Clear(plain[slot], 0, plain[slot].Length);
            long start = index == 0 ? 40 : checked(index * ResourceBundleEnvelope.SegmentSize);
            int cipherCount = (int)Math.Min(ResourceBundleEnvelope.SegmentSize - (index == 0 ? 40 : 0), envelope.PayloadLength - start);
            if (cipherCount < 16) throw new InvalidDataException("Incomplete authenticated segment.");
            input.Position = checked(envelope.EncodedPrefix.Length + start);
            ResourceBundleEnvelope.ReadExact(input, cipher, 0, cipherCount, token);
            ResourceBundleEnvelope.SetNonce(tinkHeader, index, index == envelope.SegmentCount - 1, nonce);
            int written = aead.Decrypt(cipher, cipherCount, nonce, plain[slot]);
            if (written != cipherCount - 16) throw new CryptographicException("Unexpected authenticated plaintext size.");
            lengths[slot] = written;
            indices[slot] = index;
            return slot;
        }

        private void CheckLength()
        {
            if (input.Length != deliveryLength) throw new InvalidDataException("Ciphertext length changed after authenticated open.");
        }

        private void RequireUsable()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ResourceDecryptStream));
            if (fault != null) throw new IOException("Resource content stream is permanently faulted.", fault);
        }

        private IOException Latch(Exception error)
        {
            if (fault == null) fault = error;
            ClearData();
            if (aead != null)
                try { aead.Dispose(); aead = null; }
                catch (Exception cleanup) { releaseFault = cleanup; }
            NotifyFault();
            return new IOException("Resource content read failed; state is permanently faulted.", fault);
        }

        private void NotifyFault()
        {
            if (fault == null || faultNotification == null || faultNotified) return;
            faultNotified = true;
            faultNotification(fault);
        }

        private void ClearData()
        {
            Array.Clear(cipher, 0, cipher.Length);
            Array.Clear(nonce, 0, nonce.Length);
            for (int i = 0; i < plain.Length; ++i)
            {
                Array.Clear(plain[i], 0, plain[i].Length);
                indices[i] = -1;
                lengths[i] = 0;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (gate)
                {
                    disposed = true;
                    ClearData();
                    try
                    {
                        if (aead != null) { aead.Dispose(); aead = null; }
                        if (!leaveOpen && !inputReleased) { input.Dispose(); inputReleased = true; }
                    }
                    catch (Exception error) { releaseFault = error; throw; }
                }
            }
            base.Dispose(disposing);
        }

        public override void Flush() { lock (gate) RequireUsable(); }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
