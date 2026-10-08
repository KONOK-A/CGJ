using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace YLFramework.Core.Tests
{
    public class CoreDefaultResourcePlayModeTests
    {
        private static readonly FieldInfo InstanceField = typeof(PanelManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo OwnedEventSystemField = typeof(PanelManager).GetField("_eventSystem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Refresh = typeof(PanelManager).GetMethod("RefreshEventSystem", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo GetParent = typeof(PanelManager).GetMethod("GetParent", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly List<GameObject> ownedObjects = new List<GameObject>();
        private object previousManager;
        private object manager;

        [SetUp]
        public void SetUp()
        {
            Assert.That(EventSystem.current, Is.Null, "This fixture requires an empty test scene; never remove external input objects.");
            previousManager = InstanceField.GetValue(null);
            // Isolate the resource paths without registering or clearing shared manager callbacks.
            manager = Activator.CreateInstance(typeof(PanelManager), true);
            InstanceField.SetValue(null, manager);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (manager != null)
            {
                var eventSystem = (GameObject)OwnedEventSystemField.GetValue(manager);
                if (eventSystem) UnityEngine.Object.Destroy(eventSystem);
                foreach (var canvas in ((PanelManager)manager).uiCanvas.Values)
                    if (canvas) UnityEngine.Object.Destroy(canvas.gameObject);
                InstanceField.SetValue(null, previousManager);
                manager = null;
            }
            foreach (var go in ownedObjects)
                if (go) UnityEngine.Object.Destroy(go);
            ownedObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DefaultEventSystemIsCreatedOnlyOnceWhenNoneExists()
        {
            Refresh.Invoke(null, null);
            var created = (GameObject)OwnedEventSystemField.GetValue(manager);
            Assert.That(created, Is.Not.Null);
            Assert.That(created.name, Is.EqualTo("EventSystem(Created By YLFramework.Core)"));
            Assert.That(EventSystem.current, Is.SameAs(created.GetComponent<EventSystem>()));
            yield return null;
            Refresh.Invoke(null, null);
            Assert.That(OwnedEventSystemField.GetValue(manager), Is.SameAs(created));
            Assert.That(UnityEngine.Object.FindObjectsOfType<EventSystem>(), Has.Length.EqualTo(1));
            Assert.That(created.GetComponents<BaseInputModule>(), Has.Length.EqualTo(1));
            Assert.That(EventSystem.current.currentInputModule, Is.SameAs(created.GetComponent<StandaloneInputModule>()));
            UnityEngine.Object.Destroy(created);
            yield return null;
            Assert.That(EventSystem.current, Is.Null);
        }

        [UnityTest]
        public IEnumerator ExistingXFEventSystemIsReusedWithoutTakingOwnershipOrChangingInput()
        {
            var originalPrefab = Resources.Load<GameObject>("XFGameFramework/EventSystem");
            Assert.That(originalPrefab, Is.Not.Null);
            var external = UnityEngine.Object.Instantiate(originalPrefab);
            ownedObjects.Add(external); // The fixture owns this XF instance, not PanelManager.
            var eventSystem = external.GetComponent<EventSystem>();
            var input = external.GetComponent<StandaloneInputModule>();
            var selected = new GameObject("ExternalSelection");
            ownedObjects.Add(selected);
            eventSystem.firstSelectedGameObject = selected;
            eventSystem.SetSelectedGameObject(selected);
            eventSystem.pixelDragThreshold = 23;
            input.horizontalAxis = "Vertical";
            input.repeatDelay = 0.73f;
            yield return null;

            var components = external.GetComponents<Component>();
            var currentModule = eventSystem.currentInputModule;
            for (int i = 0; i < 3; i++) Refresh.Invoke(null, null);
            var parent = (Transform)GetParent.Invoke(null, new object[] { null, null, UIType.UI });
            Assert.That(parent.GetComponent<AutomaticAdaptationCanvasScaler>(), Is.Not.Null);
            Assert.That(OwnedEventSystemField.GetValue(manager), Is.Null);
            Assert.That(EventSystem.current, Is.SameAs(eventSystem));
            Assert.That(eventSystem.currentInputModule, Is.SameAs(currentModule));
            Assert.That(external.GetComponents<Component>(), Is.EqualTo(components));
            Assert.That(eventSystem.firstSelectedGameObject, Is.SameAs(selected));
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(selected));
            Assert.That(eventSystem.pixelDragThreshold, Is.EqualTo(23));
            Assert.That(input.horizontalAxis, Is.EqualTo("Vertical"));
            Assert.That(input.repeatDelay, Is.EqualTo(0.73f));
            Assert.That(UnityEngine.Object.FindObjectsOfType<EventSystem>(), Has.Length.EqualTo(1));

            UnityEngine.Object.Destroy(parent.gameObject);
            ((PanelManager)manager).uiCanvas.Clear();
            yield return null;
            Refresh.Invoke(null, null);
            Assert.That(external && input && input.enabled && external.activeInHierarchy, Is.True);
            Assert.That(EventSystem.current, Is.SameAs(eventSystem));
            Assert.That(OwnedEventSystemField.GetValue(manager), Is.Null);
        }

        [UnityTest]
        public IEnumerator DefaultCanvasesLoadAtBothLayersAndCoexistWithXFCanvas()
        {
            var originalPrefab = Resources.Load<GameObject>("XFGameFramework/Canvas");
            Assert.That(originalPrefab, Is.Not.Null);
            var original = UnityEngine.Object.Instantiate(originalPrefab);
            ownedObjects.Add(original);
            var originalComponents = original.GetComponents<Component>();
            var ui = (Transform)GetParent.Invoke(null, new object[] { null, null, UIType.UI });
            var persistent = (Transform)GetParent.Invoke(null, new object[] { null, null, UIType.DontDestroyUI });
            yield return null;

            Assert.That(ui, Is.Not.SameAs(persistent));
            Assert.That(ui.GetComponent<Canvas>().sortingOrder, Is.Zero);
            Assert.That(persistent.GetComponent<Canvas>().sortingOrder, Is.EqualTo(10000));
            Assert.That(persistent.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            foreach (var parent in new[] { ui, persistent })
            {
                Assert.That(parent.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(parent.GetComponent<AutomaticAdaptationCanvasScaler>().enabled, Is.True);
                Assert.That(parent.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                Assert.That(parent.GetComponent<GraphicRaycaster>().enabled, Is.True);
                Assert.That(parent.GetComponents<Component>(), Has.Length.EqualTo(5));
            }
            Assert.That(GetParent.Invoke(null, new object[] { null, null, UIType.UI }), Is.SameAs(ui));
            Assert.That(GetParent.Invoke(null, new object[] { null, null, UIType.DontDestroyUI }), Is.SameAs(persistent));
            Assert.That(original.GetComponents<Component>(), Is.EqualTo(originalComponents));
            Assert.That(original.GetComponent<Canvas>().sortingOrder, Is.Zero);
            Assert.That(UnityEngine.Object.FindObjectsOfType<EventSystem>(), Has.Length.EqualTo(1));
        }
    }
}
