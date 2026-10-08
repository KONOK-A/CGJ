using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Utilities.Timing;
using Timer = YLFramework.Utilities.Timing.Timer;

namespace YLFramework.Tests
{
    public class TimerScopePlayModeTests
    {
        private TimerScope mScope;
        private float mTimeScale;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            mTimeScale = Time.timeScale;
            Time.timeScale = 1;
            Timer.Shutdown();
            mScope = new TimerScope();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            mScope.Dispose();
            Time.timeScale = mTimeScale;
            Timer.Shutdown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseFreezesAcrossFramesWithoutBlockingOtherDueTasks()
        {
            var timer = Timer.Instance;
            var calls = 0;
            var paused = timer.PostNamed("cooldown", _ => calls++, 0.04f, true, scope: mScope);
            Assert.That(paused.Pause(), Is.True);
            var remaining = paused.RemainingSeconds;
            var otherCalls = 0;
            var other = timer.Post2Really(_ => otherCalls++, 0.01f);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(paused.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(calls, Is.Zero);
            Assert.That(otherCalls, Is.EqualTo(1));
            Assert.That(other.State, Is.EqualTo(TimerState.Completed));
            Assert.That(paused.Resume(), Is.True);
            var limit = Time.realtimeSinceStartup + 2;
            while (calls == 0 && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(paused.State, Is.EqualTo(TimerState.Completed));
            Assert.That(mScope.Contains("cooldown"), Is.False);
        }

        [UnityTest]
        public IEnumerator LastCallbackCanRebuildHostAndUseSameScopeNameWithoutOldCleanup()
        {
            var oldHost = Timer.Instance;
            TimeItem replacement = null;
            var old = oldHost.PostNamed("rebuild", _ =>
            {
                Timer.Shutdown();
                replacement = Timer.Instance.PostNamed("rebuild", null, 100, true, scope: mScope);
            }, 0, true, scope: mScope);
            var limit = Time.realtimeSinceStartup + 2;
            while (replacement == null && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(replacement, Is.Not.Null);
            yield return null;
            Assert.That(old.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(old.IsRecycled, Is.True);
            Assert.That(mScope.TryGet("rebuild", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
            Assert.That(Timer.Instance, Is.Not.SameAs(oldHost));
        }

        [UnityTest]
        public IEnumerator OwnerScopeDisposeCancelsOnlyItsTasksAndDoesNotShutdownHost()
        {
            var host = Timer.Instance;
            var owned = host.PostNamed("scope", null, 100, scope: mScope);
            owned.Pause();
            var global = host.PostNamed("scope", null, 100);
            mScope.Dispose();
            yield return null;
            Assert.That(owned.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(global.State, Is.EqualTo(TimerState.Scheduled));
            Assert.That(Timer.Instance, Is.SameAs(host));
            Assert.That(Timer.TryGet("scope", out var found), Is.True);
            Assert.That(found, Is.SameAs(global));
        }
    }
}
