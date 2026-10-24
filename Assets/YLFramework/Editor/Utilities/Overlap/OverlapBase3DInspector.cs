using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Utilities;
using YLFramework.Overlap;

namespace YLFramework.Editor.Overlap
{
    [CustomEditor(typeof(OverlapBase3D), true)]
    public class OverlapBase3DInspector : UnityEditor.Editor
    {
        // Fix编码
        private List<SerializedProperty> _properties = null;

        private OverlapBase3D _overlap;

        private bool _showResults;

        // Fix编码

        protected virtual void OnEnable()
        {
            _properties = serializedObject.GetAllSerializedProperty();
            //mScripts = serializedObject.FindProperty("m_Script");

            _overlap = serializedObject.targetObject as OverlapBase3D;
        }


        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            foreach (var property in _properties)
            {
                EditorGUI.BeginDisabledGroup(property.IsScript());
                EditorGUILayout.PropertyField(property);
                EditorGUI.EndDisabledGroup();
            }

            serializedObject.ApplyModifiedProperties();

            DrawResult(ref _showResults, _overlap.results);

        }

        public static void DrawResult(ref bool resultFoldout, List<OverlapHit<Collider>> results)
        {
            resultFoldout = EditorGUILayout.Foldout(resultFoldout, "Results");
            if (resultFoldout)
            {
                var oldColor = GUI.color;
                GUI.color = Color.gray;

                if (results.Count == 0)
                {
                    EditorGUILayout.LabelField("Empty!");
                }

                foreach (var item in results)
                {
                    EditorGUILayout.LabelField("name", item.collider ? item.collider.name : "Missing Collider");
                    //EditorGUIUtility.PingObject(item.collider);
                    EditorGUILayout.Vector3Field("point", item.point);

                    if (GUILayout.Button("select"))
                    {
                        EditorGUIUtility.PingObject(item.collider);
                    }
                }
                GUI.color = oldColor;
            }
        }
    }
}
