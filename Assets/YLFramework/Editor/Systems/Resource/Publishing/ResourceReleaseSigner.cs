using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>签名只读取已发布的完整制品；RSA由调用方/外部安全提供者拥有，不导出或保存私钥。</summary>
    public static class ResourceReleaseSigner
    {
        public static byte[] Create(ResourceRelease release, string channel, long sequence, string keyId, RSA signingKey, DateTimeOffset expires)
        {
            if (release == null || signingKey == null) throw new ArgumentNullException(nameof(release));
            ResourceCatalogCodec.Id(channel); ResourceCatalogCodec.Id(keyId);
            var catalog = release.Catalog;
            // 对实际发布字节重新校验，不仅签内存中的Catalog或mtime。
            var snapshot = ResourceLocalSnapshot.Open(release.DirectoryPath, catalog.ProjectId, catalog.CatalogDigest, catalog.Platform, catalog.AppContentCompatibilityId);
            var complete = File.ReadAllBytes(Path.Combine(snapshot.DirectoryPath, ".ylr-complete.json"));
            var now = DateTimeOffset.UtcNow;
            if (sequence < 1 || expires <= now || expires - now > TimeSpan.FromDays(31) || signingKey.KeySize < 2048 || signingKey.KeySize > 8192)
                throw new ArgumentException("A positive sequence, 1-31 day validity and RSA-2048+ key are required.");
            var payload = new JObject { ["ProjectId"] = catalog.ProjectId, ["Platform"] = catalog.Platform, ["UnityCompatibility"] = catalog.UnityCompatibility,
                ["AppContentCompatibilityId"] = catalog.AppContentCompatibilityId, ["Channel"] = channel, ["ReleaseSequence"] = sequence,
                ["CatalogDigest"] = catalog.CatalogDigest, ["CatalogLength"] = ResourceCatalogCodec.Write(catalog).LongLength, ["BuildId"] = catalog.BuildId,
                ["CompleteLength"] = complete.LongLength, ["CompleteSha256"] = ResourceCatalogCodec.Digest(complete),
                ["IssuedUnixSeconds"] = now.ToUnixTimeSeconds(), ["ExpiresUnixSeconds"] = expires.ToUnixTimeSeconds() };
            var envelope = new JObject { ["SchemaVersion"] = 1, ["Algorithm"] = ResourceReleaseSignature.Algorithm, ["KeyId"] = keyId,
                ["Payload"] = Convert.ToBase64String(ResourceCatalogCodec.CanonicalBytes(payload)) };
            envelope["Signature"] = Convert.ToBase64String(signingKey.SignData(ResourceCatalogCodec.CanonicalBytes(envelope), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            var bytes = ResourceCatalogCodec.CanonicalBytes(envelope);
            ResourceReleaseSignature.Verify(bytes, keyId, signingKey.ExportParameters(false));
            return bytes;
        }

        /// <summary>读取工程外PFX；路径与密码仅由环境提供，不序列化到Project、日志或Runtime。也可直接调用Create接入HSM/CI提供的RSA。</summary>
        public static byte[] CreateFromEnvironment(ResourceRelease release, string channel, long sequence, string keyId, DateTimeOffset expires)
        {
            var path = Environment.GetEnvironmentVariable("YL_RESOURCE_SIGNING_PFX");
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")) + Path.DirectorySeparatorChar;
            if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || Path.GetFullPath(path).StartsWith(project, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("YL_RESOURCE_SIGNING_PFX must select an external private PFX, outside the repository.");
            ResourceLocalSnapshot.RejectLinks(path);
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length == 0 || input.Length > 64 * 1024) throw new InvalidDataException("Signing credential exceeds its bound.");
            var bytes = new byte[(int)input.Length];
            var read = 0;
            while (read < bytes.Length) { var count = input.Read(bytes, read, bytes.Length - read); if (count == 0) throw new EndOfStreamException(); read += count; }
            try
            {
                using var certificate = new X509Certificate2(bytes, Environment.GetEnvironmentVariable("YL_RESOURCE_SIGNING_PASSWORD"), X509KeyStorageFlags.EphemeralKeySet);
                using var rsa = certificate.GetRSAPrivateKey() ?? throw new InvalidDataException("The external credential has no RSA signing key.");
                return Create(release, channel, sequence, keyId, rsa, expires);
            }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }

        /// <summary>显式新增元数据文件，不覆盖已发布目录或任何现有文件；部署由调用方负责。</summary>
        public static void Export(string path, byte[] envelope)
        {
            ResourceReleaseSignature.ReadEnvelope(envelope);
            var full = Path.GetFullPath(path);
            var repository = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!Path.IsPathFullyQualified(path) || full.StartsWith(Application.dataPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(Path.Combine(repository, "Packages") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(Path.Combine(repository, "ProjectSettings") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Export metadata outside Assets/Packages/ProjectSettings.");
            ResourceLocalSnapshot.RejectLinks(full);
            if (!Directory.Exists(Path.GetDirectoryName(full))) throw new DirectoryNotFoundException("Select an existing export directory.");
            using var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(envelope, 0, envelope.Length); stream.Flush(true);
        }
    }
}
