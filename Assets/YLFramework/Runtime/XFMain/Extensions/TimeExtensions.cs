// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/TimeExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.Extensions
{
    public class TimeExtensions  
    {
        /// <summary>
        /// 获取DeltaTime
        /// </summary>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        public static float GetDeltaTime(bool ignoreTimeScale)
        {
            if (ignoreTimeScale)
            {
                // 大于 0.2 说明这个值有点不正常 使用0.02
                return Time.unscaledDeltaTime > 0.2f ? 0.02f : Time.unscaledDeltaTime;
            }
        
            return Time.deltaTime;
        }

    }
}

