using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Editor.Resource;
using Object = UnityEngine.Object;
using YLFramework.Tests.ResourceBuild;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceWindowTests
    {
        [UnityTest]
        public IEnumerator ActualWindowGuiSelectRefreshProjectSwitchAndCloseCancelQueuedOperations()
        {
            var previousSelection = Selection.objects;
            var previousFocus = EditorWindow.focusedWindow;
            using var fixture = new ResourceReleaseFixture();
            ResourceGuiTestWindow window = null;
            try
            {
                var path = fixture.TextPath;
                fixture.Project.Groups.RemoveRange(1, fixture.Project.Groups.Count - 1);
                window = ScriptableObject.CreateInstance<ResourceGuiTestWindow>();
                window.position = new Rect(100, 50, 760, 950);
                window.ShowUtility();
                window.Focus();
                window.SetProject(fixture.Project);
                var view = (ResServiceView)typeof(ResServiceEditorWindow).GetField("_resServiceView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                yield return null;
                view.RefreshPreview();
                for (var frame = 0; frame < 20 && (window.Repaints == 0 || window.SelectButton.width == 0); frame++)
                { window.Repaint(); yield return null; }
                Assert.Greater(window.Layouts, 0);
                Assert.Greater(window.Repaints, 0);
                Assert.Greater(window.SelectButton.width, 0, "Actual GUI selection button was not laid out.");
                Selection.objects = Array.Empty<Object>();
                Click(window, window.SelectButton.center);
                yield return null;
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(Selection.activeObject), "Button=" + window.SelectButton + "; status=" +
                    typeof(ResServiceView).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view));
                Assert.IsEmpty(AssetImporter.GetAtPath(path).assetBundleName);

                var calls = 0;
                typeof(ResServiceView).GetMethod("QueueOperation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { new Func<string>(() => { calls++; return "owned callback"; }) });
                var pending = (EditorApplication.CallbackFunction)typeof(ResServiceView).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Assert.IsTrue(EditorApplication.delayCall.GetInvocationList().Contains(pending));
                window.SetProject(null);
                Assert.IsFalse(EditorApplication.delayCall?.GetInvocationList().Contains(pending) ?? false, "Project switching cancels the old queued action.");
                typeof(ResServiceView).GetMethod("QueueOperation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { new Func<string>(() => { calls++; return "owned callback"; }) });
                pending = (EditorApplication.CallbackFunction)typeof(ResServiceView).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Assert.AreEqual(1, Selection.selectionChanged.GetInvocationList().Count(callback => callback.Target == window));
                window.Close();
                Assert.IsFalse(EditorApplication.delayCall?.GetInvocationList().Contains(pending) ?? false);
                yield return null;
                Assert.AreEqual(0, calls);
                Assert.IsFalse(Selection.selectionChanged.GetInvocationList().Any(callback => callback.Target == window));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (window) window.Close();
                Selection.objects = previousSelection;
                if (previousFocus) previousFocus.Focus();
            }
        }

        private static void Click(EditorWindow window, Vector2 position)
        {
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, clickCount = 1, mousePosition = position });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, clickCount = 1, mousePosition = position });
        }
    }

    internal class ResourceGuiTestWindow : ResServiceEditorWindow
    {
        internal int Layouts;
        internal int Repaints;
        internal Rect SelectButton;

        private void OnGUI()
        {
            var type = Event.current.type;
            typeof(ResServiceEditorWindow).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, null);
            if (type == EventType.Layout) Layouts++;
            if (type != EventType.Repaint) return;
            Repaints++;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var cache = typeof(GUILayoutUtility).GetProperty("current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) ??
                        typeof(GUILayoutUtility).GetField("current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
            Assert.IsNotNull(cache, "Unity layout cache is unavailable.");
            var group = cache.GetType().GetField("topLevel", flags).GetValue(cache);
            Read(group, Vector2.zero);
            void Read(object entry, Vector2 offset)
            {
                var entryType = entry.GetType();
                var rectangle = (Rect)entryType.GetField("rect", flags).GetValue(entry);
                if (entryType.Name == "GUIScrollGroup") offset += rectangle.position;
                var style = (GUIStyle)entryType.GetProperty("style", flags)?.GetValue(entry);
                if (style != null && style.name == "button")
                {
                    if (Mathf.Abs(rectangle.width - 50) < 1) SelectButton = new Rect(rectangle.position + offset, rectangle.size);
                }
                var children = entryType.GetField("entries", flags)?.GetValue(entry) as IEnumerable;
                if (children != null) foreach (var child in children) Read(child, offset);
            }
        }
    }
}
