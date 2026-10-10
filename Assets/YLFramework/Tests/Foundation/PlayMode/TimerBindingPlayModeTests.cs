using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Timing;
using Object = UnityEngine.Object;

namespace YLFramework.Foundation.Tests
{
    public class TimerBindingPlayModeTests
    {
        private GameObject owner;
        private GameObject replacement;
        private TimerScheduler scheduler;

        [SetUp]
        public void SetUp()
        {
            scheduler = new TimerScheduler(0, 0, 0);
            owner = new GameObject("YLTimer.BindingPlayOwner");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            scheduler.Dispose();
            if (owner) Object.Destroy(owner);
            if (replacement) Object.Destroy(replacement);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NeverActivatedPausedOwnerIsCancelledOnNextAdvanceAfterDestroy()
        {
            owner.SetActive(false);
            var cancels = 0;
            var handle = scheduler.Delay(100, onCancelled: _ => cancels++);
            handle.BindTo(owner, TimerGameObjectLifetime.CancelOnDestroy);
            handle.Pause();
            Object.Destroy(owner);
            yield return null;
            scheduler.Advance(0, 0, 1);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Cancelled));
            Assert.That(cancels, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableCancelsPausedRunButDestroyOnlyRunContinues()
        {
            var stopped = scheduler.Delay(100);
            var survives = scheduler.Delay(0);
            stopped.BindTo(owner);
            survives.BindTo(owner, TimerGameObjectLifetime.CancelOnDestroy);
            stopped.Pause();
            owner.SetActive(false);
            Assert.That(stopped.Status, Is.EqualTo(TimerState.Cancelled));
            Assert.That(survives.IsActive, Is.True);
            yield return null;
            scheduler.Advance(0, 0, 1);
            Assert.That(survives.Status, Is.EqualTo(TimerState.Completed));
        }

        [UnityTest]
        public IEnumerator CallbackRebindAndRestartIsNotCancelledByOldListenerSnapshot()
        {
            replacement = new GameObject("YLTimer.BindingReplacement");
            using var slot = new ReusableTimer(scheduler);
            TimerHandle next = default;
            var calls = 0;
            var handle = slot.Start(10, onCancelled: _ =>
            {
                next = slot.Start(0, () => calls++);
                next.BindTo(replacement);
            });
            handle.BindTo(owner);
            owner.SetActive(false);
            yield return null;
            scheduler.Advance(0, 0, 1);
            Assert.That(handle.Status, Is.EqualTo(TimerState.Invalidated));
            Assert.That(next.Status, Is.EqualTo(TimerState.Completed));
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
