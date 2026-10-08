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
    public class GlobalMessagePlayModeTests
    {
        private readonly List<GameObject> mRoots = new List<GameObject>();
        private readonly List<Action> mCleanup = new List<Action>();
        private float mTimeScale;
        private bool mOwnEnvironment;
        private bool mOwnActionSession;

        [SetUp]
        public void SetUp()
        {
            mOwnActionSession = false;
            var canOwnActionSession = ActionFixtureSession.CanOwnUninitializedSession();
            mTimeScale = Time.timeScale;
            mOwnEnvironment = false;
            Assert.That(Object.FindObjectOfType<ActionGlobalHost>(true) == null, Is.True,
                "This fixture must own the module global host; it will not modify another owner's subscriptions.");
            mOwnEnvironment = true;
            mOwnActionSession = canOwnActionSession;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!mOwnEnvironment) yield break;
            Time.timeScale = mTimeScale;
            foreach (var cleanup in mCleanup) cleanup();
            mCleanup.Clear();
            var global = Object.FindObjectOfType<ActionGlobalHost>(true);
            if (global) mRoots.Add(global.gameObject);
            foreach (var root in mRoots)
            {
                if (root) Object.Destroy(root);
            }
            mRoots.Clear();
            yield return null;
            ActionGlobalHost.ResetSession();
            if (mOwnActionSession)
            {
                try { Kit.Shutdown(); }
                finally { mOwnActionSession = false; }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator SevenPropertiesAreStableModuleOwnedEventsOnOneHost()
        {
            Assert.That(Kit.OnUpdate, Is.SameAs(Kit.OnUpdate));
            Assert.That(Kit.OnFixedUpdate, Is.SameAs(Kit.OnFixedUpdate));
            Assert.That(Kit.OnLateUpdate, Is.SameAs(Kit.OnLateUpdate));
            Assert.That(Kit.OnGUI, Is.SameAs(Kit.OnGUI));
            Assert.That(Kit.OnApplicationFocus, Is.SameAs(Kit.OnApplicationFocus));
            Assert.That(Kit.OnApplicationPause, Is.SameAs(Kit.OnApplicationPause));
            Assert.That(Kit.OnApplicationQuit, Is.SameAs(Kit.OnApplicationQuit));
            Assert.That(Kit.OnUpdate.GetType().Namespace, Is.EqualTo("YLFramework.Events"));
            Assert.That(Object.FindObjectsOfType<ActionGlobalHost>(true).Length, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator NativeUpdateFixedAndLateMessagesRunAndCanBeUnregistered()
        {
            Time.timeScale = 1;
            var updates = 0;
            var fixedUpdates = 0;
            var lateUpdates = 0;
            var update = Kit.OnUpdate;
            var fixedUpdate = Kit.OnFixedUpdate;
            var lateUpdate = Kit.OnLateUpdate;
            Action onUpdate = () => updates++;
            Action onFixed = () => fixedUpdates++;
            Action onLate = () => lateUpdates++;
            update.Register(onUpdate);
            fixedUpdate.Register(onFixed);
            lateUpdate.Register(onLate);
            mCleanup.Add(() => update.UnRegister(onUpdate));
            mCleanup.Add(() => fixedUpdate.UnRegister(onFixed));
            mCleanup.Add(() => lateUpdate.UnRegister(onLate));
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.That(updates, Is.GreaterThan(0));
            Assert.That(fixedUpdates, Is.GreaterThan(0));
            Assert.That(lateUpdates, Is.GreaterThan(0));
            update.UnRegister(onUpdate);
            fixedUpdate.UnRegister(onFixed);
            lateUpdate.UnRegister(onLate);
            var counts = new[] { updates, fixedUpdates, lateUpdates };
            yield return null;
            yield return null;
            CollectionAssert.AreEqual(counts, new[] { updates, fixedUpdates, lateUpdates });
        }

        [UnityTest]
        public IEnumerator GuiFocusPauseAndQuitHandlersForwardTheirValuesWithoutQuittingUnity()
        {
            var gui = 0;
            var focus = new List<bool>();
            var pause = new List<bool>();
            var quit = 0;
            var update = Kit.OnUpdate;
            var host = ActionGlobalHost.Instance;
            host.enabled = false;
            Action guiCallback = () => gui++;
            Action<bool> focusCallback = focus.Add;
            Action<bool> pauseCallback = pause.Add;
            Action quitCallback = () =>
            {
                Assert.That(Kit.OnUpdate, Is.SameAs(update));
                quit++;
            };
            host.OnGUIEvent.Register(guiCallback);
            host.OnApplicationFocusEvent.Register(focusCallback);
            host.OnApplicationPauseEvent.Register(pauseCallback);
            host.OnApplicationQuitEvent.Register(quitCallback);
            mCleanup.Add(() => host.OnGUIEvent.UnRegister(guiCallback));
            mCleanup.Add(() => host.OnApplicationFocusEvent.UnRegister(focusCallback));
            mCleanup.Add(() => host.OnApplicationPauseEvent.UnRegister(pauseCallback));
            mCleanup.Add(() => host.OnApplicationQuitEvent.UnRegister(quitCallback));
            host.SendMessage("OnGUI");
            host.SendMessage("OnApplicationFocus", false);
            host.SendMessage("OnApplicationFocus", true);
            host.SendMessage("OnApplicationPause", true);
            host.SendMessage("OnApplicationPause", false);
            host.SendMessage("OnApplicationQuit");
            Assert.That(gui, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { false, true }, focus);
            CollectionAssert.AreEqual(new[] { true, false }, pause);
            Assert.That(quit, Is.EqualTo(1));
            yield return null;
        }
    }
}
