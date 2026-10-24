using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Attributes;
using YLFramework.Skill;

namespace YLFramework.Editor.Skill
{
    public class ScriptBindController
    {
        // 会自动刷新
        // key:guid value:class_full_name
        private static Dictionary<string, string> _guidClassFullName = new Dictionary<string, string>();

        // key:class_full_name value:guid
        private static Dictionary<string, string> _classFullNameGuid = new Dictionary<string, string>();

        // 保存所有的继承自 SkillConfigData 的类型
        private static List<Type> _skillConfigDataTypes = null;

        [MenuItem("Tools/YLFramework/Skill/Refresh Script Bindings")]
        static void RefreshAllSkillScripts()
        {
            RefreshScript();

            foreach (SkillsConfig skillsConfig in SkillsConfigController.GetAllConfigs())
            {
                RefreshScriptClassFullName(skillsConfig);
            }

            // 保存一下
            AssetDatabase.SaveAssets();
        }

        private static void RefreshScriptClassFullName(SkillsConfig skillsConfig)
        {
            foreach (SkillConfigData skillConfigData in skillsConfig.skills)
            {
                if (skillConfigData == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(skillConfigData.skillClassGuid))
                {
                    // guid 不为空 尝试更新 class_full_name
                    string classFullName = GetClassFullNameByGuid(skillConfigData.skillClassGuid);
                    if (string.IsNullOrEmpty(classFullName))
                    {
                        // 尝试通过类名获取 guid (这种是为了通过excel导入/导出,导致的guid不存在,尝试根据class_full_name 来找到guid )
                        string guid = GetClassFullNameByGuid(skillConfigData.skillClassFullName);
                        if (!string.IsNullOrEmpty(guid) && skillConfigData.skillClassGuid != guid)
                        {
                            skillConfigData.skillClassGuid = guid;
                            EditorUtility.SetDirty(skillConfigData);
                        }
                    }
                    else
                    {
                        if (skillConfigData.skillClassFullName != classFullName)
                        {
                            skillConfigData.skillClassFullName = classFullName;
                            EditorUtility.SetDirty(skillConfigData);
                        }
                    }


                }
                else if (string.IsNullOrEmpty(skillConfigData.skillClassGuid) && !string.IsNullOrEmpty(skillConfigData.skillClassFullName))
                {
                    // guid 是空 但是 class_full_name 不为空 尝试更新guid
                    string guid = GetGuidByClassFullName(skillConfigData.skillClassFullName);

                    if (string.IsNullOrEmpty(guid))
                    {
                        continue;
                    }

                    skillConfigData.skillClassGuid = guid;

                    EditorUtility.SetDirty(skillConfigData);
                }
            }
        }

        private static void RefreshScript()
        {
            if (_guidClassFullName == null)
            {
                _guidClassFullName = new Dictionary<string, string>();
            }

            _guidClassFullName.Clear();

            if (_classFullNameGuid == null)
            {
                _classFullNameGuid = new Dictionary<string, string>();
            }

            _classFullNameGuid.Clear();

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var guid in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null)
                {
                    continue;
                }

                Type type = script.GetClass();

                if (type == null)
                {
                    continue;
                }

                if (type.IsSubclassOf(typeof(SkillBase)))
                {
                    if (!_guidClassFullName.ContainsKey(guid))
                    {
                        _guidClassFullName.Add(guid, type.FullName);
                    }

                    if (!_classFullNameGuid.ContainsKey(type.FullName))
                    {
                        _classFullNameGuid.Add(type.FullName, guid);
                    }
                }
            }
        }

        private static string GetGuidByClassFullName(string fullName)
        {
            if (_classFullNameGuid.ContainsKey(fullName))
            {
                return _classFullNameGuid[fullName];
            }

            return string.Empty;
        }

        private static string GetClassFullNameByGuid(string guid)
        {
            if (_guidClassFullName.ContainsKey(guid))
            {
                return _guidClassFullName[guid];
            }

            return string.Empty;
        }

        public static List<Type> GetAllSkillConfigDataTypes(Type parent)
        {
            if (_skillConfigDataTypes == null)
            {
                _skillConfigDataTypes = new List<Type>();
            }

            _skillConfigDataTypes.Clear();

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var guid in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null)
                {
                    continue;
                }

                Type type = script.GetClass();

                if (type == null)
                {
                    continue;
                }

                if (type.IsSubclassOf(parent) || type == parent)
                {
                    if (!_skillConfigDataTypes.Contains(type))
                    {
                        _skillConfigDataTypes.Add(type);
                    }
                }
            }

            return _skillConfigDataTypes;
        }




    }

}
