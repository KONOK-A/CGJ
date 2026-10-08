// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Other/Adaptation/BorderAdaptation.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
 
using UnityEngine;

namespace YLFramework.Core
{

    public enum BorderType { 
        Left,
        Right
    }

    /// <summary>
    /// 边界适配
    /// </summary>
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

        // Start is called before the first frame update
        void Start()
        {

           float value = Mathf.InverseLerp(1.77f, 2.22f,  (float)Screen.width / Screen.height);

            switch (borderType)
            {
                case BorderType.Left:
                    transform.GetComponent<RectTransform>().anchoredPosition =  new Vector2(Mathf.Lerp(minOffset,maxOffset, value),0);
                    break;
                case BorderType.Right:
                    transform.GetComponent<RectTransform>().anchoredPosition =  new Vector2(-Mathf.Lerp(minOffset, maxOffset, value), 0);
                    break;
            }
        }

    }

}