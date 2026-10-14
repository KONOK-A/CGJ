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
            const string root = "Assets/YLFramework/Runtime/Foundation/";
            // 精确保持原 Kernel 49 文件闭包，合并目录不代表可偷渡上层适配与 Runtime DLL。
            var directories = new[] { "Async", "Utilities", "Attributes", "Serialization", "Update", "Internal", "Timing", "Timing/Internal", "Pool/Collections" };
            var subscriptions = new[] { "CallbackUnRegister", "EventBus", "EventChannel", "EventChannelBase", "EventKey",
                "EventRegistrations", "IEventSource", "IUnRegister", "OrEvent", "RegisterListExtension", "SingleUseUnRegister", "SubscriptionCleanup", "SubscriptionScope" };
            var files = new[] { "Singleton/Internal/SingletonSessionState.cs", "Pool/Object/ObjectPool.cs", "Pool/Object/PoolOptions.cs",
                "Pool/Abstractions/IPool.cs", "Pool/Abstractions/IObjectFactory.cs", "Pool/Factories/CustomObjectFactory.cs", "Pool/Factories/DefaultObjectFactory.cs" };
            var sources = directories.SelectMany(directory => Directory.GetFiles(root + directory, "*.cs"))
                .Concat(subscriptions.Select(name => root + "Events/Subscriptions/" + name + ".cs"))
                .Concat(files.Select(path => root + path)).ToArray();
            Assert.That(sources.Length, Is.EqualTo(49));
            Assert.That(sources.All(File.Exists), Is.True);
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
                var trustedRoot = Path.GetFullPath(EditorApplication.applicationContentsPath) + Path.DirectorySeparatorChar;
                builder.excludeReferences = builder.defaultReferences.Where(path =>
                    !Path.GetFullPath(path).StartsWith(trustedRoot, StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(path).Contains("TestRunner") ||
                    (!editor && Path.GetFileName(path).StartsWith("UnityEditor", StringComparison.Ordinal))).ToArray();
                var references = builder.defaultReferences.Except(builder.excludeReferences).ToArray();
                Assert.That(references, Is.Not.Empty);
                Assert.That(references.Any(path => Path.GetFileName(path).StartsWith("YLFramework") ||
                    Path.GetFileName(path).StartsWith("XF") || Path.GetFileName(path) == "Newtonsoft.Json.dll"), Is.False);
                CompilerMessage[] messages = null;
                builder.buildFinished += (_, result) => messages = result;
                Assert.That(builder.Build(), Is.True);
                while (builder.status != AssemblyBuilderStatus.Finished) yield return null;
                Assert.That(messages, Is.Not.Null);
                Assert.That(messages.Where(m => m.type == CompilerMessageType.Error).Select(m => m.message), Is.Empty);
                Assert.That(File.Exists(output), Is.True);
            }
            finally { Directory.Delete(folder, true); }
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
