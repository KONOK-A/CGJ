// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;

using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;


namespace YLFramework.BuffSystem
{
    public class BuffsConfigController
    {
        private const string BUFF_CONFIG_DATA_PATH = "Assets/YLFrameworkData/Buff/Buffs/{0}";

        private static List<BuffsConfig> allConfigs = new List<BuffsConfig>();

        public static int GenerateID(BuffsConfig config)
        {
            int max_id = config.start_id;

            foreach (var item in config.buffs)
            {
                if (item.buff_id > max_id)
                    max_id = item.buff_id;
            }

            max_id++;

            return max_id;
        }

        public static int CreateBuffConfigData(Type type, BuffsConfig buffsConfig)
        {
            BuffConfigData buff = ScriptableObject.CreateInstance(type) as BuffConfigData;
            return CreateBuffConfigData(buff, buffsConfig);
        }

        public static int CreateBuffConfigData(BuffConfigData buff, BuffsConfig buffsConfig)
        {
            if (buffsConfig == null) return -1;

            int id = GenerateID(buffsConfig);
            buff.parent = buffsConfig;
            buff.buff_id = id;
            buff.buff_name = string.Format("Buff:{0}", id);
            string path = GetBuffConfigDataPath(buffsConfig);
            CreateAsset(buff, path, false);
            buffsConfig.buffs.Add(buff);
            EditorUtility.SetDirty(buffsConfig);
            buffsConfig.ClearCache();
            buff.Rename();
            EditorUtility.SetDirty(buff);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return id;
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

        public static string GetBuffConfigDataPath(BuffsConfig buffsConfig)
        {
            string asset_path = AssetDatabase.GetAssetPath(buffsConfig);
            string guid = AssetDatabase.AssetPathToGUID(asset_path);
            return GetBuffConfigDataPath(guid);
        }

        public static string GetBuffConfigDataPath(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return string.Empty;

            return string.Format(BUFF_CONFIG_DATA_PATH, guid);
        }


        public static List<BuffsConfig> GetAllConfigs()
        {
            allConfigs.Clear();

            string filter = string.Format("t:{0}", typeof(BuffsConfig).FullName);

            string[] guids = AssetDatabase.FindAssets(filter);
            if (guids == null || guids.Length == 0)
                return allConfigs;

            foreach (var item in guids)
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(item);
                if (string.IsNullOrEmpty(assetpath))
                    continue;
                BuffsConfig config = AssetDatabase.LoadAssetAtPath<BuffsConfig>(assetpath);
                if (config == null) continue;
                allConfigs.Add(config);
            }

            return allConfigs;
        }

    }

}
