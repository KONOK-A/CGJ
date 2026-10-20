// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
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
    public class CustomBuffConfigDataParentGUI : CustomPropertyGUI
    {
        public override void OnPropertyGUI(SerializedProperty serializedProperty)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedProperty);
            EditorGUI.EndDisabledGroup();
        }
    }

}



#endif
