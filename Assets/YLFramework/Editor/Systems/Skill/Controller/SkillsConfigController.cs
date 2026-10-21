using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace YLFramework.SkillSystem
{

    public class SkillsConfigController
    {

        private const string SKILL_CONFIG_DATA_PATH = "Assets/YLFrameworkData/Skill/Skills/{0}";

        private static List<SkillsConfig> allConfigs ;


        public static List<SkillsConfig> GetAllConfigs()
        {
            if(allConfigs == null)
                allConfigs = new List<SkillsConfig>();

            allConfigs.Clear();

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

            return allConfigs ;
        }

        public static int CreateSkillConfigData(Type type,SkillsConfig skillsConfig)
        {
            if (skillsConfig == null) return -1;
            SkillConfigData skill = ScriptableObject.CreateInstance(type) as SkillConfigData;
            return CreateSkillConfigData(skill, skillsConfig);
        }

        public static int CreateSkillConfigData(SkillConfigData skillConfigData, SkillsConfig skillsConfig)
        {
            string path = GetSkillConfigDataPath(skillsConfig);

            if (string.IsNullOrEmpty(path))
            {
                Debug.LogErrorFormat("技能创建失败,未查询到技能配表路径:{0}", skillsConfig.name);
                return -1;
            }

            int id = GenerateID(skillsConfig);
            skillConfigData.id = id;
            skillConfigData.skill_name = string.Format("技能:{0}", id);
            skillConfigData.parent = skillsConfig;

            CreateAsset(skillConfigData, path, false);

            skillsConfig.skills.Add(skillConfigData);
            EditorUtility.SetDirty(skillsConfig);
            skillsConfig.ClearCache();
            skillConfigData.Rename();
            EditorUtility.SetDirty(skillConfigData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return id;
        }

        public static int GenerateID(SkillsConfig config)
        {
            int max_id = config.start_id;

            foreach (var item in config.skills)
            {
                if (item.id > max_id)
                    max_id = item.id;
            }

            max_id++;

            return max_id;
        }

        public static string GetSkillConfigDataPath(SkillsConfig skillsConfig)
        {
            string asset_path = AssetDatabase.GetAssetPath(skillsConfig);
            string guid = AssetDatabase.AssetPathToGUID(asset_path);
            return GetSkillConfigDataPath(guid);
        }

        public static string GetSkillConfigDataPath(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return string.Empty;

            return string.Format(SKILL_CONFIG_DATA_PATH,guid);
        }

        public static void CreateAsset(ScriptableObject scriptableObject, string path, bool ping = true)
        {
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }

            string name = AssetNameTools.GetName(scriptableObject.GetType().Name, path);
            AssetDatabase.CreateAsset(scriptableObject, string.Format("{0}/{1}.asset", path, name));
            Selection.activeObject = scriptableObject;
            if (ping)
            {
                EditorGUIUtility.PingObject(scriptableObject);
            }
        }

        public static string GetCopyName(string skill_name, SkillsConfig skillsConfig)
        {

            int count = 1;

            if (Regex.IsMatch(skill_name, "\\([0-9]+\\)$"))
            {
                for (int i = skill_name.Length - 1; i >= 0; i--)
                {
                    if (skill_name[i] == '(')
                    {
                        string c = skill_name.Substring(i + 1, skill_name.Length - i - 2);
                        count = int.Parse(c);

                        skill_name = skill_name.Substring(0, i);
                        break;
                    }
                }
            }

            skill_name = skill_name.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", skill_name, count);
                count++;
            } while (IsContainsBuffName(name, skillsConfig));

            return name;
        }

        private static bool IsContainsBuffName(string skill_name, SkillsConfig skillsConfig)
        {

            foreach (var item in skillsConfig.skills)
            {
                if (item.skill_name == skill_name)
                    return true;
            }

            return false;
        }

    }
}
