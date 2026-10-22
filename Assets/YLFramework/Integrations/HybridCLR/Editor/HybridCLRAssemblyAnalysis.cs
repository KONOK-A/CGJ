using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using dnlib.DotNet;
using YLFramework.Integrations.HybridCLR;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    public sealed class HybridCLRAssemblyRecord
    {
        public string Path { get; internal set; }
        public string Name { get; internal set; }
        public string Identity { get; internal set; }
        public string Mvid { get; internal set; }
        public string[] References { get; internal set; }
        public long Length { get; internal set; }
        public string Sha256 { get; internal set; }
    }

    public sealed class HybridCLRAssemblySet
    {
        public IReadOnlyList<HybridCLRAssemblyRecord> HotUpdate { get; internal set; }
        public IReadOnlyList<HybridCLRAssemblyRecord> Aot { get; internal set; }
    }

    public static class HybridCLRAssemblyAnalysis
    {
        public static IReadOnlyList<HybridCLRAssemblyRecord> CollectRequiredAotMetadata(HybridCLRAssemblySet assemblies, int maximumIterations = 10)
        {
            if (assemblies == null) throw new ArgumentNullException(nameof(assemblies));
            if (maximumIterations < 1 || maximumIterations > 20) throw new ArgumentOutOfRangeException(nameof(maximumIterations));
            var hot = new HashSet<string>(assemblies.HotUpdate.Select(r => r.Name), StringComparer.Ordinal);
            var aot = assemblies.Aot.ToDictionary(r => r.Name, StringComparer.Ordinal);
            var collector = new global::HybridCLR.Meta.AssemblyReferenceDeepCollector(CreateResolver(assemblies), hot.OrderBy(n => n, StringComparer.Ordinal).ToList());
            var analyzer = new global::HybridCLR.CodeGen.AOT.AOTReferenceAnalyzer(new global::HybridCLR.CodeGen.AOT.AOTReferenceAnalyzer.Options
            {
                Collector = collector, MaxIterationCount = maximumIterations, ComputeAotAssembly = false
            });
            analyzer.Run();
            // 官方 cache 拥有 ModuleDefMD，且没有 Dispose 合同；只读它们，不释放借用的 module。
            var required = new HashSet<string>(collector.GetLoadedModulesExcludeRootAssemblies().Select(m => m.Assembly.Name.String), StringComparer.Ordinal);
            foreach (var module in collector.GetLoadedModules())
                foreach (var reference in module.GetAssemblyRefs())
                    if (!hot.Contains(reference.Name.String)) required.Add(reference.Name.String);
            foreach (var type in analyzer.AotGenericTypes) required.Add(type.Type.Module.Assembly.Name.String);
            foreach (var method in analyzer.AotGenericMethods) required.Add(method.Method.Module.Assembly.Name.String);
            required.ExceptWith(hot);
            foreach (var name in required)
                if (!aot.ContainsKey(name)) throw new InvalidDataException("Required metadata is absent from the explicit AOT input set: " + name);
            return Array.AsReadOnly(required.OrderBy(n => n, StringComparer.Ordinal).Select(n => aot[n]).ToArray());
        }

        internal static global::HybridCLR.Mod.IAssemblyResolver CreateResolver(HybridCLRAssemblySet assemblies) =>
            new ExactAssemblyResolver(assemblies.HotUpdate.Concat(assemblies.Aot));

        private sealed class ExactAssemblyResolver : global::HybridCLR.Mod.IAssemblyResolver
        {
            private readonly Dictionary<string, string> paths;
            public ExactAssemblyResolver(IEnumerable<HybridCLRAssemblyRecord> assemblies) =>
                paths = assemblies.ToDictionary(r => r.Name, r => r.Path, StringComparer.Ordinal);

            public string ResolveAssembly(string assemblyName, bool throwExIfNotFind)
            {
                if (paths.TryGetValue(assemblyName, out var path)) return path;
                if (throwExIfNotFind || assemblyName == "netstandard")
                    throw new InvalidDataException("Assembly is outside the checked compilation/AOT inputs: " + assemblyName);
                return null;
            }
        }

        public static HybridCLRAssemblyRecord Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("An explicit assembly path is required.", nameof(path));
            var full = System.IO.Path.GetFullPath(path);
            if (!File.Exists(full)) throw new FileNotFoundException("Assembly input does not exist.", full);
            for (var cursor = full; cursor != null; cursor = System.IO.Path.GetDirectoryName(cursor))
                if ((File.GetAttributes(cursor) & System.IO.FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked assembly input is not supported: " + full);
            var bytes = File.ReadAllBytes(full);
            if (bytes.Length == 0 || bytes.LongLength > HybridCLRManifest.MaximumPayloadBytes)
                throw new InvalidDataException("Assembly input is empty or oversized: " + full);
            try
            {
                using var module = ModuleDefMD.Load(bytes);
                if (module.Assembly == null || !module.IsILOnly || module.Mvid == null)
                    throw new InvalidDataException("A managed manifest assembly with an MVID is required: " + full);
                return new HybridCLRAssemblyRecord
                {
                    Path = full, Name = module.Assembly.Name.String,
                    Identity = new AssemblyName(module.Assembly.FullName).FullName,
                    Mvid = module.Mvid.Value.ToString("D"), Length = bytes.LongLength,
                    Sha256 = HybridCLRManifest.Hash(bytes),
                    References = module.GetAssemblyRefs().Select(r => new AssemblyName(r.FullName).FullName)
                        .OrderBy(n => n, StringComparer.Ordinal).ToArray()
                };
            }
            catch (Exception exception) when (exception is BadImageFormatException || exception is IOException || exception is ArgumentException)
            {
                throw new InvalidDataException("Cannot read managed assembly metadata: " + full, exception);
            }
        }

        public static HybridCLRAssemblySet Analyze(IEnumerable<string> hotUpdatePaths, IEnumerable<string> aotPaths)
            => Analyze(hotUpdatePaths, aotPaths, null);

        internal static HybridCLRAssemblySet Analyze(IEnumerable<string> hotUpdatePaths, IEnumerable<string> aotPaths, IReadOnlyDictionary<string, string> aotIdentityAliases)
        {
            if (hotUpdatePaths == null) throw new ArgumentNullException(nameof(hotUpdatePaths));
            if (aotPaths == null) throw new ArgumentNullException(nameof(aotPaths));
            var hot = hotUpdatePaths.Select(Read).ToArray();
            var aot = aotPaths.Select(Read).ToArray();
            if (hot.Length == 0 || hot.Length > HybridCLRManifest.MaximumAssemblyCount)
                throw new InvalidDataException("An explicit supported hot-update assembly set is required.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in hot.Concat(aot))
                if (!names.Add(record.Name)) throw new InvalidDataException("Duplicate or case-colliding assembly identity: " + record.Name);
            var all = hot.Concat(aot).ToDictionary(r => r.Name, StringComparer.Ordinal);
            var hotNames = new HashSet<string>(hot.Select(r => r.Name), StringComparer.Ordinal);
            foreach (var record in hot)
            {
                if (record.Name == "Assembly-CSharp" || record.Name == "Assembly-CSharp-firstpass")
                    throw new InvalidDataException("Explicit asmdef or external DLL inputs are required; Assembly-CSharp is not supported.");
                foreach (var reference in record.References)
                {
                    var identity = new AssemblyName(reference);
                    if (identity.Name.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                        !all.TryGetValue(identity.Name, out var resolved))
                        throw new InvalidDataException("Missing, Editor-only or incompatible dependency: " + record.Name + " -> " + reference);
                    if (resolved.Identity != identity.FullName && (hotNames.Contains(identity.Name) || aotIdentityAliases == null ||
                        !aotIdentityAliases.TryGetValue(identity.FullName, out var bound) || bound != resolved.Identity))
                        throw new InvalidDataException("Incompatible dependency identity: " + record.Name + " -> " + reference + "; resolved=" + resolved.Identity);
                }
            }
            foreach (var record in aot)
                if (record.References.Any(r => hotNames.Contains(new AssemblyName(r).Name)))
                    throw new InvalidDataException("AOT statically references a hot-update assembly: " + record.Name);
            var remaining = hot.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
            var ordered = new List<HybridCLRAssemblyRecord>();
            var completed = new HashSet<string>(StringComparer.Ordinal);
            while (remaining.Count != 0)
            {
                var next = remaining.FirstOrDefault(r => r.References.Select(n => new AssemblyName(n).Name)
                    .Where(hotNames.Contains).All(completed.Contains));
                if (next == null) throw new InvalidDataException("Cyclic hot-update assembly dependencies.");
                ordered.Add(next); completed.Add(next.Name); remaining.Remove(next);
            }
            return new HybridCLRAssemblySet { HotUpdate = ordered.AsReadOnly(), Aot = Array.AsReadOnly(aot.OrderBy(r => r.Name, StringComparer.Ordinal).ToArray()) };
        }
    }
}
