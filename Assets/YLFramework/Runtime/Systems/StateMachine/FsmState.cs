
using UnityEngine;

namespace YLFramework.StateMachine
{
    /// <summary>
    /// 状态基类
    /// </summary>
    public abstract class FsmState
    {

        #region 字段
        /// <summary>
        /// 状态控制器
        /// </summary>
        public FsmController Controller;

        /// <summary>
        /// 当前状态信息
        /// </summary>
        public FsmStateNode CurrentStateInfo;

        /// <summary>
        /// 自定义数据(通过FSMController.StartupFSM()启动状态机传递自定义数据)
        /// </summary>
        public object UserData;

        /// <summary>
        /// 上一个状态的名称
        /// </summary>
        public string LastState;

        /// <summary>
        /// 将要切换的下一个状态的名称(该字段仅在状态将要退出(OnExit)时有值)
        /// </summary>
        public string NextState;

        #endregion


        #region 属性

        public Transform Transform => Controller != null ? Controller.transform : null;

        #endregion

        #region 方法

        /// <summary>
        /// 当第一次进入该状态时调用,该方法仅调用一次(初始化相关操作推荐放该方法中)
        /// </summary>
        public virtual void OnCreate()
        {

        }

        /// <summary>
        /// 当状态进入时执行
        /// </summary>
        public virtual void OnEnter()
        {

        }

        /// <summary>
        /// 当状态退出时执行
        /// </summary>
        public virtual void OnExit()
        {
        }

        /// <summary>
        /// 每帧执行
        /// </summary>
        public virtual void OnUpdate()
        {
        }

        /// <summary>
        /// FixedUpdate时执行
        /// </summary>
        public virtual void OnFixedUpdate()
        {
        }

        /// <summary>
        /// LateUpdate时执行
        /// </summary>
        public virtual void OnLateUpdate()
        {
        }
        #endregion
    }

}
