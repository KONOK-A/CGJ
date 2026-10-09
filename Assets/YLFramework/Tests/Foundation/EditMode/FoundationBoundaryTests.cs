using System;
using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Core.Pool;

namespace YLFramework.Foundation.Tests
{
    public class FoundationBoundaryTests
    {
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
