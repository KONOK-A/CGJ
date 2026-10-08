using YLFramework.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Actions.DOTween
{
    using DOTween = global::DG.Tweening.DOTween;

    public class DOTweenActionPlayModeTests
    {
        private readonly List<ActionLease> mActions = new List<ActionLease>();
        private readonly Dictionary<Tween, object> mTweens = new Dictionary<Tween, object>();
        private readonly List<GameObject> mRoots = new List<GameObject>();
        private readonly List<IActionController> mControllers = new List<IActionController>();
        private MonoUpdateActionExecutor mLocal;
        private Action mRestoreSettings;
        private bool mOwnEnvironment;

        [SetUp]
        public void SetUp()
        {
            mOwnEnvironment = false;
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(ActionKit.Initialized, Is.False, "Shutdown tests require an unowned ActionKit session.");
            Assert.That(Object.FindObjectsOfType<MonoUpdateActionExecutor>(true), Is.Empty, "Existing controllers must not be cancelled by this fixture.");
            Assert.That(Object.FindObjectsOfType<ActionGlobalHost>(true), Is.Empty);
            Assert.That(Object.FindObjectsOfType<ActionKitCurrentScene>(true), Is.Empty);
            Assert.That(Object.FindObjectsOfType<ActionRecycleDriver>(true), Is.Empty);
            Assert.That(Object.FindObjectsOfType<ScreenTransitionCanvas>(true), Is.Empty);
            Assert.That(ActionQueue.PendingCount, Is.Zero);
            var contextType = SynchronizationContext.Current?.GetType();
            Assert.That(contextType, Is.Not.Null, "Run in the real Unity Player Test Runner with its original synchronization context.");
            Assert.That(contextType.FullName, Is.EqualTo("UnityEngine.UnitySynchronizationContext"));
            Assert.That(contextType.Assembly, Is.SameAs(typeof(Object).Assembly));
            Assert.That(contextType.IsSealed, Is.True);
            mOwnEnvironment = true;

            var timeScale = DOTween.timeScale;
            var unscaledTimeScale = DOTween.unscaledTimeScale;
            var recyclable = DOTween.defaultRecyclable;
            var safeMode = DOTween.useSafeMode;
            var log = DOTween.logBehaviour;
            var safeLog = DOTween.safeModeLogBehaviour;
            var nestedFailure = DOTween.nestedTweenFailureBehaviour;
            var report = DOTween.showUnityEditorReport;
            var smooth = DOTween.useSmoothDeltaTime;
            var maxSmooth = DOTween.maxSmoothUnscaledTime;
            var gizmos = DOTween.drawGizmos;
            var debug = DOTween.debugMode;
            var targetId = DOTween.debugStoreTargetId;
            var update = DOTween.defaultUpdateType;
            var independent = DOTween.defaultTimeScaleIndependent;
            var autoPlay = DOTween.defaultAutoPlay;
            var autoKill = DOTween.defaultAutoKill;
            var loop = DOTween.defaultLoopType;
            var ease = DOTween.defaultEaseType;
            var overshoot = DOTween.defaultEaseOvershootOrAmplitude;
            var period = DOTween.defaultEasePeriod;
            mRestoreSettings = () =>
            {
                DOTween.timeScale = timeScale;
                DOTween.unscaledTimeScale = unscaledTimeScale;
                DOTween.defaultRecyclable = recyclable;
                DOTween.useSafeMode = safeMode;
                DOTween.logBehaviour = log;
                DOTween.safeModeLogBehaviour = safeLog;
                DOTween.nestedTweenFailureBehaviour = nestedFailure;
                DOTween.showUnityEditorReport = report;
                DOTween.useSmoothDeltaTime = smooth;
                DOTween.maxSmoothUnscaledTime = maxSmooth;
                DOTween.drawGizmos = gizmos;
                DOTween.debugMode = debug;
                DOTween.debugStoreTargetId = targetId;
                DOTween.defaultUpdateType = update;
                DOTween.defaultTimeScaleIndependent = independent;
                DOTween.defaultAutoPlay = autoPlay;
                DOTween.defaultAutoKill = autoKill;
                DOTween.defaultLoopType = loop;
                DOTween.defaultEaseType = ease;
                DOTween.defaultEaseOvershootOrAmplitude = overshoot;
                DOTween.defaultEasePeriod = period;
            };
            // Retain the DOTween service; Clear/destroy would mutate shared initialization and pools.
            Assert.That(DOTween.Init(recyclable, safeMode, log), Is.Not.Null,
                "A live DOTween service must be initialized before creating lifecycle test tweens.");
            mRestoreSettings();
            var root = new GameObject("ActionKit.DOTween.PlayModeTest");
            mRoots.Add(root);
            mLocal = root.AddComponent<MonoUpdateActionExecutor>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!mOwnEnvironment) yield break;
            try
            {
                foreach (var entry in mTweens)
                {
                    var tween = entry.Key;
                    if (!tween.IsActive() || !ReferenceEquals(tween.target, entry.Value)) continue;
                    tween.onComplete = tween.onKill = tween.onPlay = tween.onPause = null;
                    tween.OnStart(null);
                }
                foreach (var lease in mActions)
                    if (lease.Matches && !lease.Action.Deinited) lease.Action.Deinit();
                // Setup proved all module hosts/controllers were absent before acquiring this session.
                ActionKit.Shutdown();
                foreach (var root in mRoots)
                    if (root) Object.Destroy(root);
                yield return UnityContextTurn();
                yield return null;
                foreach (var controller in mControllers)
                    if (controller.Action != null) controller.Recycle();
                ActionQueue.Drain();
            }
            finally
            {
                foreach (var entry in mTweens)
                    if (entry.Key.IsActive() && ReferenceEquals(entry.Key.target, entry.Value)) entry.Key.Kill(false);
                foreach (var root in mRoots)
                    if (root) Object.Destroy(root);
                mRestoreSettings?.Invoke();
                mRestoreSettings = null;
                mControllers.Clear();
                mActions.Clear();
                mTweens.Clear();
                mRoots.Clear();
                mLocal = null;
                mOwnEnvironment = false;
            }
        }

        private T Own<T>(T action) where T : IAction
        {
            mActions.Add(new ActionLease(action));
            return action;
        }

        private Tween NewTween(bool autoKill = true, bool independent = false)
        {
            var value = 0f;
            var source = new object();
            var tween = DOTween.To(() => value, next => value = next, 1f, 1f)
                .SetUpdate(UpdateType.Manual, independent).SetEase(Ease.Linear)
                .SetAutoKill(autoKill).SetRecyclable(false).SetTarget(source);
            tween.OnPause(null).Pause();
            mTweens[tween] = source;
            return tween;
        }

        private IActionController Start(IAction action, int scope, Action<IActionController> finished = null)
        {
            IActionController controller;
            if (scope == 0) controller = action.Start(mLocal, finished);
            else if (scope == 1)
            {
                var host = ActionKitCurrentScene.SceneComponent;
                if (!mRoots.Contains(host.gameObject)) mRoots.Add(host.gameObject);
                controller = action.StartCurrentScene(finished);
            }
            else
            {
                var host = ActionGlobalHost.Instance;
                if (!mRoots.Contains(host.gameObject)) mRoots.Add(host.gameObject);
                controller = action.StartGlobal(finished);
            }
            mControllers.Add(controller);
            return controller;
        }

        private static IEnumerator Await(Func<bool> condition)
        {
            var limit = Time.realtimeSinceStartup + 2f;
            while (!condition() && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(condition(), Is.True, "The real Unity executor did not reach the expected state within two seconds.");
        }

        private static IEnumerator UnityContextTurn()
        {
            var executed = false;
            SynchronizationContext.Current.Post(_ => executed = true, null);
            yield return Await(() => executed);
        }

        private void Advance(float scaled = 1.1f, float unscaled = 1.1f)
        {
            // Explicit fixture dt and allocation identity avoid advancing another owner's manual tweens.
            foreach (var entry in mTweens.ToArray())
                if (entry.Key.IsActive() && ReferenceEquals(entry.Key.target, entry.Value))
                    entry.Key.ManualUpdate(scaled, unscaled);
        }

        [UnityTest]
        public IEnumerator OwnedManualAdvanceDoesNotProgressAnUntrackedManualTween()
        {
            var value = 0f;
            var token = new object();
            var complete = 0;
            var killed = 0;
            var foreign = DOTween.To(() => value, next => value = next, 1f, 1f)
                .SetUpdate(UpdateType.Manual).SetAutoKill(false).SetRecyclable(false).SetTarget(token)
                .OnPause(null).OnComplete(() => complete++).OnKill(() => killed++).Play();
            try
            {
                var timeScale = DOTween.timeScale;
                var unscaledScale = DOTween.unscaledTimeScale;
                var own = NewTween(false).Play();
                Advance();
                yield return null;
                Assert.That(own.IsComplete(), Is.True);
                Assert.That(foreign.IsActive(), Is.True);
                Assert.That(foreign.IsPlaying(), Is.True);
                Assert.That(foreign.Elapsed(), Is.Zero);
                Assert.That(value, Is.Zero);
                Assert.That(complete, Is.Zero);
                Assert.That(killed, Is.Zero);
                Assert.That(DOTween.timeScale, Is.EqualTo(timeScale));
                Assert.That(DOTween.unscaledTimeScale, Is.EqualTo(unscaledScale));
            }
            finally
            {
                if (foreign.IsActive() && ReferenceEquals(foreign.target, token)) foreign.Kill(false);
            }
        }

        private IEnumerator CheckCompletion(int scope, bool autoKill)
        {
            var complete = 0;
            var kills = 0;
            var calls = 0;
            IActionController observed = null;
            var observedLiveAction = false;
            var tween = NewTween(autoKill).OnComplete(() => complete++).OnKill(() => kills++);
            var action = Own(tween.ToAction());
            var controller = Start(action, scope, current =>
            {
                calls++;
                observed = current;
                observedLiveAction = ReferenceEquals(current.Action, action) && !action.Deinited;
            });
            Assert.That(controller.Action, Is.SameAs(action));
            Assert.That(tween.IsPlaying(), Is.True);
            Assert.That(calls, Is.Zero);
            Advance();
            yield return Await(() => controller.Action == null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(complete, Is.EqualTo(1));
            Assert.That(observed, Is.SameAs(controller));
            Assert.That(observedLiveAction, Is.True);
            Assert.That(kills, Is.EqualTo(autoKill ? 1 : 0));
            Assert.That(tween.IsActive(), Is.EqualTo(!autoKill));
            yield return null;
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LocalStartCompletesWithNaturalAutoKill() => CheckCompletion(0, true);

        [UnityTest]
        public IEnumerator SceneStartCompletesWithNaturalAutoKill() => CheckCompletion(1, true);

        [UnityTest]
        public IEnumerator GlobalStartCompletesWithNaturalAutoKill() => CheckCompletion(2, true);

        [UnityTest]
        public IEnumerator LocalStartDetachesCompletedAutoKillFalseTween() => CheckCompletion(0, false);

        [UnityTest]
        public IEnumerator SceneStartDetachesCompletedAutoKillFalseTween() => CheckCompletion(1, false);

        [UnityTest]
        public IEnumerator GlobalStartDetachesCompletedAutoKillFalseTween() => CheckCompletion(2, false);

        private IEnumerator CheckCancellation(int scope)
        {
            var calls = 0;
            var complete = 0;
            var tween = NewTween().OnComplete(() => complete++);
            var action = Own(tween.ToAction());
            var controller = Start(action, scope, _ => calls++);
            tween.Pause();
            Advance();
            yield return null;
            yield return null;
            Assert.That(controller.Action, Is.SameAs(action));
            Assert.That(action.Deinited, Is.False);
            Assert.That(calls, Is.Zero);
            tween.Kill(false);
            yield return Await(() => controller.Action == null);
            Assert.That(complete, Is.Zero);
            Assert.That(calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LocalStartExternalPauseWaitsAndKillFalseCancels() => CheckCancellation(0);

        [UnityTest]
        public IEnumerator SceneStartExternalPauseWaitsAndKillFalseCancels() => CheckCancellation(1);

        [UnityTest]
        public IEnumerator GlobalStartExternalPauseWaitsAndKillFalseCancels() => CheckCancellation(2);

        private IEnumerator CheckDestroy(int scope)
        {
            var calls = 0;
            var killed = 0;
            var tween = NewTween().OnKill(() => killed++);
            var unrelated = NewTween(false).Play();
            var action = Own(tween.ToAction());
            var controller = Start(action, scope, _ => calls++);
            controller.Pause();
            GameObject host;
            if (scope == 0) host = mLocal.gameObject;
            else if (scope == 1) host = ActionKitCurrentScene.SceneComponent.gameObject;
            else host = ActionGlobalHost.Instance.gameObject;
            Object.Destroy(host);
            yield return Await(() => controller.Action == null);
            yield return UnityContextTurn();
            Assert.That(tween.IsActive(), Is.False);
            Assert.That(killed, Is.EqualTo(1));
            Assert.That(calls, Is.Zero);
            Assert.That(unrelated.IsActive(), Is.True);
            Assert.That(unrelated.IsPlaying(), Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyLocalHostKillsOnlyItsPausedOwnedTween() => CheckDestroy(0);

        [UnityTest]
        public IEnumerator DestroySceneHostKillsOnlyItsPausedOwnedTween() => CheckDestroy(1);

        [UnityTest]
        public IEnumerator DestroyGlobalHostKillsOnlyItsPausedOwnedTween() => CheckDestroy(2);

        [UnityTest]
        public IEnumerator ControllerPauseResumeDoesNotTakeOverAnExternalPause()
        {
            var calls = 0;
            var tween = NewTween();
            var action = Own(tween.ToAction());
            var controller = Start(action, 0, _ => calls++);
            controller.Pause();
            Assert.That(tween.IsPlaying(), Is.False);
            Advance(0.4f, 0.4f);
            yield return null;
            Assert.That(tween.Elapsed(), Is.Zero);
            controller.Resume();
            Assert.That(tween.IsPlaying(), Is.True);
            tween.Pause();
            controller.Pause();
            controller.Resume();
            Assert.That(tween.IsPlaying(), Is.False);
            Advance();
            yield return null;
            Assert.That(controller.Action, Is.SameAs(action));
            Assert.That(calls, Is.Zero);
            tween.Play();
            Advance();
            yield return Await(() => controller.Action == null);
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ControllerCancellationKeepsReservationUntilRealContextKillFalse()
        {
            var complete = 0;
            var kills = 0;
            var calls = 0;
            var tween = NewTween(false).OnComplete(() => complete++).OnKill(() => kills++);
            var action = Own(tween.ToAction());
            var controller = Start(action, 0, _ => calls++);
            controller.Pause();
            controller.Deinit();
            Assert.That(action.Deinited, Is.True);
            Assert.That(action.Status, Is.EqualTo(ActionStatus.Started));
            Assert.That(tween.IsActive(), Is.True);
            Assert.That(tween.IsPlaying(), Is.False);
            Assert.That(kills, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => tween.ToAction());
            ActionQueue.Drain();
            Assert.That(tween.IsActive(), Is.True);
            Assert.That(complete, Is.Zero);
            Assert.That(calls, Is.Zero);
            yield return UnityContextTurn();
            yield return Await(() => controller.Action == null);
            Assert.That(tween.IsActive(), Is.False);
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(complete, Is.Zero);
            Assert.That(calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator IgnoreTimeScaleDoesNotReconfigureTheTweenOrGlobalDefaults()
        {
            var defaultUpdate = DOTween.defaultUpdateType;
            var defaultIndependent = DOTween.defaultTimeScaleIndependent;
            var scaledTween = NewTween(false, false);
            var independentTween = NewTween(false, true);
            var scaled = Start(Own(scaledTween.ToAction()), 0).IgnoreTimeScale();
            var independent = Start(Own(independentTween.ToAction()), 0);
            Advance(0, 0.5f);
            yield return null;
            Assert.That(scaled.UpdateMode, Is.EqualTo(ActionUpdateModes.UnscaledDeltaTime));
            Assert.That(scaledTween.IsTimeScaleIndependent(), Is.False);
            Assert.That(independentTween.IsTimeScaleIndependent(), Is.True);
            Assert.That(scaledTween.Elapsed(), Is.Zero);
            Assert.That(independentTween.Elapsed(), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(DOTween.defaultUpdateType, Is.EqualTo(defaultUpdate));
            Assert.That(DOTween.defaultTimeScaleIndependent, Is.EqualTo(defaultIndependent));
            Assert.That(scaled.Action, Is.Not.Null);
            Assert.That(independent.Action, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator DuplicateActionOwnerAcrossScopesIsRejectedAndOriginalStillCompletes()
        {
            var tween = NewTween();
            var action = Own(tween.ToAction());
            var calls = 0;
            var controller = Start(action, 0, _ => calls++);
            Assert.Throws<InvalidOperationException>(() => Start(action, 2));
            Assert.That(controller.Action, Is.SameAs(action));
            Assert.That(action.Deinited, Is.False);
            Assert.That(tween.IsPlaying(), Is.True);
            Advance();
            yield return Await(() => controller.Action == null);
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UserOnCompleteDeinitDoesNotInvokeActionSuccessCallback()
        {
            IAction action = null;
            var userComplete = 0;
            var calls = 0;
            var tween = NewTween(false).OnComplete(() => { userComplete++; action.Deinit(); });
            action = Own(tween.ToAction());
            var controller = Start(action, 1, _ => calls++);
            Advance();
            Assert.That(action.Deinited, Is.True);
            Assert.That(tween.IsActive(), Is.True);
            Assert.Throws<InvalidOperationException>(() => tween.ToAction());
            yield return Await(() => controller.Action == null);
            yield return UnityContextTurn();
            Assert.That(userComplete, Is.EqualTo(1));
            Assert.That(calls, Is.Zero);
            Assert.That(tween.IsActive(), Is.False);
        }

        [UnityTest]
        public IEnumerator FinishCallbackCanStartAnotherActionWithoutOldCleanupKillingIt()
        {
            var firstCalls = 0;
            var nextCalls = 0;
            var firstTween = NewTween();
            var nextTween = NewTween();
            var first = Own(firstTween.ToAction());
            var next = Own(nextTween.ToAction());
            IActionController nextController = null;
            var firstController = Start(first, 0, _ =>
            {
                firstCalls++;
                nextController = Start(next, 2, __ => nextCalls++);
            });
            Advance();
            yield return Await(() => firstController.Action == null);
            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(nextCalls, Is.Zero);
            Assert.That(nextController.Action, Is.SameAs(next));
            Assert.That(nextTween.IsActive(), Is.True);
            Advance();
            yield return Await(() => nextController.Action == null);
            Assert.That(nextCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ShutdownCancelsAllThreeScopesButLeavesUnrelatedTweenAndUserRoot()
        {
            var calls = 0;
            var owned = new List<Tween>();
            for (var scope = 0; scope < 3; scope++)
            {
                var tween = NewTween();
                owned.Add(tween);
                Start(Own(tween.ToAction()), scope, _ => calls++).Pause();
            }
            var unrelated = NewTween(false).Play();
            var userRoot = mLocal.gameObject;
            ActionKit.Shutdown();
            Assert.That(ActionKit.Initialized, Is.False);
            foreach (var tween in owned)
            {
                Assert.That(tween.IsActive(), Is.True);
                Assert.Throws<InvalidOperationException>(() => tween.ToAction());
            }
            foreach (var controller in mControllers) Assert.That(controller.Action, Is.Null);
            Assert.That(unrelated.IsActive(), Is.True);
            Assert.That(unrelated.IsPlaying(), Is.True);
            Assert.That(userRoot != null, Is.True);
            yield return UnityContextTurn();
            yield return null;
            foreach (var tween in owned) Assert.That(tween.IsActive(), Is.False);
            Assert.That(userRoot != null, Is.True);
            Assert.That(userRoot.GetComponents<MonoUpdateActionExecutor>(), Is.Empty);
            Assert.That(Object.FindObjectsOfType<ActionGlobalHost>(true), Is.Empty);
            Assert.That(Object.FindObjectsOfType<ActionKitCurrentScene>(true), Is.Empty);
            Assert.That(calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShutdownInsideFactoryKillsTheLateReturnedTweenWithoutSuccess()
        {
            var calls = 0;
            var factoryCalls = 0;
            Tween returned = null;
            var unrelated = NewTween(false).Play();
            var action = Own(DOTweenAction.Allocate(() =>
            {
                factoryCalls++;
                ActionKit.Shutdown();
                return returned = NewTween();
            }));
            var controller = Start(action, 0, _ => calls++);
            Assert.That(factoryCalls, Is.EqualTo(1));
            Assert.That(controller.Action, Is.Null);
            Assert.That(returned.IsActive(), Is.True);
            Assert.Throws<InvalidOperationException>(() => returned.ToAction());
            Assert.That(unrelated.IsActive(), Is.True);
            Assert.That(ActionKit.Initialized, Is.False);
            yield return UnityContextTurn();
            Assert.That(returned.IsActive(), Is.False);
            Assert.That(unrelated.IsActive(), Is.True);
            Assert.That(calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RealExecutorRepeatCreatesFreshTweensAndCompletesExactlyOnce()
        {
            var created = new List<Tween>();
            var calls = 0;
            var repeat = Own(ActionKit.Repeat(3).DOTween(() => { var tween = NewTween(); created.Add(tween); return tween; }));
            var controller = Start(repeat, 0, _ => calls++);
            for (var round = 0; round < 3; round++)
            {
                var expected = round + 1;
                yield return Await(() => created.Count == expected);
                Advance();
            }
            yield return Await(() => controller.Action == null);
            Assert.That(created.Count, Is.EqualTo(3));
            Assert.That(new HashSet<Tween>(created).Count, Is.EqualTo(3));
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
