using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Events;
using EventType = YLFramework.Events.EventType;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class EventsLifecyclePlayModeTests
    {
        private readonly List<GameObject> mRoots = new List<GameObject>();
        private Scene mScene;
        private Scene mOtherScene;
        private AsyncOperation mUnload;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var root in mRoots)
            {
                if (root) Object.Destroy(root);
            }
            mRoots.Clear();
            if (mUnload != null) yield return mUnload;
            else if (mScene.IsValid() && mScene.isLoaded) yield return SceneManager.UnloadSceneAsync(mScene);
            if (mOtherScene.IsValid() && mOtherScene.isLoaded) yield return SceneManager.UnloadSceneAsync(mOtherScene);
            mScene = default;
            mOtherScene = default;
            mUnload = null;
            yield return null;
        }

        private GameObject Root()
        {
            var root = new GameObject("Events.Lifecycle.PlayModeTest");
            mRoots.Add(root);
            return root;
        }

        [UnityTest]
        public IEnumerator DestroyBindingWorksWithTheGameObjectOverload()
        {
            var source = new EasyEvent();
            var root = Root();
            var calls = 0;
            source.Register(() => calls++).UnRegisterWhenGameObjectDestroyed(root);
            source.Trigger();
            Object.Destroy(root);
            yield return null;
            source.Trigger();
            Assert.That(calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DestroyBindingWorksWithTheComponentOverload()
        {
            var source = new EasyEvent();
            var root = Root();
            var calls = 0;
            source.Register(() => calls++).UnRegisterWhenGameObjectDestroyed(root.transform);
            Object.Destroy(root);
            yield return null;
            source.Trigger();
            Assert.That(calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisableBindingsSupportBothOverloadsAndRepeatedEnableDisable()
        {
            var source = new EasyEvent();
            var root = Root();
            var calls = 0;
            source.Register(() => calls++).UnRegisterWhenDisabled(root);
            source.Trigger();
            root.SetActive(false);
            source.Trigger();
            root.SetActive(true);
            source.Register(() => calls++).UnRegisterWhenDisabled(root.transform);
            source.Trigger();
            root.SetActive(false);
            source.Trigger();
            Assert.That(calls, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TriggerDeduplicatesTokensAndAllowsRemovingAnOwnedToken()
        {
            var source = new EasyEvent();
            var root = Root();
            var trigger = root.AddComponent<OnDestroyUnRegisterTrigger>();
            var calls = 0;
            var token = source.Register(() => calls++);
            Assert.That(trigger.AddUnRegister(token), Is.SameAs(token));
            trigger.AddUnRegister(token);
            trigger.UnRegister();
            Assert.DoesNotThrow(() => trigger.UnRegister());
            source.Trigger();
            Assert.That(calls, Is.Zero);
            var detached = source.Register(() => calls++);
            trigger.AddUnRegister(detached);
            trigger.RemoveUnRegister(detached);
            Object.Destroy(root);
            yield return null;
            source.Trigger();
            Assert.That(calls, Is.EqualTo(1));
            detached.UnRegister();
        }

        [UnityTest]
        public IEnumerator SceneBindingKeepsTheOriginalAnyUnloadedSceneScope()
        {
            var source = new EasyEvent();
            var calls = 0;
            source.Register(() => calls++).UnRegisterWhenAnySceneUnloaded();
            var trigger = OnCurSceneUnLoadUnRegisterTrigger.Instance;
            Assert.That(OnCurSceneUnLoadUnRegisterTrigger.Instance, Is.SameAs(trigger));
            source.Trigger();
            mScene = SceneManager.CreateScene("Events.Lifecycle.UnloadTest");
            Assert.That(trigger.gameObject.scene, Is.Not.EqualTo(mScene));
            mUnload = SceneManager.UnloadSceneAsync(mScene);
            yield return mUnload;
            source.Trigger();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(trigger != null, Is.True);
        }

        [UnityTest]
        public IEnumerator TriggerAttemptsAllFailuresAndRetainsNewAndFailedResponsibilities()
        {
            var trigger = Root().AddComponent<OnDestroyUnRegisterTrigger>();
            var attempts = 0;
            var succeeded = 0;
            var added = 0;
            var fail = true;
            var firstError = new InvalidOperationException("first cancellation");
            var secondError = new ArgumentException("second cancellation");
            trigger.AddUnRegister(new CustomUnRegister(() =>
            {
                attempts++;
                if (fail) throw firstError;
            }));
            trigger.AddUnRegister(new CustomUnRegister(() =>
            {
                if (fail) throw secondError;
            }));
            trigger.AddUnRegister(new CustomUnRegister(() =>
            {
                succeeded++;
                trigger.AddUnRegister(new CustomUnRegister(() => added++));
                trigger.UnRegister();
            }));
            var error = Assert.Throws<AggregateException>(() => trigger.UnRegister());
            Assert.That(error.InnerExceptions, Is.EquivalentTo(new Exception[] { firstError, secondError }));
            Assert.That(succeeded, Is.EqualTo(1));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(added, Is.Zero);
            fail = false;
            trigger.UnRegister();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(added, Is.EqualTo(1));
            Assert.DoesNotThrow(() => trigger.UnRegister());
            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneTriggerDestructionCancelsPendingSubscriptions()
        {
            var source = new EventChannel();
            source.Register(() => { }).UnRegisterWhenAnySceneUnloaded();
            mScene = SceneManager.CreateScene("Events.Lifecycle.PendingScene");
            source.Register(() => { }).UnRegisterWhenSceneUnloaded(mScene);
            var trigger = OnCurSceneUnLoadUnRegisterTrigger.Instance;
            Object.Destroy(trigger.gameObject);
            yield return null;
            Assert.That(source.ListenerCount, Is.Zero);
            source.Dispose();
        }

        [UnityTest]
        public IEnumerator AdditiveSceneBindingCapturesExplicitOwnerAndKeepsAnySceneDistinct()
        {
            var source = new EventChannel();
            mScene = SceneManager.CreateScene("Events.Lifecycle.Owner");
            mOtherScene = SceneManager.CreateScene("Events.Lifecycle.Other");
            var root = Root();
            SceneManager.MoveGameObjectToScene(root, mScene);
            var ownerCalls = 0;
            var otherCalls = 0;
            var anyCalls = 0;
            source.Register(() => ownerCalls++).UnRegisterWhenSceneUnloaded(root.scene);
            source.Register(() => otherCalls++).UnRegisterWhenSceneUnloaded(mOtherScene);
            source.Register(() => anyCalls++).UnRegisterWhenAnySceneUnloaded();
            SceneManager.MoveGameObjectToScene(root, mOtherScene);
            yield return SceneManager.UnloadSceneAsync(mOtherScene);
            source.Trigger();
            Assert.That(ownerCalls, Is.EqualTo(1));
            Assert.That(otherCalls, Is.Zero);
            Assert.That(anyCalls, Is.Zero);
            mUnload = SceneManager.UnloadSceneAsync(mScene);
            yield return mUnload;
            source.Trigger();
            Assert.That(ownerCalls, Is.EqualTo(1));
            Assert.That(source.ListenerCount, Is.Zero);
            source.Register(() => { }).UnRegisterWhenSceneUnloaded(mScene);
            source.Register(() => { }).UnRegisterWhenSceneUnloaded(default);
            Assert.That(source.ListenerCount, Is.Zero);
            source.Dispose();
        }

        [UnityTest]
        public IEnumerator SceneCleanupKeepsFailuresAndDefersReentrantRegistrations()
        {
            mScene = SceneManager.CreateScene("Events.Lifecycle.ReentrantOwner");
            var trigger = OnCurSceneUnLoadUnRegisterTrigger.Instance;
            var fail = true;
            var attempts = 0;
            var sceneCalls = 0;
            var laterCalls = 0;
            var error = new InvalidOperationException("scene cancellation failed");
            new CustomUnRegister(() =>
            {
                attempts++;
                if (fail) throw error;
            }).UnRegisterWhenSceneUnloaded(mScene);
            new CustomUnRegister(() => sceneCalls++).UnRegisterWhenSceneUnloaded(mScene);
            new CustomUnRegister(() =>
                new CustomUnRegister(() => laterCalls++).UnRegisterWhenSceneUnloaded(mScene))
                .UnRegisterWhenAnySceneUnloaded();
            Assert.That(Assert.Throws<InvalidOperationException>(() => trigger.UnRegister()), Is.SameAs(error));
            Assert.That(sceneCalls, Is.EqualTo(1));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(laterCalls, Is.Zero);
            fail = false;
            trigger.UnRegister();
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(laterCalls, Is.EqualTo(1));
            mUnload = SceneManager.UnloadSceneAsync(mScene);
            yield return mUnload;
            Assert.That(attempts, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RemovingASceneOwnedTokenTransfersResponsibilityWithoutCancellingIt()
        {
            mScene = SceneManager.CreateScene("Events.Lifecycle.DetachedOwner");
            var source = new EventChannel();
            var token = source.Register(() => { });
            var trigger = OnCurSceneUnLoadUnRegisterTrigger.Instance;
            trigger.AddUnRegister(token, mScene);
            trigger.RemoveUnRegister(token);
            mUnload = SceneManager.UnloadSceneAsync(mScene);
            yield return mUnload;
            try { Assert.That(source.ListenerCount, Is.EqualTo(1)); }
            finally { token.UnRegister(); source.Dispose(); }
        }

        [UnityTest]
        public IEnumerator InactiveDisableBindingWaitsForActualDisableAndDeadHostsCancelImmediately()
        {
            var source = new EventChannel();
            var root = Root();
            var component = root.transform;
            root.SetActive(false);
            source.Register(() => { }).UnRegisterWhenDisabled(root);
            Assert.That(source.ListenerCount, Is.EqualTo(1));
            root.SetActive(true);
            root.SetActive(false);
            Assert.That(source.ListenerCount, Is.Zero);
            Object.Destroy(root);
            yield return null;
            source.Register(() => { }).UnRegisterWhenGameObjectDestroyed(root);
            source.Register(() => { }).UnRegisterWhenGameObjectDestroyed(component);
            source.Register(() => { }).UnRegisterWhenDisabled(root);
            source.Register(() => { }).UnRegisterWhenDisabled(component);
            Assert.That(source.ListenerCount, Is.Zero);
            source.Dispose();
        }

        [UnityTest]
        public IEnumerator DisableAndDestroyReportErrorsAfterAttemptingAllResponsibilities()
        {
            var root = Root();
            var trigger = root.AddComponent<OnDisableUnRegisterTrigger>();
            var fail = true;
            var attempts = 0;
            var laterCalls = 0;
            trigger.AddUnRegister(new CustomUnRegister(() =>
            {
                attempts++;
                if (fail) throw new InvalidOperationException("disable cleanup failed");
            }));
            trigger.AddUnRegister(new CustomUnRegister(() => laterCalls++));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: disable cleanup failed"));
            root.SetActive(false);
            Assert.That(laterCalls, Is.EqualTo(1));
            fail = false;
            trigger.UnRegister();
            Assert.That(attempts, Is.EqualTo(2));
            trigger.AddUnRegister(new CustomUnRegister(() => { throw new InvalidOperationException("destroy cleanup failed"); }));
            trigger.AddUnRegister(new CustomUnRegister(() => laterCalls++));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: destroy cleanup failed"));
            Object.Destroy(root);
            yield return null;
            Assert.That(laterCalls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ListenerKeepsUnityEventChannelAndCodeOwnershipDistinct()
        {
            var root = Root();
            var listener = root.AddComponent<EventListener>();
            var order = new List<string>();
            var unityEvent = new UnityEvent<PointerEventData>();
            unityEvent.AddListener(_ => order.Add("unity"));
            var field = typeof(EventListener).GetField("pointerClick", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(listener, unityEvent);
            var oldToken = listener.Register<PointerEventData>(EventType.OnClick, _ => order.Add("old"));
            listener.Register<PointerEventData>(EventType.OnClick, null).UnRegister();
            listener.OnPointerClick(null);
            Assert.That(order, Is.EqualTo(new[] { "old", "unity" }));
            listener.UnRegisterAll();
            Assert.That(field.GetValue(listener), Is.SameAs(unityEvent));
            var token = listener.Register<PointerEventData>(EventType.OnClick, _ => order.Add("new"));
            oldToken.UnRegister();
            order.Clear();
            listener.OnPointerClick(null);
            Assert.That(order, Is.EqualTo(new[] { "new", "unity" }));
            root.SetActive(false);
            root.SetActive(true);
            order.Clear();
            listener.OnPointerClick(null);
            Assert.That(order, Is.EqualTo(new[] { "new", "unity" }));
            token.UnRegister();
            var failing = listener.Register<PointerEventData>(EventType.OnClick, _ => { throw new InvalidOperationException("code failed"); });
            order.Clear();
            Assert.Throws<InvalidOperationException>(() => listener.OnPointerClick(null));
            Assert.That(order, Is.Empty);
            failing.UnRegister();
            listener.UnRegisterAll(true);
            Assert.That(field.GetValue(listener), Is.Null);
            listener.OnPointerClick(null);
            Assert.That(order, Is.Empty);
            unityEvent.Invoke(null);
            Assert.That(order, Is.EqualTo(new[] { "unity" }));
            listener.Register<PointerEventData>(EventType.OnClick, _ => order.Add("destroyed"));
            Object.Destroy(root);
            yield return null;
            Assert.Throws<ObjectDisposedException>(() => listener.Register<PointerEventData>(EventType.OnClick, _ => { }));
        }

        [UnityTest]
        public IEnumerator ListenerRejectsWrongNativePayloadBeforeCreatingACodeChannel()
        {
            var listener = Root().AddComponent<EventListener>();
            Assert.Throws<ArgumentException>(() => listener.Register<int>(EventType.OnClick, _ => { }));
            var calls = 0;
            var token = listener.Register<PointerEventData>(EventType.OnClick, _ => calls++);
            listener.OnPointerClick(null);
            Assert.That(calls, Is.EqualTo(1));
            token.UnRegister();
            yield return null;
        }
    }
}
