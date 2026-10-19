
namespace YLFramework.StateMachine
{

    public enum CompareType {

        Greater = 0,
        Less,
        Equal,
        NotEqual,

    }

    [System.Serializable]
    public class FSMConditionData
    {

        public float targetValue;

        public string parameterName;

        public CompareType compareType;

    }

}
