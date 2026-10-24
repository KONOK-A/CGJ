using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YLFramework.Resource;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    internal sealed class HybridCLRBaselineFile
    {
        public string Path;
        public long Length;
        public string Sha256;
        public string Identity;
        public string Mvid;
    }

    internal sealed class HybridCLRBuildBaseline
    {
        internal static readonly string[] GeneratedNames = { "AssemblyManifest.inc", "MethodBridge.inc", "UnityVersion.inc", "link.xml" };
        public int SchemaVersion = 1;
        public string ProfileGuid;
        public string SettingsFingerprint;
        public string PlayerBuildHash;
        public string[] NativeRequirements;
        public HybridCLRManifest InitialManifest;
        public HybridCLRBaselineFile[] Files;
        [JsonIgnore]
        public string Root
        {
            get; private set;
        }

        internal static HybridCLRBuildBaseline Read(string directory)
        {
            var root = System.IO.Path.GetFullPath(directory);
            RejectLinks(root);
            var descriptorPath = System.IO.Path.Combine(root, "baseline.json");
            var markerPath = System.IO.Path.Combine(root, "complete.json");
            if (!File.Exists(descriptorPath) || !File.Exists(markerPath))
            {
                throw new InvalidDataException("A complete persistent HybridCLR baseline is required.");
            }

            if (new FileInfo(descriptorPath).Length > 4 * 1024 * 1024 || new FileInfo(markerPath).Length > 4096)
            {
                throw new InvalidDataException("Oversized baseline receipt.");
            }

            var bytes = File.ReadAllBytes(descriptorPath);
            HybridCLRBuildBaseline baseline;
            try
            {
                var marker = ReadObject(File.ReadAllText(markerPath));
                if (marker.Properties().Count() != 2 || marker.Value<int>("schemaVersion") != 1 ||
                    marker.Value<string>("baselineSha256") != HybridCLRManifest.Hash(bytes))
                {
                    throw new InvalidDataException("The baseline completion marker does not match its receipt.");
                }

                baseline = ReadObject(Encoding.UTF8.GetString(bytes)).ToObject<HybridCLRBuildBaseline>(JsonSerializer.Create(
                    new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None }));
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid baseline JSON.", exception);
            }
            if (baseline == null || baseline.SchemaVersion != 1 || !IsHash(baseline.SettingsFingerprint) ||
                !IsHash(baseline.PlayerBuildHash) || !Guid.TryParseExact(baseline.ProfileGuid, "N", out _) ||
                baseline.InitialManifest == null || baseline.Files == null || baseline.Files.Length == 0 || baseline.Files.Length > 2048 ||
                baseline.NativeRequirements == null || baseline.NativeRequirements.Length > 100000 ||
                baseline.NativeRequirements.Any(n => string.IsNullOrEmpty(n) || n.Length > 8192) ||
                baseline.NativeRequirements.Distinct(StringComparer.Ordinal).Count() != baseline.NativeRequirements.Length)
            {
                throw new InvalidDataException("Incomplete or unsupported baseline receipt.");
            }

            HybridCLRManifest.Parse(baseline.InitialManifest.ToJson());
            var expected = new HashSet<string>(new[] { descriptorPath, markerPath }, StringComparer.OrdinalIgnoreCase);
            var assemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in baseline.Files)
            {
                if (file == null || !IsHash(file.Sha256) || file.Length <= 0 || file.Length > HybridCLRManifest.MaximumPayloadBytes)
                {
                    throw new InvalidDataException("Invalid baseline file record.");
                }

                var path = Child(root, file.Path);
                if (!expected.Add(path) || !File.Exists(path) || new FileInfo(path).Length != file.Length ||
                    HybridCLRManifest.Hash(File.ReadAllBytes(path)) != file.Sha256)
                {
                    throw new InvalidDataException("Missing, duplicate or changed frozen baseline file: " + file.Path);
                }

                var assembly = file.Path.StartsWith("Aot/", StringComparison.Ordinal) || file.Path.StartsWith("HotUpdate/", StringComparison.Ordinal);
                if (assembly)
                {
                    var actual = HybridCLRAssemblyAnalysis.Read(path);
                    var prefix = file.Path.StartsWith("Aot/", StringComparison.Ordinal) ? "Aot/" : "HotUpdate/";
                    if (file.Path != prefix + actual.Name + ".dll" || file.Identity != actual.Identity || file.Mvid != actual.Mvid ||
                        !assemblyNames.Add(actual.Name))
                    {
                        throw new InvalidDataException("Frozen assembly identity mismatch: " + file.Path);
                    }
                }
                else if (!GeneratedNames.Any(n => file.Path == "Generated/" + n) || file.Identity != null || file.Mvid != null)
                {
                    throw new InvalidDataException("Unexpected baseline artifact: " + file.Path);
                }
            }
            if (!baseline.Files.Any(f => f.Path.StartsWith("Aot/", StringComparison.Ordinal)) ||
                GeneratedNames.Any(n => !baseline.Files.Any(f => f.Path == "Generated/" + n)))
            {
                throw new InvalidDataException("The baseline lacks final AOT or required native generation artifacts.");
            }

            foreach (var payload in baseline.InitialManifest.HotUpdateAssemblies)
            {
                RequirePayload(baseline.Files, payload, "HotUpdate/");
            }

            foreach (var payload in baseline.InitialManifest.AotMetadata)
            {
                RequirePayload(baseline.Files, payload, "Aot/");
            }

            var registered = baseline.InitialManifest.HotUpdateAssemblies.Select(a => "HotUpdate/" + a.Name + ".dll").OrderBy(p => p, StringComparer.Ordinal);
            var frozen = baseline.Files.Where(f => f.Path.StartsWith("HotUpdate/", StringComparison.Ordinal)).Select(f => f.Path).OrderBy(p => p, StringComparer.Ordinal);
            if (!registered.SequenceEqual(frozen))
            {
                throw new InvalidDataException("Frozen hot-update inputs must exactly match the global native registration.");
            }

            foreach (var file in SafeFiles(root))
            {
                if (!expected.Contains(file))
                {
                    throw new InvalidDataException("Unrecorded file in the frozen baseline: " + file);
                }
            }

            baseline.Root = root;
            return baseline;
        }

        private static void RequirePayload(IEnumerable<HybridCLRBaselineFile> files, HybridCLRPayload payload, string prefix)
        {
            var file = files.SingleOrDefault(f => f.Path == prefix + payload.Name + ".dll");
            if (file == null || file.Identity != payload.Identity || file.Mvid != payload.Mvid || file.Sha256 != payload.Sha256 || file.Length != payload.Length)
            {
                throw new InvalidDataException("Published metadata/code does not match the final Player baseline: " + payload.Name);
            }
        }

        internal void Freeze(string directory, IEnumerable<KeyValuePair<string, string>> sources)
        {
            var destination = System.IO.Path.GetFullPath(directory);
            RejectLinks(destination);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                throw new IOException("An immutable baseline destination already exists: " + destination);
            }

            var stage = destination + ".stage-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(stage);
            var files = new List<HybridCLRBaselineFile>();
            foreach (var source in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                RejectLinks(source.Value);
                var path = Child(stage, source.Key);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                File.Copy(source.Value, path, false);
                var bytes = File.ReadAllBytes(path);
                var record = new HybridCLRBaselineFile { Path = source.Key, Length = bytes.LongLength, Sha256 = HybridCLRManifest.Hash(bytes) };
                if (source.Key.EndsWith(".dll", StringComparison.Ordinal))
                {
                    var assembly = HybridCLRAssemblyAnalysis.Read(path);
                    record.Identity = assembly.Identity;
                    record.Mvid = assembly.Mvid;
                }
                files.Add(record);
            }
            Files = files.ToArray();
            var descriptor = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
            File.WriteAllBytes(stage + "/baseline.json", descriptor);
            File.WriteAllText(stage + "/complete.json", JsonConvert.SerializeObject(new
            {
                schemaVersion = 1,
                baselineSha256 = HybridCLRManifest.Hash(descriptor)
            }), new UTF8Encoding(false));
            Read(stage);
            Directory.Move(stage, destination);
            Root = destination;
        }

        internal static string Child(string root, string relative)
        {
            try
            {
                _ = new ResourceAddress("baseline", relative);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Invalid owned relative path.", exception);
            }
            if (relative.IndexOf(':') >= 0 || relative.Length > 1024)
            {
                throw new InvalidDataException("Invalid owned relative path.");
            }

            var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
            if (!full.StartsWith(System.IO.Path.GetFullPath(root).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("An owned file escaped its root.");
            }

            RejectLinks(full);
            return full;
        }

        internal static void RejectLinks(string path)
        {
            for (var cursor = System.IO.Path.GetFullPath(path); cursor != null; cursor = System.IO.Path.GetDirectoryName(cursor))
            {
                if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Linked input/output is not supported: " + cursor);
                }
            }
        }

        internal static IReadOnlyList<string> SafeFiles(string directory)
        {
            var files = new List<string>();
            if (!Directory.Exists(directory))
            {
                return files;
            }

            var pending = new Stack<string>(new[] { System.IO.Path.GetFullPath(directory) });
            while (pending.Count != 0)
            {
                foreach (var path in Directory.GetFileSystemEntries(pending.Pop()))
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException("Linked input/output is not supported: " + path);
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(path);
                    }
                    else
                    {
                        files.Add(System.IO.Path.GetFullPath(path));
                    }

                    if (files.Count > 16384)
                    {
                        throw new InvalidDataException("Owned file count limit exceeded.");
                    }
                }
            }

            return files;
        }

        internal static JObject ReadObject(string json)
        {
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
            var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read())
            {
                throw new InvalidDataException("Trailing receipt data.");
            }

            return value;
        }

        internal static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }
}
