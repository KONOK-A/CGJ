using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace YLFramework.Foundation.Tests
{
    public class CoroutineStarterTests
    {
        private sealed class Probe : CustomAsyncOperation<Probe>
        {
            public void Finish() => Completed();
        }

        private CoroutineStarter _host;
        private readonly List<CoroutineStarter> _ownedHosts = new List<CoroutineStarter>();
        private GameObject _foreignObject;
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            Assert.That(FindHosts(), Is.Empty, "Fixture must not take ownership of an existing Foundation host.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var host in _ownedHosts)
                if (host) Object.Destroy(host.gameObject);
            _ownedHosts.Clear();
            _host = null;
            if (_foreignObject) Object.Destroy(_foreignObject);
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartCreatesOneIndependentNamedHostAndReusesIt()
        {
            Assert.That(ApplicationTool.isPlaying, Is.True);
            _foreignObject = new GameObject("CoroutineStarter");
            var ticks = 0;
            var first = StartOwned(Tick(() => ticks++));
            var originalHost = _host;
            var second = StartOwned(Tick(() => ticks++));
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(_host, Is.SameAs(originalHost));
            Assert.That(_host.gameObject, Is.Not.SameAs(_foreignObject));
            Assert.That(_host.name, Is.EqualTo("YLFramework.Foundation.CoroutineStarter"));
            Assert.That(FindHosts().Length, Is.EqualTo(1));
            yield return null;
            Assert.That(ticks, Is.GreaterThanOrEqualTo(4));
            CoroutineStarter.Stop(first);
            CoroutineStarter.Stop(second);
            Object.Destroy(_host.gameObject);
            yield return null;
            Assert.That(_foreignObject != null, Is.True);
        }

        [UnityTest]
        public IEnumerator StopOnlyStopsSpecifiedCoroutineAndNullDoesNotCreateHost()
        {
            CoroutineStarter.Stop(null);
            Assert.That(FindHosts(), Is.Empty);
            var stoppedTicks = 0;
            var runningTicks = 0;
            var stopped = StartOwned(Tick(() => stoppedTicks++));
            var running = StartOwned(Tick(() => runningTicks++));
            CoroutineStarter.Stop(stopped);
            var countAtStop = stoppedTicks;
            yield return null;
            yield return null;
            Assert.That(stoppedTicks, Is.EqualTo(countAtStop));
            Assert.That(runningTicks, Is.GreaterThan(1));
            CoroutineStarter.Stop(running);
        }

        [UnityTest]
        public IEnumerator HostAndCoroutineSurviveOriginatingSceneUnload()
        {
            var previous = SceneManager.GetActiveScene();
            _scene = SceneManager.CreateScene("YLFoundation.CoroutineFixture");
            SceneManager.SetActiveScene(_scene);
            var ticks = 0;
            Coroutine coroutine;
            try
            {
                coroutine = StartOwned(Tick(() => ticks++));
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
            }
            Assert.That(_host.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            yield return SceneManager.UnloadSceneAsync(_scene);
            Assert.That(_host != null, Is.True);
            var afterUnload = ticks;
            yield return null;
            yield return null;
            Assert.That(ticks, Is.GreaterThan(afterUnload));
            CoroutineStarter.Stop(coroutine);
        }

        [UnityTest]
        public IEnumerator DestroyedHostStopsItsCoroutineAndNextStartCreatesNewHost()
        {
            var oldTicks = 0;
            StartOwned(Tick(() => oldTicks++));
            var oldHost = _host;
            Object.Destroy(oldHost.gameObject);
            yield return null;
            Assert.That(oldHost == null, Is.True);
            Assert.That(FindHosts(), Is.Empty);
            var countAfterDestroy = oldTicks;
            var newTicks = 0;
            var coroutine = StartOwned(Tick(() => newTicks++));
            Assert.That(_host, Is.Not.SameAs(oldHost));
            yield return null;
            yield return null;
            Assert.That(oldTicks, Is.EqualTo(countAfterDestroy));
            Assert.That(newTicks, Is.GreaterThan(1));
            CoroutineStarter.Stop(coroutine);
        }

        [UnityTest]
        public IEnumerator YieldingOperationWaitsUntilCompletionThenResumes()
        {
            var operation = new Probe();
            var resumed = false;
            StartOwned(WaitForOperation(operation, () => resumed = true));
            yield return null;
            Assert.That(resumed, Is.False);
            operation.Finish();
            yield return null;
            yield return null;
            Assert.That(resumed, Is.True);
        }

        [UnityTest]
        public IEnumerator AbortCompletesOperationButDoesNotCancelProducerCoroutine()
        {
            var operation = new Probe();
            var producerTicks = 0;
            var completionCount = 0;
            var allowProducerCompletion = false;
            operation.AddCompleteEvent(_ => completionCount++);
            StartOwned(Produce(operation, () => producerTicks++, () => allowProducerCompletion));
            operation.Abort();
            var ticksAtAbort = producerTicks;
            yield return null;
            yield return null;
            Assert.That(operation.isDone, Is.True);
            Assert.That(operation.error, Is.EqualTo("request abort!"));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(producerTicks, Is.GreaterThan(ticksAtAbort));
            allowProducerCompletion = true;
            yield return null;
            yield return null;
            Assert.That(operation.error, Is.Null);
            Assert.That(completionCount, Is.EqualTo(1));
        }

        private Coroutine StartOwned(IEnumerator enumerator)
        {
            var before = FindHosts();
            Coroutine coroutine;
            try
            {
                coroutine = CoroutineStarter.Start(enumerator);
            }
            finally
            {
                // Track only this fixture's new hosts, even when Start or its assertion fails.
                foreach (var host in FindHosts())
                    if (Array.IndexOf(before, host) < 0) _ownedHosts.Add(host);
            }
            var hosts = FindHosts();
            if (hosts.Length == 1) _host = hosts[0];
            Assert.That(hosts.Length, Is.EqualTo(1));
            return coroutine;
        }

        private static CoroutineStarter[] FindHosts() => Object.FindObjectsByType<CoroutineStarter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static IEnumerator Tick(Action tick)
        {
            while (true)
            {
                tick();
                yield return null;
            }
        }

        private static IEnumerator WaitForOperation(Probe operation, Action resumed)
        {
            yield return operation;
            resumed();
        }

        private static IEnumerator Produce(Probe operation, Action tick, Func<bool> canFinish)
        {
            while (!canFinish())
            {
                tick();
                yield return null;
            }
            operation.Finish();
        }
    }
}
