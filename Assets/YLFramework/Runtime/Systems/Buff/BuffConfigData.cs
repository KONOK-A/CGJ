// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using System;
using System.Collections.Generic;

using UnityEngine;
using YLFramework.Excel;

namespace YLFramework.BuffSystem
{

    [Serializable]
    public class IconInfo
    {
        public string projectName;
        public string iconName;
    }

    public enum BuffMode
    {
        /// <summary>
        /// 不可叠加
        /// </summary>
        [Tooltip("不可叠加")]
        Single,
        /// <summary>
        /// 可叠加
        /// </summary>
        [Tooltip("可叠加")]
        Additive
    }


    /// <summary>
    /// Buff配置数据
    /// </summary>
    public class BuffConfigData : ScriptableObject
    {

        [Tooltip("当前数据所在的配表")]
        [Header("当前数据所在的配表")]

        [ExcelIgnore]
        public BuffsConfig parent;

        [Tooltip("唯一标识")]

        [ExcelScriptableObjectID]
        public int buff_id;
        [Tooltip("名称")]
        [Header("名称")]
        public string buff_name;
        [Tooltip("图标")]
        [Header("图标")]

        public IconInfo icon = new IconInfo();
        [Tooltip("描述")]
        [Header("描述")]
        [TextArea(3, 8)]
        public string description;
        [Tooltip("持续时间(如果小于等于0,认为该Buff长期存在!)")]
        [Header("持续时间(如果小于等于0,认为该Buff长期存在!)")]
        public float duration;
        [Tooltip("模式(如果有多个相同的buff，它们的效果是否能够叠加?)")]
        [Header("模式(如果有多个相同的buff，它们的效果是否能够叠加?)")]
        public BuffMode mode = BuffMode.Additive;
        [Tooltip("Buff效果(当前Buff有哪些效果)")]
        [Header("Buff效果(当前Buff有哪些效果)")]

        [BuffEffectExcel]
        public List<EffectData> effects = new List<EffectData>();
        [Tooltip("Buff脚本名称")]

        public string buff_class_full_name;

        [Tooltip("Buff脚本guid")]
        [HideInInspector]
        [ExcelIgnore]
        public string buff_class_guid;

    }
}
