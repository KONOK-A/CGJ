using YLFramework.Actions;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Kit = YLFramework.Actions.ActionKit;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Actions
{
    public class ActionExecutionTests
    {
        private readonly List<IAction> mActions = new List<IAction>();
        private readonly List<IActionController> mControllers = new List<IActionController>();
        private GameObject mRoot;
        private MonoUpdateActionExecutor mExecutor;

        [SetUp]
        public void SetUp()
        {
            ActionQueue.Drain();
            mRoot = new GameObject("ActionKit.Execution.EditModeTest");
            mExecutor = mRoot.AddComponent<MonoUpdateActionExecutor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (mRoot) Object.DestroyImmediate(mRoot);
            foreach (var action in mActions)
            {
                if (!action.Deinited) action.Deinit();
            }
            foreach (var controller in mControllers)
            {
                if (controller.Action != null) controller.Recycle();
            }
            mActions.Clear();
            mControllers.Clear();
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

        public sealed class DeltaProbe : AbstractAction<DeltaProbe>
        {
            public float Delta;
            public int Starts;
            public int Executions;
            public bool Complete;

            public override void OnStart() => Starts++;
            public override void OnExecute(float dt)
            {
                Executions++;
                Delta = dt;
                if (Complete) this.Finish();
            }

            protected override void OnReset()
            {
                Starts = Executions = 0;
                Delta = 0;
                Complete = false;
            }
        }

        [Test]
        public void ControllerMembersForwardToTheirActionAndRetainIdGuards()
        {
            var action = Own(DeltaProbe.Allocate());
            var controller = Track(ActionController.Allocate());
            controller.Action = action;
            controller.ActionID = action.ActionID;
            controller.Paused = true;
            Assert.That(action.Paused, Is.True);
            controller.Resume();
            Assert.That(controller.Paused, Is.False);
            controller.Pause();
            Assert.That(action.Paused, Is.True);
            action.Execute(0);
            controller.Reset();
            Assert.That(action.Status, Is.EqualTo(ActionStatus.NotStart));
            Assert.That(action.Paused, Is.False);
            Assert.That(controller.IgnoreTimeScale(), Is.SameAs(controller));
            Assert.That(controller.UpdateMode, Is.EqualTo(ActionUpdateModes.UnscaledDeltaTime));
            controller.Deinit();
            Assert.That(action.Deinited, Is.True);
        }

        [Test]
        public void StaleIdsBlockAllForwardedControllerOperations()
        {
            var action = Own(DeltaProbe.Allocate());
            var controller = Track(ActionController.Allocate());
            controller.Action = action;
            controller.ActionID = action.ActionID + 1;
            action.Execute(0);
            controller.Pause();
            controller.Reset();
            controller.Deinit();
            Assert.That(action.Paused, Is.False);
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Started));
            Assert.That(action.Deinited, Is.False);
            action.Paused = true;
            controller.Resume();
            Assert.That(action.Paused, Is.True);
            controller.Paused = false;
            Assert.That(action.Paused, Is.True);
        }

        [Test]
        public void ControllerRecycleClearsTheActionIdAndRestoresScaledMode()
        {
            var action = Own(Kit.Condition(() => false));
            var first = Track(ActionController.Allocate());
            first.Action = action;
            first.ActionID = action.ActionID;
            first.IgnoreTimeScale();
            first.Recycle();
            Assert.That(first.Action, Is.Null);
            Assert.That(first.ActionID, Is.Zero);
            Assert.That(first.UpdateMode, Is.EqualTo(ActionUpdateModes.ScaledDeltaTime));
            var reused = Track(ActionController.Allocate());
            Assert.That(reused, Is.SameAs(first));
            // The null-action allocation is not scheduled, so this test owns its recycling.
            reused.Recycle();
        }

        [Test]
        public void EmptyControllerOperationsAreSafeAndRecyclingIsIdempotent()
        {
            var controller = Track(ActionController.Allocate());
            Assert.DoesNotThrow(() => controller.Deinit());
            Assert.DoesNotThrow(() => controller.Reset());
            Assert.DoesNotThrow(() => controller.Pause());
            Assert.DoesNotThrow(() => controller.Resume());
            Assert.That(controller.Paused, Is.False);
            controller.Recycle();
            Assert.DoesNotThrow(() => controller.Recycle());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LocalStartOverloadsExecuteSynchronouslyBeforeReturning(bool typedCallback)
        {
            var calls = 0;
            var callbackObservedBeforeDeinit = false;
            var action = Own(Kit.Callback(() => calls++));
            IActionController callbackController = null;
            var controller = typedCallback
                ? Track(action.Start(mExecutor, value =>
                {
                    callbackController = value;
                    callbackObservedBeforeDeinit = !value.Action.Deinited;
                    calls++;
                }))
                : Track(action.Start(mExecutor, () => calls++));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null, "Synchronous completion must release the controller too.");
            if (typedCallback)
            {
                Assert.That(callbackController, Is.SameAs(controller));
                Assert.That(callbackObservedBeforeDeinit, Is.True);
            }
        }

        [TestCase(ActionUpdateModes.ScaledDeltaTime, 0.25f)]
        [TestCase(ActionUpdateModes.UnscaledDeltaTime, 0.75f)]
        public void ExecutorSelectsTheConfiguredDeltaAndRecyclesAfterCompletion(ActionUpdateModes mode, float expected)
        {
            var action = Own(DeltaProbe.Allocate());
            var calls = 0;
            var controller = Track(action.Start(mExecutor, value =>
            {
                Assert.That(value.Action, Is.SameAs(action));
                Assert.That(value.Action.Deinited, Is.False);
                calls++;
            }));
            controller.UpdateMode = mode;
            Assert.That(action.Starts, Is.EqualTo(1));
            Assert.That(action.Executions, Is.Zero);
            action.Complete = true;
            mExecutor.Tick(0.25f, 0.75f);
            Assert.That(action.Delta, Is.EqualTo(expected));
            Assert.That(action.Deinited, Is.True);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(controller.Action, Is.Null);
            mExecutor.Tick(1, 1);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void CancellationSkipsFinishNotificationAndIsRemovedOnTheNextTick()
        {
            var action = Own(Kit.Condition(() => false));
            var calls = 0;
            var controller = Track(action.Start(mExecutor, () => calls++));
            controller.Deinit();
            mExecutor.Tick(1, 1);
            Assert.That(calls, Is.Zero);
            Assert.That(controller.Action, Is.Null);
        }

        [Test]
        public void RestartingFinishedActionResetsBeforeTheSynchronousStart()
        {
            var starts = 0;
            var action = Own(Kit.Callback(() => starts++));
            action.Finish();
            Track(action.Start(mExecutor));
            Assert.That(starts, Is.EqualTo(1));
        }

        [Test]
        public void ExecuteByUpdateReturnsTheActionAndUsesTheExistingComponent()
        {
            var action = Own(Kit.Condition(() => true));
            var controller = Track(ActionController.Allocate());
            controller.Action = action;
            controller.ActionID = action.ActionID;
            var calls = 0;
            Assert.That(mExecutor.ExecuteByUpdate(action, controller, _ => calls++), Is.SameAs(action));
            Assert.That(mRoot.GetComponents<MonoUpdateActionExecutor>().Length, Is.EqualTo(1));
            mExecutor.Tick(0, 0);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantStartIsPreparedForTheNextTickNotTheCurrentDictionaryIteration()
        {
            var ready = false;
            var first = Own(Kit.Condition(() => ready));
            var second = Own(DeltaProbe.Allocate());
            Track(first.Start(mExecutor, () => Track(second.Start(mExecutor))));
            ready = true;
            mExecutor.Tick(1, 1);
            Assert.That(second.Starts, Is.EqualTo(1));
            Assert.That(second.Executions, Is.Zero);
            mExecutor.Tick(2, 2);
            Assert.That(second.Executions, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateStartIsRejectedWithoutLosingTheFirstController()
        {
            var ready = false;
            var firstCalls = 0;
            var secondCalls = 0;
            var action = Own(Kit.Condition(() => ready));
            var first = Track(action.Start(mExecutor, () => firstCalls++));
            Assert.Throws<InvalidOperationException>(() => action.Start(mExecutor, () => secondCalls++));
            ready = true;
            mExecutor.Tick(1, 1);
            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.Zero);
            Assert.That(first.Action, Is.Null);
            Assert.That(mExecutor.ScheduledCount, Is.Zero);
        }

        [Test]
        public void DestroyingTheExecutorCancelsItsActionsAndReleasesControllers()
        {
            var action = Own(Kit.Condition(() => false));
            var controller = Track(action.Start(mExecutor));
            Object.DestroyImmediate(mExecutor);
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        [Test]
        public void EditorLifecycleCallbacksDoNotAutomaticallyExecuteScheduledActions()
        {
            var polls = 0;
            var action = Own(Kit.Condition(() => { polls++; return true; }));
            var controller = Track(action.Start(mExecutor));
            mExecutor.SendMessage("Update");
            Assert.That(polls, Is.Zero);
            Assert.That(action.Deinited, Is.False);
            Assert.That(controller.Action, Is.SameAs(action));
            mExecutor.Tick(0, 0);
            Assert.That(polls, Is.EqualTo(1));
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        [Test]
        public void SameActionCannotBeScheduledOnTwoExecutors()
        {
            var other = mRoot.AddComponent<MonoUpdateActionExecutor>();
            var action = Own(Kit.Condition(() => false));
            var controller = Track(action.Start(mExecutor));
            var second = Track(ActionController.Allocate());
            second.Action = action;
            second.ActionID = action.ActionID;
            Assert.Throws<InvalidOperationException>(() => other.Execute(second));
            Assert.That(controller.Action, Is.SameAs(action));
        }

        [Test]
        public void RestartInsideCompletionIsRejectedWithoutResettingTheFinishingAction()
        {
            var ready = false;
            var action = Own(Kit.Condition(() => ready));
            var controller = Track(action.Start(mExecutor, () =>
            {
                Assert.That(action.Status, Is.EqualTo(ActionStatus.Finished));
                Assert.Throws<InvalidOperationException>(() => action.Start(mExecutor));
                Assert.That(action.Status, Is.EqualTo(ActionStatus.Finished));
            }));
            ready = true;
            mExecutor.Tick(0, 0);
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        public sealed class FaultProbe : AbstractAction<FaultProbe>
        {
            internal Exception StartError;
            internal Exception CleanupError;
            public override void OnStart() => throw StartError;
            protected override void OnDeinit() => throw CleanupError;
        }

        [Test]
        public void OriginalExecutionExceptionIsPreservedWhenCleanupAlsoFails()
        {
            var original = new InvalidOperationException("execution");
            var cleanup = new InvalidOperationException("cleanup");
            var action = Own(FaultProbe.Allocate());
            action.StartError = original;
            action.CleanupError = cleanup;
            var error = Assert.Throws<InvalidOperationException>(() => action.Start(mExecutor));
            Assert.That(error, Is.SameAs(original));
            Assert.That(error.Data["ActionKit.CleanupException"], Is.InstanceOf<AggregateException>());
            Assert.That(action.Deinited, Is.True);
            Assert.That(mExecutor.ScheduledCount, Is.Zero);
        }

        [Test]
        public void AFailingCallbackCannotRecycleANewControllerLease()
        {
            var original = new InvalidOperationException("callback");
            var next = Own(Kit.Condition(() => false));
            IActionController replacement = null;
            var first = Own(Kit.Callback(null));
            var error = Assert.Throws<InvalidOperationException>(() => first.Start(mExecutor, controller =>
            {
                controller.Recycle();
                replacement = Track(next.Start(mExecutor));
                throw original;
            }));
            Assert.That(error, Is.SameAs(original));
            Assert.That(replacement.Action, Is.SameAs(next));
            Assert.That(next.Deinited, Is.False);
            mExecutor.Tick(0, 0);
            Assert.That(replacement.Action, Is.SameAs(next));
        }

        public sealed class ResetProbe : AbstractAction<ResetProbe>
        {
            internal Action ResetCallback;
            protected override void OnReset() => ResetCallback?.Invoke();
            protected override void OnDeinit() => ResetCallback = null;
        }

        [Test]
        public void ShutdownDuringFinishedActionResetIsInsideTheRegisteredExecutionScope()
        {
            var action = Own(ResetProbe.Allocate());
            action.Finish();
            action.ResetCallback = Kit.Shutdown;
            IActionController controller = null;
            Assert.DoesNotThrow(() => controller = action.Start(mExecutor));
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
            Assert.That(Kit.Initialized, Is.False);
        }

        [Test]
        public void NestedStartDuringResetIsRejectedBeforeItCanReplaceTheOuterRegistration()
        {
            var action = Own(ResetProbe.Allocate());
            action.Finish();
            action.ResetCallback = () => Assert.Throws<InvalidOperationException>(() => action.Start(mExecutor));
            var controller = Track(action.Start(mExecutor));
            Assert.That(controller.Action, Is.SameAs(action));
            Assert.That(mExecutor.ScheduledCount, Is.EqualTo(1));
        }

        [Test]
        public void ACancelledPooledActionCanStartANewLeaseBeforeTheOldTaskIsRemoved()
        {
            var first = Own(Kit.Condition(() => false));
            var oldController = Track(first.Start(mExecutor));
            first.Deinit();
            ActionQueue.Drain();
            var replacement = Own(Kit.Condition(() => false));
            Assert.That(replacement, Is.SameAs(first));
            IActionController current = null;
            Assert.DoesNotThrow(() => current = Track(replacement.Start(mExecutor)));
            mExecutor.Tick(0, 0);
            Assert.That(oldController.Action, Is.Null);
            Assert.That(current.Action, Is.SameAs(replacement));
            Assert.That(replacement.Deinited, Is.False);
        }

        [Test]
        public void PublicUpdateHelperDoesNotNotifySuccessAfterTheActionCancelsItself()
        {
            var cancelled = Own(Kit.Custom(api => api.OnStart(() =>
            {
                api.Finish();
                ((IAction)api).Deinit();
            })));
            var other = Track(ActionController.Allocate());
            other.Action = cancelled;
            other.ActionID = cancelled.ActionID;
            var calls = 0;
            Assert.That(mExecutor.UpdateAction(other, 0, _ => calls++), Is.True);
            Assert.That(calls, Is.Zero);
            Assert.That(cancelled.Deinited, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SequenceCancellationDuringExecuteReleasesItsControllerInTheSameTick(bool conditional)
        {
            var child = Own(Kit.Condition(() => false));
            var steps = 0;
            var finishes = 0;
            var parent = Own(conditional
                ? Kit.Sequence().ConditionSequence(() => true, inner => inner.Append(child)).Callback(() => steps++)
                : Kit.Sequence().Append(child).Callback(() => steps++));
            var controller = Track(parent.Start(mExecutor, () => finishes++));
            mExecutor.Tick(0, 0);
            child.Deinit();
            mExecutor.Tick(0, 0);
            Assert.That(parent.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
            Assert.That(mExecutor.ScheduledCount, Is.Zero);
            Assert.That(steps, Is.Zero);
            Assert.That(finishes, Is.Zero);
        }
    }
}
