// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Other/Adaptation/BackgroundAdaptation.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
 
using UnityEngine; 

namespace YLFramework.Core
{

    /// <summary>
    /// 背景适配 ( 原理是让当前的游戏物体 与 屏幕大小保持一致 )
    /// </summary>
    public class BackgroundAdaptation : MonoBehaviour
    { 
        private float currentScreenWidth;
        private float currentScreenHeight;

        private float width;
        private float height;

        private Canvas canvas;
        private RectTransform canvasRectTransform;

        private void Awake()
        {
            RectTransform rect = GetComponent<RectTransform>();
            if (rect == null) return;
            width = rect.rect.width;
            height = rect.rect.height;

            canvas = GetComponentInParent<Canvas>();
            canvasRectTransform = canvas.GetComponent<RectTransform>(); 
        }
  
        private void OnEnable()
        {
            Refresh();
        }


        // 如果能够动态调整分辨率 这里需要监听对应事件刷新( 暂时不考虑 )
        private void Refresh()
        {
            if (currentScreenWidth == canvasRectTransform.rect.width && currentScreenHeight == canvasRectTransform.rect.height) 
                return;

            currentScreenWidth = canvasRectTransform.rect.width;
            currentScreenHeight = canvasRectTransform.rect.height;

            float scale_x = currentScreenWidth / width;
            float scale_y = currentScreenHeight / height;
            float s = scale_x > scale_y ? scale_x : scale_y;

            if (s < 1) s = 1; // 不考虑缩小的情况

            transform.localScale = new Vector3(s, s, s);
        }
    }
}

