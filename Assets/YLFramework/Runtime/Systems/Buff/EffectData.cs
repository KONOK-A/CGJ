// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using UnityEngine;

namespace YLFramework.BuffSystem
{
    /// <summary>
    /// 效果配置数据
    /// </summary>
    [EffectName("基础效果(EffectData)")]
    public class EffectData : ScriptableObject
    {
        [Tooltip("唯一标识")]
        public int id;

        [Tooltip("名称")]
        public string effect_name;

        [Tooltip("类型")]
        [Header("类型")]
        public int type;

        [Tooltip("描述")]
        [Header("描述")]
        [TextArea(3, 8)]
        public string description;



    }
}
