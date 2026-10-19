using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Overlap
{
    [CustomEditor(typeof(OverlapBase3D),true)]
    public class OverlapBase3DInpector : UnityEditor.Editor
    {
        // Fix编码
        private List<SerializedProperty> properties = null;

        private OverlapBase3D overlap;

        private bool foldout;

        // Fix编码

        protected virtual void OnEnable()
        {
            properties = serializedObject.GetAllSerializedProperty();
            //mScripts = serializedObject.FindProperty("m_Script");

            overlap = serializedObject.targetObject as OverlapBase3D;
        }


        public override void OnInspectorGUI()
        {

            serializedObject.Update();

            foreach (var property in properties)
            {
                EditorGUI.BeginDisabledGroup(property.IsScript());
                EditorGUILayout.PropertyField(property);
                EditorGUI.EndDisabledGroup();
            }

            serializedObject.ApplyModifiedProperties();

            DrawResult(ref foldout, overlap.Results);

        }

        public static void DrawResult(ref bool resultFoldout, List<OverlapHit<Collider>> Results)
        {
            resultFoldout = EditorGUILayout.Foldout(resultFoldout, "Results");
            if (resultFoldout)
            {
                var oldColor = GUI.color;
                GUI.color = Color.gray;

                if (Results.Count == 0)
                    EditorGUILayout.LabelField("Empty!");

                foreach (var item in Results)
                {
                    EditorGUILayout.LabelField("name", item.collider ? item.collider.name : "Missing Collider");
                    //EditorGUIUtility.PingObject(item.collider);
                    EditorGUILayout.Vector3Field("point", item.point);

                    if (GUILayout.Button("select"))
                        EditorGUIUtility.PingObject(item.collider);

                }
                GUI.color = oldColor;
            }
        }
    }
}
