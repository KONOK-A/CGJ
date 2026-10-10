using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Timing;
using Timer = YLFramework.Timing.Timer;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class TimerScopePlayModeTests
    {
        private TimerScope mScope;
        private TimerScope mOtherScope;
        private readonly List<Component> mHosts = new();
        private bool mIsolated;
        private static Type DriverType => typeof(Timer).Assembly.GetType("YLFramework.Timing.Internal.TimerDriver", true);
        private static Component CurrentHost => DriverType.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null) as Component;
        private static Component[] FindHosts() => Object.FindObjectsByType(DriverType,
            FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>().ToArray();

        [SetUp]
        public void SetUp()
        {
            mIsolated = false;
            Assert.That(FindHosts(), Is.Empty, "不接管用户已有或停用的 TimerDriver。");
            Assert.That(CurrentHost, Is.Null, "不接管尚未释放的静态 owner。");
            mIsolated = true;
            mScope = new TimerScope();
            mOtherScope = new TimerScope();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!mIsolated) yield break;
            mScope.Dispose();
            mOtherScope.Dispose();
            if (!ReferenceEquals(CurrentHost, null) && mHosts.Contains(CurrentHost)) Timer.Shutdown();
            foreach (var host in mHosts) if (host) Object.Destroy(host.gameObject);
            mHosts.Clear();
            yield return null;
        }

        private TimerHandle Post(Func<TimerHandle> post)
        {
            var before = FindHosts();
            Assert.That(before.All(mHosts.Contains), Is.True, "不接管并行创建的非自有宿主。");
            try { return post(); }
            finally
            {
                foreach (var host in FindHosts())
                    if (!before.Contains(host) && !mHosts.Contains(host)) mHosts.Add(host);
            }
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            var limit = Time.realtimeSinceStartupAsDouble + 3;
            while (!condition() && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            Assert.That(condition(), Is.True, "真实 Unity 帧未在期限内完成预期操作。");
        }

        [UnityTest]
        public IEnumerator PauseFreezesAcrossFramesWithoutBlockingOtherDueTasks()
        {
            var calls = 0;
            var paused = Post(() => mScope.PostNamed("cooldown", 0.04, () => calls++));
            Assert.That(mScope.Pause("cooldown"), Is.True);
            var remaining = paused.RemainingSeconds;
            var otherCalls = 0;
            var other = Post(() => Timer.Delay(0.01, () => otherCalls++));
            yield return new WaitForSecondsRealtime(0.08f);
            yield return Until(() => otherCalls == 1);
            Assert.That(paused.RemainingSeconds, Is.EqualTo(remaining));
            Assert.That(paused.Status, Is.EqualTo(TimerState.Paused));
            Assert.That(calls, Is.Zero);
            Assert.That(other.Status, Is.EqualTo(TimerState.Completed));
            Assert.That(mScope.Resume("cooldown"), Is.True);
            yield return Until(() => calls == 1);
            Assert.That(paused.Status, Is.EqualTo(TimerState.Completed));
            Assert.That(mScope.Contains("cooldown"), Is.False);
        }

        [UnityTest]
        public IEnumerator LastCallbackCanRebuildHostAndUseSameScopeNameWithoutOldCleanup()
        {
            TimerHandle replacement = default;
            var created = false;
            var calls = 0;
            var old = Post(() => mScope.PostNamed("rebuild", 0, () =>
            {
                Assert.That(mScope.Contains("rebuild"), Is.False);
                Timer.Shutdown();
                replacement = Post(() => mScope.PostNamed("rebuild", 100, () => calls++));
                created = true;
            }));
            var oldHost = CurrentHost;
            yield return Until(() => created);
            yield return null;
            Assert.That(old.Status, Is.EqualTo(TimerState.Completed), "末轮在 callback 前预留完成，不被 Shutdown 改为取消。");
            Assert.That(old.Cancel(), Is.False);
            Assert.That(mScope.TryGet("rebuild", out var found), Is.True);
            Assert.That(found, Is.EqualTo(replacement));
            Assert.That(replacement.Status, Is.EqualTo(TimerState.Scheduled));
            Assert.That(calls, Is.Zero);
            Assert.That(CurrentHost, Is.Not.SameAs(oldHost));
            Assert.That(FindHosts(), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OwnerScopeDisposeCancelsOnlyItsTasksAndDoesNotShutdownHost()
        {
            var owned = Post(() => mScope.PostNamed("scope", 100));
            owned.Pause();
            var other = Post(() => mOtherScope.PostNamed("scope", 100));
            var global = Post(() => Timer.Delay(100));
            var host = CurrentHost;
            mScope.Dispose();
            yield return null;
            Assert.That(owned.Status, Is.EqualTo(TimerState.Cancelled));
            Assert.That(other.Status, Is.EqualTo(TimerState.Scheduled));
            Assert.That(global.Status, Is.EqualTo(TimerState.Scheduled));
            Assert.That(CurrentHost, Is.SameAs(host));
            Assert.That(mOtherScope.TryGet("scope", out var found), Is.True);
            Assert.That(found, Is.EqualTo(other));
            Assert.That(mScope.Contains("scope"), Is.False);
        }
    }
}
