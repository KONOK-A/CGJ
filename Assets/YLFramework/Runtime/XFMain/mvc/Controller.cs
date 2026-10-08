// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/mvc/Controller.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 

namespace YLFramework.Core
{

    [Obsolete("已过时",true)]
    public enum ControllerType 
    {
        /// <summary>
        /// 普通类型
        /// </summary>
        Normal,
        /// <summary>
        /// 不能被移除的类型
        /// </summary>
        CannotRemove
    }
     
    public abstract class Controller
    {

        #region 属性

        [Obsolete("已过时",true)]
        public virtual ControllerType Type => ControllerType.Normal;

        /// <summary>
        /// 当前Controller所在的模块
        /// </summary>
        public Module Module { get; internal set; } 

        #endregion

        #region 方法
        /// <summary>
        /// 第一次加载该Controller时触发，仅触发一次
        /// </summary>
        public virtual void OnInit() { }

        /// <summary>
        /// 移除Controller时触发
        /// </summary>
        public virtual void OnExit() { }

        #endregion

    }

}
