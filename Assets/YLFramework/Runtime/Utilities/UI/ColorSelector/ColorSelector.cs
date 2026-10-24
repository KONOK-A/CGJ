using System;
using UnityEngine;
using UnityEngine.EventSystems;
using YLFramework.Core;

namespace YLFramework.UI
{
    /// <summary>按需使用的 HSV 取色工具；遵守祖先 Panel 的输入策略，不拥有窗口生命周期。</summary>
    [RequireComponent(typeof(ColorWheelGraphic))]
    public sealed class ColorSelector : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        [SerializeField] private RectTransform outerCursor;
        [SerializeField] private RectTransform innerCursor;
        private ColorWheelGraphic _wheel;
        private Panel _inputPanel;
        private Action<Color> _changed;
        private float _hue;
        private float _saturation;
        private float _value;
        private float _alpha;
        private bool _isSessionActive;
        private int? _pointerId;
        private bool _isDraggingHue;
        public Color CurrentColor
        {
            get; private set;
        }

        /// <summary>替换 SDR 初值和调用方回调；RGB 必须在 [0,1] 内。灰色采用 Hue=0，不继承上一轮任意 Hue。</summary>
        public void BeginSession(Color initial, Action<Color> onChanged)
        {
            if (!IsFinite(initial.r) || !IsFinite(initial.g) || !IsFinite(initial.b) || !IsFinite(initial.a))
            {
                throw new ArgumentException("Color must be finite.", nameof(initial));
            }

            if (initial.r < 0 || initial.r > 1 || initial.g < 0 || initial.g > 1 || initial.b < 0 || initial.b > 1)
            {
                throw new ArgumentException("ColorSelector edits SDR RGB values in [0,1].", nameof(initial));
            }

            _wheel = GetComponent<ColorWheelGraphic>();
            _inputPanel = GetComponentInParent<Panel>();
            Color.RGBToHSV(initial, out _hue, out _saturation, out _value);
            if (_saturation == 0)
            {
                _hue = 0;
            }

            _alpha = initial.a;
            CurrentColor = initial;
            _changed = onChanged;
            _pointerId = null;
            _isSessionActive = true;
            UpdateVisual();
        }

        private static bool IsFinite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
        public void EndSession()
        {
            _isSessionActive = false;
            _pointerId = null;
            _changed = null;
            _inputPanel = null;
        }
        private void OnDisable() => _pointerId = null;
        private void OnDestroy() => EndSession();
        private void OnRectTransformDimensionsChange()
        {
            if (_isSessionActive && _wheel)
            {
                UpdateVisual();
            }
        }

        private bool CanInput => _isSessionActive && isActiveAndEnabled && (!_inputPanel || _inputPanel.IsLoaded && !_inputPanel.IsInputBlocked);

        private bool Normalize(PointerEventData data, out Vector2 point)
        {
            point = default;
            var rect = _wheel.rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0 || !IsFinite(radius))
            {
                throw new InvalidOperationException("ColorSelector requires a non-degenerate RectTransform.");
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_wheel.rectTransform, data.position, data.pressEventCamera, out var local))
            {
                return false;
            }

            point = (local - rect.center) / radius;
            return true;
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
        public void OnPointerDown(PointerEventData data)
        {
            if (!CanInput || _pointerId.HasValue || data.button != PointerEventData.InputButton.Left || !Normalize(data, out var point))
            {
                return;
            }

            float distance = point.magnitude;
            var weights = ColorWheelGraphic.Weights(point);
            bool ring = distance >= ColorWheelGraphic.InnerRadius && distance <= 1;
            if (!ring && (weights.x < 0 || weights.y < 0 || weights.z < 0))
            {
                return;
            }

            _pointerId = data.pointerId;
            _isDraggingHue = ring;
            Select(point);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!CanInput)
            {
                _pointerId = null;
                return;
            }
            if (_pointerId != data.pointerId || !Normalize(data, out var point))
            {
                return;
            }

            Select(point);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (_pointerId == data.pointerId)
            {
                _pointerId = null;
            }
        }

        private void Select(Vector2 point)
        {
            if (_isDraggingHue)
            {
                if (point.sqrMagnitude < 0.000001f)
                {
                    return;
                }

                _hue = Mathf.Repeat(Mathf.Atan2(point.y, point.x) / (Mathf.PI * 2), 1);
            }
            else
            {
                var weights = ColorWheelGraphic.Weights(ColorWheelGraphic.ClampTriangle(point));
                _value = Mathf.Clamp01(weights.x + weights.z);
                _saturation = _value > 0 ? Mathf.Clamp01(weights.x / _value) : 0;
            }
            var next = Color.HSVToRGB(_hue, _saturation, _value);
            next.a = _alpha;
            bool notify = next != CurrentColor;
            CurrentColor = next;
            UpdateVisual();
            // 回调可以 Close、开启新会话或销毁控件；通知后不再写旧状态。
            if (notify)
            {
                _changed?.Invoke(next);
            }
        }

        private void UpdateVisual()
        {
            _wheel.Hue = _hue;
            float radius = Mathf.Min(_wheel.rectTransform.rect.width, _wheel.rectTransform.rect.height) * 0.5f;
            var center = _wheel.rectTransform.rect.center;
            if (outerCursor)
            {
                outerCursor.localPosition = center + new Vector2(Mathf.Cos(_hue * Mathf.PI * 2), Mathf.Sin(_hue * Mathf.PI * 2)) * (radius * 0.88f);
            }

            if (innerCursor)
            {
                float colored = _saturation * _value;
                innerCursor.localPosition = center + radius * (ColorWheelGraphic.HueVertex * colored +
                    ColorWheelGraphic.BlackVertex * (1 - _value) + ColorWheelGraphic.WhiteVertex * (_value - colored));
            }
        }
    }
}
