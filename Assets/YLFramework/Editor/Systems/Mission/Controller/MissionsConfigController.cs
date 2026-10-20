// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
using UnityEditor;
using UnityEngine;
using System;

namespace YLFramework.MissionSystem
{
    public class MissionsConfigController
    {

        private static List<MissionsConfig> allConfigs = new List<MissionsConfig>();

        public static List<MissionsConfig> GetAllConfigs()
        {
            allConfigs.Clear();

            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(MissionsConfig).FullName));
            if (guids == null || guids.Length == 0)
                return allConfigs;

            foreach (var item in guids)
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(assetpath))
                    continue;
                MissionsConfig config = AssetDatabase.LoadAssetAtPath<MissionsConfig>(assetpath);
                if (config == null) continue;
                allConfigs.Add(config);
            }

            return allConfigs;
        }

        public static int CreateMissionConfigData(Type type, MissionsConfig config)
        {
            MissionConfigData mission = ScriptableObject.CreateInstance(type) as MissionConfigData;
            return CreateMissionConfigData(mission,config);
        }

        public static int CreateMissionConfigData(MissionConfigData mission, MissionsConfig config)
        {
            if (config == null) return -1;
            string asset_path = AssetDatabase.GetAssetPath(config);
            string guid = AssetDatabase.AssetPathToGUID(asset_path);

            int id = GenerateID(config);
            mission.id = id;
            mission.mission_name = string.Format("Mission:{0}", id);
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

        public static int GenerateID(MissionsConfig config)
        {
            int max_id = config.start_id;

            foreach (var item in config.missions)
            {
                if (item.id > max_id)
                    max_id = item.id;
            }

            max_id++;

            return max_id;
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
