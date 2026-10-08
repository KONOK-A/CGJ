/****************************************************************************
 * Copyright (c) 2017 snowcold
 * Copyright (c) 2017 liangxie
 ****************************************************************************/
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Utilities.Timing
{
    /// <summary>轻量任务收集器：记录若干 <see cref="TimeItem"/> 供所有者批量取消；自身不拥有任务，也不参与调度。</summary>
    public class TimerHelper
    {
        /// <summary>已记录的任务列表；首次 Add 时惰性创建。</summary>
        protected List<TimeItem> m_TimeItemList;
        /// <summary>是否允许继续添加任务；为 false 时 Add 记录错误并忽略，不再收集。</summary>
        protected bool m_IsUsable = true;

        /// <summary>记录一个待统一取消的任务。</summary>
        /// <param name="item">要记录的任务句柄；已结束的任务也可以记录，取消时不会改写其终态。</param>
        /// <exception cref="System.ArgumentNullException">item 为 null 时抛出。</exception>
        public void Add(TimeItem item)
        {
            if (!m_IsUsable)
            {
                Debug.LogError("TimerHelper is unavailable.");
                return;
            }
            if (item == null) throw new System.ArgumentNullException(nameof(item));
            if (m_TimeItemList == null) m_TimeItemList = new List<TimeItem>(2);
            m_TimeItemList.Add(item);
        }

        /// <summary>取消并清空全部已记录的任务；已结束的任务保持原终态，列表随后可继续 Add。</summary>
        public void Clear()
        {
            if (m_TimeItemList == null) return;
            for (var i = m_TimeItemList.Count - 1; i >= 0; --i) m_TimeItemList[i].Cancel();
            m_TimeItemList.Clear();
        }
    }
}
