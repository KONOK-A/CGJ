// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Other/Adaptation/AutomaticAdaptationCanvasScaler.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
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

