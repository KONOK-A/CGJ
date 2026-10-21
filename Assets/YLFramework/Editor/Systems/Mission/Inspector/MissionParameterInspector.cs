using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.MissionSystem;

namespace YLFramework.MissionSystem
{
public class MissionParameterInspector
{

    private static SerializedObject serializedObject;

    private static MissionsConfig currentMissionConfig;

    private static float update_time;

    private static ParamListTree paramListTree;

    private static int last_index;

    private static ReorderableList reorderableList = null;



    public static void OnInspectorGUI(MissionsConfig missionsConfig ,MissionParameter missionParameter,ParamListTree paramListTree)
    {
        if (missionParameter == null)
        {

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("空");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();

            return;
        }

        int param_index = missionsConfig.param.IndexOf(missionParameter);

        MissionParameterInspector.paramListTree = paramListTree;

        if (currentMissionConfig != missionsConfig)
        {
            serializedObject?.Dispose();
            currentMissionConfig = missionsConfig;
            serializedObject = null;
            reorderableList = null;
        }

        if(serializedObject == null)
            serializedObject = new SerializedObject(missionsConfig);

        serializedObject.Update();

        if (last_index != param_index)
        {
            update_time = Time.realtimeSinceStartup;
            last_index = param_index;
            reorderableList = null;
        }

        SerializedProperty param = serializedObject.FindProperty("param");

        SerializedProperty property = param.GetArrayElementAtIndex(param_index);

        if (property == null) return;



        EditorGUI.BeginChangeCheck();

        //EditorGUILayout.PropertyField(property.FindPropertyRelative("name"));

        OnInspectorID(property.FindPropertyRelative("name"));

        EditorGUILayout.PropertyField(property.FindPropertyRelative("type"));

        EditorGUILayout.PropertyField(property.FindPropertyRelative("only_can_set_in_progress"));

        EditorGUILayout.PropertyField(property.FindPropertyRelative("only_can_set_larger"));

        EditorGUILayout.PropertyField(property.FindPropertyRelative("des"));

        OnInspectorCustomData(property.FindPropertyRelative("custom_data"));


        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(missionsConfig);
        }

        serializedObject.ApplyModifiedProperties();
    }


    private static void OnInspectorID(SerializedProperty data)
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(string.Empty, "参数名称", "BoldLabel");

        string name = data.stringValue;

        EditorGUI.BeginChangeCheck();
        name = EditorGUILayout.DelayedTextField("name", data.stringValue);
        if (EditorGUI.EndChangeCheck() && Time.realtimeSinceStartup - update_time > 1)
        {
            paramListTree.RenameParam(data.stringValue, name);
            paramListTree.Reload();
        }
    }

    private static void OnInspectorCustomData(SerializedProperty data)
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField(string.Empty, "自定义数据(Custom_data)", "BoldLabel");


        if (reorderableList == null)
        {
            reorderableList = new ReorderableList(serializedObject, data);
            reorderableList.headerHeight = 1;
            reorderableList.drawElementCallback = ElementCallbackDelegate;
        }

        reorderableList.DoLayoutList();
    }

    private static void ElementCallbackDelegate(Rect rect, int index, bool isActive, bool isFocused)
    {
        if (index < 0 || index >= reorderableList.serializedProperty.arraySize)
            return;

        SerializedProperty serialized = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
        EditorGUI.PropertyField(rect,serialized);


    }

    internal static void ReleaseSelection()
    {
        serializedObject?.Dispose();
        serializedObject = null;
        currentMissionConfig = null;
        paramListTree = null;
        reorderableList = null;
        last_index = -1;
    }

}

}
