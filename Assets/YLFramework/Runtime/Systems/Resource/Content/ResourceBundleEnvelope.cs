using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace YLFramework.Services.Resource
{
    // YL C# adaptation adds the outer context envelope and ownership/failure contracts.
    internal sealed class ResourceBundleEnvelopeHeader
    {
        internal string KeyId
        {
            get;
        }
        internal long PlainLength
        {
            get;
        }
        internal string PlainSha256
        {
            get;
        }
        internal int CiphertextSegmentSize => ResourceBundleEnvelope.SegmentSize;
        internal byte[] EncodedPrefix
        {
            get;
        }
        internal long PayloadLength
        {
            get;
        }
        internal long SegmentCount
        {
            get;
        }

        internal ResourceBundleEnvelopeHeader(string keyId, long plainLength, string plainSha256, byte[] prefix)
        {
            KeyId = keyId;
            PlainLength = plainLength;
            PlainSha256 = plainSha256;
            EncodedPrefix = prefix;
            try
            {
                SegmentCount = checked((plainLength + 39) / ResourceBundleEnvelope.PlainCapacity + 1);
                if (SegmentCount > 1L << 32)
                {
                    throw new InvalidDataException("Tink segment counter limit exceeded.");
                }

                PayloadLength = checked(plainLength + 40 + SegmentCount * 16);
            }
            catch (OverflowException error)
            {
                throw new InvalidDataException("Content length overflow.", error);
            }
        }
    }

    /// <summary>独立于冻结 Catalog v1 的有界文件编码；调用方须先验证完整交付 length/SHA。</summary>
    internal static class ResourceBundleEnvelope
    {
        internal const int MaximumHeaderBytes = 4096;
        internal const int SegmentSize = 65536;
        internal const int PlainCapacity = SegmentSize - 16;
        internal const int NativeReadBufferSize = 32768;
        internal const long DataBufferBytes = SegmentSize + 2L * PlainCapacity;
        private const string Algorithm = "AES256-GCM-HKDF-SEGMENTED";
        private const string Fields = "Algorithm,CiphertextSegmentSize,FileRole,KeyId,LogicalId,PlainLength,PlainSha256,Platform,ProjectId,UnityCompatibility,Version";
        private static readonly byte[] _magic = Encoding.ASCII.GetBytes("YLRENC01");
        private static readonly byte[] _unityFs = Encoding.ASCII.GetBytes("UnityFS\0");
        private static readonly byte[] _domain = Encoding.ASCII.GetBytes("YL.Resource.BundleEnvelope/v1");
        private static readonly UTF8Encoding _utf8 = new(false, true);
        internal static bool IsSupported => CngSegmentAead.IsSupported;

        // Inspect 不认证，不获取密钥，不改变调用方游标，头不能替代可信 Catalog context。
        internal static ResourceBundleEnvelopeHeader Inspect(Stream input, ResourceBundleContext context)
        {
            RequireInput(input);
            context.RequireValid();
            long position = input.Position;
            try
            {
                input.Position = 0;
                var signature = new byte[8];
                ReadExact(input, signature, 0, signature.Length);
                if (signature.SequenceEqual(_unityFs))
                {
                    return null;
                }

                if (!signature.SequenceEqual(_magic))
                {
                    throw new InvalidDataException("Unknown resource content encoding.");
                }

                var size = new byte[4];
                ReadExact(input, size, 0, 4);
                uint count = (uint)(size[0] | size[1] << 8 | size[2] << 16 | size[3] << 24);
                if (count == 0 || count > MaximumHeaderBytes)
                {
                    throw new InvalidDataException("Content header byte limit exceeded.");
                }

                var json = new byte[(int)count];
                ReadExact(input, json, 0, json.Length);
                var data = Parse(json);
                var canonical = Canonical(data);
                if (!canonical.SequenceEqual(json))
                {
                    throw new InvalidDataException("Content header is not canonical UTF-8.");
                }

                if (!data.Properties().Select(value => value.Name).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(Fields.Split(',')))
                {
                    throw new InvalidDataException("Missing or unknown content header fields.");
                }

                if (Integer(data, "Version") != 1 || Text(data, "Algorithm") != Algorithm || Integer(data, "CiphertextSegmentSize") != SegmentSize)
                {
                    throw new InvalidDataException("Unsupported resource content format.");
                }

                var platform = Text(data, "Platform");
                if (platform != "StandaloneWindows64" && platform != "StandaloneOSX" && platform != "StandaloneLinux64")
                {
                    throw new InvalidDataException("Unknown content platform.");
                }

                var role = Text(data, "FileRole");
                if (role != "Bundle" && role != "Manifest" || role == "Manifest" && Text(data, "LogicalId") != "manifest")
                {
                    throw new InvalidDataException("Invalid content role/logical identity.");
                }

                if (Text(data, "ProjectId") != context.ProjectId || platform != context.Platform ||
                    Text(data, "UnityCompatibility") != context.UnityCompatibility || role != context.FileRole ||
                    Text(data, "LogicalId") != context.LogicalId)
                {
                    throw new InvalidDataException("Resource content context mismatch.");
                }

                var keyId = Text(data, "KeyId");
                ValidateKeyId(keyId);
                long length = Integer(data, "PlainLength");
                if (length < 0)
                {
                    throw new InvalidDataException("Negative plaintext length.");
                }

                var digest = Text(data, "PlainSha256");
                ValidateDigest(digest);
                var prefix = new byte[12 + json.Length];
                Buffer.BlockCopy(_magic, 0, prefix, 0, 8);
                Buffer.BlockCopy(size, 0, prefix, 8, 4);
                Buffer.BlockCopy(json, 0, prefix, 12, json.Length);
                var header = new ResourceBundleEnvelopeHeader(keyId, length, digest, prefix);
                if (input.Length != checked(header.PayloadLength + prefix.Length))
                {
                    throw new InvalidDataException("Content delivery length does not match the bounded envelope.");
                }

                return header;
            }
            finally
            {
                input.Position = position;
            }
        }

        internal static void Encrypt(Stream raw, Stream output, ResourceBundleContext context, string keyId,
            byte[] key, CancellationToken token = default)
        {
            RequireInput(raw);
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (!output.CanWrite || ReferenceEquals(raw, output))
            {
                throw new ArgumentException("Independent writable delivery output required.");
            }

            if (output.CanSeek && (output.Position != 0 || output.Length != 0))
            {
                throw new ArgumentException("Delivery output must be empty.");
            }

            context.RequireValid();
            RequireSupported(context);
            ValidateKeyId(keyId);
            ValidateKey(key);
            token.ThrowIfCancellationRequested();
            var plain = new byte[PlainCapacity];
            var cipher = new byte[SegmentSize];
            var tinkHeader = new byte[40];
            var nonce = new byte[12];
            byte[] fileKey = null;
            CngSegmentAead aead = null;
            try
            {
                long length = raw.Length;
                string digest = HashRaw(raw, length, plain, token);
                var data = new JObject
                {
                    ["Version"] = 1,
                    ["Algorithm"] = Algorithm,
                    ["CiphertextSegmentSize"] = SegmentSize,
                    ["KeyId"] = keyId,
                    ["ProjectId"] = context.ProjectId,
                    ["Platform"] = context.Platform,
                    ["UnityCompatibility"] = context.UnityCompatibility,
                    ["FileRole"] = context.FileRole,
                    ["LogicalId"] = context.LogicalId,
                    ["PlainLength"] = length,
                    ["PlainSha256"] = digest
                };
                var json = Canonical(data);
                var prefix = new byte[12 + json.Length];
                Buffer.BlockCopy(_magic, 0, prefix, 0, 8);
                for (int i = 0; i < 4; ++i)
                {
                    prefix[8 + i] = (byte)(json.Length >> (8 * i));
                }

                Buffer.BlockCopy(json, 0, prefix, 12, json.Length);
                var header = new ResourceBundleEnvelopeHeader(keyId, length, digest, prefix);
                using (var random = RandomNumberGenerator.Create())
                {
                    random.GetBytes(tinkHeader);
                }

                tinkHeader[0] = 40;
                fileKey = DeriveKey(key, prefix, tinkHeader);
                aead = new CngSegmentAead(fileKey);
                Array.Clear(fileKey, 0, fileKey.Length);
                token.ThrowIfCancellationRequested();
                output.Write(prefix, 0, prefix.Length);
                output.Write(tinkHeader, 0, tinkHeader.Length);
                raw.Position = 0;
                long remaining = length;
                using var hash = SHA256.Create();
                for (long index = 0; index < header.SegmentCount; ++index)
                {
                    token.ThrowIfCancellationRequested();
                    int count = (int)Math.Min(remaining, PlainCapacity - (index == 0 ? 40 : 0));
                    ReadExact(raw, plain, 0, count, token);
                    hash.TransformBlock(plain, 0, count, plain, 0);
                    SetNonce(tinkHeader, index, index == header.SegmentCount - 1, nonce);
                    int written = aead.Encrypt(plain, count, nonce, cipher);
                    output.Write(cipher, 0, written);
                    Array.Clear(plain, 0, count);
                    remaining -= count;
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                if (raw.Length != length || raw.ReadByte() != -1 || Digest(hash.Hash) != digest)
                {
                    throw new InvalidDataException("Raw content changed while producing delivery bytes.");
                }

                token.ThrowIfCancellationRequested();
            }
            finally
            {
                Array.Clear(plain, 0, plain.Length);
                Array.Clear(cipher, 0, cipher.Length);
                Array.Clear(nonce, 0, nonce.Length);
                if (fileKey != null)
                {
                    Array.Clear(fileKey, 0, fileKey.Length);
                }

                aead?.Dispose();
            }
        }

        internal static ResourceDecryptStream OpenDecrypt(Stream verifiedStream, ResourceBundleContext context,
            byte[] key, bool leaveOpen = false, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            var header = Inspect(verifiedStream, context) ?? throw new InvalidDataException("An encrypted resource envelope is required.");
            ValidateKey(key);
            RequireSupported(context);
            return new ResourceDecryptStream(verifiedStream, header, key, leaveOpen, token);
        }

        internal static void ValidateAll(Stream verifiedStream, ResourceBundleContext context, byte[] key,
            CancellationToken token = default, string expectedPlainSha256 = null, long? expectedPlainLength = null)
        {
            token.ThrowIfCancellationRequested();
            var header = Inspect(verifiedStream, context);
            if (expectedPlainSha256 != null)
            {
                ValidateDigest(expectedPlainSha256);
            }

            if (header == null)
            {
                if (expectedPlainLength.HasValue && verifiedStream.Length != expectedPlainLength.Value)
                {
                    throw new InvalidDataException("Raw build/delivery length mismatch.");
                }

                if (expectedPlainSha256 != null)
                {
                    var buffer = new byte[NativeReadBufferSize];
                    try
                    {
                        if (HashRaw(verifiedStream, verifiedStream.Length, buffer, token) != expectedPlainSha256)
                        {
                            throw new InvalidDataException("Raw build/delivery digest mismatch.");
                        }
                    }
                    finally
                    {
                        Array.Clear(buffer, 0, buffer.Length);
                    }
                }
                return;
            }
            if (expectedPlainLength.HasValue && expectedPlainLength.Value != header.PlainLength ||
                expectedPlainSha256 != null && expectedPlainSha256 != header.PlainSha256)
            {
                throw new InvalidDataException("Encrypted delivery does not match the independent raw build identity.");
            }

            using var stream = OpenDecrypt(verifiedStream, context, key, true, token);
            stream.ValidateAll(token);
        }

        internal static void ValidateKeyId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64 || value.Any(character =>
                !(character >= 'a' && character <= 'z' || character >= 'A' && character <= 'Z' ||
                  character >= '0' && character <= '9' || character == '.' || character == '_' || character == '-')))
            {
                throw new InvalidDataException("KeyId must be 1..64 ASCII letters, digits, '.', '_' or '-'.");
            }
        }

        internal static void ValidateKey(byte[] key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length != 32)
            {
                throw new ArgumentException("Content key copies must contain exactly 32 bytes.", nameof(key));
            }
        }

        internal static void ReadExact(Stream input, byte[] buffer, int offset, int count, CancellationToken token = default)
        {
            while (count > 0)
            {
                token.ThrowIfCancellationRequested();
                int got = input.Read(buffer, offset, Math.Min(count, NativeReadBufferSize));
                if (got <= 0 || got > Math.Min(count, NativeReadBufferSize))
                {
                    throw new InvalidDataException("Truncated or invalid resource content read.");
                }

                offset += got;
                count -= got;
            }
        }

        internal static byte[] DeriveKey(byte[] key, byte[] prefix, byte[] tinkHeader)
        {
            var salt = new byte[32];
            Buffer.BlockCopy(tinkHeader, 1, salt, 0, salt.Length);
            var info = new byte[_domain.Length + prefix.Length];
            Buffer.BlockCopy(_domain, 0, info, 0, _domain.Length);
            Buffer.BlockCopy(prefix, 0, info, _domain.Length, prefix.Length);
            var derived = new byte[32];
            try
            {
                var hkdf = new HkdfBytesGenerator(new Sha256Digest());
                hkdf.Init(new HkdfParameters(key, salt, info));
                hkdf.GenerateBytes(derived, 0, derived.Length);
                return derived;
            }
            catch
            {
                Array.Clear(derived, 0, derived.Length);
                throw;
            }
            finally
            {
                Array.Clear(salt, 0, salt.Length);
                Array.Clear(info, 0, info.Length);
            }
        }

        internal static void SetNonce(byte[] header, long index, bool last, byte[] nonce)
        {
            if (index < 0 || index > uint.MaxValue)
            {
                throw new InvalidDataException("Tink segment counter overflow.");
            }

            Buffer.BlockCopy(header, 33, nonce, 0, 7);
            nonce[7] = (byte)(index >> 24);
            nonce[8] = (byte)(index >> 16);
            nonce[9] = (byte)(index >> 8);
            nonce[10] = (byte)index;
            nonce[11] = last ? (byte)1 : (byte)0;
        }

        internal static string Digest(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private static void RequireSupported(ResourceBundleContext context)
        {
            if (!IsSupported || context.Platform != "StandaloneWindows64")
            {
                throw new PlatformNotSupportedException("Encrypted Resource content requires Windows x64 Editor or a validated Windows64 Mono/IL2CPP Player backend.");
            }
        }

        private static void RequireInput(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (!input.CanRead || !input.CanSeek)
            {
                throw new ArgumentException("A readable, seekable, exclusively owned input is required.");
            }
        }

        private static string HashRaw(Stream raw, long length, byte[] buffer, CancellationToken token)
        {
            raw.Position = 0;
            using var hash = SHA256.Create();
            long remaining = length;
            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested();
                int count = (int)Math.Min(remaining, buffer.Length);
                ReadExact(raw, buffer, 0, count, token);
                hash.TransformBlock(buffer, 0, count, buffer, 0);
                remaining -= count;
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            if (raw.Length != length || raw.ReadByte() != -1)
            {
                throw new InvalidDataException("Raw content length changed.");
            }

            return Digest(hash.Hash);
        }

        private static JObject Parse(byte[] json)
        {
            try
            {
                using var text = new StringReader(_utf8.GetString(json));
                using var reader = new JsonTextReader(text) { MaxDepth = 4, DateParseHandling = DateParseHandling.None };
                var data = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Load
                });
                if (reader.Read())
                {
                    throw new InvalidDataException("Additional content header JSON.");
                }

                return data;
            }
            catch (Exception error) when (error is JsonException || error is DecoderFallbackException)
            {
                throw new InvalidDataException("Invalid bounded content header JSON.", error);
            }
        }

        private static byte[] Canonical(JObject data)
        {
            var sorted = new JObject(data.Properties().OrderBy(value => value.Name, StringComparer.Ordinal)
                .Select(value => new JProperty(value.Name, value.Value.DeepClone())));
            var builder = new StringBuilder();
            using (var writer = new JsonTextWriter(new StringWriter(builder, CultureInfo.InvariantCulture))
            {
                Formatting = Formatting.None,
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                Culture = CultureInfo.InvariantCulture
            })
            {
                sorted.WriteTo(writer);
            }

            var bytes = _utf8.GetBytes(builder.ToString());
            if (bytes.Length > MaximumHeaderBytes)
            {
                throw new InvalidDataException("Content header byte limit exceeded.");
            }

            return bytes;
        }

        private static string Text(JObject data, string name)
        {
            if (data[name]?.Type != JTokenType.String)
            {
                throw new InvalidDataException("Expected content string: " + name);
            }

            string value = (string)data[name];
            if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value != value.Trim() || value.Any(char.IsControl))
            {
                throw new InvalidDataException("Invalid bounded content string: " + name);
            }

            return value;
        }

        private static long Integer(JObject data, string name)
        {
            try
            {
                if (data[name]?.Type != JTokenType.Integer)
                {
                    throw new InvalidDataException("Expected strict content integer: " + name);
                }

                return (long)data[name];
            }
            catch (Exception error) when (error is OverflowException || error is InvalidCastException)
            {
                throw new InvalidDataException("Content integer overflow: " + name, error);
            }
        }

        private static void ValidateDigest(string digest)
        {
            if (digest == null || digest.Length != 64 || digest.Any(value => !(value >= '0' && value <= '9' || value >= 'a' && value <= 'f')))
            {
                throw new InvalidDataException("Expected canonical lowercase SHA-256.");
            }
        }
    }
}
