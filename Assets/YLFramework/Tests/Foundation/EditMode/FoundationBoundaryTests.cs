using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
