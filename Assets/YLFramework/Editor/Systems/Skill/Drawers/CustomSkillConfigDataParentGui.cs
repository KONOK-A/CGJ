using YLFramework.Foundation;
using YLFramework.Attributes;
#if UNITY_EDITOR

using UnityEditor;


namespace YLFramework.Editor.Skill
{
    public class CustomSkillConfigDataParentGui : CustomPropertyGui
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
