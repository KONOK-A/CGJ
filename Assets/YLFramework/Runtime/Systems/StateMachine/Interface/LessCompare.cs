
namespace YLFramework.StateMachine
{
    public class LessCompare : IPamaterCompare
    {
        public bool IsMeetCondition(FSMParameterData parameter, float v)
        {
            if (parameter == null) return false;
            return parameter.Value.CompareTo(v) == -1;
        }
    }

}
