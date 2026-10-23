using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YLFramework.Foundation;

namespace YLFramework.BuffSystem
{

    /// <summary>
    /// Buff配表
    /// </summary>
    [CreateAssetMenu(menuName = "YLFramework/BuffsConfig(Buff配置)")]
    public class BuffsConfig : ScriptableObject
    {

        [HideInInspector]
        [FormerlySerializedAs("start_id")] public int startId;

        [Header("显示名称")]
        public string displayName;

        [Tooltip("Buff配表集合")]
        public List<BuffConfigData> buffs = new List<BuffConfigData>();

        private Dictionary<int, BuffConfigData> _buffsDic = new Dictionary<int, BuffConfigData>();


        [FormerlySerializedAs("type_script_guid")] public string typeScriptGuid;

        /// <summary>
        /// 查询Buff配置信息
        /// </summary>
        /// <param name="buffId"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public BuffConfigData GetBuffData(int buffId)
        {

            if (_buffsDic.Count == 0)
            {
                foreach (var item in buffs)
                {
                    if (!item)
                    {
                        continue;
                    }

                    if (_buffsDic.ContainsKey(item.id))
                    {
                        throw new System.Exception(string.Format("BuffInfo id:{0}重复!", item.id));
                    }

                    _buffsDic.Add(item.id, item);
                }
            }

            if (_buffsDic.ContainsKey(buffId))
            {
                return _buffsDic[buffId];
            }

            return null;
        }


        public void ClearCache()
        {
            _buffsDic.Clear();
        }

    }
}
