// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace YLFramework.BuffSystem
{
    [Serializable]
    public class EffectType
    {
        public int type;
        public string name;
    }

    [Serializable]
    public class GroupEffectData
    {
        public List<EffectData> effectDatas = new List<EffectData>();

        public string GroupName
        {
            get
            {
                foreach (var item in effectDatas)
                {
                    if(item == null) continue;

                    string groupName = string.Empty;

                    EffectName effectName = item.GetType().GetCustomAttribute<EffectName>();
                    if (effectName != null)
                    {
                        groupName = effectName.Name;
                    }
                    else
                    {
                        groupName = item.GetType().FullName;
                    }

                    return groupName;
                }

                return string.Empty;
            }
        }

        public void Clear() {
            effectDatas.Clear();
        }


        public void Add(EffectData data) {
            effectDatas.Add(data);
        }


        public bool IsEmpty()
        {
            foreach (var item in effectDatas)
            {
                if(item != null)
                    return false;
            }

            return true;
        }

    }

    public class BuffSystemSettings : ProjectSettingsScriptableObject<BuffSystemSettings>
    {

        #region 字段

        [Tooltip("效果类型")]
        [Header("效果类型")]
        [SerializeField]
        private List<EffectType> allEffectTypes;

        [Tooltip("效果配置")]
        [SerializeField]
        private List<EffectData> allEffectDatas;

        [SerializeField]
        private List<GroupEffectData> allEffectDataWithType;

        private float lastUpdateTime;

        private float lastUpdateTypeTime;

        private Dictionary<Type, GroupEffectData> allEffectDataDic;

        private Dictionary<int, EffectData> effectDatasDic = null;

        #endregion

        #region 属性

        public List<EffectData> AllEffectDatas
        {
            get
            {
                if(allEffectDatas == null)
                    allEffectDatas = new List<EffectData>();

                bool remove = false;

                for (int i = allEffectDatas.Count - 1; i >= 0; i--)
                {
                    if (allEffectDatas[i] != null) continue;
                    allEffectDatas.RemoveAt(i);
                    remove = true;
                }

                if (remove)
                {
                    UpdateAllEffectData();
                    Save();
                }

                if (allEffectDatas.Count == 0)
                {
                    // 尝试查询 同步一下
                    SyncEffectData();
                }

                return allEffectDatas;
            }
        }

        public List<GroupEffectData> AllEffectDataWithType
        {
            get
            {
                if (allEffectDataWithType == null)
                    allEffectDataWithType = new List<GroupEffectData>();

                for (int i = allEffectDataWithType.Count - 1; i >= 0 ; i--)
                {
                    if (allEffectDataWithType[i].IsEmpty())
                        allEffectDataWithType.RemoveAt(i);
                }

                if (allEffectDataWithType.Count == 0)
                {
                    // 尝试同步
                    SyncEffectDataWithType();
                }


                return allEffectDataWithType;
            }
        }

        public List<EffectType> AllEffectType
        {
            get {
                if(allEffectTypes == null)
                    allEffectTypes = new List<EffectType>();

                return allEffectTypes;
            }
        }

        #endregion


        public string GetType(int type,bool richText = true)
        {
            foreach (var item in AllEffectType)
            {
                if (item.type == type)
                {
                    if (string.IsNullOrEmpty(item.name))
                        return item.type.ToString();

                    if(richText)
                        return string.Format("{0}<color=grey>({1})</color>",type,item.name);
                    else
                        return string.Format("{0}({1})", type, item.name);
                }
            }

            return "空";
        }

        internal void SyncEffectData()
        {
            if (Time.realtimeSinceStartup - lastUpdateTime <= 1)
                return;
            lastUpdateTime = Time.realtimeSinceStartup;

            UpdateAllEffectData();

            Save();
        }

        internal void SyncEffectDataWithType()
        {
            if (Time.realtimeSinceStartup - lastUpdateTypeTime <= 1)
                return;

            lastUpdateTypeTime = Time.realtimeSinceStartup;

            UpdateAllEffectDataWithType();

            Save();
        }

        internal void UpdateAllEffectData()
        {
            allEffectDatas ??= new List<EffectData>();
            allEffectDatas.Clear();
            ClearCache();
            string filter = string.Format("t:{0}", typeof(EffectData).FullName);

            string[] guids = AssetDatabase.FindAssets(filter);

            if (guids == null || guids.Length == 0)
                return;

            foreach (var guid in guids)
            {
                string assetpath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetpath))
                    continue;
                EffectData config = AssetDatabase.LoadAssetAtPath<EffectData>(assetpath);
                if (config == null) continue;
                allEffectDatas.Add(config);
            }

            //Save();
        }

        internal void UpdateAllEffectDataWithType()
        {
            if(allEffectDataDic == null)
                allEffectDataDic = new Dictionary<Type, GroupEffectData>();

            foreach (var item in allEffectDataDic.Keys)
            {
                allEffectDataDic[item].Clear();
            }

            foreach (var item in AllEffectDatas)
            {
                Type type = item.GetType();
                if (!allEffectDataDic.ContainsKey(type))
                    allEffectDataDic.Add(type, new GroupEffectData());

                allEffectDataDic[type].Add(item);
            }

            allEffectDataWithType ??= new List<GroupEffectData>();
            allEffectDataWithType.Clear();

            foreach (var item in allEffectDataDic.Values)
            {
                allEffectDataWithType.Add(item);
            }


        }

        public void AddEffectData(EffectData effectData)
        {
            if (AllEffectDatas.Contains(effectData)) return;
            AllEffectDatas.Add(effectData);

            UpdateAllEffectDataWithType();

            Save();
            ClearCache();
        }

        public void RemoveEffectData(EffectData effectData)
        {
            if (!AllEffectDatas.Contains(effectData)) return;
            AllEffectDatas.Remove(effectData);

            UpdateAllEffectDataWithType();

            Save();
            ClearCache();
        }


        public EffectData GetEffectDataByID(int id)
        {

            if (effectDatasDic == null)
                effectDatasDic = new Dictionary<int, EffectData>();

            if (effectDatasDic.Count == 0)
            {
                foreach (var item in AllEffectDatas)
                {
                    if (effectDatasDic.ContainsKey(item.id))
                    {
                        Debug.LogErrorFormat("EffectData发现重复的id:{0}",item.id);
                        continue;
                    }

                    effectDatasDic.Add(item.id, item);
                }
            }


            if(effectDatasDic.ContainsKey(id))
                return effectDatasDic[id];

            return null;
        }


        public void ClearCache()
        {
            if (effectDatasDic != null)
                effectDatasDic.Clear();
        }

    }

}

#endif
