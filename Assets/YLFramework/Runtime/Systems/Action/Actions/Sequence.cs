using System;
using System.Collections.Generic;

namespace YLFramework.Actions
{
    /// <summary>
    /// 可追加子动作的组合接口，供顺序、并行和重复组合共用。
    /// </summary>
    public interface ISequence : IAction
    {
        /// <summary>
        /// 追加子动作，由组合在 Deinit 时负责释放；具体执行顺序由组合类型决定。
        /// </summary>
        /// <param name="action">由组合管理生命周期的子动作。</param>
        /// <returns>当前组合，可继续追加动作。</returns>
        ISequence Append(IAction action);
    }

    // 顺序组合：同一时间只推进一个子动作；启动和切换子动作时会连续执行即时完成的步骤，切换出的后续动作以 dt=0 起步，不继承剩余时间。
    // 子动作取消或租约失效视为整个组合取消，组合取消时释放所有仍匹配的子动作。
    internal class Sequence : ISequence
    {
        private IAction mCurrentAction = null;
        private int mCurrentActionIndex = 0;
        private List<ActionLease> mActions = new List<ActionLease>();

        private Sequence()
        {
        }

        private static ActionPool<Sequence> mPool =
            new ActionPool<Sequence>(() => new Sequence(), null, 10);

        public static Sequence Allocate()
        {
            var sequence = mPool.Allocate();
            sequence.ActionID = ActionKit.ID_GENERATOR++;
            sequence.Reset();
            sequence.Deinited = false;
            return sequence;
        }

        public bool Paused { get; set; }

        public bool Deinited { get; set; }

        public ulong ActionID { get; set; }
        public ActionStatus Status { get; set; }

        public void OnStart()
        {
            if (TryCancelIfChildDeinited()) return;
            if (mActions.Count > 0)
            {
                mCurrentActionIndex = 0;
                mCurrentAction = mActions[mCurrentActionIndex].Action;
                mCurrentAction.Reset();
                TryExecuteUntilNextNotFinished();
            }
            else
            {
                this.Finish();
            }
        }

        void TryExecuteUntilNextNotFinished()
        {
            // 同步完成的子动作在同次调用中连续推进；后续动作以 dt=0 启动，不传递剩余时间。
            while (mCurrentAction != null)
            {
                if (TryCancelIfChildDeinited()) return;
                var finished = ExecuteChild(mCurrentAction, 0);
                if (TryCancelIfChildDeinited()) return;
                if (!finished) return;
                mCurrentActionIndex++;

                if (mCurrentActionIndex < mActions.Count)
                {
                    mCurrentAction = mActions[mCurrentActionIndex].Action;
                    mCurrentAction.Reset();
                }
                else
                {
                    mCurrentAction = null;
                    this.Finish();
                }
            }
        }

        public void OnExecute(float dt)
        {
            if (TryCancelIfChildDeinited()) return;
            if (mCurrentAction != null)
            {
                var finished = ExecuteChild(mCurrentAction, dt);
                if (TryCancelIfChildDeinited()) return;
                if (finished)
                {
                    mCurrentActionIndex++;

                    if (mCurrentActionIndex < mActions.Count)
                    {
                        mCurrentAction = mActions[mCurrentActionIndex].Action;
                        mCurrentAction.Reset();

                        TryExecuteUntilNextNotFinished();
                    }
                    else
                    {
                        this.Finish();
                    }
                }
            }
            else
            {
                this.Finish();
            }
        }

        public void OnFinish()
        {
        }

        public ISequence Append(IAction action)
        {
            mActions.Add(new ActionLease(action));
            return this;
        }

        internal bool TryCancelIfChildDeinited()
        {
            // 子动作取消或租约失效时取消整个组合，不能视为正常完成。
            if (Deinited) return true;
            foreach (var lease in mActions)
            {
                if (lease.Matches && !lease.Action.Deinited) continue;
                Deinit();
                return true;
            }
            return false;
        }

        private bool ExecuteChild(IAction action, float dt)
        {
            try
            {
                return action.Execute(dt);
            }
            catch (Exception exception)
            {
                try
                {
                    TryCancelIfChildDeinited();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(exception, cleanupException);
                }
                throw;
            }
        }

        public void Deinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                // 先快照并清空容器，避免清理回调改变本次迭代；只释放仍匹配的租约。
                var actions = mActions.ToArray();
                mActions.Clear();
                mCurrentAction = null;
                mCurrentActionIndex = 0;
                List<Exception> exceptions = null;
                try
                {
                    foreach (var lease in actions)
                    {
                        try
                        {
                            if (!lease.Matches) continue;
                            var action = lease.Action;
                            if (!ReferenceEquals(action, this) && !action.Deinited) action.Deinit();
                        }
                        catch (Exception exception)
                        {
                            if (exceptions == null) exceptions = new List<Exception>();
                            exceptions.Add(exception);
                        }
                    }
                }
                finally
                {
                    // 回收入队后延迟执行，不在此处立即复用对象。
                    ActionQueue.AddCallback(new ActionQueueRecycleCallback<Sequence>(mPool, this));
                }
                if (exceptions != null) throw new AggregateException(exceptions);
            }
        }

        public void Reset()
        {
            mCurrentActionIndex = 0;
            Status = ActionStatus.NotStart;
            Paused = false;
            if (TryCancelIfChildDeinited()) return;
            var actionId = ActionID;
            // 子动作 Reset 的清理回调可能追加或回收其他子动作，快照与租约检查保护本次迭代。
            var actions = mActions.ToArray();
            foreach (var lease in actions)
            {
                if (Deinited || ActionID != actionId) return;
                if (!lease.Matches || lease.Action.Deinited) { Deinit(); return; }
                lease.Action.Reset();
                if (Deinited || ActionID != actionId) return;
                if (!lease.Matches || lease.Action.Deinited) { Deinit(); return; }
            }
        }
    }

    /// <summary>
    /// 向组合追加嵌套顺序序列的链式扩展。
    /// </summary>
    public static class SequenceExtension
    {
        /// <summary>
        /// 立即配置并追加子序列；子序列按追加顺序执行动作。
        /// </summary>
        /// <param name="self">要追加子序列的父组合。</param>
        /// <param name="sequenceSetting">追加前立即执行的子序列配置回调。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Sequence(this ISequence self, Action<ISequence> sequenceSetting)
        {
            var sequence = global::YLFramework.Actions.Sequence.Allocate();
            sequenceSetting(sequence);
            return self.Append(sequence);
        }
    }
}
