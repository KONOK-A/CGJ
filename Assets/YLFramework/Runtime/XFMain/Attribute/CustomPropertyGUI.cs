// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Attribute/CustomPropertyGUI.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 

namespace YLFramework.Core
{
    
    /// <summary>
    /// 自定义OnGUI(适用于技能系统、Buff系统、装备系统、任务系统、配表系统...))
    /// </summary>
    public abstract class CustomPropertyGUI : Attribute
    {
#if UNITY_EDITOR

        public bool reloadTreeViewOnValueChange;

        /// <summary>
        /// 构造函数
        /// </summary> 
        public CustomPropertyGUI()
        {
            this.reloadTreeViewOnValueChange = false;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="reloadTreeViewOnValueChange">当属性的值改变时,是否重新加载TreeView</param>
        public CustomPropertyGUI(bool reloadTreeViewOnValueChange) 
        {
            this.reloadTreeViewOnValueChange = reloadTreeViewOnValueChange;
        }


        public abstract void OnPropertyGUI(UnityEditor.SerializedProperty serializedProperty);
         
#endif
    }
     
}

