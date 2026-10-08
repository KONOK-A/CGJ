using YLFramework.Actions;
using System;
using System.Collections;
using System.Threading.Tasks;
using YLFramework.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kit = YLFramework.Actions.ActionKit;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Actions
{
    public class ActionKitLifecyclePlayModeTests
    {
        private Action<Kit> mPreviousPatch;
        private GameObject mUserRoot;
        private TaskCompletionSource<bool> mPending;
        private bool mOwnActionSession;

        [SetUp]
        public void SetUp()
        {
            mOwnActionSession = false;
            var canOwnActionSession = ActionFixtureSession.CanOwnUninitializedSession();
            mPreviousPatch = Kit.OnRegisterPatch;
            Assert.That(canOwnActionSession, Is.True,
                "This fixture must exclusively own an uninitialized ActionKit session; it will not shut down another owner's state.");
            mOwnActionSession = canOwnActionSession;
            Kit.Shutdown();
            Kit.OnRegisterPatch = _ => { };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!mOwnActionSession) yield break;
            try { Kit.Shutdown(); }
            finally
            {
                mOwnActionSession = false;
                mPending?.TrySetResult(true);
                mPending = null;
                Kit.OnRegisterPatch = mPreviousPatch;
                if (mUserRoot) Object.Destroy(mUserRoot);
            }
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BorrowedInactiveGlobalHostKeepsItsGameObjectAndDoesNotDuplicateUpdates()
        {
            mUserRoot = new GameObject("ActionKit.Lifecycle.BorrowedGlobal");
            mUserRoot.SetActive(false);
            var host = mUserRoot.AddComponent<ActionGlobalHost>();
            Assert.That(ActionGlobalHost.Instance, Is.SameAs(host));
            mUserRoot.SetActive(true);
            var calls = 0;
            Kit.OnUpdate.Register(() => calls++);
            yield return null;
            var before = calls;
            yield return null;
            Assert.That(calls, Is.EqualTo(before + 1));
            Kit.Shutdown();
            var next = ActionGlobalHost.Instance;
            Assert.That(next, Is.Not.SameAs(host));
            yield return null;
            Assert.That(mUserRoot != null, Is.True);
            Assert.That(host == null, Is.True);
            Assert.That(ActionGlobalHost.Instance, Is.SameAs(next));
        }

        [UnityTest]
        public IEnumerator GlobalHostAndRecycleDriverShareOnlyTheFrameworkHierarchy()
        {
            var host = ActionGlobalHost.Instance;
            ActionRecycleDriver.EnsureCreated();
            var driver = ActionRecycleDriver.Instance;
            Assert.That(host.transform.parent, Is.SameAs(driver.transform.parent));
            Assert.That(host.transform.parent.name, Is.EqualTo("Action"));
            Assert.That(host.transform.root.GetComponent<YLFramework.YLFrameworkRoot>(), Is.Not.Null);
            var finishes = 0;
            var startFrame = Time.frameCount;
            Kit.Condition(() => Time.frameCount > startFrame + 1).StartGlobal(() => finishes++);
            for (var frame = 0; frame < 10 && finishes == 0; frame++) yield return null;
            Assert.That(finishes, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ShutdownReleasesGlobalHostEventsAndAllowsImmediateRestart()
        {
            var oldEvent = Kit.OnUpdate;
            var oldCalls = 0;
            oldEvent.Register(() => oldCalls++);
            yield return null;
            Kit.Shutdown();
            var count = oldCalls;
            oldEvent.Trigger();
            Assert.That(oldCalls, Is.EqualTo(count));
            var nextEvent = Kit.OnUpdate;
            Assert.That(nextEvent, Is.Not.SameAs(oldEvent));
            var newCalls = 0;
            Action callback = () => newCalls++;
            nextEvent.Register(callback);
            yield return null;
            yield return null;
            Assert.That(Object.FindObjectsOfType<ActionGlobalHost>(true).Length, Is.EqualTo(1));
            Assert.That(newCalls, Is.GreaterThan(0));
            nextEvent.UnRegister(callback);
        }

        [UnityTest]
        public IEnumerator ShutdownDetachesPendingTaskWithoutCancellingTheExternalTask()
        {
            mPending = new TaskCompletionSource<bool>();
            var completed = 0;
            var action = Kit.Task(() => mPending.Task);
            var controller = action.StartGlobal(() => completed++);
            Kit.Shutdown();
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
            Assert.That(mPending.Task.IsCompleted, Is.False);
            mPending.SetResult(true);
            yield return null;
            yield return null;
            Assert.That(completed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CancelledTaskChildCancelsItsSequenceWithoutSuccessfulCompletion()
        {
            mPending = new TaskCompletionSource<bool>();
            var steps = 0;
            var finishes = 0;
            var parent = Kit.Sequence().Task(() => mPending.Task).Callback(() => steps++);
            var controller = parent.StartGlobal(() => finishes++);
            mPending.SetCanceled();
            for (var frame = 0; frame < 8 && !parent.Deinited; frame++) yield return null;
            Assert.That(parent.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
            Assert.That(steps, Is.Zero);
            Assert.That(finishes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CancelledTaskInsideConditionalSequenceCancelsTheOuterParent()
        {
            mPending = new TaskCompletionSource<bool>();
            var steps = 0;
            var finishes = 0;
            var parent = Kit.Sequence().ConditionSequence(() => true,
                inner => inner.Task(() => mPending.Task).Callback(() => steps++)).Callback(() => steps++);
            var controller = parent.StartGlobal(() => finishes++);
            yield return null;
            mPending.SetCanceled();
            for (var frame = 0; frame < 8 && !parent.Deinited; frame++) yield return null;
            Assert.That(parent.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
            Assert.That(steps, Is.Zero);
            Assert.That(finishes, Is.Zero);
        }

        private static IEnumerator Endless(Action step)
        {
            while (true)
            {
                step();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ShutdownStopsModuleCoroutinesButLeavesUserCoroutinesRunning()
        {
            mUserRoot = new GameObject("ActionKit.Lifecycle.CoroutineOwner");
            var caller = mUserRoot.AddComponent<ActionStartTestCaller>();
            var userSteps = 0;
            var moduleSteps = 0;
            caller.StartCoroutine(Endless(() => userSteps++));
            var action = Kit.Coroutine(() => Endless(() => moduleSteps++));
            var controller = action.Start(caller);
            yield return null;
            Assert.That(moduleSteps, Is.GreaterThan(0));
            Kit.Shutdown();
            var before = userSteps;
            var moduleBefore = moduleSteps;
            yield return null;
            yield return null;
            Assert.That(userSteps, Is.GreaterThan(before));
            Assert.That(moduleSteps, Is.EqualTo(moduleBefore));
            Assert.That(mUserRoot != null, Is.True);
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator ShutdownStopsOnlyTheModuleGlobalCoroutines()
        {
            var steps = 0;
            var action = Kit.Coroutine(() => Endless(() => steps++));
            action.StartGlobal();
            yield return null;
            Kit.Shutdown();
            var count = steps;
            yield return null;
            yield return null;
            Assert.That(steps, Is.EqualTo(count));
            Assert.That(action.Deinited, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingTheCoroutineHostCancelsAnActionScheduledOnALocalCaller()
        {
            mUserRoot = new GameObject("ActionKit.Lifecycle.LocalCoroutineOwner");
            var caller = mUserRoot.AddComponent<ActionStartTestCaller>();
            var action = Kit.Coroutine(() => Endless(() => { }));
            var completed = 0;
            var controller = action.Start(caller, () => completed++);
            Object.Destroy(ActionGlobalHost.Instance.gameObject);
            yield return null;
            yield return null;
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
            Assert.That(completed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SameFrameLocalRestartUsesANewExecutorOnTheSameUserObject()
        {
            mUserRoot = new GameObject("ActionKit.Lifecycle.SameFrameCaller");
            var caller = mUserRoot.AddComponent<ActionStartTestCaller>();
            var previous = Kit.Condition(() => false);
            previous.Start(caller);
            Kit.Shutdown();
            var completed = 0;
            var current = Kit.Condition(() => true);
            var controller = current.Start(caller, () => completed++);
            for (var frame = 0; frame < 5 && completed == 0; frame++) yield return null;
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(previous.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
            Assert.That(mUserRoot.GetComponents<MonoUpdateActionExecutor>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ShutdownDoesNotDestroyAUserSuppliedCanvasOrCallerRoot()
        {
            mUserRoot = new GameObject("ActionKit.Lifecycle.UserRoot");
            var canvas = mUserRoot.AddComponent<ScreenTransitionCanvas>();
            Assert.That(ScreenTransitionCanvas.Instance, Is.SameAs(canvas));
            var caller = mUserRoot.AddComponent<ActionStartTestCaller>();
            var action = Kit.Condition(() => false);
            action.Start(caller);
            Kit.Shutdown();
            yield return null;
            Assert.That(mUserRoot != null, Is.True);
            Assert.That(canvas != null, Is.True);
            Assert.That(action.Deinited, Is.True);
            Assert.That(mUserRoot.GetComponent<MonoUpdateActionExecutor>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator SameFrameRestartDoesNotLoseTheNewSceneOrRecycleHost()
        {
            var previous = ActionKitCurrentScene.SceneComponent;
            var action = Kit.Condition(() => false);
            action.StartCurrentScene();
            Kit.Shutdown();
            var next = ActionKitCurrentScene.SceneComponent;
            Assert.That(next, Is.Not.SameAs(previous));
            var other = Kit.Condition(() => false);
            other.StartCurrentScene();
            yield return null;
            yield return null;
            Assert.That(ActionKitCurrentScene.mSceneComponent, Is.SameAs(next));
            Assert.That(next != null, Is.True);
        }

        [UnityTest]
        public IEnumerator ShutdownClearsModuleEventsButLeavesUserLifecycleTriggersAlone()
        {
            mUserRoot = new GameObject("ActionKit.Lifecycle.SubscriptionOwner");
            var trigger = mUserRoot.AddComponent<OnDestroyUnRegisterTrigger>();
            var update = Kit.OnUpdate;
            var calls = 0;
            var token = update.Register(() => calls++);
            trigger.AddUnRegister(token);
            yield return null;
            var callsBeforeShutdown = calls;
            Assert.That(callsBeforeShutdown, Is.GreaterThan(0));
            Kit.Shutdown();
            Assert.That(mUserRoot != null, Is.True);
            Assert.That(mUserRoot.GetComponent<OnDestroyUnRegisterTrigger>() != null, Is.True);
            Assert.DoesNotThrow(() => token.UnRegister());
            yield return null;
            Assert.That(calls, Is.EqualTo(callsBeforeShutdown));
        }
    }
}
