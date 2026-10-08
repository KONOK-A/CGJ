// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/CustomClassNameGUI.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;

#if UNITY_EDITOR
namespace YLFramework.Core
{

/// <summary>
/// 用于编辑器的类名自定义显示
/// </summary>
public class CustomClassNameGUI : Attribute
{
    private string name;
    public CustomClassNameGUI(string name) 
    {
        this.name = name;
    } 

    public string GetName() { 
        return name;
    } 
}

}
#endif
