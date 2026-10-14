using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YLFramework.Services.Resource
{
    /// <summary>有界固定 Schema codec。v1 已可读写；最终冻结仍须 8.2c 生成消费门禁，不提供旧格式回退。</summary>
    public static class ResourceCatalogCodec
    {
        public const int MaximumBytes = 16 * 1024 * 1024;
        private const int Limit = 16384;
        private static readonly UTF8Encoding Utf8 = new(false, true);
        private const string Header = "SchemaVersion,ProjectId,Platform,UnityCompatibility,AppContentCompatibilityId,BuildSettingsFingerprint,ManifestFile,Bundles,Assets,Groups,SupportFiles";

        /// <summary>验证无 BuildId 的固定 payload，排序并计算身份，再写入最终规范字节。</summary>
        public static byte[] Seal(byte[] payload)
        {
            var data = ParseObject(payload);
            Validate(data, false);
            SortCollections(data);
            data["BuildId"] = Digest(CanonicalBytes(data));
            return CanonicalBytes(data);
        }

        /// <summary>严格读回，精确匹配调用方显式兼容值；不能省略兼容检查或把 digest 当签名。</summary>
        public static ResourceCatalogDocument Read(byte[] bytes, string projectId, string platform,
            string unityCompatibility, string appContentCompatibilityId)
        {
            var data = ParseObject(bytes);
            Validate(data, true);
            if ((string)data["ProjectId"] != projectId || (string)data["Platform"] != platform ||
                (string)data["UnityCompatibility"] != unityCompatibility || (string)data["AppContentCompatibilityId"] != appContentCompatibilityId)
                throw new InvalidDataException("Catalog project/platform/Unity/app compatibility mismatch.");
            SortCollections(data);
            var buildId = (string)data["BuildId"];
            data.Remove("BuildId");
            if (Digest(CanonicalBytes(data)) != buildId) throw new InvalidDataException("Catalog BuildId does not match its payload.");
            data["BuildId"] = buildId;
            var canonical = CanonicalBytes(data);
            if (!bytes.SequenceEqual(canonical)) throw new InvalidDataException("Catalog bytes are not canonical v1 UTF-8.");
            return new ResourceCatalogDocument(data, canonical);
        }

        public static byte[] Write(ResourceCatalogDocument document) =>
            (document ?? throw new ArgumentNullException(nameof(document))).CopyBytes();

        public static string Digest(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        // 不做 CLR 类型反序列化：所有字段逐项读取，$type 等未知键被拒绝，等价于 TypeNameHandling.None。
        internal static JObject ParseObject(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes) throw new InvalidDataException("JSON byte limit exceeded or empty input.");
            try
            {
                using var text = new StringReader(Utf8.GetString(bytes));
                using var reader = new JsonTextReader(text) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
                var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Load });
                if (reader.Read()) throw new InvalidDataException("Additional JSON content.");
                return data;
            }
            catch (Exception error) when (error is JsonException || error is DecoderFallbackException)
            { throw new InvalidDataException("Invalid bounded JSON.", error); }
        }

        internal static byte[] CanonicalBytes(JToken data)
        {
            JToken Ordered(JToken token) => token switch
            {
                JObject obj => new JObject(obj.Properties().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => new JProperty(x.Name, Ordered(x.Value)))),
                JArray array => new JArray(array.Select(Ordered)),
                _ => token.DeepClone()
            };
            var builder = new StringBuilder();
            using (var writer = new JsonTextWriter(new StringWriter(builder, CultureInfo.InvariantCulture))
                { Formatting = Formatting.None, StringEscapeHandling = StringEscapeHandling.EscapeNonAscii, Culture = CultureInfo.InvariantCulture })
                Ordered(data).WriteTo(writer);
            var bytes = Utf8.GetBytes(builder.ToString());
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Canonical JSON byte limit exceeded.");
            return bytes;
        }

        internal static void Fields(JObject data, string names)
        {
            if (data == null || !data.Properties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(names.Split(',').OrderBy(x => x, StringComparer.Ordinal)))
                throw new InvalidDataException("Missing or unknown JSON fields.");
        }
        internal static string Text(JObject data, string key, bool nullable = false)
        {
            var token = data[key];
            if (nullable && token?.Type == JTokenType.Null) return null;
            if (token?.Type != JTokenType.String) throw new InvalidDataException("Expected string: " + key);
            var value = (string)token;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value != value.Trim() || value.Any(char.IsControl))
                throw new InvalidDataException("Invalid bounded string: " + key);
            return value;
        }
        internal static long Integer(JObject data, string key)
        {
            try
            {
                if (data[key]?.Type != JTokenType.Integer) throw new InvalidDataException("Expected integer: " + key);
                return (long)data[key];
            }
            catch (Exception error) when (error is OverflowException || error is InvalidCastException)
            { throw new InvalidDataException("Invalid integer: " + key, error); }
        }
        internal static bool Boolean(JObject data, string key)
        {
            if (data[key]?.Type != JTokenType.Boolean) throw new InvalidDataException("Expected boolean: " + key);
            return (bool)data[key];
        }
        private static JArray Items(JObject data, string key, bool nonempty = false)
        {
            if (data[key] is not JArray array || array.Count > Limit || nonempty && array.Count == 0)
                throw new InvalidDataException("Invalid bounded collection: " + key);
            return array;
        }
        private static JObject Object(JToken token) => token as JObject ?? throw new InvalidDataException("Expected object entry.");
        internal static void Hex(string value, int length)
        {
            if (value == null || value.Length != length || value.Any(x => !(x >= '0' && x <= '9' || x >= 'a' && x <= 'f')))
                throw new InvalidDataException("Expected canonical lowercase hex identity.");
        }
        internal static void Id(string value)
        {
            if (value?.Length > 128) throw new InvalidDataException("Identifier length exceeded.");
            try { ResourceAddress.ValidateId(value, nameof(value)); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid stable identifier.", error); }
        }
        private static void PathValue(string value)
        {
            try { ResourceAddress.ValidatePath(value, nameof(value)); }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid catalog address/path.", error); }
        }
        private static void Native(string value, string prefix)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal) || value.Length <= prefix.Length ||
                value.Any(x => !(x >= 'a' && x <= 'z' || x >= 'A' && x <= 'Z' || x >= '0' && x <= '9' || x == '_')))
                throw new InvalidDataException("Bundle file/native identity is outside its YL project namespace.");
        }

        private static void Validate(JObject data, bool sealedCatalog)
        {
            Fields(data, Header + (sealedCatalog ? ",BuildId" : ""));
            if (Integer(data, "SchemaVersion") != 1) throw new InvalidDataException("Unsupported Catalog SchemaVersion.");
            var project = Text(data, "ProjectId"); Id(project);
            var platform = Text(data, "Platform");
            if (!new[] { "StandaloneWindows64", "StandaloneOSX", "StandaloneLinux64" }.Contains(platform))
                throw new InvalidDataException("Unsupported catalog platform.");
            Text(data, "UnityCompatibility"); Id(Text(data, "AppContentCompatibilityId"));
            Hex(Text(data, "BuildSettingsFingerprint"), 64);
            if (sealedCatalog) Hex(Text(data, "BuildId"), 64);
            var prefix = "ylr_" + Digest(Utf8.GetBytes(project)).Substring(0, 32) + "_";
            var manifest = Text(data, "ManifestFile");
            if (manifest != "manifest") throw new InvalidDataException("Invalid manifest file locator.");
            var bundles = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Items(data, "Bundles", true))
            {
                var bundle = Object(item);
                Fields(bundle, "LogicalId,NativeName,RelativeFile,Length,Sha256,UnityHash,IsScene,Dependencies");
                var id = Text(bundle, "LogicalId"); Native(id, prefix);
                if (Text(bundle, "NativeName") != id || Text(bundle, "RelativeFile") != "b_" + Digest(Utf8.GetBytes(id)).Substring(0, 32) || bundles.ContainsKey(id))
                    throw new InvalidDataException("Bundle logical/native/file identity conflict.");
                bundles.Add(id, bundle);
                FileRecord(bundle, files);
                Hex(Text(bundle, "UnityHash"), 32); Boolean(bundle, "IsScene");
                foreach (var edge in Items(bundle, "Dependencies"))
                    if (edge.Type != JTokenType.String) throw new InvalidDataException("Expected dependency identifier.");
            }
            try
            {
                _ = new ResourceCatalog(project, Array.Empty<ResourceCatalogEntry>(), bundles.Select(x =>
                    new ResourceDependencyNode(x.Key, ((JArray)x.Value["Dependencies"]).Select(a => (string)a))));
            }
            catch (ArgumentException error) { throw new InvalidDataException("Invalid Catalog Bundle DAG.", error); }
            var expectedSupport = new HashSet<string>(bundles.Values.Select(x => (string)x["RelativeFile"] + ".manifest"), StringComparer.Ordinal)
                { manifest, manifest + ".manifest" };
            foreach (var item in Items(data, "SupportFiles", true))
            {
                var file = Object(item); Fields(file, "RelativeFile,Length,Sha256");
                FileRecord(file, files);
                if (!expectedSupport.Remove(Text(file, "RelativeFile"))) throw new InvalidDataException("Unknown support file.");
            }
            if (expectedSupport.Count != 0) throw new InvalidDataException("Missing manifest/support files.");
            var assets = new Dictionary<string, JObject>(StringComparer.Ordinal);
            var addresses = new HashSet<(string, string)>();
            var mainFiles = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            var mainGuids = new HashSet<string>(StringComparer.Ordinal);
            var subIdentities = new HashSet<(string, string, string, string)>();
            foreach (var item in Items(data, "Assets", true))
            {
                var asset = Object(item);
                Fields(asset, "AssetId,Guid,LocalId,Address,SubAssetKey,BundleId,LoadName,AssetType,Kind,SubAssetName");
                var guid = Text(asset, "Guid"); Hex(guid, 32);
                var local = Integer(asset, "LocalId");
                var id = Text(asset, "AssetId");
                if (local == 0 || id != guid + "_" + local.ToString(CultureInfo.InvariantCulture) || assets.ContainsKey(id))
                    throw new InvalidDataException("Invalid or duplicate canonical Unity asset identity.");
                assets.Add(id, asset);
                var address = Text(asset, "Address"); PathValue(address);
                var sub = Text(asset, "SubAssetKey", true);
                if (sub != null) PathValue(sub);
                if (!addresses.Add((address.ToUpperInvariant(), sub?.ToUpperInvariant()))) throw new InvalidDataException("Case-folded address conflict.");
                var bundle = Text(asset, "BundleId");
                if (!bundles.TryGetValue(bundle, out var owner)) throw new InvalidDataException("Unknown asset Bundle.");
                var kind = Text(asset, "Kind");
                var load = Text(asset, "LoadName"); PathValue(load);
                var type = Text(asset, "AssetType");
                if (type.Contains(",") || type.Contains("[") || type.Contains(" ")) throw new InvalidDataException("AssetType must be a descriptive full name, not an AQN.");
                var subName = Text(asset, "SubAssetName", true);
                if (kind == "SubAsset")
                {
                    if ((bool)owner["IsScene"] || sub != local.ToString(CultureInfo.InvariantCulture) || subName == null ||
                        !subIdentities.Add((bundle, load, type, subName))) throw new InvalidDataException("Ambiguous or invalid subasset identity.");
                }
                else if (kind == "Asset" || kind == "Scene")
                {
                    if (sub != null || subName != null || (kind == "Scene") != (bool)owner["IsScene"] || mainFiles.ContainsKey(load) || !mainGuids.Add(guid))
                        throw new InvalidDataException("Invalid main asset/Scene descriptor.");
                    mainFiles.Add(load, asset);
                }
                else throw new InvalidDataException("Unknown asset kind.");
                if (kind == "Scene" ? !load.StartsWith("Assets/", StringComparison.Ordinal) || !load.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) :
                    !load.StartsWith("assets/", StringComparison.Ordinal) || load != load.ToLowerInvariant())
                    throw new InvalidDataException("Invalid native load path casing/kind.");
            }
            foreach (var asset in assets.Values.Where(x => (string)x["Kind"] == "SubAsset"))
                if (!mainFiles.TryGetValue((string)asset["LoadName"], out var main) || (string)main["Guid"] != (string)asset["Guid"] ||
                    (string)main["Address"] != (string)asset["Address"] || (string)main["BundleId"] != (string)asset["BundleId"])
                    throw new InvalidDataException("Subasset is not bound to its canonical main asset.");
            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Items(data, "Groups", true))
            {
                var group = Object(item); Fields(group, "Id,Members");
                var id = Text(group, "Id"); Id(id);
                if (!groups.Add(id)) throw new InvalidDataException("Case-folded Group conflict.");
                var members = new HashSet<string>(StringComparer.Ordinal);
                foreach (var member in Items(group, "Members", true))
                {
                    var entry = Object(member); Fields(entry, "AssetId,Required"); Boolean(entry, "Required");
                    var asset = Text(entry, "AssetId");
                    if (!assets.ContainsKey(asset) || !members.Add(asset)) throw new InvalidDataException("Unknown or duplicate Group member.");
                }
            }
        }

        private static void FileRecord(JObject data, HashSet<string> files)
        {
            var name = Text(data, "RelativeFile");
            if (name.IndexOfAny(new[] { '/', '\\', ':', '%', '?' }) >= 0 || name == "." || name == ".." || !files.Add(name))
                throw new InvalidDataException("Unsafe or conflicting catalog file.");
            if (Integer(data, "Length") <= 0) throw new InvalidDataException("Empty/invalid Bundle/support file length.");
            Hex(Text(data, "Sha256"), 64);
        }

        private static void SortCollections(JObject data)
        {
            void Sort(JObject owner, string key, string field) => owner[key] = new JArray(((JArray)owner[key]).OrderBy(x => (string)x[field], StringComparer.Ordinal));
            Sort(data, "Bundles", "LogicalId"); Sort(data, "Assets", "AssetId"); Sort(data, "Groups", "Id"); Sort(data, "SupportFiles", "RelativeFile");
            foreach (var bundle in ((JArray)data["Bundles"]).Cast<JObject>())
                bundle["Dependencies"] = new JArray(((JArray)bundle["Dependencies"]).OrderBy(x => (string)x, StringComparer.Ordinal));
            foreach (var group in ((JArray)data["Groups"]).Cast<JObject>()) Sort(group, "Members", "AssetId");
        }
    }
}
