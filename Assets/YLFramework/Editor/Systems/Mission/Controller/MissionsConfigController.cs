using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Attributes;
using YLFramework.Editor.Utilities;
using YLFramework.Mission;

namespace YLFramework.Editor.Mission
{
    public class MissionsConfigController
    {

        private static List<MissionsConfig> _allConfigs = new List<MissionsConfig>();

        public static List<MissionsConfig> GetAllConfigs()
        {
            _allConfigs.Clear();

            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(MissionsConfig).FullName));
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

                MissionsConfig config = AssetDatabase.LoadAssetAtPath<MissionsConfig>(assetpath);
                if (config == null)
                {
                    continue;
                }

                _allConfigs.Add(config);
            }

            return _allConfigs;
        }

        public static int CreateMissionConfigData(Type type, MissionsConfig config)
        {
            MissionConfigData mission = ScriptableObject.CreateInstance(type) as MissionConfigData;
            return CreateMissionConfigData(mission, config);
        }

        public static int CreateMissionConfigData(MissionConfigData mission, MissionsConfig config)
        {
            if (config == null)
            {
                return -1;
            }

            string assetPath = AssetDatabase.GetAssetPath(config);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            int id = GenerateId(config);
            mission.id = id;
            mission.missionName = string.Format("Mission:{0}", id);
            mission.parent = config;

            string path = MissionAuthoring.GeneratedDirectory(config);
            CreateAsset(mission, path, false);
            config.missions.Add(mission);
            EditorUtility.SetDirty(config);
            config.ClearCache();
            mission.Rename();
            EditorUtility.SetDirty(mission);

            return id;
        }

        public static int GenerateId(MissionsConfig config)
        {
            int maxId = config.startId;

            foreach (var item in config.missions)
            {
                if (item.id > maxId)
                {
                    maxId = item.id;
                }
            }

            maxId++;

            return maxId;
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

            if (ping)
            {
                Selection.activeObject = scriptableObject;
                EditorGUIUtility.PingObject(scriptableObject);
            }
        }

    }

}
