using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using YLFramework.Events;
using YLFramework.SkillSystem;

namespace YLFramework.Tests.Extensions.Domain
{
    public class SkillLifecycleTests
    {
        private sealed class Host : IEquippableSkill
        {
            public Dictionary<int, SkillBase> Skills { get; } = new();
            public bool IsCanRelease() => true;
        }
        public sealed class Probe : SkillBase
        {
            public int Updates, Completes, Interrupts;
            protected override void OnUpdate() => Updates++;
            protected override void OnReleaseComplete() => Completes++;
            protected override void OnInterrupt() => Interrupts++;
        }
        private static void Advance(SkillBase skill, float delta) => typeof(SkillBase).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skill, new object[] { delta });

        [Test] public void ReleaseCompletionThenCooldownStaysAtOneAndCompletesOnce()
        {
            var skill = new Probe { Releaser = new Host(), Duration = 1, CoolingTime = 2 };
            var completed = 0;
            skill.onCooldownCompleteCallback = _ => completed++;
            Assert.That(skill.Release(), Is.EqualTo(ReleaseSkillStatus.Success));
            Advance(skill, 1);
            Assert.That(skill.Completes, Is.EqualTo(1));
            Advance(skill, 1);
            Assert.That(skill.CooldownProgress, Is.EqualTo(0.5f));
            Advance(skill, 1);
            Advance(skill, 10);
            Assert.That(skill.CooldownProgress, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test] public void CancellationKeepsCooldownAndInterruptCompletesExactlyOnce()
        {
            var skill = new Probe { Releaser = new Host(), Duration = 5, CoolingTime = 2, ActiveCancellation = true };
            skill.Release();
            skill.Cancel();
            skill.Interrupt();
            Assert.That(skill.IsReleasing, Is.False);
            Assert.That(skill.IsCooldown, Is.False);
            Assert.That(skill.Interrupts, Is.EqualTo(1));
            Assert.That(skill.Completes, Is.EqualTo(1));
        }

        [Test] public void RemovingDuringProgressDoesNotContinueOldLeaseCallbacks()
        {
            var skill = new Probe { Releaser = new Host(), Duration = 1 };
            skill.onReleasingCallback = (_, __) => typeof(SkillBase).GetMethod("ClearRuntime", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skill, null);
            skill.Release();
            Advance(skill, 2);
            Assert.That(skill.Updates, Is.Zero);
            Assert.That(skill.Completes, Is.Zero);
            Assert.That(skill.Releaser, Is.Null);
            Assert.That(skill.onReleasingCallback, Is.Null);
        }
        [Test] public void SkillMustOfferRealReleaseAndShutdownBoundaries()
        {
            var assembly = typeof(EventChannel).Assembly;
            var skill = assembly.GetType("YLFramework.SkillSystem.SkillBase");
            Assert.That(skill, Is.Not.Null, "领域释放执行器尚未承接。");
            Assert.That(skill.GetMethod("Release"), Is.Not.Null);
            var manager = assembly.GetType("YLFramework.SkillSystem.SkillManager");
            Assert.That(manager, Is.Not.Null, "宿主/池/三相位责任尚未接入。");
            Assert.That(manager.GetMethod("Shutdown"), Is.Not.Null);
        }
    }
}
