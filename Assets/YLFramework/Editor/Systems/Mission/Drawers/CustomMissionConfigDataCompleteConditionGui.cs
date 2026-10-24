using YLFramework.Foundation;
using YLFramework.Attributes;
#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.Mission;



namespace YLFramework.Editor.Mission
{
    public class CustomMissionConfigDataCompleteConditionGui : CustomPropertyGui
    {

        private ReorderableList _completeReorderableList = null;

        //private MissionConfigData currentMissionConfigData;

        private MissionConfigData _currentMissionConfigDataRead;

        private GUIContent _paramContent;

        private GUIStyle _conditionStyle;

        private GUIContent _compareContent;

        private GUIContent _valueContent;

        public override void OnPropertyGui(SerializedProperty conditionsProperty)
        {

            if (_completeReorderableList == null)
            {
                _completeReorderableList = new ReorderableList(conditionsProperty.serializedObject, conditionsProperty);
                _completeReorderableList.drawElementCallback += CompleteElementCallbackDelegate;
                _completeReorderableList.drawHeaderCallback += CompleteHeaderCallbackDelegate;

                _currentMissionConfigDataRead = conditionsProperty.serializedObject.targetObject as MissionConfigData;

            }

            EditorGUI.BeginChangeCheck();

            GUILayout.Space(10);

            EditorGUILayout.LabelField(string.Empty, "Conditions(条件)", "BoldLabel");

            _completeReorderableList.DoLayoutList();

            if (EditorGUI.EndChangeCheck())
            {
                conditionsProperty.serializedObject.ApplyModifiedProperties();
            }
        }

        private void CompleteElementCallbackDelegate(Rect rect, int index, bool isActive, bool isFocused)
        {

            if (index < 0 || index >= _completeReorderableList.serializedProperty.arraySize)
            {
                return;
            }

            SerializedProperty condition = _completeReorderableList.serializedProperty.GetArrayElementAtIndex(index);
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

            if (_paramContent == null)
            {
                _paramContent = new GUIContent();
            }

            SerializedProperty paramName = condition.FindPropertyRelative("paramName");
            SerializedProperty compareType = condition.FindPropertyRelative("compareType");
            SerializedProperty target = condition.FindPropertyRelative("target");

            MissionParameter param = _currentMissionConfigDataRead.parent.GetMissionParam(paramName.stringValue);

            //if (param == null)
            //    condition.param_name = string.Empty;

            if (param != null)
            {
                if (string.IsNullOrEmpty(param.description))
                {
                    _paramContent.text = param.name;
                }
                else
                {
                    _paramContent.text = string.Format("{0}<color=grey>({1})</color>", param.name, param.description.Replace("\n", string.Empty));
                }

                _paramContent.tooltip = param.description;
            }
            else
            {
                _paramContent.text = "空";
            }



            if (_conditionStyle == null)
            {
                _conditionStyle = new GUIStyle("DropDown");
                _conditionStyle.richText = true;
            }

            if (EditorGUI.DropdownButton(rect, _paramContent, FocusType.Passive, _conditionStyle))
            {
                PopupWindow.Show(rect, new SelectParamPopWindow(rect.width, _currentMissionConfigDataRead.parent, condition));
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
                if (_compareContent == null)
                {
                    _compareContent = new GUIContent();
                }

                if (compareType.enumValueIndex >= 0 && compareType.enumValueIndex < compareType.enumNames.Length)
                {
                    _compareContent.text = compareType.enumNames[compareType.enumValueIndex];
                }
                else
                {
                    _compareContent.text = "Error";
                }

                if (EditorGUI.DropdownButton(rect, _compareContent, FocusType.Passive, _conditionStyle))
                {
                    //string[] names = Enum.GetNames(typeof(CompareType));
                    GenericMenu menu = new GenericMenu();

                    for (int i = 0; i < compareType.enumNames.Length; i++)
                    {
                        CompareType type = (CompareType)Enum.Parse(typeof(CompareType), compareType.enumNames[i]);

                        if (param.type == MissionParamType.Float && type == CompareType.Equals)
                        {
                            continue;
                        }

                        if (param.type == MissionParamType.Float && type == CompareType.NotEquals)
                        {
                            continue;
                        }

                        int index = i;

                        menu.AddItem(new GUIContent(compareType.enumNames[index]), index == compareType.enumValueIndex, () =>
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

                    if (_valueContent == null)
                    {
                        _valueContent = new GUIContent();
                    }

                    _valueContent.text = (int)target.floatValue == 1 ? "True" : "False";

                    if (EditorGUI.DropdownButton(rect, _valueContent, FocusType.Passive))
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
