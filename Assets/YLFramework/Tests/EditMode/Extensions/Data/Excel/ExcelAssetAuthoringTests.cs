using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Excel;
using Object = UnityEngine.Object;
using Owner = YLFramework.Tests.Extensions.Data.ExcelAssetOwnerFixture;
using AssetRow = YLFramework.Tests.Extensions.Data.ExcelAssetRowFixture;

namespace YLFramework.Tests.Extensions.Data
{
    /// <summary>必须在主Unity运行；离线runner仅编译本类，不执行AssetDatabase或GUI。</summary>
    [TestFixture]
    public class ExcelAssetAuthoringTests
    {
        private string assetFolder;
        private string diskFolder;
        private Owner owner;
        public sealed class ReferenceRow
        {
            [ExcelScriptableObjectID] public int Id;
            public Object Reference;
        }
        [SetUp] public void SetUp()
        {
            string name = "YLExcel-" + Guid.NewGuid().ToString("N");
            assetFolder = "Assets/" + name;
            AssetDatabase.CreateFolder("Assets", name);
            diskFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/Extensions.Data.Excel/Unity", name);
            Directory.CreateDirectory(diskFolder);
            owner = ScriptableObject.CreateInstance<Owner>();
            AssetDatabase.CreateAsset(owner, assetFolder + "/Owner.asset");
            Add(1, "old");
            Add(9, "remove");
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssetIfDirty(owner);
        }
        [TearDown] public void TearDown()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().FullName == "YLFramework.Excel.Editor.ExcelPreviewWindow" && w.titleContent.text == windowTitle)) window.Close();
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                Console.WriteLine("Preserved failed Excel asset fixture: " + assetFolder + " | " + diskFolder);
                return;
            }
            if (!string.IsNullOrEmpty(assetFolder)) AssetDatabase.DeleteAsset(assetFolder);
        }
        private const string windowTitle = "YLExcel-Test-Preview";
        private AssetRow Add(int id, string text)
        {
            var row = ScriptableObject.CreateInstance<AssetRow>();
            row.Id = id;
            row.Text = text;
            row.name = "Row-" + id;
            row.Parent = owner;
            AssetDatabase.AddObjectToAsset(row, owner);
            owner.Rows.Add(row);
            return row;
        }
        private static void Export(List<AssetRow> rows, string path) => ExcelAuthoringTests.Call("ScriptableObjectToExcel", typeof(AssetRow), rows, path, "Sheet1");
        private object Preview(string path, string folder = null, Action<AssetRow> initialize = null) => ExcelAuthoringTests.Call("PreviewScriptableObjects", typeof(AssetRow), path, owner.Rows, owner, folder, null, initialize);
        private byte[] OwnerBytes() => File.ReadAllBytes(assetFolder + "/Owner.asset");
        private string WriteNewWorkbook()
        {
            string path = Path.Combine(diskFolder, "data.xlsx");
            var changed = Object.Instantiate(owner.Rows[0]);
            var added = ScriptableObject.CreateInstance<AssetRow>();
            try
            {
                changed.Text = "updated";
                changed.State = ExcelAssetState.On;
                changed.Numbers = new List<int> { 2, 5 };
                changed.Reference = owner.Rows[0];
                added.Id = 2;
                added.Text = "added";
                Export(new List<AssetRow> { changed, added }, path);
            }
            finally { Object.DestroyImmediate(changed); Object.DestroyImmediate(added); }
            return path;
        }

        [Test] public void PreviewAndCancelPreserveAssetBytesAndReferences()
        {
            string path = WriteNewWorkbook();
            byte[] before = OwnerBytes();
            var rows = owner.Rows.ToArray();
            using (var plan = (IDisposable)Preview(path))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Empty);
                Assert.That(OwnerBytes(), Is.EqualTo(before));
                Assert.That(ExcelAuthoringTests.Commit(plan, false), Is.EqualTo("Cancelled"));
            }
            Assert.That(OwnerBytes(), Is.EqualTo(before));
            Assert.That(owner.Rows, Is.EqualTo(rows));
            Assert.That(owner.Rows[0].Text, Is.EqualTo("old"));
        }
        [Test] public void ConfirmUpdatesInPlaceAndSavesNewSubassetAndTypedGuidReference()
        {
            // 参照资产不参与待删除列表，验证GUID:localFileID而非同GUID主资产回退。
            var reference = ScriptableObject.CreateInstance<AssetRow>();
            reference.Id = 42;
            AssetDatabase.AddObjectToAsset(reference, owner);
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssetIfDirty(owner);
            var old = owner.Rows[0];
            var changed = Object.Instantiate(old);
            var added = ScriptableObject.CreateInstance<AssetRow>();
            string path = Path.Combine(diskFolder, "confirmed.xlsx");
            try
            {
                changed.Text = "updated";
                changed.State = ExcelAssetState.On;
                changed.Reference = reference;
                changed.Numbers = new List<int> { 2, 5 };
                added.Id = 2;
                Export(new List<AssetRow> { changed, added }, path);
            }
            finally { Object.DestroyImmediate(changed); Object.DestroyImmediate(added); }
            using (var plan = (IDisposable)Preview(path, initialize: row => row.Parent = owner))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Empty);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Applied"));
            }
            Assert.That(owner.Rows[0], Is.SameAs(old));
            Assert.That(old.Text, Is.EqualTo("updated"));
            Assert.That(old.name, Is.EqualTo("Row-1"));
            Assert.That(old.State, Is.EqualTo(ExcelAssetState.On));
            Assert.That(old.Numbers, Is.EqualTo(new[] { 2, 5 }));
            Assert.That(old.Reference, Is.SameAs(reference));
            Assert.That(owner.Rows.Select(r => r.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(owner.Rows.All(r => r.Parent == owner), Is.True);
            AssetDatabase.ImportAsset(assetFolder + "/Owner.asset", ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<Owner>(assetFolder + "/Owner.asset");
            Assert.That(reloaded.Rows.Select(r => r.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(reloaded.Rows[0].Reference, Is.SameAs(reference));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(assetFolder + "/Owner.asset").OfType<AssetRow>().Select(r => r.Id), Is.EquivalentTo(new[] { 1, 2, 42 }));
        }
        [Test] public void MissingGuidAndUnavailableTypeBlockAllAssetChanges()
        {
            string path = Path.Combine(diskFolder, "invalid-guid.xlsx");
            ExcelAuthoringTests.WriteWorkbook(path, new[] { "Id", "Reference" }, new[] { "int", "objectreference" }, new object[] { 1, "ffffffffffffffffffffffffffffffff" });
            byte[] before = OwnerBytes();
            using (var plan = (IDisposable)Preview(path))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan).Any(e => e.Contains("B4") && e.Contains("GUID")), Is.True);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
            }
            string unavailable = Path.Combine(diskFolder, "unavailable-type.xlsx");
            ExcelAuthoringTests.WriteWorkbook(unavailable, new[] { "DATA_TYPE_FULL_CLASS_NAME", "Id" }, new[] { "string", "int" }, new object[] { "Missing.Type", 1 });
            using (var plan = (IDisposable)Preview(unavailable))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan).Any(e => e.Contains("Missing.Type")), Is.True);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
            }
            Assert.That(OwnerBytes(), Is.EqualTo(before));
            Assert.That(owner.Rows.Select(r => r.Id), Is.EqualTo(new[] { 1, 9 }));
        }
        [Test] public void DomainInitializationFailureAndDirtyOwnerNeverWriteAssets()
        {
            string path = WriteNewWorkbook();
            byte[] before = OwnerBytes();
            using (var plan = (IDisposable)Preview(path, initialize: row => throw new InvalidOperationException("domain binding rejected")))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan).Any(e => e.Contains("domain binding rejected")), Is.True);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
            }
            owner.Rows[0].Text = "unsaved user edit";
            EditorUtility.SetDirty(owner.Rows[0]);
            using (var plan = (IDisposable)Preview(path))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan).Any(e => e.Contains("Save the item")), Is.True);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
            }
            Assert.That(owner.Rows[0].Text, Is.EqualTo("unsaved user edit"));
            Assert.That(OwnerBytes(), Is.EqualTo(before));
        }
        [Test] public void ClosingRealPreviewWindowCancelsAndReleasesStagedObjects()
        {
            string path = WriteNewWorkbook();
            byte[] before = OwnerBytes();
            int originalCount = Resources.FindObjectsOfTypeAll<AssetRow>().Length;
            var plan = Preview(path);
            var commands = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("YLFramework.Excel.Editor.ExcelAuthoringCommands")).Single(t => t != null);
            var window = (EditorWindow)commands.GetMethod("ShowPreview").Invoke(null, new[] { plan });
            window.titleContent = new GUIContent(windowTitle);
            window.Repaint();
            window.Close();
            Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Cancelled"));
            Assert.That(OwnerBytes(), Is.EqualTo(before));
            Assert.That(Resources.FindObjectsOfTypeAll<AssetRow>().Length, Is.EqualTo(originalCount));
        }
        private string UseStandaloneRows()
        {
            AssetDatabase.CreateFolder(assetFolder, "Entries");
            string folder = assetFolder + "/Entries";
            var rows = owner.Rows.Select(Object.Instantiate).ToArray();
            owner.Rows.Clear();
            foreach (var row in rows)
            {
                row.name = "Standalone-" + row.Id;
                AssetDatabase.CreateAsset(row, folder + "/" + row.Id + ".asset");
                owner.Rows.Add(row);
            }
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssetIfDirty(owner);
            return folder;
        }
        [Test] public void ExplicitOwnedFolderUpdatesAddsDeletesStandaloneAssetsAndKeepsGuid()
        {
            string folder = UseStandaloneRows();
            string guid = AssetDatabase.AssetPathToGUID(folder + "/1.asset");
            var changed = Object.Instantiate(owner.Rows[0]);
            var added = ScriptableObject.CreateInstance<AssetRow>();
            string path = Path.Combine(diskFolder, "standalone.xlsx");
            try
            {
                changed.Text = "standalone updated";
                added.Id = 2;
                Export(new List<AssetRow> { changed, added }, path);
            }
            finally { Object.DestroyImmediate(changed); Object.DestroyImmediate(added); }
            using (var plan = (IDisposable)Preview(path, folder, row => row.Parent = owner))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Empty);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Applied"));
            }
            Assert.That(AssetDatabase.AssetPathToGUID(folder + "/1.asset"), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<AssetRow>(folder + "/1.asset").Text, Is.EqualTo("standalone updated"));
            Assert.That(AssetDatabase.LoadAssetAtPath<AssetRow>(folder + "/2.asset").Parent, Is.SameAs(owner));
            Assert.That(File.Exists(folder + "/9.asset"), Is.False);
            Assert.That(owner.Rows.Select(r => r.Id), Is.EqualTo(new[] { 1, 2 }));
        }
        [Test] public void MainAssetsOutsideExplicitFolderAreNeverUpdatedOrDeleted()
        {
            string folder = UseStandaloneRows();
            string path = WriteNewWorkbook();
            byte[] rootBefore = OwnerBytes();
            byte[] itemBefore = File.ReadAllBytes(folder + "/1.asset");
            using (var plan = (IDisposable)Preview(path, assetFolder))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan).Any(e => e.Contains("ownership boundary")), Is.True);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
            }
            Assert.That(OwnerBytes(), Is.EqualTo(rootBefore));
            Assert.That(File.ReadAllBytes(folder + "/1.asset"), Is.EqualTo(itemBefore));
            Assert.That(File.Exists(folder + "/9.asset"), Is.True);
        }
        [Test] public void ReferenceToPendingRemovalIsRejectedBeforeAnyAssetWrite()
        {
            string path = Path.Combine(diskFolder, "deleted-reference.xlsx");
            var changed = Object.Instantiate(owner.Rows[0]);
            try
            {
                changed.Reference = owner.Rows[1];
                Export(new List<AssetRow> { changed }, path);
            }
            finally { Object.DestroyImmediate(changed); }
            byte[] before = OwnerBytes();
            using (var plan = (IDisposable)Preview(path))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan).Any(e => e.Contains("Reference") && e.Contains("deletion")), Is.True);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
            }
            Assert.That(OwnerBytes(), Is.EqualTo(before));
            Assert.That(owner.Rows.Select(r => r.Id), Is.EqualTo(new[] { 1, 9 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeletingNewReferenceAfterPreviewRejectsCommitAndPreservesOriginalTarget(bool plainList)
        {
            string referencePath = assetFolder + "/NewReference.asset";
            var reference = ScriptableObject.CreateInstance<AssetRow>();
            reference.Id = 42;
            AssetDatabase.CreateAsset(reference, referencePath);
            string path = Path.Combine(diskFolder, "stale-reference.xlsx");
            byte[] before = OwnerBytes();
            var original = new ReferenceRow { Id = 1 };
            var values = new List<ReferenceRow> { original };
            object preview;
            if (plainList)
            {
                ExcelAuthoringTests.Call("ListToExcel", typeof(ReferenceRow), new List<ReferenceRow> { new ReferenceRow { Id = 1, Reference = reference } }, path, "Sheet1");
                preview = ExcelAuthoringTests.Call("PreviewList", typeof(ReferenceRow), path, values, null);
            }
            else
            {
                var changed = Object.Instantiate(owner.Rows[0]);
                try
                {
                    changed.Reference = reference;
                    Export(new List<AssetRow> { changed, owner.Rows[1] }, path);
                }
                finally { Object.DestroyImmediate(changed); }
                preview = Preview(path, initialize: row => row.Parent = owner);
            }
            using (var plan = (IDisposable)preview)
            {
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Empty);
                Assert.That(AssetDatabase.DeleteAsset(referencePath), Is.True);
                Assert.That(OwnerBytes(), Is.EqualTo(before));
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Not.Empty);
            }
            Assert.That(OwnerBytes(), Is.EqualTo(before));
            Assert.That(owner.Rows.Select(r => r.Id), Is.EqualTo(new[] { 1, 9 }));
            Assert.That(owner.Rows[0].Reference, Is.Null);
            Assert.That(values[0], Is.SameAs(original));
            Assert.That(values[0].Reference, Is.Null);
        }

        [Test]
        public void InjectedFailureAfterCreatingAnAssetRestoresOriginalsAndRemovesOwnedNewAsset()
        {
            string folder = UseStandaloneRows();
            var previous = owner.Rows.ToArray();
            var originalFiles = new Dictionary<string, byte[]>();
            foreach (var path in previous.Select(AssetDatabase.GetAssetPath).Concat(new[] { AssetDatabase.GetAssetPath(owner) }))
            {
                originalFiles.Add(path, File.ReadAllBytes(path));
                originalFiles.Add(path + ".meta", File.ReadAllBytes(path + ".meta"));
            }
            var staged = new List<AssetRow> { Object.Instantiate(previous[0]), ScriptableObject.CreateInstance<AssetRow>(), ScriptableObject.CreateInstance<AssetRow>() };
            try
            {
                staged[0].Text = "partially updated before failure";
                staged[1].Id = 2;
                staged[2].Id = 3;
                foreach (var row in staged) { row.Parent = owner; row.hideFlags = HideFlags.HideAndDontSave; }
                var existing = previous.ToDictionary(row => row.Id.ToString());
                var destinations = new Dictionary<string, string> { ["1"] = folder + "/1.asset", ["2"] = folder + "/2.asset" };
                // 受控缺键故障在更新1、实际创建2后触发；不把此测试冒称为真实Unity IO故障复现。
                var api = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("YLFramework.Excel.Editor.ExcelAuthoring")).Single(t => t != null);
                var apply = api.GetMethod("ApplyAssets", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(typeof(AssetRow));
                var failure = Assert.Throws<TargetInvocationException>(() => apply.Invoke(null, new object[]
                {
                    owner.Rows, owner, typeof(Owner).GetField("Rows"), AssetDatabase.GetAssetPath(owner), previous, staged, existing, destinations, originalFiles, folder
                }));
                Assert.That(failure.InnerException, Is.TypeOf<KeyNotFoundException>(), "A rollback failure must not be hidden by weakening this assertion.");
                foreach (var file in originalFiles) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
                Assert.That(File.Exists(folder + "/2.asset"), Is.False);
                Assert.That(File.Exists(folder + "/2.asset.meta"), Is.False);
                Assert.That(owner.Rows.Select(row => row.Id), Is.EqualTo(new[] { 1, 9 }));
                Assert.That(AssetDatabase.LoadAssetAtPath<AssetRow>(folder + "/1.asset").Text, Is.EqualTo("old"));
            }
            finally { foreach (var row in staged) if (row != null) Object.DestroyImmediate(row); }
        }
    }
}
