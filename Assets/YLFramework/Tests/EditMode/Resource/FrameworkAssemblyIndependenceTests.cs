using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Services.Resource;
using Assembly = UnityEditor.Compilation.Assembly;

namespace YLFramework.Tests.Resource
{
    public class FrameworkAssemblyIndependenceTests
    {
        [TestCase(AssembliesType.Editor, "YLFramework.Runtime")]
        [TestCase(AssembliesType.Editor, "YLFramework.Editor")]
        [TestCase(AssembliesType.Player, "YLFramework.Runtime")]
        public void ActualCompilationAndBinaryClosuresDoNotRequireXfab(AssembliesType profile, string name)
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False);
            var graph = CompilationPipeline.GetAssemblies(profile).Single(a => a.name == name);
            Assert.That(graph.sourceFiles, Is.Not.Empty);
            var pending = new Stack<Assembly>(new[] { graph });
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (pending.Count != 0)
            {
                var assembly = pending.Pop();
                if (!visited.Add(assembly.name)) continue;
                Assert.That(IsXfab(assembly.name), Is.False, assembly.name);
                foreach (var reference in assembly.assemblyReferences) pending.Push(reference);
            }
            // UGUI 自身也会收到工程全局自动插件输入；主体必须白名单隔离这些输入，
            // 第三方程序集的真实必要依赖则由下面的已编译 AssemblyRef 闭包核对。
            foreach (var reference in graph.compiledAssemblyReferences)
                Assert.That(IsXfab(reference), Is.False, reference);
            var binaries = new Stack<System.Reflection.Assembly>(new[] { System.Reflection.Assembly.Load(name) });
            visited.Clear();
            while (binaries.Count != 0)
            {
                var assembly = binaries.Pop();
                if (!visited.Add(assembly.FullName)) continue;
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    Assert.That(IsXfab(reference.Name), Is.False, assembly.GetName().Name + " -> " + reference);
                    binaries.Push(System.Reflection.Assembly.Load(reference));
                }
            }
            TestContext.WriteLine("{0}: {1} sources; binary closure: {2}", profile, graph.sourceFiles.Length,
                string.Join(", ", visited.OrderBy(x => x, StringComparer.Ordinal)));
        }

        private static bool IsXfab(string name) => new[] { "xfab", "xfgameframework", "xffsm", "ICSharpCode.SharpZipLib", "EPPlus", "MiniExcel" }
            .Any(part => name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);

        internal static bool IsApprovedBouncyCastleReference(string path)
        {
            var approved = Path.Combine(Application.dataPath, "YLFramework/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll");
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(approved), StringComparison.OrdinalIgnoreCase) || !File.Exists(approved))
                return false;
            using var stream = File.OpenRead(approved);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() ==
                "d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495";
        }

        [Test]
        public void ReferenceAllowlistOnlyAddsTheExactApprovedBouncyCastleFile()
        {
            var project = Directory.GetParent(Application.dataPath).FullName;
            var contents = EditorApplication.applicationContentsPath;
            var ui = typeof(UnityEngine.UI.Image).Assembly.Location;
            var approved = Path.Combine(Application.dataPath, "YLFramework/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll");
            bool Allowed(string path) => IsApprovedBouncyCastleReference(path) ||
                Audio.AudioSourceIndependenceTests.IsAllowedReference(path, contents, ui, true);
            Assert.That(IsApprovedBouncyCastleReference(approved), Is.True, "The official 2.7.0 netstandard2.0 file must match both its path and SHA-256.");
            foreach (var path in new[]
            {
                Path.Combine(project, "Library/ScriptAssemblies/BouncyCastle.Cryptography.dll"),
                Path.Combine(Application.dataPath, "Plugins/BouncyCastle.Cryptography.dll"),
                Path.Combine(Application.dataPath, "YLFramework/Plugins/Other/BouncyCastle.Cryptography.dll"),
                Path.Combine(Application.dataPath, "YLFramework/Plugins/BouncyCastle/BouncyCastle.Crypto.dll"),
                Path.Combine(Application.dataPath, "YLFramework/Plugins/BouncyCastle/System.Fake.dll"),
                Path.Combine(Application.dataPath, "YLFramework/Plugins/BouncyCastle/UnityEngine.Fake.dll"),
                Path.Combine(Application.dataPath, "YLFramework/Plugins/BouncyCastle/XFABManager.dll"),
                Path.Combine(Application.dataPath, "YLFramework/Plugins/Sirenix/Sirenix.OdinInspector.Attributes.dll")
            })
            {
                Assert.That(IsApprovedBouncyCastleReference(path), Is.False, path);
                Assert.That(Allowed(path), Is.False, path);
            }
        }

        [Test]
        public void FormalResourceHasNoSecondLegacyAuthorityOrImporterMetadataEntry()
        {
            foreach (var name in new[] { "ResManager", "ServiceManager", "ResLoader", "IResLoader", "ResFactory", "IResCreator",
                "IRes", "Res", "ResKey", "ResTable", "GameObjectLoader", "SubProject", "ResData", "AssetBundleSetting",
                "AssetBundleHelper", "HotUpdateManager", "FakeResServer", "BinarySerializer", "ZipFileHelper" })
                Assert.That(typeof(ResourceSystem).Assembly.GetType("YLFramework.Services.Resource." + name), Is.Null, name);
            Assert.That(typeof(YLFramework.Editor.Resource.ResourceProject).Assembly
                .GetType("YLFramework.Editor.Resource.SubProjectData"), Is.Null);
        }

        [UnityTest]
        public IEnumerator CompleteRuntimeAndEditorSourcesCompileWithoutXfabOrOptionalAdapters()
        {
            return CompileSubjects(true);
        }

        [UnityTest]
        public IEnumerator CompleteRuntimeSourcesCompileWithoutXfabInWindowsDevelopmentProfile()
        {
            return CompileSubjects(false);
        }

        private static IEnumerator CompileSubjects(bool editor)
        {
            var folder = Path.Combine(Path.GetTempPath(), "kilo", "YLFramework.SubjectClosure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var profiles = CompilationPipeline.GetAssemblies(editor ? AssembliesType.Editor : AssembliesType.Player);
                string runtimeOutput = null;
                foreach (var name in editor ? new[] { "YLFramework.Runtime", "YLFramework.Editor" } : new[] { "YLFramework.Runtime" })
                {
                    var graph = profiles.Single(a => a.name == name);
                    var root = "Assets/YLFramework/" + (name == "YLFramework.Runtime" ? "Runtime/" : "Editor/");
                    var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                    CollectionAssert.AreEquivalent(sources.Select(Path.GetFullPath), graph.sourceFiles.Select(Path.GetFullPath),
                        "Compile every subject source, not a hand-picked closure.");
                    var output = Path.Combine(folder, name + ".dll");
                    var builder = new AssemblyBuilder(output, sources)
                    {
                        flags = editor ? AssemblyBuilderFlags.EditorAssembly : AssemblyBuilderFlags.DevelopmentBuild,
                        buildTarget = BuildTarget.StandaloneWindows64,
                        buildTargetGroup = BuildTargetGroup.Standalone,
                        referencesOptions = ReferencesOptions.UseEngineModules,
                        compilerOptions = new ScriptCompilerOptions { ApiCompatibilityLevel = graph.compilerOptions.ApiCompatibilityLevel,
                            AllowUnsafeCode = graph.compilerOptions.AllowUnsafeCode },
                        additionalDefines = Array.Empty<string>()
                    };
                    var json = graph.compiledAssemblyReferences.Single(p => Path.GetFileName(p) == "Newtonsoft.Json.dll");
                    var ui = typeof(UnityEngine.UI.Image).Assembly.Location;
                    bool Allowed(string path) => IsApprovedBouncyCastleReference(path) ||
                        Audio.AudioSourceIndependenceTests.IsAllowedReference(path,
                            EditorApplication.applicationContentsPath, ui, editor, json);
                    // AssemblyBuilder 的 defaultReferences 在本 Editor 仍返回 netstandard shims；
                    // 使用真实 profile 的 BCL/Unity 输入，避免把 .NET 4.8 的 CodeDom 错当成缺失产品依赖。
                    var trusted = graph.allReferences.Select(Path.GetFullPath).Where(Allowed).ToArray();
                    // excludeReferences 会在合并 additionalReferences 之后过滤，不能排除仍要提供的 BCL。
                    builder.excludeReferences = builder.defaultReferences
                        .Where(p => !trusted.Contains(Path.GetFullPath(p), StringComparer.OrdinalIgnoreCase)).ToArray();
                    builder.additionalReferences = trusted.Concat(runtimeOutput == null ? Array.Empty<string>() : new[] { runtimeOutput }).ToArray();
                    var effective = builder.defaultReferences.Except(builder.excludeReferences).Concat(builder.additionalReferences)
                        .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    Assert.That(effective.All(p => Allowed(p) || p == runtimeOutput), Is.True);
                    Assert.That(effective.Any(IsXfab), Is.False);
                    Assert.That(effective.Count(p => Path.GetFileName(p) == "UnityEngine.UI.dll"), Is.EqualTo(1));
                    Assert.That(effective.Count(p => Path.GetFileName(p) == "Newtonsoft.Json.dll"), Is.EqualTo(1));
                    if (name == "YLFramework.Runtime")
                        Assert.That(effective.Count(IsApprovedBouncyCastleReference), Is.EqualTo(1));
                    Assert.That(effective.Any(p => Path.GetFileName(p) == "mscorlib.dll"), Is.True);
                    CompilerMessage[] messages = null;
                    builder.buildFinished += (_, result) => messages = result;
                    Assert.That(builder.Build(), Is.True);
                    while (builder.status != AssemblyBuilderStatus.Finished) yield return null;
                    Assert.That(messages, Is.Not.Null);
                    Assert.That(messages.Where(m => m.type == CompilerMessageType.Error)
                        .Select(m => m.file + ":" + m.line + ": " + m.message), Is.Empty);
                    Assert.That(File.Exists(output), Is.True);
                    TestContext.WriteLine("Compiled {0}: {1} sources, {2}; references:\n{3}", name, sources.Length, builder.flags,
                        string.Join("\n", effective));
                    runtimeOutput = output;
                }
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
