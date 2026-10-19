using YLFramework.Core.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class FSMFloatConditionInspector : FSMConditionInspector
    {
        private Rect leftRect;
        private Rect rightRect;

        //private GUIContent tempContent = new GUIContent();

        public override void OnGUI(Rect rect, FSMConditionData condition, RuntimeFSMController controller)
        {

            leftRect.Set(rect.x, rect.y, rect.width / 2, rect.height);
            rightRect.Set(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);

            //tempContent.text = condition.compareType.ToString();
            if (EditorGUI.DropdownButton(leftRect, new GUIContent(condition.compareType.ToString()), FocusType.Keyboard)) {

                GenericMenu menu = new GenericMenu();

                for (int i = 0; i < Enum.GetValues(typeof(CompareType)).Length; i++)
                {
                    CompareType v = (CompareType)Enum.GetValues(typeof(CompareType)).GetValue(i);

                    if (v == CompareType.Equal || v == CompareType.NotEqual) continue;

                    menu.AddItem(new GUIContent(v.ToString()), condition.compareType == v, () => {
                        FSMConditionFactory.SetComparison(controller, condition, v);
                    });

                }

                menu.ShowAsContext();

            }

            FSMConditionFactory.SetTargetValue(controller, condition, EditorGUI.FloatField(rightRect, condition.targetValue));

        }
    }

}
