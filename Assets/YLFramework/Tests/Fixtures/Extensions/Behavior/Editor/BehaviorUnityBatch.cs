using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace YLFramework.Behavior.Acceptance
{
    public static class BehaviorUnityBatch
    {
        [Serializable]
        private sealed class FileRecord
        {
            public string path;
            public string sha256;
            public string guid;
        }
        [Serializable]
        private sealed class Baseline
        {
            public string baseline;
            public FileRecord[] files;
        }
        [Serializable]
        private sealed class AssemblyRecord
        {
            public string name;
            public string outputPath;
            public string[] sourceFiles;
            public string[] references;
        }
        [Serializable]
        private sealed class PackageRecord
        {
            public string name;
            public string version;
            public string source;
            public string resolvedPath;
        }
        [Serializable]
        private sealed class Audit
        {
            public string utc;
            public string unity;
            public string project;
            public string output;
            public int pid;
            public int ownedMetaCount;
            public int expectedMetaCount;
            public int missingScriptCount;
            public bool batchMode;
            public bool compileFailed;
            public bool lightweightUnchanged;
            public string globalRawGuidAudit = "Separate PowerShell 7 raw Assets audit; Unity Mono cannot read existing overlong paths reliably.";
            public string[] missingExpectedMeta;
            public string[] missingAssetMeta;
            public string[] duplicateOwnedGuids;
            public string[] assemblyErrors;
            public FileRecord[] meta;
            public AssemblyRecord[] assemblies;
            public PackageRecord[] packages;
        }

        private static readonly string[] _modules = { "Overlap", "CharacterController2D", "MoveablePlatform", "StateMachine" };

        private static bool Owns(string path)
        {
            path = path.Replace('\\', '/');
            foreach (string module in _modules)
            {
                string layer = module == "StateMachine" ? "Systems/" : "Utilities/";
                if (new[] { "Runtime/" + layer + module, "Editor/" + layer + module,
                    "Tests/EditMode/" + (module == "StateMachine" ? "Editor/" : "Utilities/") + module,
                    "Tests/PlayMode/" + layer + module }.Any(root =>
                        path.StartsWith("Assets/YLFramework/" + root + "/", StringComparison.Ordinal) ||
                        path == "Assets/YLFramework/" + root || path == "Assets/YLFramework/" + root + ".meta"))
                {
                    return true;
                }
            }

            return path.StartsWith("Assets/YLFramework/Tests/EditMode/Editor/Behavior", StringComparison.Ordinal)
                || path.StartsWith("Assets/YLFramework/Tests/EditMode/Systems/Behavior", StringComparison.Ordinal)
                || path.StartsWith("Assets/YLFramework/Tests/PlayMode/Systems/Behavior", StringComparison.Ordinal)
                || path.StartsWith("Assets/YLFramework/Tests/Fixtures/Extensions/Behavior", StringComparison.Ordinal);
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        [Serializable]
        private sealed class Cleanup
        {
            public string[] folders;
            public FileRecord[] archived;
        }

        public static void ArchiveAndCleanGenerated()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Use isolated A EditMode BatchMode.");
            }

            string output = Output();
            const string Parent = "Assets/YLFramework/Tests/Fixtures/Extensions/Behavior";
            var folders = Directory.GetDirectories(Parent, "GeneratedAcceptance_*", SearchOption.TopDirectoryOnly).Select(path => path.Replace('\\', '/')).ToArray();
            var files = new List<FileRecord>();
            string archive = Path.Combine(output, "fixture-archive");
            Directory.CreateDirectory(archive);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^GeneratedAcceptance_[0-9a-f]{32}$"))
                {
                    throw new InvalidOperationException("Unexpected generated folder name: " + folder);
                }

                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Concat(File.Exists(folder + ".meta") ? new[] { folder + ".meta" } : Array.Empty<string>()))
                {
                    string relative = path.Replace('\\', '/').Substring(Parent.Length + 1);
                    string target = Path.Combine(archive, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(path, target, false);
                    files.Add(new FileRecord { path = path.Replace('\\', '/'), sha256 = Hash(path), guid = AssetDatabase.AssetPathToGUID(path.EndsWith(".meta", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path) });
                }
            }
            // 仅删除上面已逐文件归档的本入口GUID临时目录，不扫描/删除其他用户内容。
            foreach (string folder in folders)
            {
                if (!AssetDatabase.DeleteAsset(folder))
                {
                    throw new InvalidOperationException("Could not remove owned temporary fixture: " + folder);
                }
            }

            File.WriteAllText(Path.Combine(output, "fixture-cleanup.json"), JsonUtility.ToJson(new Cleanup { folders = folders, archived = files.ToArray() }, true), new UTF8Encoding(false));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"T3-A archived {files.Count} generated fixture files and removed {folders.Length} owned temporary folders.");
        }

        internal static string Output()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-behaviorOutput");
            if (index < 0 || index + 1 >= args.Length)
            {
                throw new ArgumentException("Missing -behaviorOutput.");
            }

            string path = Path.GetFullPath(args[index + 1]);
            string parent = Path.GetFullPath("C:/Users/Kano/AppData/Local/Temp/kilo/BA26") + Path.DirectorySeparatorChar;
            if (!path.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(path))
            {
                throw new InvalidOperationException("Use the existing dedicated A run directory.");
            }

            return path;
        }

        public static void AuditImport()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("This entry is for the isolated A BatchMode only.");
            }

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (project != "G:/Unity/Projects/CGJ2024/.kilo/worktrees/fusion-xf-ext-behavior-901d14f1ae336a53")
            {
                throw new InvalidOperationException("Wrong projectPath.");
            }

            string output = Output();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var raw = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var ownMeta = new List<FileRecord>();
            foreach (string full in Directory.GetFiles(Application.dataPath, "*.meta", SearchOption.AllDirectories))
            {
                string path = full.Substring(project.Length + 1).Replace('\\', '/');
                if (!Owns(path))
                {
                    continue;
                }

                string guid = File.ReadLines(full).FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal))?.Substring(6).Trim();
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                if (!raw.TryGetValue(guid, out var paths))
                {
                    raw.Add(guid, paths = new List<string>());
                }

                paths.Add(path);
                if (Owns(path))
                {
                    ownMeta.Add(new FileRecord { path = path, guid = guid, sha256 = Hash(full) });
                }
            }
            string expectedPath = Path.Combine(output, "expected-meta-146.txt");
            if (!File.Exists(expectedPath))
            {
                throw new InvalidOperationException("Missing frozen pre-import meta expectation.");
            }

            var expected = File.ReadAllLines(expectedPath);
            string[] assets = AssetDatabase.GetAllAssetPaths().Where(Owns).ToArray();
            var missing = assets.Where(path => !path.EndsWith(".meta", StringComparison.Ordinal) && !File.Exists(path + ".meta")).ToArray();
            var duplicates = ownMeta.Where(record => raw[record.guid].Count != 1 || AssetDatabase.AssetPathToGUID(record.path.Substring(0, record.path.Length - 5)) != record.guid).Select(record => record.path).ToArray();
            int missingScripts = 0;
            foreach (string path in assets.Where(path => path.EndsWith(".prefab", StringComparison.Ordinal)))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab)
                {
                    continue;
                }

                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                }
            }
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText("Tools/Behavior/LightweightFSM.baseline.json"));
            bool lightweightUnchanged = baseline.files.All(record => File.Exists(record.path) && Hash(record.path) == record.sha256);
            var compilation = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            string[] names = { "YLFramework.Runtime", "YLFramework.Editor", "YLFramework.Tests.EditMode", "YLFramework.Tests.PlayMode", "YLFramework.Behavior.Acceptance", "YLFramework.Behavior.Acceptance.Editor" };
            var assemblyErrors = names.Where(name => compilation.All(assembly => assembly.name != name)).Select(name => "Missing assembly: " + name).ToList();
            foreach (string path in Directory.GetFiles("Assets/YLFramework", "*.cs", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/')).Where(Owns))
            {
                string expectedAssembly = path.StartsWith("Assets/YLFramework/Runtime/", StringComparison.Ordinal) ? "YLFramework.Runtime"
                    : path.StartsWith("Assets/YLFramework/Editor/", StringComparison.Ordinal) ? "YLFramework.Editor"
                    : path.Contains("/Tests/EditMode/") ? "YLFramework.Tests.EditMode" : path.Contains("/Tests/PlayMode/") ? "YLFramework.Tests.PlayMode"
                    : path.Contains("/Behavior/Editor/") ? "YLFramework.Behavior.Acceptance.Editor" : "YLFramework.Behavior.Acceptance";
                var owner = compilation.FirstOrDefault(assembly => assembly.sourceFiles.Any(file => file.Replace('\\', '/').EndsWith(path, StringComparison.Ordinal)));
                if (owner == null || owner.name != expectedAssembly)
                {
                    assemblyErrors.Add(path + " -> " + (owner?.name ?? "not compiled") + "; expected " + expectedAssembly);
                }
            }
            var audit = new Audit
            {
                utc = DateTime.UtcNow.ToString("o"),
                unity = Application.unityVersion,
                project = project,
                output = output,
                pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                batchMode = Application.isBatchMode,
                compileFailed = EditorUtility.scriptCompilationFailed,
                ownedMetaCount = ownMeta.Count,
                expectedMetaCount = expected.Length,
                missingExpectedMeta = expected.Where(path => !File.Exists(path)).ToArray(),
                missingAssetMeta = missing,
                duplicateOwnedGuids = duplicates,
                missingScriptCount = missingScripts,
                lightweightUnchanged = lightweightUnchanged,
                meta = ownMeta.ToArray(),
                assemblyErrors = assemblyErrors.ToArray(),
                assemblies = compilation.Where(assembly => names.Contains(assembly.name)).Select(assembly => new AssemblyRecord { name = assembly.name, outputPath = assembly.outputPath, sourceFiles = assembly.sourceFiles, references = assembly.compiledAssemblyReferences }).ToArray(),
                packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(package => new PackageRecord { name = package.name, version = package.version, source = package.source.ToString(), resolvedPath = package.resolvedPath }).ToArray()
            };
            File.WriteAllText(Path.Combine(output, "import-audit.json"), JsonUtility.ToJson(audit, true), new UTF8Encoding(false));
            string rspRoot = Path.Combine(output, "assembly-inputs");
            Directory.CreateDirectory(rspRoot);
            int counter = 0;
            foreach (string path in Directory.GetFiles("Library/Bee", "*.rsp", SearchOption.AllDirectories).Where(path => names.Any(name => Path.GetFileName(path).StartsWith(name + ".", StringComparison.Ordinal))))
            {
                File.Copy(path, Path.Combine(rspRoot, (++counter).ToString("D3") + "-" + Path.GetFileName(path)));
            }

            Debug.Log($"T3-A import audit: metas={audit.ownedMetaCount}; expected={audit.expectedMetaCount}; missing={audit.missingExpectedMeta.Length}; duplicates={duplicates.Length}; assemblies={audit.assemblies.Length}; errors={assemblyErrors.Count}; lightweightUnchanged={lightweightUnchanged}.");
            if (audit.compileFailed || audit.missingExpectedMeta.Length != 0 || missing.Length != 0 || duplicates.Length != 0 || missingScripts != 0 || !lightweightUnchanged || assemblyErrors.Count != 0)
            {
                throw new InvalidOperationException("T3-A import audit failed. Read this run's import-audit.json.");
            }
        }
    }
}
