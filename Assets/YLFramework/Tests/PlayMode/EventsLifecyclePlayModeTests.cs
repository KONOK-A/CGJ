using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Events;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class EventsLifecyclePlayModeTests
    {
        private readonly List<GameObject> mRoots = new List<GameObject>();
        private Scene mScene;
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
            source.Register(() => calls++).UnRegisterWhenCurrentSceneUnloaded();
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
    }
}
