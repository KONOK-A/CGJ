using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.DataAssets;
using YLFramework.DataAssets.Editor;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Extensions.Data
{
    [TestFixture]
    public class ExcelDataAssetsBindingTests
    {
        private ScriptableObjectConfigs root;
        private string folder;
        private string workbook;
        private string entries;
        private byte[] original;
        [SetUp] public void SetUp()
        {
            string name = "YLExcelBinding-" + Guid.NewGuid().ToString("N");
            folder = "Assets/" + name;
            YLFramework.Tests.DataAssets.DataAcceptanceOwnership.Own(folder);
            AssetDatabase.CreateFolder("Assets", name);
            root = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            AssetDatabase.CreateAsset(root, folder + "/Root.asset");
            var sample = ScriptableObject.CreateInstance<ExcelBoundDataFixture>();
            try
            {
                var script = MonoScript.FromScriptableObject(sample);
                Assert.That(script, Is.Not.Null, "Fixture MonoScript must be genuinely imported by Unity.");
                ScriptableObjectConfigsController.SetDataType(root, script);
                ScriptableObjectConfigsController.Save(root);
            }
            finally { Object.DestroyImmediate(sample); }
            entries = ScriptableObjectConfigsController.GetDataPath(root);
            YLFramework.Tests.DataAssets.DataAcceptanceOwnership.Own(entries);
            original = File.ReadAllBytes(folder + "/Root.asset");
            string output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/Extensions.Data.Excel/Unity", name);
            Directory.CreateDirectory(output);
            workbook = Path.Combine(output, "bound.xlsx");
            ExcelAuthoringTests.WriteWorkbook(workbook, new[] { "id", "config_name", "power" }, new[] { "int", "string", "int" }, new object[] { 1, "bound data", 42 });
        }
        [TearDown] public void TearDown()
        {
            if (!string.IsNullOrEmpty(entries) && AssetDatabase.IsValidFolder(entries)) AssetDatabase.DeleteAsset(entries);
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }
        private static object Adapter(string name, params object[] args)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("YLFramework.Excel.Editor.DataAssetsExcelCommands")).Single(t => t != null);
            try { return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }
        [Test] public void BaseListUsesBoundDerivedFieldsAndCreatesOwnedFolderOnlyAfterConfirmation()
        {
            using (var plan = (IDisposable)Adapter("PreviewImport", root, workbook))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Empty);
                Assert.That(Directory.Exists(entries), Is.False);
                Assert.That(ExcelAuthoringTests.Commit(plan, false), Is.EqualTo("Cancelled"));
            }
            Assert.That(File.ReadAllBytes(folder + "/Root.asset"), Is.EqualTo(original));
            Assert.That(Directory.Exists(entries), Is.False);
            using (var plan = (IDisposable)Adapter("PreviewImport", root, workbook))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Empty);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Applied"));
            }
            Assert.That(root.configs, Has.Count.EqualTo(1));
            Assert.That(root.configs[0], Is.TypeOf<ExcelBoundDataFixture>());
            Assert.That(((ExcelBoundDataFixture)root.configs[0]).power, Is.EqualTo(42));
            Assert.That(root.configs[0].parent, Is.SameAs(root));
            Assert.That(AssetDatabase.GetAssetPath(root.configs[0]), Is.EqualTo(entries + "/1.asset"));
            Assert.That(root.GetScriptableObjectData(1), Is.SameAs(root.configs[0]));
        }
        [Test] public void InvalidTypeScriptGuidProducesVisibleFailureAndLeavesRootUnchanged()
        {
            root.type_script_guid = "ffffffffffffffffffffffffffffffff";
            ScriptableObjectConfigsController.Save(root);
            byte[] before = File.ReadAllBytes(folder + "/Root.asset");
            using (var plan = (IDisposable)Adapter("PreviewImport", root, workbook))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan).Any(e => e.Contains("type_script_guid")), Is.True);
                Assert.That(ExcelAuthoringTests.Commit(plan, true), Is.EqualTo("Failed"));
            }
            Assert.That(root.configs, Is.Empty);
            Assert.That(File.ReadAllBytes(folder + "/Root.asset"), Is.EqualTo(before));
        }
        [Test] public void OptionalSelectedRootMenusAreRegisteredWithoutOrdinaryEditorReferenceToExcel()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("YLFramework.Excel.Editor.DataAssetsExcelCommands")).Single(t => t != null);
            var menus = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).SelectMany(m => m.GetCustomAttributes<MenuItem>()).Where(m => !m.validate).Select(m => m.menuItem).ToArray();
            Assert.That(menus, Is.EquivalentTo(new[] { "Assets/YLFramework/Excel/Preview Import Selected Data Root", "Assets/YLFramework/Excel/Export Selected Data Root" }));
            Assert.That(typeof(ScriptableObjectConfigsController).Assembly.GetReferencedAssemblies().Any(a => a.Name == "YLFramework.Excel.Editor" || a.Name == "MiniExcel"), Is.False);
        }
        [Test] public void ExplicitPreviewDoesNotPersistWorkbookPathInRuntimeRoot()
        {
            Assert.That(typeof(ScriptableObjectConfigs).GetField("excel_path"), Is.Null);
            byte[] before = File.ReadAllBytes(folder + "/Root.asset");
            using (var plan = (IDisposable)Adapter("PreviewImport", root, workbook))
            {
                Assert.That(ExcelAuthoringTests.Errors(plan), Is.Empty);
                Assert.That(ExcelAuthoringTests.Commit(plan, false), Is.EqualTo("Cancelled"));
            }
            Assert.That(EditorJsonUtility.ToJson(root), Does.Not.Contain("excel_path"));
            Assert.That(File.ReadAllBytes(folder + "/Root.asset"), Is.EqualTo(before));
            Assert.That(Directory.Exists(entries), Is.False);
        }
        [Test] public void ExportRemembersWorkbookForRevealByAssetGuid()
        {
            string output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/Extensions.Data.Excel/Unity",
                Path.GetFileName(folder), "exported.xlsx");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            try
            {
                Adapter("Export", root, output);
                Assert.That(File.Exists(output), Is.True);
                Assert.That(DataAssetsWorkbookMemory.TryGetPath(root, out var remembered), Is.True);
                Assert.That(remembered, Is.EqualTo(Path.GetFullPath(output)));
                Assert.That(DataAssetsWorkbookMemory.CanReveal(root), Is.True);
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root));
                Assert.That(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(root), folder + "/Renamed.asset"), Is.Empty);
                Assert.That(AssetDatabase.AssetPathToGUID(folder + "/Renamed.asset"), Is.EqualTo(guid));
                Assert.That(DataAssetsWorkbookMemory.TryGetPath(root, out var afterMove), Is.True);
                Assert.That(afterMove, Is.EqualTo(Path.GetFullPath(output)));
            }
            finally { DataAssetsWorkbookMemory.Forget(root); }
        }
    }
}
