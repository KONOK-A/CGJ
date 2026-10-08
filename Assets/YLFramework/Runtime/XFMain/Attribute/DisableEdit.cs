// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/DisableEdit.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using UnityEngine;

namespace YLFramework.Core
{

    /// <summary>
    /// 标记某个字段为不可编辑(字段添加该特性后无法在Inspector面板编辑)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class DisableEdit : PropertyAttribute
    {
        
    }
}

