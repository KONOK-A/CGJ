/****************************************************************************
 * Copyright (c) 2015 - 2024 liangxiegame UNDER MIT License
 * https://github.com/liangxiegame/QFramework
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace YLFramework.Actions
{
    // 在所属 GameObject 每帧推进动作任务：所有 Start* 路径的运行时实现，对象、场景与全局宿主共用同一套调度逻辑。
    // 任务记录在 mTasks 中按创建顺序推进；释放与回收集中在最外层执行栈退出时进行，避免回调期间复用对象。
    [ExecuteAlways]
    internal class MonoUpdateActionExecutor : MonoBehaviour, IActionExecutor
    {
        // 一次调度的运行时记录：绑定动作与控制器，并保存用于检测租约失效的标识快照。
        public class ActionTask
        {
            public IAction Action;
            public IActionController Controller;
            public Action<IActionController> OnFinish;
            // 调度时快照的动作标识；运行中与当前值不一致说明动作已被复用，任务应立即作废。
            internal ulong ActionID;
            // 调度时快照的控制器代次，用于识别控制器被池复用。
            internal uint ControllerGeneration;
            // 已结束（完成、被取消或被判定失效）；ReleaseFinished 会回收 Done 的任务。
            internal bool Done;
            // 命名任务的名字；匿名任务为 null，不进入名字索引。
            internal string Flag;
            // 名字登记代次；句柄用它确认自己引用的仍是登记时的那个任务。
            internal ulong NameToken;
        }

        private readonly List<ActionTask> mTasks = new List<ActionTask>();
        // 当前执行嵌套深度；大于 0 时说明有任务正在回调栈中执行，宿主释放要等栈退出。
        private int mExecutionDepth;
        private bool mStopping;
        // 匿名执行不创建名字索引，也不分配额外包装对象。
        private Dictionary<string, ActionTask> mNamedTasks;
        private ulong mNextNameToken;
        private sealed class ActionIdentity : IEqualityComparer<IAction>
        {
            public bool Equals(IAction first, IAction second) => ReferenceEquals(first, second);
            public int GetHashCode(IAction value) => RuntimeHelpers.GetHashCode(value);
        }
        // 全局唯一所有权：某个动作实例同一时间只能被一个执行任务持有，防止池对象被重复调度或跨执行器共享。
        private static readonly Dictionary<IAction, ActionTask> Owners =
            new Dictionary<IAction, ActionTask>(new ActionIdentity());
        // 模块级执行作用域计数（所有执行器共享）；归零时处理被延迟的 ActionKit.Shutdown。
        internal static int ExecutionScopes { get; private set; }
        internal static void EnterExecutionScope() => ExecutionScopes++;
        internal static void LeaveExecutionScope()
        {
            ExecutionScopes--;
            if (ExecutionScopes == 0) ActionKit.CompletePendingShutdown();
        }

        // ActionTask 池化复用；回收时清空全部字段，避免旧任务引用被带到下一次调度。
        private static readonly ActionPool<ActionTask> mActionTaskPool = new ActionPool<ActionTask>(
            () => new ActionTask(), task =>
            {
                task.Action = null;
                task.Controller = null;
                task.OnFinish = null;
                task.ActionID = 0;
                task.ControllerGeneration = 0;
                task.Done = false;
                task.Flag = null;
                task.NameToken = 0;
            }, 50);

        internal int ScheduledCount => mTasks.Count;
        internal bool IsStopping => mStopping;

        public void Execute(IActionController controller, Action<IActionController> onFinish = null)
        {
            Execute(controller, onFinish, null);
        }

        internal static void ValidateFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag))
                throw new ArgumentException("A named action requires a non-blank flag.", nameof(flag));
        }

        // 调度前校验：动作非空、未清理，且没有被其他执行任务以相同标识持有。
        internal static void ValidateAction(IAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Deinited)
                throw new InvalidOperationException("Allocate a new action before scheduling a deinitialized action.");
            if (Owners.TryGetValue(action, out var registered) && registered.ActionID == action.ActionID)
                throw new InvalidOperationException("The same action already has an executor.");
        }

        // 在指定 GameObject 上查找仍可用的执行器；create 为 true 时按需添加组件，否则返回 null。
        internal static MonoUpdateActionExecutor Find(GameObject owner, bool create = false)
        {
            if (!owner) return null;
            foreach (var candidate in owner.GetComponents<MonoUpdateActionExecutor>())
                if (!candidate.IsStopping) return candidate;
            return create ? owner.AddComponent<MonoUpdateActionExecutor>() : null;
        }

        // 命名启动：先验证，再按 replace 语义取消旧同名任务，登记新名字并立即做首次推进；
        // 动作若同步完成，返回的句柄在返回时已失效。
        internal ActionHandle StartNamed(IAction action, string flag, bool replace,
            Action<IActionController> onFinish)
        {
            ValidateFlag(flag);
            ValidateAction(action);
            if (mStopping || ActionKit.IsShuttingDown)
                throw new InvalidOperationException("ActionKit executor is shutting down.");
            if (TryGetNamed(flag, out var previous))
            {
                if (!replace) throw new InvalidOperationException("An active action already uses flag: " + flag);
                previous.Cancel();
            }
            // 取消时的清理回调可能已重入登记同名任务或关闭宿主；不覆盖它，也不复活旧任务。
            if (!this || mStopping || ActionKit.IsShuttingDown)
                throw new InvalidOperationException("ActionKit executor is shutting down.");
            if (TryGetNamed(flag, out _))
                throw new InvalidOperationException("An active action already uses flag: " + flag);
            ValidateAction(action);
            var controller = (ActionController)ActionController.Allocate();
            var generation = controller.Generation;
            controller.Action = action;
            controller.ActionID = action.ActionID;
            controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
            try { return Execute(controller, onFinish, flag); }
            catch
            {
                if (controller.Generation == generation) controller.Recycle();
                throw;
            }
        }

        // 查询命名任务：若登记仍在但租约已失效，会顺手剔除这条过期登记，避免句柄指向死任务。
        internal bool TryGetNamed(string flag, out ActionHandle handle)
        {
            ValidateFlag(flag);
            handle = default;
            if (mStopping || ActionKit.IsShuttingDown || mNamedTasks == null ||
                !mNamedTasks.TryGetValue(flag, out var task)) return false;
            handle = new ActionHandle(this, task);
            if (handle.IsActive) return true;
            RemoveName(task);
            handle = default;
            return false;
        }

        // 句柄操作共用的租约复核：名字代次、任务身份、动作标识与控制器代次全部匹配且都在效时才为 true；
        // 只要动作已 Finished、已清理或控制器被复用，就删除名字登记并返回 false。
        internal bool IsNamedLeaseActive(ActionTask task, ulong token, IAction action, ulong actionId,
            ActionController controller, uint generation)
        {
            if (mStopping || ActionKit.IsShuttingDown || task == null || token == 0 ||
                task.NameToken != token || task.Done || !ReferenceEquals(task.Action, action) ||
                task.ActionID != actionId || !ReferenceEquals(task.Controller, controller) ||
                task.ControllerGeneration != generation) return false;
            if (action.ActionID == actionId && !action.Deinited && action.Status != ActionStatus.Finished &&
                controller.Generation == generation && HasController(task)) return true;
            RemoveName(task);
            return false;
        }

        // 取消命名任务：先移出名字索引并标记 Done，再在受控执行作用域内清理动作，
        // 栈退出时统一回收任务、控制器与动作对象。
        internal void CancelNamed(ActionTask task)
        {
            task.Done = true;
            RemoveName(task);
            mExecutionDepth++;
            EnterExecutionScope();
            Exception executionError = null;
            try { task.Controller.Deinit(); }
            catch (Exception exception)
            {
                executionError = exception;
                throw;
            }
            finally
            {
                try { ExitExecution(); }
                catch (Exception cleanupError)
                {
                    if (executionError == null) throw;
                    AddDiagnostic(executionError, "ActionKit.CleanupException", cleanupError);
                }
            }
        }

        private void RemoveName(ActionTask task)
        {
            if (task.Flag == null) return;
            // 只删除仍属于本任务的登记：同名名字可能已被取消回调重入替换成新任务。
            if (mNamedTasks != null && mNamedTasks.TryGetValue(task.Flag, out var registered) &&
                ReferenceEquals(registered, task) && registered.NameToken == task.NameToken)
                mNamedTasks.Remove(task.Flag);
            task.Flag = null;
            task.NameToken = 0;
        }

        // 调度入口：校验动作所有权与重复调度，登记任务（可选命名），随后以 dt=0 做首次同步推进（触发 OnStart，可立即完成）。
        private ActionHandle Execute(IActionController controller, Action<IActionController> onFinish, string flag)
        {
            if (mStopping || ActionKit.IsShuttingDown)
                throw new InvalidOperationException("ActionKit executor is shutting down.");
            if (controller == null || controller.Action == null || controller.ActionID != controller.Action.ActionID)
                throw new ArgumentException("The controller must identify the action it schedules.", nameof(controller));
            if (controller.Action.Deinited)
                throw new InvalidOperationException("Allocate a new action before scheduling a deinitialized action.");
            if (Owners.TryGetValue(controller.Action, out var registered) && registered.ActionID == controller.ActionID)
                throw new InvalidOperationException("The same action already has an executor.");
            foreach (var existing in mTasks)
            {
                if (ReferenceEquals(existing.Action, controller.Action) && existing.ActionID == controller.ActionID)
                    throw new InvalidOperationException("The same action cannot be started twice on this executor.");
            }

            var task = mActionTaskPool.Allocate();
            task.Action = controller.Action;
            task.Controller = controller;
            task.OnFinish = onFinish;
            task.ActionID = controller.ActionID;
            task.ControllerGeneration = controller is ActionController concrete ? concrete.Generation : 0;
            mTasks.Add(task);
            Owners[task.Action] = task;
            var handle = default(ActionHandle);
            if (flag != null)
            {
                if (mNamedTasks == null) mNamedTasks = new Dictionary<string, ActionTask>(StringComparer.Ordinal);
                task.Flag = flag;
                task.NameToken = ++mNextNameToken;
                if (task.NameToken == 0) task.NameToken = ++mNextNameToken;
                mNamedTasks.Add(flag, task);
                handle = new ActionHandle(this, task);
            }
            mExecutionDepth++;
            EnterExecutionScope();
            Exception executionError = null;
            try
            {
                // 动作若带着 Finished 状态再次被调度（尚未回收），先 Reset 回到起点再推进。
                if (task.Action.Status == ActionStatus.Finished) task.Action.Reset();
                if (mStopping || ActionKit.IsShuttingDown) task.Done = true;
                else Step(task, 0);
            }
            catch (Exception exception)
            {
                task.Done = true;
                executionError = exception;
                throw;
            }
            finally
            {
                try { ExitExecution(); }
                catch (Exception cleanupError)
                {
                    if (executionError == null) throw;
                    AddDiagnostic(executionError, "ActionKit.CleanupException", cleanupError);
                }
            }
            return handle;
        }

        private void Update()
        {
            // ExecuteAlways 用于编辑器生命周期清理，编辑状态不自动推进动作。
            if (Application.IsPlaying(gameObject)) Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        // 每次宿主 Update 调用一次：按任务创建顺序推进，每个任务按自身控制器的 UpdateMode 选择时间增量。
        internal void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (mStopping || ActionKit.IsShuttingDown) return;
            // 固定本轮迭代上限：推进期间新启动的任务已完成首次推进，留到下一轮再走常规更新。
            var count = mTasks.Count;
            mExecutionDepth++;
            EnterExecutionScope();
            Exception executionError = null;
            try
            {
                for (var index = 0; index < count && !mStopping; index++)
                {
                    var task = mTasks[index];
                    if (task.Done) continue;
                    var mode = task.Controller.UpdateMode;
                    if (mode == ActionUpdateModes.ScaledDeltaTime) Step(task, scaledDeltaTime);
                    else if (mode == ActionUpdateModes.UnscaledDeltaTime) Step(task, unscaledDeltaTime);
                }
            }
            catch (Exception exception)
            {
                executionError = exception;
                throw;
            }
            finally
            {
                try { ExitExecution(); }
                catch (Exception cleanupError)
                {
                    if (executionError == null) throw;
                    AddDiagnostic(executionError, "ActionKit.CleanupException", cleanupError);
                }
            }
        }

        // 控制器、动作与代次三方匹配：任何一方被复用或失配都视为任务失效。
        private static bool HasController(ActionTask task) =>
            task.Controller.ActionID == task.ActionID && ReferenceEquals(task.Controller.Action, task.Action) &&
            (!(task.Controller is ActionController concrete) || concrete.Generation == task.ControllerGeneration);

        private void Step(ActionTask task, float dt)
        {
            try
            {
                // 推进前后都复核租约：动作可能在 OnStart/OnExecute 中取消自己、被替换或触发 Shutdown。
                if (task.Done || !HasController(task) || task.Action.ActionID != task.ActionID || task.Action.Deinited)
                {
                    task.Done = true;
                    return;
                }
                var completed = task.Action.Execute(dt);
                if (task.Done || !HasController(task) || task.Action.ActionID != task.ActionID || task.Action.Deinited)
                {
                    task.Done = true;
                    return;
                }
                if (!completed) return;
                task.Done = true;
                RemoveName(task);
                // 仅在未停止且租约仍有效时通知正常完成；回调抛出的异常会向上传播，并在执行栈退出时完成清理。
                if (!mStopping && HasController(task) && !task.Action.Deinited) task.OnFinish?.Invoke(task.Controller);
            }
            catch
            {
                task.Done = true;
                RemoveName(task);
                throw;
            }
        }

        // 宿主释放或 Shutdown 时停用执行器：不再接受新任务、清空名字索引；
        // 若当前没有执行中的回调则立即释放全部任务（不触发正常完成通知），否则等最外层执行退出后释放。
        internal void StopAll()
        {
            mStopping = true;
            mNamedTasks?.Clear();
            if (mExecutionDepth == 0) ReleaseFinished();
        }

        private void ExitExecution()
        {
            mExecutionDepth--;
            Exception releaseError = null;
            try
            {
                // 只有最外层执行退出时才统一释放：更内层的回调仍可能在访问任务与控制器。
                if (mExecutionDepth == 0) ReleaseFinished();
            }
            catch (Exception exception)
            {
                releaseError = exception;
                throw;
            }
            finally
            {
                try { LeaveExecutionScope(); }
                catch (Exception shutdownError)
                {
                    if (releaseError == null) throw;
                    AddDiagnostic(releaseError, "ActionKit.ShutdownException", shutdownError);
                }
            }
        }

        private static void AddDiagnostic(Exception primary, string key, Exception secondary)
        {
            primary.Data[key] = primary.Data[key] is Exception previous
                ? new AggregateException(previous, secondary) : secondary;
        }

        private void ReleaseFinished()
        {
            // 逆序移除（原地删除不影响前面的索引）；mStopping 时连未 Done 的任务一起释放，即取消语义。
            List<Exception> errors = null;
            for (var index = mTasks.Count - 1; index >= 0; index--)
            {
                var task = mTasks[index];
                if (!mStopping && !task.Done) continue;
                mTasks.RemoveAt(index);
                RemoveName(task);
                if (Owners.TryGetValue(task.Action, out var owner) && ReferenceEquals(owner, task)) Owners.Remove(task.Action);
                try
                {
                    // 依次释放动作、归还控制器、回收任务对象；每步独立收集异常，保证释放流程完整。
                    if (task.Action.ActionID == task.ActionID && !task.Action.Deinited) task.Action.Deinit();
                }
                catch (Exception exception)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(exception);
                }
                finally
                {
                    try
                    {
                        if (HasController(task)) task.Controller.Recycle();
                    }
                    catch (Exception exception)
                    {
                        if (errors == null) errors = new List<Exception>();
                        errors.Add(exception);
                    }
                    finally
                    {
                        mActionTaskPool.Recycle(task);
                    }
                }
            }
            if (errors != null) throw new AggregateException("ActionKit action cleanup failed.", errors);
        }

        private void OnDestroy() => StopAll();
    }

    /// <summary>将动作交给组件所属 GameObject 的 Update 执行器。</summary>
    public static class MonoUpdateActionExecutorExtension
    {
        /// <summary>复用或添加模块执行器，立即接管控制器并首次推进动作。</summary>
        /// <typeparam name="T">宿主组件的类型。</typeparam>
        /// <param name="self">执行器所在 GameObject 的组件。</param>
        /// <param name="action">返回给调用方的动作，应与 controller.Action 相同。</param>
        /// <param name="controller">已绑定动作和匹配标识的控制器。</param>
        /// <param name="onFinish">正常完成且清理前调用的回调。</param>
        /// <returns>传入的动作；实际调度以 controller.Action 为准。</returns>
        public static IAction ExecuteByUpdate<T>(this T self, IAction action, IActionController controller,
            Action<IActionController> onFinish = null) where T : MonoBehaviour
        {
            MonoUpdateActionExecutor executor = null;
            foreach (var candidate in self.gameObject.GetComponents<MonoUpdateActionExecutor>())
            {
                if (!candidate.IsStopping) { executor = candidate; break; }
            }
            if (!executor) executor = self.gameObject.AddComponent<MonoUpdateActionExecutor>();
            executor.Execute(controller, onFinish);
            return action;
        }
    }
}
