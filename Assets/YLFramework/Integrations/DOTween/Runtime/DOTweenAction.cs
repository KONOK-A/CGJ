/****************************************************************************
 * 适配自本地 ActionKit DOTween 集成。
 * Copyright (c) 2016 - 2023 liangxiegame UNDER MIT License
 *
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * https://gitee.com/liangxiegame/QFramework
 * DOTween itself is separately licensed by Demigiant: https://dotween.demigiant.com/license.php
 ****************************************************************************/

using System;
using System.Threading;
using DG.Tweening;

namespace YLFramework.Actions
{
    /// <summary>把一个独占持有的 Tween 桥接到 ActionKit 动作生命周期。</summary>
    /// <remarks>
    /// 绑定或启动必须使用 Unity 主线程及原始 UnitySynchronizationContext；Tween 由 DOTween 驱动，不由动作时间增量推进。
    /// Tween 正常完成才算动作完成；未完成的 Kill(false) 或归属标记丢失会取消动作。
    /// 取消不触发正常完成；仅向 Unity 上下文延迟投递 Kill(false)，不保证立即终止或终止已失去归属的外部 Tween。
    /// </remarks>
    public class DOTweenAction : IAction, IActionExecutionVersion
    {
        private static readonly ActionPool<DOTweenAction> mPool = new ActionPool<DOTweenAction>(() => new DOTweenAction(), null, 10);
        private Func<Tween> mTweenGetter;
        private TweenLease mLease;
        private ulong mExecutionVersion;
        private bool mPaused;
        private bool mBound;
        private bool mBoundStarted;
        private static int mMainThread;
        ulong IActionExecutionVersion.ExecutionVersion => mExecutionVersion;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CaptureMainThread() => mMainThread = Thread.CurrentThread.ManagedThreadId;
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void CaptureEditorThread() => CaptureMainThread();
#endif

        private static SynchronizationContext RequireContext()
        {
            var context = SynchronizationContext.Current;
            var type = context?.GetType();
            if (Thread.CurrentThread.ManagedThreadId != mMainThread || type == null ||
                type.FullName != "UnityEngine.UnitySynchronizationContext" ||
                type.Assembly != typeof(UnityEngine.Object).Assembly || !type.IsSealed)
                throw new InvalidOperationException("DOTween actions require the Unity main thread and its original UnitySynchronizationContext.");
            return context;
        }

        private DOTweenAction() { }

        /// <summary>分配动作，启动时才调用 Tween 工厂。</summary>
        /// <remarks>每次重新执行应提供新的有效 Tween，不要复用已被 Kill 的实例。</remarks>
        public static DOTweenAction Allocate(Func<Tween> tweenGetter)
        {
            if (tweenGetter == null) throw new ArgumentNullException(nameof(tweenGetter));
            var action = mPool.Allocate();
            action.ActionID = ActionKit.ID_GENERATOR++;
            action.Deinited = false;
            action.Reset();
            action.mBound = false;
            action.mBoundStarted = false;
            action.mTweenGetter = tweenGetter;
            return action;
        }

        /// <summary>读取或设置暂停标记，并同步到仍由动作持有的 Tween。</summary>
        /// <remarks>恢复只撤销动作发起的暂停，不接管外部暂停。</remarks>
        public bool Paused
        {
            get => mPaused;
            set { mPaused = value; mLease?.SetPaused(value); }
        }
        /// <summary>读取或设置动作已释放的标记。</summary>
        public bool Deinited { get; set; }
        /// <summary>读取或设置当前分配的动作标识，用于隔离旧 Tween 回调。</summary>
        public ulong ActionID { get; set; }
        /// <summary>读取或设置动作执行状态。</summary>
        public ActionStatus Status { get; set; }

        /// <summary>重置执行状态，按启动阶段保留或关闭 Tween 持有关系。</summary>
        /// <remarks>首次启动前保留 ToAction 的原绑定；启动后重置会安排 Kill(false)，重放应使用工厂创建新 Tween。</remarks>
        public void Reset()
        {
            mExecutionVersion++;
            Status = ActionStatus.NotStart;
            mPaused = false;
            // 组合动作在首次启动前会重置子动作；保留原绑定，不重新占用 Tween。
            if (mBound && !mBoundStarted && mLease != null)
            {
                mLease.RebaseVersion(mExecutionVersion);
                return;
            }
            Release(true);
        }

        /// <summary>释放动作并排队回收，不触发正常完成。</summary>
        /// <remarks>对仍持有的 Tween 安排延迟 Kill(false)；返回时不保证 Tween 已停止。</remarks>
        public void Deinit()
        {
            if (Deinited) return;
            Deinited = true;
            mTweenGetter = null;
            try { Release(true); }
            finally { ActionQueue.AddCallback(new ActionQueueRecycleCallback<DOTweenAction>(mPool, this)); }
        }

        private void Release(bool kill)
        {
            var lease = mLease;
            mLease = null;
            lease?.Close(kill);
        }

        /// <summary>在 Unity 主线程取得或使用已绑定的 Tween，并从头播放。</summary>
        public void OnStart()
        {
            var context = RequireContext();
            if (mBound)
            {
                var bound = mLease;
                if (bound == null || !IsCurrent(bound)) { Deinit(); return; }
                mBoundStarted = true;
                try
                {
                    bound.Restart();
                    if (IsCurrent(bound)) bound.SetPaused(mPaused);
                }
                catch (Exception original)
                {
                    if (IsCurrent(bound))
                    {
                        try { Deinit(); }
                        catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
                    }
                    throw;
                }
                return;
            }
            var lease = new TweenLease(this, context);
            mLease = lease;
            try
            {
                // 每次执行只持有工厂返回的一个 Tween；Reset 后应为新执行提供新 Tween。
                lease.Attach(mTweenGetter());
                if (!IsCurrent(lease)) return;
                lease.Restart();
                if (IsCurrent(lease)) lease.SetPaused(mPaused);
            }
            catch (Exception original)
            {
                if (IsCurrent(lease))
                {
                    try { Deinit(); }
                    catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
                }
                throw;
            }
        }

        /// <summary>检查 Tween 的完成或取消状态，不用 dt 推进播放。</summary>
        public void OnExecute(float dt) => mLease?.Poll();
        /// <summary>正常完成时只解除桥接回调，不主动 Kill Tween。</summary>
        public void OnFinish() => Release(false);

        private bool IsCurrent(TweenLease lease) => !Deinited && ActionID == lease.ActionId &&
            mExecutionVersion == lease.Version && ReferenceEquals(mLease, lease);

        internal static DOTweenAction Bind(Tween tween)
        {
            var context = RequireContext();
            ValidateTween(tween);
            var action = Allocate(() => tween);
            action.mBound = true;
            var lease = new TweenLease(action, context);
            action.mLease = lease;
            try { lease.Attach(tween); tween.Pause(); }
            catch { action.Deinit(); throw; }
            return action;
        }

        internal static void ValidateTween(Tween tween)
        {
            if (tween == null) throw new ArgumentNullException(nameof(tween));
            if (!tween.IsActive()) throw new InvalidOperationException("A killed tween cannot be wrapped; create a new tween in the factory.");
            if (tween.onKill == null) return;
            foreach (var callback in tween.onKill.GetInvocationList())
            {
                if (callback.Target is TweenLease)
                    throw new InvalidOperationException("The tween is already owned by an active ActionKit execution.");
            }
        }

        private sealed class TweenLease
        {
            internal readonly ulong ActionId;
            internal ulong Version { get; private set; }
            internal bool mClosed;
            private DOTweenAction mTarget;
            private Tween mTween;
            private readonly TweenCallback mCompletedCallback, mKilledCallback;
            private TweenCallback mVerifiedKillCallback;
            private bool mCompleted, mKilled, mPausedByAction;
            private readonly SynchronizationContext mContext;

            internal TweenLease(DOTweenAction target, SynchronizationContext context)
            {
                mTarget = target; ActionId = target.ActionID; Version = target.mExecutionVersion;
                mCompletedCallback = Completed; mKilledCallback = Killed;
                mContext = context;
            }

            internal void RebaseVersion(ulong version) => Version = version;

            internal void Attach(Tween tween)
            {
                ValidateTween(tween);
                mTween = tween;
                tween.onComplete += mCompletedCallback;
                tween.onKill += mKilledCallback;
                if (mClosed) { mTween = null; ScheduleKill(tween); }
            }

            private bool HasMarker(Tween tween)
            {
                if (tween == null || !tween.IsActive() || tween.onKill == null) return false;
                // 缓存已验证的委托引用，避免每帧分配调用列表。
                if (ReferenceEquals(tween.onKill, mVerifiedKillCallback)) return true;
                if (Array.IndexOf(tween.onKill.GetInvocationList(), mKilledCallback) < 0) return false;
                mVerifiedKillCallback = tween.onKill;
                return true;
            }
            private bool Owns(Tween tween) => !mClosed && HasMarker(tween);

            internal void Restart()
            {
                if (Owns(mTween))
                {
                    mCompleted = false;
                    mTween.Restart();
                }
                Poll();
            }

            internal void SetPaused(bool paused)
            {
                var tween = mTween;
                if (!Owns(tween)) return;
                if (paused)
                {
                    if (!tween.IsPlaying()) return;
                    mPausedByAction = true;
                    tween.Pause();
                }
                else if (mPausedByAction)
                {
                    mPausedByAction = false;
                    tween.Play();
                }
            }

            private void Completed()
            {
                if (mClosed) return;
                mCompleted = true;
                Deliver();
            }

            private void Killed()
            {
                if (mClosed) return;
                var tween = mTween;
                // DOTween 1.2.825 在仍活跃时调用 onKill，随后重置完成状态与回调字段。
                if (Owns(tween) && tween.IsComplete()) mCompleted = true;
                mTween = null;
                mKilled = true;
                Deliver();
            }

            private void Deliver()
            {
                var target = mTarget;
                if (target == null || !target.IsCurrent(this)) return;
                if (target.mBound && !target.mBoundStarted)
                {
                    if (mKilled) target.Deinit();
                    return;
                }
                if (mCompleted) target.Finish();
                else if (mKilled) target.Deinit();
            }

            internal void Poll()
            {
                if (mClosed) return;
                if (!mCompleted && !mKilled)
                {
                    var tween = mTween;
                    if (!Owns(tween)) mKilled = true;
                    else if (tween.IsComplete()) mCompleted = true;
                }
                Deliver();
            }

            internal void Close(bool kill)
            {
                if (mClosed) return;
                var tween = mTween;
                var owned = Owns(tween);
                mClosed = true; mTarget = null; mTween = null;
                if (!owned) return; // 归属标记丢失时，对象可能已被回收并交给其他持有者。
                if (kill) ScheduleKill(tween);
                else { tween.onComplete -= mCompletedCallback; tween.onKill -= mKilledCallback; }
            }

            private void ScheduleKill(Tween tween)
            {
                // 保留占用标记，等 Unity 离开当前 DOTween 回调栈后再清理。
                mContext.Post(_ =>
                {
                    if (Thread.CurrentThread.ManagedThreadId != mMainThread || !HasMarker(tween)) return;
                    foreach (var callback in tween.onKill.GetInvocationList())
                        if (callback.Target is TweenLease other && !ReferenceEquals(other, this)) return;
                    tween.onComplete -= mCompletedCallback;
                    tween.onKill -= mKilledCallback;
                    tween.Kill(false);
                }, null);
            }
        }
    }

    /// <summary>DOTween 与 ActionKit 动作的桥接扩展。</summary>
    public static class ActionKitDOTweenExtension
    {
        /// <summary>立即绑定并暂停已有 Tween，动作启动时从头播放。</summary>
        /// <remarks>绑定需 Unity 主线程及原始同步上下文；该绑定仅用于首次执行，重复执行应改用 Tween 工厂。</remarks>
        public static IAction ToAction(this Tween self)
        {
            return DOTweenAction.Bind(self);
        }

        /// <summary>将启动时创建 Tween 的动作追加到序列，并返回原序列。</summary>
        /// <remarks>工厂每次执行应创建新的有效 Tween；播放的时间模式仍由 DOTween 控制。</remarks>
        public static ISequence DOTween(this ISequence self, Func<Tween> tweenGetter)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return self.Append(DOTweenAction.Allocate(tweenGetter));
        }
    }
}
