using System;
using UnityEditor;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    public class FsmIntConditionInspector : FsmConditionInspector
    {

        private Rect _leftRect;
        private Rect _rightRect;

        //private GUIContent tempContent = new GUIContent();

        public override void OnGUI(Rect rect, FsmConditionData condition, RuntimeFsmController controller)
        {
            _leftRect.Set(rect.x, rect.y, rect.width / 2, rect.height);
            _rightRect.Set(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);

            //tempContent.text = condition.compareType.ToString();
            if (EditorGUI.DropdownButton(_leftRect, new GUIContent(condition.compareType.ToString()), FocusType.Keyboard))
            {

                GenericMenu menu = new GenericMenu();

                for (int i = 0; i < Enum.GetValues(typeof(CompareType)).Length; i++)
                {
                    CompareType v = (CompareType)Enum.GetValues(typeof(CompareType)).GetValue(i);

                    //if (v == CompareType.Equal || v == CompareType.NotEqual) continue;

                    //tempContent.text = v.ToString();
                    menu.AddItem(new GUIContent(v.ToString()), condition.compareType == v, () =>
                    {
                        FsmConditionFactory.SetComparison(controller, condition, v);
                    });

                }

                menu.ShowAsContext();

            }

            FsmConditionFactory.SetTargetValue(controller, condition, EditorGUI.IntField(_rightRect, (int)condition.targetValue));
        }
    }

}
