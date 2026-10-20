using System.Collections.Generic;

namespace YLFramework.StateMachine
{

    [System.Serializable]
    public class GroupCondition
    {
        public List<FSMConditionData> conditions = new List<FSMConditionData>();
    }

    [System.Serializable]
    public class FSMTransitionData
    {

        public string fromStateName;
        public string toStateName;

        public string Key
        {
            get {
                return string.Format("{0}:{1}",fromStateName,toStateName);
            }
        }


        public List<FSMConditionData> conditions = new List<FSMConditionData>();

        public List<GroupCondition> group_conditions = new List<GroupCondition>();

        public bool AutoSwtich = false;

        public bool Empty
        {
            get {

                if(conditions.Count != 0)
                    return false;

                foreach (GroupCondition condition in group_conditions) {
                    if(condition.conditions.Count != 0)
                        return false;
                }

                return true;
            }
        }

    }

}
