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

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceWindowTests
    {
        [UnityTest]
        public IEnumerator ActualWindowGuiSelectCancelRefreshAndClose()
        {
            var previousSelection = Selection.objects;
            var previousFocus = EditorWindow.focusedWindow;
            var folder = "Assets/YLFramework/Tests/EditMode/Window62-" + Guid.NewGuid().ToString("N");
            ResourceGuiTestWindow window = null;
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode", Path.GetFileName(folder));
            try
            {
                var path = folder + "/Click.bytes";
                File.WriteAllText(path, "isolated GUI asset");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ResServiceAssetsMenu.MarkAB(path);
                window = ScriptableObject.CreateInstance<ResourceGuiTestWindow>();
                window.position = new Rect(100, 100, 760, 850);
                window.ShowUtility();
                window.Focus();
                for (var frame = 0; frame < 20 && (window.Repaints == 0 || window.SelectButton.width == 0); frame++)
                { window.Repaint(); yield return null; }
                Assert.Greater(window.Layouts, 0);
                Assert.Greater(window.Repaints, 0);
                Assert.Greater(window.SelectButton.width, 0, "Actual GUI selection button was not laid out.");
                var view = typeof(ResServiceEditorWindow).GetField("_resServiceView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                var preview = (string)typeof(ResServiceView).GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                StringAssert.Contains("StandaloneWindows64 / Windows", preview);
                StringAssert.Contains(Path.GetFullPath("AssetBundles/Windows"), preview);
                Selection.objects = Array.Empty<Object>();
                Click(window, window.SelectButton.center);
                yield return null;
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(Selection.activeObject));
                Click(window, window.CancelButton.center);
                yield return null;
                Assert.IsFalse(ResServiceAssetsMenu.IsMarked(path));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Assert.IsEmpty(AssetImporter.GetAtPath(path).assetBundleName);
                Assert.IsFalse(Directory.Exists("AssetBundles/Windows"));

                var calls = 0;
                typeof(ResServiceView).GetMethod("QueueOperation", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { new Func<bool>(() => { calls++; return true; }), "test owned callback" });
                var pending = (EditorApplication.CallbackFunction)typeof(ResServiceView).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Assert.IsTrue(EditorApplication.delayCall.GetInvocationList().Contains(pending));
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
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.RemoveUnusedAssetBundleNames();
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
        internal Rect CancelButton;

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
            Read(group);
            void Read(object entry)
            {
                var entryType = entry.GetType();
                var rectangle = (Rect)entryType.GetField("rect", flags).GetValue(entry);
                var style = (GUIStyle)entryType.GetProperty("style", flags)?.GetValue(entry);
                if (style != null && style.name == "button")
                {
                    if (Mathf.Abs(rectangle.width - 50) < 1) SelectButton = rectangle;
                    if (Mathf.Abs(rectangle.width - 75) < 1) CancelButton = rectangle;
                }
                var children = entryType.GetField("entries", flags)?.GetValue(entry) as IEnumerable;
                if (children != null) foreach (var child in children) Read(child);
            }
        }
    }
}
