using YLFramework.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kit = YLFramework.Actions.ActionKit;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Actions
{
    public class ScreenTransitionPlayModeTests
    {
        private readonly List<IAction> mActions = new List<IAction>();
        private readonly List<IActionController> mControllers = new List<IActionController>();
        private ScreenTransitionCanvas mCanvas;
        private ActionRecycleDriver mPreviousRecycle;
        private float mTimeScale;
        private bool mOwnEnvironment;
        private bool mOwnActionSession;

        [SetUp]
        public void SetUp()
        {
            mOwnEnvironment = false;
            mOwnActionSession = false;
            var canOwnActionSession = ActionFixtureSession.CanOwnUninitializedSession();
            mTimeScale = Time.timeScale;
            mPreviousRecycle = Object.FindObjectOfType<ActionRecycleDriver>(true);
            Assert.That(Object.FindObjectOfType<ScreenTransitionCanvas>() == null, Is.True,
                "This fixture must own its transition canvas.");
            Assert.That(Object.FindObjectOfType<ActionGlobalHost>(true) == null, Is.True,
                "This fixture must own its global executor.");
            Assert.That(Resources.Load<GameObject>(ScreenTransitionCanvas.ResourcePath), Is.Not.Null,
                "Run YLFramework > Action > 创建屏幕过渡资源 once, outside Play Mode.");
            mOwnEnvironment = true;
            mOwnActionSession = canOwnActionSession;
            Time.timeScale = 1;
            ActionQueue.Drain();
            mCanvas = ScreenTransitionCanvas.Instance;
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
            if (!mOwnEnvironment) yield break;
            Time.timeScale = mTimeScale;
            var host = Object.FindObjectOfType<ActionGlobalHost>(true);
            if (host) Object.Destroy(host.gameObject);
            var canvas = Object.FindObjectOfType<ScreenTransitionCanvas>();
            if (canvas) Object.Destroy(canvas.gameObject);
            yield return null;
            foreach (var action in mActions)
            {
                if (action is ScreenTransitionInOut<ScreenTransitionFade, ScreenTransitionFade> fadeInOut)
                    fadeInOut.In(null).Out(null).OnInFinish(null);
                if (action is ScreenTransitionInOut<Probe, Probe> probeInOut)
                    probeInOut.In(null).Out(null).OnInFinish(null);
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
            if (mOwnActionSession)
            {
                try { Kit.Shutdown(); }
                finally { mOwnActionSession = false; }
            }
            yield return null;
        }

        public sealed class Probe : AbstractAction<Probe>
        {
            public List<string> Trace;
            public string Name;
            public bool Ready;

            public override void OnStart() => Trace?.Add(Name + ".start");
            public override void OnExecute(float dt)
            {
                if (Ready) this.Finish();
            }
            public override void OnFinish() => Trace?.Add(Name + ".finish");
            protected override void OnDeinit()
            {
                // Sequence resets steps before starting them; keep fluent configuration until disposal.
                Trace = null;
                Name = null;
                Ready = false;
            }
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            var end = Time.realtimeSinceStartup + 2f;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(condition(), Is.True, "The expected transition did not complete within the test bound.");
        }

        [UnityTest]
        public IEnumerator NewResourceInstantiatesOnePersistentCanvasAndKeepsTheImageReference()
        {
            Assert.That(mCanvas, Is.Not.Null);
            Assert.That(mCanvas.ColorImage, Is.Not.Null);
            Assert.That(ScreenTransitionCanvas.Instance, Is.SameAs(mCanvas));
            Assert.That(Object.FindObjectsOfType<ScreenTransitionCanvas>().Length, Is.EqualTo(1));
            Assert.That(mCanvas.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FadeInAndFadeOutRunInSequenceAndFinishTheReturnedController()
        {
            var completed = 0;
            var sequence = Own(Kit.Sequence()
                .Append(Own(Kit.ScreenTransition.FadeIn().Duration(0.01f)))
                .Append(Own(Kit.ScreenTransition.FadeOut().Duration(0.01f))));
            var controller = Track(sequence.StartGlobal(() => completed++));
            yield return Until(() => completed == 1);
            Assert.That(mCanvas.ColorImage.color.a, Is.Zero);
            Assert.That(sequence.Deinited, Is.True);
            Assert.That(controller.Action, Is.Null);
        }

        [UnityTest]
        public IEnumerator IgnoreTimeScaleCanDriveFadeWhileTheScaledTransitionWaits()
        {
            Time.timeScale = 0;
            var completed = 0;
            var fade = Own(Kit.ScreenTransition.FadeIn().Duration(0.01f));
            var controller = Track(fade.StartGlobal(() => completed++));
            yield return null;
            yield return null;
            Assert.That(completed, Is.Zero);
            controller.IgnoreTimeScale();
            yield return Until(() => completed == 1);
            Assert.That(mCanvas.ColorImage.color.a, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FadeInOutPreservesItsMiddleCallbackButDoesNotPretendTheParentCompletes()
        {
            var middle = 0;
            var finished = 0;
            ScreenTransitionFade outFade = null;
            var transition = Own(Kit.ScreenTransition.FadeInOut()
                .In(fade => Own(fade).Duration(0.01f))
                .Out(fade => outFade = Own(fade).Duration(0.01f))
                .OnInFinish(() =>
                {
                    Assert.That(mCanvas.ColorImage.color.a, Is.EqualTo(1));
                    middle++;
                }));
            Track(transition.StartGlobal(() => finished++));
            yield return Until(() => outFade != null && outFade.Deinited);
            Assert.That(middle, Is.EqualTo(1));
            Assert.That(mCanvas.ColorImage.color.a, Is.Zero);
            Assert.That(transition.Status, Is.EqualTo(ActionStatus.Started), "AK-912 remains open.");
            Assert.That(finished, Is.Zero);
        }

        [UnityTest]
        public IEnumerator GenericInOutKeepsSetterAndChildOrderEvenWhenTheParentIsPaused()
        {
            var trace = new List<string>();
            var input = Own(Probe.Allocate());
            var output = Own(Probe.Allocate());
            var parent = Own(ScreenTransitionInOut<Probe, Probe>.Allocate(input, output)
                .In(value => { value.Trace = trace; value.Name = "in"; value.Ready = true; })
                .Out(value => { value.Trace = trace; value.Name = "out"; value.Ready = true; })
                .OnInFinish(() => trace.Add("middle")));
            var controller = Track(parent.StartGlobal());
            controller.Pause();
            yield return Until(() => output.Deinited);
            CollectionAssert.AreEqual(new[] { "in.start", "in.finish", "middle", "out.start", "out.finish" }, trace);
            Assert.That(parent.Paused, Is.True);
            Assert.That(parent.Status, Is.EqualTo(ActionStatus.Started));
        }

        [UnityTest]
        public IEnumerator DeinitOfTheInOutParentDoesNotCancelItsOriginalChildSequence()
        {
            var middle = 0;
            var input = Own(Probe.Allocate());
            var output = Own(Probe.Allocate());
            output.Ready = true;
            var parent = Own(ScreenTransitionInOut<Probe, Probe>.Allocate(input, output).OnInFinish(() => middle++));
            Track(parent.StartGlobal());
            parent.Deinit();
            Assert.That(input.Deinited, Is.False);
            input.Ready = true;
            yield return Until(() => output.Deinited);
            Assert.That(middle, Is.EqualTo(1), "AK-912: parent cancellation is not propagated to the child sequence.");
        }

        [UnityTest]
        public IEnumerator DestroyedCanvasIsRecreatedFromTheNewResource()
        {
            var previous = mCanvas;
            Object.Destroy(previous.gameObject);
            yield return null;
            Assert.That(previous == null, Is.True);
            mCanvas = ScreenTransitionCanvas.Instance;
            Assert.That(mCanvas, Is.Not.Null);
            Assert.That(mCanvas.ColorImage, Is.Not.Null);
            Assert.That(Object.FindObjectsOfType<ScreenTransitionCanvas>().Length, Is.EqualTo(1));
        }
    }
}
