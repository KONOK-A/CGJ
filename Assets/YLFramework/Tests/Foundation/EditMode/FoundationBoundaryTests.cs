using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using YLFramework.Core.Pool;

namespace YLFramework.Foundation.Tests
{
    public class FoundationBoundaryTests
    {
        [Test]
        public void KernelKeepsItsAssetIdentityAndNeutralSourceClosureWithoutAbsorbingRuntimeAdapters()
        {
            const string runtimeRoot = "Assets/YLFramework/Runtime/";
            const string kernel = runtimeRoot + "Foundation/Kernel";
            Assert.That(AssetDatabase.AssetPathToGUID(kernel), Is.EqualTo("cbd6de2a25410264eb6ff85bef17fe73"));
            Assert.That(AssetDatabase.AssetPathToGUID(kernel + "/YLFramework.Foundation.Runtime.asmdef"),
                Is.EqualTo("6b2be150d22ce3f4c9d858f1dafc81a4"));
            Assert.That(Directory.Exists(runtimeRoot + "Shared"), Is.False);
            Assert.That(File.Exists(runtimeRoot + "Shared.meta"), Is.False);
            foreach (var retired in new[] { "Tools/AssemblyTool", "Tools/Attributes" })
            {
                Assert.That(Directory.Exists(runtimeRoot + retired), Is.False, retired);
                Assert.That(File.Exists(runtimeRoot + retired + ".meta"), Is.False, retired);
            }

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            var foundation = assemblies.Single(a => a.name == "YLFramework.Foundation.Runtime");
            Assert.That(CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(foundation.name),
                Is.EqualTo(kernel + "/YLFramework.Foundation.Runtime.asmdef"));
            Assert.That(foundation.sourceFiles.Select(Path.GetFullPath), Is.EquivalentTo(
                Directory.GetFiles(kernel, "*.cs", SearchOption.AllDirectories).Select(Path.GetFullPath)));
            var adapters = Directory.GetFiles(runtimeRoot + "Foundation", "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetFullPath(path).StartsWith(Path.GetFullPath(kernel) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)).Select(Path.GetFullPath).ToArray();
            Assert.That(adapters, Is.Not.Empty);
            Assert.That(assemblies.Single(a => a.name == "YLFramework.Runtime").sourceFiles.Select(Path.GetFullPath),
                Is.SupersetOf(adapters));
        }

        [Test]
        public void FoundationHasNoFrameworkResourceOrPluginAssemblyDependencies()
        {
            var assembly = typeof(ApplicationTool).Assembly;
            Assert.That(assembly.GetName().Name, Is.EqualTo("YLFramework.Foundation.Runtime"));
            var unexpected = assembly.GetReferencedAssemblies().Select(reference => reference.Name)
                .Where(name => name != "mscorlib" && name != "netstandard" && name != "System"
                    && !name.StartsWith("System.", StringComparison.Ordinal)
                    && !name.StartsWith("UnityEngine", StringComparison.Ordinal)
                    && !name.StartsWith("UnityEditor", StringComparison.Ordinal));
            Assert.That(unexpected, Is.Empty);
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
                Is.EquivalentTo(new[] { "YLFramework.Runtime", "YLFramework.Core.Runtime" }));
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
