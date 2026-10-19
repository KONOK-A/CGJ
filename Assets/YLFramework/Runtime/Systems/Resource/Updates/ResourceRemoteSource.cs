using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace YLFramework.Services.Resource
{
    public sealed class ResourceSigningKey
    {
        public string KeyId { get; }
        private readonly byte[] modulus;
        private readonly byte[] exponent;
        internal RSAParameters Parameters => new() { Modulus = (byte[])modulus.Clone(), Exponent = (byte[])exponent.Clone() };

        public ResourceSigningKey(string keyId, string modulusBase64, string exponentBase64)
        {
            ResourceCatalogCodec.Id(keyId);
            try { modulus = Convert.FromBase64String(modulusBase64); exponent = Convert.FromBase64String(exponentBase64); }
            catch (FormatException error) { throw new ArgumentException("Invalid RSA public key encoding.", error); }
            if (modulus.Length < 256 || modulus.Length > 1024 || modulus[0] < 128 || exponent.Length == 0 || exponent.Length > 8 ||
                exponent[0] == 0 || (exponent[exponent.Length - 1] & 1) == 0 || exponent.Length == 1 && exponent[0] < 3)
                throw new ArgumentException("RSA public keys require 2048-8192 bits and a canonical odd exponent of at least 3.");
            using var rsa = RSA.Create(); rsa.ImportParameters(Parameters);
            KeyId = keyId;
        }
    }

    /// <summary>应用显式绑定的HTTPS发布根与channel。没有默认公钥、不接受远端公钥或任意URL。</summary>
    public sealed class ResourceRemoteSource
    {
        public Uri Endpoint { get; }
        public string Channel { get; }
        public long MinimumReleaseSequence { get; }
        public long MaximumFileBytes { get; }
        public long MaximumDownloadBytes { get; }
        public int TimeoutSeconds { get; }
        public int IdleTimeoutSeconds { get; }
        public int Attempts { get; }
        internal IReadOnlyList<ResourceSigningKey> Keys { get; }
#if UNITY_EDITOR || (UNITY_STANDALONE_WIN && (ENABLE_MONO || ENABLE_IL2CPP) && DEVELOPMENT_BUILD && YL_RESOURCE_ACCEPTANCE_TESTS)
        private byte[] loopbackCertificate;
#endif

        public ResourceRemoteSource(string endpoint, string channel, IEnumerable<ResourceSigningKey> trustedKeys,
            long minimumReleaseSequence = 1, long maximumFileBytes = 1024L * 1024 * 1024, long maximumDownloadBytes = 4L * 1024 * 1024 * 1024,
            int timeoutSeconds = 60, int idleTimeoutSeconds = 15, int attempts = 3)
        {
            if (string.IsNullOrEmpty(endpoint) || endpoint.Length > 2048 || endpoint.IndexOfAny(new[] { '\\', '%', '?', '#' }) >= 0 ||
                !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
                !endpoint.EndsWith("/", StringComparison.Ordinal) || endpoint != uri.AbsoluteUri || uri.AbsolutePath.Split('/').Any(x => x == "." || x == ".."))
                throw new ArgumentException("A canonical HTTPS directory endpoint without credentials, query or escapes is required.");
            ResourceCatalogCodec.Id(channel);
            Keys = ResourceCatalog.Copy(trustedKeys ?? throw new ArgumentNullException(nameof(trustedKeys)));
            if (Keys.Count == 0 || Keys.Count > 16 || Keys.Any(x => x == null) || Keys.Select(x => x.KeyId).Distinct(StringComparer.Ordinal).Count() != Keys.Count)
                throw new ArgumentException("Explicit distinct trusted signing keys are required.");
            if (minimumReleaseSequence < 1 || maximumFileBytes < 1 || maximumDownloadBytes < maximumFileBytes ||
                timeoutSeconds < 1 || timeoutSeconds > 600 || idleTimeoutSeconds < 1 || idleTimeoutSeconds > timeoutSeconds || attempts < 1 || attempts > 5)
                throw new ArgumentOutOfRangeException(nameof(maximumDownloadBytes), "Invalid bounded download policy.");
            Endpoint = uri; Channel = channel; MinimumReleaseSequence = minimumReleaseSequence;
            MaximumFileBytes = maximumFileBytes; MaximumDownloadBytes = maximumDownloadBytes;
            TimeoutSeconds = timeoutSeconds; IdleTimeoutSeconds = idleTimeoutSeconds; Attempts = attempts;
        }

        internal Uri Resolve(string relative)
        {
            if (string.IsNullOrEmpty(relative) || relative.Split('/').Any(x => x.Length == 0 || x == "." || x == "..") ||
                relative.Any(x => !(x >= 'a' && x <= 'z' || x >= 'A' && x <= 'Z' || x >= '0' && x <= '9' || x == '-' || x == '_' || x == '.' || x == '/')))
                throw new InvalidDataException("Remote path is outside the release root.");
            var uri = new Uri(Endpoint, relative);
            if (!Endpoint.IsBaseOf(uri) || uri.Scheme != "https" || uri.Authority != Endpoint.Authority) throw new InvalidDataException("Remote path escaped its origin.");
            return uri;
        }

        internal static void RequireSupportedRuntime()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor) return;
#if UNITY_STANDALONE_WIN && (ENABLE_MONO || ENABLE_IL2CPP) && !UNITY_EDITOR
            // 只开放8.10实际受测的引擎、架构及脚本后端，不由平台枚举推断其他组合。
            if (Application.platform == RuntimePlatform.WindowsPlayer && IntPtr.Size == 8 && Application.unityVersion == "2022.3.62f3c1") return;
#endif
            throw new ResourceException(ResourceError.Unsupported, "Release schema v1 transport/crypto requires Windows Editor or the validated Unity 2022.3.62f3c1 Windows64 Mono/IL2CPP Player backend.");
        }

        internal CertificateHandler CreateCertificateHandler()
        {
#if UNITY_EDITOR || (UNITY_STANDALONE_WIN && (ENABLE_MONO || ENABLE_IL2CPP) && DEVELOPMENT_BUILD && YL_RESOURCE_ACCEPTANCE_TESTS)
            if (loopbackCertificate != null)
            {
                if (!Endpoint.IsLoopback) throw new InvalidOperationException("Isolated certificate trust is restricted to loopback.");
                return new LoopbackCertificate(loopbackCertificate);
            }
#endif
            return null; // Unity/平台正常证书链与主机名校验；生产没有允许未知证书的开关。
        }

#if UNITY_EDITOR || (UNITY_STANDALONE_WIN && (ENABLE_MONO || ENABLE_IL2CPP) && DEVELOPMENT_BUILD && YL_RESOURCE_ACCEPTANCE_TESTS)
        /// <summary>仅Editor或显式Development验收构建的隔离TLS夹具信任；不进入普通Player，不修改系统信任。</summary>
        internal void TrustLoopbackCertificate(byte[] certificate)
        {
            if (!Endpoint.IsLoopback || loopbackCertificate != null || certificate == null || certificate.Length == 0 || certificate.Length > 16384)
                throw new InvalidOperationException("Explicit isolated loopback trust is required.");
            loopbackCertificate = (byte[])certificate.Clone();
        }
        private sealed class LoopbackCertificate : CertificateHandler
        {
            private readonly byte[] certificate;
            internal LoopbackCertificate(byte[] certificate) => this.certificate = certificate;
            protected override bool ValidateCertificate(byte[] received) => received != null && received.SequenceEqual(certificate);
        }
#endif
    }
}
