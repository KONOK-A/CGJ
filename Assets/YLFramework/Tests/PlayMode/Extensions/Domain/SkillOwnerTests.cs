using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YLFramework.SkillSystem;

namespace YLFramework.Tests.Extensions.Domain
{
    public class SkillOwnerTests
    {
        private sealed class Host : IEquippableSkill
        {
            public Dictionary<int, SkillBase> Skills { get; } = new();
            public bool IsCanRelease() => true;
        }
        [UnityEngine.Scripting.Preserve]
        public sealed class ReentrantSkill : SkillBase
        {
            protected override void OnRemove() => SkillManager.RemoveSkills(Releaser, SkillData.id);
        }

        [Test] public void ConfigIsCopiedAndClrHostCloseReturnsSkillAndCallbacks()
        {
            var config = ScriptableObject.CreateInstance<SkillsConfig>();
            var data = ScriptableObject.CreateInstance<SkillConfigData>();
            try
            {
                config.name = "Domain.Skill.Config";
                data.id = 100;
                data.cooling_time = 2;
                data.duration = 1;
                data.skill_class_full_name = typeof(ReentrantSkill).FullName;
                config.skills.Add(data);
                SkillManager.AddConfig(config);
                var firstHost = new Host();
                var skill = SkillManager.AddSkills(firstHost, 100);
                Assert.That(skill.SkillData, Is.Not.SameAs(data));
                skill.SkillData.cooling_time = 99;
                Assert.That(data.cooling_time, Is.EqualTo(2));
                skill.onStartReleaseCallback = _ => SkillManager.CloseHost(firstHost);
                skill.Release();
                Assert.That(firstHost.Skills, Is.Empty);
                Assert.That(skill.Releaser, Is.Null);
                Assert.That(skill.SkillData, Is.Null);
                var secondHost = new Host();
                var next = SkillManager.AddSkills(secondHost, 100);
                Assert.That(next, Is.SameAs(skill));
                Assert.That(next.onStartReleaseCallback, Is.Null);
                Assert.That(next.CoolingTime, Is.EqualTo(2));
                SkillManager.Shutdown();
                Assert.That(secondHost.Skills, Is.Empty);
            }
            finally { SkillManager.Shutdown(); Object.DestroyImmediate(config); Object.DestroyImmediate(data); }
        }
    }
}
