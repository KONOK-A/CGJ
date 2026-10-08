// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/RoslynAnalyzers/RestrictedCallAttribute.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers
{
    // Fixed 编码

    /// <summary>
    /// 标记某个方法,使其只能由指定的方法调用,否则会报错!
    /// </summary>
    public class RestrictedCallAttribute : Attribute
    {

        /// <summary>
        /// 限制某个方法的调用(比如可以通过这个特性,限制某个方法只能由某些方法调用,否则会报错!)
        /// </summary>
        /// <param name="caller_method">允许调用的方法</param>
        public RestrictedCallAttribute(string caller_method) { }

        /// <summary>
        /// 限制某个方法的调用(比如可以通过这个特性,限制某个方法只能由某些方法调用,否则会报错!)
        /// </summary>
        /// <param name="caller_methods">允许调用的方法</param>
        public RestrictedCallAttribute(string[] caller_methods) { } 
    }

}

