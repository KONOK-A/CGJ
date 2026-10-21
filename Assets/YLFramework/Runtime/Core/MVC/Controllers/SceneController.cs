using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.SceneManagement;

namespace YLFramework.Core
{
    /// <summary>通过显式 Core provider 加载场景；只管理本 Controller 的在途请求，不拥有成功场景的卸载权。</summary>
    /// <remarks>主线程使用。完成后调用方负责消费/释放原请求；退出会取消并释放仍在途的请求，不能回滚原生 Single。</remarks>
    public sealed class SceneController : Controller
    {
        private sealed class PendingLoad
        {
            internal readonly (string Project, string Address) Key;
            internal readonly string Context;
            internal CoreResourceRequest<CoreSceneResult> Request;
            internal Action<CoreResourceRequest<CoreSceneResult>> Tracker;
            internal bool Subscribed;

            internal PendingLoad(string project, string address, LoadSceneMode mode, Module owner)
            {
                Key = (project, address);
                Context = $"Module={owner.ModuleName}, project={project}, address={address}, mode={mode}";
            }
        }

        private readonly Dictionary<(string Project, string Address), PendingLoad> pending = new();
        private object registration;
        private int threadId;

        public override void OnInit()
        {
            CheckThread();
            if (Module == null || registration != null)
                throw new InvalidOperationException("SceneController requires a fresh Module registration.");
            threadId = Thread.CurrentThread.ManagedThreadId;
            registration = new object();
        }

        /// <summary>使用当前 Module.ProjectName；address 必须是后端目录中登记的地址或显式别名。</summary>
        public CoreResourceRequest<CoreSceneResult> LoadSceneAsync(string address, LoadSceneMode mode = LoadSceneMode.Single)
        {
            var owner = RequireOwner();
            var stamp = registration;
            var project = owner.ProjectName;
            EnsureRegistration(stamp, owner);
            return LoadSceneAsync(project, address, mode);
        }

        /// <summary>提交新的加载意图；相同项目/地址在本实例仍在途时拒绝，不跨 owner 合并或按短名复用场景。</summary>
        public CoreResourceRequest<CoreSceneResult> LoadSceneAsync(string projectId, string address,
            LoadSceneMode mode = LoadSceneMode.Single)
        {
            var owner = RequireOwner();
            var stamp = registration;
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("A resource project is required.", nameof(projectId));
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("A scene address is required.", nameof(address));
            if (mode != LoadSceneMode.Single && mode != LoadSceneMode.Additive)
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported scene load mode.");

            var entry = new PendingLoad(projectId, address, mode, owner);
            var provider = CoreResourceAccess.Provider ?? throw new CoreResourceException(CoreResourceError.Unavailable,
                "Scene loading requires an explicitly bound resource provider. " + entry.Context);
            if (pending.TryGetValue(entry.Key, out var previous) && previous.Request != null &&
                (previous.Request.IsDisposed || previous.Request.Status != CoreResourceStatus.Pending))
                Detach(previous);
            EnsureRegistration(stamp, owner);
            if (pending.ContainsKey(entry.Key))
                throw new CoreResourceException(CoreResourceError.InProgress, "A scene load is already pending. " + entry.Context);

            // 后端和 Completed 订阅都可能同步重入：先占位，后附请求，再订阅。
            pending.Add(entry.Key, entry);
            try
            {
                entry.Request = provider.LoadSceneAsync(projectId, address, mode) ??
                    throw new InvalidOperationException("The scene provider returned null. " + entry.Context);
                EnsureRegistration(stamp, owner);
                if (!pending.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry))
                    throw new InvalidOperationException("The scene load reservation was canceled. " + entry.Context);
                entry.Tracker = _ => Detach(entry);
                entry.Subscribed = true;
                entry.Request.Completed += entry.Tracker;
                EnsureRegistration(stamp, owner);
                if (entry.Request.IsDisposed)
                    throw new InvalidOperationException("The scene request was disposed before delivery. " + entry.Context);
                return entry.Request;
            }
            catch (Exception error)
            {
                error.Data["YLFramework.SceneController.Context"] = entry.Context;
                var cleanup = new List<Exception>();
                Release(entry, false, cleanup);
                if (cleanup.Count == 0) throw;
                cleanup.Insert(0, error);
                throw new AggregateException("Scene load and cleanup failed. " + entry.Context, cleanup);
            }
        }

        /// <summary>结束调用时的在途快照；取消回调中新提交的请求不属于旧快照。逐项收尾，失败汇总报告。</summary>
        public void CancelPendingLoads()
        {
            CheckThread();
            if (pending.Count == 0) return;
            var snapshot = new List<PendingLoad>(pending.Values);
            pending.Clear();
            var errors = new List<Exception>();
            foreach (var entry in snapshot) Release(entry, true, errors);
            if (errors.Count != 0) throw new AggregateException("SceneController request cleanup failed.", errors);
        }

        public override void OnExit()
        {
            CheckThread();
            registration = null;
            CancelPendingLoads();
        }

        private void CheckThread()
        {
            if (threadId != 0 && threadId != Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("SceneController must run on its Module registration thread.");
        }

        private Module RequireOwner()
        {
            CheckThread();
            var owner = Module;
            if (registration == null || owner == null || IsExiting)
                throw new InvalidOperationException("SceneController is not in an active Module registration.");
            owner.EnsureCanAddObjects();
            return owner;
        }

        private void EnsureRegistration(object stamp, Module owner)
        {
            if (!ReferenceEquals(registration, stamp) || !ReferenceEquals(Module, owner) || IsExiting)
                throw new InvalidOperationException("The scene Controller registration changed during loading. Module=" + owner.ModuleName);
            owner.EnsureCanAddObjects();
        }

        private void Detach(PendingLoad entry)
        {
            if (pending.TryGetValue(entry.Key, out var current) && ReferenceEquals(current, entry))
                pending.Remove(entry.Key);
            if (!entry.Subscribed) return;
            entry.Subscribed = false;
            entry.Request.Completed -= entry.Tracker;
        }

        private void Release(PendingLoad entry, bool pendingOnly, List<Exception> errors)
        {
            try { Detach(entry); }
            catch (Exception error) { error.Data["YLFramework.SceneController.Context"] = entry.Context; errors.Add(error); }
            var request = entry.Request;
            if (request == null || request.IsDisposed || (pendingOnly && request.Status != CoreResourceStatus.Pending)) return;
            try { request.Cancel(); }
            catch (Exception error) { error.Data["YLFramework.SceneController.Context"] = entry.Context; errors.Add(error); }
            try { request.Dispose(); }
            catch (Exception error) { error.Data["YLFramework.SceneController.Context"] = entry.Context; errors.Add(error); }
        }
    }
}
