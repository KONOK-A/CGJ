using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YLFramework.Core.Tests
{
    public class ColorSelectorTests
    {
        [Test]
        public void InitialColorIsCurrentWithoutSendingUserNotification()
        {
            var go = new GameObject("YLColor.Initial", typeof(RectTransform));
            try
            {
                var selector = go.AddComponent<ColorSelector>();
                int changes = 0;
                var initial = new Color(0.25f, 0.5f, 0.75f, 0.3f);
                selector.BeginSession(initial, _ => changes++);
                Assert.That(selector.CurrentColor, Is.EqualTo(initial));
                Assert.That(changes, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TriangleVerticesAndEdgesAgreeWithHSVAndPreserveAlpha()
        {
            var go = new GameObject("YLColor.Triangle", typeof(RectTransform));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(320, 240);
                rect.pivot = new Vector2(0.2f, 0.7f);
                rect.position = new Vector3(410, 270);
                rect.localScale = new Vector3(0.75f, 0.75f, 1);
                rect.localRotation = Quaternion.Euler(0, 0, 27);
                var selector = go.AddComponent<ColorSelector>();
                selector.BeginSession(new Color(1, 0, 0, 0.4f), null);
                var data = Pointer(rect, new Vector2(0, 0), 4);
                selector.OnPointerDown(data);
                data.position = Screen(rect, new Vector2(0, 0.65f));
                selector.OnDrag(data);
                AssertColor(selector.CurrentColor, new Color(1, 0, 0, 0.4f));
                data.position = Screen(rect, new Vector2(-0.5629165f, -0.325f));
                selector.OnDrag(data);
                AssertColor(selector.CurrentColor, new Color(0, 0, 0, 0.4f));
                data.position = Screen(rect, new Vector2(0.5629165f, -0.325f));
                selector.OnDrag(data);
                AssertColor(selector.CurrentColor, new Color(1, 1, 1, 0.4f));
                data.position = Screen(rect, new Vector2(0, -0.9f));
                selector.OnDrag(data);
                AssertColor(selector.CurrentColor, new Color(0.5f, 0.5f, 0.5f, 0.4f));
                selector.OnPointerUp(data);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void HueDragKeepsRegionAndPointerAndCallbackCanReplaceSession()
        {
            var go = new GameObject("YLColor.Reentrant", typeof(RectTransform));
            try
            {
                var selector = go.AddComponent<ColorSelector>();
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(300, 300);
                int old = 0, next = 0;
                selector.BeginSession(Color.red, color =>
                {
                    old++;
                    AssertColor(color, Color.green);
                    selector.BeginSession(Color.blue, _ => next++);
                });
                var data = Pointer(rect, new Vector2(-0.44f, 0.7621024f), 6);
                selector.OnPointerDown(data);
                Assert.That(old, Is.EqualTo(1));
                AssertColor(selector.CurrentColor, Color.blue);
                data.position = Screen(rect, new Vector2(0.3f, 0));
                selector.OnDrag(data);
                AssertColor(selector.CurrentColor, Color.blue);
                Assert.That(next, Is.Zero);
                selector.BeginSession(Color.red, _ => next++);
                selector.OnPointerDown(Pointer(rect, new Vector2(0.88f, 0), 7));
                data.pointerId = 8;
                selector.OnDrag(data);
                AssertColor(selector.CurrentColor, Color.red);
                data.pointerId = 7;
                data.position = Screen(rect, new Vector2(-0.1f, 0.1732051f));
                selector.OnDrag(data);
                AssertColor(selector.CurrentColor, Color.green);
                Assert.That(next, Is.EqualTo(1), "Hue drag remains Hue even after crossing into the triangle.");
                selector.EndSession();
                selector.OnPointerDown(Pointer(rect, new Vector2(0.88f, 0), 9));
                AssertColor(selector.CurrentColor, Color.green);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void WheelMeshUsesSameTriangleAndIndependentVertexColorsNotSharedMaterialMutation()
        {
            var first = new GameObject("YLColor.MeshA", typeof(RectTransform), typeof(ColorWheelGraphic));
            var second = new GameObject("YLColor.MeshB", typeof(RectTransform), typeof(ColorWheelGraphic));
            var mesh = new Mesh();
            try
            {
                var a = first.GetComponent<ColorWheelGraphic>();
                var b = second.GetComponent<ColorWheelGraphic>();
                a.rectTransform.sizeDelta = new Vector2(200, 200);
                var material = a.material;
                a.Hue = 1f / 3;
                using (var helper = new VertexHelper())
                {
                    typeof(ColorWheelGraphic).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                        .Invoke(a, new object[] { helper });
                    helper.FillMesh(mesh);
                }
                var colors = mesh.colors32;
                var vertices = mesh.vertices;
                Assert.That(vertices[vertices.Length - 3], Is.EqualTo(new Vector3(0, 65, 0)));
                Assert.That(colors[colors.Length - 3], Is.EqualTo((Color32)Color.green));
                Assert.That(colors[colors.Length - 2], Is.EqualTo((Color32)Color.black));
                Assert.That(colors[colors.Length - 1], Is.EqualTo((Color32)Color.white));
                Assert.That(b.Hue, Is.Zero);
                Assert.That(a.material, Is.SameAs(material));
                Assert.That(a.material, Is.SameAs(b.material));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [TestCase(1f, 0f, 0f, 0f)]
        [TestCase(0f, 1f, 0f, 0.3333333f)]
        [TestCase(0f, 0f, 1f, 0.6666667f)]
        [TestCase(0f, 0f, 0f, 0f)]
        [TestCase(1f, 1f, 1f, 0f)]
        [TestCase(0.5f, 0.5f, 0.5f, 0f)]
        public void InitialPrimaryAndAchromaticColorsReplacePriorHueAndKeepAlpha(float r, float g, float b, float hue)
        {
            var go = new GameObject("YLColor.InitialEndpoints", typeof(RectTransform));
            try
            {
                var selector = go.AddComponent<ColorSelector>();
                selector.BeginSession(Color.magenta, null);
                var initial = new Color(r, g, b, 0.4f);
                int changes = 0;
                selector.BeginSession(initial, _ => changes++);
                Assert.That(selector.CurrentColor, Is.EqualTo(initial));
                Assert.That(go.GetComponent<ColorWheelGraphic>().Hue, Is.EqualTo(hue).Within(0.001f));
                Assert.That(changes, Is.Zero);
                selector.OnPointerDown(Pointer(go.GetComponent<RectTransform>(), new Vector2(-0.44f, 0.7621024f), 2));
                AssertColor(selector.CurrentColor, r == g && g == b ? initial : new Color(0, 1, 0, 0.4f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void InvalidInitialRGBDoesNotReplaceTheHealthySession()
        {
            var go = new GameObject("YLColor.Invalid", typeof(RectTransform));
            try
            {
                var selector = go.AddComponent<ColorSelector>();
                selector.BeginSession(Color.blue, null);
                Assert.Throws<ArgumentException>(() => selector.BeginSession(new Color(float.NaN, 0, 0), null));
                Assert.Throws<ArgumentException>(() => selector.BeginSession(new Color(1.5f, 0, 0), null));
                Assert.That(selector.CurrentColor, Is.EqualTo(Color.blue));
                go.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
                Assert.Throws<InvalidOperationException>(() => selector.OnPointerDown(new PointerEventData(null)));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static PointerEventData Pointer(RectTransform rect, Vector2 normalized, int id)
            => new PointerEventData(null) { position = Screen(rect, normalized), pointerId = id, button = PointerEventData.InputButton.Left };
        private static Vector2 Screen(RectTransform rect, Vector2 normalized)
            => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center + normalized * (Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f)));
        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }
    }
}
