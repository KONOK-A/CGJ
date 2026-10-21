using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;



namespace YLFramework.MissionSystem
{
    public class CustomMissionConfigDataCompleteConditionGUI : CustomPropertyGUI
    {

        private ReorderableList completeReorderableList = null;

        //private MissionConfigData currentMissionConfigData;

        private MissionConfigData currentMissionConfigDataRead;

        private GUIContent paramContent;

        private GUIStyle condition_style;

        private GUIContent compareContent;

        private GUIContent valueContent;

        public override void OnPropertyGUI(SerializedProperty conditions_property)
        {

            if (completeReorderableList == null)
            {
                completeReorderableList = new ReorderableList(conditions_property.serializedObject, conditions_property);
                completeReorderableList.drawElementCallback += CompleteElementCallbackDelegate;
                completeReorderableList.drawHeaderCallback += CompleteHeaderCallbackDelegate;

                currentMissionConfigDataRead = conditions_property.serializedObject.targetObject as MissionConfigData;

            }

            EditorGUI.BeginChangeCheck();

            GUILayout.Space(10);

            EditorGUILayout.LabelField(string.Empty, "Conditions(条件)", "BoldLabel");

            completeReorderableList.DoLayoutList();

            if (EditorGUI.EndChangeCheck())
            {
                conditions_property.serializedObject.ApplyModifiedProperties();
            }
        }

        private void CompleteElementCallbackDelegate(Rect rect, int index, bool isActive, bool isFocused)
        {

            if (index < 0 || index >= completeReorderableList.serializedProperty.arraySize)
            {
                return;
            }

            SerializedProperty condition = completeReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            DrawCondition(condition, rect);
        }

        private void CompleteHeaderCallbackDelegate(Rect rect)
        {
            GUI.Label(rect, "完成任务条件");
        }

        private void DrawCondition(SerializedProperty condition, Rect rect)
        {
            rect.width /= 2;

            //rect.height -= 10;

            if (paramContent == null)
                paramContent = new GUIContent();

            SerializedProperty param_name = condition.FindPropertyRelative("param_name");
            SerializedProperty compareType = condition.FindPropertyRelative("compareType");
            SerializedProperty target = condition.FindPropertyRelative("target");

            MissionParameter param = currentMissionConfigDataRead.parent.GetMissionParam( param_name.stringValue);

            //if (param == null)
            //    condition.param_name = string.Empty;

            if (param != null)
            {
                if (string.IsNullOrEmpty(param.des))
                    paramContent.text = param.name;
                else
                    paramContent.text = string.Format("{0}<color=grey>({1})</color>", param.name, param.des.Replace("\n", string.Empty));

                paramContent.tooltip = param.des;
            }
            else
            {
                paramContent.text = "空";
            }



            if (condition_style == null)
            {
                condition_style = new GUIStyle("DropDown");
                condition_style.richText = true;
            }

            if (EditorGUI.DropdownButton(rect, paramContent, FocusType.Passive, condition_style))
            {
                PopupWindow.Show(rect, new SelectParamPopWindow(rect.width, currentMissionConfigDataRead.parent, condition));
            }

            rect.x += rect.width;

            if (param == null)
            {

                GUI.Label(rect, EditorGUIUtility.TrTempContent("请选择参数!"));
                return;
            }

            if (param.type != MissionParamType.Bool)
            {
                rect.width /= 2;

                // 先绘制比较方式
                if (compareContent == null)
                    compareContent = new GUIContent();

                if(compareType.enumValueIndex >= 0 && compareType.enumValueIndex < compareType.enumNames.Length)
                    compareContent.text = compareType.enumNames[compareType.enumValueIndex];
                else
                    compareContent.text = "Error";

                if (EditorGUI.DropdownButton(rect, compareContent, FocusType.Passive, condition_style))
                {
                    //string[] names = Enum.GetNames(typeof(CompareType));
                    GenericMenu menu = new GenericMenu();

                    for (int i = 0; i < compareType.enumNames.Length; i++)
                    {
                        CompareType type = (CompareType)Enum.Parse(typeof(CompareType), compareType.enumNames[i]);

                        if (param.type == MissionParamType.Float && type == CompareType.Equals) continue;
                        if (param.type == MissionParamType.Float && type == CompareType.NotEquals) continue;

                        int index = i;

                        menu.AddItem(new GUIContent(compareType.enumNames[index]), index ==  compareType.enumValueIndex, () =>
                        {
                            compareType.enumValueIndex = index;
                            condition.serializedObject.ApplyModifiedProperties();
                            AssetDatabase.SaveAssets();
                        });
                    }

                    menu.ShowAsContext();
                }

                rect.x += rect.width;
            }

            switch (param.type)
            {
                case MissionParamType.Float:
                    target.floatValue = EditorGUI.FloatField(rect, target.floatValue);
                    break;
                case MissionParamType.Int:
                    target.floatValue = EditorGUI.IntField(rect, (int)target.floatValue);
                    break;
                case MissionParamType.Bool:

                    if (valueContent == null)
                        valueContent = new GUIContent();

                    valueContent.text = (int)target.floatValue == 1 ? "True" : "False";

                    if (EditorGUI.DropdownButton(rect, valueContent, FocusType.Passive))
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("True"), (int)target.floatValue == 1, () =>
                        {
                            target.floatValue = 1;
                            condition.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("False"), (int)target.floatValue != 1, () =>
                        {
                            target.floatValue = 0;
                            condition.serializedObject.ApplyModifiedProperties();
                        });

                        menu.ShowAsContext();
                    }

                    break;
            }
        }

    }

}

#endif
