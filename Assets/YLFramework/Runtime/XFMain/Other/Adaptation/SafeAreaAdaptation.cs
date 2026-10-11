using UnityEngine;

namespace YLFramework.Core
{
    /// <summary>显式添加到内容子容器，将屏幕安全区转换为父 RectTransform 内的布局；不改变背景。</summary>
    [DisallowMultipleComponent]
    public class SafeAreaAdaptation : MonoBehaviour
    {
        private RectTransform rect;
        private string lastError;

        /// <summary>按当前设备安全区刷新；失败时保留已有布局并报告一次错误。</summary>
        public bool Refresh() => Refresh(Screen.safeArea, new Vector2(Screen.width, Screen.height));

        /// <summary>
        /// 按指定屏幕尺寸中的安全区刷新，可用于设备布局预览。
        /// 坐标先按 screenSize 归一化，再转换到实际 Canvas 显示尺寸与事件 Camera。
        /// 只支持主显示器的 ScreenSpaceOverlay/Camera 内容子容器；容器保持单位缩放且与 Canvas 轴向一致。
        /// </summary>
        public bool Refresh(Rect safeArea, Vector2 screenSize)
        {
            if (!rect) rect = GetComponent<RectTransform>();
            if (!rect || !(rect.parent is RectTransform parent))
                return Reject("SafeAreaAdaptation requires a RectTransform with a RectTransform parent.");
            var canvas = GetComponentInParent<Canvas>();
            if (!canvas || canvas.transform == transform)
                return Reject("SafeAreaAdaptation requires a parent Canvas; apply it only to a content container.");
            canvas = canvas.rootCanvas;
            if (canvas.renderMode == RenderMode.WorldSpace || canvas.targetDisplay != 0)
                return Reject("SafeAreaAdaptation supports only screen-space UI on the main display.");
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && !camera)
                return Reject("SafeAreaAdaptation requires the screen-space Canvas camera.");
            if (rect.localScale != Vector3.one || rect.localRotation != Quaternion.identity ||
                Quaternion.Inverse(canvas.transform.rotation) * parent.rotation != Quaternion.identity)
                return Reject("SafeAreaAdaptation requires an unscaled content container aligned with the Canvas axes.");
            if (!PositiveFinite(screenSize) || !PositiveFinite(safeArea.size) ||
                !Finite(safeArea.xMin) || !Finite(safeArea.yMin) || !Finite(safeArea.xMax) || !Finite(safeArea.yMax))
                return Reject("SafeAreaAdaptation requires a positive finite screen size and safe area.");
            if (!PositiveFinite(parent.rect.size))
                return Reject("SafeAreaAdaptation requires a positive finite parent size.");

            Vector2 displaySize = canvas.renderingDisplaySize;
            if (camera && camera.targetTexture)
                displaySize = new Vector2(camera.targetTexture.width, camera.targetTexture.height);
            if (!PositiveFinite(displaySize)) return Reject("SafeAreaAdaptation requires a positive finite rendering size.");
            Vector2 screenMin = new Vector2(Mathf.Max(0, safeArea.xMin) / screenSize.x * displaySize.x,
                Mathf.Max(0, safeArea.yMin) / screenSize.y * displaySize.y);
            Vector2 screenMax = new Vector2(Mathf.Min(screenSize.x, safeArea.xMax) / screenSize.x * displaySize.x,
                Mathf.Min(screenSize.y, safeArea.yMax) / screenSize.y * displaySize.y);
            if (screenMax.x <= screenMin.x || screenMax.y <= screenMin.y)
                return Reject("SafeAreaAdaptation safe area does not intersect the screen.");
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMin, camera, out var localMin) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMax, camera, out var localMax))
                return Reject("SafeAreaAdaptation could not convert the safe area to the parent RectTransform.");

            Rect bounds = parent.rect;
            var min = new Vector2(Mathf.Clamp01((localMin.x - bounds.xMin) / bounds.width),
                Mathf.Clamp01((localMin.y - bounds.yMin) / bounds.height));
            var max = new Vector2(Mathf.Clamp01((localMax.x - bounds.xMin) / bounds.width),
                Mathf.Clamp01((localMax.y - bounds.yMin) / bounds.height));
            if (!Finite(min.x) || !Finite(min.y) || !Finite(max.x) || !Finite(max.y) || max.x <= min.x || max.y <= min.y)
                return Reject("SafeAreaAdaptation safe area does not intersect the parent layout.");
            if (rect.anchorMin != min) rect.anchorMin = min;
            if (rect.anchorMax != max) rect.anchorMax = max;
            if (rect.offsetMin != Vector2.zero) rect.offsetMin = Vector2.zero;
            if (rect.offsetMax != Vector2.zero) rect.offsetMax = Vector2.zero;
            lastError = null;
            return true;
        }

        private bool Reject(string error)
        {
            if (lastError != error) Debug.LogError(error, this);
            lastError = error;
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool PositiveFinite(Vector2 value) => value.x > 0 && value.y > 0 && Finite(value.x) && Finite(value.y);

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();
        private void OnTransformParentChanged()
        {
            if (isActiveAndEnabled) Refresh();
        }
    }
}
