// Adapted from XF Skill. Original copyright and MIT License: Runtime/Systems/Skill/LICENSE.txt.
using YLFramework.Foundation;
using System.Collections.Generic;

namespace YLFramework.SkillSystem
{
    /// <summary>
    /// 可装备技能的接口,如果一个游戏角色能够装备技能和释放技能,请实现该接口
    /// </summary>
    public interface IEquippableSkill
    {

        /// <summary>
        /// 所有的技能
        /// </summary>
        Dictionary<int, SkillBase> Skills { get; }


        /// <summary>
        /// 是否能够释放技能
        /// </summary>
        /// <returns></returns>
        bool IsCanRelease();

    }

}
