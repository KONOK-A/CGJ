// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.MissionSystem
{
    public class ScriptBindController
    {

        // key:guid value:class_full_name
        private static Dictionary<string,string> guid_class_full_name = new Dictionary<string,string>();

        // key:class_full_name value:guid
        private static Dictionary<string,string> class_full_name_guid = new Dictionary<string,string>();

        // 保存所有的继承自 SkillConfigData 的类型
        private static List<Type> skill_config_data_types = null;

        [MenuItem("Tools/YLFramework/Mission/Refresh Script Bindings")]
        static void RefreshAllSkillScripts()
        {
            RefreshScript();

            foreach (MissionsConfig skillsConfig in MissionsConfigController.GetAllConfigs())
            {
                RefreshScriptClassFullName(skillsConfig);
            }

            // 保存一下
            AssetDatabase.SaveAssets();
        }

        private static void RefreshScriptClassFullName(MissionsConfig skillsConfig)
        {
            foreach (MissionConfigData missionConfigData in skillsConfig.missions)
            {
                if (missionConfigData == null) continue;


                if (!string.IsNullOrEmpty(missionConfigData.mission_type_guid))
                {
                    // guid 不为空 尝试更新 class_full_name
                    string class_full_name = GetClassFullNameByGUID(missionConfigData.mission_type_guid);
                    if (string.IsNullOrEmpty(class_full_name))
                    {

                        // 尝试通过类名获取 guid (这种是为了通过excel导入/导出,导致的guid不存在,尝试根据class_full_name 来找到guid )
                        string guid = GetClassFullNameByGUID(missionConfigData.mission_type_class_fullname);
                        if (!string.IsNullOrEmpty(guid) && missionConfigData.mission_type_guid != guid)
                        {
                            missionConfigData.mission_type_guid = guid;
                            EditorUtility.SetDirty(missionConfigData);
                        }
                    }
                    else
                    {
                        if (missionConfigData.mission_type_class_fullname != class_full_name)
                        {
                            missionConfigData.mission_type_class_fullname = class_full_name;
                            EditorUtility.SetDirty(missionConfigData);
                        }
                    }


                }
                else if (string.IsNullOrEmpty(missionConfigData.mission_type_guid) && !string.IsNullOrEmpty(missionConfigData.mission_type_class_fullname))
                {
                    // guid 是空 但是 class_full_name 不为空 尝试更新guid
                    string guid = GetGUIDByClassFullName(missionConfigData.mission_type_class_fullname);

                    if (string.IsNullOrEmpty(guid)) continue;

                    missionConfigData.mission_type_guid = guid;

                    EditorUtility.SetDirty(missionConfigData);
                }
            }
        }

        private static void RefreshScript()
        {
            if (guid_class_full_name == null)
                guid_class_full_name = new Dictionary<string, string>();

            guid_class_full_name.Clear();

            if (class_full_name_guid == null)
                class_full_name_guid = new Dictionary<string, string>();

            class_full_name_guid.Clear();

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var guid in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null) continue;

                Type type = script.GetClass();

                if (type == null) continue;

                if (type.IsSubclassOf(typeof(MissionBase)) )
                {
                    if (!guid_class_full_name.ContainsKey(guid))
                        guid_class_full_name.Add(guid, type.FullName);

                    if (!class_full_name_guid.ContainsKey(type.FullName))
                        class_full_name_guid.Add(type.FullName, guid);

                }
            }
        }

        private static string GetGUIDByClassFullName(string fullName)
        {
            if(class_full_name_guid.ContainsKey(fullName))
                return class_full_name_guid[fullName];

            return string.Empty;
        }

        private static string GetClassFullNameByGUID(string guid)
        {
            if(guid_class_full_name.ContainsKey(guid))
                return guid_class_full_name[guid];

            return string.Empty;
        }

        public static List<Type> GetAllSkillConfigDataTypes()
        {
            if(skill_config_data_types == null)
                skill_config_data_types = new List<Type>();

            skill_config_data_types.Clear();

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var guid in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null) continue;

                Type type = script.GetClass();

                if (type == null) continue;

                if (type.IsSubclassOf(typeof(MissionConfigData)) || type == typeof(MissionConfigData))
                {
                    if (!skill_config_data_types.Contains(type))
                        skill_config_data_types.Add(type);
                }
            }

            return skill_config_data_types;
        }




    }

}
