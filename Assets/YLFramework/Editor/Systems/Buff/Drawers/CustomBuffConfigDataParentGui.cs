using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.BuffSystem
{
    public class CustomBuffConfigDataParentGui : CustomPropertyGui
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
