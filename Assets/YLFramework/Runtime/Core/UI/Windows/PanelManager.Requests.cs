using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core
{
    public partial class PanelManager
    {
        // 每个异步消费者恰好一份记录进入 Module.pendingViews。批次只存在于当前提交栈，不是第二套 pending 权威。
        internal sealed class PanelOperation : IPendingViewRequest
        {
            internal Module Owner;
            private object lifecycle;
            internal readonly UIRootContext Root;
            internal readonly string Name;
            internal GameObject Prefab;
            private readonly bool named;
            internal readonly PanelRuleSnapshot Rules;
            private readonly PanelOverrides overrides;
            private readonly Type expected;
            internal readonly long Sequence;
            internal readonly bool Async;
            private readonly int thread = Thread.CurrentThread.ManagedThreadId;
            private readonly ICoreResourceProvider provider;
            private readonly List<string> projects;
            internal LoadViewArgs Args;
            internal CorePrefabLease Lease;
            private CoreResourceRequest<CorePrefabLease> resource;
            private readonly List<(GameObject Source, UIRootContext Root)> closedScopes = new List<(GameObject, UIRootContext)>();
            internal PanelConfig Settings;
            private bool prepared;
            internal bool Committed, Terminal;
            private bool failing;
            internal void MarkFailing()
            {
                if (failing) return;
                failing = true;
                BeginFailure?.Invoke();
            }
            private bool invalidating;
            internal Action BeginCommit;
            internal Action BeginFailure;
            internal Action<float> ProgressChanged;
            internal Action<Panel, Exception, LoadViewStatus> Completed;
            internal Panel Result;
            internal object DeliveredSession;
            internal Exception Failure;

            internal PanelOperation(Module owner, string name, GameObject prefab, LoadViewArgs args, UIRootContext root,
                PanelRuleSnapshot rules, PanelOverrides overrides, Type expected, long sequence, bool asynchronous)
            {
                Owner = owner; lifecycle = owner.ViewLifecycle; Name = name; Prefab = prefab; Args = args; Root = root;
                named = name != null; Rules = rules; this.overrides = overrides; this.expected = expected; Sequence = sequence; Async = asynchronous;
                provider = named ? CoreResourceAccess.Provider : null;
                projects = ModuleManager.ViewResourceProjects(owner);
                if (asynchronous)
                {
                    owner.pendingViews.Add(this);
                    Application.quitting += OnExecutionEnded;
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged += OnPlayChanged;
#endif
                }
            }
            private void OnExecutionEnded() => Finish(null, null, new InvalidOperationException("Panel 执行会话已结束。"), LoadViewStatus.Invalidated);
#if UNITY_EDITOR
            private void OnPlayChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) OnExecutionEnded(); }
#endif
            internal void Validate()
            {
                if (Terminal || invalidating || Owner == null || !ReferenceEquals(Owner.ViewLifecycle, lifecycle))
                    throw new InvalidOperationException("Panel 请求的 Module 代次已结束。");
                ViewManager.EnsureOwner(Owner);
                if (!named && !Prefab) throw new InvalidOperationException("直接 Panel 来源已销毁，不能退回命名加载。");
                if (!Root || !Root.HasValidStructure || !Root.gameObject.scene.isLoaded) throw new InvalidOperationException("Panel 请求的根已失效。");
                if (prepared && !Prefab) throw new InvalidOperationException("Panel 来源 Prefab 已销毁。");
                if (Lease != null && !Lease.IsValid) throw new InvalidOperationException("Panel 来源租约已失效。");
                foreach (var closed in closedScopes)
                    if (Prefab && ReferenceEquals(closed.Source, Prefab) && closed.Root == Root)
                        throw new InvalidOperationException("Panel 请求接受后，其单实例范围已关闭。");
            }
            internal bool KnownSame(PanelOperation other)
            {
                if (Owner != other.Owner || Root != other.Root) return false;
                if (Prefab && other.Prefab) return ReferenceEquals(Prefab, other.Prefab);
                if (!named || !other.named || !ReferenceEquals(provider, other.provider) || !StringComparer.Ordinal.Equals(Name, other.Name) || projects.Count != other.projects.Count) return false;
                for (int i = 0; i < projects.Count; i++) if (!StringComparer.Ordinal.Equals(projects[i], other.projects[i])) return false;
                return true;
            }
            internal void Prepare(GameObject resolved)
            {
                Validate();
                if (!prepared)
                {
                    Prefab = resolved;
                    var view = Prefab.GetComponent<View>();
                    if (!view) throw new InvalidOperationException("Panel 来源没有根 View 组件。");
                    if (!expected.IsInstanceOfType(view)) throw new InvalidCastException($"View {view.GetType().FullName} cannot be loaded as {expected.FullName}.");
                    if (!(view is Panel panel)) throw new InvalidCastException("Panel 入口需要根 Panel。");
                    Settings = overrides.ApplyTo(Rules.Resolve(Name, Prefab).ApplyTo(panel.Defaults));
                    prepared = true;
                }
                Validate();
            }
            internal void ResolveSync()
            {
                Validate();
                if (!named) { Prepare(Prefab); return; }
                CoreResourceAccess.RequireProvider(provider);
                Exception failure = null;
                foreach (var project in projects)
                {
                    try { Lease = provider.AcquirePrefab(project, Name); }
                    catch (Exception error) when (CoreResourceAccess.CanFallback(error)) { failure = error; }
                    Validate();
                    if (Lease == null) continue;
                    Prepare(Lease.Prefab);
                    return;
                }
                throw new InvalidOperationException("Panel prefab not found: " + Name, failure);
            }
            internal void ValidateAcquired(View view)
            {
                if (!view || !expected.IsInstanceOfType(view) || view.GetType() != Prefab.GetComponent<View>().GetType())
                    throw new InvalidCastException("Panel 实例后端交付的根类型与已解析 Prefab/请求类型不匹配。");
            }
            private IEnumerator ResolveAsync()
            {
                yield return null;
                Validate();
                if (Prefab) { Prepare(Prefab); yield break; }
                CoreResourceAccess.RequireProvider(provider);
                Exception failure = null;
                foreach (var project in projects)
                {
                    CoreResourceRequest<CorePrefabLease> acquired;
                    try { acquired = provider.AcquirePrefabAsync(project, Name); }
                    catch (Exception error) when (CoreResourceAccess.CanFallback(error)) { failure = error; continue; }
                    try { Validate(); }
                    catch { acquired?.Dispose(); throw; }
                    resource = acquired ?? throw new InvalidOperationException("Panel 资源请求为 null。");
                    while (resource.Status == CoreResourceStatus.Pending)
                    { Validate(); ProgressChanged?.Invoke(resource.Progress); yield return null; }
                    Validate();
                    var status = resource.Status;
                    if (status != CoreResourceStatus.Succeeded)
                    {
                        failure = resource.Error ?? new CoreResourceException(CoreResourceError.LoadFailed, "Panel 资源解析失败。");
                        resource.Dispose(); resource = null;
                        if (status == CoreResourceStatus.Canceled || status == CoreResourceStatus.Invalidated || !CoreResourceAccess.CanFallback(failure)) throw failure;
                        continue;
                    }
                    Lease = resource.TakeResult();
                    resource.Dispose(); resource = null;
                    Prepare(Lease.Prefab);
                    // 热资源也留出同帧消费者解析边界，所有数据回调仍晚于接受入口。
                    yield return null;
                    yield break;
                }
                throw new InvalidOperationException("Panel prefab not found: " + Name, failure);
            }
            internal IEnumerator Run()
            {
                var routine = ResolveAsync();
                Exception failure = null;
                while (!Terminal && !invalidating)
                {
                    bool more;
                    try { more = routine.MoveNext(); }
                    catch (Exception error) { failure = error; break; }
                    if (!more) break;
                    yield return routine.Current;
                }
                (routine as IDisposable)?.Dispose();
                if (Terminal || invalidating) yield break;
                if (failure == null)
                {
                    try { Instance.Execute(this); }
                    catch (Exception error) { failure = error; }
                }
                if (!Terminal && !invalidating) Finish(null, null, failure ?? new InvalidOperationException("Panel 生产未产生终态。"),
                    StillValid() ? LoadViewStatus.Failed : LoadViewStatus.Invalidated);
            }
            internal bool StillValid() { try { Validate(); return true; } catch { return false; } }
            internal void Abort()
            {
                if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Panel Abort 必须在接受线程执行。");
                if (Terminal || Committed || invalidating || failing) return;
                Finish(null, null, new OperationCanceledException("Panel 请求已取消。"), LoadViewStatus.Canceled);
            }
            void IPendingViewRequest.Invalidate()
            {
                if (Terminal) return;
                invalidating = true;
                Failure = new InvalidOperationException("Panel owner 生命周期已结束。");
                ReleaseInputs();
            }
            void IPendingViewRequest.PublishInvalidation()
            { invalidating = false; Finish(null, null, Failure, LoadViewStatus.Invalidated); }
            internal void AddClosedScope(GameObject source, UIRootContext root) => closedScopes.Add((source, root));

            internal void ReleaseInputs()
            {
                Application.quitting -= OnExecutionEnded;
#if UNITY_EDITOR
                EditorApplication.playModeStateChanged -= OnPlayChanged;
#endif
                Owner?.pendingViews.Remove(this);
                var oldArgs = Args; var oldLease = Lease; var oldRequest = resource;
                Args = null; Lease = null; resource = null; BeginCommit = null; BeginFailure = null;
                var errors = new List<Exception>();
                try { oldRequest?.Dispose(); } catch (Exception error) { errors.Add(error); }
                try { oldLease?.Dispose(); } catch (Exception error) { errors.Add(error); }
                try { oldArgs?.Release(); } catch (Exception error) { errors.Add(error); }
                ViewManager.ThrowCleanupErrors(errors);
            }
            internal void Finish(Panel result, object session, Exception failure, LoadViewStatus status)
            {
                if (Terminal || invalidating) return;
                if (status == LoadViewStatus.Succeeded)
                {
                    try
                    {
                        Validate();
                        if (!result || !result.IsLoaded || !ReferenceEquals(result.Session, session) || result.Module != Owner ||
                            !Instance.states.TryGetValue(result, out var state) || !ResourcesValid(state))
                            throw new InvalidOperationException("Panel 交付前原 Session 或资源责任已结束。");
                    }
                    catch (Exception error) { result = null; failure = error; status = LoadViewStatus.Invalidated; }
                }
                Terminal = true;
                try { ReleaseInputs(); }
                catch (Exception cleanup)
                {
                    if (failure == null) { failure = cleanup; result = null; status = LoadViewStatus.Failed; }
                    else StartUpModuleRequest.AttachCleanupError(failure, cleanup);
                }
                Result = result; DeliveredSession = result ? session : null; Failure = failure;
                var notify = Completed;
                Completed = null; ProgressChanged = null; Owner = null; lifecycle = null; Prefab = null; closedScopes.Clear();
                notify?.Invoke(result, failure, status);
            }
        }

        private static bool ResourcesValid(PanelState state)
        {
            if (state.Panel.BackendLease != null && !state.Panel.BackendLease.IsValid) return false;
            foreach (var pin in state.SourcePins) if (!pin.IsValid) return false;
            return ReferenceEquals(state.Source, null) || state.Source;
        }
        internal static bool IsPreparedValid(Panel panel) => _instance != null && _instance.states.TryGetValue(panel, out var state) &&
            _instance.Current(state) && state.Root && state.Root.HasValidStructure &&
            panel.GetComponentInParent<UIRootContext>(true) == state.Root && ResourcesValid(state);
        private void ValidateRules(PanelOperation operation, PanelState existing, List<PanelOperation> candidates)
        {
            int matched = operation.Rules.Match(operation.Name, operation.Prefab);
            void Check(string name)
            {
                int other = operation.Rules.Match(name, operation.Prefab);
                if (matched >= 0 && other >= 0 && matched != other) throw new ArgumentException("不同地址已证明指向同一 Prefab，但命中多条 Panel 规则。");
                if (other >= 0) matched = other;
            }
            foreach (var state in states.Values)
                if (Current(state) && state.Panel.Module == operation.Owner && state.Root == operation.Root && ReferenceEquals(state.Source, operation.Prefab))
                    foreach (var name in state.SourceNames) Check(name);
            foreach (var candidate in candidates) if (candidate.Prefab == operation.Prefab) Check(candidate.Name);
        }
        private void Execute(PanelOperation origin)
        {
            var rejected = new List<(PanelOperation Operation, Exception Error)>();
            try
            {
            origin.Validate();
            var candidates = new List<PanelOperation> { origin };
            foreach (var pending in origin.Owner.pendingViews)
                if (pending is PanelOperation candidate && !ReferenceEquals(candidate, origin) && !candidate.Terminal && origin.KnownSame(candidate)) candidates.Add(candidate);
            candidates.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            PanelState existing = null;
            foreach (var state in states.Values)
                if (Current(state) && state.Panel.Module == origin.Owner && state.Root == origin.Root && ReferenceEquals(state.Source, origin.Prefab))
                { existing = state; break; }
            if (!origin.Async && existing == null && candidates.Count > 1)
            {
                origin.Prepare(origin.Prefab);
                if (origin.Settings.InstanceMode == PanelInstanceMode.Single)
                    throw new InvalidOperationException("同范围 Single 异步首开仍在途，同步 LoadPanel 不接管其生产或意图。");
            }
            var valid = new List<PanelOperation>();
            foreach (var candidate in candidates)
            {
                try
                {
                    candidate.Prepare(origin.Prefab);
                    ValidateRules(candidate, existing, candidates);
                    ValidateOpen(candidate.Root, candidate.Settings, existing != null && candidate.Settings.InstanceMode == PanelInstanceMode.Single ? existing.Panel : null);
                    valid.Add(candidate);
                }
                catch (Exception error)
                {
                    if (!origin.Async && ReferenceEquals(candidate, origin)) throw;
                    rejected.Add((candidate, error));
                }
            }
            if (valid.Count == 0) return;
            var mode = existing != null ? existing.Panel.EffectiveSettings.InstanceMode : valid[0].Settings.InstanceMode;
            foreach (var candidate in valid.ToArray())
                if (candidate.Settings.InstanceMode != mode)
                {
                    var error = new InvalidOperationException("同范围活动会话或在途请求不能混用 Single/Multiple。");
                    if (!origin.Async && candidate == origin) throw error;
                    rejected.Add((candidate, error));
                    valid.Remove(candidate);
                }
            if (!valid.Contains(origin)) return;
            if (mode == PanelInstanceMode.Multiple)
            {
                // Multiple 只提交当前消费者；其他消费者仍由自己正常调度，不能被 Single 合流丢弃。
                valid = new List<PanelOperation> { origin };
                existing = null;
            }
            if (existing != null && (!existing.Panel.IsLoaded || existing.Panel.IsInitializing || existing.Panel.IsAttaching))
                throw new InvalidOperationException("Single Panel 仍在首开初始化，不能重开半成品会话。");
            var winner = valid[valid.Count - 1];
            Panel result = existing?.Panel;
            object resultSession = existing?.Session;
            try
            {
                if (existing != null && !ResourcesValid(existing))
                    throw new InvalidOperationException("Single 原会话的实例或来源租约已撤销，不能运行重开数据回调。");
                winner.BeginCommit = () => { foreach (var request in valid) request.Committed = true; };
                winner.BeginFailure = () => { foreach (var request in valid) request.MarkFailing(); };
                if (existing == null)
                {
                    var productionLease = winner.Lease;
                    if (productionLease == null)
                        foreach (var request in valid) if (request.Lease != null) { productionLease = request.Lease; break; }
                    result = ViewManager.CompleteAsync<Panel>(winner.Owner, winner.Prefab, winner.Name ?? winner.Prefab.name,
                        winner.Root.GetLayer(winner.Settings.Layer), winner.Root.gameObject.scene, winner.Args, null,
                        winner.Validate, productionLease, winner);
                    resultSession = result.Session;
                    existing = states[result];
                }
                else if (winner.Sequence > existing.IntentSequence)
                {
                    if (!Current(existing)) throw new InvalidOperationException("Single 原会话已结束，不能重开复用引用的新 Session。");
                    winner.Owner.viewTransactions++;
                    try
                    {
                        winner.Validate();
                        winner.BeginCommit();
                        result.Open(winner.Args);
                        winner.Validate();
                        if (!Current(existing) || !result.IsLoaded || !ResourcesValid(existing)) throw new InvalidOperationException("Panel 重开使原会话或资源责任失效。");
                        if (!CommitDisplay(existing, winner.Settings, true, winner.Sequence))
                            throw new InvalidOperationException("切层原生回调结束了原 Panel Session，不能提交旧打开意图。");
                        existing.IntentSequence = winner.Sequence;
                        Reconcile();
                    }
                    finally { winner.Owner.viewTransactions--; }
                }
                foreach (var operation in valid)
                {
                    operation.Validate();
                    if (operation.Name != null) existing.SourceNames.Add(operation.Name);
                    if (operation.Lease != null && ViewManager.IsDirectLease(result.BackendLease))
                    {
                        existing.SourcePins.Add(operation.Lease);
                        operation.Lease = null;
                        // Foundation 缓存不能接管 provider pin；本 Session 可保活，但 Close 必须 Discard 后释放来源。
                        result.DiscardOnExit = true;
                    }
                    operation.Committed = true;
                }
            }
            catch (Exception error)
            {
                var statuses = new Dictionary<PanelOperation, LoadViewStatus>();
                bool revoked = existing != null && Current(existing) && !ResourcesValid(existing);
                foreach (var operation in valid) statuses.Add(operation, !revoked && operation.StillValid() ? LoadViewStatus.Failed : LoadViewStatus.Invalidated);
                foreach (var operation in valid) operation.MarkFailing();
                if (result && ReferenceEquals(result.Session, resultSession))
                {
                    result.DiscardOnExit = true;
                    try { result.Close(); } catch (Exception cleanup) { StartUpModuleRequest.AttachCleanupError(error, cleanup); }
                }
                foreach (var operation in valid) operation.Finish(null, null, error, statuses[operation]);
                if (!origin.Async) throw;
                return;
            }
            // 参数和事务在完成通知前收尾；每个通知前仍复核同一个原 Session，不把复用引用当成功。
            foreach (var operation in valid)
            {
                try { operation.ReleaseInputs(); }
                catch (Exception error) { operation.Finish(null, null, error, LoadViewStatus.Failed); }
            }
            foreach (var operation in valid) operation.Finish(result, resultSession, null, LoadViewStatus.Succeeded);
            }
            finally
            {
                // 非法消费者的观察通知晚于本次仲裁，不能在预校验途中重入并改变 winner/原 Session。
                foreach (var failure in rejected) failure.Operation.Finish(null, null, failure.Error, LoadViewStatus.Failed);
            }
        }
    }
}
