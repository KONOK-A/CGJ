using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;

namespace YLFramework.SkillSystem
{

    public class SkillsConfigController
    {

        private const string SkillConfigDataPath = "Assets/YLFrameworkData/Skill/Skills/{0}";

        private static List<SkillsConfig> _allConfigs;


        public static List<SkillsConfig> GetAllConfigs()
        {
            if (_allConfigs == null)
            {
                _allConfigs = new List<SkillsConfig>();
            }

            _allConfigs.Clear();

            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(SkillsConfig).FullName));
            if (guids == null || guids.Length == 0)
            {
                return _allConfigs;
            }

            foreach (var item in guids)
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(assetpath))
                {
                    continue;
                }

                SkillsConfig config = AssetDatabase.LoadAssetAtPath<SkillsConfig>(assetpath);
                if (config == null)
                {
                    continue;
                }

                _allConfigs.Add(config);
            }

            return _allConfigs;
        }

        public static int CreateSkillConfigData(Type type, SkillsConfig skillsConfig)
        {
            if (skillsConfig == null)
            {
                return -1;
            }

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

            int id = GenerateId(skillsConfig);
            skillConfigData.id = id;
            skillConfigData.skillName = string.Format("技能:{0}", id);
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

        public static int GenerateId(SkillsConfig config)
        {
            int maxId = config.startId;

            foreach (var item in config.skills)
            {
                if (item.id > maxId)
                {
                    maxId = item.id;
                }
            }

            maxId++;

            return maxId;
        }

        public static string GetSkillConfigDataPath(SkillsConfig skillsConfig)
        {
            string assetPath = AssetDatabase.GetAssetPath(skillsConfig);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return GetSkillConfigDataPath(guid);
        }

        public static string GetSkillConfigDataPath(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return string.Empty;
            }

            return string.Format(SkillConfigDataPath, guid);
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

        public static string GetCopyName(string skillName, SkillsConfig skillsConfig)
        {

            int count = 1;

            if (Regex.IsMatch(skillName, "\\([0-9]+\\)$"))
            {
                for (int i = skillName.Length - 1; i >= 0; i--)
                {
                    if (skillName[i] == '(')
                    {
                        string c = skillName.Substring(i + 1, skillName.Length - i - 2);
                        count = int.Parse(c);

                        skillName = skillName.Substring(0, i);
                        break;
                    }
                }
            }

            skillName = skillName.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", skillName, count);
                count++;
            } while (IsContainsBuffName(name, skillsConfig));

            return name;
        }

        private static bool IsContainsBuffName(string skillName, SkillsConfig skillsConfig)
        {

            foreach (var item in skillsConfig.skills)
            {
                if (item.skillName == skillName)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
