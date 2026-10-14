using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YLFramework.Core.Editor.Tests
{
    public class CoreDefaultResourceEditorTests
    {
        [TestCase("Canvas", "CreateCanvas")]
        [TestCase("EventSystem", "CreateEventSystem")]
        public void DefaultFactoriesCreateOwnedInactivePreviewObjectsAndSaveIndependentPrefabs(string name, string factoryName)
        {
            string folderName = "YLCoreEditorTest_" + Guid.NewGuid().ToString("N");
            string directory = "Assets/YLFramework/Tests/Core/Editor/" + folderName;
            Scene activeScene = SceneManager.GetActiveScene();
            bool activeWasDirty = activeScene.isDirty;
            int[] originalRoots = activeScene.GetRootGameObjects().Select(go => go.GetInstanceID()).ToArray();
            var externalEventSystem = EventSystem.current;
            Scene preview = default(Scene);
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Core/Editor", folderName), Is.Not.Empty);
                preview = EditorSceneManager.NewPreviewScene();
                var factory = typeof(ListenerInitOnLoad).GetMethod(factoryName, BindingFlags.NonPublic | BindingFlags.Static);
                var enable = typeof(ListenerInitOnLoad).GetMethod("EnablePrefabAsset", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(factory, Is.Not.Null);
                Assert.That(enable, Is.Not.Null);
                var created = (GameObject)factory.Invoke(null, new object[] { preview });
                Assert.That(created.scene, Is.EqualTo(preview));
                Assert.That(created.activeSelf, Is.False);
                Assert.That(EventSystem.current, Is.SameAs(externalEventSystem));
                string path = directory + "/" + name + ".prefab";
                Assert.That(PrefabUtility.SaveAsPrefabAsset(created, path), Is.Not.Null);
                enable.Invoke(null, new object[] { path });
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.activeSelf, Is.True);
                Assert.That(created.activeSelf, Is.False, "Only the serialized asset may be enabled.");
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(path));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(
                    name == "Canvas" ? ListenerInitOnLoad.CanvasPath : ListenerInitOnLoad.EventSystemPath)));
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
                    foreach (var component in transform.GetComponents<Component>())
                        Assert.That(component.GetType().Assembly.GetName().Name, Does.Not.StartWith("XFGameFramework"));
                }
                if (name == "Canvas")
                {
                    Assert.That(prefab.GetComponents<Component>().Select(c => c.GetType()), Is.EqualTo(new[]
                    {
                        typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                        typeof(AutomaticAdaptationCanvasScaler)
                    }));
                    Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                    Assert.That(prefab.GetComponent<Canvas>().additionalShaderChannels, Is.EqualTo(
                        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3));
                    var scaler = prefab.GetComponent<CanvasScaler>();
                    Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                    Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                    Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(1));
                    Assert.That(prefab.GetComponent<AutomaticAdaptationCanvasScaler>().GetType().Assembly.GetName().Name,
                        Is.EqualTo("YLFramework.Runtime"));
                }
                else
                {
                    Assert.That(prefab.GetComponents<Component>().Select(c => c.GetType()), Is.EqualTo(new[]
                    {
                        typeof(Transform), typeof(EventSystem)
                    }));
                    var eventSystem = prefab.GetComponent<EventSystem>();
                    Assert.That(eventSystem.sendNavigationEvents, Is.True);
                    Assert.That(eventSystem.pixelDragThreshold, Is.EqualTo(10));
                    Assert.That(prefab.GetComponents<BaseInputModule>(), Is.Empty);
                    var bridge = typeof(PanelManager).Assembly.GetType("YLFramework.Core.InputSystemBridge")
                        .GetMethod("EnsureEventSystemInputModule", BindingFlags.Public | BindingFlags.Static);
                    var instanceSystem = created.GetComponent<EventSystem>();
                    bridge.Invoke(null, new object[] { instanceSystem });
                    var input = created.GetComponent<BaseInputModule>();
#if ENABLE_INPUT_SYSTEM
                    Assert.That(input.GetType().FullName, Is.EqualTo("UnityEngine.InputSystem.UI.InputSystemUIInputModule"));
#else
                    Assert.That(input, Is.TypeOf<StandaloneInputModule>());
#endif
                    bridge.Invoke(null, new object[] { instanceSystem });
                    Assert.That(created.GetComponents<BaseInputModule>(), Is.EqualTo(new[] { input }));
                    Assert.That(EventSystem.current, Is.SameAs(externalEventSystem));
                    Assert.That(prefab.GetComponents<BaseInputModule>(), Is.Empty, "Runtime setup must not rewrite the asset.");
                }
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (AssetDatabase.IsValidFolder(directory)) AssetDatabase.DeleteAsset(directory);
            }
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
            Assert.That(activeScene.isDirty, Is.EqualTo(activeWasDirty));
            Assert.That(activeScene.GetRootGameObjects().Select(go => go.GetInstanceID()), Is.EqualTo(originalRoots));
            Assert.That(EventSystem.current, Is.SameAs(externalEventSystem));
        }

        [Test]
        public void RepeatedDefaultGenerationDoesNotChangeExistingOwnedResources()
        {
            var snapshots = CaptureExistingDefaults();
            ListenerInitOnLoad.GenerateDefaultResources();
            ListenerInitOnLoad.GenerateDefaultResources();
            AssertDefaultsUnchanged(snapshots);
        }

        [Test]
        public void ActualInitializeOnLoadMethodSchedulesOnlyItsOwnDeferredGenerator()
        {
            var snapshots = CaptureExistingDefaults();
            MethodInfo initializer = typeof(ListenerInitOnLoad).GetMethod("InitOnLoad", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(initializer, Is.Not.Null);
            Assert.That(initializer.IsDefined(typeof(InitializeOnLoadMethodAttribute), false), Is.True);
            EditorApplication.CallbackFunction callback = ListenerInitOnLoad.GenerateDefaultResources;
            Delegate[] before = EditorApplication.delayCall == null ? new Delegate[0] : EditorApplication.delayCall.GetInvocationList();
            bool scheduled = false;
            try
            {
                initializer.Invoke(null, null);
                scheduled = true;
                var after = EditorApplication.delayCall.GetInvocationList();
                Assert.That(after.Count(d => d.Equals(callback)), Is.EqualTo(before.Count(d => d.Equals(callback)) + 1));
                Assert.That(after.Where(d => !d.Equals(callback)), Is.EqualTo(before.Where(d => !d.Equals(callback))));
                // Remove and execute only this newly scheduled method, never the global delayCall delegate.
                EditorApplication.delayCall -= callback;
                scheduled = false;
                callback();
                AssertDefaultsUnchanged(snapshots);
                Assert.That(EditorApplication.delayCall == null ? new Delegate[0] : EditorApplication.delayCall.GetInvocationList(),
                    Is.EqualTo(before));
            }
            finally
            {
                if (scheduled)
                    EditorApplication.delayCall -= callback;
            }
        }

        [TestCase(ListenerInitOnLoad.CanvasPath)]
        [TestCase(ListenerInitOnLoad.EventSystemPath)]
        [TestCase(ListenerInitOnLoad.ResourceRoot)]
        [TestCase("Assets/YLFramework/Runtime/Core/Resources")]
        [TestCase("Assets/YLFramework/Runtime/Core/Resources/YLFramework")]
        [TestCase("Assets/YLFramework/Runtime/Core")]
        public void DeletionProtectionRecognizesOnlyOwnedDefaultsAndNarrowAncestors(string path)
        {
            Assert.That(ListenerFileChange.IsProtectedDefaultResource(path), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Assets/Games")]
        [TestCase("Assets/Games/MainModule/Resources/Canvas.prefab")]
        [TestCase("Assets/Resources/Canvas.prefab")]
        [TestCase("Assets/Resources/EventSystem.prefab")]
        [TestCase("Assets/Resources/XFGameFramework/Canvas.prefab")]
        [TestCase("Assets/Resources/XFGameFramework/EventSystem.prefab")]
        [TestCase("Packages/com.xfkj.xfgameframework/Runtime/Resources/Canvas.prefab")]
        [TestCase("Assets/YLFramework/Editor/Help")]
        [TestCase("Assets/YLFramework/Editor/Core")]
        [TestCase("Assets/YLFramework/Runtime/XFMain")]
        [TestCase("Assets/YLFramework/Runtime")]
        [TestCase("Assets/YLFramework")]
        [TestCase("Assets/YLFramework/Runtime/Core/Resources/YLFramework/CoreExtra")]
        [TestCase("Assets/YLFramework/Runtime/Core/Resources/YLFramework/Core/Unrelated.asset")]
        [TestCase("Assets/YLFramework/Runtime/Core/Resources/YLFramework/Core/Canvas.prefab.backup")]
        public void DeletionCallbackDoesNotBlockGameOriginalXFHelpOrUnrelatedAssets(string path)
        {
            Assert.That(ListenerFileChange.IsProtectedDefaultResource(path), Is.False);
            Assert.That(ListenerFileChange.OnWillDeleteAsset(path, RemoveAssetOptions.DeleteAssets),
                Is.EqualTo(AssetDeleteResult.DidNotDelete));
        }

        private static DefaultFileSnapshot[] CaptureExistingDefaults()
        {
            Assert.That(ListenerInitOnLoad.ResourceRoot,
                Is.EqualTo("Assets/YLFramework/Runtime/Core/Resources/YLFramework/Core"));
            var paths = new[]
            {
                ListenerInitOnLoad.CanvasPath, ListenerInitOnLoad.CanvasPath + ".meta",
                ListenerInitOnLoad.EventSystemPath, ListenerInitOnLoad.EventSystemPath + ".meta"
            };
            // Missing-resource generation belongs to coordinator verification, not destructive test setup.
            foreach (string path in paths)
                Assert.That(File.Exists(path), Is.True, "Coordinator must create/import default resources first: " + path);
            return paths.Select(path => new DefaultFileSnapshot
            {
                Path = path,
                Bytes = File.ReadAllBytes(path),
                LastWriteTime = File.GetLastWriteTimeUtc(path)
            }).ToArray();
        }

        private static void AssertDefaultsUnchanged(DefaultFileSnapshot[] snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                Assert.That(File.ReadAllBytes(snapshot.Path), Is.EqualTo(snapshot.Bytes), snapshot.Path);
                Assert.That(File.GetLastWriteTimeUtc(snapshot.Path), Is.EqualTo(snapshot.LastWriteTime), snapshot.Path);
            }
        }

        private sealed class DefaultFileSnapshot
        {
            public string Path;
            public byte[] Bytes;
            public DateTime LastWriteTime;
        }
    }
}
