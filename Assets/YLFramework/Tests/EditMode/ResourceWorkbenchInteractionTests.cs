using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Editor.Resource;
using YLFramework.Tests.ResourceBuild;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceWorkbenchInteractionTests
    {
        [UnityTest]
        public IEnumerator QueuedOperationRunsOnEditorUpdateWithoutWaitingForInspectorIdle()
        {
            var view = new ResServiceView();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var calls = 0;
            try
            {
                typeof(ResServiceView).GetMethod("QueueOperation", flags).Invoke(view,
                    new object[] { new Func<string>(() => { calls++; return "Executed"; }) });
                var pending = (EditorApplication.CallbackFunction)typeof(ResServiceView).GetField("pending", flags).GetValue(view);
                Assert.That(EditorApplication.delayCall?.GetInvocationList().Contains(pending) ?? false, Is.False,
                    "A formal queued action must not stall until background Editor inspectors become idle.");
                for (var frame = 0; frame < 20 && calls == 0; frame++) yield return null;
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(typeof(ResServiceView).GetField("pending", flags).GetValue(view), Is.Null);
            }
            finally { view.OnDispose(); }
        }

        [Test]
        public void RefreshRebuildsSelectionRowsAfterDefinitionChangesOutsideTheView()
        {
            using var fixture = new ResourceReleaseFixture();
            var view = new ResServiceView();
            try
            {
                view.SetProject(fixture.Project);
                view.RefreshPreview();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var definition = typeof(ResServiceView).GetField("definition", flags).GetValue(view);
                var rows = definition.GetType().GetMethod("MatchingSelections", flags);
                Assert.That(((int[])rows.Invoke(definition, new object[] { fixture.Project })).Length, Is.EqualTo(1));
                var path = fixture.Folder + "/Content/external-edit.txt";
                Assert.That(AssetDatabase.CopyAsset(fixture.TextPath, path), Is.True);
                ResourceEditorOperations.AddSelections(fixture.Project, 0, new[] { AssetDatabase.LoadMainAssetAtPath(path) });
                view.RefreshPreview();
                Assert.That(((int[])rows.Invoke(definition, new object[] { fixture.Project })).Length, Is.EqualTo(2),
                    "Explicit refresh must not retain stale selection indexes after Inspector or API edits.");
                Undo.ClearUndo(fixture.Project);
            }
            finally { view.OnDispose(); }
        }

        [UnityTest]
        public IEnumerator DisabledCustomDropRejectsInputWithoutMutatingTheDefinitionOrUndo()
        {
            using var fixture = new ResourceReleaseFixture();
            var path = fixture.Folder + "/Content/disabled-drop.txt";
            Assert.That(AssetDatabase.CopyAsset(fixture.TextPath, path), Is.True);
            var window = ScriptableObject.CreateInstance<ResourceDropTestWindow>();
            window.Project = fixture.Project;
            try
            {
                window.position = new Rect(120, 80, 500, 220);
                window.ShowUtility(); window.Focus();
                yield return null;
                var before = JsonUtility.ToJson(fixture.Project);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { AssetDatabase.LoadMainAssetAtPath(path) };
                DragAndDrop.paths = new[] { path };
                window.AllowEdits = false;
                window.PendingDrop = true;
                while (window.PendingDrop) { window.Repaint(); yield return null; }
                Assert.That(JsonUtility.ToJson(fixture.Project), Is.EqualTo(before), "A disabled custom input handler must respect the same editing gate as native controls.");
                Assert.That(window.Changes, Is.Zero);
                Assert.That(window.LastVisualMode, Is.EqualTo(DragAndDropVisualMode.Rejected), window.LastInput);
                window.AllowEdits = true;
                DragAndDrop.objectReferences = new[] { AssetDatabase.LoadMainAssetAtPath(path) };
                window.PendingDrop = true;
                while (window.PendingDrop) { window.Repaint(); yield return null; }
                Assert.That(fixture.Project.Groups[0].Selections.Count, Is.EqualTo(2), "The enabled handler must remain functional.");
                Assert.That(window.Changes, Is.EqualTo(1));
                Undo.ClearUndo(fixture.Project);
            }
            finally
            {
                window.Close();
                DragAndDrop.objectReferences = Array.Empty<Object>();
                DragAndDrop.paths = Array.Empty<string>();
            }
        }
    }

    internal sealed class ResourceDropTestWindow : EditorWindow
    {
        internal ResourceProject Project;
        internal bool AllowEdits;
        internal int Changes;
        internal bool PendingDrop;
        internal DragAndDropVisualMode LastVisualMode;
        internal string LastInput;
        private object definition;
        private MethodInfo drop;

        private void OnEnable()
        {
            var type = typeof(ResServiceView).Assembly.GetType("YLFramework.Editor.Resource.ResourceDefinitionView");
            definition = Activator.CreateInstance(type, true);
            drop = type.GetMethod("HandleDrop", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void OnGUI()
        {
            if (!Project || !PendingDrop || Event.current.type != EventType.Repaint) return;
            PendingDrop = false;
            using var disabled = new EditorGUI.DisabledScope(!AllowEdits);
            var previous = Event.current;
            var input = new Event { type = EventType.DragPerform, mousePosition = new Vector2(30, 30) };
            Event.current = input;
            try
            {
                LastInput = Event.current.type + " refs=" + DragAndDrop.objectReferences.Length + " enabled=" + GUI.enabled;
                drop.Invoke(definition, new object[] { new Rect(10, 10, 440, 150), Project,
                    new Action(() => Changes++), new Action<Func<string>>(operation => operation()) });
                LastVisualMode = DragAndDrop.visualMode;
            }
            finally { Event.current = previous; }
        }
    }
}
