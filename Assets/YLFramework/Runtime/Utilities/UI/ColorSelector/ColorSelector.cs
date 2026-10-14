using System;
using UnityEngine;
using UnityEngine.EventSystems;
using YLFramework.UI;

namespace YLFramework.Core
{
    /// <summary>HSV Runtime 控件。初始同步不通知；不编辑 Alpha，回调前先提交颜色和游标状态。</summary>
    [RequireComponent(typeof(ColorWheelGraphic))]
    public sealed class ColorSelector : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        [SerializeField] private RectTransform outerCursor;
        [SerializeField] private RectTransform innerCursor;
        private ColorWheelGraphic wheel;
        private Component inputGateComponent;
        private IUIInputGate inputGate;
        private Action<Color> changed;
        private float hue;
        private float saturation;
        private float value;
        private float alpha;
        private bool session;
        private int? pointer;
        private bool draggingHue;
        public Color CurrentColor { get; private set; }

        /// <summary>替换 SDR 初值和调用方回调；RGB 必须在 [0,1] 内。灰色采用 Hue=0，不继承上一轮任意 Hue。</summary>
        public void BeginSession(Color initial, Action<Color> onChanged)
            => BeginSession(initial, onChanged, GetComponentInParent<IUIInputGate>());

        // 正式窗口显式提供原策略宿主，通用控件不识别或争夺 Panel 的优先级。
        internal void BeginSession(Color initial, Action<Color> onChanged, IUIInputGate gate)
        {
            if (!Finite(initial.r) || !Finite(initial.g) || !Finite(initial.b) || !Finite(initial.a))
                throw new ArgumentException("Color must be finite.", nameof(initial));
            if (initial.r < 0 || initial.r > 1 || initial.g < 0 || initial.g > 1 || initial.b < 0 || initial.b > 1)
                throw new ArgumentException("ColorSelector edits SDR RGB values in [0,1].", nameof(initial));
            wheel = GetComponent<ColorWheelGraphic>();
            inputGate = gate;
            inputGateComponent = inputGate as Component;
            Color.RGBToHSV(initial, out hue, out saturation, out value);
            if (saturation == 0) hue = 0;
            alpha = initial.a;
            CurrentColor = initial;
            changed = onChanged;
            pointer = null;
            session = true;
            UpdateVisual();
        }

        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
        public void EndSession() { session = false; pointer = null; changed = null; inputGate = null; inputGateComponent = null; }
        private void OnDisable() => pointer = null;
        private void OnDestroy() => EndSession();
        private void OnRectTransformDimensionsChange() { if (session && wheel) UpdateVisual(); }

        private bool CanInput => session && isActiveAndEnabled && (!inputGateComponent || inputGate.IsInputAllowed);

        private bool Normalize(PointerEventData data, out Vector2 point)
        {
            point = default;
            var rect = wheel.rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0 || !Finite(radius)) throw new InvalidOperationException("ColorSelector requires a non-degenerate RectTransform.");
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(wheel.rectTransform, data.position, data.pressEventCamera, out var local)) return false;
            point = (local - rect.center) / radius;
            return true;
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
        public void OnPointerDown(PointerEventData data)
        {
            if (!CanInput || pointer.HasValue || data.button != PointerEventData.InputButton.Left || !Normalize(data, out var point)) return;
            float distance = point.magnitude;
            var weights = ColorWheelGraphic.Weights(point);
            bool ring = distance >= ColorWheelGraphic.InnerRadius && distance <= 1;
            if (!ring && (weights.x < 0 || weights.y < 0 || weights.z < 0)) return;
            pointer = data.pointerId;
            draggingHue = ring;
            Select(point);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!CanInput) { pointer = null; return; }
            if (pointer != data.pointerId || !Normalize(data, out var point)) return;
            Select(point);
        }

        public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) pointer = null; }

        private void Select(Vector2 point)
        {
            if (draggingHue)
            {
                if (point.sqrMagnitude < 0.000001f) return;
                hue = Mathf.Repeat(Mathf.Atan2(point.y, point.x) / (Mathf.PI * 2), 1);
            }
            else
            {
                var weights = ColorWheelGraphic.Weights(ColorWheelGraphic.ClampTriangle(point));
                value = Mathf.Clamp01(weights.x + weights.z);
                saturation = value > 0 ? Mathf.Clamp01(weights.x / value) : 0;
            }
            var next = Color.HSVToRGB(hue, saturation, value);
            next.a = alpha;
            bool notify = next != CurrentColor;
            CurrentColor = next;
            UpdateVisual();
            // 回调可以 Close、开启新会话或销毁控件；通知后不再写旧状态。
            if (notify) changed?.Invoke(next);
        }

        private void UpdateVisual()
        {
            wheel.Hue = hue;
            float radius = Mathf.Min(wheel.rectTransform.rect.width, wheel.rectTransform.rect.height) * 0.5f;
            var center = wheel.rectTransform.rect.center;
            if (outerCursor)
                outerCursor.localPosition = center + new Vector2(Mathf.Cos(hue * Mathf.PI * 2), Mathf.Sin(hue * Mathf.PI * 2)) * (radius * 0.88f);
            if (innerCursor)
            {
                float colored = saturation * value;
                innerCursor.localPosition = center + radius * (ColorWheelGraphic.HueVertex * colored +
                    ColorWheelGraphic.BlackVertex * (1 - value) + ColorWheelGraphic.WhiteVertex * (value - colored));
            }
        }
    }
}
