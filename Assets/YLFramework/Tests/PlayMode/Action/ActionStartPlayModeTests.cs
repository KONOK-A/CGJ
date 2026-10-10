using YLFramework.Actions;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Timing;
using Kit = YLFramework.Actions.ActionKit;

namespace YLFramework.Tests.Actions
{
    public class ActionStartPlayModeTests
    {
        private readonly List<IAction> mActions = new List<IAction>();
        private readonly List<IActionController> mControllers = new List<IActionController>();
        private readonly List<GameObject> mRoots = new List<GameObject>();
        private readonly List<AsyncOperation> mUnloads = new List<AsyncOperation>();
        private readonly List<Scene> mScenes = new List<Scene>();
        private ActionStartTestCaller mCaller;
        private ActionGlobalHost mPreviousGlobal;
        private ActionKitCurrentScene mPreviousSceneHost;
        private ActionRecycleDriver mPreviousRecycle;
        private float mPreviousTimeScale;
        private Scene mPreviousActiveScene;
        private bool mOwnActionSession;

        [SetUp]
        public void SetUp()
        {
            mOwnActionSession = false;
            var canOwnActionSession = ActionFixtureSession.CanOwnUninitializedSession();
            mPreviousTimeScale = Time.timeScale;
            mPreviousActiveScene = SceneManager.GetActiveScene();
            mPreviousGlobal = Object.FindObjectOfType<ActionGlobalHost>(true);
            mPreviousSceneHost = Object.FindObjectOfType<ActionKitCurrentScene>(true);
            mPreviousRecycle = Object.FindObjectOfType<ActionRecycleDriver>(true);
            // Only self-owned executors may be destroyed before recycling this fixture's controllers.
            Assert.That(mPreviousGlobal == null, Is.True, "Run this fixture in an isolated test scene without an existing ActionKit global host; it will not modify another owner's executor.");
            Assert.That(mPreviousSceneHost == null, Is.True, "Run this fixture without an existing ActionKit current-scene host; it will not replace or destroy another owner's host.");
            mOwnActionSession = canOwnActionSession;
            ActionQueue.Drain();
            var root = new GameObject("ActionKit.Start.PlayModeTest");
            mRoots.Add(root);
            mCaller = root.AddComponent<ActionStartTestCaller>();
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = mPreviousTimeScale;
            if (mPreviousActiveScene.IsValid() && mPreviousActiveScene.isLoaded)
                SceneManager.SetActiveScene(mPreviousActiveScene);

            var global = Object.FindObjectOfType<ActionGlobalHost>(true);
            var sceneHost = Object.FindObjectOfType<ActionKitCurrentScene>(true);
            if (global && global != mPreviousGlobal) mRoots.Add(global.gameObject);
            if (sceneHost && sceneHost != mPreviousSceneHost) mRoots.Add(sceneHost.gameObject);
            foreach (var root in mRoots)
            {
                if (root) Object.Destroy(root);
            }
            mRoots.Clear();
            yield return null;

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
            var recycle = Object.FindObjectOfType<ActionRecycleDriver>(true);
            if (recycle && recycle != mPreviousRecycle) Object.Destroy(recycle.gameObject);
            foreach (var unload in mUnloads) yield return unload;
            foreach (var scene in mScenes)
            {
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            mUnloads.Clear();
            mScenes.Clear();
            if (mOwnActionSession)
            {
                try { Kit.Shutdown(); }
                finally { mOwnActionSession = false; }
            }
            yield return null;
        }

        private IEnumerator CheckSynchronousStart(int scope, bool typed)
        {
            var calls = 0;
            var action = Own(Kit.Callback(() => calls++));
            IActionController observed = null;
            var beforeDeinit = false;
            System.Action<IActionController> typedCallback = controller =>
            {
                observed = controller;
                beforeDeinit = !controller.Action.Deinited;
                calls++;
            };
            System.Action callback = () => calls++;
            IActionController result;
            if (scope == 0) result = typed ? action.Start(mCaller, typedCallback) : action.Start(mCaller, callback);
            else if (scope == 1) result = typed ? action.StartCurrentScene(typedCallback) : action.StartCurrentScene(callback);
            else result = typed ? action.StartGlobal(typedCallback) : action.StartGlobal(callback);
            Track(result);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(action.Deinited, Is.True);
            if (typed)
            {
                Assert.That(observed, Is.SameAs(result));
                Assert.That(beforeDeinit, Is.True);
            }
            yield return null;
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LocalTypedStartCompletesSynchronously() => CheckSynchronousStart(0, true);
        [UnityTest]
        public IEnumerator LocalActionStartCompletesSynchronously() => CheckSynchronousStart(0, false);
        [UnityTest]
        public IEnumerator SceneTypedStartCompletesSynchronously() => CheckSynchronousStart(1, true);
        [UnityTest]
        public IEnumerator SceneActionStartCompletesSynchronously() => CheckSynchronousStart(1, false);
        [UnityTest]
        public IEnumerator GlobalTypedStartCompletesSynchronously() => CheckSynchronousStart(2, true);
        [UnityTest]
        public IEnumerator GlobalActionStartCompletesSynchronously() => CheckSynchronousStart(2, false);

        [UnityTest]
        public IEnumerator LocalStartAddsOneExecutorAndDrivesASequenceAutomatically()
        {
            var calls = 0;
            var sequence = Own(Kit.Sequence().DelayFrame(1).Callback(() => calls++));
            var controller = Track(sequence.Start(mCaller, () => calls++));
            Assert.That(mCaller.GetComponents<MonoUpdateActionExecutor>().Length, Is.EqualTo(1));
            for (var frame = 0; frame < 8 && calls < 2; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(sequence.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator TimerAndActionNamesAndCleanupStayInTheirOwnDomains()
        {
            var driverType = typeof(YLFramework.Timing.Timer).Assembly.GetType("YLFramework.Timing.Internal.TimerDriver", true);
            Assert.That(Object.FindObjectsByType(driverType, FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty,
                "不接管已有 TimerDriver。");
            using var scope = new TimerScope();
            Component ownedHost = null;
            try
            {
                var ready = false;
                var calls = 0;
                var action = Own(Kit.Condition(() => ready));
                var actionRun = action.StartNamed(mCaller, "same", () => calls++);
                var timerRun = scope.PostNamed("same", 60);
                ownedHost = (Component)Object.FindObjectsByType(driverType, FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
                scope.CancelAll();
                Assert.That(timerRun.Status, Is.EqualTo(TimerState.Cancelled));
                Assert.That(actionRun.IsActive, Is.True, "Timer Scope 清理不取消同名 Action。");
                timerRun = scope.PostNamed("same", 60);
                Assert.That(actionRun.Cancel(), Is.True);
                Assert.That(timerRun.IsActive, Is.True, "Action 清理不取消同名 Timer。");
                var survivor = Own(Kit.Condition(() => ready)).StartNamed(mCaller, "same", () => calls++);
                YLFramework.Timing.Timer.Shutdown();
                Assert.That(timerRun.Status, Is.EqualTo(TimerState.Invalidated));
                Assert.That(survivor.IsActive, Is.True, "默认 Timer owner 结束不清 Action 执行器。");
                ready = true;
                for (var frame = 0; frame < 5 && calls == 0; frame++) yield return null;
                Assert.That(calls, Is.EqualTo(1), "只有未取消的 Action 正常完成。");
                Assert.That(survivor.IsActive, Is.False);
            }
            finally
            {
                if (ownedHost) Object.Destroy(ownedHost.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ControllerPauseResumeAndDeinitDoNotTurnCancellationIntoCompletion()
        {
            var ready = false;
            var calls = 0;
            var action = Own(Kit.Condition(() => ready));
            var controller = Track(action.Start(mCaller, () => calls++));
            controller.Pause();
            ready = true;
            yield return null;
            yield return null;
            Assert.That(calls, Is.Zero);
            Assert.That(action.Deinited, Is.False);
            controller.Resume();
            for (var frame = 0; frame < 5 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1));

            var cancelled = Own(Kit.Condition(() => false));
            var cancelController = Track(cancelled.Start(mCaller, () => calls++));
            cancelController.Deinit();
            yield return null;
            yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(cancelController.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator IgnoreTimeScaleSelectsUnscaledDeltaWhileScaledLerpWaits()
        {
            Time.timeScale = 0;
            var scaledCalls = 0;
            var unscaledCalls = 0;
            var scaled = Own(Kit.Lerp(0, 1, 0.01f, value => { }, () => scaledCalls++));
            var unscaled = Own(Kit.Lerp(0, 1, 0.01f, value => { }, () => unscaledCalls++));
            var scaledController = Track(scaled.Start(mCaller));
            var unscaledController = Track(unscaled.Start(mCaller)).IgnoreTimeScale();
            var limit = Time.realtimeSinceStartup + 2f;
            while (unscaledCalls == 0 && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(unscaledCalls, Is.EqualTo(1));
            Assert.That(scaledCalls, Is.Zero);
            Assert.That(unscaledController.Action, Is.Null);
            Assert.That(scaledController.Action, Is.SameAs(scaled));
        }

        [UnityTest]
        public IEnumerator DisablingOnlyTheCallerComponentDoesNotDisableTheExecutor()
        {
            var ready = false;
            var calls = 0;
            var action = Own(Kit.Condition(() => ready));
            Track(action.Start(mCaller, () => calls++));
            mCaller.enabled = false;
            ready = true;
            for (var frame = 0; frame < 5 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisablingTheGameObjectPausesAutomaticExecutionUntilReenabled()
        {
            var ready = false;
            var calls = 0;
            var action = Own(Kit.Condition(() => ready));
            Track(action.Start(mCaller, () => calls++));
            mCaller.gameObject.SetActive(false);
            ready = true;
            yield return null;
            yield return null;
            Assert.That(calls, Is.Zero);
            mCaller.gameObject.SetActive(true);
            for (var frame = 0; frame < 5 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DestroyingLocalHostCancelsItsOwnedActions()
        {
            var action = Own(Kit.Condition(() => false));
            var controller = Track(action.Start(mCaller));
            Object.Destroy(mCaller.gameObject);
            yield return null;
            yield return null;
            Assert.That(action.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator GlobalActionSurvivesCallerDestruction()
        {
            var ready = false;
            var calls = 0;
            var action = Own(Kit.Condition(() => ready));
            Track(action.StartGlobal(() => calls++));
            Object.Destroy(mCaller.gameObject);
            yield return null;
            ready = true;
            for (var frame = 0; frame < 5 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<ActionGlobalHost>(true).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SceneHostIsUnloadedWhileGlobalHostAndItsActionSurvive()
        {
            Assert.That(mPreviousSceneHost == null, Is.True, "This scene test must create and own its scene host.");
            var scene = SceneManager.CreateScene("ActionKit.Start.SceneTest");
            mScenes.Add(scene);
            SceneManager.SetActiveScene(scene);
            var sceneAction = Own(Kit.Condition(() => false));
            Track(sceneAction.StartCurrentScene());
            var host = ActionKitCurrentScene.SceneComponent;
            Assert.That(host.gameObject.scene, Is.EqualTo(scene));
            var ready = false;
            var calls = 0;
            var globalAction = Own(Kit.Condition(() => ready));
            Track(globalAction.StartGlobal(() => calls++));
            var global = ActionGlobalHost.Instance;
            Assert.That(global.gameObject.scene, Is.Not.EqualTo(scene));
            SceneManager.SetActiveScene(mPreviousActiveScene);
            var unload = SceneManager.UnloadSceneAsync(scene);
            mUnloads.Add(unload);
            yield return unload;
            Assert.That(host == null, Is.True);
            Assert.That(ActionKitCurrentScene.mSceneComponent == null, Is.True);
            Assert.That(sceneAction.Deinited, Is.True);
            Assert.That(global != null, Is.True);
            ready = true;
            for (var frame = 0; frame < 5 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NamedScopesAreIndependentButComponentsOnTheSameObjectShareNames()
        {
            var before = Object.FindObjectsOfType<MonoUpdateActionExecutor>(true).Length;
            Assert.That(Kit.TryGetNamedCurrentScene("shared", out _) || Kit.TryGetNamedGlobal("shared", out _), Is.False);
            Assert.That(Object.FindObjectsOfType<MonoUpdateActionExecutor>(true).Length, Is.EqualTo(before));
            Assert.That(Object.FindObjectOfType<ActionGlobalHost>(true), Is.Null);
            Assert.That(Object.FindObjectOfType<ActionKitCurrentScene>(true), Is.Null);

            var sibling = mCaller.gameObject.AddComponent<ActionStartTestCaller>();
            var local = Own(Kit.Condition(() => false)).StartNamed(mCaller, "shared");
            var rejected = Own(Kit.Condition(() => false));
            Assert.Throws<System.InvalidOperationException>(() => rejected.StartNamed(sibling, "shared"));
            Assert.That(rejected.Deinited, Is.False);
            Assert.That(Kit.TryGetNamed(sibling, "shared", out var sameObject), Is.True);
            Assert.That(sameObject.Pause(), Is.True);
            Assert.That(Kit.ResumeNamed(mCaller, "shared"), Is.True);

            var otherRoot = new GameObject("ActionKit.Named.OtherObject");
            mRoots.Add(otherRoot);
            var other = rejected.StartNamed(otherRoot.AddComponent<ActionStartTestCaller>(), "shared");
            var scene = Own(Kit.Condition(() => false)).StartNamedCurrentScene("shared");
            var global = Own(Kit.Condition(() => false)).StartNamedGlobal("shared");
            Assert.That(local.IsActive && other.IsActive && scene.IsActive && global.IsActive, Is.True);
            Assert.That(Kit.CancelNamedCurrentScene("shared"), Is.True);
            Assert.That(scene.IsActive, Is.False);
            Assert.That(local.IsActive && other.IsActive && global.IsActive, Is.True);
            var replaced = Own(Kit.Condition(() => false)).ReplaceNamedGlobal("shared");
            Assert.That(global.IsActive, Is.False);
            Assert.That(replaced.IsActive, Is.True);
            yield return null;
            Assert.That(Kit.ContainsNamedGlobal("shared"), Is.True);
        }

        [UnityTest]
        public IEnumerator NamedObjectExecutionPausesWithGameObjectAndDestroyInvalidatesItsHandle()
        {
            var ready = false;
            var calls = 0;
            var handle = Own(Kit.Condition(() => ready)).StartNamed(mCaller, "owner", () => calls++);
            mCaller.gameObject.SetActive(false);
            ready = true;
            yield return null;
            yield return null;
            Assert.That(handle.IsActive, Is.True);
            Assert.That(Kit.ContainsNamed(mCaller, "owner"), Is.True);
            Assert.That(calls, Is.Zero);
            mCaller.gameObject.SetActive(true);
            mCaller.enabled = false;
            for (var frame = 0; frame < 5 && calls == 0; frame++) yield return null;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(handle.IsActive, Is.False);
            var pending = Own(Kit.Condition(() => false)).ReplaceNamed(mCaller, "owner", () => calls++);
            Object.Destroy(mCaller.gameObject);
            yield return null;
            Assert.That(pending.IsActive || pending.Cancel() || pending.Pause() || pending.Resume(), Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NamedSceneUnloadReleasesOnlyItsScopeAndGlobalNameSurvives()
        {
            var scene = SceneManager.CreateScene("ActionKit.Named.SceneTest");
            mScenes.Add(scene);
            SceneManager.SetActiveScene(scene);
            var sceneHandle = Own(Kit.Condition(() => false)).StartNamedCurrentScene("scope");
            var globalHandle = Own(Kit.Condition(() => false)).StartNamedGlobal("scope");
            SceneManager.SetActiveScene(mPreviousActiveScene);
            var unload = SceneManager.UnloadSceneAsync(scene);
            mUnloads.Add(unload);
            yield return unload;
            Assert.That(sceneHandle.IsActive, Is.False);
            Assert.That(Kit.ContainsNamedCurrentScene("scope"), Is.False);
            Assert.That(globalHandle.IsActive, Is.True);
            Assert.That(Kit.ContainsNamedGlobal("scope"), Is.True);
            Assert.That(Kit.CancelNamedGlobal("scope"), Is.True);
            Assert.That(Kit.CancelNamedGlobal("scope"), Is.False);
        }
    }
}
