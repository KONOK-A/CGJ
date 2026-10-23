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
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN && (ENABLE_MONO || ENABLE_IL2CPP))
        private readonly object _gate = new();
        private IntPtr _algorithm;
        private IntPtr _key;
        private bool _isDisposed;
        private Exception _releaseFault;
        private long _keyObjectBytes;
        private static long _activeAlgorithms;
        private static long _activeKeys;
        private static long _activeKeyObjectBytes;
        private static long _releaseFailures;
        // 无法确认销毁的系统 owner 必须留存；不重试有歧义的 native 释放，也不关闭其 provider。
        private static readonly List<CngSegmentAead> _retainedOwners = new();

        internal static bool IsSupported => Environment.OSVersion.Platform == PlatformID.Win32NT && IntPtr.Size == 8
#if ENABLE_IL2CPP && !UNITY_EDITOR
            // 该getter由Unity明确标记IsThreadSafe，可在认证worker中核对精确受测版本。
            && UnityEngine.Application.unityVersion == "2022.3.62f3c1"
#endif
            ;
        internal static long ActiveAlgorithms => Interlocked.Read(ref _activeAlgorithms);
        internal static long ActiveKeys => Interlocked.Read(ref _activeKeys);
        internal static long ActiveKeyObjectBytes => Interlocked.Read(ref _activeKeyObjectBytes);
        internal static long ReleaseFailures => Interlocked.Read(ref _releaseFailures);
        internal static int RetainedReleaseOwners
        {
            get
            {
                lock (_retainedOwners)
                {
                    return _retainedOwners.Count;
                }
            }
        }
        internal long KeyObjectBytes => _keyObjectBytes;
        internal Exception ReleaseFault
        {
            get
            {
                lock (_gate)
                {
                    return _releaseFault;
                }
            }
        }

        internal CngSegmentAead(byte[] secret)
        {
            if (!IsSupported)
            {
                throw Unsupported();
            }

            ResourceBundleEnvelope.ValidateKey(secret);
            ValidateAbi();
            try
            {
                int status = BCryptOpenAlgorithmProvider(out _algorithm, "AES", "Microsoft Primitive Provider", 0);
                if (_algorithm != IntPtr.Zero)
                {
                    Interlocked.Increment(ref _activeAlgorithms);
                }

                Check(status, "BCryptOpenAlgorithmProvider");
                if (_algorithm == IntPtr.Zero)
                {
                    throw new CryptographicException("CNG returned no algorithm owner.");
                }

                const string Mode = "ChainingModeGCM";
                Check(BCryptSetProperty(_algorithm, "ChainingMode", Mode, (Mode.Length + 1) * 2, 0), "BCryptSetProperty");
                Check(BCryptGetTagLengths(_algorithm, "AuthTagLength", out var tags, 12, out var tagBytes, 0), "BCryptGetProperty(AuthTagLength)");
                if (tagBytes != 12 || tags.Minimum < 0 || tags.Increment < 0 || tags.Minimum > 16 || tags.Maximum < 16 ||
                    tags.Increment != 0 && (16 - tags.Minimum) % tags.Increment != 0)
                {
                    throw new CryptographicException("CNG provider does not support the required 128-bit tag.");
                }

                Check(BCryptGetProperty(_algorithm, "ObjectLength", out var bytes, sizeof(int), out var propertyBytes, 0), "BCryptGetProperty(ObjectLength)");
                if (propertyBytes != sizeof(int) || bytes <= 0)
                {
                    throw new CryptographicException("Invalid CNG key-object size.");
                }

                GenerateKey(secret, bytes);
            }
            catch (Exception error)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException("CNG construction and owner cleanup failed.", error, cleanup);
                }
                throw;
            }
        }

        private unsafe void GenerateKey(byte[] secret, int bytes)
        {
            fixed (byte* pointer = secret)
            {
                int status = BCryptGenerateSymmetricKey(_algorithm, out _key, IntPtr.Zero, 0, (IntPtr)pointer, secret.Length, 0);
                if (_key != IntPtr.Zero)
                {
                    _keyObjectBytes = bytes;
                    Interlocked.Increment(ref _activeKeys);
                    Interlocked.Add(ref _activeKeyObjectBytes, bytes);
                }
                Check(status, "BCryptGenerateSymmetricKey");
                if (_key == IntPtr.Zero)
                {
                    throw new CryptographicException("CNG returned no key owner.");
                }
            }
        }

        internal int Encrypt(byte[] input, int count, byte[] nonce, byte[] output) => Transform(true, input, count, nonce, output);
        internal int Decrypt(byte[] input, int count, byte[] nonce, byte[] output) => Transform(false, input, count, nonce, output);

        private unsafe int Transform(bool encrypt, byte[] input, int count, byte[] nonce, byte[] output)
        {
            lock (_gate)
            {
                if (_isDisposed || _key == IntPtr.Zero || _releaseFault != null)
                {
                    throw new ObjectDisposedException(nameof(CngSegmentAead));
                }

                if (input == null || output == null || nonce == null)
                {
                    throw new ArgumentNullException();
                }

                if (count < (encrypt ? 0 : 16) || count > input.Length || nonce.Length != 12 || ReferenceEquals(input, output))
                {
                    throw new ArgumentException("Invalid single-segment AEAD input.");
                }

                int plainCount = encrypt ? count : count - 16;
                int resultCount = encrypt ? checked(count + 16) : plainCount;
                if (output.Length < resultCount)
                {
                    throw new ArgumentException("Segment output too short.");
                }

                fixed (byte* inputPointer = input, outputPointer = output, noncePointer = nonce)
                {
                    byte empty = 0;
                    var info = new AuthInfo
                    {
                        Size = 88,
                        Version = 1,
                        Nonce = (IntPtr)noncePointer,
                        NonceBytes = 12,
                        Tag = (IntPtr)(encrypt ? outputPointer + plainCount : inputPointer + plainCount),
                        TagBytes = 16
                    };
                    // 空消息仍须认证；NULL output 会变成尺寸查询，不能当作 tag 验证。
                    var source = plainCount == 0 ? (IntPtr)(&empty) : (IntPtr)inputPointer;
                    var destination = plainCount == 0 ? (IntPtr)(&empty) : (IntPtr)outputPointer;
                    int status = encrypt
                        ? BCryptEncrypt(_key, source, plainCount, ref info, IntPtr.Zero, 0, destination, plainCount, out var written, 0)
                        : BCryptDecrypt(_key, source, plainCount, ref info, IntPtr.Zero, 0, destination, plainCount, out written, 0);
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
            lock (_gate)
            {
                if (_releaseFault != null)
                {
                    throw new ResourceContentCleanupException(this, _releaseFault);
                }

                if (_isDisposed)
                {
                    return;
                }

                if (_key != IntPtr.Zero)
                {
                    int status = BCryptDestroyKey(_key);
                    if (status != 0)
                    {
                        throw RetainFailure(status, "BCryptDestroyKey");
                    }

                    _key = IntPtr.Zero;
                    Interlocked.Decrement(ref _activeKeys);
                    Interlocked.Add(ref _activeKeyObjectBytes, -_keyObjectBytes);
                }
                if (_algorithm != IntPtr.Zero)
                {
                    int status = BCryptCloseAlgorithmProvider(_algorithm, 0);
                    if (status != 0)
                    {
                        throw RetainFailure(status, "BCryptCloseAlgorithmProvider");
                    }

                    _algorithm = IntPtr.Zero;
                    Interlocked.Decrement(ref _activeAlgorithms);
                }
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        private ResourceContentCleanupException RetainFailure(int status, string operation)
        {
            _releaseFault = NativeFailure(status, operation);
            Interlocked.Increment(ref _releaseFailures);
            lock (_retainedOwners)
            {
                _retainedOwners.Add(this);
            }

            GC.SuppressFinalize(this);
            return new ResourceContentCleanupException(this, _releaseFault);
        }

        ~CngSegmentAead()
        {
            try
            {
                Dispose();
            }
            catch (ResourceContentCleanupException) { /* owner 已留存并记入诊断，不在 finalizer 重试。 */ }
        }

        private static void ValidateAbi()
        {
            var names = new[] { nameof(AuthInfo.Size), nameof(AuthInfo.Version), nameof(AuthInfo.Nonce), nameof(AuthInfo.NonceBytes),
                nameof(AuthInfo.AuthData), nameof(AuthInfo.AuthDataBytes), nameof(AuthInfo.Tag), nameof(AuthInfo.TagBytes),
                nameof(AuthInfo.MacContext), nameof(AuthInfo.MacContextBytes), nameof(AuthInfo.AadBytes), nameof(AuthInfo.DataBytes), nameof(AuthInfo.Flags) };
            var offsets = new[] { 0, 4, 8, 16, 24, 32, 40, 48, 56, 64, 68, 72, 80 };
            if (Marshal.SizeOf<AuthInfo>() != 88 || Marshal.SizeOf<TagLengths>() != 12)
            {
                throw new PlatformNotSupportedException("Unexpected CNG AEAD ABI size.");
            }

            for (int i = 0; i < names.Length; ++i)
            {
                if (Marshal.OffsetOf<AuthInfo>(names[i]).ToInt32() != offsets[i])
                {
                    throw new PlatformNotSupportedException("Unexpected CNG AEAD ABI member offset.");
                }
            }
        }

        private static void Check(int status, string operation)
        {
            if (status != 0)
            {
                throw NativeFailure(status, operation);
            }
        }

        private static CryptographicException NativeFailure(int status, string operation) =>
            new(operation + " NTSTATUS=0x" + unchecked((uint)status).ToString("X8"));

        [StructLayout(LayoutKind.Sequential)]
        private struct TagLengths
        {
            internal int Minimum;
            internal int Maximum;
            internal int Increment;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AuthInfo
        {
            internal int Size;
            internal int Version;
            internal IntPtr Nonce;
            internal int NonceBytes;
            internal IntPtr AuthData;
            internal int AuthDataBytes;
            internal IntPtr Tag;
            internal int TagBytes;
            internal IntPtr MacContext;
            internal int MacContextBytes;
            internal int AadBytes;
            internal ulong DataBytes;
            internal int Flags;
        }

#if ENABLE_IL2CPP && !UNITY_EDITOR
        // IL2CPP不传递DefaultDllImportSearchPaths，按确切System32模块句柄绑定，不走插件目录搜索。
        // 与P/Invoke运行时缓存相同，此模块是进程期依赖；provider/key仍逐owner确定释放。
        private static readonly IntPtr SystemModule = LoadSystemModule();
        private static readonly OpenAlgorithm BCryptOpenAlgorithmProvider = Bind<OpenAlgorithm>("BCryptOpenAlgorithmProvider");
        private static readonly SetProperty BCryptSetProperty = Bind<SetProperty>("BCryptSetProperty");
        private static readonly GetProperty BCryptGetProperty = Bind<GetProperty>("BCryptGetProperty");
        private static readonly GetTagLengths BCryptGetTagLengths = Bind<GetTagLengths>("BCryptGetProperty");
        private static readonly GenerateSymmetricKey BCryptGenerateSymmetricKey = Bind<GenerateSymmetricKey>("BCryptGenerateSymmetricKey");
        private static readonly TransformSegment BCryptEncrypt = Bind<TransformSegment>("BCryptEncrypt");
        private static readonly TransformSegment BCryptDecrypt = Bind<TransformSegment>("BCryptDecrypt");
        private static readonly DestroyKey BCryptDestroyKey = Bind<DestroyKey>("BCryptDestroyKey");
        private static readonly CloseAlgorithm BCryptCloseAlgorithmProvider = Bind<CloseAlgorithm>("BCryptCloseAlgorithmProvider");

        private static IntPtr LoadSystemModule()
        {
            var directory = new System.Text.StringBuilder(32768);
            uint length = GetSystemDirectoryW(directory, (uint)directory.Capacity);
            if (length == 0 || length >= directory.Capacity) throw new PlatformNotSupportedException("Cannot resolve the Windows system directory.");
            string path = Path.GetFullPath(Path.Combine(directory.ToString(), "bcrypt.dll"));
            var module = LoadLibraryExW(path, IntPtr.Zero, 0x00000800); // LOAD_LIBRARY_SEARCH_SYSTEM32
            if (module == IntPtr.Zero) throw new PlatformNotSupportedException("Cannot load the System32 CNG dependency: " + Marshal.GetLastWin32Error());
            try
            {
                var actual = new System.Text.StringBuilder(32768);
                uint written = GetModuleFileNameW(module, actual, (uint)actual.Capacity);
                if (written == 0 || written >= actual.Capacity || !Path.GetFullPath(actual.ToString()).Equals(path, StringComparison.OrdinalIgnoreCase))
                    throw new PlatformNotSupportedException("The CNG dependency is not the exact System32 module.");
                return module;
            }
            catch (Exception error)
            {
                if (!FreeLibrary(module)) throw new AggregateException("CNG dependency validation and module cleanup failed.", error,
                    new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
                throw;
            }
        }

        private static T Bind<T>(string entry) where T : Delegate
        {
            var address = GetProcAddress(SystemModule, entry);
            if (address == IntPtr.Zero) throw new EntryPointNotFoundException("System32 bcrypt.dll: " + entry);
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private delegate int OpenAlgorithm(out IntPtr algorithm, [MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string implementation, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private delegate int SetProperty(IntPtr algorithm, [MarshalAs(UnmanagedType.LPWStr)] string property, [MarshalAs(UnmanagedType.LPWStr)] string input, int inputBytes, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private delegate int GetProperty(IntPtr algorithm, [MarshalAs(UnmanagedType.LPWStr)] string property, out int value, int valueBytes, out int written, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private delegate int GetTagLengths(IntPtr algorithm, [MarshalAs(UnmanagedType.LPWStr)] string property, out TagLengths value, int valueBytes, out int written, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GenerateSymmetricKey(IntPtr algorithm, out IntPtr key, IntPtr keyObject, int keyObjectBytes, IntPtr secret, int secretBytes, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int TransformSegment(IntPtr key, IntPtr input, int inputBytes, ref AuthInfo auth, IntPtr iv, int ivBytes, IntPtr output, int outputBytes, out int written, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DestroyKey(IntPtr key);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CloseAlgorithm(IntPtr algorithm, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetSystemDirectoryW(System.Text.StringBuilder buffer, uint size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetModuleFileNameW(IntPtr module, System.Text.StringBuilder path, uint size);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string entry);
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);
#else
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
#endif
#else
        internal static bool IsSupported => false;
        internal static long ActiveAlgorithms => 0;
        internal static long ActiveKeys => 0;
        internal static long ActiveKeyObjectBytes => 0;
        internal static long ReleaseFailures => 0;
        internal static int RetainedReleaseOwners => 0;
        internal long KeyObjectBytes => 0;
        internal Exception ReleaseFault => null;
        internal CngSegmentAead(byte[] secret)
        {
            throw Unsupported();
        }
        internal int Encrypt(byte[] input, int count, byte[] nonce, byte[] output) => throw Unsupported();
        internal int Decrypt(byte[] input, int count, byte[] nonce, byte[] output) => throw Unsupported();
        public void Dispose()
        {
        }
#endif
        private static PlatformNotSupportedException Unsupported() =>
            new("Encrypted Resource content requires Windows x64 Editor or a validated Windows64 Mono/IL2CPP Player backend.");
    }

    internal sealed class ResourceContentCleanupException : IOException
    {
        internal IDisposable Owner
        {
            get;
        }
        internal ResourceContentCleanupException(IDisposable owner, Exception cause)
            : base("Resource native content owner release failed; owner retained without automatic retry.", cause) => Owner = owner;
    }
}
