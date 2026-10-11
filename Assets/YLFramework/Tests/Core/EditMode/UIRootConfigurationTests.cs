using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core.Tests
{
    public class UIRootConfigurationTests
    {
        [Test]
        public void CameraRootUsesConfiguredCameraWithoutCreatingAnotherCamera()
        {
            var go = new GameObject("YLRoot.Configuration", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var cameraObject = new GameObject("YLRoot.Camera", typeof(Camera));
            try
            {
                go.SetActive(false);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                var context = go.AddComponent<UIRootContext>();
                var camera = cameraObject.GetComponent<Camera>();
                typeof(UIRootContext).GetField("uiCamera", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(context, camera);
                go.SetActive(true);
                typeof(UIRootContext).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(context, null);
                Assert.That(canvas.worldCamera, Is.SameAs(camera));
                Assert.That(context.UICamera, Is.SameAs(camera));
                Assert.That(go.GetComponentsInChildren<Camera>(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ExplicitModeAndResolutionConfigurationUsesOneScalerAuthority()
        {
            var go = new GameObject("YLRoot.Explicit", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cameraObject = new GameObject("YLRoot.ExplicitCamera", typeof(Camera));
            try
            {
                var context = go.AddComponent<UIRootContext>();
                var canvas = go.GetComponent<Canvas>();
                var camera = cameraObject.GetComponent<Camera>();
                context.ConfigureCanvas(RenderMode.ScreenSpaceCamera, camera, 5);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
                Assert.That(canvas.worldCamera, Is.SameAs(camera));
                Assert.That(canvas.planeDistance, Is.EqualTo(5));
                Assert.Throws<System.ArgumentException>(() => context.ConfigureCanvas(RenderMode.ScreenSpaceCamera, camera, -1));
                Assert.That(canvas.planeDistance, Is.EqualTo(5));
                context.ConfigureCanvas(RenderMode.ScreenSpaceOverlay);
                Assert.That(canvas.worldCamera, Is.Null);
                context.ConfigureResolution(new Vector2(800, 600), false, 0.25f);
                var scaler = go.GetComponent<CanvasScaler>();
                var adaptation = go.GetComponent<AutomaticAdaptationCanvasScaler>();
                scaler.matchWidthOrHeight = 1;
                adaptation.Refresh();
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.25f));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(800, 600)));
                context.ConfigureResolution(new Vector2(600, 800), false, 0.75f);
                Assert.That(go.GetComponents<AutomaticAdaptationCanvasScaler>(), Has.Length.EqualTo(1));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.75f));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(600, 800)));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(cameraObject); }
        }
    }
}
