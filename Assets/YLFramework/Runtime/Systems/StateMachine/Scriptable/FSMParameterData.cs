using System;

namespace YLFramework.StateMachine
{

    public enum ParameterType {
        Float = 0,
        Int,
        Bool,
        Trigger
    }

    [System.Serializable]
    public class FSMParameterData
    {
        #region 字段
        public string name;

        public float value;

        public ParameterType parameterType;

        public Action onValueChange;

        public int nameHash;

        #endregion

        #region 属性

        public float Value {
            get { return value; }
            set {

                if (this.value == value) {
                    return;
                }

                this.value = value;
                onValueChange?.Invoke();
            }
        }

        #endregion


    }

}
