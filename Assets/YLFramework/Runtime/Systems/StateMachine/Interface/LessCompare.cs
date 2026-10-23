
namespace YLFramework.StateMachine
{
    public class LessCompare : IParameterComparer
    {
        public bool Matches(FsmParameterData parameter, float v)
        {
            if (parameter == null)
            {
                return false;
            }

            return parameter.Value.CompareTo(v) == -1;
        }
    }

}
