// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace YLFramework.MissionSystem
{
    /// <summary>
    /// 图标数据
    /// </summary>
    [Serializable]
    public class IconInfo
    {
        /// <summary>
        /// 资源模块名称
        /// </summary>
        [Tooltip("资源模块名称")]
        public string projectName;
        /// <summary>
        /// 图标名称
        /// </summary>
        [Tooltip("图标名称")]
        public string iconName;
    }

    /// <summary>
    /// 奖品数据
    /// </summary>
    [Serializable]
    public class RewardInfo
    {
        /// <summary>
        /// 奖励物品ID
        /// </summary>
        [Tooltip("奖励物品ID")]
        public int id;

        /// <summary>
        /// 奖励物品数量
        /// </summary>
        [Tooltip("奖励物品数量")]
        public int count;
    }

    /// <summary>
    /// 默认任务配置数据
    /// </summary>
    [Serializable]
    public class MissionConfigData  : ScriptableObject
    {


        /// <summary>
        /// 当前数据所在的配表
        /// </summary>
        [Tooltip("当前数据所在的配表")]
        [Header("当前数据所在的配表")]
        [YLFramework.Excel.ExcelIgnore]

        public MissionsConfig parent;

        /// <summary>
        /// 任务ID(唯一标识,不可重复)
        /// </summary>
        [Tooltip("任务ID(唯一标识,不可重复)")]

        public int id;

        /// <summary>
        /// 任务名称
        /// </summary>
        [Tooltip("任务名称")]
        [Header("任务名称")]
        public string mission_name;

        /// <summary>
        /// 任务图标
        /// </summary>
        [Tooltip("任务图标")]

        public IconInfo icon = new IconInfo();

        /// <summary>
        /// 任务描述
        /// </summary>
        [Tooltip("任务描述")]
        [Header("任务描述")]
        [TextArea(3,8)]
        public string description;


        /// <summary>
        /// 完成任务的条件
        /// </summary>
        [Tooltip("完成任务的条件")]

        public List<MissionCondition> conditions = new List<MissionCondition>();

        /// <summary>
        /// 任务完成的奖励
        /// </summary>
        [Tooltip("任务完成的奖励")]
        [Header("任务完成的奖励")]
        public List<RewardInfo> rewards = new List<RewardInfo>();


        /// <summary>
        /// 任务类型文件对应的guid
        /// </summary>
        [Tooltip("任务类型文件对应的guid")]
        [HideInInspector]
        [YLFramework.Excel.ExcelIgnore]
        public string mission_type_guid;

        /// <summary>
        /// 任务类全名称
        /// </summary>
        [Tooltip("任务类全名称")]

        public string mission_type_class_fullname;

        /// <summary>
        /// 任务完成后是否重置参数(该参数在没有其他正在执行中的任务引用的情况下,默认:true)
        /// </summary>
        [Tooltip("任务完成后是否重置参数(该参数在没有其他正在执行中的任务引用的情况下,默认:true)")]
        [Header("任务完成后是否重置参数(该参数在没有其他正在执行中的任务引用的情况下,默认:true)")]
        public bool reset_param_on_completed = true;

    }
}
