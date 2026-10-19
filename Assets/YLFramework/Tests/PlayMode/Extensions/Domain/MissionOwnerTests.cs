using NUnit.Framework;
using UnityEngine;
using YLFramework.MissionSystem;

namespace YLFramework.Tests.Extensions.Domain
{
    public class MissionOwnerTests
    {
        [Test] public void RuntimeConditionsRewardsResetAndReentrantRemovalDoNotModifyAssets()
        {
            var root = ScriptableObject.CreateInstance<MissionsConfig>();
            var data = ScriptableObject.CreateInstance<MissionConfigData>();
            var parameter = new MissionParameter { name = "amount", type = MissionParamType.Float };
            var condition = new MissionCondition { param_name = "amount", target = 3, compareType = CompareType.Equals };
            try
            {
                root.name = "Domain.Mission.Config";
                root.param.Add(parameter);
                data.id = 100;
                data.mission_type_class_fullname = typeof(MissionBase).FullName;
                data.conditions.Add(condition);
                data.rewards.Add(new RewardInfo { id = 1, count = 5 });
                root.missions.Add(data);
                MissionManager.AddConfig(root);
                var mission = MissionManager.GetMission(root.name, 100);
                Assert.That(mission.MissionConfig, Is.Not.SameAs(data));
                Assert.That(mission.MissionConfig.conditions[0], Is.Not.SameAs(condition));
                mission.MissionConfig.rewards[0].count = 99;
                Assert.That(data.rewards[0].count, Is.EqualTo(5));
                mission.State = MissionState.InProgress;
                MissionManager.SetFloat(root.name, "amount", 2);
                Assert.That(mission.Progress, Is.EqualTo(0), "Equals条件只有0或1进度。");
                Assert.That(parameter.Value, Is.Zero);
                MissionManager.Reset(root.name);
                Assert.That(mission.State, Is.EqualTo(MissionState.NotStarted));
                Assert.That(MissionManager.GetFloat(root.name, "amount"), Is.Zero);
                var runtimeParameter = MissionManager.GetAllParameters(root.name)[0];
                var callbacks = 0;
                mission.State = MissionState.InProgress;
                mission.onStatusChange = current => { callbacks++; MissionManager.RemoveConfig(root.name); };
                MissionManager.SetFloat(root.name, "amount", 3);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(MissionManager.GetConfig(root.name), Is.Null);
                Assert.That(mission.MissionConfig, Is.Null);
                runtimeParameter.Value = 10;
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(parameter.Value, Is.Zero);
                Assert.That(condition.Param, Is.Null);
            }
            finally { MissionManager.Shutdown(); Object.DestroyImmediate(root); Object.DestroyImmediate(data); }
        }
    }
}
