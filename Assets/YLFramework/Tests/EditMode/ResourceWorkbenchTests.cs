using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Editor.Resource;
using YLFramework.Tests.ResourceBuild;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceWorkbenchTests
    {
        [UnityTest]
        public IEnumerator ActualWorkbenchKeyboardCreatesGroupSavesAndFocusesSearchWithoutTouchingImporter()
        {
            using var fixture = new ResourceReleaseFixture();
            var previousFocus = EditorWindow.focusedWindow;
            var previousSelection = Selection.objects;
            var meta = File.ReadAllBytes(fixture.TextPath + ".meta");
            var window = ScriptableObject.CreateInstance<WorkbenchInputTestWindow>();
            try
            {
                window.position = new Rect(80, 60, 1080, 720);
                window.ShowUtility(); window.Focus(); window.SetProject(fixture.Project);
                yield return null;
                var count = fixture.Project.Groups.Count;
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.G, alt = true });
                yield return null;
                Assert.AreEqual(count + 1, fixture.Project.Groups.Count, "Alt+G must create a real undoable Group in the open workbench.");
                Assert.IsTrue(EditorUtility.IsDirty(fixture.Project));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, control = true });
                yield return null;
                Assert.IsFalse(EditorUtility.IsDirty(fixture.Project), "Ctrl+S saves only the selected Project definition.");
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.F, control = true });
                for (var i = 0; i < 10 && window.FocusedControl != "Resource.EntrySearch"; i++)
                { window.Repaint(); yield return null; }
                Assert.AreEqual("Resource.EntrySearch", window.FocusedControl);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(fixture.TextPath + ".meta"));
                Assert.IsEmpty(AssetImporter.GetAtPath(fixture.TextPath).assetBundleName);
                Undo.ClearUndo(fixture.Project);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (window) window.Close();
                Selection.objects = previousSelection;
                if (previousFocus) previousFocus.Focus();
            }
        }

        [Test]
        public void BatchMoveAndRemovePreserveAddressesAliasesAndUndoAsOneEdit()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            var source = project.Groups[0];
            source.Selections[0].Address = "formal/address";
            source.Selections[0].Aliases = new[] { "formal/alias" };
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssetIfDirty(project);
            var sourceGuid = source.Selections[0].Guid;
            var originalCount = source.Selections.Count;
            var target = (int)Invoke("AddGroup", project, "batch-target");
            Undo.ClearUndo(project);
            var before = JsonUtility.ToJson(project);
            var meta = File.ReadAllBytes(fixture.TextPath + ".meta");
            Invoke("MoveSelections", project, 0, new[] { 0 }, target);
            Assert.AreEqual(originalCount - 1, project.Groups[0].Selections.Count);
            Assert.AreEqual(sourceGuid, project.Groups[target].Selections.Single().Guid);
            Assert.AreEqual("formal/address", project.Groups[target].Selections[0].Address);
            CollectionAssert.AreEqual(new[] { "formal/alias" }, project.Groups[target].Selections[0].Aliases);
            Undo.PerformUndo(); Assert.AreEqual(before, JsonUtility.ToJson(project));
            Undo.PerformRedo();
            Invoke("RemoveSelections", project, target, new[] { 0 });
            Assert.IsEmpty(project.Groups[target].Selections);
            Undo.PerformUndo(); Assert.AreEqual(sourceGuid, project.Groups[target].Selections.Single().Guid);
            CollectionAssert.AreEqual(meta, File.ReadAllBytes(fixture.TextPath + ".meta"));
            Undo.ClearUndo(project);
        }

        [Test]
        public void InvalidBatchAndGroupRenameAreAtomicAndDoNotMutateSourceData()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            var target = (int)Invoke("AddGroup", project, "valid-target");
            var before = JsonUtility.ToJson(project);
            Assert.Throws<ArgumentException>(() => Invoke("MoveSelections", project, 0, new[] { 0, int.MaxValue }, target));
            Assert.Throws<ArgumentException>(() => Invoke("RemoveSelections", project, 0, new[] { -1 }));
            Assert.Throws<ArgumentException>(() => Invoke("RenameGroup", project, target, project.Groups[0].Id.ToUpperInvariant()));
            Assert.Throws<ArgumentException>(() => Invoke("RenameGroup", project, target, "../escape"));
            Assert.AreEqual(before, JsonUtility.ToJson(project), "A failed batch must not partially remove or copy selections.");
            Invoke("RenameGroup", project, target, "renamed-target");
            Assert.AreEqual("renamed-target", project.Groups[target].Id);
            Undo.PerformUndo(); Assert.AreEqual("valid-target", project.Groups[target].Id);
            Assert.IsEmpty(AssetImporter.GetAtPath(fixture.TextPath).assetBundleName);
            Undo.ClearUndo(project);
        }

        private static object Invoke(string name, params object[] arguments)
        {
            var method = typeof(ResourceEditorOperations).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(x => x.Name == name && x.GetParameters().Length == arguments.Length);
            Assert.IsNotNull(method, "Missing formal workbench operation: " + name);
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }

    internal sealed class WorkbenchInputTestWindow : ResServiceEditorWindow
    {
        internal string FocusedControl;
        private void OnGUI()
        {
            typeof(ResServiceEditorWindow).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, null);
            if (Event.current.type == EventType.Repaint) FocusedControl = GUI.GetNameOfFocusedControl();
        }
    }
}
