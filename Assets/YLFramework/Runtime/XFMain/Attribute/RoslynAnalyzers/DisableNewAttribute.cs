// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/RoslynAnalyzers/DisableNewAttribute.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.RoslynAnalyzers
{

    /// <summary>
    /// 标记某个类不能通过New创建对象
    /// </summary>
    public class DisableNewAttribute : Attribute
    {

        public DisableNewAttribute() { }

        public DisableNewAttribute(string message) { }

    } 
}

