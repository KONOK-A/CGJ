using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.Overlap
{
    [CustomEditor(typeof(OverlapBase2D), true)]
    public class OverlapBase2DInspector : UnityEditor.Editor
    {

        private List<SerializedProperty> _properties = null;


        private OverlapBase2D _overlap;

        private bool _showResults;

        // Fix编码

        protected virtual void OnEnable()
        {
            _properties = serializedObject.GetAllSerializedProperty();

            _overlap = serializedObject.targetObject as OverlapBase2D;
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

        public static void DrawResult(ref bool resultFoldout, List<OverlapHit<Collider2D>> results)
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
