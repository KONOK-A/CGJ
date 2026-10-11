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
            PanelManager.RegisterRoot(canvas, PanelManager.GetUIType(canvas.transform));
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
