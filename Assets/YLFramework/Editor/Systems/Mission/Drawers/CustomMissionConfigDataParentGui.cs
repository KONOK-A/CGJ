using YLFramework.Foundation;
using YLFramework.Attributes;
#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;




namespace YLFramework.Editor.Mission
{
    public class CustomMissionConfigDataParentGui : CustomPropertyGui
    {
        public override void OnPropertyGui(SerializedProperty serializedProperty)
        {

            EditorGUI.BeginDisabledGroup(true);

            EditorGUILayout.PropertyField(serializedProperty);

            EditorGUI.EndDisabledGroup();

        }
    }

}

#endif
