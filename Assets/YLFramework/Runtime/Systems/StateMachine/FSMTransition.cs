using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.StateMachine
{
    /// <summary>
    /// 状态之间的过渡
    /// </summary>
    public class FSMTransition
    {
        #region 字段
        private FSMTransitionData data;
        private RuntimeFSMControllerInstance controller;
        private FSMStateNode toState;

        public List<FSMConditionGroup> condition_groups;

        #endregion

        #region 属性

        /// <summary>
        /// 过渡数据
        /// </summary>
        public FSMTransitionData Data => data;

        /// <summary>
        /// 过渡目标状态
        /// </summary>
        public FSMStateNode ToState => toState;

        // 条件是否已经满足
        internal bool IsMeet
        {
            get
            {
                // 如果过渡没有条件认为条件满足,可以切换状态
                //if (conditions.Count == 0) return true;

                // 判断是不是所有的条件都满足了
                //foreach (var item in conditions)
                //{
                //    if (item.state == ConditionState.NotMeet) return false;
                //}

                if(!ConditionGroupMeet())
                    return false;

                if (toState == null) {
                    Debug.LogError("查询目标状态失败!");
                    return false;
                }

                FSMStateNodeData from = controller.RuntimeFSMController.GetStateNodeData(data.fromStateName);
                if (from == null) return false;
                string parent = from.Parent;
                FSMStateNode currentState = controller.GetCurrentState(parent);

                if (currentState == null) return false;

                if (!data.fromStateName.Equals(currentState.data.name))
                {
                    // AnyState
                    if (!from.IsAnyState) return false;

                    // 判断当前的状态 跟 目标状态是不是一个
                    if (currentState.data.name.Equals(data.toStateName)) return false;
                }
                return true;
            }

        }

        #endregion

        #region 方法

        internal FSMTransition(RuntimeFSMControllerInstance controller, FSMTransitionData data) {
            this.data = data;
            this.controller = controller;

            //this.conditions = new List<FSMCondition>();

            this.condition_groups = new List<FSMConditionGroup>();

            FSMConditionGroup group = new FSMConditionGroup(controller, this.data.conditions,data);
            group.onConditionMeet += this.CheckCondition;
            condition_groups.Add(group);

            foreach (var item in data.group_conditions)
            {
                FSMConditionGroup condition_group = new FSMConditionGroup(controller, item.conditions,data);
                condition_group.onConditionMeet += this.CheckCondition;
                condition_groups.Add(condition_group);
            }

            if ( controller.states.ContainsKey(data.toStateName) ) {
                toState = controller.states[data.toStateName];
            }

        }

        // 检测条件是否满足
        private void CheckCondition()
        {
            CheckConditionAndSwitch();
        }

        // 检测条件是不是都满足了
        internal bool CheckConditionAndSwitch()
        {
            if (!controller.Started || controller.ResettingTriggers) return false;
            bool isMeet = IsMeet;
            if (isMeet)
            {
                // reset trigger
                ResetTrigger();
                // 切换状态
                controller.SwitchState(toState,this);
            }
            return isMeet;
        }

        private void ResetTrigger()
        {
            controller.ResettingTriggers = true;
            try { foreach (var item in condition_groups) item.ResetTrigger(); }
            finally { controller.ResettingTriggers = false; }
        }

        private bool ConditionGroupMeet()
        {
            foreach (var item in condition_groups)
            {
                if (item.IsMeet)
                    return true;
            }
            return false;
        }

        #endregion

    }

}
