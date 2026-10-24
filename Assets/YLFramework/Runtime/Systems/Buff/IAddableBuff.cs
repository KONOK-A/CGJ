using System.Collections.Generic;
using YLFramework.Foundation;

namespace YLFramework.Buff
{
    /// <summary>
    /// 可添加Buff的接口
    /// </summary>
    public interface IAddableBuff
    {
        /// <summary>
        /// 所有的Buff
        /// </summary>
        List<BuffBase> Buffs
        {
            get;
        }

        /// <summary>
        /// 判断是否能够添加某个buff
        /// </summary>
        /// <param name="buff"></param>
        /// <returns></returns>
        bool CanAdd(int buffId);

        /// <summary>
        /// 添加Buff回调
        /// </summary>
        /// <param name="buff"></param>
        void OnAddBuff(BuffBase buff);

        /// <summary>
        /// 移除Buff回调
        /// </summary>
        /// <param name="buff"></param>
        void OnRemoveBuff(BuffBase buff);
    }
}
