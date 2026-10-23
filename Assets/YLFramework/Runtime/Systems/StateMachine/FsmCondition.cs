using System;
using System.Collections.Generic;


namespace YLFramework.StateMachine
{

    public enum ConditionState
    {
        // 满足
        Meet,
        // 未满足
        NotMeet

    }

    public class FsmCondition
    {

        #region 字段
        private FsmConditionData _data;

        private FsmParameterData _parameter;

        public Action ConditionMet;

        private static Dictionary<CompareType, IParameterComparer> _compares = null;

        #endregion

        #region 属性
        public ConditionState State
        {
            get
            {
                if (Compare != null && Compare.Matches(this._parameter, _data.targetValue))
                {
                    return ConditionState.Meet;
                }
                else
                {
                    return ConditionState.NotMeet;
                }
            }
        }

        private IParameterComparer Compare => GetCompare(_data.compareType);

        public FsmParameterData Parameter => _parameter;


        private static Dictionary<CompareType, IParameterComparer> Compares
        {
            get
            {
                if (_compares == null)
                {
                    _compares = new Dictionary<CompareType, IParameterComparer>();
                    _compares.Add(CompareType.Equal, new EqualCompare());
                    _compares.Add(CompareType.Greater, new GreaterCompare());
                    _compares.Add(CompareType.Less, new LessCompare());
                    _compares.Add(CompareType.NotEqual, new NotEqualCompare());
                }

                return _compares;
            }

        }

        #endregion

        public FsmCondition(FsmConditionData data, RuntimeFsmControllerInstance controller)
        {
            this._data = data;

            int parameterNameHash = FsmController.StringToHash(data.parameterName);

            if (controller.Parameters.ContainsKey(parameterNameHash))
            {
                _parameter = controller.Parameters[parameterNameHash];
            }

            if (_parameter != null)
            {
                _parameter.ValueChanged += CheckParameterValueChange;
            }


            // 检测条件
            CheckParameterValueChange();

        }

        public void CheckParameterValueChange()
        {
            // 判断条件有没有满足
            if (State == ConditionState.Meet)
            {
                ConditionMet?.Invoke();
            }
        }

        public static IParameterComparer GetCompare(CompareType type)
        {
            IParameterComparer pamaterCompare;
            if (Compares.TryGetValue(type, out pamaterCompare))
            {
                return pamaterCompare;
            }
            return null;
        }

    }


    public class FsmConditionGroup
    {

        private List<FsmCondition> _conditions = new List<FsmCondition>();

        public Action ConditionMet;

        public bool IsMet
        {
            get
            {
                // 当前过渡没有条件并且自动切换时，直接返回true，已满足
                if (_transition.IsEmpty && _transition.autoSwitch)
                {
                    return true;
                }

                if (ConditionCount == 0)
                {
                    return false;
                }

                foreach (var item in _conditions)
                {
                    if (item.State == ConditionState.NotMeet)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public int ConditionCount
        {
            get
            {
                if (_conditions == null)
                {
                    return 0;
                }

                return _conditions.Count;
            }
        }

        private RuntimeFsmControllerInstance _controller;

        private FsmTransitionData _transition;

        public FsmConditionGroup(RuntimeFsmControllerInstance controller, List<FsmConditionData> conditions, FsmTransitionData transition)
        {
            this._controller = controller;
            this._transition = transition;
            this._conditions.Clear();
            foreach (var item in conditions)
            {
                FsmCondition condition = new FsmCondition(item, controller);
                condition.ConditionMet += this.CheckIsMeet;
                this._conditions.Add(condition);
            }
        }


        //private void OnConditionMeet()
        //{
        //    //Debug.Log("条件满足!");
        //    DelayTools.DelayCall(CheckIsMeet);
        //}

        private void CheckIsMeet()
        {
            if (IsMet)
            {
                ConditionMet?.Invoke();
            }
        }

        internal void ResetTrigger()
        {
            foreach (var item in _conditions)
            {
                if (item.Parameter != null && item.Parameter.parameterType == ParameterType.Trigger)
                {
                    _controller.ClearTrigger(item.Parameter.nameHash);
                }
            }
        }

    }

}
