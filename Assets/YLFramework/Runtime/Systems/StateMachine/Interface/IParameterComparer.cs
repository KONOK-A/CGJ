
namespace YLFramework.StateMachine
{
    public interface IParameterComparer
    {
        bool Matches(FsmParameterData parameter, float v);
    }

}
