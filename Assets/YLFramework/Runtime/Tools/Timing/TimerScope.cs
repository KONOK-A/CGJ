using System;
using System.Collections.Generic;

namespace YLFramework.Utilities.Timing
{
    /// <summary>
    /// 计时任务的独立命名与清理范围：维护范围内的任务与名字，用于按名查询、暂停和批量取消。
    /// 自身没有帧驱动，任务仍由宿主（<see cref="Timer"/>）执行；不与 Action 共用任务表。
    /// 显式创建不会创建宿主，范围可跨宿主重建继续使用。
    /// </summary>
    public sealed class TimerScope : IDisposable
    {
        private readonly HashSet<TimeItem> mTasks = new HashSet<TimeItem>();
        private readonly Dictionary<string, TimeItem> mNames = new Dictionary<string, TimeItem>(StringComparer.Ordinal);

        /// <summary>范围是否已关闭；关闭后投递任务会抛出 ObjectDisposedException，查询与控制不再命中任务。</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>查询范围内是否存在活动同名任务，不创建任何宿主。</summary>
        public bool Contains(string flag) => TryGet(flag, out _);

        /// <summary>取得范围内当前有效的命名任务；名字按序数（大小写敏感）比较，未被登记或已结束时返回 false。</summary>
        /// <param name="flag">任务名，不能为 null 或空白。</param>
        /// <param name="item">找到的活动任务；未找到时为 null。</param>
        /// <returns>找到活动任务时为 true。</returns>
        public bool TryGet(string flag, out TimeItem item)
        {
            ValidateFlag(flag);
            if (!IsDisposed && mNames.TryGetValue(flag, out item))
            {
                // 从未激活的 Unity 宿主没有 OnDestroy；查询时只补清旧宿主，不创建替代对象。
                if (!ReferenceEquals(item.Owner, null)) item.Owner.ReleaseIfDestroyed();
                if (item.IsActive && ReferenceEquals(item.Scope, this)) return true;
                if (mNames.TryGetValue(flag, out var registered) && ReferenceEquals(registered, item)) mNames.Remove(flag);
            }
            item = null;
            return false;
        }

        /// <summary>取消当前同名活动任务。</summary>
        /// <returns>找到并执行取消时为 true；未找到时为 false。</returns>
        public bool Cancel(string flag)
        {
            if (!TryGet(flag, out var item)) return false;
            item.Cancel();
            return true;
        }

        /// <summary>暂停当前同名活动任务并冻结剩余时间。</summary>
        /// <returns>任务存在且接受暂停时为 true。</returns>
        public bool Pause(string flag) => TryGet(flag, out var item) && item.Pause();

        /// <summary>恢复当前同名活动任务；保留原来的时间源。</summary>
        /// <returns>任务存在且接受恢复时为 true。</returns>
        public bool Resume(string flag) => TryGet(flag, out var item) && item.Resume();

        /// <summary>取消本范围内全部命名和匿名任务（含暂停与执行中），范围随后仍可使用。</summary>
        public void CancelAll()
        {
            foreach (var item in new List<TimeItem>(mTasks)) item.Cancel();
        }

        /// <summary>先关闭范围再取消全部任务，阻止取消过程中重入投递；重复关闭无副作用，不关闭 Timer 宿主。</summary>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            CancelAll();
            mNames.Clear();
        }

        // 投递前的名字校验：null、空或空白字符串都视为非法。
        internal static void ValidateFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) throw new ArgumentException("Timer flag cannot be empty.", nameof(flag));
        }

        // 投递前的范围校验：已关闭的范围拒绝新任务；命名任务在同范围内不允许活动重名。
        internal void ValidateRegistration(string flag)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(TimerScope));
            if (flag != null && Contains(flag)) throw new InvalidOperationException("Timer flag already exists in this scope: " + flag);
        }

        // 任务入堆后登记；匿名任务（Flag 为 null）只进任务表，不占名字。
        internal void Register(TimeItem item)
        {
            mTasks.Add(item);
            if (item.Flag != null) mNames.Add(item.Flag, item);
        }

        // 仅在名字仍指向该任务时摘除，避免清掉回调内新投递的同名任务。
        internal void ReleaseName(TimeItem item)
        {
            if (item.Flag != null && mNames.TryGetValue(item.Flag, out var current) && ReferenceEquals(current, item))
                mNames.Remove(item.Flag);
        }

        // 任务结束时统一摘除名字与任务登记。
        internal void Remove(TimeItem item)
        {
            ReleaseName(item);
            mTasks.Remove(item);
        }
    }
}
