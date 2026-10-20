// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace YLFramework.MissionSystem
{
    /// <summary>
    /// 任务配表
    /// </summary>
    [CreateAssetMenu(menuName = "YLFramework/MissionSystem(任务配置)")]
    public class MissionsConfig : ScriptableObject
    {

        [HideInInspector]
        public int start_id = 0;

        /// <summary>
        /// 任务配置集合
        /// </summary>
        [HideInInspector]
        [Tooltip("任务配置集合")]
        public List<MissionConfigData> missions = new List<MissionConfigData>();

        /// <summary>
        /// 参数集合
        /// </summary>
        [Tooltip("参数集合")]
        [HideInInspector]
        public List<MissionParameter> param = new List<MissionParameter>();

        private Dictionary<int,MissionConfigData> missions_dic = new Dictionary<int, MissionConfigData>();

        private Dictionary<string, MissionParameter> param_dic = new Dictionary<string, MissionParameter>();

        /// <summary>
        /// 显示名
        /// </summary>
        [Tooltip("显示名")]
        [Header("显示名")]
        public string display_name;

        public string type_script_guid;


        /// <summary>
        /// 查询任务配置
        /// </summary>
        /// <param name="id">任务id</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public MissionConfigData GetMissionConfigData(int id)
        {

            if(missions_dic.Count == 0)
            {
                foreach (var item in missions)
                {
                    if(item == null) continue;

                    if (missions_dic.ContainsKey(item.id))
                        throw new Exception(string.Format("任务id:{0}重复!",id));
                    missions_dic.Add(item.id, item);
                }
            }

            if (missions_dic.ContainsKey(id))
            {
                return missions_dic[id];
            }

            return null;
        }

        /// <summary>
        /// 查询参数配置
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public MissionParameter GetMissionParam(string paramName)
        {

            if (string.IsNullOrEmpty(paramName))
                return null;

            if (param_dic.Count == 0)
            {
                foreach (var item in param)
                {
                    if (param_dic.ContainsKey(item.name))
                        throw new Exception(string.Format("参数名称:{0}重复!",item.name));
                    param_dic.Add(item.name, item);
                }
            }

            if(param_dic.ContainsKey(paramName))
                return param_dic[paramName];

            return null;
        }

        /// <summary>
        /// 插件内部使用的方法
        /// </summary>
        public void ClearCache()
        {
            missions_dic.Clear();
            param_dic.Clear();
        }

        /// <summary>
        /// 插件内部使用的方法
        /// </summary>
        public string GetDisplayName()
        {
            if (string.IsNullOrEmpty(display_name))
            {
                return name;
            }
            else
            {
               return string.Format("{0}({1})", name, display_name);
            }
        }

    }

}
