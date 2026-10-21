using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Actions
{
    /// <summary>
    /// 在同次执行中逐一推进未完成子动作的组合接口，不使用多线程。
    /// </summary>
    /// <remarks>非空组合在全部子动作完成后结束；空组合不会自动完成。</remarks>
    public interface IParallel : ISequence
    {
    }

    // 并行组合：每次执行按加入顺序逐一推进未完成子动作，全部完成后标记完成；
    // 任一子动作取消或租约失效都会取消整个组合，组合自身取消时会释放所有仍匹配的子动作。
    internal class Parallel : IParallel
    {
        private Parallel()
        {
        }

        private static ActionPool<Parallel> mPool =
            new ActionPool<Parallel>(() => new Parallel(), null, 5);

        private List<ActionLease> mActions = new List<ActionLease>();

        private int mFinishedCount = 0;

        public static Parallel Allocate()
        {
            var parallel = mPool.Allocate();
            parallel.ActionID = ActionKit.ID_GENERATOR++;
            parallel.Deinited = false;
            parallel.Reset();
            return parallel;
        }
        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus Status { get; set; }
        public void OnStart()
        {
            TryCancelIfChildDeinited();
        }

        public void OnExecute(float dt)
        {
            if (TryCancelIfChildDeinited()) return;
            for (var i = mFinishedCount; i < mActions.Count; i++)
            {
                bool finished;
                try
                {
                    finished = mActions[i].Action.Execute(dt);
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
                if (TryCancelIfChildDeinited()) return;
                if (!finished) continue;

                mFinishedCount++;

                if (mFinishedCount >= mActions.Count)
                {
                    this.Finish();
                }
                else
                {
                    // 将已完成动作移入前段，后续只推进未完成动作。
                    (mActions[i], mActions[mFinishedCount - 1]) = (mActions[mFinishedCount - 1], mActions[i]);
                }
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

        private bool TryCancelIfChildDeinited()
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

        public void Deinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                // 先快照并清空容器，避免清理回调改变本次迭代；只释放仍匹配的租约。
                var actions = mActions.ToArray();
                mActions.Clear();
                mFinishedCount = 0;
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
                    ActionQueue.AddCallback(new ActionQueueRecycleCallback<Parallel>(mPool, this));
                }
                if (exceptions != null) throw new AggregateException(exceptions);
            }
        }

        public void Reset()
        {
            Status = ActionStatus.NotStart;
            mFinishedCount = 0;
            Paused = false;
            if (TryCancelIfChildDeinited()) return;
            var actionId = ActionID;
            // Reset 可能释放订阅并修改或回收其他子动作，快照与租约检查保护本次迭代。
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
    /// 向组合追加并行子组合的链式扩展。
    /// </summary>
    public static class ParallelExtension
    {
        /// <summary>
        /// 立即配置并追加并行子组合；其子动作在同次执行中分别推进。
        /// </summary>
        /// <param name="self">要追加并行子组合的父组合。</param>
        /// <param name="parallelSetting">追加前立即执行的并行子组合配置回调。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Parallel(this ISequence self, Action<ISequence> parallelSetting)
        {
            var parallel = global::YLFramework.Actions.Parallel.Allocate();
            parallelSetting(parallel);
            return self.Append(parallel);
        }
    }
}
