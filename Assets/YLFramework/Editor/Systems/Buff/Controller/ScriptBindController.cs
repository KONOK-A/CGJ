using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;

namespace YLFramework.BuffSystem
{
    public class ScriptBindController
    {

        // key:guid value:class_full_name
        private static Dictionary<string, string> _guidClassFullName = new Dictionary<string, string>();

        // key:class_full_name value:guid
        private static Dictionary<string, string> _classFullNameGuid = new Dictionary<string, string>();

        // 保存所有的继承自 SkillConfigData 的类型
        private static List<Type> _skillConfigDataTypes = null;

        [MenuItem("Tools/YLFramework/Buff/Refresh Script Bindings")]
        static void RefreshAllSkillScripts()
        {
            RefreshScript();

            foreach (BuffsConfig buffsConfig in BuffsConfigController.GetAllConfigs())
            {
                RefreshScriptClassFullName(buffsConfig);
            }

            // 保存一下
            AssetDatabase.SaveAssets();
        }

        private static void RefreshScriptClassFullName(BuffsConfig skillsConfig)
        {
            foreach (BuffConfigData buffConfigData in skillsConfig.buffs)
            {
                if (buffConfigData == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(buffConfigData.buffClassGuid))
                {
                    // guid 不为空 尝试更新 class_full_name
                    string classFullName = GetClassFullNameByGuid(buffConfigData.buffClassGuid);
                    if (string.IsNullOrEmpty(classFullName))
                    {
                        // 尝试通过类名获取 guid (这种是为了通过excel导入/导出,导致的guid不存在,尝试根据class_full_name 来找到guid )
                        string guid = GetClassFullNameByGuid(buffConfigData.buffClassFullName);
                        if (!string.IsNullOrEmpty(guid) && buffConfigData.buffClassGuid != guid)
                        {
                            buffConfigData.buffClassGuid = guid;
                            EditorUtility.SetDirty(buffConfigData);
                        }
                    }
                    else
                    {
                        if (buffConfigData.buffClassFullName != classFullName)
                        {
                            buffConfigData.buffClassFullName = classFullName;
                            EditorUtility.SetDirty(buffConfigData);
                        }
                    }


                }
                else if (string.IsNullOrEmpty(buffConfigData.buffClassGuid) && !string.IsNullOrEmpty(buffConfigData.buffClassFullName))
                {
                    // guid 是空 但是 class_full_name 不为空 尝试更新guid
                    string guid = GetGuidByClassFullName(buffConfigData.buffClassFullName);
                    if (string.IsNullOrEmpty(guid))
                    {
                        continue;
                    }

                    buffConfigData.buffClassGuid = guid;
                    EditorUtility.SetDirty(buffConfigData);
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

                if (type.IsSubclassOf(typeof(BuffBase)))
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

        public static List<Type> GetAllSkillConfigDataTypes()
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

                if (type.IsSubclassOf(typeof(BuffConfigData)) || type == typeof(BuffConfigData))
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
