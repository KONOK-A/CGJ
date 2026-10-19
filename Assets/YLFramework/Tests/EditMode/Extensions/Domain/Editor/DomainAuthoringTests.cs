using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.BuffSystem;
using YLFramework.MissionSystem;
using YLFramework.SkillSystem;

namespace YLFramework.Tests.Extensions.Domain
{
    public class DomainAuthoringTests
    {
        private string _folder;
        [SetUp] public void Setup()
        {
            _folder = "Assets/DomainFixture-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(_folder));
        }
        [TearDown] public void Cleanup() => DomainTestAssets.DeleteFolder(_folder);

        [TestCase("Skill")]
        [TestCase("Buff")]
        [TestCase("Mission")]
        public void RootDeletionUsesItsOwnGeneratedDirectoryAndPreservesExternalAssets(string module)
        {
            var external = ScriptableObject.CreateInstance<EffectData>();
            AssetDatabase.CreateAsset(external, _folder + "/external.asset");
            ScriptableObject root = module == "Skill" ? ScriptableObject.CreateInstance<SkillsConfig>() : module == "Buff" ? ScriptableObject.CreateInstance<BuffsConfig>() : ScriptableObject.CreateInstance<MissionsConfig>();
            var rootPath = _folder + "/root.asset";
            AssetDatabase.CreateAsset(root, rootPath);
            string generated;
            if (root is SkillsConfig skill)
            {
                SkillsConfigController.CreateSkillConfigData(typeof(SkillConfigData), skill);
                generated = SkillsConfigController.GetSkillConfigDataPath(skill);
            }
            else if (root is BuffsConfig buff)
            {
                BuffsConfigController.CreateBuffConfigData(typeof(BuffConfigData), buff);
                generated = BuffsConfigController.GetBuffConfigDataPath(buff);
                buff.buffs[0].effects.Add(external);
            }
            else
            {
                var mission = (MissionsConfig)root;
                MissionsConfigController.CreateMissionConfigData(typeof(MissionConfigData), mission);
                generated = "Assets/YLFrameworkData/Mission/" + AssetDatabase.AssetPathToGUID(rootPath);
            }
            Assert.That(AssetDatabase.IsValidFolder(generated), Is.True);
            Assert.That(AssetDatabase.DeleteAsset(rootPath), Is.True);
            Assert.That(AssetDatabase.IsValidFolder(generated), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<EffectData>(_folder + "/external.asset"), Is.SameAs(external));
        }

        [Test] public void MissionParameterRenameUpdatesOnlyItsConditionsAndCanUndo()
        {
            var root = ScriptableObject.CreateInstance<MissionsConfig>();
            var data = ScriptableObject.CreateInstance<MissionConfigData>();
            AssetDatabase.CreateAsset(root, _folder + "/missions.asset");
            AssetDatabase.AddObjectToAsset(data, root);
            root.param.Add(new MissionParameter { name = "old" }); root.missions.Add(data);
            data.conditions.Add(new MissionCondition { param_name = "old" });
            AssetDatabase.SaveAssetIfDirty(root);
            var tree = new ParamListTree(new UnityEditor.IMGUI.Controls.TreeViewState(), root);
            tree.Reload();
            try
            {
                tree.RenameParam("old", "new");
                Assert.That(data.conditions[0].param_name, Is.EqualTo("new"));
                Assert.That(root.param[0].name, Is.EqualTo("new"));
                Undo.PerformUndo();
                Assert.That(root.param[0].name, Is.EqualTo("old"));
                Assert.That(data.conditions[0].param_name, Is.EqualTo("old"));
            }
            finally { tree.Dispose(); }
        }
    }

    internal static class DomainTestAssets
    {
        internal static void DeleteFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            // Unity删除父目录不会逐个调用子资产的OnWillDeleteAsset，先删除自己的配表根。
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadMainAssetAtPath(path);
                if (root is SkillsConfig || root is BuffsConfig || root is MissionsConfig) AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.DeleteAsset(folder);
        }
    }
}
