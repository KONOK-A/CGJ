using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.EquipmentSystem;

namespace YLFramework.Tests.Extensions.Equipment
{
    public class EquipmentAuthoringTests
    {
        private EquipmentsConfig root;
        private string folder, entries;
        private static Type Controller => typeof(YLFramework.Core.Editor.SplitWindow).Assembly.GetType("YLFramework.EquipmentSystem.EquipmentsConfigController");
        private static object Call(string name, params object[] args)
        {
            Assert.That(Controller, Is.Not.Null, "YL equipment authoring has not been migrated.");
            var method = Controller.GetMethods(BindingFlags.Static | BindingFlags.Public).Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p,i) => p.ParameterType.IsInstanceOfType(args[i])).All(v => v));
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        [SetUp]
        public void SetUp()
        {
            Assert.That(Controller, Is.Not.Null, "YL equipment authoring has not been migrated.");
            var parent = "Assets/YLFramework/Tests/Fixtures/Extensions";
            folder = parent + "/EqAuthor-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            root = ScriptableObject.CreateInstance<EquipmentsConfig>(); root.start_id = 1000;
            AssetDatabase.CreateAsset(root, folder + "/Root.asset");
            entries = "Assets/YLFrameworkData/Equipment/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root));
            Assert.That(AssetDatabase.IsValidFolder(entries), Is.False);
        }
        [TearDown]
        public void TearDown()
        {
            if (root) root.Equipments.Clear();
            if (entries != null && AssetDatabase.IsValidFolder(entries)) AssetDatabase.DeleteAsset(entries);
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void CreateAndCopyKeepDerivedFieldsWithoutSavingUnrelatedDirtyAsset()
        {
            var unrelated = ScriptableObject.CreateInstance<EquipmentDerivedData>();
            AssetDatabase.CreateAsset(unrelated, folder + "/Unrelated.asset");
            var bytes = File.ReadAllBytes(folder + "/Unrelated.asset");
            unrelated.bonus = 90; EditorUtility.SetDirty(unrelated);
            var id = (int)Call("CreateEquipmentConfigData", typeof(EquipmentDerivedData), root);
            Assert.That(id, Is.EqualTo(1001));
            var first = root.GetEquipmentConfigData(id) as EquipmentDerivedData;
            Assert.That(first.bonus, Is.EqualTo(17)); first.bonus = 33;
            var copyId = (int)Call("CopyEquipmentConfigData", first, root);
            var copy = root.GetEquipmentConfigData(copyId) as EquipmentDerivedData;
            Assert.That(copyId, Is.EqualTo(1002)); Assert.That(copy.bonus, Is.EqualTo(33)); Assert.That(copy.parent, Is.SameAs(root));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(copy)), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first))));
            Assert.That(EditorUtility.IsDirty(unrelated), Is.True); CollectionAssert.AreEqual(bytes, File.ReadAllBytes(folder + "/Unrelated.asset"));
        }

        [Test]
        public void DeleteExternalReferenceDoesNotDeleteItsFileOrOtherSubassets()
        {
            var external = ScriptableObject.CreateInstance<EquipmentConfigData>(); external.id = 1010;
            var marker = ScriptableObject.CreateInstance<EquipmentDerivedData>();
            var path = folder + "/External.asset";
            AssetDatabase.CreateAsset(external, path); AssetDatabase.AddObjectToAsset(marker, external); AssetDatabase.SaveAssetIfDirty(external);
            root.Equipments.Add(external);
            var bytes = File.ReadAllBytes(path);
            Call("DeleteEquipmentConfigData", external, root);
            Assert.That(root.Equipments, Is.Empty); Assert.That(external && marker, Is.True);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
        }

        [Test]
        public void ExplicitRefreshFindsNewRowsInNonEmptyOwnerFolder()
        {
            Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            var extra = ScriptableObject.CreateInstance<EquipmentConfigData>(); extra.id = 1008; extra.parent = root;
            AssetDatabase.CreateAsset(extra, entries + "/Extra.asset");
            Assert.That(root.Equipments.Count, Is.EqualTo(1));
            Call("RefreshEntries", root);
            Assert.That(root.GetEquipmentConfigData(1008), Is.SameAs(extra)); Assert.That(root.Equipments.Count, Is.EqualTo(2));
        }

        [Test]
        public void InvalidDataBindingIsDiagnosedAndIdEditUndoRefreshesLookup()
        {
            root.type_script_guid = "invalid-guid";
            Assert.That(Assert.Throws<InvalidOperationException>(() => Call("GetDataType", root)).Message, Does.Contain("invalid-guid"));
            root.type_script_guid = "";
            Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            var data = root.GetEquipmentConfigData(1001);
            Undo.IncrementCurrentGroup();
            using (var serialized = new SerializedObject(data))
            {
                serialized.FindProperty("id").intValue = 1005; serialized.ApplyModifiedProperties();
            }
            Undo.FlushUndoRecordObjects();
            Assert.That(root.GetEquipmentConfigData(1005), Is.SameAs(data));
            Undo.PerformUndo();
            Assert.That(root.GetEquipmentConfigData(1001), Is.SameAs(data)); Assert.That(root.GetEquipmentConfigData(1005), Is.Null);
        }

        [Test]
        public void WindowsOwnSeparateTreeAndSelectionState()
        {
            Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            var type = Controller.Assembly.GetType("YLFramework.EquipmentSystem.ConfigsWindow");
            var a = (EditorWindow)ScriptableObject.CreateInstance(type);
            var b = (EditorWindow)ScriptableObject.CreateInstance(type);
            try
            {
                type.GetMethod("SetCurrentConfigs").Invoke(a, new object[] { root });
                type.GetMethod("SetCurrentConfigs").Invoke(b, new object[] { root });
                type.GetMethod("SetSelection").Invoke(a, new object[] { 1001 });
                var field = type.GetField("equipmentListTree", BindingFlags.NonPublic | BindingFlags.Instance);
                var first = field.GetValue(a); var second = field.GetValue(b);
                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.GetType().GetProperty("Selected").GetValue(first), Is.SameAs(root.GetEquipmentConfigData(1001)));
                Assert.That(second.GetType().GetProperty("Selected").GetValue(second), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }

        [Test]
        public void DeletingOwnedEntrySupportsUndoRedoWithoutLosingGuid()
        {
            var id = (int)Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            var row = root.GetEquipmentConfigData(id); var path = AssetDatabase.GetAssetPath(row); var guid = AssetDatabase.AssetPathToGUID(path);
            Undo.IncrementCurrentGroup();
            Call("DeleteEquipmentConfigData", row, root); Undo.FlushUndoRecordObjects();
            Assert.That(root.Equipments, Is.Empty);
            Undo.PerformUndo();
            var restored = AssetDatabase.LoadAssetAtPath<EquipmentConfigData>(path);
            Assert.That(restored, Is.Not.Null); Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(root.GetEquipmentConfigData(id), Is.SameAs(restored));
            Undo.PerformRedo(); Assert.That(root.Equipments, Is.Empty); Assert.That(File.Exists(path), Is.False);
        }

        [Test]
        public void TreeSelectionFollowsObjectAcrossReorderAndUndo()
        {
            var id = (int)Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            var selected = root.GetEquipmentConfigData(id);
            using var tree = new EquipmentListTree(new TreeViewState(), root); tree.SelectEquipment(id);
            Undo.IncrementCurrentGroup(); Undo.RecordObject(root, "Reorder fixture");
            root.Equipments.Reverse(); EditorUtility.SetDirty(root); Undo.FlushUndoRecordObjects(); tree.Reload();
            Assert.That(tree.Selected, Is.SameAs(selected));
            Undo.PerformUndo(); Assert.That(tree.Selected, Is.SameAs(selected));
        }

        [Test]
        public void OldDeletionDoesNotTouchReplacementFileDuringUnrelatedUndo()
        {
            var id = (int)Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            var row = root.GetEquipmentConfigData(id); var path = AssetDatabase.GetAssetPath(row);
            Call("DeleteEquipmentConfigData", row, root);
            var replacement = ScriptableObject.CreateInstance<EquipmentConfigData>();
            AssetDatabase.CreateAsset(replacement, path);
            var guid = AssetDatabase.AssetPathToGUID(path); var bytes = File.ReadAllBytes(path);
            var unrelated = ScriptableObject.CreateInstance<EquipmentDerivedData>();
            try
            {
                Undo.IncrementCurrentGroup(); Undo.RecordObject(unrelated, "Unrelated fixture edit"); unrelated.bonus = 77; Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
            }
            finally { UnityEngine.Object.DestroyImmediate(unrelated); }
        }

        [Test]
        public void SameObjectMetadataChangesRefreshTreeLabelsAndSearch()
        {
            var id = (int)Call("CreateEquipmentConfigData", typeof(EquipmentConfigData), root);
            var row = root.GetEquipmentConfigData(id);
            var window = ScriptableObject.CreateInstance<ConfigsWindow>();
            try
            {
                window.position = new Rect(80, 80, 900, 600); window.SetCurrentConfigs(root); window.Show();
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                row.equipment_name = "UpdatedEquipmentMetadata";
                var tree = (EquipmentListTree)typeof(ConfigsWindow).GetField("equipmentListTree", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.That(tree.GetRows().Any(item => item.displayName.Contains("UpdatedEquipmentMetadata")), Is.True);
                tree.searchString = "UpdatedEquipmentMetadata";
                Assert.That(tree.GetRows().Count, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }
    }
}
