using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using UnityEditor;


namespace YLFramework.SkillSystem
{
    public class CustomSkillConfigDataParentGUI : CustomPropertyGUI
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