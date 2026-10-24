using UnityEditor;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{

    public class FsmBoolConditionInspector : FsmConditionInspector
    {

        public override void OnGUI(Rect rect, FsmConditionData condition, RuntimeFsmController controller)
        {
            string text = condition.targetValue == 1 ? "True" : "False";
            if (EditorGUI.DropdownButton(rect, new GUIContent(text), FocusType.Keyboard))
            {

                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("True"), condition.targetValue == 1, () =>
                {
                    FsmConditionFactory.SetTargetValue(controller, condition, 1);
                });

                //tempContent.text = "False";
                menu.AddItem(new GUIContent("False"), condition.targetValue == 0, () =>
                {
                    FsmConditionFactory.SetTargetValue(controller, condition, 0);
                });

                menu.ShowAsContext();
            }

        }
    }

}
