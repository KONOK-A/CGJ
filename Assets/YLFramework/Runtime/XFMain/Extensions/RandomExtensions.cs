// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/RandomExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.Extensions
{

    public static class RandomExtensions 
    {

        /// <summary>
        /// 按概率返回 true /false 的随机方法
        /// </summary>
        /// <param name="probability">概率(0-1)</param>
        /// <returns></returns>
        public static bool Random(float probability)
        {
            if (probability >= 1) return true;
            if (probability <= 0) return false;

            int p = Mathf.RoundToInt( probability * 100); 
            return UnityEngine.Random.Range(1, 101) <= p;
        }
    }

}

