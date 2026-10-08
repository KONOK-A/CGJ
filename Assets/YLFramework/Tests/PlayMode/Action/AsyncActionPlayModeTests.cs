using YLFramework.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kit = YLFramework.Actions.ActionKit;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Actions
{
    public class AsyncActionPlayModeTests
    {
        private readonly List<IAction> mActions = new List<IAction>();
        private readonly List<IActionController> mControllers = new List<IActionController>();
        private readonly List<TaskCompletionSource<bool>> mTasks = new List<TaskCompletionSource<bool>>();
        private readonly List<Action> mReleaseGates = new List<Action>();
        private readonly List<GameObject> mRoots = new List<GameObject>();
        private readonly List<AudioClip> mClips = new List<AudioClip>();
        private ActionRecycleDriver mPreviousRecycle;
        private bool mOwnEnvironment;
        private bool mOwnActionSession;

        [SetUp]
        public void SetUp()
        {
            mOwnEnvironment = false;
            mOwnActionSession = false;
            var canOwnActionSession = ActionFixtureSession.CanOwnUninitializedSession();
            mPreviousRecycle = Object.FindObjectOfType<ActionRecycleDriver>(true);
            Assert.That(Object.FindObjectOfType<ActionGlobalHost>(true) == null, Is.True,
                "This fixture must own its global coroutine host.");
            mOwnEnvironment = true;
            mOwnActionSession = canOwnActionSession;
            ActionQueue.Drain();
        }

        private T Own<T>(T action) where T : IAction
        {
            mActions.Add(action);
            return action;
        }

        private IActionController Track(IActionController controller)
        {
            mControllers.Add(controller);
            return controller;
        }

        private TaskCompletionSource<bool> PendingTask()
        {
            var task = new TaskCompletionSource<bool>();
            mTasks.Add(task);
            return task;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!mOwnEnvironment) yield break;
            foreach (var action in mActions)
            {
                if (action is ICustomAPI<AudioSource> audio) audio.Data = null;
                if (!action.Deinited) action.Deinit();
            }
            foreach (var release in mReleaseGates) release();
            foreach (var task in mTasks) task.TrySetResult(true);
            // Release owned work after detaching actions, then let awaiting observers settle.
            for (var frame = 0; frame < 6; frame++) yield return null;
            var host = Object.FindObjectOfType<ActionGlobalHost>(true);
            if (host) Object.Destroy(host.gameObject);
            foreach (var root in mRoots)
            {
                if (root) Object.Destroy(root);
            }
            yield return null;
            foreach (var controller in mControllers)
            {
                if (controller.Action != null) controller.Recycle();
            }
            ActionQueue.Drain();
            var recycle = Object.FindObjectOfType<ActionRecycleDriver>(true);
            if (recycle && recycle != mPreviousRecycle) Object.Destroy(recycle.gameObject);
            foreach (var clip in mClips)
            {
                if (clip) Object.Destroy(clip);
            }
            mActions.Clear();
            mControllers.Clear();
            mTasks.Clear();
            mReleaseGates.Clear();
            mRoots.Clear();
            mClips.Clear();
            if (mOwnActionSession)
            {
                try { Kit.Shutdown(); }
                finally { mOwnActionSession = false; }
            }
            yield return null;
        }

        private static IEnumerator OneFrame(Action onFinish)
        {
            yield return null;
            onFinish();
        }

        private static IEnumerator Gate(Func<bool> ready, Action onFinish)
        {
            while (!ready()) yield return null;
            onFinish();
        }

        [UnityTest]
        public IEnumerator CoroutineFactoryIsReevaluatedAfterReset()
        {
            var getters = 0;
            var completed = 0;
            var action = Own(Kit.Coroutine(() =>
            {
                getters++;
                return OneFrame(() => completed++);
            }));
            Assert.That(action.Execute(0), Is.False);
            for (var frame = 0; frame < 12 && action.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(action.Execute(0), Is.True);
            Assert.That(completed, Is.EqualTo(1));
            action.Reset();
            Assert.That(action.Execute(0), Is.False);
            for (var frame = 0; frame < 12 && action.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(action.Execute(0), Is.True);
            Assert.That(getters, Is.EqualTo(2));
            Assert.That(completed, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator EnumeratorToActionUsesTheOriginalEnumeratorRatherThanRecreatingIt()
        {
            var completed = 0;
            var action = Own(OneFrame(() => completed++).ToAction());
            action.Execute(0);
            for (var frame = 0; frame < 12 && action.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(action.Execute(0), Is.True);
            action.Reset();
            action.Execute(0);
            for (var frame = 0; frame < 12 && action.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(action.Execute(0), Is.True);
            Assert.That(completed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SequenceWaitsForCoroutineThenTaskBeforeContinuing()
        {
            var trace = new List<string>();
            var task = PendingTask();
            var taskStarted = false;
            var sequence = Own(Kit.Sequence()
                .Coroutine(() => OneFrame(() => trace.Add("coroutine")))
                .Task(() => { taskStarted = true; return task.Task; })
                .Callback(() => trace.Add("callback")));
            var controller = Track(sequence.StartGlobal(() => trace.Add("finish")));
            for (var frame = 0; frame < 12 && !taskStarted; frame++) yield return null;
            Assert.That(taskStarted, Is.True);
            Assert.That(task.Task.IsCompleted, Is.False);
            CollectionAssert.AreEqual(new[] { "coroutine" }, trace);
            task.SetResult(true);
            for (var frame = 0; frame < 12 && trace.Count < 3; frame++) yield return null;
            CollectionAssert.AreEqual(new[] { "coroutine", "callback", "finish" }, trace);
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator CoroutinePauseDoesNotPauseUnderlyingWorkOrBlockTheFinishedState()
        {
            var released = false;
            var completed = 0;
            mReleaseGates.Add(() => released = true);
            var action = Own(Kit.Coroutine(() => Gate(() => released, () => completed++)));
            action.Execute(0);
            action.Paused = true;
            released = true;
            for (var frame = 0; frame < 12 && action.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(action.Paused, Is.True);
            Assert.That(action.Execute(0), Is.True, "AK-907: paused does not stop the underlying coroutine.");
        }

        [UnityTest]
        public IEnumerator CoroutineDeinitStopsOnlyItsOwnRunAndPreventsLateCompletion()
        {
            var released = false;
            var completed = 0;
            var otherCompleted = 0;
            mReleaseGates.Add(() => released = true);
            var action = Own(Kit.Coroutine(() => Gate(() => released, () => completed++)));
            var other = Own(Kit.Coroutine(() => Gate(() => released, () => otherCompleted++)));
            action.Execute(0);
            other.Execute(0);
            Assert.DoesNotThrow(action.Deinit);
            released = true;
            for (var frame = 0; frame < 12 && other.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(action.Deinited, Is.True);
            Assert.That(completed, Is.Zero);
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Started));
            Assert.That(otherCompleted, Is.EqualTo(1));
            Assert.That(other.Execute(0), Is.True);
        }

        [UnityTest]
        public IEnumerator PendingTaskCompletesAndRemovesItsGlobalController()
        {
            var task = PendingTask();
            var calls = 0;
            var action = Own(Kit.Task(() => task.Task));
            var controller = Track(action.StartGlobal(() => calls++));
            yield return null;
            Assert.That(calls, Is.Zero);
            task.SetResult(true);
            for (var frame = 0; frame < 12 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator PausingTheTaskControllerDoesNotPreventUnderlyingTaskCompletion()
        {
            var task = PendingTask();
            var calls = 0;
            var action = Own(Kit.Task(() => task.Task));
            var controller = Track(action.StartGlobal(() => calls++));
            controller.Pause();
            task.SetResult(true);
            for (var frame = 0; frame < 12 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1), "AK-908: awaiting a Task is not paused by the controller.");
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator DeinitOfPendingTaskLeavesExternalWorkRunningAndPreventsLateFinish()
        {
            var task = PendingTask();
            var action = Own(TaskAction.Allocate(() => task.Task));
            action.Execute(0);
            Assert.DoesNotThrow(action.Deinit);
            Assert.That(action.Deinited, Is.True);
            Assert.That(task.Task.IsCompleted, Is.False);
            Assert.That(task.Task.IsCanceled, Is.False);
            task.SetResult(true);
            for (var frame = 0; frame < 6; frame++) yield return null;
            Assert.That(task.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Started));
        }

        [UnityTest]
        public IEnumerator CoroutineResetStopsTheOldRunBeforeStartingANewRun()
        {
            var oldReleased = false;
            var newReleased = false;
            var oldCompleted = 0;
            var newCompleted = 0;
            var getters = 0;
            mReleaseGates.Add(() => { oldReleased = true; newReleased = true; });
            var action = Own(Kit.Coroutine(() => ++getters == 1
                ? Gate(() => oldReleased, () => oldCompleted++)
                : Gate(() => newReleased, () => newCompleted++)));
            Assert.That(action.Execute(0), Is.False);
            action.Paused = true;
            action.Reset();
            Assert.That(action.Deinited, Is.False);
            Assert.That(action.Status, Is.EqualTo(ActionStatus.NotStart));
            Assert.That(action.Paused, Is.False);
            Assert.That(action.Execute(0), Is.False);
            oldReleased = true;
            for (var frame = 0; frame < 6; frame++) yield return null;
            Assert.That(oldCompleted, Is.Zero);
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Started));
            newReleased = true;
            for (var frame = 0; frame < 12 && action.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(action.Execute(0), Is.True);
            Assert.That(getters, Is.EqualTo(2));
            Assert.That(newCompleted, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CancelledCoroutineCannotFinishAReusedAction()
        {
            var oldReleased = false;
            var newReleased = false;
            var oldCompleted = 0;
            var newCompleted = 0;
            mReleaseGates.Add(() => { oldReleased = true; newReleased = true; });
            var old = Own(Kit.Coroutine(() => Gate(() => oldReleased, () => oldCompleted++)));
            old.Execute(0);
            var oldID = old.ActionID;
            old.Deinit();
            ActionQueue.Drain();
            var reused = Own(Kit.Coroutine(() => Gate(() => newReleased, () => newCompleted++)));
            Assert.That(reused, Is.SameAs(old));
            Assert.That(reused.ActionID, Is.Not.EqualTo(oldID));
            Assert.That(reused.Execute(0), Is.False);
            oldReleased = true;
            for (var frame = 0; frame < 6; frame++) yield return null;
            Assert.That(oldCompleted, Is.Zero);
            Assert.That(reused.Deinited, Is.False);
            Assert.That(reused.Status, Is.EqualTo(ActionStatus.Started));
            newReleased = true;
            for (var frame = 0; frame < 12 && reused.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(reused.Execute(0), Is.True);
            Assert.That(newCompleted, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DetachedTaskResultCannotFinishAReusedAction()
        {
            var oldTask = PendingTask();
            var newTask = PendingTask();
            var old = Own(Kit.Task(() => oldTask.Task));
            Assert.That(old.Execute(0), Is.False);
            var oldID = old.ActionID;
            old.Deinit();
            ActionQueue.Drain();
            var reused = Own(Kit.Task(() => newTask.Task));
            Assert.That(reused, Is.SameAs(old));
            Assert.That(reused.ActionID, Is.Not.EqualTo(oldID));
            Assert.That(reused.Execute(0), Is.False);
            oldTask.SetResult(true);
            for (var frame = 0; frame < 6; frame++) yield return null;
            Assert.That(reused.Deinited, Is.False);
            Assert.That(reused.Status, Is.EqualTo(ActionStatus.Started));
            Assert.That(newTask.Task.IsCompleted, Is.False);
            newTask.SetResult(true);
            for (var frame = 0; frame < 12 && reused.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(reused.Execute(0), Is.True);
        }

        [UnityTest]
        public IEnumerator TaskResetDetachesTheOldFaultAndKeepsTheNewRunActive()
        {
            var oldTask = PendingTask();
            var newTask = PendingTask();
            var getters = 0;
            var action = Own(Kit.Task(() => ++getters == 1 ? oldTask.Task : newTask.Task));
            Assert.That(action.Execute(0), Is.False);
            action.Paused = true;
            action.Reset();
            Assert.That(action.Deinited, Is.False);
            Assert.That(action.Status, Is.EqualTo(ActionStatus.NotStart));
            Assert.That(action.Paused, Is.False);
            Assert.That(action.Execute(0), Is.False);
            oldTask.SetException(new InvalidOperationException("task-detached-fault"));
            for (var frame = 0; frame < 6; frame++) yield return null;
            Assert.That(action.Deinited, Is.False);
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Started));
            Assert.That(newTask.Task.IsCompleted, Is.False);
            newTask.SetResult(true);
            for (var frame = 0; frame < 12 && action.Status != ActionStatus.Finished; frame++) yield return null;
            Assert.That(action.Execute(0), Is.True);
            Assert.That(getters, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator FaultedTaskReportsAndRemovesItsControllerWithoutSuccessfulFinish()
        {
            var task = PendingTask();
            var calls = 0;
            var action = Own(Kit.Task(() => task.Task));
            var controller = Track(action.StartGlobal(() => calls++));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: task-controller-fault"));
            task.SetException(new InvalidOperationException("task-controller-fault"));
            for (var frame = 0; frame < 12 && controller.Action != null; frame++) yield return null;
            Assert.That(calls, Is.Zero);
            Assert.That(action.Deinited, Is.True);
            Assert.That(action.Status, Is.Not.EqualTo(ActionStatus.Finished));
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator CancelledTaskRemovesItsControllerWithoutSuccessfulFinish()
        {
            var task = PendingTask();
            var calls = 0;
            var action = Own(Kit.Task(() => task.Task));
            var controller = Track(action.StartGlobal(() => calls++));
            task.SetCanceled();
            for (var frame = 0; frame < 12 && controller.Action != null; frame++) yield return null;
            Assert.That(task.Task.IsCanceled, Is.True);
            Assert.That(calls, Is.Zero);
            Assert.That(action.Deinited, Is.True);
            Assert.That(action.Status, Is.Not.EqualTo(ActionStatus.Finished));
            Assert.That(controller.Action, Is.Null);
        }

        private AudioSource Source()
        {
            var root = new GameObject("ActionKit.AudioSource.ShortcutTest");
            mRoots.Add(root);
            if (!Object.FindObjectOfType<AudioListener>()) root.AddComponent<AudioListener>();
            var source = root.AddComponent<AudioSource>();
            var clip = AudioClip.Create("ActionKit.Shortcut.Silence", 44100, 1, 44100, false);
            mClips.Add(clip);
            source.clip = clip;
            source.loop = true;
            return source;
        }

        [UnityTest]
        public IEnumerator AudioSourceShortcutFinishesWhenTheSourceIsStopped()
        {
            var source = Source();
            var action = Own(source.PlayAction());
            Assert.That(action.Execute(0), Is.False);
            source.Stop();
            Assert.That(action.Execute(0), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AudioSourceShortcutKeepsItsOriginalDestroyedSourceWait()
        {
            var source = Source();
            var action = Own(source.PlayAction());
            action.Execute(0);
            Object.Destroy(source.gameObject);
            yield return null;
            Assert.That(action.Execute(0), Is.False, "AK-914: a destroyed AudioSource does not satisfy the original predicate.");
        }

        [UnityTest]
        public IEnumerator AudioSourceShortcutTreatsPauseAsNotPlayingAndDoesNotStopOnDeinit()
        {
            var source = Source();
            var paused = Own(source.PlayAction());
            paused.Execute(0);
            source.Pause();
            Assert.That(paused.Execute(0), Is.True);
            source.UnPause();
            var cancelled = Own(source.PlayAction());
            cancelled.Execute(0);
            cancelled.Deinit();
            Assert.That(source.isPlaying, Is.True, "The original shortcut does not stop AudioSource on Deinit.");
            source.Stop();
            yield return null;
        }
    }
}
