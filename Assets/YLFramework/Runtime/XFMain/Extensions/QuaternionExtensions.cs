// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/QuaternionExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core.Extensions
{
    public class QuaternionExtensions 
    {

        /// <summary>
        /// Quaternion反向插值
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="current"></param>
        /// <returns></returns>
        public static float InverseLerp(Quaternion from, Quaternion to, Quaternion current)
        {
            // 转换为角轴便于计算 
            float dot = Quaternion.Dot(from, to);
            if (dot < 0f)
            {
                to = new Quaternion(-to.x, -to.y, -to.z, -to.w);
            }

            // 计算总角度与当前角度（弧度）
            float totalAngle = Mathf.Acos(Mathf.Clamp(Quaternion.Dot(from, to), -1f, 1f));
            float currentAngle = Mathf.Acos(Mathf.Clamp(Quaternion.Dot(from, current), -1f, 1f));

            // 避免除零
            if (totalAngle < Mathf.Epsilon)
                return 0f;

            return currentAngle / totalAngle;
        }

    }

}

