// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/ComponentExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.

using UnityEngine;


namespace YLFramework.Core.Extensions
{
    public static class ComponentExtensions
    {

        // Fix编码


        /// <summary>
        /// 判断组件是否被销毁
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns> 
        public static bool IsDestroy(this Component component)
        {  
            return !component;
        }

    }

}

