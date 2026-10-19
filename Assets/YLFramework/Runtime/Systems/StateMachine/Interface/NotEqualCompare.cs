
namespace YLFramework.StateMachine
{
    public class NotEqualCompare : IPamaterCompare
    {
        public bool IsMeetCondition(FSMParameterData parameter, float v)
        {
            if (parameter == null) return false;
            return !parameter.Value.Equals(v);
        }
    }

}
