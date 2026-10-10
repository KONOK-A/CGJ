using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Timing;
using Object = UnityEngine.Object;

namespace YLFramework.Foundation.Tests
{
    public class TimerGameObjectBindingTests
    {
        private TimerScheduler scheduler;
        private GameObject a;
        private GameObject b;

        [SetUp]
        public void SetUp()
        {
            scheduler = new TimerScheduler(0, 0, 0);
            a = new GameObject("YLTimer.BindingA");
            b = new GameObject("YLTimer.BindingB");
        }

        [TearDown]
        public void TearDown()
        {
            scheduler.Dispose();
            if (a) Object.DestroyImmediate(a);
            if (b) Object.DestroyImmediate(b);
        }

        private static int ListenerCount(GameObject obj)
        {
            var type = typeof(TimerScheduler).Assembly.GetType("YLFramework.Timing.Internal.TimerLifetimeListener", true);
            var listener = obj.GetComponent(type);
            if (!listener) return 0;
            var bindings = type.GetField("_bindings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(listener);
            return (int)bindings.GetType().GetProperty("Count").GetValue(bindings);
        }

        [Test]
        public void RebindingRemovesOldListenerAndPausedRunFollowsNewOwner()
        {
            var cancels = 0;
            var handle = scheduler.Delay(10, onCancelled: _ => cancels++);
            Assert.That(handle.BindTo(a), Is.True);
            Assert.That(handle.Pause(), Is.True);
            Assert.That(handle.BindTo(b), Is.True);
            Assert.That(ListenerCount(a), Is.Zero);
            a.SetActive(false);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Paused));
            b.SetActive(false);
            scheduler.Advance(0, 0, 1);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Cancelled));
            Assert.That(cancels, Is.EqualTo(1));
            Assert.That(ListenerCount(b), Is.Zero);
        }

        [Test]
        public void NeverActiveDestroyedOwnerIsFoundEvenForPausedRun()
        {
            a.SetActive(false);
            var handle = scheduler.Delay(10);
            Assert.That(handle.BindTo(a, TimerGameObjectLifetime.CancelOnDestroy), Is.True);
            handle.Pause();
            Object.DestroyImmediate(a);
            scheduler.Advance(0, 0, 1);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Cancelled));
        }

        [Test]
        public void InvalidAndInactiveOwnersRejectActiveBindingWithoutSuccessfulCallback()
        {
            a.SetActive(false);
            var calls = 0;
            var handle = scheduler.Delay(0, () => calls++);
            Assert.That(handle.BindTo(a), Is.False);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Cancelled));
            var missing = scheduler.Delay(0, () => calls++);
            Assert.That(missing.BindTo(null), Is.False);
            scheduler.Advance(1, 1, 1);
            Assert.That(calls, Is.Zero);
            Assert.That(ListenerCount(a), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => missing.BindTo(a, (TimerGameObjectLifetime)99));
        }

        [Test]
        public void DestroyOnlyBindingSurvivesDisableAndListenerComponentDisable()
        {
            var handle = scheduler.Delay(10);
            handle.BindTo(a, TimerGameObjectLifetime.CancelOnDestroy);
            var type = typeof(TimerScheduler).Assembly.GetType("YLFramework.Timing.Internal.TimerLifetimeListener", true);
            ((Behaviour)a.GetComponent(type)).enabled = false;
            a.SetActive(false);
            scheduler.Advance(0, 0, 1);
            Assert.That(handle.IsActive, Is.True);
            Object.DestroyImmediate(a);
            scheduler.Advance(0, 0, 2);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Cancelled));
        }

        [Test]
        public void TerminalPathsReleaseBindingBeforeCallbacksAndClearRunReferences()
        {
            var handle = scheduler.Delay(0, () =>
            {
                Assert.That(ListenerCount(a), Is.Zero);
                a.SetActive(false);
            }, onCancelled: _ => Assert.Fail("成功末轮不能 Abort"));
            handle.BindTo(a);
            scheduler.Advance(0, 0, 1);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Completed));
            var run = typeof(TimerHandle).GetField("_run", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(handle);
            Assert.That(run.GetType().GetField("Binding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run), Is.Null);
            a.SetActive(true);
            var fault = scheduler.Delay(0, () => throw new InvalidOperationException("binding fault"));
            fault.BindTo(a);
            Assert.Throws<InvalidOperationException>(() => scheduler.Advance(0, 0, 2));
            Assert.That(fault.Status, Is.EqualTo(TimerState.Faulted));
            Assert.That(ListenerCount(a), Is.Zero);
            var invalid = scheduler.Delay(10);
            invalid.BindTo(a);
            scheduler.Dispose();
            Assert.That(invalid.Status, Is.EqualTo(TimerState.Invalidated));
            Assert.That(ListenerCount(a), Is.Zero);
        }

        [Test]
        public void OwnerFallbackAttemptsAllCancellationsAndReadyTasksBeforeReportingFailure()
        {
            var failure = new InvalidOperationException("binding cancellation failure");
            var first = scheduler.Delay(100, onCancelled: _ => throw failure);
            var notified = 0;
            var second = scheduler.Delay(100, onCancelled: _ => notified++);
            first.BindTo(a);
            second.BindTo(a);
            second.Pause();
            var calls = 0;
            var unrelated = scheduler.Delay(0, () => calls++);
            a.SetActive(false);
            Assert.That(Assert.Throws<InvalidOperationException>(() => scheduler.Advance(0, 0, 1)), Is.SameAs(failure));
            Assert.That(first.Status, Is.EqualTo(TimerState.Cancelled));
            Assert.That(second.Status, Is.EqualTo(TimerState.Cancelled));
            Assert.That(notified, Is.EqualTo(1));
            Assert.That(ListenerCount(a), Is.Zero);
            Assert.That(unrelated.Status, Is.EqualTo(TimerState.Completed));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DisableSnapshotCannotCancelReenabledNewGeneration()
        {
            using var slot = new ReusableTimer(scheduler);
            TimerHandle next = default;
            var cancels = 0;
            var old = slot.Start(10, onCancelled: _ =>
            {
                cancels++;
                a.SetActive(true);
                next = slot.Start(10);
                next.BindTo(a);
            });
            old.BindTo(a);
            a.SetActive(false);
            scheduler.Advance(0, 0, 1);
            Assert.That(old.Status, Is.EqualTo(TimerState.Invalidated));
            Assert.That(next.IsActive, Is.True);
            Assert.That(cancels, Is.EqualTo(1));
            Assert.That(ListenerCount(a), Is.EqualTo(1));
        }
    }
}
