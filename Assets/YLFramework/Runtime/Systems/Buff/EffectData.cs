using UnityEngine;
using UnityEngine.Serialization;
using YLFramework.Foundation;

namespace YLFramework.Buff
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
        [FormerlySerializedAs("effect_name")] public string effectName;

        [Tooltip("类型")]
        [Header("类型")]
        public int type;

        [Tooltip("描述")]
        [Header("描述")]
        [TextArea(3, 8)]
        public string description;



    }
}
