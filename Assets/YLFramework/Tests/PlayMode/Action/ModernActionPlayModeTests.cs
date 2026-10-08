using YLFramework.Actions;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Kit = YLFramework.Actions.ActionKit;

namespace YLFramework.Tests.Actions
{
    public class ModernActionPlayModeTests
    {
        private readonly List<IAction> mOwned = new List<IAction>();
        private ActionRecycleDriver mPreviousDriver;
        private float mPreviousTimeScale;
        private Scene mCreatedScene;
        private AsyncOperation mSceneUnload;

        private T Own<T>(T action) where T : IAction
        {
            mOwned.Add(action);
            return action;
        }

        [SetUp]
        public void SetUp()
        {
            mPreviousDriver = Object.FindObjectOfType<ActionRecycleDriver>(true);
            mPreviousTimeScale = Time.timeScale;
            mCreatedScene = default;
            mSceneUnload = null;
            ActionQueue.Drain();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = mPreviousTimeScale;
            foreach (var action in mOwned)
            {
                if (!action.Deinited) action.Deinit();
            }
            mOwned.Clear();
            ActionQueue.Drain();
            var current = Object.FindObjectOfType<ActionRecycleDriver>(true);
            if (current && current != mPreviousDriver) Object.Destroy(current.gameObject);
            if (mSceneUnload != null)
            {
                yield return mSceneUnload;
            }
            else if (mCreatedScene.IsValid() && mCreatedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(mCreatedScene);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrameDelayUsesActualFramesInsteadOfSuppliedDelta()
        {
            var calls = 0;
            var action = Own(Kit.DelayFrame(2, () => calls++));
            var frame = Time.frameCount;
            Assert.That(action.Execute(1000), Is.False);
            Assert.That(action.Execute(1000), Is.False);
            yield return null;
            Assert.That(Time.frameCount, Is.EqualTo(frame + 1));
            Assert.That(action.Execute(1000), Is.False);
            yield return null;
            Assert.That(action.Execute(0), Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NextFrameFactoryAndBothFrameFluentMethodsRemainAvailable()
        {
            var calls = 0;
            var single = Own(Kit.NextFrame(() => calls++));
            Assert.That(single.Execute(0), Is.False);
            var sequence = Own(Kit.Sequence().DelayFrame(0).NextFrame().Callback(() => calls++));
            Assert.That(sequence.Execute(0), Is.False);
            Assert.That(sequence.Execute(0), Is.False);
            yield return null;
            Assert.That(single.Execute(0), Is.True);
            Assert.That(sequence.Execute(0), Is.True);
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PausedFrameDelayObservesElapsedGlobalFramesAfterResume()
        {
            Time.timeScale = 0;
            var calls = 0;
            var action = Own(Kit.NextFrame(() => calls++));
            action.Execute(0);
            action.Paused = true;
            yield return null;
            yield return null;
            Assert.That(action.Execute(0), Is.False);
            Assert.That(calls, Is.Zero);
            action.Paused = false;
            Assert.That(action.Execute(0), Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ManualSecondsAndCompositionUseCallerDeltaEvenWhenTimeScaleIsZero()
        {
            Time.timeScale = 0;
            var calls = 0;
            var sequence = Own(Kit.Sequence().Callback(() => calls++).Delay(1f).Callback(() => calls++));
            Assert.That(sequence.Execute(10), Is.False);
            yield return null;
            Assert.That(sequence.Execute(0.5f), Is.False);
            yield return null;
            Assert.That(sequence.Execute(0.5f), Is.False);
            yield return null;
            Assert.That(sequence.Execute(0), Is.True);
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RuntimeRecycleIsDeferredAndReusesTheActionAfterUpdate()
        {
            var first = Own(Kit.Condition(() => false));
            var id = first.ActionID;
            first.Deinit();
            Assert.That(ActionQueue.PendingCount, Is.GreaterThan(0));
            var second = Own(Kit.Condition(() => false));
            Assert.That(second, Is.Not.SameAs(first));
            yield return null;
            yield return null;
            Assert.That(ActionQueue.PendingCount, Is.Zero);
            var reused = Own(Kit.Condition(() => false));
            Assert.That(reused, Is.SameAs(first));
            Assert.That(reused.ActionID, Is.Not.EqualTo(id));
            Assert.That(reused.Deinited, Is.False);
        }

        [UnityTest]
        public IEnumerator RecycleDriverIsAdoptedAfterStaticResetAndRecreatedAfterDestruction()
        {
            Assert.That(mPreviousDriver == null, Is.True, "Run this lifecycle test without an existing module driver; it will not destroy another owner's host.");
            ActionRecycleDriver.EnsureCreated();
            var original = Object.FindObjectOfType<ActionRecycleDriver>(true);
            Assert.That(original, Is.Not.Null);
            ActionRecycleDriver.ResetSession();
            ActionRecycleDriver.EnsureCreated();
            Assert.That(Object.FindObjectOfType<ActionRecycleDriver>(true), Is.SameAs(original));
            Assert.That(Object.FindObjectsOfType<ActionRecycleDriver>(true).Length, Is.EqualTo(1));
            Object.Destroy(original.gameObject);
            yield return null;
            Assert.That(original == null, Is.True);
            var action = Own(Kit.Callback(null));
            action.Deinit();
            var recreated = Object.FindObjectOfType<ActionRecycleDriver>(true);
            Assert.That(recreated, Is.Not.Null);
            yield return null;
            yield return null;
            Assert.That(ActionQueue.PendingCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RecycleDriverSurvivesTheSceneWhereItWasCreated()
        {
            Assert.That(mPreviousDriver == null, Is.True, "This test must create and own its driver.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = SceneManager.CreateScene("ActionKit.Recycle.SceneTest");
            mCreatedScene = scene;
            try
            {
                SceneManager.SetActiveScene(scene);
                ActionRecycleDriver.EnsureCreated();
                var driver = Object.FindObjectOfType<ActionRecycleDriver>(true);
                Assert.That(driver, Is.Not.Null);
                Assert.That(driver.gameObject.scene, Is.Not.EqualTo(scene));
                SceneManager.SetActiveScene(previousScene);
                mSceneUnload = SceneManager.UnloadSceneAsync(scene);
                yield return mSceneUnload;
                Assert.That(driver != null, Is.True);
                var action = Own(Kit.Callback(null));
                action.Deinit();
                yield return null;
                yield return null;
                Assert.That(ActionQueue.PendingCount, Is.Zero);
            }
            finally
            {
                SceneManager.SetActiveScene(previousScene);
                if (mSceneUnload == null && scene.IsValid() && scene.isLoaded)
                {
                    mSceneUnload = SceneManager.UnloadSceneAsync(scene);
                }
            }
        }
    }
}
