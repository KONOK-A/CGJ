using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Overlap
{
    [CustomEditor(typeof(OverlapBase2D),true)]
    public class OverlapBase2DInpector : UnityEditor.Editor
    {

        private List<SerializedProperty> properties = null;


        private OverlapBase2D overlap;

        private bool foldout;

        // Fix编码

        protected virtual void OnEnable() {
            properties = serializedObject.GetAllSerializedProperty();

            overlap = serializedObject.targetObject as OverlapBase2D;
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

        public static void DrawResult(ref bool resultFoldout,List<OverlapHit<Collider2D>> Results) {
            resultFoldout = EditorGUILayout.Foldout(resultFoldout, "Results");
            if (resultFoldout)
            {
                var oldColor = GUI.color;
                GUI.color = Color.gray;

                if ( Results.Count == 0)
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
