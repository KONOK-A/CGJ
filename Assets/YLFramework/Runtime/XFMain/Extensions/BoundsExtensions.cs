// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/BoundsExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core.Extensions
{
    public static class BoundsExtensions
    {

        /// <summary>
        /// 判断一个Bounds是否包含另外一个Bounds
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static bool Contains(this Bounds b, Bounds bounds) 
        {
            if (b.min.x <= bounds.min.x && b.min.y <= bounds.min.y && b.min.z <= bounds.min.z &&
                b.max.x >= bounds.max.x && b.max.y >= bounds.max.y && b.max.z >= bounds.max.z)
            {
                return true;
            }

            return false;
        }

    }

}

