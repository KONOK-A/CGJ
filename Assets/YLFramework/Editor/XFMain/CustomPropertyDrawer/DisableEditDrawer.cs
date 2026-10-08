// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/CustomPropertyDrawer/DisableEditDrawer.cs
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
