using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YLFramework.Core.Tests
{
    public class CoreDefaultResourceTests
    {
        private const string ResourceRoot = "Assets/YLFramework/Runtime/Core/Resources/";
        private const string OriginalRoot = "Assets/Resources/XFGameFramework/";
        private const string OriginalAdaptationGuid = "abf116dc13c27ef4583812631aacc1c3";
        private const string AdaptationPath = "Assets/YLFramework/Runtime/Core/UI/Adaptation/AutomaticAdaptationCanvasScaler.cs";

        [TestCase("Canvas")]
        [TestCase("EventSystem")]
        public void DefaultResourcesHaveUniqueKeysAndIndependentIdentities(string name)
        {
            string key = "YLFramework/Core/" + name;
            string path = ResourceRoot + key + ".prefab";
            var prefab = Resources.Load<GameObject>(key);
            var original = Resources.Load<GameObject>("XFGameFramework/" + name);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(original, Is.Not.Null);
            Assert.That(prefab, Is.Not.SameAs(original));
            Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(path));
            Assert.That(AssetDatabase.GetAssetPath(original), Is.EqualTo(OriginalRoot + name + ".prefab"));
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Has.Length.EqualTo(32));
            Assert.That(guid, Is.Not.EqualTo(AssetDatabase.AssetPathToGUID(OriginalRoot + name + ".prefab")));
            Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.EqualTo(path));

            foreach (string resourceKey in new[] { key, "XFGameFramework/" + name })
            {
                var paths = AssetDatabase.GetAllAssetPaths().Where(p =>
                {
                    int index = p.LastIndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
                    return index >= 0 && !AssetDatabase.IsValidFolder(p) &&
                        string.Equals(Path.ChangeExtension(p.Substring(index + 11), null), resourceKey, StringComparison.OrdinalIgnoreCase);
                }).ToArray();
                Assert.That(paths, Has.Length.EqualTo(1), resourceKey + ": " + string.Join(", ", paths));
            }

            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
                foreach (var component in transform.GetComponents<Component>())
                {
                    Assert.That(component, Is.Not.Null);
                    Assert.That(component.GetType().Assembly.GetName().Name, Does.Not.StartWith("XFGameFramework"));
                    using (var serialized = new SerializedObject(component))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
                                Assert.That(property.objectReferenceInstanceIDValue, Is.Zero, component.GetType() + ": " + property.propertyPath);
                        }
                    }
                }
            }
            Assert.That(AssetDatabase.GetDependencies(path).Any(p => p.StartsWith("Packages/com.xfkj.") || p.StartsWith(OriginalRoot)), Is.False);
        }

        [Test]
        public void CanvasPreservesOriginalSerializedDataExceptYLAdaptationBinding()
        {
            string path = ResourceRoot + "YLFramework/Core/Canvas.prefab";
            string adaptationGuid = AssetDatabase.AssetPathToGUID(AdaptationPath);
            Assert.That(adaptationGuid, Is.Not.Empty.And.Not.EqualTo(OriginalAdaptationGuid));
            string expected = File.ReadAllText(OriginalRoot + "Canvas.prefab").Replace(OriginalAdaptationGuid, adaptationGuid);
            Assert.That(File.ReadAllText(path), Is.EqualTo(expected));

            var prefab = Resources.Load<GameObject>("YLFramework/Core/Canvas");
            Assert.That(prefab.GetComponents<Component>().Select(c => c.GetType()), Is.EqualTo(new[]
            {
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AutomaticAdaptationCanvasScaler)
            }));
            var adaptation = prefab.GetComponent<AutomaticAdaptationCanvasScaler>();
            Assert.That(adaptation.GetType().Assembly.GetName().Name, Is.EqualTo("YLFramework.Runtime"));
            Assert.That(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(adaptation)), Is.EqualTo(AdaptationPath));
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(prefab.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
        }

        [Test]
        public void EventSystemPreservesNavigationSettingsAndDefersBackendSelectionToRuntime()
        {
            var prefab = Resources.Load<GameObject>("YLFramework/Core/EventSystem");
            Assert.That(prefab.GetComponents<Component>().Select(c => c.GetType()), Is.EqualTo(new[]
            {
                typeof(Transform), typeof(EventSystem)
            }));
            Assert.That(prefab.GetComponent<EventSystem>().sendNavigationEvents, Is.True);
            Assert.That(prefab.GetComponent<EventSystem>().pixelDragThreshold, Is.EqualTo(10));
            Assert.That(prefab.GetComponents<BaseInputModule>(), Is.Empty,
                "An active Legacy module must not run before the framework chooses the supported backend.");
        }
    }
}
