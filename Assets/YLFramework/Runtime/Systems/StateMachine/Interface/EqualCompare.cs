
namespace YLFramework.StateMachine
{
    public class EqualCompare : IParameterComparer
    {
        public bool Matches(FsmParameterData parameter, float v)
        {
            if (parameter == null)
            {
                return false;
            }

            return parameter.Value.Equals(v);
        }
    }

}
