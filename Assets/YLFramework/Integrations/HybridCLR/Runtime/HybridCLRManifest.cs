using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using YLFramework.Services.Resource;

namespace YLFramework.Integrations.HybridCLR
{
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class HybridCLRPayload
    {
        [JsonProperty(Order = 0)]
        public string Name { get; set; }
        [JsonProperty(Order = 1)]
        public string Identity { get; set; }
        [JsonProperty(Order = 2)]
        public string Mvid { get; set; }
        [JsonProperty(Order = 3)]
        public string Address { get; set; }
        [JsonProperty(Order = 4)]
        public long Length { get; set; }
        [JsonProperty(Order = 5)]
        public string Sha256 { get; set; }
        [JsonProperty(Order = 6)]
        public string[] Dependencies { get; set; } = Array.Empty<string>();
        [JsonProperty(Order = 7)]
        public string[] References { get; set; } = Array.Empty<string>();
    }

    /// <summary>同一 Resource Project 内的代码描述；所有检查均可在不可逆 native 加载前执行。</summary>
    [JsonObject(ItemRequired = Required.Always)]
    public sealed class HybridCLRManifest
    {
        public const int MaximumManifestBytes = 1024 * 1024;
        public const int MaximumAssemblyCount = 128;
        public const long MaximumPayloadBytes = 64L * 1024 * 1024;
        public const long MaximumTotalBytes = 256L * 1024 * 1024;
        public const string ManifestPath = "hybridclr/manifest.json";

        [JsonProperty(Order = 0)]
        public int SchemaVersion { get; set; } = 1;
        [JsonProperty(Order = 1)]
        public string ProjectId { get; set; }
        [JsonProperty(Order = 2)]
        public string Platform { get; set; } = "StandaloneWindows64";
        [JsonProperty(Order = 3)]
        public string UnityVersion { get; set; }
        [JsonProperty(Order = 4)]
        public string HybridCLRVersion { get; set; } = "9.0.3";
        [JsonProperty(Order = 5)]
        public bool Development { get; set; }
        [JsonProperty(Order = 6)]
        public string AppContentCompatibilityId { get; set; }
        // Seal 计算摘要时尚无 ID；解析后的完整性仍由 ValidatePackageId 强制检查。
        [JsonProperty(Order = 7, Required = Required.AllowNull)]
        public string CodePackageId { get; set; }
        [JsonProperty(Order = 8)]
        public HybridCLRPayload[] HotUpdateAssemblies { get; set; } = Array.Empty<HybridCLRPayload>();
        [JsonProperty(Order = 9)]
        public HybridCLRPayload[] AotMetadata { get; set; } = Array.Empty<HybridCLRPayload>();

        private static JsonSerializerSettings Settings() => new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
            MaxDepth = 32
        };

        public static HybridCLRManifest Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumManifestBytes)
                throw new InvalidDataException("Missing or oversized HybridCLR manifest.");
            try
            {
                using var text = new StringReader(json);
                using var reader = new JsonTextReader(text) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
                var token = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("Trailing data after the HybridCLR manifest.");
                var manifest = token.ToObject<HybridCLRManifest>(JsonSerializer.Create(Settings()));
                manifest.ValidateStructure(true);
                manifest.ValidatePackageId();
                return manifest;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid HybridCLR manifest JSON.", exception);
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None, Settings());

        /// <summary>仅构建端使用；排序后计算不包含自身 ID 的规范化描述摘要。</summary>
        public void Seal()
        {
            ValidateStructure(false);
            foreach (var payload in HotUpdateAssemblies.Concat(AotMetadata))
            {
                payload.Dependencies = payload.Dependencies.OrderBy(n => n, StringComparer.Ordinal).ToArray();
                payload.References = payload.References.OrderBy(n => n, StringComparer.Ordinal).ToArray();
            }
            HotUpdateAssemblies = DependencyOrder();
            AotMetadata = AotMetadata.OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
            CodePackageId = ComputePackageId();
        }

        public void Validate(string projectId, string appContentCompatibilityId, string unityVersion, bool development)
        {
            ValidateStructure(true);
            if (ProjectId != projectId || AppContentCompatibilityId != appContentCompatibilityId ||
                UnityVersion != unityVersion || Development != development)
                throw new InvalidDataException("HybridCLR package does not match the requested project, base, Unity version or Development profile.");
            ValidatePackageId();
        }

        public static void ValidatePayload(HybridCLRPayload payload, byte[] bytes)
        {
            if (payload == null || bytes == null || bytes.LongLength != payload.Length ||
                bytes.LongLength == 0 || bytes.LongLength > MaximumPayloadBytes || Hash(bytes) != payload.Sha256)
                throw new InvalidDataException("HybridCLR payload length or SHA256 mismatch: " + payload?.Name);
        }

        public static string Hash(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        public static string PayloadAddress(string name, bool aot) => "hybridclr/" + (aot ? "metadata/" : "assemblies/") + name + ".dll.bytes";

        private string ComputePackageId()
        {
            var description = JObject.FromObject(this, JsonSerializer.Create(Settings()));
            description.Remove("codePackageId");
            return Hash(Encoding.UTF8.GetBytes(description.ToString(Formatting.None)));
        }

        private void ValidatePackageId()
        {
            if (!IsHash(CodePackageId) || CodePackageId != ComputePackageId())
                throw new InvalidDataException("HybridCLR code package identity does not match its descriptor and payload hashes.");
        }

        private void ValidateStructure(bool ordered)
        {
            if (SchemaVersion != 1 || HybridCLRVersion != "9.0.3" || Platform != "StandaloneWindows64" ||
                string.IsNullOrWhiteSpace(UnityVersion) || UnityVersion.Length > 64)
                throw new InvalidDataException("Unsupported HybridCLR schema, package version, platform or Unity version.");
            try
            {
                _ = new ResourceAddress(ProjectId, ManifestPath);
                _ = new ResourceAddress(AppContentCompatibilityId, ManifestPath);
            }
            catch (ArgumentException exception) { throw new InvalidDataException("Invalid HybridCLR project or base identity.", exception); }
            if (HotUpdateAssemblies == null || AotMetadata == null || HotUpdateAssemblies.Length == 0 ||
                HotUpdateAssemblies.Length > MaximumAssemblyCount || AotMetadata.Length > MaximumAssemblyCount)
                throw new InvalidDataException("Invalid HybridCLR assembly count.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var payload in HotUpdateAssemblies.Concat(AotMetadata))
            {
                if (payload == null || string.IsNullOrEmpty(payload.Name) || !names.Add(payload.Name))
                    throw new InvalidDataException("Missing, duplicate or case-colliding HybridCLR assembly.");
                try { _ = new ResourceAddress(payload.Name, ManifestPath); }
                catch (ArgumentException exception) { throw new InvalidDataException("Invalid assembly name.", exception); }
                var identity = ReadIdentity(payload.Identity);
                var hasMvid = Guid.TryParseExact(payload.Mvid, "D", out var mvid) && mvid != Guid.Empty;
                if (identity.Name != payload.Name || identity.Version == null || identity.FullName != payload.Identity ||
                    !hasMvid)
                    throw new InvalidDataException("Invalid HybridCLR assembly identity or MVID: " + payload.Name +
                        "; declared=" + payload.Identity + "; parsed=" + identity.FullName + "; parsedName=" + identity.Name +
                        "; parsedVersion=" + identity.Version + "; mvid=" + payload.Mvid + "; validMvid=" + hasMvid);
                if (payload.Length <= 0 || payload.Length > MaximumPayloadBytes || !IsHash(payload.Sha256))
                    throw new InvalidDataException("Invalid HybridCLR payload size or hash: " + payload.Name);
                total += payload.Length;
                ValidateUniqueStrings(payload.Dependencies, payload.Name);
                ValidateUniqueStrings(payload.References, payload.Name);
                foreach (var reference in payload.References) ReadIdentity(reference);
                if (ordered && (!payload.Dependencies.SequenceEqual(payload.Dependencies.OrderBy(n => n, StringComparer.Ordinal)) ||
                    !payload.References.SequenceEqual(payload.References.OrderBy(n => n, StringComparer.Ordinal))))
                    throw new InvalidDataException("HybridCLR dependency descriptions must be canonical.");
            }
            if (total > MaximumTotalBytes) throw new InvalidDataException("HybridCLR payload budget exceeded.");

            var hot = HotUpdateAssemblies.ToDictionary(p => p.Name, StringComparer.Ordinal);
            foreach (var payload in HotUpdateAssemblies)
            {
                if (payload.Address != PayloadAddress(payload.Name, false))
                    throw new InvalidDataException("HybridCLR assembly address escaped its owned project path: " + payload.Name);
                foreach (var dependency in payload.Dependencies)
                    if (!hot.ContainsKey(dependency)) throw new InvalidDataException("Missing hot-update dependency: " + dependency);
                var expected = payload.References.Select(ReadIdentity).Where(r => hot.ContainsKey(r.Name)).ToArray();
                if (expected.Any(r => hot[r.Name].Identity != r.FullName) ||
                    !expected.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal)
                        .SequenceEqual(payload.Dependencies.OrderBy(n => n, StringComparer.Ordinal)))
                    throw new InvalidDataException("Hot-update dependency identity mismatch: " + payload.Name);
            }
            foreach (var payload in AotMetadata)
                if (payload.Address != PayloadAddress(payload.Name, true) || payload.Dependencies.Length != 0 ||
                    payload.References.Any(r => hot.ContainsKey(ReadIdentity(r).Name)))
                    throw new InvalidDataException("Invalid AOT address or AOT-to-hot-update reference: " + payload.Name);
            var dependencyOrder = DependencyOrder();
            if (ordered && (!HotUpdateAssemblies.Select(p => p.Name).SequenceEqual(dependencyOrder.Select(p => p.Name)) ||
                !AotMetadata.Select(p => p.Name).SequenceEqual(AotMetadata.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal))))
                throw new InvalidDataException("HybridCLR assemblies are not in stable dependency-first order.");
        }

        private HybridCLRPayload[] DependencyOrder()
        {
            var remaining = HotUpdateAssemblies.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            var loaded = new HashSet<string>(StringComparer.Ordinal);
            var ordered = new List<HybridCLRPayload>();
            while (remaining.Count != 0)
            {
                var next = remaining.FirstOrDefault(p => p.Dependencies.All(loaded.Contains));
                if (next == null) throw new InvalidDataException("Cyclic hot-update assembly dependencies.");
                remaining.Remove(next); loaded.Add(next.Name); ordered.Add(next);
            }
            return ordered.ToArray();
        }

        private static bool IsHash(string value) => value != null && value.Length == 64 &&
            value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

        private static void ValidateUniqueStrings(string[] values, string assembly)
        {
            if (values == null || values.Length > 512 || values.Any(string.IsNullOrWhiteSpace) ||
                values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
                throw new InvalidDataException("Missing, duplicate or oversized dependencies: " + assembly);
        }

        internal static string CanonicalIdentity(AssemblyName identity)
        {
            if (identity.CultureInfo == null) identity.CultureInfo = CultureInfo.InvariantCulture;
            return identity.FullName;
        }

        private static AssemblyName ReadIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 1024) throw new InvalidDataException("Missing or oversized assembly identity.");
            try
            {
                var identity = new AssemblyName(value);
                // Unity IL2CPP 将显式 neutral 转为 null；只还原这条精确语义，不放宽身份比较。
                if (value.IndexOf(", Culture=neutral, ", StringComparison.Ordinal) >= 0 && identity.CultureInfo == null)
                    CanonicalIdentity(identity);
                return identity;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is System.IO.FileLoadException)
            { throw new InvalidDataException("Invalid assembly identity: " + value, exception); }
        }
    }
}
