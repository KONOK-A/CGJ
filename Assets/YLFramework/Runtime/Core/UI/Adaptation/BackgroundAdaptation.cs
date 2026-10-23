using UnityEngine;

namespace YLFramework.Core
{

    /// <summary>按当前 Canvas 与自身尺寸等比放大以覆盖背景，保留不缩小的既有行为。</summary>
    [DisallowMultipleComponent]
    public class BackgroundAdaptation : MonoBehaviour
    {
        private RectTransform _rect;
        private string _lastError;

        /// <summary>重新计算 cover；依赖或尺寸无效时保留原缩放、报告一次错误并返回 false。</summary>
        public bool Refresh()
        {
            if (!_rect)
            {
                _rect = GetComponent<RectTransform>();
            }

            if (!_rect)
            {
                return Reject("BackgroundAdaptation requires a RectTransform.");
            }

            var canvas = GetComponentInParent<Canvas>();
            if (!canvas || canvas.transform == transform)
            {
                return Reject("BackgroundAdaptation requires a parent Canvas.");
            }

            var canvasRect = canvas.transform as RectTransform;
            if (!canvasRect)
            {
                return Reject("BackgroundAdaptation requires a Canvas RectTransform.");
            }

            Vector2 size = canvasRect.rect.size;
            Vector2 contentSize = _rect.rect.size;
            if (!IsPositiveFinite(size) || !IsPositiveFinite(contentSize))
            {
                return Reject("BackgroundAdaptation requires positive finite Canvas and content sizes.");
            }

            float scale = Mathf.Max(1, Mathf.Max(size.x / contentSize.x, size.y / contentSize.y));
            if (float.IsInfinity(scale))
            {
                return Reject("BackgroundAdaptation cover scale is not finite.");
            }

            var target = new Vector3(scale, scale, scale);
            if (_rect.localScale != target)
            {
                _rect.localScale = target;
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

        private static bool IsPositiveFinite(Vector2 value) => value.x > 0 && value.y > 0 &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y);

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
