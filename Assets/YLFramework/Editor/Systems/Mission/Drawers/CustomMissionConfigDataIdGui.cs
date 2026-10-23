using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace YLFramework.MissionSystem
{
    public class CustomMissionConfigDataIdGui : CustomPropertyGui
    {

        public CustomMissionConfigDataIdGui() : base(true) { }


        public override void OnPropertyGui(SerializedProperty serializedProperty)
        {
            GUILayout.Space(10);

            EditorGUILayout.LabelField(string.Empty, "任务ID(唯一标识,不可重复)", "BoldLabel");

            int id = serializedProperty.intValue;

            EditorGUI.BeginChangeCheck();
            id = EditorGUILayout.DelayedIntField("Id", id);
            if (EditorGUI.EndChangeCheck())
            {
                if (ContainsId(id))
                {
                    EditorWindow.focusedWindow.ShowNotification(EditorGUIUtility.TrTempContent(string.Format("ID:{0}已经存在,请使用其他的数字作为ID!", id)));
                    return;
                }

                serializedProperty.intValue = id;
                serializedProperty.serializedObject.ApplyModifiedProperties();
                MissionConfigData missionConfigData = serializedProperty.serializedObject.targetObject as MissionConfigData;
                if (missionConfigData != null)
                {
                    missionConfigData.Rename();
                }

                if (missionConfigData != null && missionConfigData.parent != null)
                {
                    missionConfigData.parent.ClearCache();
                }
            }
        }


        private bool ContainsId(int id)
        {

            List<MissionsConfig> allConfigs = GetAllConfigs();

            foreach (var item in allConfigs)
            {
                if (item.GetMissionConfigData(id) != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static List<MissionsConfig> GetAllConfigs()
        {
            List<MissionsConfig> allConfigs = new List<MissionsConfig>();

            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(MissionsConfig).FullName));
            if (guids == null || guids.Length == 0)
            {
                return allConfigs;
            }

            foreach (var item in guids)
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(assetpath))
                {
                    continue;
                }

                MissionsConfig config = AssetDatabase.LoadAssetAtPath<MissionsConfig>(assetpath);
                if (config == null)
                {
                    continue;
                }

                allConfigs.Add(config);
            }

            return allConfigs;
        }

    }

}

#endif
