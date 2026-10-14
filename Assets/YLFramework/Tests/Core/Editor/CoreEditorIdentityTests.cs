using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine.TestTools;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace YLFramework.Core.Editor.Tests
{
    [Serializable]
    internal sealed class CoreEditorAssemblyDefinition
    {
        public string name;
        public string rootNamespace;
        public string[] references;
        public string[] includePlatforms;
        public string[] excludePlatforms;
        public bool overrideReferences;
        public string[] precompiledReferences;
        public bool autoReferenced;
        public string[] defineConstraints;
    }

    public class CoreEditorIdentityTests
    {
        private const string EditorRoot = "Assets/YLFramework/Editor/Core";
        private const string EditorAssemblyName = "YLFramework.Editor";

        [UnityTest]
        public IEnumerator MergedRuntimeHasActualUnityPositiveAndNegativeAnalyzerWitnesses()
            => CompileAnalyzerWitnesses(AssemblyBuilderFlags.EditorAssembly);

        [UnityTest]
        public IEnumerator MergedRuntimeHasActualUnityPlayerPositiveAndNegativeAnalyzerWitnesses()
            => CompileAnalyzerWitnesses(AssemblyBuilderFlags.DevelopmentBuild);

        private static IEnumerator CompileAnalyzerWitnesses(AssemblyBuilderFlags flags)
        {
            var editor = (flags & AssemblyBuilderFlags.EditorAssembly) != 0;
            var runtime = CompilationPipeline.GetAssemblies(editor ? AssembliesType.Editor : AssembliesType.Player)
                .Single(a => a.name == "YLFramework.Runtime");
            Assert.That(runtime.compilerOptions.RoslynAnalyzerDllPaths.Count(path => path.EndsWith("YLFramework.Analyzers.dll")), Is.EqualTo(1));
            var folder = Path.Combine(Path.GetTempPath(), "kilo", "YLFramework.Analyzer.Unity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                foreach (bool negative in new[] { true, false })
                {
                    string source = "using YLFramework.Core;class Probe:View{" + (negative
                        ? "LoadViewArgs held;protected override void Start(){}void OnEnable(){}void Wrong(Model model){RegisterEvent(1,Changed);EventManager.AddEvent(1,Changed);var args=new LoadViewArgs();model.Id=1;}"
                        : "protected override void Start(){base.Start();}void Awake(){RegisterEvent(1,Changed);}void Borrow(){var args=LoadViewArgs.New();}long Read(Model model)=>model.Id;") + "void Changed(){}}";
                    string path = Path.Combine(folder, negative ? "Negative.cs" : "Positive.cs");
                    string output = Path.ChangeExtension(path, ".dll");
                    File.WriteAllText(path, source);
                    var builder = new AssemblyBuilder(output, new[] { path })
                    {
                        flags = flags,
                        buildTarget = BuildTarget.StandaloneWindows64,
                        buildTargetGroup = BuildTargetGroup.Standalone,
                        referencesOptions = ReferencesOptions.UseEngineModules,
                        additionalReferences = new[] { typeof(Module).Assembly.Location },
                        compilerOptions = new ScriptCompilerOptions { RoslynAnalyzerDllPaths = runtime.compilerOptions.RoslynAnalyzerDllPaths }
                    };
                    builder.excludeReferences = builder.defaultReferences.Where(reference =>
                        Path.GetFileName(reference).StartsWith("YLFramework", StringComparison.Ordinal) ||
                        Path.GetFileName(reference).EndsWith("TestRunner.dll", StringComparison.Ordinal) ||
                        (!editor && Path.GetFileName(reference).StartsWith("UnityEditor", StringComparison.Ordinal))).ToArray();
                    Assert.That(builder.defaultDefines.Contains("UNITY_EDITOR"), Is.EqualTo(editor));
                    CompilerMessage[] messages = null;
                    builder.buildFinished += (_, result) => messages = result;
                    Assert.That(builder.Build(), Is.True);
                    while (builder.status != AssemblyBuilderStatus.Finished) yield return null;
                    Assert.That(messages, Is.Not.Null);
                    var errors = messages.Where(message => message.type == CompilerMessageType.Error).ToArray();
                    // 故意违规同时触发原 XF 全局分析器；保留其 Error，不用关闭它来验收自有 Warning。
                    Assert.That(errors.Select(message => Regex.Match(message.message, @"\berror ([A-Z0-9_]+):").Groups[1].Value),
                        Is.EquivalentTo(negative ? new[] { "MUST_CALL_BASE_ATTRIBUTE", "DISABLE_NEW", "PROTECTED_HIERARCHY_MODIFICATION",
                            "METHOD_LOCAL_ONLY_TYPE", "RESTRICTED_CALL_ATTRIBUTE" } : Array.Empty<string>()),
                        string.Join("\n", errors.Select(message => message.message)));
                    Assert.That(messages.Any(message => message.message.Contains("AD0001")), Is.False);
                    var diagnostics = messages.Where(message => Regex.IsMatch(message.message, @"\bYL000[1-7]\b")).ToArray();
                    Assert.That(diagnostics.All(message => message.type == CompilerMessageType.Warning), Is.True);
                    Assert.That(diagnostics.Select(message => Regex.Match(message.message, @"\bYL000[1-7]\b").Value),
                        Is.EquivalentTo(negative ? Enumerable.Range(1, 7).Select(id => "YL000" + id) : Array.Empty<string>()));
                    Assert.That(File.Exists(output), Is.EqualTo(!negative));
                }
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void FormalCoreEditorRootPreservesGuidAndExactEditorAssemblyOwnership()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(EditorRoot), Is.EqualTo("d9b09bbe8b78666468f046d49ff546f3"));
            var assembly = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)
                .Single(a => a.name == EditorAssemblyName);
            var files = Directory.GetFiles("Assets/YLFramework/Editor", "*.cs", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).ToArray();
            Assert.That(assembly.sourceFiles.Select(p => p.Replace('\\', '/')), Is.EquivalentTo(files));
            Assert.That(File.Exists(EditorRoot + "/YLFramework.Core.Editor.asmdef"), Is.False);
            Assert.That(File.Exists(EditorRoot + "/YLFramework.Core.Editor.asmdef.meta"), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/YLFramework/Editor/YLFramework.Editor.asmdef"),
                Is.EqualTo("62b3fd14a5c704743835cacd6a6870a1"));
            var help = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("YLFramework.Editor.Help.HelpWindow"))
                .Single(type => type != null);
            Assert.That(typeof(CreateModuleCommand).Assembly, Is.SameAs(help.Assembly));
            Assert.That(Directory.Exists("Assets/YLFramework/Editor/XFMain"), Is.False);
        }

        [Test]
        public void EditorAssemblyIsEditorOnlyWithExplicitAllowedDependencies()
        {
            var definition = ReadDefinition("Assets/YLFramework/Editor/YLFramework.Editor.asmdef");
            Assert.That(definition.name, Is.EqualTo(EditorAssemblyName));
            Assert.That(definition.rootNamespace, Is.EqualTo("YLFramework.Editor"));
            Assert.That(definition.includePlatforms, Is.EqualTo(new[] { "Editor" }));
            Assert.That(definition.excludePlatforms, Is.Empty);
            Assert.That(definition.autoReferenced, Is.True);
            var references = definition.references.Select(ResolveReference).ToArray();
            Assert.That(references, Is.EquivalentTo(new[]
            {
                "YLFramework.Runtime", "UnityEngine.UI", "XFABManager"
            }), "Only the declared temporary XFAB backend may reference original assemblies.");
            Assert.That(references.Distinct().Count(), Is.EqualTo(references.Length));

            var assembly = typeof(CreateModuleCommand).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo(EditorAssemblyName));
            var compiledReferences = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(compiledReferences, Does.Contain("YLFramework.Runtime"));
            Assert.That(compiledReferences, Does.Not.Contain("YLFramework.Editor.Help"));
            Assert.That(compiledReferences, Does.Not.Contain("YLFramework.Core.Editor"));
            Assert.That(compiledReferences.Any(n => n == "UnityEditor" || n.StartsWith("UnityEditor.", StringComparison.Ordinal)), Is.True);
            Assert.That(compiledReferences.Any(IsForbiddenReference), Is.False,
                string.Join(", ", compiledReferences));
        }

        [Test]
        public void TestAssemblyHasNoOriginalGameOrExistingYLEditorDependency()
        {
            var definition = ReadDefinition("Assets/YLFramework/Tests/Core/Editor/YLFramework.Core.Editor.Tests.asmdef");
            Assert.That(definition.name, Is.EqualTo("YLFramework.Core.Editor.Tests"));
            Assert.That(definition.includePlatforms, Is.EqualTo(new[] { "Editor" }));
            Assert.That(definition.excludePlatforms, Is.Empty);
            Assert.That(definition.references.Select(ResolveReference), Is.EquivalentTo(new[]
            {
                EditorAssemblyName, "YLFramework.Runtime",
                "UnityEngine.UI", "UnityEngine.TestRunner", "UnityEditor.TestRunner"
            }));
            Assert.That(definition.overrideReferences, Is.True);
            Assert.That(definition.precompiledReferences, Is.EqualTo(new[] { "nunit.framework.dll" }));
            Assert.That(definition.defineConstraints, Does.Contain("UNITY_INCLUDE_TESTS"));
            Assert.That(definition.autoReferenced, Is.False);
            Assert.That(typeof(CoreEditorIdentityTests).Assembly.GetReferencedAssemblies()
                .Any(a => IsForbiddenReference(a.Name) || a.Name.StartsWith("XFAB", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void EditorTypeNamesAreOwnedAndUniqueAcrossLoadedAssemblies()
        {
            var assembly = typeof(CreateModuleCommand).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo(EditorAssemblyName));
            foreach (var type in assembly.GetTypes().Where(type => type.Namespace == "YLFramework.Core.Editor" ||
                (type.Namespace != null && type.Namespace.StartsWith("YLFramework.Core.Editor.", StringComparison.Ordinal))))
            {
                var root = type;
                while (root.DeclaringType != null)
                    root = root.DeclaringType;
                if (root.IsDefined(typeof(CompilerGeneratedAttribute), false))
                    continue;
                Assert.That(type.Namespace == "YLFramework.Core.Editor" ||
                    (type.Namespace != null && type.Namespace.StartsWith("YLFramework.Core.Editor.", StringComparison.Ordinal)), Is.True, type.FullName);
                foreach (var other in AppDomain.CurrentDomain.GetAssemblies().Where(a => a != assembly))
                    Assert.That(other.GetType(type.FullName), Is.Null,
                        type.FullName + " also exists in " + other.GetName().Name);
            }
        }

        [Test]
        public void OnlyNewYLMenuEntriesMustBeUniqueAcrossAllRegisteredMenus()
        {
            var entries = TypeCache.GetMethodsWithAttribute<MenuItem>()
                .SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), false)
                    .Cast<MenuItem>().Select(attribute => new { method, attribute })).ToArray();
            var owned = entries.Where(e => e.method.DeclaringType.Assembly == typeof(CreateModuleCommand).Assembly &&
                e.method.DeclaringType.Namespace != null && e.method.DeclaringType.Namespace.StartsWith("YLFramework.Core.Editor", StringComparison.Ordinal)).ToArray();
            Assert.That(owned.Any(e => e.attribute.menuItem == CreateModuleCommand.MenuPath), Is.True);
            Assert.That(owned.Any(e => e.attribute.menuItem == "YLFramework/Core/Create Missing Default Resources"), Is.True);
            foreach (var entry in owned)
            {
                string path = NormalizeMenuPath(entry.attribute.menuItem);
                Assert.That(path.StartsWith("YLFramework/Core/", StringComparison.Ordinal) ||
                    path.StartsWith("Assets/Create/YLFramework/Core/", StringComparison.Ordinal), Is.True, path);
                // A matching validation item is legal; unrelated original XF duplicates are not this test's scope.
                var conflicts = entries.Where(e => NormalizeMenuPath(e.attribute.menuItem) == path &&
                    e.attribute.validate == entry.attribute.validate).ToArray();
                Assert.That(conflicts.Length, Is.EqualTo(1), path + ": " + string.Join(", ",
                    conflicts.Select(e => e.method.DeclaringType.FullName + "." + e.method.Name)));
            }
        }

        [TestCase(typeof(DisableEditDrawer), typeof(CustomPropertyDrawer), typeof(YLFramework.Foundation.Attributes.DisableEdit))]
        public void InspectorAndDrawerBindNeutralFoundationMetadata(Type editorType, Type attributeType, Type runtimeType)
        {
            var binding = editorType.GetCustomAttributesData().Single(a => a.AttributeType == attributeType);
            Assert.That(binding.ConstructorArguments[0].Value, Is.EqualTo(runtimeType));
            Assert.That(runtimeType.Assembly.GetName().Name, Is.EqualTo("YLFramework.Runtime"));
            Assert.That(editorType.Assembly.GetName().Name, Is.EqualTo(EditorAssemblyName));
        }

        [Test]
        public void CoreAudioHasRetiredWhileFormalRuntimeAndOriginalPackageKeepSeparateIdentities()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var core = typeof(Module).Assembly;
            var formal = assemblies.Single(a => a.GetName().Name == "YLFramework.Runtime");
            var original = assemblies.Single(a => a.GetName().Name == "XFGameFramework");
            foreach (var name in new[] { "AudioData", "AudioController", "AudioPlayer", "AudioPlayerExtension", "AudioPlayerAutoPause",
                "AudioAssetType", "AudioPlayType", "StopType", "ClipAssetNameInfo", "ClipAssetInfo" })
                Assert.IsNull(core.GetType("YLFramework.Core." + name), name);
            Assert.IsNull(typeof(CreateModuleCommand).Assembly.GetType("YLFramework.Core.Editor.AudioPlayerInspector"));
            foreach (var name in new[] { "AudioKit", "AudioHandle", "AudioEmitter", "AudioMusicPlayer", "AudioClipLibrary", "AudioGroupPlayer" })
                Assert.IsNotNull(formal.GetType("YLFramework.Services.Audio." + name), name);
            Assert.IsNotNull(original.GetType("XFGameFramework.AudioPlayer"));
            CollectionAssert.DoesNotContain(core.GetReferencedAssemblies().Select(a => a.Name).ToArray(), formal.GetName().Name);
            CollectionAssert.DoesNotContain(formal.GetReferencedAssemblies().Select(a => a.Name).ToArray(), core.GetName().Name);
        }

        [Test]
        public void EditorFilesHaveIndependentImportedGuidsAndExcludePackageManagementAndDlls()
        {
            var package = PackageInfo.GetAllRegisteredPackages().Single(p => p.name == "com.xfkj.xfgameframework");
            string originalRoot = Path.Combine(package.resolvedPath, "Editor");
            Assert.That(Directory.Exists(originalRoot), Is.True, originalRoot);
            var originalGuids = Directory.GetFiles(package.resolvedPath, "*.meta", SearchOption.AllDirectories)
                .Select(ReadGuid).ToArray();
            var files = Directory.GetFiles(EditorRoot, "*", SearchOption.AllDirectories)
                .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.That(files, Is.Not.Empty);
            foreach (string path in files)
                Assert.That(File.Exists(path + ".meta"), Is.True, "Not imported: " + path);
            Assert.That(File.Exists(EditorRoot + ".meta"), Is.True, "Editor root must also have an independent GUID.");
            var guids = Directory.GetFiles(EditorRoot, "*.meta", SearchOption.AllDirectories)
                .Concat(new[] { EditorRoot + ".meta" }).Select(ReadGuid).ToArray();
            Assert.That(guids.Distinct().Count(), Is.EqualTo(guids.Length), "Duplicate new asset GUIDs.");
            Assert.That(guids.Intersect(originalGuids), Is.Empty, "A new asset reused an original GUID.");
            foreach (string file in files)
            {
                string relative = file.Replace('\\', '/').Substring(EditorRoot.Length + 1);
                Assert.That(Path.GetExtension(file), Is.Not.EqualTo(".dll").IgnoreCase, relative);
                Assert.That(relative.Split('/').Any(part => part.Equals("About", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("Expansion", StringComparison.OrdinalIgnoreCase)), Is.False, relative);
                if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;
                string source = File.ReadAllText(file);
                Assert.That(source, Does.Not.Contain("XFGameFrameworkCodeAnalyzer"), relative);
                Assert.That(source, Does.Not.Contain("ExpansionWindow"), relative);
                Assert.That(source, Does.Not.Contain("ExpansionPackageListTree"), relative);
                Assert.That(source, Does.Not.Contain("ExpansionPackages"), relative);
                Assert.That(source, Does.Not.Contain("Packages/com.xfkj.xfgameframework/Editor/About"), relative);
            }
        }

        [Test]
        public void FrameworkRootCarriesTheOnlyCompleteXfLicense()
        {
            var package = PackageInfo.GetAllRegisteredPackages().Single(p => p.name == "com.xfkj.xfgameframework");
            string upstreamLicense = Path.Combine(package.resolvedPath, "LICENSE.md");
            if (!File.Exists(upstreamLicense))
                upstreamLicense = Path.Combine(package.resolvedPath, "LICENSE");
            Assert.That(File.Exists(upstreamLicense), Is.True, "Upstream license must be available for comparison.");
            const string licensePath = "Assets/YLFramework/LICENSE.md";
            Assert.That(File.Exists(licensePath), Is.True, "The shared XF license must be distributed at the framework root.");
            string license = File.ReadAllText(licensePath);
            Assert.That(license.Replace("\r\n", "\n").Trim(),
                Is.EqualTo(File.ReadAllText(upstreamLicense).Replace("\r\n", "\n").Trim()));
            Assert.That(license, Does.Contain("Copyright (c) 2021 \u5f26\u5c0f\u98ce"));
            foreach (var oldPath in new[] { "Runtime/Core/LICENSE.md", "Runtime/Foundation/LICENSE.md", "Editor/Core/LICENSE.md" })
            {
                Assert.That(File.Exists("Assets/YLFramework/" + oldPath), Is.False, oldPath);
                Assert.That(File.Exists("Assets/YLFramework/" + oldPath + ".meta"), Is.False, oldPath + ".meta");
            }
        }

        internal static CoreEditorAssemblyDefinition ReadDefinition(string path)
        {
            return JsonUtility.FromJson<CoreEditorAssemblyDefinition>(File.ReadAllText(path));
        }

        private static string ResolveReference(string reference)
        {
            if (!reference.StartsWith("GUID:", StringComparison.Ordinal))
                return reference;
            return ReadDefinition(AssetDatabase.GUIDToAssetPath(reference.Substring(5))).name;
        }

        private static bool IsForbiddenReference(string name)
        {
            return name.StartsWith("XFGameFramework", StringComparison.Ordinal) ||
                name.StartsWith("Project.Game", StringComparison.Ordinal) || name == "Assembly-CSharp" ||
                name == "Assembly-CSharp-Editor" ||
                name == "YLFramework.Core.Editor" || name.StartsWith("YLFramework.Editor.", StringComparison.Ordinal);
        }

        private static string NormalizeMenuPath(string path)
        {
            return Regex.Replace(path.Trim(), @"\s+[_%&#][^/\s]+$", "");
        }

        private static string ReadGuid(string path)
        {
            var match = Regex.Match(File.ReadAllText(path), @"(?m)^guid:\s*([0-9a-f]{32})\s*$");
            Assert.That(match.Success, Is.True, "Missing GUID: " + path);
            return match.Groups[1].Value;
        }
    }
}
