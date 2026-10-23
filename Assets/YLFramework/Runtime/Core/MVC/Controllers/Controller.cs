using System;

namespace YLFramework.Core
{

    [Obsolete("已过时", true)]
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
        internal bool IsInitializing;
        internal bool IsExiting;

        #region 属性

        [Obsolete("已过时", true)]
        public virtual ControllerType Type => ControllerType.Normal;

        /// <summary>
        /// 拥有者；初始化及退出回调期间有效，移除或初始化失败后为null。
        /// </summary>
        public Module Module
        {
            get; internal set;
        }

        #endregion

        #region 方法
        /// <summary>
        /// 每次有效注册时触发一次；期间可查询自身，但注册尚未提交完成。
        /// </summary>
        public virtual void OnInit()
        {
        }

        /// <summary>
        /// 移除Controller时触发
        /// </summary>
        public virtual void OnExit()
        {
        }

        #endregion

    }

}
