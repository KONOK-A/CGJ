// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/RoslynAnalyzers/MethodLocalOnlyAttribute.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers
{
    /// <summary>
    /// 标记某个类只能在方法的内部声明变量，不能作为类的成员变量
    /// </summary>
    public class MethodLocalOnlyAttribute : Attribute
    {

        public MethodLocalOnlyAttribute() { }

        public MethodLocalOnlyAttribute(string message) { }

    }
}

