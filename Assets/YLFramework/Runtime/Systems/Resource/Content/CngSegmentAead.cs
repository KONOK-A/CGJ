using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace YLFramework.Services.Resource
{
    /// <summary>只封装系统单段 AEAD ABI，不实现 AES/GHASH，也不提供软件回退。</summary>
    internal sealed class CngSegmentAead : IDisposable
    {
#if (UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN && ENABLE_MONO)) && !ENABLE_IL2CPP
        private readonly object gate = new();
        private IntPtr algorithm;
        private IntPtr key;
        private bool disposed;
        private Exception releaseFault;
        private long keyObjectBytes;
        private static long activeAlgorithms, activeKeys, activeKeyObjectBytes, releaseFailures;
        // 无法确认销毁的系统 owner 必须留存；不重试有歧义的 native 释放，也不关闭其 provider。
        private static readonly List<CngSegmentAead> RetainedOwners = new();

        internal static bool IsSupported => Environment.OSVersion.Platform == PlatformID.Win32NT && IntPtr.Size == 8;
        internal static long ActiveAlgorithms => Interlocked.Read(ref activeAlgorithms);
        internal static long ActiveKeys => Interlocked.Read(ref activeKeys);
        internal static long ActiveKeyObjectBytes => Interlocked.Read(ref activeKeyObjectBytes);
        internal static long ReleaseFailures => Interlocked.Read(ref releaseFailures);
        internal static int RetainedReleaseOwners { get { lock (RetainedOwners) return RetainedOwners.Count; } }
        internal long KeyObjectBytes => keyObjectBytes;
        internal Exception ReleaseFault { get { lock (gate) return releaseFault; } }

        internal CngSegmentAead(byte[] secret)
        {
            if (!IsSupported) throw Unsupported();
            ResourceBundleEnvelope.ValidateKey(secret);
            ValidateAbi();
            try
            {
                int status = BCryptOpenAlgorithmProvider(out algorithm, "AES", "Microsoft Primitive Provider", 0);
                if (algorithm != IntPtr.Zero) Interlocked.Increment(ref activeAlgorithms);
                Check(status, "BCryptOpenAlgorithmProvider");
                if (algorithm == IntPtr.Zero) throw new CryptographicException("CNG returned no algorithm owner.");
                const string mode = "ChainingModeGCM";
                Check(BCryptSetProperty(algorithm, "ChainingMode", mode, (mode.Length + 1) * 2, 0), "BCryptSetProperty");
                Check(BCryptGetTagLengths(algorithm, "AuthTagLength", out var tags, 12, out var tagBytes, 0), "BCryptGetProperty(AuthTagLength)");
                if (tagBytes != 12 || tags.Minimum < 0 || tags.Increment < 0 || tags.Minimum > 16 || tags.Maximum < 16 ||
                    tags.Increment != 0 && (16 - tags.Minimum) % tags.Increment != 0)
                    throw new CryptographicException("CNG provider does not support the required 128-bit tag.");
                Check(BCryptGetProperty(algorithm, "ObjectLength", out var bytes, sizeof(int), out var propertyBytes, 0), "BCryptGetProperty(ObjectLength)");
                if (propertyBytes != sizeof(int) || bytes <= 0) throw new CryptographicException("Invalid CNG key-object size.");
                GenerateKey(secret, bytes);
            }
            catch (Exception error)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("CNG construction and owner cleanup failed.", error, cleanup); }
                throw;
            }
        }

        private unsafe void GenerateKey(byte[] secret, int bytes)
        {
            fixed (byte* pointer = secret)
            {
                int status = BCryptGenerateSymmetricKey(algorithm, out key, IntPtr.Zero, 0, (IntPtr)pointer, secret.Length, 0);
                if (key != IntPtr.Zero)
                {
                    keyObjectBytes = bytes;
                    Interlocked.Increment(ref activeKeys);
                    Interlocked.Add(ref activeKeyObjectBytes, bytes);
                }
                Check(status, "BCryptGenerateSymmetricKey");
                if (key == IntPtr.Zero) throw new CryptographicException("CNG returned no key owner.");
            }
        }

        internal int Encrypt(byte[] input, int count, byte[] nonce, byte[] output) => Transform(true, input, count, nonce, output);
        internal int Decrypt(byte[] input, int count, byte[] nonce, byte[] output) => Transform(false, input, count, nonce, output);

        private unsafe int Transform(bool encrypt, byte[] input, int count, byte[] nonce, byte[] output)
        {
            lock (gate)
            {
                if (disposed || key == IntPtr.Zero || releaseFault != null) throw new ObjectDisposedException(nameof(CngSegmentAead));
                if (input == null || output == null || nonce == null) throw new ArgumentNullException();
                if (count < (encrypt ? 0 : 16) || count > input.Length || nonce.Length != 12 || ReferenceEquals(input, output))
                    throw new ArgumentException("Invalid single-segment AEAD input.");
                int plainCount = encrypt ? count : count - 16;
                int resultCount = encrypt ? checked(count + 16) : plainCount;
                if (output.Length < resultCount) throw new ArgumentException("Segment output too short.");
                fixed (byte* inputPointer = input, outputPointer = output, noncePointer = nonce)
                {
                    byte empty = 0;
                    var info = new AuthInfo { Size = 88, Version = 1, Nonce = (IntPtr)noncePointer, NonceBytes = 12,
                        Tag = (IntPtr)(encrypt ? outputPointer + plainCount : inputPointer + plainCount), TagBytes = 16 };
                    // 空消息仍须认证；NULL output 会变成尺寸查询，不能当作 tag 验证。
                    var source = plainCount == 0 ? (IntPtr)(&empty) : (IntPtr)inputPointer;
                    var destination = plainCount == 0 ? (IntPtr)(&empty) : (IntPtr)outputPointer;
                    int status = encrypt
                        ? BCryptEncrypt(key, source, plainCount, ref info, IntPtr.Zero, 0, destination, plainCount, out var written, 0)
                        : BCryptDecrypt(key, source, plainCount, ref info, IntPtr.Zero, 0, destination, plainCount, out written, 0);
                    GC.KeepAlive(this);
                    if (status != 0 || written != plainCount)
                    {
                        Array.Clear(output, 0, output.Length);
                        Check(status, encrypt ? "BCryptEncrypt" : "BCryptDecrypt");
                        throw new CryptographicException("Unexpected authenticated segment size.");
                    }
                    return resultCount;
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (releaseFault != null) throw new ResourceContentCleanupException(this, releaseFault);
                if (disposed) return;
                if (key != IntPtr.Zero)
                {
                    int status = BCryptDestroyKey(key);
                    if (status != 0) throw RetainFailure(status, "BCryptDestroyKey");
                    key = IntPtr.Zero;
                    Interlocked.Decrement(ref activeKeys);
                    Interlocked.Add(ref activeKeyObjectBytes, -keyObjectBytes);
                }
                if (algorithm != IntPtr.Zero)
                {
                    int status = BCryptCloseAlgorithmProvider(algorithm, 0);
                    if (status != 0) throw RetainFailure(status, "BCryptCloseAlgorithmProvider");
                    algorithm = IntPtr.Zero;
                    Interlocked.Decrement(ref activeAlgorithms);
                }
                disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private ResourceContentCleanupException RetainFailure(int status, string operation)
        {
            releaseFault = NativeFailure(status, operation);
            Interlocked.Increment(ref releaseFailures);
            lock (RetainedOwners) RetainedOwners.Add(this);
            GC.SuppressFinalize(this);
            return new ResourceContentCleanupException(this, releaseFault);
        }

        ~CngSegmentAead()
        {
            try { Dispose(); }
            catch (ResourceContentCleanupException) { /* owner 已留存并记入诊断，不在 finalizer 重试。 */ }
        }

        private static void ValidateAbi()
        {
            var names = new[] { nameof(AuthInfo.Size), nameof(AuthInfo.Version), nameof(AuthInfo.Nonce), nameof(AuthInfo.NonceBytes),
                nameof(AuthInfo.AuthData), nameof(AuthInfo.AuthDataBytes), nameof(AuthInfo.Tag), nameof(AuthInfo.TagBytes),
                nameof(AuthInfo.MacContext), nameof(AuthInfo.MacContextBytes), nameof(AuthInfo.AadBytes), nameof(AuthInfo.DataBytes), nameof(AuthInfo.Flags) };
            var offsets = new[] { 0, 4, 8, 16, 24, 32, 40, 48, 56, 64, 68, 72, 80 };
            if (Marshal.SizeOf<AuthInfo>() != 88 || Marshal.SizeOf<TagLengths>() != 12) throw new PlatformNotSupportedException("Unexpected CNG AEAD ABI size.");
            for (int i = 0; i < names.Length; ++i)
                if (Marshal.OffsetOf<AuthInfo>(names[i]).ToInt32() != offsets[i])
                    throw new PlatformNotSupportedException("Unexpected CNG AEAD ABI member offset.");
        }

        private static void Check(int status, string operation)
        {
            if (status != 0) throw NativeFailure(status, operation);
        }

        private static CryptographicException NativeFailure(int status, string operation) =>
            new(operation + " NTSTATUS=0x" + unchecked((uint)status).ToString("X8"));

        [StructLayout(LayoutKind.Sequential)]
        private struct TagLengths { internal int Minimum, Maximum, Increment; }

        [StructLayout(LayoutKind.Sequential)]
        private struct AuthInfo
        {
            internal int Size, Version;
            internal IntPtr Nonce;
            internal int NonceBytes;
            internal IntPtr AuthData;
            internal int AuthDataBytes;
            internal IntPtr Tag;
            internal int TagBytes;
            internal IntPtr MacContext;
            internal int MacContextBytes, AadBytes;
            internal ulong DataBytes;
            internal int Flags;
        }

        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptOpenAlgorithmProvider(out IntPtr algorithm, string id, string implementation, int flags);
        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptSetProperty(IntPtr algorithm, string property, string input, int inputBytes, int flags);
        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptGetProperty(IntPtr algorithm, string property, out int value, int valueBytes, out int written, int flags);
        [DllImport("bcrypt.dll", EntryPoint = "BCryptGetProperty", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptGetTagLengths(IntPtr algorithm, string property, out TagLengths value, int valueBytes, out int written, int flags);
        [DllImport("bcrypt.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptGenerateSymmetricKey(IntPtr algorithm, out IntPtr key, IntPtr keyObject, int keyObjectBytes, IntPtr secret, int secretBytes, int flags);
        [DllImport("bcrypt.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptEncrypt(IntPtr key, IntPtr input, int inputBytes, ref AuthInfo auth, IntPtr iv, int ivBytes, IntPtr output, int outputBytes, out int written, int flags);
        [DllImport("bcrypt.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptDecrypt(IntPtr key, IntPtr input, int inputBytes, ref AuthInfo auth, IntPtr iv, int ivBytes, IntPtr output, int outputBytes, out int written, int flags);
        [DllImport("bcrypt.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptDestroyKey(IntPtr key);
        [DllImport("bcrypt.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr algorithm, int flags);
#else
        internal static bool IsSupported => false;
        internal static long ActiveAlgorithms => 0;
        internal static long ActiveKeys => 0;
        internal static long ActiveKeyObjectBytes => 0;
        internal static long ReleaseFailures => 0;
        internal static int RetainedReleaseOwners => 0;
        internal long KeyObjectBytes => 0;
        internal Exception ReleaseFault => null;
        internal CngSegmentAead(byte[] secret) { throw Unsupported(); }
        internal int Encrypt(byte[] input, int count, byte[] nonce, byte[] output) => throw Unsupported();
        internal int Decrypt(byte[] input, int count, byte[] nonce, byte[] output) => throw Unsupported();
        public void Dispose() { }
#endif
        private static PlatformNotSupportedException Unsupported() =>
            new("Encrypted Resource content requires Windows x64 Editor or Mono Player.");
    }

    internal sealed class ResourceContentCleanupException : IOException
    {
        internal IDisposable Owner { get; }
        internal ResourceContentCleanupException(IDisposable owner, Exception cause)
            : base("Resource native content owner release failed; owner retained without automatic retry.", cause) => Owner = owner;
    }
}
