using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.DataAssets;
using YLFramework.DataAssets.Editor;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.DataAssets
{
    public class DataAssetAuthoringTests
    {
        private string folder;
        private string childFolder;
        private ScriptableObjectConfigs root;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/__YLDataAssets_" + Guid.NewGuid().ToString("N");
            DataAcceptanceOwnership.Own(folder);
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            root = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            AssetDatabase.CreateAsset(root, folder + "/Items.asset");
            childFolder = ScriptableObjectConfigsController.GetDataPath(root);
            DataAcceptanceOwnership.Own(childFolder);
        }

        [TearDown]
        public void TearDown()
        {
            if (root)
            {
                foreach (var item in root.configs) if (item) Undo.ClearUndo(item);
                Undo.ClearUndo(root);
            }
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                Console.WriteLine("Preserved failed DataAssets fixture: " + folder + " | " + childFolder);
                return;
            }
            AssetDatabase.DeleteAsset(folder);
            if (AssetDatabase.IsValidFolder(childFolder)) AssetDatabase.DeleteAsset(childFolder);
        }

        [Test]
        public void EmptyRangeMissingSlotsAndSameCountIdMutationDoNotUseStaleCache()
        {
            Assert.That(root.MinID, Is.Zero);
            Assert.That(root.MaxID, Is.Zero);
            var item = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            Assert.That(root.GetScriptableObjectData(1), Is.SameAs(item));
            item.id = 17;
            Assert.That(root.GetScriptableObjectData(1), Is.Null);
            Assert.That(root.GetScriptableObjectData(17), Is.SameAs(item));
            Assert.That(root.MinID, Is.EqualTo(17));
            root.configs[0] = null;
            Assert.That(root.TryGetIDRange(out _, out _), Is.False);
        }

        [Test]
        public void DuplicateFailureCannotPublishPartialCacheAndRepairRestoresQueries()
        {
            var first = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var second = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            second.id = first.id;
            Assert.Throws<InvalidOperationException>(() => root.GetScriptableObjectData(first.id));
            Assert.Throws<InvalidOperationException>(() => root.GetScriptableObjectData(first.id));
            second.id = 8;
            Assert.That(root.GetScriptableObjectData(8), Is.SameAs(second));
            Assert.That(root.MaxID, Is.EqualTo(8));
        }

        [Test]
        public void SameCountAndSameIdReferenceReplacementReturnsCurrentObject()
        {
            var first = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var second = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            root.configs.RemoveAt(1);
            Assert.That(root.GetScriptableObjectData(1), Is.SameAs(first));
            second.id = 1;
            root.configs[0] = second;
            Assert.That(root.GetScriptableObjectData(1), Is.SameAs(second));
        }

        [Test]
        public void CreateCopyAndSaveReopenPreserveConcreteDataParentAndTypeGuid()
        {
            var temporary = ScriptableObject.CreateInstance<DataAssetTestData>();
            var script = MonoScript.FromScriptableObject(temporary);
            Object.DestroyImmediate(temporary);
            ScriptableObjectConfigsController.SetDataType(root, script);
            var guid = root.type_script_guid;
            var first = (DataAssetTestData)ScriptableObjectConfigsController.CreateData(typeof(DataAssetTestData), root);
            first.amount = 23;
            first.config_name = "Potion";
            first.icon.projectName = "items";
            first.icon.iconName = "icons/potion";
            var copy = (DataAssetTestData)ScriptableObjectConfigsController.CopyData(root, first);
            Assert.That(copy.amount, Is.EqualTo(23));
            Assert.That(copy.id, Is.EqualTo(2));
            Assert.That(copy.config_name, Is.EqualTo("Potion (1)"));
            Assert.That(copy.parent, Is.SameAs(root));
            Assert.That(root.GetScriptableObjectData("Potion (1)"), Is.SameAs(copy));
            Assert.That(root.GetIcon(copy.id).TryGetAddress(out var address), Is.True);
            Assert.That(address.ToString(), Is.EqualTo("items:icons/potion"));
            ScriptableObjectConfigsController.Save(root);
            AssetDatabase.ImportAsset(folder + "/Items.asset", ImportAssetOptions.ForceUpdate);
            var reopened = AssetDatabase.LoadAssetAtPath<ScriptableObjectConfigs>(folder + "/Items.asset");
            Assert.That(reopened.type_script_guid, Is.EqualTo(guid));
            Assert.That(reopened.GetDataType(), Is.EqualTo(typeof(DataAssetTestData)));
            Assert.That(((DataAssetTestData)reopened.GetScriptableObjectData(2)).amount, Is.EqualTo(23));
            Assert.That(reopened.GetScriptableObjectData(2).parent, Is.SameAs(reopened));
            Assert.Throws<ArgumentException>(() => ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), reopened));
        }

        [Test]
        public void InvalidTypeBindingIsReportedRatherThanDowngradedToBaseData()
        {
            root.type_script_guid = "ffffffffffffffffffffffffffffffff";
            Assert.That(ScriptableObjectConfigsController.Validate(root).Any(x => x.Message.Contains("type_script_guid")), Is.True);
            Assert.Throws<InvalidOperationException>(() => ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root));
        }

        [Test]
        public void RenameRejectsDuplicateBeforeMutationAndSupportsUndo()
        {
            var first = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var second = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            Assert.Throws<InvalidOperationException>(() => ScriptableObjectConfigsController.RenameData(root, second, first.id, "bad"));
            Assert.That(second.id, Is.EqualTo(2));
            ScriptableObjectConfigsController.RenameData(root, second, 9, "Elixir");
            Assert.That(root.GetName(9), Is.EqualTo("Elixir"));
            Undo.PerformUndo();
            Assert.That(root.GetScriptableObjectData(9), Is.Null);
            Assert.That(root.GetScriptableObjectData(2), Is.SameAs(second));
        }

        [Test]
        public void StableSortPreservesMissingSlotsDuplicateReferencesAndUndo()
        {
            var first = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var second = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            root.configs = new System.Collections.Generic.List<ScriptableObjectData> { second, null, first, first };
            ScriptableObjectConfigsController.Sort(root, DataAssetSort.Id, true);
            Assert.That(root.configs, Is.EqualTo(new[] { first, first, second, null }));
            Undo.PerformUndo();
            Assert.That(root.configs, Is.EqualTo(new[] { second, null, first, first }));
        }

        [Test]
        public void DeleteCancelAndForeignReferencesDoNotDeleteAnyAsset()
        {
            var owned = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var external = ScriptableObject.CreateInstance<ScriptableObjectData>();
            AssetDatabase.CreateAsset(external, folder + "/External.asset");
            root.configs.Add(external);
            Assert.That(ScriptableObjectConfigsController.DeleteData(root, owned, false), Is.False);
            Assert.Throws<InvalidOperationException>(() => ScriptableObjectConfigsController.DeleteData(root, external, true));
            Assert.That(AssetDatabase.LoadAssetAtPath<ScriptableObjectData>(folder + "/External.asset"), Is.SameAs(external));
            Assert.That(root.configs.Count, Is.EqualTo(2));
            ScriptableObjectConfigsController.RemoveReference(root, 1, external);
            Assert.That(root.configs.Count, Is.EqualTo(1));
            Undo.PerformUndo();
            Assert.That(root.configs[1], Is.SameAs(external));
        }

        [Test]
        public void DeleteOwnedChildAndRootNeverCascadeIntoExternalAssets()
        {
            var item = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var path = AssetDatabase.GetAssetPath(item);
            Assert.That(ScriptableObjectConfigsController.DeleteData(root, item, true), Is.True);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.configs, Is.Empty);
            Assert.That(AssetDatabase.LoadAssetAtPath<ScriptableObjectData>(path), Is.Null);
            var external = ScriptableObject.CreateInstance<ScriptableObjectData>();
            AssetDatabase.CreateAsset(external, folder + "/External.asset");
            root.configs.Add(external);
            Assert.That(ScriptableObjectConfigsController.DeleteRoot(root, false), Is.False);
            Assert.That(ScriptableObjectConfigsController.DeleteRoot(root, true), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<ScriptableObjectData>(folder + "/External.asset"), Is.SameAs(external));
        }

        [Test]
        public void SubAssetDeletionOnlyDestroysChildAndCanUndo()
        {
            var child = ScriptableObject.CreateInstance<ScriptableObjectData>();
            child.parent = root;
            child.id = 5;
            AssetDatabase.AddObjectToAsset(child, root);
            root.configs.Add(child);
            Assert.That(ScriptableObjectConfigsController.IsOwned(root, child), Is.True, "Immediate sub-assets must be editable before their first save.");
            Assert.That(ScriptableObjectConfigsController.DeleteData(root, child, true), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<ScriptableObjectConfigs>(folder + "/Items.asset"), Is.SameAs(root));
            Undo.PerformUndo();
            Assert.That(root.configs.Count, Is.EqualTo(1));
            Assert.That(root.configs[0] && root.configs[0].id == 5, Is.True);
        }

        [Test]
        public void DiagnosticsLocateMissingAndDuplicateAndDisappearAfterRepair()
        {
            var first = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var second = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            second.id = first.id;
            root.configs.Add(null);
            var errors = ScriptableObjectConfigsController.Validate(root);
            Assert.That(errors.Any(x => x.Row == 1 && x.Asset == second && x.Message.Contains("duplicate")), Is.True);
            Assert.That(errors.Any(x => x.Row == 2 && x.Message.Contains("Missing")), Is.True);
            second.id = 8;
            ScriptableObjectConfigsController.RemoveReference(root, 2, null);
            Assert.That(ScriptableObjectConfigsController.Validate(root), Is.Empty);
        }

        [Test]
        public void DuplicateBusinessIdsAndMissingRowsKeepDistinctSelectableTreeRows()
        {
            var first = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var second = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            second.id = first.id;
            root.configs.Add(null);
            using var tree = new ScriptableObjectListTree(root, new TreeViewState());
            Assert.That(tree.GetRows().Select(row => row.id), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(tree.GetData(1), Is.SameAs(first));
            Assert.That(tree.GetData(2), Is.SameAs(second));
            Assert.That(tree.GetData(3), Is.Null);
        }

        [Test]
        public void RefreshAfterAnotherWindowReordersKeepsSelectionOnActualObject()
        {
            var first = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var second = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            using var tree = new ScriptableObjectListTree(root, new TreeViewState());
            tree.SetSelection(new[] { 2 });
            ScriptableObjectData selected = null;
            tree.Selected = value => selected = value;
            root.configs = new System.Collections.Generic.List<ScriptableObjectData> { second, first };
            tree.RefreshKeepingSelection();
            Assert.That(tree.GetSelection(), Is.EqualTo(new[] { 1 }));
            Assert.That(selected, Is.SameAs(second));
        }

        [Test]
        public void MovingRootPreservesChildDirectoryAndOwnershipByGuid()
        {
            var item = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var before = ScriptableObjectConfigsController.GetDataPath(root);
            Assert.That(AssetDatabase.MoveAsset(folder + "/Items.asset", folder + "/Moved.asset"), Is.Empty);
            Assert.That(ScriptableObjectConfigsController.GetDataPath(root), Is.EqualTo(before));
            Assert.That(ScriptableObjectConfigsController.IsOwned(root, item), Is.True);
        }

        [Test]
        public void RootDeleteRejectsUnownedSubAssetsBeforeDeletingOwnedChildren()
        {
            var owned = ScriptableObjectConfigsController.CreateData(typeof(ScriptableObjectData), root);
            var path = AssetDatabase.GetAssetPath(owned);
            var unrelated = ScriptableObject.CreateInstance<ScriptableObjectData>();
            AssetDatabase.AddObjectToAsset(unrelated, root);
            Assert.Throws<InvalidOperationException>(() => ScriptableObjectConfigsController.DeleteRoot(root, true));
            Assert.That(AssetDatabase.LoadAssetAtPath<ScriptableObjectData>(path), Is.SameAs(owned));
            Assert.That(root && unrelated, Is.True);
        }
    }
}
