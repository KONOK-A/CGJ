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

    public class FSMCondition
    {

        #region 字段
        private FSMConditionData data;

        private FSMParameterData parameter;

        public Action onConditionMeet;

        private static Dictionary<CompareType, IPamaterCompare> compares = null;

        #endregion

        #region 属性
        public ConditionState State
        {
            get
            {
                if (compare != null && compare.IsMeetCondition(this.parameter, data.targetValue))
                    return ConditionState.Meet;
                else
                    return ConditionState.NotMeet;
            }
        }

        private IPamaterCompare compare => GetCompare(data.compareType);

        public FSMParameterData Parameter => parameter;


        private static Dictionary<CompareType, IPamaterCompare> Compares
        {
            get
            {
                if (compares == null)
                {
                    compares = new Dictionary<CompareType, IPamaterCompare>();
                    compares.Add(CompareType.Equal, new EqualCompare());
                    compares.Add(CompareType.Greater, new GreaterCompare());
                    compares.Add(CompareType.Less, new LessCompare());
                    compares.Add(CompareType.NotEqual, new NotEqualCompare());
                }

                return compares;
            }

        }

        #endregion

        public FSMCondition(FSMConditionData data, RuntimeFSMControllerInstance controller) {
            this.data = data;

            int parameter_name_hash = FSMController.StringToHash(data.parameterName);

            if (controller.parameters.ContainsKey(parameter_name_hash))
            {
                parameter = controller.parameters[parameter_name_hash];
            }

            if (parameter != null)
                parameter.onValueChange += CheckParameterValueChange;


            // 检测条件
            CheckParameterValueChange();

        }

        public void CheckParameterValueChange()
        {
            // 判断条件有没有满足
            if (State == ConditionState.Meet)
                onConditionMeet?.Invoke();
        }

        public static IPamaterCompare GetCompare(CompareType type) {
            IPamaterCompare pamaterCompare;
            if ( Compares.TryGetValue(type,out pamaterCompare) ) {
                return pamaterCompare;
            }
            return null;
        }

    }


    public class FSMConditionGroup
    {

        private List<FSMCondition> conditions = new List<FSMCondition>();

        public Action onConditionMeet;

        public bool IsMeet
        {
            get
            {
                // 当前过渡没有条件并且自动切换时，直接返回true，已满足
                if (transition.Empty && transition.AutoSwtich)
                    return true;

                if (ConditionCount == 0)
                    return false;

                foreach (var item in conditions)
                {
                    if (item.State == ConditionState.NotMeet)
                        return false;
                }

                return true;
            }
        }

        public int ConditionCount
        {
            get
            {
                if (conditions == null)
                    return 0;
                return conditions.Count;
            }
        }

        private RuntimeFSMControllerInstance controller;

        private FSMTransitionData transition;

        public FSMConditionGroup(RuntimeFSMControllerInstance controller, List<FSMConditionData> conditions,FSMTransitionData transition)
        {
            this.controller = controller;
            this.transition = transition;
            this.conditions.Clear();
            foreach (var item in conditions)
            {
                FSMCondition condition = new FSMCondition(item, controller);
                condition.onConditionMeet += this.CheckIsMeet;
                this.conditions.Add(condition);
            }
        }


        //private void OnConditionMeet()
        //{
        //    //Debug.Log("条件满足!");
        //    DelayTools.DelayCall(CheckIsMeet);
        //}

        private void CheckIsMeet()
        {
            if (IsMeet)
                onConditionMeet?.Invoke();
        }

        internal void ResetTrigger() {
            foreach (var item in conditions)
            {
                if (item.Parameter != null && item.Parameter.parameterType == ParameterType.Trigger)
                    controller.ClearTrigger(item.Parameter.nameHash);
            }
        }

    }

}
