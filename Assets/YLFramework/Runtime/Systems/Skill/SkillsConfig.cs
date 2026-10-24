using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YLFramework.Foundation;

namespace YLFramework.Skill
{
    /// <summary>
    /// 技能配表
    /// </summary>
    [CreateAssetMenu(menuName = "YLFramework/SkillSystem(技能配置)")]
    public class SkillsConfig : ScriptableObject
    {

        [HideInInspector]
        [FormerlySerializedAs("start_id")] public int startId = 0;

        [Header("显示名称")]
        public string displayName;

        [Tooltip("技能配置集合")]
        public List<SkillConfigData> skills = new List<SkillConfigData>();

        [HideInInspector]
        [FormerlySerializedAs("type_script_guid")] public string typeScriptGuid;

        private Dictionary<int, SkillConfigData> _skillsDic = new Dictionary<int, SkillConfigData>();

        /// <summary>
        /// 查询技能数据
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public SkillConfigData GetSkillConfigData(int id)
        {

            if (_skillsDic.Count == 0)
            {
                foreach (var item in skills)
                {

                    if (item == null)
                    {
                        continue;
                    }

                    if (_skillsDic.ContainsKey(item.id))
                    {
                        throw new Exception(string.Format("技能id:{0}重复!", id));
                    }

                    _skillsDic.Add(item.id, item);
                }
            }

            if (_skillsDic.ContainsKey(id))
            {
                return _skillsDic[id];
            }

            return null;
        }

        public void ClearCache()
        {
            _skillsDic.Clear();
        }

    }

}
