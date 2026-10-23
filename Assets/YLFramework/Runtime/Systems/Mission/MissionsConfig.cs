using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YLFramework.Foundation;

namespace YLFramework.MissionSystem
{
    /// <summary>
    /// 任务配表
    /// </summary>
    [CreateAssetMenu(menuName = "YLFramework/MissionSystem(任务配置)")]
    public class MissionsConfig : ScriptableObject
    {

        [HideInInspector]
        [FormerlySerializedAs("start_id")] public int startId = 0;

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
        [FormerlySerializedAs("param")] public List<MissionParameter> parameters = new List<MissionParameter>();

        private Dictionary<int, MissionConfigData> _missionsDic = new Dictionary<int, MissionConfigData>();

        private Dictionary<string, MissionParameter> _paramDic = new Dictionary<string, MissionParameter>();

        /// <summary>
        /// 显示名
        /// </summary>
        [Tooltip("显示名")]
        [Header("显示名")]
        [FormerlySerializedAs("display_name")] public string displayName;

        [FormerlySerializedAs("type_script_guid")] public string typeScriptGuid;


        /// <summary>
        /// 查询任务配置
        /// </summary>
        /// <param name="id">任务id</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public MissionConfigData GetMissionConfigData(int id)
        {

            if (_missionsDic.Count == 0)
            {
                foreach (var item in missions)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (_missionsDic.ContainsKey(item.id))
                    {
                        throw new Exception(string.Format("任务id:{0}重复!", id));
                    }

                    _missionsDic.Add(item.id, item);
                }
            }

            if (_missionsDic.ContainsKey(id))
            {
                return _missionsDic[id];
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
            {
                return null;
            }

            if (_paramDic.Count == 0)
            {
                foreach (var item in parameters)
                {
                    if (_paramDic.ContainsKey(item.name))
                    {
                        throw new Exception(string.Format("参数名称:{0}重复!", item.name));
                    }

                    _paramDic.Add(item.name, item);
                }
            }

            if (_paramDic.ContainsKey(paramName))
            {
                return _paramDic[paramName];
            }

            return null;
        }

        /// <summary>
        /// 插件内部使用的方法
        /// </summary>
        public void ClearCache()
        {
            _missionsDic.Clear();
            _paramDic.Clear();
        }

        /// <summary>
        /// 插件内部使用的方法
        /// </summary>
        public string GetDisplayName()
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return name;
            }
            else
            {
                return string.Format("{0}({1})", name, displayName);
            }
        }

    }

}
