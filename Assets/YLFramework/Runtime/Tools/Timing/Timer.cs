/****************************************************************************
 * Copyright (c) 2017 snowcold
 * Copyright (c) 2017 liangxie
 * https://github.com/akbiggs/UnityTimer
 ****************************************************************************/
using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Utilities.Timing
{
    /// <summary>
    /// 计时的默认宿主：以两套最小堆分别调度缩放时间（<c>Time.time</c>）与不受缩放影响（<c>Time.unscaledTime</c>）的到期任务，
    /// 由 Unity 的 <c>Update</c> 驱动（<see cref="UpdateMgr"/>）。
    /// 首次通过 <see cref="Instance"/> 或投递入口使用时，会复用已有场景实例或创建宿主（播放模式下按框架层级常驻）；
    /// 也可手动挂载组件后直接投递。全部操作都在主线程同步执行，不保证线程安全。
    /// </summary>
    public class Timer : MonoSingletonHost<Timer>
    {
        private readonly BinaryHeap<TimeItem> mUnscaledTimeHeap = new BinaryHeap<TimeItem>(128, BinaryHeapSortMode.kMin);
        private readonly BinaryHeap<TimeItem> mScaleTimeHeap = new BinaryHeap<TimeItem>(128, BinaryHeapSortMode.kMin);
        private float mCurrentUnscaledTime = -1;
        private float mCurrentScaleTime = -1;
        private int mResetVersion;
        private bool mUpdating;
        private readonly HashSet<TimeItem> mOwnedTasks = new HashSet<TimeItem>();
        private readonly TimerScope mDefaultScope = new TimerScope();

        /// <summary>未显式指定 scope 时任务归属的默认范围；显式范围独立于宿主，可在宿主重建后继续使用。</summary>
        public TimerScope DefaultScope => mDefaultScope;

        /// <summary>查询默认或显式范围内的活动命名任务；不会创建 Timer 宿主。</summary>
        /// <param name="flag">任务名，不能为 null 或空白。</param>
        /// <param name="item">找到的活动任务；未找到时为 null。</param>
        /// <param name="scope">显式范围；null 表示当前宿主的 <see cref="DefaultScope"/>，宿主不存在时直接返回 false。</param>
        /// <returns>找到活动任务时为 true。</returns>
        public static bool TryGet(string flag, out TimeItem item, TimerScope scope = null)
        {
            TimerScope.ValidateFlag(flag);
            if (scope != null) return scope.TryGet(flag, out item);
            if (!ReferenceEquals(CachedHost, null) && !CachedHost) CachedHost.ReleaseHost();
            if (TryGetInstance(out var host)) return host.DefaultScope.TryGet(flag, out item);
            item = null;
            return false;
        }

        /// <summary>非创建式检查默认或显式范围内是否存在活动命名任务。</summary>
        public static bool Contains(string flag, TimerScope scope = null) => TryGet(flag, out _, scope);

        /// <summary>非创建式取消默认或显式范围内的命名任务，不影响其他范围中的同名任务。</summary>
        /// <returns>找到并执行取消时为 true；任务不存在时为 false。</returns>
        public static bool Cancel(string flag, TimerScope scope = null)
        {
            if (!TryGet(flag, out var item, scope)) return false;
            item.Cancel();
            return true;
        }

        /// <summary>非创建式暂停默认或显式范围内的命名任务。</summary>
        /// <returns>任务存在且接受暂停时为 true；不存在或无法暂停（如单次任务正在执行）时为 false。</returns>
        public static bool Pause(string flag, TimerScope scope = null) => TryGet(flag, out var item, scope) && item.Pause();

        /// <summary>非创建式恢复默认或显式范围内的命名任务。</summary>
        /// <returns>任务存在且接受恢复时为 true；不存在或不在可恢复状态时为 false。</returns>
        public static bool Resume(string flag, TimerScope scope = null) => TryGet(flag, out var item, scope) && item.Resume();

        /// <summary>获取默认宿主，必要时创建跨场景存活的 GameObject；会触发场景实例收养与宿主初始化。</summary>
        /// <exception cref="InvalidOperationException">应用正在退出、不允许创建宿主时抛出。</exception>
        public new static Timer Instance
        {
            get
            {
                if (!CanCreateHost(true)) throw new InvalidOperationException("Timer is shutting down.");
                return GetInstance(true);
            }
        }

        /// <summary>自建宿主在播放模式下挂到 YLFrameworkRoot 的 Timing 模块节点，非播放模式保持顶级节点；借用对象层级不变。</summary>
        protected override Transform HostParent => Application.isPlaying
            ? YLFrameworkRoot.Instance.GetModuleRoot("Timing") : null;

        /// <summary>模块级清理：同步释放全部计时宿主和任务；借用对象只移除组件，之后可重新获取。</summary>
        public static void Shutdown()
        {
            // 从未激活的宿主外部销毁后没有 OnDestroy，仍需释放其托管任务。
            if (!ReferenceEquals(CachedHost, null)) CachedHost.ReleaseHost();
            foreach (var host in FindObjectsByType<Timer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                host.ReleaseHost();
        }

        /// <summary>最近一次时钟同步时的缩放时间，即 <c>Time.time</c>。</summary>
        public float currentScaleTime => mCurrentScaleTime;
        /// <summary>最近一次时钟同步时的不受缩放影响的时间，即 <c>Time.unscaledTime</c>。</summary>
        public float currentUnscaledTime => mCurrentUnscaledTime;

        // 进入播放模式时（SubsystemRegistration）重置会话状态：清理没有 OnDestroy 的失效缓存宿主，
        // 重置场景中全部宿主并清空静态引用，避免关闭 Domain Reload 时残留上一会话的任务与缓存。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (!ReferenceEquals(CachedHost, null) && CachedHost == null) CachedHost.ReleaseHost();
            foreach (var host in FindObjectsByType<Timer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                host.ResetMgr();
                host.StartMgr();
            }
            ResetHostReference();
        }

        /// <summary>激活时按基类规则登记为宿主并触发初始化；重复组件会被释放，不保留独立调度队列。</summary>
        protected override void Awake()
        {
            base.Awake();
            // 经 Host 的初始化标记初始化，避免首次 Instance 再次清空已投递任务。
            if (!HostReleased) _ = GetInstance(true);
        }

        /// <summary>释放本宿主拥有的任务与回调，并关闭默认范围；显式范围不随宿主释放，但其中属于本宿主的任务同样被取消。</summary>
        protected override void OnHostRelease()
        {
            ResetMgr();
            mDefaultScope.Dispose();
        }

        /// <summary>宿主首次初始化入口：清空已有任务并同步时钟；手动调用同样会取消全部任务。</summary>
        public override void OnSingletonInit()
        {
            ResetMgr();
            StartMgr();
        }

        /// <summary>取消并释放本宿主全部任务（含暂停与执行中的任务）；在回调中调用会终止当前任务的后续调度。</summary>
        public void ResetMgr()
        {
            mResetVersion++;
            // 所有权集合包含暂停和回调已出堆的任务；更新仍只检查堆顶。
            foreach (var item in new List<TimeItem>(mOwnedTasks)) CancelItem(item);
        }

        /// <summary>把两个时钟同步到当前 <c>Time</c> 值；<see cref="UpdateMgr"/> 每帧调用，也可手动对齐。</summary>
        public void StartMgr()
        {
            mCurrentUnscaledTime = Time.unscaledTime;
            mCurrentScaleTime = Time.time;
        }

        /// <summary>投递指定次数的缩放时间任务（基于 <c>Time.time</c>）。</summary>
        /// <param name="callback">到期回调，参数为从 1 开始的触发次数。</param>
        /// <param name="delay">首次及每轮间隔的秒数；零或负值按零等待处理。</param>
        /// <param name="repeat">执行次数；负数表示无限重复直到取消。</param>
        /// <param name="scope">任务归属范围；null 使用当前宿主的 <see cref="DefaultScope"/>。</param>
        /// <returns>新任务句柄，可用于 Cancel / Pause / Resume。</returns>
        /// <exception cref="ArgumentOutOfRangeException">delay 为 NaN 或 Infinity 时抛出。</exception>
        /// <exception cref="ObjectDisposedException">目标 scope 已 Dispose 时抛出。</exception>
        /// <exception cref="InvalidOperationException">宿主已释放时抛出。</exception>
        public TimeItem Post2Scale(Action<int> callback, float delay, int repeat, TimerScope scope = null)
        {
            var item = TimeItem.Allocate(callback, delay, repeat);
            Post2Scale(item, scope);
            return item;
        }
        /// <summary>投递一次缩放时间任务（基于 <c>Time.time</c>）。</summary>
        public TimeItem Post2Scale(Action<int> callback, float delay) => Post2Scale(callback, delay, 1);
        /// <summary>将已有任务重新投递到缩放时间堆；执行中的任务可在回调内重投递为新租约。</summary>
        public void Post2Scale(TimeItem item, TimerScope scope = null)
        {
            EnsureReady();
            Post(item, false, scope, null);
        }

        /// <summary>在指定本地日期时间后执行一次任务；目标时间已过时记录 Warning 并按零延迟执行。</summary>
        /// <param name="callback">到期回调，参数为从 1 开始的触发次数。</param>
        /// <param name="toTime">本地目标时间，基于 <see cref="DateTime.Now"/> 换算为延迟。</param>
        /// <returns>新任务句柄。</returns>
        public TimeItem Post2Really(Action<int> callback, DateTime toTime)
        {
            var delay = (float)(toTime - DateTime.Now).TotalSeconds;
            if (delay < 0) Debug.LogWarning("Timer target time has already passed.");
            return Post2Really(callback, Mathf.Max(0, delay));
        }
        /// <summary>投递指定次数的真实时间任务（基于 <c>Time.unscaledTime</c>）。</summary>
        /// <param name="callback">到期回调，参数为从 1 开始的触发次数。</param>
        /// <param name="delay">首次及每轮间隔的秒数；零或负值按零等待处理。</param>
        /// <param name="repeat">执行次数；负数表示无限重复直到取消。</param>
        /// <param name="scope">任务归属范围；null 使用当前宿主的 <see cref="DefaultScope"/>。</param>
        /// <returns>新任务句柄，可用于 Cancel / Pause / Resume。</returns>
        /// <exception cref="ArgumentOutOfRangeException">delay 为 NaN 或 Infinity 时抛出。</exception>
        /// <exception cref="ObjectDisposedException">目标 scope 已 Dispose 时抛出。</exception>
        /// <exception cref="InvalidOperationException">宿主已释放时抛出。</exception>
        public TimeItem Post2Really(Action<int> callback, float delay, int repeat, TimerScope scope = null)
        {
            var item = TimeItem.Allocate(callback, delay, repeat);
            Post2Really(item, scope);
            return item;
        }
        /// <summary>投递一次不受时间缩放影响的任务（基于 <c>Time.unscaledTime</c>）。</summary>
        public TimeItem Post2Really(Action<int> callback, float delay) => Post2Really(callback, delay, 1);
        /// <summary>将已有任务重新投递到真实时间堆；执行中的任务可在回调内重投递为新租约。</summary>
        public void Post2Really(TimeItem item, TimerScope scope = null)
        {
            EnsureReady();
            Post(item, true, scope, null);
        }

        /// <summary>投递命名任务；同一范围内拒绝活动重名，名字在单次 / 最后一轮进入回调前释放。</summary>
        /// <param name="flag">任务名，按序数（大小写敏感）比较，不能为 null 或空白。</param>
        /// <param name="callback">到期回调，参数为从 1 开始的触发次数。</param>
        /// <param name="delay">首次及每轮间隔的秒数；零或负值按零等待处理。</param>
        /// <param name="ignoreTimeScale">true 使用 <c>Time.unscaledTime</c>，否则使用 <c>Time.time</c>。</param>
        /// <param name="repeat">执行次数；负数表示无限重复。</param>
        /// <param name="scope">任务归属范围；null 使用当前宿主的 <see cref="DefaultScope"/>。</param>
        /// <returns>新任务句柄。</returns>
        /// <exception cref="ArgumentException">flag 为 null、空或空白时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">delay 为 NaN 或 Infinity 时抛出。</exception>
        /// <exception cref="ObjectDisposedException">目标 scope 已 Dispose 时抛出。</exception>
        /// <exception cref="InvalidOperationException">同范围内已有活动同名任务，或宿主已释放时抛出。</exception>
        public TimeItem PostNamed(string flag, Action<int> callback, float delay, bool ignoreTimeScale = false,
            int repeat = 1, TimerScope scope = null)
        {
            TimerScope.ValidateFlag(flag);
            EnsureReady();
            var item = TimeItem.Allocate(callback, delay, repeat);
            Post(item, ignoreTimeScale, scope, flag);
            return item;
        }

        /// <summary>验证参数后先取消同范围旧任务，再启动新命名任务；不是事务性替换，新任务启动失败不会恢复旧任务。</summary>
        /// <param name="flag">任务名，按序数（大小写敏感）比较，不能为 null 或空白。</param>
        /// <param name="callback">到期回调，参数为从 1 开始的触发次数。</param>
        /// <param name="delay">首次及每轮间隔的秒数；零或负值按零等待处理。</param>
        /// <param name="ignoreTimeScale">true 使用 <c>Time.unscaledTime</c>，否则使用 <c>Time.time</c>。</param>
        /// <param name="repeat">执行次数；负数表示无限重复。</param>
        /// <param name="scope">任务归属范围；null 使用当前宿主的 <see cref="DefaultScope"/>。</param>
        /// <returns>新任务句柄。</returns>
        /// <exception cref="ArgumentException">flag 为 null、空或空白时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">delay 为 NaN 或 Infinity 时抛出（此时旧任务不会被取消）。</exception>
        /// <exception cref="ObjectDisposedException">目标 scope 已 Dispose 时抛出。</exception>
        /// <exception cref="InvalidOperationException">宿主已释放时抛出。</exception>
        public TimeItem ReplaceNamed(string flag, Action<int> callback, float delay, bool ignoreTimeScale = false,
            int repeat = 1, TimerScope scope = null)
        {
            TimerScope.ValidateFlag(flag);
            EnsureReady();
            var item = TimeItem.Allocate(callback, delay, repeat);
            scope = scope ?? mDefaultScope;
            if (scope.IsDisposed) throw new ObjectDisposedException(nameof(TimerScope));
            scope.Cancel(flag);
            Post(item, ignoreTimeScale, scope, flag);
            return item;
        }

        private void EnsureReady()
        {
            if (HostReleased) throw new InvalidOperationException("Timer host has been released.");
            // 手动挂载的组件可能尚未执行 Awake；入堆前完成 Host 初始化，不能在入堆后清空。
            _ = GetInstance(true);
        }

        private void Post(TimeItem item, bool ignoreTimeScale, TimerScope scope, string flag)
        {
            if (HostReleased) throw new InvalidOperationException("Timer host has been released.");
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.HeapIndex != 0 || item.IsRecycled ||
                (item.State != TimerState.Created && item.State != TimerState.Executing) ||
                (!ReferenceEquals(item.Owner, null) && item.State != TimerState.Executing))
                throw new InvalidOperationException("TimeItem is already scheduled, paused, or recycled.");
            scope = scope ?? mDefaultScope;
            scope.ValidateRegistration(flag);
            // 回调显式重投递是新调度租约，旧 finally 不能清理新归属。
            if (!ReferenceEquals(item.Owner, null)) item.Owner.Detach(item);
            item.ScheduleVersion++;
            item.Owner = this;
            item.Heap = ignoreTimeScale ? mUnscaledTimeHeap : mScaleTimeHeap;
            item.IgnoreTimeScale = ignoreTimeScale;
            item.Scope = scope;
            item.Flag = flag;
            item.PauseNext = false;
            item.State = TimerState.Scheduled;
            scope.Register(item);
            mOwnedTasks.Add(item);
            // 零或负间隔重复任务只在下一次时钟推进后执行，避免同一帧死循环。
            var currentTime = ignoreTimeScale ? mCurrentUnscaledTime : mCurrentScaleTime;
            item.SortScore = currentTime + Mathf.Max(0, item.DelayTime());
            item.Heap.Insert(item);
        }

        /// <summary>读取指定时间源的当前值，供 <see cref="TimeItem.RemainingSeconds"/>、暂停恢复计算分数使用。</summary>
        internal float ReadClock(bool ignoreTimeScale) => ignoreTimeScale ? Time.unscaledTime : Time.time;

        /// <summary>Unity 对象被外部销毁后兜底释放；从未激活的宿主不会收到 OnDestroy，需要调用方补一次。</summary>
        internal void ReleaseIfDestroyed()
        {
            if (!this) ReleaseHost();
        }

        /// <summary>暂停等待中的任务：冻结本轮剩余时间并移出堆；执行中的任务只对仍有重复的任务置下一轮暂停请求。</summary>
        internal bool PauseItem(TimeItem item)
        {
            if (!ReferenceEquals(item.Owner, this) || !item.IsActive) return false;
            if (item.State == TimerState.Paused) return true;
            if (item.State == TimerState.Executing)
            {
                if (!item.NeedRepeat()) return false;
                item.PauseNext = true;
                return true;
            }
            item.FrozenRemaining = item.RemainingSeconds;
            item.Heap.RemoveAt(item.HeapIndex);
            item.State = TimerState.Paused;
            return true;
        }

        /// <summary>恢复暂停任务：按原时间源当前值加冻结剩余时间重新入堆；也可撤销执行中回调的下一轮暂停请求。</summary>
        internal bool ResumeItem(TimeItem item)
        {
            if (!ReferenceEquals(item.Owner, this)) return false;
            if (item.State == TimerState.Executing && item.PauseNext)
            {
                item.PauseNext = false;
                return true;
            }
            if (item.State != TimerState.Paused) return false;
            item.SortScore = ReadClock(item.IgnoreTimeScale) + item.FrozenRemaining;
            item.State = TimerState.Scheduled;
            item.Heap.Insert(item);
            return true;
        }

        /// <summary>取消任务：出堆、摘除名字并置为 Cancelled；执行中的任务不打断回调，由 <see cref="UpdateHeap"/> 的租约收尾。</summary>
        internal bool CancelItem(TimeItem item)
        {
            if (!ReferenceEquals(item.Owner, this) || !item.HasActiveState) return false;
            var executing = item.ExecutingVersion == item.ScheduleVersion;
            if (item.HeapIndex != 0) item.Heap.RemoveAt(item.HeapIndex);
            item.State = TimerState.Cancelled;
            item.Scope.Remove(item);
            // 不进入公开 Cancel 递归路径；终止状态下回调立即释放。
            if (!executing) Finish(item, TimerState.Cancelled);
            else item.ClearCallback();
            return true;
        }

        // 清除任务与本宿主的全部归属引用；重投递时旧宿主先 Detach 再交给新宿主。
        private void Detach(TimeItem item)
        {
            item.Scope.Remove(item);
            mOwnedTasks.Remove(item);
            item.Owner = null;
            item.Heap = null;
            item.Scope = null;
            item.Flag = null;
        }

        // 统一收尾：先摘除归属，再写入终态并回收；终态在回收后仍可供外部查询。
        private void Finish(TimeItem item, TimerState state)
        {
            Detach(item);
            item.State = state;
            item.Recycle2Cache();
        }

        /// <summary>Unity 帧更新入口。</summary>
        public void Update() => UpdateMgr();

        /// <summary>更新时钟并执行到期任务（先非缩放堆，后缩放堆）；更新期间的重入调用会被忽略。</summary>
        public void UpdateMgr()
        {
            if (mUpdating || HostReleased) return;
            mUpdating = true;
            try
            {
                StartMgr();
                UpdateHeap(mUnscaledTimeHeap, mCurrentUnscaledTime);
                UpdateHeap(mScaleTimeHeap, mCurrentScaleTime);
            }
            finally { mUpdating = false; }
        }

        // 执行堆中所有严格早于 now 的任务；最小堆保证堆顶最早到期，只需检查堆顶即可。
        // 收尾按任务出堆时记录的租约核验：回调内重投递或 ResetMgr 后，旧 finally 不再处理新的调度状态。
        private void UpdateHeap(BinaryHeap<TimeItem> heap, float now)
        {
            while (heap.HasValue())
            {
                var item = heap.Top();
                if (item.SortScore >= now) break;
                heap.Pop();
                var version = mResetVersion;
                var lease = item.ScheduleVersion;
                item.ExecutingVersion = lease;
                item.State = TimerState.Executing;
                var succeeded = false;
                try
                {
                    item.OnTimeTick();
                    succeeded = true;
                }
                finally
                {
                    // 回调失败或重置时仍释放当前已出堆任务，异常不被吞掉。
                    if (lease == item.ScheduleVersion)
                    {
                        item.ExecutingVersion = 0;
                        if (item.State == TimerState.Cancelled || version != mResetVersion) Finish(item, TimerState.Cancelled);
                        else if (!succeeded) Finish(item, TimerState.Faulted);
                        else if (!item.NeedRepeat()) Finish(item, TimerState.Completed);
                        else if (item.PauseNext)
                        {
                            item.FrozenRemaining = Mathf.Max(0, item.DelayTime());
                            item.PauseNext = false;
                            item.State = TimerState.Paused;
                        }
                        else
                        {
                            item.SortScore = now + Mathf.Max(0, item.DelayTime());
                            item.State = TimerState.Scheduled;
                            heap.Insert(item);
                        }
                    }
                }
            }
        }

        /// <summary>输出两个时钟值、各堆待处理数量与堆顶到期分数，用于运行期排查。</summary>
        public void Dump()
        {
            Debug.Log($"Timer: scaled={mCurrentScaleTime}, unscaled={mCurrentUnscaledTime}, " +
                $"scaledCount={mScaleTimeHeap.Count}, unscaledCount={mUnscaledTimeHeap.Count}, " +
                $"nextScaled={mScaleTimeHeap.Top()?.SortScore.ToString() ?? "none"}, " +
                $"nextUnscaled={mUnscaledTimeHeap.Top()?.SortScore.ToString() ?? "none"}", this);
        }
    }
}
