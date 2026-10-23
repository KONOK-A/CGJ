using UnityEngine;

namespace YLFramework.Core
{

    public enum BorderType
    {
        Left,
        Right
    }

    /// <summary>
    /// 按宽高比调整左右边距，不改变垂直位置；不是 Safe Area。
    /// </summary>
    [DisallowMultipleComponent]
    public class BorderAdaptation : MonoBehaviour
    {
        /// <summary>
        /// 边界类型
        /// </summary>
        [SerializeField]
        private BorderType borderType = BorderType.Right;

        /// <summary>
        /// 最小偏移
        /// </summary>
        [SerializeField]
        private float minOffset = 0;

        /// <summary>
        /// 最大偏移
        /// </summary>
        [SerializeField]
        private float maxOffset = 100;

        [SerializeField, Tooltip("达到最小偏移的 Canvas 宽高比；无 Canvas 时使用屏幕尺寸。")]
        private float minAspect = 1.77f;
        [SerializeField, Tooltip("达到最大偏移的 Canvas 宽高比，必须大于最小宽高比。")]
        private float maxAspect = 2.22f;
        private RectTransform _rect;
        private string _lastError;

        /// <summary>刷新水平边距，保留 Y；非法配置或尺寸报告一次错误并返回 false。</summary>
        public bool Refresh()
        {
            if (!_rect)
            {
                _rect = GetComponent<RectTransform>();
            }

            if (!_rect)
            {
                return Reject("BorderAdaptation requires a RectTransform.");
            }

            if (!IsFinite(minAspect) || !IsFinite(maxAspect) || minAspect <= 0 || maxAspect <= minAspect ||
                !IsFinite(minOffset) || !IsFinite(maxOffset) || maxOffset < minOffset ||
                (borderType != BorderType.Left && borderType != BorderType.Right))
            {
                return Reject("BorderAdaptation requires increasing positive aspect bounds and finite ordered offsets.");
            }

            var canvas = GetComponentInParent<Canvas>();
            Vector2 size = canvas ? ((RectTransform)canvas.transform).rect.size : new Vector2(Screen.width, Screen.height);
            if (!IsFinite(size.x) || !IsFinite(size.y) || size.x <= 0 || size.y <= 0)
            {
                return Reject("BorderAdaptation requires a positive finite Canvas or screen size.");
            }

            float value = Mathf.InverseLerp(minAspect, maxAspect, size.x / size.y);
            float offset = Mathf.Lerp(minOffset, maxOffset, value);
            Vector2 position = _rect.anchoredPosition;
            position.x = borderType == BorderType.Left ? offset : -offset;
            if (_rect.anchoredPosition != position)
            {
                _rect.anchoredPosition = position;
            }

            _lastError = null;
            return true;
        }

        private bool Reject(string error)
        {
            if (_lastError != error)
            {
                Debug.LogError(error, this);
            }

            _lastError = error;
            return false;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void Start() => Refresh();
        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        private void OnTransformParentChanged()
        {
            if (isActiveAndEnabled)
            {
                Refresh();
            }
        }
    }

}
