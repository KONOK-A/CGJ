// Adapted from XF Skill. Original copyright and MIT License: Runtime/Systems/Skill/LICENSE.txt.
using YLFramework.Foundation;
using System;
using System.Collections.Generic;

using UnityEngine;

namespace YLFramework.SkillSystem
{
    [Serializable]
    public class IconInfo
    {
        public string projectName;
        public string iconName;
    }

    /// <summary>
    /// 默认技能配置数据
    /// </summary>
    public class SkillConfigData : ScriptableObject
    {

        [Tooltip("当前数据所在的配表")]
        [Header("当前数据所在的配表")]

        [YLFramework.Excel.ExcelIgnore]
        public SkillsConfig parent;

        [Tooltip("技能ID(唯一标识,不可重复)")]
        [Header("技能ID(唯一标识,不可重复)")]

        public int id;
        [Tooltip("技能名称")]
        [Header("技能名称")]
        public string skill_name;

        [Tooltip("技能图标")]
        [Header("技能图标")]

        public IconInfo icon = new IconInfo();
        [Tooltip("冷却时间")]
        [Header("冷却时间")]
        public float cooling_time;
        [Tooltip("持续时间")]
        [Header("持续时间")]
        public float duration;
        [Tooltip("能够同时释放的技能(默认所有的技能都不能同时释放)")]
        [Header("能够同时释放的技能(默认所有的技能都不能同时释放)")]

        public List<int> canbe_released_at_the_same_time = new List<int>();
        [Tooltip("是否能够主动取消")]
        [Header("是否能够主动取消")]
        public bool active_cancellation;

        [Tooltip("技能描述")]
        [Header("技能描述")]
        //[Multiline(8)]
        [TextArea(3,8)]
        public string description;

        [Tooltip("技能类名称(含命名空间)")]

        public string skill_class_full_name;

        [Tooltip("技能类脚本guid")]
        [HideInInspector]
        [YLFramework.Excel.ExcelIgnore]
        public string skill_class_guid;

    }
}
