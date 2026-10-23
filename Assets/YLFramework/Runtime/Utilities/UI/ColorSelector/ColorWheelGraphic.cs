using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    /// <summary>独立生成 HSV 色环与三角网格，使用 UGUI 自有材质路径，支持 stencil、RectMask2D 与 alpha。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColorWheelGraphic : MaskableGraphic
    {
        internal const float InnerRadius = 0.76f;
        internal static readonly Vector2 HueVertex = new Vector2(0, 0.65f);
        internal static readonly Vector2 BlackVertex = new Vector2(-0.5629165f, -0.325f);
        internal static readonly Vector2 WhiteVertex = new Vector2(0.5629165f, -0.325f);
        private float _hue;
        public float Hue
        {
            get => _hue;
            set
            {
                if (_hue == value)
                {
                    return;
                }
                _hue = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0)
            {
                return;
            }

            const int Segments = 120;
            for (int i = 0; i < Segments; i++)
            {
                float a = (float)i / Segments;
                float b = (float)(i + 1) / Segments;
                Vector2 p = new Vector2(Mathf.Cos(a * Mathf.PI * 2), Mathf.Sin(a * Mathf.PI * 2));
                Vector2 q = new Vector2(Mathf.Cos(b * Mathf.PI * 2), Mathf.Sin(b * Mathf.PI * 2));
                int start = mesh.currentVertCount;
                var first = Color.HSVToRGB(a, 1, 1) * color;
                var next = Color.HSVToRGB(b, 1, 1) * color;
                mesh.AddVert(rect.center + p * radius, first, Vector2.zero);
                mesh.AddVert(rect.center + p * (radius * InnerRadius), first, Vector2.zero);
                mesh.AddVert(rect.center + q * (radius * InnerRadius), next, Vector2.zero);
                mesh.AddVert(rect.center + q * radius, next, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
            int triangle = mesh.currentVertCount;
            mesh.AddVert(rect.center + HueVertex * radius, Color.HSVToRGB(_hue, 1, 1) * color, Vector2.zero);
            mesh.AddVert(rect.center + BlackVertex * radius, Color.black * color, Vector2.zero);
            mesh.AddVert(rect.center + WhiteVertex * radius, Color.white * color, Vector2.zero);
            mesh.AddTriangle(triangle, triangle + 1, triangle + 2);
        }

        internal static Vector3 Weights(Vector2 point)
        {
            var fromBlack = point - BlackVertex;
            var hueEdge = HueVertex - BlackVertex;
            var whiteEdge = WhiteVertex - BlackVertex;
            float hueWeight = fromBlack.y / hueEdge.y;
            float whiteWeight = (fromBlack.x - hueWeight * hueEdge.x) / whiteEdge.x;
            return new Vector3(hueWeight, 1 - hueWeight - whiteWeight, whiteWeight);
        }

        internal static Vector2 ClampTriangle(Vector2 point)
        {
            var weights = Weights(point);
            if (weights.x >= 0 && weights.y >= 0 && weights.z >= 0)
            {
                return point;
            }

            Vector2 Closest(Vector2 a, Vector2 b) => a + (b - a) * Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude);
            var first = Closest(HueVertex, BlackVertex);
            var second = Closest(BlackVertex, WhiteVertex);
            var third = Closest(WhiteVertex, HueVertex);
            var result = (first - point).sqrMagnitude < (second - point).sqrMagnitude ? first : second;
            return (third - point).sqrMagnitude < (result - point).sqrMagnitude ? third : result;
        }
    }
}
