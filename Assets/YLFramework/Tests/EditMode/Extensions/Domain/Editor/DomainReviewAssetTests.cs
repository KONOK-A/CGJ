using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.BuffSystem;
using YLFramework.MissionSystem;
using YLFramework.SkillSystem;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Extensions.Domain
{
    public class DomainReviewAssetTests
    {
        private string _folder;
        [SetUp] public void Setup()
        {
            _folder = "Assets/DomainReviewFixture-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(_folder));
        }
        [TearDown] public void Cleanup() => DomainTestAssets.DeleteFolder(_folder);

        [TestCase("Skill")]
        [TestCase("Buff")]
        [TestCase("Mission")]
        public void EntryRenameAndDeletePreserveRootSiblingAndBorrowedFile(string module)
        {
            var rootType = module == "Skill" ? typeof(SkillsConfig) : module == "Buff" ? typeof(BuffsConfig) : typeof(MissionsConfig);
            var dataType = module == "Skill" ? typeof(SkillConfigData) : module == "Buff" ? typeof(BuffConfigData) : typeof(MissionConfigData);
            var listName = module == "Skill" ? "skills" : module == "Buff" ? "buffs" : "missions";
            var root = ScriptableObject.CreateInstance(rootType);
            var rootPath = _folder + "/root.asset";
            AssetDatabase.CreateAsset(root, rootPath);
            var values = (IList)rootType.GetField(listName).GetValue(root);
            var idField = dataType.GetField(module == "Buff" ? "buff_id" : "id");
            var first = ScriptableObject.CreateInstance(dataType);
            var sibling = ScriptableObject.CreateInstance(dataType);
            var borrowed = ScriptableObject.CreateInstance(dataType);
            var entries = new[] { first, sibling, borrowed };
            for (var i = 0; i < entries.Length; i++)
            {
                idField.SetValue(entries[i], 100 + i);
                dataType.GetField("parent").SetValue(entries[i], root);
                dataType.GetField(module == "Skill" ? "skill_name" : module == "Buff" ? "buff_name" : "mission_name").SetValue(entries[i], "entry-" + i);
                values.Add(entries[i]);
            }
            AssetDatabase.AddObjectToAsset(first, root); AssetDatabase.AddObjectToAsset(sibling, root);
            AssetDatabase.CreateAsset(borrowed, _folder + "/borrowed.asset");
            EditorUtility.SetDirty(root); AssetDatabase.SaveAssetIfDirty(root);
            var name = root.name;
            idField.SetValue(first, 200);
            var author = typeof(SkillsConfigController).Assembly.GetType("YLFramework." + module + "System." + module + "Authoring");
            author.GetMethod("Rename", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { first });
            Assert.That(AssetDatabase.GetAssetPath(root), Is.EqualTo(rootPath));
            Assert.That(root.name, Is.EqualTo(name));
            Assert.That(first.name, Is.EqualTo("200"));
            var treeType = author.Assembly.GetType("YLFramework." + module + "System." + module + "ListTree");
            var state = new UnityEditor.IMGUI.Controls.TreeViewState();
            var tree = Activator.CreateInstance(treeType, state, root);
            try
            {
                var delete = treeType.GetMethod("Delete" + module, BindingFlags.NonPublic | BindingFlags.Instance);
                delete.Invoke(tree, new object[] { 200 });
                Assert.That(AssetDatabase.LoadMainAssetAtPath(rootPath), Is.SameAs(root));
                Assert.That(AssetDatabase.IsSubAsset(sibling), Is.True);
                Assert.That(values.Count, Is.EqualTo(2));
                delete.Invoke(tree, new object[] { 102 });
                Assert.That(AssetDatabase.LoadMainAssetAtPath(_folder + "/borrowed.asset"), Is.SameAs(borrowed));
                Assert.That(values.Count, Is.EqualTo(1));
            }
            finally { ((IDisposable)tree).Dispose(); }
        }

        [TestCase("Skill")]
        [TestCase("Buff")]
        [TestCase("Mission")]
        public void NonemptyMissingDataScriptBindingCannotFallBackToBase(string module)
        {
            var type = module == "Skill" ? typeof(SkillsConfig) : module == "Buff" ? typeof(BuffsConfig) : typeof(MissionsConfig);
            var root = ScriptableObject.CreateInstance(type);
            try
            {
                type.GetField("type_script_guid").SetValue(root, "ffffffffffffffffffffffffffffffff");
                var author = typeof(SkillsConfigController).Assembly.GetType("YLFramework." + module + "System." + module + "Authoring");
                var error = Assert.Throws<TargetInvocationException>(() => author.GetMethod("GetDataType", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { root }));
                Assert.That(error.InnerException, Is.InstanceOf<InvalidOperationException>());
                Assert.That(error.InnerException.Message, Does.Contain("ffffffffffffffffffffffffffffffff"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Skill")]
        [TestCase("Buff")]
        [TestCase("Mission")]
        public void SelectionChangeAndReleaseDropStatefulDefaultDrawers(string module)
        {
            var dataType = module == "Skill" ? typeof(SkillConfigData) : module == "Buff" ? typeof(BuffConfigData) : typeof(MissionConfigData);
            var inspectorType = typeof(SkillsConfigController).Assembly.GetType("YLFramework." + module + "System." + module + "ConfigDataInspector");
            var inspector = ScriptableObject.CreateInstance(inspectorType);
            var first = ScriptableObject.CreateInstance(dataType); var second = ScriptableObject.CreateInstance(dataType);
            try
            {
                var bind = inspectorType.GetMethod("BindSelection", BindingFlags.Instance | BindingFlags.NonPublic);
                var drawers = inspectorType.GetField("drawers", BindingFlags.Instance | BindingFlags.NonPublic);
                bind.Invoke(inspector, new object[] { first });
                var previous = (IDictionary)drawers.GetValue(inspector);
                bind.Invoke(inspector, new object[] { second });
                Assert.That(drawers.GetValue(inspector), Is.Not.SameAs(previous));
                inspectorType.GetMethod("ReleaseSelection", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inspector, null);
                Assert.That(((IDictionary)drawers.GetValue(inspector)).Count, Is.Zero);
            }
            finally { Object.DestroyImmediate(inspector); Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        [Test] public void MissionClipboardDistinguishesTwoSubassetsInOneFile()
        {
            var root = ScriptableObject.CreateInstance<MissionsConfig>();
            AssetDatabase.CreateAsset(root, _folder + "/missions.asset");
            for (var i = 0; i < 2; i++)
            {
                var data = ScriptableObject.CreateInstance<MissionConfigData>();
                data.parent = root; data.id = 100 + i; data.mission_name = "mission-" + i;
                data.rewards.Add(new RewardInfo { id = i + 1, count = 1 });
                AssetDatabase.AddObjectToAsset(data, root); root.missions.Add(data);
            }
            EditorUtility.SetDirty(root); AssetDatabase.SaveAssetIfDirty(root);
            var tree = new MissionListTree(new UnityEditor.IMGUI.Controls.TreeViewState(), root);
            tree.Reload();
            var clipboard = GUIUtility.systemCopyBuffer;
            try
            {
                tree.SetSelection(new[] { 100, 101 });
                typeof(MissionListTree).GetMethod("CopySelection", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tree, null);
                typeof(MissionListTree).GetMethod("PasteSelection", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tree, null);
                Assert.That(root.missions.Count, Is.EqualTo(4));
                Assert.That(root.missions[2].rewards[0].id, Is.EqualTo(1));
                Assert.That(root.missions[3].rewards[0].id, Is.EqualTo(2));
                Assert.That(root.missions[2], Is.Not.SameAs(root.missions[3]));
            }
            finally { GUIUtility.systemCopyBuffer = clipboard; tree.Dispose(); }
        }

        [Test] public void ParameterDuplicateKeepsCustomConfigurationAndResetsRuntime()
        {
            var root = ScriptableObject.CreateInstance<MissionsConfig>();
            AssetDatabase.CreateAsset(root, _folder + "/parameters.asset");
            var source = new MissionParameter { name = "parameter" };
            source.Value = 7;
            typeof(MissionParameter).GetField("custom_data", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(source, new System.Collections.Generic.List<string> { "category" });
            root.param.Add(source);
            var tree = new ParamListTree(new UnityEditor.IMGUI.Controls.TreeViewState(), root);
            try
            {
                typeof(ParamListTree).GetMethod("CopyMissionParam", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tree, new object[] { source });
                Assert.That(root.param.Count, Is.EqualTo(2));
                Assert.That(root.param[1].name, Is.Not.EqualTo(source.name));
                Assert.That(root.param[1].ContainsCustomData("category"), Is.True);
                Assert.That(root.param[1].Value, Is.Zero);
                Assert.That(source.Value, Is.EqualTo(7));
            }
            finally { tree.Dispose(); }
        }
    }
}
