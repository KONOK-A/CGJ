using System;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    /// <summary>统一 UI 的配置宿主，不拥有 Module、窗口实例或加载请求。</summary>
    public sealed class UIRootContext : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public Transform root;
            public bool enableMask;
            public Image maskImage;
        }

        [SerializeField] private Camera uiCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private Layer[] uiLayers = Array.Empty<Layer>();
        [SerializeField] private RectTransform dragLayer;

        public Canvas Canvas => canvas;
        public Camera UICamera => uiCamera;
        public RectTransform DragLayer => dragLayer;
        internal int OverlayLayer => Math.Max(1, uiLayers.Length);

        private void OnEnable()
        {
            if (!canvas) canvas = GetComponent<Canvas>();
            if (!canvas) throw new InvalidOperationException("UIRootContext requires a root Canvas.");
            if (!canvasScaler) canvasScaler = canvas.GetComponent<CanvasScaler>();
            if (!graphicRaycaster) graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
            // Unity 在 Camera 模式尚未绑定 Camera 时会暂时报告 Overlay，先恢复明确的 Camera 引用。
            if (uiCamera) canvas.worldCamera = uiCamera;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (!uiCamera) uiCamera = canvas.worldCamera;
                if (!uiCamera) throw new InvalidOperationException("Camera UI root requires an explicitly configured Camera.");
                canvas.worldCamera = uiCamera;
                canvas.targetDisplay = uiCamera.targetDisplay;
            }
            PanelManager.RegisterRoot(canvas, PanelManager.GetUIType(canvas.transform));
        }

        /// <summary>显式切换屏幕 UI 模式。不创建 Camera，也不修改 Camera 自身设置；WorldSpace 不在此配置入口内。</summary>
        public void ConfigureCanvas(RenderMode mode, Camera camera = null, float planeDistance = 100)
        {
            if (!canvas) canvas = GetComponent<Canvas>();
            if (!canvas) throw new InvalidOperationException("UIRootContext requires a root Canvas.");
            if (mode != RenderMode.ScreenSpaceOverlay && mode != RenderMode.ScreenSpaceCamera)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == RenderMode.ScreenSpaceCamera && (!camera || float.IsNaN(planeDistance) || float.IsInfinity(planeDistance) ||
                planeDistance <= camera.nearClipPlane || planeDistance >= camera.farClipPlane))
                throw new ArgumentException("Camera UI plane must lie between the configured Camera clipping planes.");
            uiCamera = mode == RenderMode.ScreenSpaceCamera ? camera : null;
            canvas.renderMode = mode;
            canvas.worldCamera = uiCamera;
            if (uiCamera)
            {
                canvas.planeDistance = planeDistance;
                canvas.targetDisplay = uiCamera.targetDisplay;
            }
            MarkLayout();
        }

        /// <summary>参考分辨率和自动/手动匹配由唯一适配组件持有。默认根没有第二份分辨率或每帧覆盖设置。</summary>
        public void ConfigureResolution(Vector2 referenceResolution, bool automaticMatch = true, float manualMatch = 0.5f)
        {
            if (!canvas) canvas = GetComponent<Canvas>();
            if (!canvasScaler && canvas) canvasScaler = canvas.GetComponent<CanvasScaler>();
            if (!canvasScaler) throw new InvalidOperationException("Resolution configuration requires a CanvasScaler.");
            if (!canvasScaler.TryGetComponent<AutomaticAdaptationCanvasScaler>(out var adaptation))
            {
                // 在增加配置组件前校验，非法调用不改变根的组件或缩放模式。
                if (referenceResolution.x <= 0 || referenceResolution.y <= 0 || float.IsNaN(referenceResolution.x) ||
                    float.IsNaN(referenceResolution.y) || float.IsInfinity(referenceResolution.x) || float.IsInfinity(referenceResolution.y) ||
                    float.IsNaN(manualMatch) || manualMatch < 0 || manualMatch > 1)
                    throw new ArgumentException("Reference resolution and match must be finite and valid.");
                adaptation = canvasScaler.gameObject.AddComponent<AutomaticAdaptationCanvasScaler>();
            }
            adaptation.Configure(referenceResolution, automaticMatch, manualMatch);
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            adaptation.Refresh();
            MarkLayout();
        }

        private void MarkLayout()
        {
            if (canvas && canvas.transform is RectTransform rect) LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        public Transform GetLayer(int index)
        {
            if (uiLayers.Length == 0 && index == 0) return canvas.transform;
            if (index < 0 || index >= uiLayers.Length || !uiLayers[index].root)
                throw new ArgumentOutOfRangeException(nameof(index), "Window layer is not configured.");
            return uiLayers[index].root;
        }

        internal bool HasMask(int index) => index >= 0 && index < uiLayers.Length &&
            uiLayers[index].enableMask && uiLayers[index].maskImage;

        internal void RefreshMasks()
        {
            if (!canvas) return;
            for (int i = 0; i < uiLayers.Length; i++)
            {
                var layer = uiLayers[i];
                if (!layer.maskImage) continue;
                var modal = PanelManager.GetLayerModal(canvas, i);
                var session = modal ? modal.Session : null;
                bool show = isActiveAndEnabled && layer.enableMask && modal;
                layer.maskImage.gameObject.SetActive(show);
                layer.maskImage.raycastTarget = show;
                if (!show) continue;
                if (!modal || !modal.IsLoaded || !ReferenceEquals(modal.Session, session)) continue;
                var maskCanvas = layer.maskImage.GetComponent<Canvas>();
                if (!maskCanvas) maskCanvas = layer.maskImage.gameObject.AddComponent<Canvas>();
                if (!layer.maskImage.GetComponent<GraphicRaycaster>()) layer.maskImage.gameObject.AddComponent<GraphicRaycaster>();
                maskCanvas.overrideSorting = true;
                maskCanvas.sortingLayerID = modal.Canvas.sortingLayerID;
                maskCanvas.sortingOrder = modal.DisplaySortingOrder - 1;
            }
        }

        private void OnDisable()
        {
            foreach (var layer in uiLayers)
                if (layer.maskImage) layer.maskImage.gameObject.SetActive(false);
        }
    }
}
