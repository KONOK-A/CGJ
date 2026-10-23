using System.Collections.Generic;
using UnityEngine.Serialization;

namespace YLFramework.StateMachine
{

    [System.Serializable]
    public class GroupCondition
    {
        public List<FsmConditionData> conditions = new List<FsmConditionData>();
    }

    [System.Serializable]
    public class FsmTransitionData
    {

        public string fromStateName;
        public string toStateName;

        public string Key
        {
            get
            {
                return string.Format("{0}:{1}", fromStateName, toStateName);
            }
        }


        public List<FsmConditionData> conditions = new List<FsmConditionData>();

        [FormerlySerializedAs("group_conditions")] public List<GroupCondition> groupConditions = new List<GroupCondition>();

        [FormerlySerializedAs("AutoSwtich")] public bool autoSwitch = false;

        public bool IsEmpty
        {
            get
            {

                if (conditions.Count != 0)
                {
                    return false;
                }

                foreach (GroupCondition condition in groupConditions)
                {
                    if (condition.conditions.Count != 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

    }

}
