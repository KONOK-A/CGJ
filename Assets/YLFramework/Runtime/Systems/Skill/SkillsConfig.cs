using YLFramework.Foundation;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace YLFramework.SkillSystem
{
    /// <summary>
    /// 技能配表
    /// </summary>
    [CreateAssetMenu(menuName ="YLFramework/SkillSystem(技能配置)")]
    public class SkillsConfig : ScriptableObject
    {

        [HideInInspector]
        public int start_id = 0;

        [Header("显示名称")]
        public string displayName;

        [Tooltip("技能配置集合")]
        public List<SkillConfigData> skills = new List<SkillConfigData>();

        [HideInInspector]
        public string type_script_guid;

        private Dictionary<int,SkillConfigData> skills_dic = new Dictionary<int, SkillConfigData>();

        /// <summary>
        /// 查询技能数据
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public SkillConfigData GetSkillConfigData(int id) {

            if(skills_dic.Count == 0) {
                foreach (var item in skills)
                {

                    if(item == null) continue;

                    if (skills_dic.ContainsKey(item.id))
                        throw new Exception(string.Format("技能id:{0}重复!",id));
                    skills_dic.Add(item.id, item);
                }
            }

            if (skills_dic.ContainsKey(id))
            {
                return skills_dic[id];
            }

            return null;
        }

        public void ClearCache()
        {
            skills_dic.Clear();
        }

    }

}
