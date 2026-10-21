using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;

namespace YLFramework.SkillSystem
{
    public class CustomSkillConfigDataSkillGUI : CustomPropertyGUI
    {

        private bool skillFoldout = true;

        private ReorderableList skillList = null;

        private SerializedProperty serializedProperty;

        private Dictionary<int,GUIContent> sameContents = new Dictionary<int,GUIContent>();

        private SkillsConfig currentSkillConfig;
        private SkillConfigData currentSkillConfigData;

        private List<SkillConfigData> skills = new List<SkillConfigData>();

        public override void OnPropertyGUI(SerializedProperty canbe_released_at_the_same_time_property)
        {
            if (this.serializedProperty != canbe_released_at_the_same_time_property)
            {
                skillList = null;
                this.serializedProperty = canbe_released_at_the_same_time_property;

                currentSkillConfigData = canbe_released_at_the_same_time_property.serializedObject.targetObject as SkillConfigData;
                currentSkillConfig = currentSkillConfigData.parent;
            }


            EditorGUILayout.LabelField(string.Empty, "能够同时释放的技能(默认所有的技能都不能同时释放)", "BoldLabel");
            skillFoldout = EditorGUILayout.Foldout(skillFoldout, "CanbeReleasedAtTheSameTime");
            if (skillFoldout)
            {
                if (skillList == null)
                {
                    skillList = new ReorderableList(canbe_released_at_the_same_time_property.serializedObject,canbe_released_at_the_same_time_property, true, false, true, true);
                    skillList.drawElementCallback += OnDrawRow;
                    skillList.onRemoveCallback += OnRemove;
                    skillList.headerHeight = 1;
                }
                GUILayout.BeginHorizontal();

                GUILayout.Space(15);
                GUILayout.BeginVertical();

                EditorGUI.BeginChangeCheck();
                skillList.DoLayoutList();

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }


        private void OnDrawRow(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= serializedProperty.arraySize)
                return;

            SerializedProperty skill_id_property = serializedProperty.GetArrayElementAtIndex(index);

            int v = skill_id_property.intValue;

            if (!sameContents.ContainsKey(index))
                sameContents.Add(index, new GUIContent());

            GUIContent content = sameContents[index];

            SkillConfigData skillData = currentSkillConfig.GetSkillConfigData(v);


            if (skillData == null)
                content.text = "空";
            else
                content.text = string.Format("{0}({1})", skillData.skill_name, skillData.id);

            if (EditorGUI.DropdownButton(rect, content, FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();

                skills.Clear();

                foreach (var item in currentSkillConfig.skills)
                {
                    if (item == null) continue;
                    if (item.id == currentSkillConfigData.id) continue;
                    if (currentSkillConfigData.canbe_released_at_the_same_time.Contains(item.id))
                        continue;
                    skills.Add(item);
                }

                if (skills.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("无"), false);
                }
                else
                {
                    foreach (var item in skills)
                    {
                        string display = string.Format("{0}({1})", item.skill_name, item.id);
                        menu.AddItem(new GUIContent(display), false, () =>
                        {
                            skill_id_property.intValue = item.id;
                            serializedProperty.serializedObject.ApplyModifiedProperties();
                        });
                    }
                }

                menu.ShowAsContext();
            }
        }

        private void OnRemove(ReorderableList list)
        {
            if (list.index < 0 || list.index >= list.serializedProperty.arraySize)
                return;

            int id = list.serializedProperty.GetArrayElementAtIndex(list.index).intValue;
            list.serializedProperty.DeleteArrayElementAtIndex(list.index);
            list.serializedProperty.serializedObject.ApplyModifiedProperties();
        }



    }

}

#endif
