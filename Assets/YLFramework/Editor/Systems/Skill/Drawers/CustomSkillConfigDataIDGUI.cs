using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.SkillSystem
{
    public class CustomSkillConfigDataIDGUI : CustomPropertyGUI
    {

        public CustomSkillConfigDataIDGUI() : base(true) { }

        public override void OnPropertyGUI(SerializedProperty serializedProperty)
        {

            EditorGUILayout.LabelField(string.Empty, "技能id(唯一标识,不可重复)", "BoldLabel");
            int id = serializedProperty.intValue;
            EditorGUI.BeginChangeCheck();

            id = EditorGUILayout.DelayedIntField("Id", id);

            if (EditorGUI.EndChangeCheck())
            {
                SkillConfigData skillData = null;

                // 判断方式不需要调整，这个是所有的配置都不能重复
                List<SkillsConfig> allConfig = GetAllConfigs();

                foreach (var item in allConfig)
                {
                    skillData = item.GetSkillConfigData(id);
                    if (skillData != null) break;
                }

                if (skillData == null)
                {
                    serializedProperty.intValue = id;
                    serializedProperty.serializedObject.ApplyModifiedProperties();
                    SkillConfigData skillConfigData = serializedProperty.serializedObject.targetObject as SkillConfigData;
                    if (skillConfigData != null) skillConfigData.Rename();
                    if (skillConfigData != null && skillConfigData.parent != null)
                        skillConfigData.parent.ClearCache();
                }
                else
                {
                    string message = string.Format("技能id:{0}已被占用,请使用其他数字来作为id!", id);
                    EditorWindow.focusedWindow.ShowNotification(new GUIContent(message));
                }
            }

        }


        internal static List<SkillsConfig> GetAllConfigs()
        {
            List<SkillsConfig> allConfigs = new List<SkillsConfig>();

            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(SkillsConfig).FullName));
            if (guids == null || guids.Length == 0)
                return allConfigs;

            foreach (var item in guids)
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(assetpath))
                    continue;
                SkillsConfig config = AssetDatabase.LoadAssetAtPath<SkillsConfig>(assetpath);
                if (config == null) continue;
                allConfigs.Add(config);
            }

            return allConfigs;
        }
    }


}
#endif
