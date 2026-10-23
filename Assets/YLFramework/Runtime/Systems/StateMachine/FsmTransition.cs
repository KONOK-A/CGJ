using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.StateMachine
{
    /// <summary>
    /// 状态之间的过渡
    /// </summary>
    public class FsmTransition
    {
        #region 字段
        private FsmTransitionData _data;
        private RuntimeFsmControllerInstance _controller;
        private FsmStateNode _toState;

        public List<FsmConditionGroup> ConditionGroups;

        #endregion

        #region 属性

        /// <summary>
        /// 过渡数据
        /// </summary>
        public FsmTransitionData Data => _data;

        /// <summary>
        /// 过渡目标状态
        /// </summary>
        public FsmStateNode ToState => _toState;

        // 条件是否已经满足
        internal bool IsMet
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

                if (!ConditionGroupMeet())
                {
                    return false;
                }

                if (_toState == null)
                {
                    Debug.LogError("查询目标状态失败!");
                    return false;
                }

                FsmStateNodeData from = _controller.RuntimeFsmController.GetStateNodeData(_data.fromStateName);
                if (from == null)
                {
                    return false;
                }

                string parent = from.Parent;
                FsmStateNode currentState = _controller.GetCurrentState(parent);

                if (currentState == null)
                {
                    return false;
                }

                if (!_data.fromStateName.Equals(currentState.Data.name))
                {
                    // AnyState
                    if (!from.IsAnyState)
                    {
                        return false;
                    }

                    // 判断当前的状态 跟 目标状态是不是一个
                    if (currentState.Data.name.Equals(_data.toStateName))
                    {
                        return false;
                    }
                }
                return true;
            }

        }

        #endregion

        #region 方法

        internal FsmTransition(RuntimeFsmControllerInstance controller, FsmTransitionData data)
        {
            this._data = data;
            this._controller = controller;

            //this.conditions = new List<FSMCondition>();

            this.ConditionGroups = new List<FsmConditionGroup>();

            FsmConditionGroup group = new FsmConditionGroup(controller, this._data.conditions, data);
            group.ConditionMet += this.CheckCondition;
            ConditionGroups.Add(group);

            foreach (var item in data.groupConditions)
            {
                FsmConditionGroup conditionGroup = new FsmConditionGroup(controller, item.conditions, data);
                conditionGroup.ConditionMet += this.CheckCondition;
                ConditionGroups.Add(conditionGroup);
            }

            if (controller.States.ContainsKey(data.toStateName))
            {
                _toState = controller.States[data.toStateName];
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
            if (!_controller.HasStarted || _controller.IsResettingTriggers)
            {
                return false;
            }

            bool isMeet = IsMet;
            if (isMeet)
            {
                // reset trigger
                ResetTrigger();
                // 切换状态
                _controller.SwitchState(_toState, this);
            }
            return isMeet;
        }

        private void ResetTrigger()
        {
            _controller.IsResettingTriggers = true;
            try
            {
                foreach (var item in ConditionGroups)
                {
                    item.ResetTrigger();
                }
            }
            finally
            {
                _controller.IsResettingTriggers = false;
            }
        }

        private bool ConditionGroupMeet()
        {
            foreach (var item in ConditionGroups)
            {
                if (item.IsMet)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

    }

}
