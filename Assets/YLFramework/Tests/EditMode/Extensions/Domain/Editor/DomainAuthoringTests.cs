using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
            DomainTestAssets.OwnPath(_folder);
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

        [TestCase("Skill")]
        [TestCase("Buff")]
        [TestCase("Mission")]
        public void AuthorTreeCopiesReordersUndoRedoesAndReopensCurrentDataBindings(string module)
        {
            var rootType = module == "Skill" ? typeof(SkillsConfig) : module == "Buff" ? typeof(BuffsConfig) : typeof(MissionsConfig);
            var dataType = module == "Skill" ? typeof(SkillConfigData) : module == "Buff" ? typeof(BuffConfigData) : typeof(MissionConfigData);
            var root = ScriptableObject.CreateInstance(rootType);
            string rootPath = _folder + "/" + module + ".asset";
            AssetDatabase.CreateAsset(root, rootPath);
            var listField = rootType.GetField(module == "Skill" ? "skills" : module == "Buff" ? "buffs" : "missions");
            IList Values() => (IList)listField.GetValue(root);
            var id = dataType.GetField(module == "Buff" ? "buff_id" : "id");
            var controller = typeof(SkillsConfigController).Assembly.GetType("YLFramework." + module + "System." +
                (module == "Skill" ? "Skills" : module == "Buff" ? "Buffs" : "Missions") + "ConfigController");
            controller.GetMethod("Create" + module + "ConfigData", new[] { typeof(Type), rootType }).Invoke(null, new object[] { dataType, root });
            var first = (ScriptableObject)Values()[0];
            string binding = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(first)));
            rootType.GetField("type_script_guid").SetValue(root, binding);
            var author = controller.Assembly.GetType("YLFramework." + module + "System." + module + "Authoring");
            Assert.That(author.GetMethod("GetDataType", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { root }), Is.EqualTo(dataType));
            var icon = dataType.GetField("icon").GetValue(first);
            icon.GetType().GetField("projectName").SetValue(icon, "author-fixture");
            icon.GetType().GetField("iconName").SetValue(icon, "selected-icon");
            if (first is MissionConfigData mission)
            {
                ((MissionsConfig)root).param.Add(new MissionParameter { name = "amount", type = MissionParamType.Float });
                mission.conditions.Add(new MissionCondition { param_name = "amount", compareType = CompareType.GreaterEqual, target = 3 });
                mission.rewards.Add(new RewardInfo { id = 2, count = 4 });
            }
            if (first is BuffConfigData buff)
            {
                var effect = ScriptableObject.CreateInstance<EffectData>(); effect.id = 8;
                AssetDatabase.CreateAsset(effect, _folder + "/effect.asset"); buff.effects.Add(effect);
            }
            var treeType = controller.Assembly.GetType("YLFramework." + module + "System." + module + "ListTree");
            var tree = (UnityEditor.IMGUI.Controls.TreeView)Activator.CreateInstance(treeType, new UnityEditor.IMGUI.Controls.TreeViewState(), root);
            var previousFocus = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<YLFramework.Tests.Extensions.Shared.E0ReorderableTestWindow>();
            window.Tree = tree; window.ShowUtility();
            int copiedId;
            try
            {
                copiedId = (int)treeType.GetMethod("Copy" + module, BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(tree, new[] { module == "Skill" ? (object)(int)id.GetValue(first) : first });
                Assert.That(Values().Count, Is.EqualTo(2)); tree.Reload();
                var basis = treeType.BaseType;
                // 调用实际TreeView准备/放置路径，不冒称物理鼠标拖拽。
                window.Dispatch(EventType.MouseDrag, () => Assert.That(basis.GetMethod("PrepareDrag", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(tree, new object[] { new[] { (int)id.GetValue(first) } }), Is.True));
                var drop = basis.GetMethod("HandleDragAndDrop", BindingFlags.NonPublic | BindingFlags.Instance);
                var args = Activator.CreateInstance(drop.GetParameters()[0].ParameterType);
                void Set(string name, object value)
                {
                    var type = args.GetType(); var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (property != null) property.SetValue(args, value);
                    else type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(args, value);
                }
                Set("parentItem", typeof(UnityEditor.IMGUI.Controls.TreeView).GetProperty("rootItem", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tree));
                Set("insertAtIndex", 2); Set("performDrop", true);
                window.Dispatch(EventType.DragPerform, () => Assert.That(drop.Invoke(tree, new[] { args }), Is.EqualTo(DragAndDropVisualMode.Move)));
                Assert.That((int)id.GetValue(Values()[0]), Is.EqualTo(copiedId));
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.That(Values()[0], Is.SameAs(first));
                Undo.PerformRedo(); Assert.That((int)id.GetValue(Values()[0]), Is.EqualTo(copiedId));
                treeType.GetMethod("Delete" + module, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tree, new object[] { (int)id.GetValue(first) });
                Assert.That(Values().Count, Is.EqualTo(1));
                var copy = (ScriptableObject)Values()[0];
                EditorUtility.SetDirty(copy); EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssetIfDirty(copy); AssetDatabase.SaveAssetIfDirty(root);
            }
            finally { window.Close(); Undo.ClearUndo(root); if (previousFocus) previousFocus.Focus(); }
            AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            root = (ScriptableObject)AssetDatabase.LoadAssetAtPath(rootPath, rootType);
            var reopened = (ScriptableObject)Values()[0];
            Assert.That((int)id.GetValue(reopened), Is.EqualTo(copiedId));
            Assert.That(dataType.GetField("parent").GetValue(reopened), Is.SameAs(root));
            Assert.That(rootType.GetField("type_script_guid").GetValue(root), Is.EqualTo(binding));
            Assert.That(icon.GetType().GetField("iconName").GetValue(dataType.GetField("icon").GetValue(reopened)), Is.EqualTo("selected-icon"));
            if (reopened is MissionConfigData restoredMission)
            {
                Assert.That(restoredMission.conditions.Single().param_name, Is.EqualTo(((MissionsConfig)root).param.Single().name));
                Assert.That(restoredMission.rewards.Single().count, Is.EqualTo(4));
            }
            if (reopened is BuffConfigData restoredBuff) Assert.That(restoredBuff.effects.Single().id, Is.EqualTo(8));
        }

        [Test] public void RootConfigsDoNotStoreWorkbookPaths()
        {
            Assert.That(typeof(SkillsConfig).GetField("excel_path"), Is.Null);
            Assert.That(typeof(BuffsConfig).GetField("excel_path"), Is.Null);
            Assert.That(typeof(MissionsConfig).GetField("excel_path"), Is.Null);
            var root = ScriptableObject.CreateInstance<MissionsConfig>();
            AssetDatabase.CreateAsset(root, _folder + "/missions.asset");
            Assert.That(EditorJsonUtility.ToJson(root), Does.Not.Contain("excel_path"));
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
        internal static void OwnPath(string path)
        {
            if (!SessionState.GetBool("YLFramework.Domain.MainAcceptance.Protection", false)) return;
            var entry = Type.GetType("YLFramework.Tests.DomainMain.DomainMainAcceptance, Assembly-CSharp-Editor", true);
            entry.GetMethod("OwnPath").Invoke(null, new object[] { path });
        }

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
