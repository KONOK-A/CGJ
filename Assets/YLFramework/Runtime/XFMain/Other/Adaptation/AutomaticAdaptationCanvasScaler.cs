using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(CanvasScaler))]
    public class AutomaticAdaptationCanvasScaler : MonoBehaviour
    {
        private CanvasScaler scaler;
        private int currentWidth;
        private int currentHeight;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
        }

        // 如果能够动态调整分辨率 这里需要监听对应事件刷新( 暂时不考虑 )
        private void UpdateScreenAspect()
        {
            if (Screen.width == currentWidth && Screen.height == currentHeight)
                return;

            // 计算出比例
            float aspect = (float)Screen.width / Screen.height; // 当前屏幕的宽高比

            float scaler_aspect = scaler.referenceResolution.x / scaler.referenceResolution.y; // 设定的宽高比

            scaler.matchWidthOrHeight = aspect > scaler_aspect ? 1 : 0;


            currentWidth = Screen.width;
            currentHeight = Screen.height;
        }


        private void Update()
        {
            UpdateScreenAspect();
        }

    }
}

