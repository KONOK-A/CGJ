using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Attributes;
using YLFramework.Buff;
using YLFramework.Editor.Utilities;


namespace YLFramework.Editor.Buff
{
    public class BuffsConfigController
    {
        private const string BuffConfigDataPath = "Assets/YLFrameworkData/Buff/Buffs/{0}";

        private static List<BuffsConfig> _allConfigs = new List<BuffsConfig>();

        public static int GenerateId(BuffsConfig config)
        {
            int maxId = config.startId;

            foreach (var item in config.buffs)
            {
                if (item.id > maxId)
                {
                    maxId = item.id;
                }
            }

            maxId++;

            return maxId;
        }

        public static int CreateBuffConfigData(Type type, BuffsConfig buffsConfig)
        {
            BuffConfigData buff = ScriptableObject.CreateInstance(type) as BuffConfigData;
            return CreateBuffConfigData(buff, buffsConfig);
        }

        public static int CreateBuffConfigData(BuffConfigData buff, BuffsConfig buffsConfig)
        {
            if (buffsConfig == null)
            {
                return -1;
            }

            int id = GenerateId(buffsConfig);
            buff.parent = buffsConfig;
            buff.id = id;
            buff.buffName = string.Format("Buff:{0}", id);
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
            string assetPath = AssetDatabase.GetAssetPath(buffsConfig);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return GetBuffConfigDataPath(guid);
        }

        public static string GetBuffConfigDataPath(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return string.Empty;
            }

            return string.Format(BuffConfigDataPath, guid);
        }


        public static List<BuffsConfig> GetAllConfigs()
        {
            _allConfigs.Clear();

            string filter = string.Format("t:{0}", typeof(BuffsConfig).FullName);

            string[] guids = AssetDatabase.FindAssets(filter);
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

                BuffsConfig config = AssetDatabase.LoadAssetAtPath<BuffsConfig>(assetpath);
                if (config == null)
                {
                    continue;
                }

                _allConfigs.Add(config);
            }

            return _allConfigs;
        }

    }

}
