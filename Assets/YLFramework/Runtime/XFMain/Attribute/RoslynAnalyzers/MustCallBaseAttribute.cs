// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/RoslynAnalyzers/MustCallBaseAttribute.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
 
namespace YLFramework.Core.RoslynAnalyzers
{

    /// <summary>
    /// 标记某个虚方法,当重写虚方法时必须调用父类的方法
    /// </summary>
    public class MustCallBaseAttribute : Attribute
    {
        
        public MustCallBaseAttribute() { }

        public MustCallBaseAttribute(string message) { }

    }
    
}

