
namespace YLFramework.StateMachine
{
    public class EqualCompare : IPamaterCompare
    {
        public bool IsMeetCondition(FSMParameterData parameter, float v)
        {
            if (parameter == null) return false;
            return parameter.Value.Equals(v);
        }
    }

}
