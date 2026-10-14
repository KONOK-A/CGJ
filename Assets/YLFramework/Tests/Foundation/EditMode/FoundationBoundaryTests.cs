using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Core.Pool;

namespace YLFramework.Foundation.Tests
{
    public class FoundationBoundaryTests
    {
        [Test]
        public void FoundationFamiliesHaveOneRuntimeOwnerAndTheKernelContainerIsRetired()
        {
            const string runtimeRoot = "Assets/YLFramework/Runtime/";
            const string kernel = runtimeRoot + "Foundation/Kernel";
            Assert.That(Directory.Exists(kernel), Is.False);
            Assert.That(File.Exists(kernel + ".meta"), Is.False);
            Assert.That(File.Exists(kernel + "/YLFramework.Foundation.Runtime.asmdef"), Is.False);
            Assert.That(File.Exists(kernel + "/YLFramework.Foundation.Runtime.asmdef.meta"), Is.False);
            Assert.That(Directory.Exists(runtimeRoot + "Shared"), Is.False);
            Assert.That(File.Exists(runtimeRoot + "Shared.meta"), Is.False);
            foreach (var retired in new[] { "Tools/AssemblyTool", "Tools/Attributes" })
            {
                Assert.That(Directory.Exists(runtimeRoot + retired), Is.False, retired);
                Assert.That(File.Exists(runtimeRoot + retired + ".meta"), Is.False, retired);
            }
            const string convenienceHelper = runtimeRoot + "Foundation/Utilities/UnityObjectUtility.cs";
            Assert.That(File.Exists(convenienceHelper), Is.False);
            Assert.That(File.Exists(convenienceHelper + ".meta"), Is.False);
            Assert.That(typeof(ApplicationTool).Assembly.GetType("YLFramework.Foundation.UnityObjectUtility"), Is.Null);

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var foundation = assemblies.Single(a => a.name == "YLFramework.Runtime");
            Assert.That(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(foundation.name),
                Is.EqualTo(runtimeRoot + "YLFramework.Runtime.asmdef"));
            Assert.That(foundation.sourceFiles.Select(Path.GetFullPath), Is.SupersetOf(
                Directory.GetFiles(runtimeRoot + "Foundation", "*.cs", SearchOption.AllDirectories).Select(Path.GetFullPath)));
            var adapters = new[] { runtimeRoot + "Foundation/Timing/Components/TimerObject.cs",
                runtimeRoot + "Foundation/Events/Unity/AnimationEventListener.cs",
                runtimeRoot + "Core/MVC/Views/Components/CloseView.cs" }.Select(Path.GetFullPath).ToArray();
            Assert.That(adapters, Is.Not.Empty);
            Assert.That(assemblies.Single(a => a.name == "YLFramework.Runtime").sourceFiles.Select(Path.GetFullPath),
                Is.SupersetOf(adapters));
        }

        [UnityTest]
        public IEnumerator FoundationHasNoFrameworkResourceOrPluginAssemblyDependencies()
            => CompileNeutralSources(AssemblyBuilderFlags.EditorAssembly);

        [UnityTest]
        public IEnumerator NeutralFoundationSourcesCompileForDevelopmentPlayerWithoutEditorReferences()
            => CompileNeutralSources(AssemblyBuilderFlags.DevelopmentBuild);

        private static IEnumerator CompileNeutralSources(AssemblyBuilderFlags flags)
        {
            Assert.That(typeof(ApplicationTool).Assembly.GetName().Name, Is.EqualTo("YLFramework.Runtime"));
            var root = Path.Combine(Application.dataPath, "YLFramework/Runtime/Foundation");
            var entry = Path.GetFullPath(Path.Combine(root, "../YLFrameworkRoot.cs"));
            // Root 是唯一根级基础入口；目录内所有源码都必须独立编译，不能附加上层实现。
            var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Select(Path.GetFullPath)
                .Append(entry).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.That(sources.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(sources.Length));
            Assert.That(sources.Where(path => IsInsideFolder(path, root)), Is.EquivalentTo(
                Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Select(Path.GetFullPath)));
            Assert.That(sources.Where(path => !IsInsideFolder(path, root)), Is.EqualTo(new[] { entry }));
            foreach (var source in sources) Assert.That(File.Exists(source), Is.True, source);
            var graph = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var uiAssembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "UnityEngine.UI");
            Assert.That(uiAssembly.GetType("UnityEngine.EventSystems.EventSystem"), Is.Not.Null);
            var ui = Path.GetFullPath(uiAssembly.Location);
            Assert.That(ui, Is.EqualTo(Path.GetFullPath(graph.Single(a => a.name == "UnityEngine.UI").outputPath)).IgnoreCase);
            var inputDefines = graph.Single(a => a.name == "YLFramework.Runtime").defines
                .Where(define => define == "ENABLE_INPUT_SYSTEM" || define == "ENABLE_LEGACY_INPUT_MANAGER").ToArray();
            Assert.That(inputDefines, Is.Not.Empty, "The probe must use the product input configuration.");
            var parent = Path.GetTempPath();
            Assert.That(Directory.Exists(parent), Is.True);
            var folder = Path.Combine(parent, "kilo", "YLFramework.Foundation.SourceClosure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var output = Path.Combine(folder, "FoundationSourceClosure.dll");
                var editor = (flags & AssemblyBuilderFlags.EditorAssembly) != 0;
                var builder = new AssemblyBuilder(output, sources) { flags = flags,
                    buildTarget = BuildTarget.StandaloneWindows64, buildTargetGroup = BuildTargetGroup.Standalone,
                    referencesOptions = ReferencesOptions.UseEngineModules };
                Assert.That(builder.defaultDefines.Contains("UNITY_EDITOR"), Is.EqualTo(editor));
                builder.additionalDefines = inputDefines.Except(builder.defaultDefines).ToArray();
                var defines = builder.defaultDefines.Concat(builder.additionalDefines).Distinct().ToArray();
                Assert.That(defines.Contains("DEVELOPMENT_BUILD"), Is.EqualTo(!editor));
                Assert.That(defines.Where(define => define == "ENABLE_INPUT_SYSTEM" || define == "ENABLE_LEGACY_INPUT_MANAGER"),
                    Is.EquivalentTo(inputDefines));
                var contents = EditorApplication.applicationContentsPath;
                builder.excludeReferences = builder.defaultReferences.Where(path => !IsAllowedReference(path, contents, ui, editor)).ToArray();
                builder.additionalReferences = new[] { ui };
                var references = builder.defaultReferences.Except(builder.excludeReferences)
                    .Concat(builder.additionalReferences).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                Assert.That(references, Is.Not.Empty);
                Assert.That(references.All(path => IsAllowedReference(path, contents, ui, editor)), Is.True);
                Assert.That(references.Any(path => Path.GetFileName(path) == "UnityEngine.CoreModule.dll"), Is.True);
                Assert.That(references.Any(path => Path.GetFileName(path) == "mscorlib.dll" || Path.GetFileName(path) == "netstandard.dll"), Is.True);
                Assert.That(references.Any(path => Path.GetFileName(path).StartsWith("YLFramework") ||
                    Path.GetFileName(path).StartsWith("XF") || Path.GetFileName(path) == "Newtonsoft.Json.dll"), Is.False);
                TestContext.WriteLine("Compiling {0} complete Foundation sources with {1}; defines:\n{2}\nReferences:\n{3}",
                    sources.Length, flags, string.Join("\n", defines), string.Join("\n", references));
                CompilerMessage[] messages = null;
                builder.buildFinished += (_, result) => messages = result;
                Assert.That(builder.Build(), Is.True);
                while (builder.status != AssemblyBuilderStatus.Finished) yield return null;
                Assert.That(messages, Is.Not.Null);
                foreach (var message in messages)
                    TestContext.WriteLine("{0}:{1}:{2}: {3}: {4}", message.file, message.line, message.column, message.type, message.message);
                Assert.That(messages.Where(m => m.type == CompilerMessageType.Error)
                    .Select(m => m.file + ":" + m.line + ": " + m.message), Is.Empty);
                Assert.That(File.Exists(output), Is.True);
            }
            finally { Directory.Delete(folder, true); }
        }

        private static bool IsAllowedReference(string path, string contents, string ui, bool editor)
        {
            var absolute = Path.GetFullPath(path);
            if (!File.Exists(absolute)) return false;
            if (string.Equals(absolute, Path.GetFullPath(ui), StringComparison.OrdinalIgnoreCase)) return true;
            var name = Path.GetFileName(absolute);
            if (name.Contains("TestRunner")) return false;
            if (name == "UnityEngine.dll" || name.StartsWith("UnityEngine.", StringComparison.Ordinal))
                return IsInsideFolder(absolute, Path.Combine(contents, "Managed/UnityEngine")) ||
                    IsInsideFolder(absolute, Path.Combine(contents, "PlaybackEngines")) ||
                    string.Equals(absolute, Path.GetFullPath(Path.Combine(contents, "Managed/UnityEngine.dll")), StringComparison.OrdinalIgnoreCase);
            if (editor && (name == "UnityEditor.dll" || name.StartsWith("UnityEditor.", StringComparison.Ordinal)))
                return IsInsideFolder(absolute, Path.Combine(contents, "Managed/UnityEngine")) ||
                    IsInsideFolder(absolute, Path.Combine(contents, "Managed/UnityEditor")) ||
                    string.Equals(absolute, Path.GetFullPath(Path.Combine(contents, "Managed/UnityEditor.dll")), StringComparison.OrdinalIgnoreCase);
            if (name == "mscorlib.dll" || name == "netstandard.dll" || name == "System.dll" || name.StartsWith("System.", StringComparison.Ordinal))
                return IsInsideFolder(absolute, Path.Combine(contents, "NetStandard")) ||
                    IsInsideFolder(absolute, Path.Combine(contents, "UnityReferenceAssemblies")) ||
                    IsInsideFolder(absolute, Path.Combine(contents, "MonoBleedingEdge/lib/mono"));
            return false;
        }

        private static bool IsInsideFolder(string path, string folder)
        {
            var prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        [Test]
        public void NeutralLookupMaskAndDisplayMetadataHaveOneFoundationOwner()
        {
            var foundation = typeof(ApplicationTool).Assembly;
            foreach (string name in new[] { "YLFramework.Foundation.AssemblyTool", "YLFramework.Foundation.LayerMaskUtility",
                "YLFramework.Foundation.Attributes.CustomClassNameGUI", "YLFramework.Foundation.Attributes.CustomPropertyGUI",
                "YLFramework.Foundation.Attributes.DisableEdit" })
            {
                Assert.That(foundation.GetType(name), Is.Not.Null, name);
                Assert.That(AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetType(name) != null), Is.EqualTo(1), name);
            }
            foreach (string oldName in new[] { "YLFramework.Core.AssemblyTools", "YLFramework.Utilities.AssemblyTool.AssemblyTool",
                "YLFramework.Utilities.Extensions.LayerMaskUtility", "YLFramework.Core.CustomClassNameGUI",
                "YLFramework.Core.CustomPropertyGUI", "YLFramework.Core.DisableEdit",
                "YLFramework.Core.Attributes.CustomClassNameGUI", "YLFramework.Core.Attributes.CustomPropertyGUI" })
                Assert.That(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(oldName) != null), Is.False, oldName);
        }

        [Test]
        public void NewFoundationTypesHaveUniqueFullyQualifiedNames()
        {
            foreach (var type in new[] { typeof(ApplicationTool), typeof(CoroutineStarter), typeof(CustomAsyncOperation<>) })
            {
                Assert.That(type.Namespace, Is.EqualTo("YLFramework.Foundation"));
                Assert.That(type.Assembly, Is.SameAs(typeof(ApplicationTool).Assembly));
                var definitions = AppDomain.CurrentDomain.GetAssemblies()
                    .Count(assembly => assembly.GetType(type.FullName, false) != null);
                Assert.That(definitions, Is.EqualTo(1), type.FullName);
            }
        }

        [Test]
        public void SharedPoolTypesHaveOneFoundationOwnerAndOnlyNarrowSessionFriends()
        {
            var foundation = typeof(ApplicationTool).Assembly;
            foreach (var type in new[] { typeof(ObjectPool<>), typeof(IPool<>), typeof(IObjectFactory<>),
                typeof(DefaultObjectFactory<>), typeof(CustomObjectFactory<>), typeof(ListPool<>),
                typeof(DictionaryPool<,>), typeof(PoolCacheOrder), typeof(PoolPrewarmMode) })
            {
                Assert.That(type.Assembly, Is.SameAs(foundation), type.FullName);
                Assert.That(AppDomain.CurrentDomain.GetAssemblies()
                    .Count(assembly => assembly.GetType(type.FullName, false) != null), Is.EqualTo(1), type.FullName);
            }
            var session = foundation.GetType("YLFramework.Core.Singleton.SingletonSessionState", true);
            Assert.That(session.IsNotPublic, Is.True);
            Assert.That(foundation.GetCustomAttributes(typeof(InternalsVisibleToAttribute), false)
                .Cast<InternalsVisibleToAttribute>().Select(attribute => attribute.AssemblyName),
                Is.EquivalentTo(new[] { "YLFramework.Tests.EditMode", "YLFramework.Tests.PlayMode", "YLFramework.Editor",
                    "YLFramework.Actions.DOTween", "YLFramework.Actions.DOTween.Tests.EditMode", "YLFramework.Actions.DOTween.Tests.PlayMode" }));
        }

        [Test]
        public void ApplicationStateUsesEditorPlaymodeTransitionState()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            Assert.That(ApplicationTool.isPlaying, Is.EqualTo(EditorApplication.isPlayingOrWillChangePlaymode));
        }

        [Test]
        public void StartingOutsidePlayModeDoesNotCreateHostOrAdvanceEnumerator()
        {
            var before = UnityEngine.Object.FindObjectsByType<CoroutineStarter>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(before, Is.Empty);
            Assert.That(CoroutineStarter.Start(MustNotRun()), Is.Null);
            CoroutineStarter.Stop(null);
            Assert.That(UnityEngine.Object.FindObjectsByType<CoroutineStarter>(FindObjectsInactive.Include,
                FindObjectsSortMode.None), Is.Empty);
        }

        private static IEnumerator MustNotRun()
        {
            Assert.Fail("EditMode must not advance the coroutine.");
            yield break;
        }
    }
}
