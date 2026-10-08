using UnityEngine;
using UnityEngine.UI;
using YLFramework.Core.Singleton;

namespace YLFramework.Services.UI
{
    [MonoSingletonPath("YLFramework/UISystem")]
    internal class UIRoot : MonoSingleton<UIRoot>
    {
        [SerializeField] private Camera uiCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        
        public Camera UICamera => uiCamera;

        public void SetResolution(int width, int height, float matchOnWidthOrHeight)
        {
            canvasScaler.referenceResolution = new Vector2(width, height);
            canvasScaler.matchWidthOrHeight = matchOnWidthOrHeight;
        }

        public Vector2 GetResolution()
        {
            return canvasScaler.referenceResolution;
        }

        public float GetMatchOnWidthOrHeight()
        {
            return canvasScaler.matchWidthOrHeight;
        }
        public void ScreenSpaceOverlayRenderMode()
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UICamera.gameObject.SetActive(false);
        }

        public void ScreenSpaceCameraRenderMode()
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            UICamera.gameObject.SetActive(true);
            canvas.worldCamera = uiCamera;
        }
    }
}