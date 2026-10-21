using YLFramework.Foundation;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace YLFramework.BuffSystem
{

    /// <summary>
    /// Buff配表
    /// </summary>
    [CreateAssetMenu(menuName ="YLFramework/BuffsConfig(Buff配置)")]
    public class BuffsConfig : ScriptableObject
    {

        [HideInInspector]
        public int start_id;

        [Header("显示名称")]
        public string displayName;

        [Tooltip("Buff配表集合")]
        public List<BuffConfigData> buffs = new List<BuffConfigData>();

        private Dictionary<int, BuffConfigData> buffs_dic = new Dictionary<int, BuffConfigData>();


        public string type_script_guid;

        /// <summary>
        /// 查询Buff配置信息
        /// </summary>
        /// <param name="buffId"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public BuffConfigData GetBuffData(int buffId)
        {

            if (buffs_dic.Count == 0)
            {
                foreach (var item in buffs)
                {
                    if (!item) continue;
                    if (buffs_dic.ContainsKey(item.buff_id))
                        throw new System.Exception(string.Format("BuffInfo id:{0}重复!", item.buff_id));
                    buffs_dic.Add(item.buff_id, item);
                }
            }

            if (buffs_dic.ContainsKey(buffId))
            {
                return buffs_dic[buffId];
            }

            return null;
        }


        public void ClearCache() {
            buffs_dic.Clear();
        }

    }
}
