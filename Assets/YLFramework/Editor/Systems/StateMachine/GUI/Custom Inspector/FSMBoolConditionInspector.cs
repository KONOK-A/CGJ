using YLFramework.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{

    public class FSMBoolConditionInspector : FSMConditionInspector
    {

        public override void OnGUI(Rect rect, FSMConditionData condition, RuntimeFSMController controller)
        {
            string text = condition.targetValue == 1 ? "True" : "False";
            if (EditorGUI.DropdownButton(rect, new GUIContent(text), FocusType.Keyboard)) {

                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("True"), condition.targetValue == 1, () => {
                    FSMConditionFactory.SetTargetValue(controller, condition, 1);
                });

                //tempContent.text = "False";
                menu.AddItem(new GUIContent("False"), condition.targetValue == 0, () => {
                    FSMConditionFactory.SetTargetValue(controller, condition, 0);
                });

                menu.ShowAsContext();
            }

        }
    }

}
