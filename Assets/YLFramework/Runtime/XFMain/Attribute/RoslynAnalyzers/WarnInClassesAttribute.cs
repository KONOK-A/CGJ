// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/RoslynAnalyzers/WarnInClassesAttribute.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers 
{

    // Fixed 编码

    /// <summary>
    /// 标记某个方法,当该方法在某个类中调用时显示警告信息!
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class WarnInClassesAttribute : Attribute
    {

        public WarnInClassesAttribute(Type type,string message) { }

        public WarnInClassesAttribute(Type[] type, string message) { }

    }
}

