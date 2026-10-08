// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/ColorExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core.Extensions
{

    public static class ColorExtensions
    {

        /// <summary>
        /// 颜色反向插值
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="current"></param>
        /// <returns></returns>
        public static float InverseLerp(Color from, Color to, Color current)
        {

            Vector4 from_color = new Vector4(from.r, from.g, from.b, from.a);
            Vector4 to_color = new Vector4(to.r, to.g, to.b, to.a); 
            Vector4 current_color = new Vector4(current.r,current.g,current.b,current.a);

            float lerp = Mathf.InverseLerp(from_color.x, to_color.x, current_color.x);

            if (lerp > 0 && lerp <= 1)
            { 
                if (Vector4.Distance(current_color, Vector4.Lerp(from_color, to_color, lerp)) <= 0.01f)
                    return lerp;
            }

            lerp = Mathf.InverseLerp(from_color.y, to_color.y, current_color.y);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector4.Distance(current_color, Vector4.Lerp(from_color, to_color, lerp)) <= 0.01f)
                    return lerp;
            }

            lerp = Mathf.InverseLerp(from_color.z, to_color.z, current_color.z);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector4.Distance(current_color, Vector4.Lerp(from_color, to_color, lerp)) <= 0.01f)
                    return lerp;
            }

            lerp = Mathf.InverseLerp(from_color.w, to_color.w, current_color.w);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector4.Distance(current_color, Vector4.Lerp(from_color, to_color, lerp)) <= 0.01f)
                    return lerp;
            }

            return 0;
        }

    }

}

