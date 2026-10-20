using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;

namespace YLFramework.Platform2D
{

    [CustomEditor(typeof(Moveable), true)]
    public class MoveableInspector : UnityEditor.Editor
    {

        private List<SerializedProperty> serializedProperties;

        //private SerializedProperty m_Script = null;

        private SerializedProperty positionType = null;
        private SerializedProperty transforms = null;
        private SerializedProperty positions = null;

        private SerializedProperty onStartRun = null;
        private SerializedProperty onMove = null;
        private SerializedProperty onPause = null;
        private SerializedProperty onStop = null;

        private ReorderableList reorderableList = null;

        //private bool foldout = false;

        private bool positionsFoldout = false;

        private Moveable moveable;

        private Vector3 snap = new Vector3(0.1f, 0.1f, 0.01f);

        private void OnEnable()
        {
            serializedProperties = serializedObject.GetAllSerializedProperty();
            //m_Script = serializedObject.FindProperty("m_Script");

            positionType = serializedObject.FindProperty("positionType");
            transforms = serializedObject.FindProperty("transforms");
            positions = serializedObject.FindProperty("positions");

            onStartRun = serializedObject.FindProperty("onStartRun");
            onMove = serializedObject.FindProperty("onMove");
            onPause = serializedObject.FindProperty("onPause");
            onStop = serializedObject.FindProperty("onStop");

            reorderableList = new ReorderableList(serializedObject, positions);
            reorderableList.headerHeight = 1;
            reorderableList.drawElementCallback += OnDrawElementCallback;
            reorderableList.onAddCallback += OnAddCallback;
            reorderableList.onSelectCallback += OnSelect;
            moveable = target as Moveable;
        }




        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            //EditorGUI.BeginDisabledGroup(true);
            //EditorGUILayout.PropertyField(m_Script);
            //EditorGUI.EndDisabledGroup();

            foreach (var item in serializedProperties)
            {
                //if (item.displayName == "Script") continue;
                if (item.name == positionType.name) continue;
                if (item.name == transforms.name) continue;
                if (item.name == positions.name) continue;
                if (item.name == onStartRun.name) continue;
                if (item.name == onMove.name) continue;
                if (item.name == onPause.name) continue;
                if (item.name == onStop.name) continue;

                EditorGUI.BeginDisabledGroup(item.IsScript());
                EditorGUILayout.PropertyField(item);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.PropertyField (positionType);

            if (positionType.enumValueIndex == 0)
            {
                positionsFoldout = EditorGUILayout.Foldout(positionsFoldout, "Positions");

                if(positionsFoldout)
                    reorderableList.DoLayoutList();
            }
            else {
                EditorGUILayout.PropertyField(transforms);
            }

            onStartRun.isExpanded = EditorGUILayout.Foldout(onStartRun.isExpanded, "事件");

            if (onStartRun.isExpanded)
            {
                EditorGUILayout.PropertyField(onStartRun);
                EditorGUILayout.PropertyField(onMove);
                EditorGUILayout.PropertyField(onPause);
                EditorGUILayout.PropertyField(onStop);
            }


            serializedObject.ApplyModifiedProperties();
            if (!moveable.ValidatePath(out string error))
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        private void OnDrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            EditorGUI.BeginChangeCheck();

            SerializedProperty property = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
            property.vector3Value = EditorGUI.Vector3Field(rect, "", property.vector3Value);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

        }

        private void OnAddCallback(ReorderableList list)
        {
            Vector3 target = Vector3.zero;

            int size = list.serializedProperty.arraySize;

            list.serializedProperty.InsertArrayElementAtIndex(size);
            SerializedProperty property = list.serializedProperty.GetArrayElementAtIndex(size);

            if (size == 0)
            {
                target = moveable.transform.position;
            }
            else if (size == 1)
            {
                target = list.serializedProperty.GetArrayElementAtIndex(0).vector3Value + Vector3.right;
            }
            else
            {
                SerializedProperty last2 = list.serializedProperty.GetArrayElementAtIndex(size - 2);
                SerializedProperty last1 = list.serializedProperty.GetArrayElementAtIndex(size - 1);

                target =(last1.vector3Value - last2.vector3Value).normalized + last1.vector3Value;
            }

            property.vector3Value = target;
            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {

            if (positionType.enumValueIndex != 0 || reorderableList.index < 0 || reorderableList.index >= positions.arraySize)
                return;

            SerializedProperty property = reorderableList.serializedProperty.GetArrayElementAtIndex(reorderableList.index);

            if (property == null)
                return;

            EditorGUI.BeginChangeCheck();


            float size = HandleUtility.GetHandleSize(property.vector3Value) * 0.1f;

            //Handles.color = Color.green;
            //property.vector3Value = Handles.FreeMoveHandle(property.vector3Value, Quaternion.identity, size, snap, Handles.CubeHandleCap);

            property.vector3Value = Handles.PositionHandle(property.vector3Value, Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                serializedObject.ApplyModifiedProperties();
            }


        }

        private void OnSelect(ReorderableList list)
        {
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Repaint();
        }

    }

}
