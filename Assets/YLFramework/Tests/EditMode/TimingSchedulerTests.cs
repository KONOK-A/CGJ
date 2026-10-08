using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Utilities.Timing;
using Timer = YLFramework.Utilities.Timing.Timer;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class TimingSchedulerTests
    {
        private Timer mTimer;

        [SetUp]
        public void SetUp()
        {
            Timer.Shutdown();
            mTimer = Timer.Instance;
            mTimer.enabled = false;
            SetClocks(0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Timer.Shutdown();
        }

        private void SetClocks(float scaled, float unscaled)
        {
            typeof(Timer).GetField("mCurrentScaleTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mTimer, scaled);
            typeof(Timer).GetField("mCurrentUnscaledTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mTimer, unscaled);
        }

        // 定向驱动真实堆处理，固定时间边界，不给运行时代码新增测试时钟接口。
        private void Tick(float scaled, float unscaled)
        {
            SetClocks(scaled, unscaled);
            var update = typeof(Timer).GetMethod("UpdateHeap", BindingFlags.Instance | BindingFlags.NonPublic);
            var unscaledHeap = typeof(Timer).GetField("mUnscaledTimeHeap", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mTimer);
            var scaledHeap = typeof(Timer).GetField("mScaleTimeHeap", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mTimer);
            update.Invoke(mTimer, new[] { unscaledHeap, (object)unscaled });
            update.Invoke(mTimer, new[] { scaledHeap, (object)scaled });
        }

        [Test]
        public void SeparateClocksRequireStrictlyPassedDeadlinesAndRunUnscaledFirst()
        {
            SetClocks(10, 100);
            var trace = new List<string>();
            var scaled = mTimer.Post2Scale(_ => trace.Add("scaled"), 2);
            var unscaled = mTimer.Post2Really(_ => trace.Add("unscaled"), 2);
            Assert.That(scaled.SortScore, Is.EqualTo(12));
            Assert.That(unscaled.SortScore, Is.EqualTo(102));
            Tick(12, 102);
            Assert.That(trace, Is.Empty);
            Tick(12, 103);
            CollectionAssert.AreEqual(new[] { "unscaled" }, trace);
            Tick(13, 103);
            CollectionAssert.AreEqual(new[] { "unscaled", "scaled" }, trace);

            trace.Clear();
            mTimer.Post2Scale(_ => trace.Add("scaled"), 0);
            mTimer.Post2Really(_ => trace.Add("unscaled"), 0);
            Tick(14, 104);
            CollectionAssert.AreEqual(new[] { "unscaled", "scaled" }, trace);
        }

        [Test]
        public void RepeatsScheduleFromNowAndCancellationIsRemovedWithoutCallingBack()
        {
            var ticks = new List<int>();
            var repeated = mTimer.Post2Really(ticks.Add, 1, 2);
            var cancelled = mTimer.Post2Really(_ => Assert.Fail("Cancelled callback ran."), 2);
            cancelled.Cancel();
            var later = mTimer.Post2Really(_ => ticks.Add(99), 3);
            Tick(0, 4);
            CollectionAssert.AreEqual(new[] { 1, 99 }, ticks);
            Assert.That(repeated.SortScore, Is.EqualTo(5));
            Assert.That(cancelled.IsRecycled && later.IsRecycled, Is.True);
            Tick(0, 5);
            Assert.That(ticks.Count, Is.EqualTo(2));
            Tick(0, 6);
            CollectionAssert.AreEqual(new[] { 1, 99, 2 }, ticks);
            Assert.That(repeated.IsRecycled, Is.True);
        }

        [Test]
        public void ZeroIntervalRepeatAndCallbackPostedTaskWaitForClockAdvance()
        {
            var calls = 0;
            var addedCalls = 0;
            var repeated = mTimer.Post2Really(_ =>
            {
                calls++;
                if (calls == 1) mTimer.Post2Really(__ => addedCalls++, 0);
            }, -1, -1);
            Tick(0, 1);
            Tick(0, 1);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(addedCalls, Is.Zero);
            Tick(0, 2);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(addedCalls, Is.EqualTo(1));
            repeated.Cancel();
            Tick(0, 2);
            Assert.That(repeated.IsRecycled, Is.True);
        }

        [Test]
        public void CallbackMayRepostItsPoppedHandleWithoutPrematureRelease()
        {
            var calls = 0;
            TimeItem item = null;
            item = mTimer.Post2Really(_ =>
            {
                calls++;
                if (calls == 1) mTimer.Post2Really(item);
            }, 1, 2);
            Tick(0, 2);
            Assert.That(item.IsRecycled, Is.False);
            Assert.That(item.HeapIndex, Is.GreaterThan(0));
            Tick(0, 4);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(item.IsRecycled, Is.True);
        }

        [Test]
        public void CallbackExceptionReleasesOnlyCurrentTaskAndLaterUpdateStillWorks()
        {
            var failure = new InvalidOperationException("timing callback");
            var broken = mTimer.Post2Really(_ => throw failure, 0, -1);
            var calls = 0;
            var pending = mTimer.Post2Really(_ => calls++, 2);
            var thrown = Assert.Throws<TargetInvocationException>(() => Tick(0, 1));
            Assert.That(thrown.InnerException, Is.SameAs(failure));
            Assert.That(broken.IsRecycled, Is.True);
            Assert.That(broken.callback, Is.Null);
            Assert.That(pending.IsRecycled, Is.False);
            Tick(0, 3);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantUpdateAndShutdownFromCallbackCannotRepeatOrAcceptOldHostPosts()
        {
            var calls = 0;
            var old = mTimer;
            var pending = old.Post2Scale(_ => Assert.Fail("Shutdown task ran."), 100);
            var repeated = old.Post2Really(_ =>
            {
                calls++;
                old.UpdateMgr();
                Timer.Shutdown();
            }, 0, -1);
            repeated.SortScore = -100;
            old.UpdateMgr();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(repeated.IsRecycled && pending.IsRecycled, Is.True);
            Assert.Throws<InvalidOperationException>(() => old.Post2Scale(TimeItem.Allocate(null, 0)));
            mTimer = Timer.Instance;
            Assert.That(mTimer, Is.Not.SameAs(old));
        }

        [Test]
        public void AwakeAndFirstInstanceAccessDoNotInitializeTwice()
        {
            Timer.Shutdown();
            var root = new GameObject("Timing.Borrowed");
            try
            {
                var host = root.AddComponent<Timer>();
                var pending = host.Post2Scale(null, 100);
                Assert.That(Timer.Instance, Is.SameAs(host));
                Assert.That(pending.IsRecycled, Is.False);
                Timer.Shutdown();
                Assert.That(root != null, Is.True);
                Assert.That(root.GetComponent<Timer>(), Is.Null);
                Assert.That(pending.IsRecycled, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DestroyedNeverActiveBorrowedHostsStillReleaseManagedTasks()
        {
            Timer.Shutdown();
            var root = new GameObject("Timing.Inactive");
            root.SetActive(false);
            root.AddComponent<Timer>();
            var host = Timer.Instance;
            var pending = host.Post2Scale(null, 100);
            Object.DestroyImmediate(root);
            Timer.Shutdown();
            Assert.That(pending.IsRecycled, Is.True);

        }

        [Test]
        public void SessionResetCancelsHeapAndPausedTasksWithoutRelyingOnHostResetOrder()
        {
            var pending = mTimer.Post2Scale(null, 100);
            var paused = mTimer.PostNamed("session", null, 100);
            paused.Pause();
            typeof(Timer).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Assert.That(pending.IsRecycled, Is.True);
            Assert.That(paused.State, Is.EqualTo(TimerState.Cancelled));
            Assert.That(Timer.Contains("session"), Is.False);
            Assert.That(Timer.Instance, Is.SameAs(mTimer));
            Assert.That(mTimer.PostNamed("session", null, 100), Is.Not.Null);
        }
    }
}
