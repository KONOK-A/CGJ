using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Utilities.Timing;
using Timer = YLFramework.Utilities.Timing.Timer;

namespace YLFramework.Tests
{
    public class TimerScopeTests
    {
        private Timer mTimer;
        private TimerScope mScope;

        [SetUp]
        public void SetUp()
        {
            Timer.Shutdown();
            mTimer = Timer.Instance;
            mScope = new TimerScope();
        }

        [TearDown]
        public void TearDown()
        {
            mScope.Dispose();
            Timer.Shutdown();
        }

        private void TickUnscaled(float now)
        {
            var field = typeof(Timer).GetField("mUnscaledTimeHeap", BindingFlags.Instance | BindingFlags.NonPublic);
            var method = typeof(Timer).GetMethod("UpdateHeap", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(mTimer, new[] { field.GetValue(mTimer), (object)now });
        }

        [Test]
        public void ScopeNamesAreOrdinalIsolatedAndQueriesNeverCreateHosts()
        {
            var first = mTimer.PostNamed("cooldown", null, 100, scope: mScope);
            var second = mTimer.PostNamed("cooldown", null, 100);
            var upper = mTimer.PostNamed("Cooldown", null, 100, scope: mScope);
            Assert.That(mScope.TryGet("cooldown", out var found), Is.True);
            Assert.That(found, Is.SameAs(first));
            Assert.That(Timer.TryGet("cooldown", out found), Is.True);
            Assert.That(found, Is.SameAs(second));
            Assert.That(mScope.TryGet("Cooldown", out found), Is.True);
            Assert.That(found, Is.SameAs(upper));
            Assert.Throws<InvalidOperationException>(() => mTimer.PostNamed("cooldown", null, 1, scope: mScope));
            Assert.That(first.State, Is.EqualTo(TimerState.Scheduled));
            Assert.Throws<ArgumentException>(() => mTimer.PostNamed(" ", null, 1));
            Timer.Shutdown();
            Assert.That(Timer.Contains("cooldown"), Is.False);
            Assert.That(Timer.TryGetInstance(out _), Is.False);
            Assert.That(first.State, Is.EqualTo(TimerState.Cancelled));
        }

        [Test]
        public void ReplacementValidatesBeforeCancellingAndOldHandlesCannotCancelNewNames()
        {
            var old = mTimer.PostNamed("timer", null, 100, scope: mScope);
            Assert.Throws<ArgumentOutOfRangeException>(() => mTimer.ReplaceNamed("timer", null, float.NaN, scope: mScope));
            Assert.That(old.IsActive, Is.True);
            var current = mTimer.ReplaceNamed("timer", null, 100, scope: mScope);
            old.Cancel();
            Assert.That(old.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(old.HeapIndex, Is.Zero);
            Assert.That(mScope.TryGet("timer", out var found), Is.True);
            Assert.That(found, Is.SameAs(current));
            Assert.That(mScope.Cancel("timer"), Is.True);
            Assert.That(current.IsRecycled, Is.True);
            Assert.That(mScope.Cancel("timer"), Is.False);
        }

        [Test]
        public void PauseFreezesRemainingRemovesHeapAndCancelProtectsTerminalState()
        {
            var item = mTimer.PostNamed("pause", null, 5, true, scope: mScope);
            item.SortScore = Time.unscaledTime + 5;
            Assert.That(item.RemainingSeconds, Is.InRange(4.99f, 5.01f));
            Assert.That(mScope.Pause("pause"), Is.True);
            Assert.That(item.State, Is.EqualTo(TimerState.Paused));
            Assert.That(item.HeapIndex, Is.Zero);
            var remaining = item.RemainingSeconds;
            Assert.Throws<InvalidOperationException>(() => item.Set(null, 1, 1));
            Assert.Throws<InvalidOperationException>(item.Recycle2Cache);
            Assert.Throws<InvalidOperationException>(() => mTimer.Post2Really(item));
            var calls = 0;
            var later = mTimer.Post2Really(_ => calls++, 0);
            later.SortScore = -1;
            TickUnscaled(Time.unscaledTime);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(item.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(mScope.Resume("pause"), Is.True);
            Assert.That(item.HeapIndex, Is.GreaterThan(0));
            Assert.That(item.SortScore, Is.EqualTo(Time.unscaledTime + remaining).Within(0.01f));
            item.Cancel();
            Assert.That(item.IsActive, Is.False);
            Assert.That(item.IsRecycled, Is.True);
            Assert.That(item.RemainingSeconds, Is.Zero);
            Assert.That(item.Pause() || item.Resume(), Is.False);
            item.Cancel();
            Assert.That(item.State, Is.EqualTo(TimerState.Cancelled));
        }

        [Test]
        public void LastCallbackFreesNameAndOldFinallyDoesNotRemoveReplacement()
        {
            TimeItem replacement = null;
            var old = mTimer.PostNamed("same", _ =>
            {
                Assert.That(mScope.Contains("same"), Is.False);
                replacement = mTimer.PostNamed("same", null, 100, true, scope: mScope);
            }, 0, true, scope: mScope);
            old.SortScore = -1;
            TickUnscaled(Time.unscaledTime);
            Assert.That(old.State, Is.EqualTo(TimerState.Completed));
            old.Cancel();
            Assert.That(old.State, Is.EqualTo(TimerState.Completed));
            Assert.That(mScope.TryGet("same", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
        }

        [Test]
        public void CallbackPauseTargetsNextRepeatAndLastIterationCannotPause()
        {
            TimeItem item = null;
            var calls = 0;
            item = mTimer.PostNamed("repeat", _ =>
            {
                calls++;
                Assert.That(item.RemainingSeconds, Is.Zero);
                if (calls == 1)
                {
                    Assert.That(mScope.Contains("repeat"), Is.True);
                    Assert.That(item.Pause(), Is.True);
                }
                else Assert.That(item.Pause(), Is.False);
            }, 2, true, 2, mScope);
            item.SortScore = -1;
            TickUnscaled(Time.unscaledTime);
            Assert.That(item.State, Is.EqualTo(TimerState.Paused));
            Assert.That(item.RemainingSeconds, Is.EqualTo(2));
            Assert.That(item.Resume(), Is.True);
            TickUnscaled(item.SortScore + 1);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(item.State, Is.EqualTo(TimerState.Completed));
        }

        [Test]
        public void CallbackCanUndoNextPauseAndReplaceItsLoopWithoutOldRescheduling()
        {
            TimeItem loop = null;
            TimeItem replacement = null;
            loop = mTimer.PostNamed("loop", _ =>
            {
                Assert.That(loop.Pause() && loop.Resume(), Is.True);
                replacement = mTimer.ReplaceNamed("loop", null, 100, true, scope: mScope);
            }, 1, true, -1, mScope);
            loop.SortScore = -1;
            TickUnscaled(Time.unscaledTime);
            Assert.That(loop.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(loop.IsRecycled, Is.True);
            Assert.That(mScope.TryGet("loop", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
        }

        [Test]
        public void ScopeDisposeCancelsPausedAnonymousAndExecutingAndRejectsNewPosts()
        {
            var paused = mTimer.Post2Scale(null, 100, 1, mScope);
            paused.Pause();
            TimeItem executing = null;
            executing = mTimer.PostNamed("dispose", _ =>
            {
                mScope.Dispose();
                Assert.Throws<ObjectDisposedException>(() => mTimer.PostNamed("new", null, 0, scope: mScope));
            }, 0, true, -1, mScope);
            executing.SortScore = -1;
            TickUnscaled(Time.unscaledTime);
            Assert.That(paused.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(executing.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(paused.IsRecycled && executing.IsRecycled, Is.True);
            Assert.That(mScope.Contains("dispose"), Is.False);
            Assert.DoesNotThrow(mScope.Dispose);
        }

        [Test]
        public void ExceptionAndRepostPreserveLeaseAndAllTerminalStates()
        {
            var failure = new InvalidOperationException("expected timer failure");
            var broken = mTimer.PostNamed("broken", _ => throw failure, 0, true, scope: mScope);
            broken.SortScore = -1;
            Assert.That(Assert.Throws<TargetInvocationException>(() => TickUnscaled(Time.unscaledTime)).InnerException, Is.SameAs(failure));
            Assert.That(broken.State, Is.EqualTo(TimerState.Faulted));
            Assert.That(mScope.Contains("broken"), Is.False);
            broken.Cancel();
            Assert.That(broken.State, Is.EqualTo(TimerState.Faulted));

            TimeItem reposted = null;
            var calls = 0;
            reposted = mTimer.PostNamed("old-name", _ =>
            {
                calls++;
                if (calls == 1) mTimer.Post2Really(reposted, mScope);
            }, 1, true, 2, mScope);
            reposted.SortScore = -1;
            TickUnscaled(Time.unscaledTime);
            Assert.That(reposted.State, Is.EqualTo(TimerState.Scheduled));
            Assert.That(mScope.Contains("old-name"), Is.False);
            TickUnscaled(reposted.SortScore + 1);
            Assert.That(reposted.State, Is.EqualTo(TimerState.Completed));
        }

        [Test]
        public void ResetCancelsPausedAndPendingButScopeCanBeReused()
        {
            var paused = mTimer.PostNamed("paused", null, 100, scope: mScope);
            paused.Pause();
            var pending = mTimer.Post2Really(null, 100, 1, mScope);
            mTimer.ResetMgr();
            Assert.That(paused.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(pending.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(mTimer.PostNamed("paused", null, 100, scope: mScope).IsActive, Is.True);
        }

        [Test]
        public void NamedQueryReleasesDestroyedNeverActiveHostWithoutCreatingReplacement()
        {
            Timer.Shutdown();
            var root = new GameObject("Scope.InactiveOwner");
            root.SetActive(false);
            root.AddComponent<Timer>();
            mTimer = Timer.Instance;
            var item = mTimer.PostNamed("dead", null, 100, scope: mScope);
            var paused = mTimer.Post2Scale(null, 100);
            paused.Pause();
            UnityEngine.Object.DestroyImmediate(root);
            Assert.That(item.IsActive, Is.False);
            Assert.That(item.RemainingSeconds, Is.Zero);
            Assert.That(mScope.TryGet("dead", out _), Is.False);
            Assert.That(item.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(paused.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(item.callback, Is.Null);
            Assert.That(Timer.TryGetInstance(out _), Is.False);
        }
    }
}
