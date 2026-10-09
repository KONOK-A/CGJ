using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace YLFramework.Core.Editor
{
    [CustomPropertyDrawer(typeof(YLFramework.Core.DisableEdit))]
    public class DisableEditDrawer : PropertyDrawer
    {


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            EditorGUI.BeginDisabledGroup(true); 
            EditorGUI.PropertyField(position, property, label); 
            EditorGUI.EndDisabledGroup();

        }

    }
}
