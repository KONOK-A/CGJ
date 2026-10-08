using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Utilities.Timing;
using Timer = YLFramework.Utilities.Timing.Timer;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class TimingPlayModeTests
    {
        private float mTimeScale;
        private GameObject mBorrowedRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            mTimeScale = Time.timeScale;
            Time.timeScale = 1;
            Timer.Shutdown();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = mTimeScale;
            Timer.Shutdown();
            if (mBorrowedRoot) Object.Destroy(mBorrowedRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutomaticUpdatesUseTheirOwnClocksWhileGameTimeIsPaused()
        {
            Time.timeScale = 0;
            var timer = Timer.Instance;
            var scaledCalls = 0;
            var realCalls = 0;
            timer.Post2Scale(_ => scaledCalls++, 0.02f);
            timer.Post2Really(_ => realCalls++, 0.02f);
            var limit = Time.realtimeSinceStartup + 2;
            while (realCalls == 0 && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(realCalls, Is.EqualTo(1));
            Assert.That(scaledCalls, Is.Zero);
            Time.timeScale = 1;
            limit = Time.realtimeSinceStartup + 2;
            while (scaledCalls == 0 && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(scaledCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OwnedHostUsesFrameworkHierarchyAndSurvivesSceneUnload()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("YL.Timing.TestScene");
            Timer timer = null;
            try
            {
                SceneManager.SetActiveScene(scene);
                timer = Timer.Instance;
                Assert.That(timer.transform.parent.name, Is.EqualTo("Timing"));
                Assert.That(timer.transform.root.name, Is.EqualTo("YLFramework"));
                Assert.That(timer.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            }
            finally { SceneManager.SetActiveScene(previous); }
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.That(timer != null, Is.True);
            var pending = timer.Post2Really(null, 100);
            Object.Destroy(timer.gameObject);
            yield return null;
            Assert.That(pending.IsRecycled, Is.True);
            Assert.That(Timer.Instance, Is.Not.SameAs(timer));
        }

        [UnityTest]
        public IEnumerator RepeatedSameFrameShutdownCannotReadoptOldHostsOrClearReplacementTasks()
        {
            var first = Timer.Instance;
            var oldItem = first.Post2Really(null, 100);
            Timer.Shutdown();
            var second = Timer.Instance;
            Timer.Shutdown();
            var current = Timer.Instance;
            var pending = current.Post2Really(null, 100);
            Assert.That(current, Is.Not.SameAs(first));
            Assert.That(current, Is.Not.SameAs(second));
            Assert.That(oldItem.IsRecycled, Is.True);
            yield return null;
            Assert.That(pending.IsRecycled, Is.False);
            Assert.That(Timer.Instance, Is.SameAs(current));
        }

        [UnityTest]
        public IEnumerator InactiveBorrowedHostIsNotActivatedAndShutdownPreservesItsGameObject()
        {
            mBorrowedRoot = new GameObject("Timing.Borrowed");
            mBorrowedRoot.SetActive(false);
            var borrowed = mBorrowedRoot.AddComponent<Timer>();
            Assert.That(Timer.Instance, Is.SameAs(borrowed));
            var pending = borrowed.Post2Really(null, 100);
            Assert.That(mBorrowedRoot.activeSelf, Is.False);
            Assert.That(mBorrowedRoot.transform.parent, Is.Null);
            Timer.Shutdown();
            Assert.That(pending.IsRecycled, Is.True);
            yield return null;
            Assert.That(mBorrowedRoot != null, Is.True);
            Assert.That(mBorrowedRoot.GetComponent<Timer>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator DuplicateComponentsCannotTakeOwnershipOfCurrentTasks()
        {
            var current = Timer.Instance;
            var item = current.Post2Really(null, 100);
            mBorrowedRoot = new GameObject("Timing.Duplicate");
            mBorrowedRoot.AddComponent<Timer>();
            yield return null;
            Assert.That(mBorrowedRoot != null, Is.True);
            Assert.That(mBorrowedRoot.GetComponent<Timer>(), Is.Null);
            Assert.That(Timer.Instance, Is.SameAs(current));
            Assert.That(item.IsRecycled, Is.False);
        }

        [UnityTest]
        public IEnumerator ShutdownInsideCallbackLeavesNewHostAndTasksAlive()
        {
            var old = Timer.Instance;
            var calls = 0;
            TimeItem replacement = null;
            old.Post2Really(_ =>
            {
                Timer.Shutdown();
                replacement = Timer.Instance.PostNamed("new", __ => calls++, 100);
            }, 0);
            var limit = Time.realtimeSinceStartup + 2;
            while (replacement == null && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(replacement, Is.Not.Null);
            var current = Timer.Instance;
            Assert.That(current, Is.Not.SameAs(old));
            yield return null;
            Assert.That(Timer.Contains("new"), Is.True);
            Assert.That(replacement.IsActive, Is.True);
            Assert.That(calls, Is.Zero);
        }
    }
}
