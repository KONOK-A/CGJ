// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/Vector2Extensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core.Extensions
{

    public class Vector2Extensions  
    {

        /// <summary>
        /// 反向插值
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="current"></param>
        /// <returns></returns>
        public static float InverseLerp(Vector2 from, Vector2 to, Vector2 current)
        {
            float lerp = Mathf.InverseLerp(from.x, to.x, current.x);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector2.Distance(current, Vector2.Lerp(from, to, lerp)) <= 0.01f)
                    return lerp;
            }

            lerp = Mathf.InverseLerp(from.y, to.y, current.y);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector2.Distance(current, Vector2.Lerp(from, to, lerp)) <= 0.01f)
                    return lerp;
            }
              
            return 0;
        }

    }
}