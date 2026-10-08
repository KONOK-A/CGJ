using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Core.Singleton;

namespace YLFramework.Tests
{
    public sealed class RuntimeHostProbe : MonoSingletonHost<RuntimeHostProbe>
    {
        public int Releases;
        public void Release() => ReleaseHost();
        protected override void OnHostRelease() => Releases++;
    }

    [MonoSingletonPath("YL.Singleton.Path/Child")]
    public sealed class PathSingletonProbe : MonoSingleton<PathSingletonProbe> { }

    public class SingletonHostPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var host in Object.FindObjectsByType<RuntimeHostProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                host.Release();
            MonoSingletonProperty<PathSingletonProbe>.Dispose();
            var pathRoot = GameObject.Find("YL.Singleton.Path");
            if (pathRoot) Object.Destroy(pathRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedSameFrameReleaseDoesNotReadoptAnyRetiredHost()
        {
            var first = RuntimeHostProbe.Instance;
            first.Release();
            var second = RuntimeHostProbe.Instance;
            second.Release();
            var current = RuntimeHostProbe.Instance;
            Assert.That(current, Is.Not.SameAs(first));
            Assert.That(current, Is.Not.SameAs(second));
            Assert.That(first.Releases, Is.EqualTo(1));
            yield return null;
            Assert.That(first == null && second == null, Is.True);
            Assert.That(RuntimeHostProbe.Instance, Is.SameAs(current));
            Assert.That(Object.FindObjectsByType<RuntimeHostProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OwnedHostSurvivesSceneUnloadAndRecreatesAfterExternalDestroy()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("YL.Singleton.HostScene");
            SceneManager.SetActiveScene(scene);
            var host = RuntimeHostProbe.Instance;
            SceneManager.SetActiveScene(previous);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(host != null, Is.True);
            Object.Destroy(host.gameObject);
            yield return null;
            Assert.That(RuntimeHostProbe.HasInstance, Is.False);
            Assert.That(RuntimeHostProbe.Instance, Is.Not.SameAs(host));
        }

        [UnityTest]
        public IEnumerator ExistingMonoSingletonPathAndDisposeStillWork()
        {
            var first = PathSingletonProbe.Instance;
            Assert.That(first.transform.parent.name, Is.EqualTo("YL.Singleton.Path"));
            Assert.That(first.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            first.Dispose();
            var second = PathSingletonProbe.Instance;
            second.Dispose();
            var current = PathSingletonProbe.Instance;
            Assert.That(current, Is.Not.SameAs(first));
            Assert.That(current, Is.Not.SameAs(second));
            yield return null;
            Assert.That(first == null && second == null, Is.True);
            Assert.That(PathSingletonProbe.Instance, Is.SameAs(current));
        }
    }
}
