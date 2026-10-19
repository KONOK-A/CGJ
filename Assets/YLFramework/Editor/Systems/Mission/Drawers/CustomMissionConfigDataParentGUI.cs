// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;




namespace YLFramework.MissionSystem
{
    public class CustomMissionConfigDataParentGUI : CustomPropertyGUI
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
