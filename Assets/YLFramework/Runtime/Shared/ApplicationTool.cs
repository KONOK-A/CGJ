// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/xfabmanager.git
// Revision: c35dc4058ecd831409199b43cb328024202d4557 (XFABManager 1.24.0)
// Original path: Runtime/Tools/ApplicationTool.cs
// YL adaptation: namespace only; Editor/Player state semantics retained.
 
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace YLFramework.Foundation
{ 
    /// <summary>
    /// EditorApplication工具
    /// </summary>
    public class ApplicationTool
    { 

#pragma warning disable IDE1006 // 命名样式
        /// <summary>
        /// 判断当前编辑器是否处于运行状态(UnityEngine.Application.isPlaying在停止运行编辑器时触发OnDestroy时仍返回true)
        /// </summary>
        public static bool isPlaying 
        {
            get 
            { 
#if UNITY_EDITOR
                return EditorApplication.isPlayingOrWillChangePlaymode;  // 如果是编辑器模式 使用这个数据
#else  
                return true; // 如果是非编辑器模式一定是true
#endif  
            }

        }
#pragma warning restore IDE1006 // 命名样式 
 
    } 
}
