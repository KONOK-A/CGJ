using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core
{
    public enum LoadViewStatus { Pending, Succeeded, Failed, Canceled, Invalidated }

    internal interface IPendingViewRequest
    {
        void Invalidate();
        void PublishInvalidation();
    }

    /// <summary>主线程表现请求；Abort仅取消Core业务结果，不停止或卸载共享后端加载。</summary>
    public sealed class LoadViewRequest<T> : YLFramework.Foundation.CustomAsyncOperation<LoadViewRequest<T>>, IPendingViewRequest where T : View
    {
        public T view { get; private set; }
        public LoadViewStatus Status { get; private set; }
        /// <summary>原始失败原因；附加收尾错误保存在Data["YLFramework.Core.CleanupException"]。</summary>
        public Exception Exception { get; private set; }

        private Module owner;
        private object lifecycle;
        private string assetName;
        private List<string> projects;
        private ICoreResourceProvider provider;
        private CoreResourceRequest<CorePrefabLease> resourceRequest;
        private CorePrefabLease prefabLease;
        private Transform parent;
        private bool hasParent;
        private Scene targetScene;
        private readonly List<KeyValuePair<View, object>> parentSessions = new List<KeyValuePair<View, object>>();
        private LoadViewArgs args;
        private object[] param;
        private GameObject directPrefab;
        private bool hasDirectPrefab;
        private bool executing;
        private bool closingOwner;
        private bool terminal;
        private Exception interruption;
        private LoadViewStatus interruptedStatus;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;

        internal LoadViewRequest(Module module, string name, Transform target, LoadViewArgs claimedArgs, object[] parameters, GameObject prefab = null)
        {
            owner = module;
            lifecycle = module.ViewLifecycle;
            assetName = name;
            projects = ModuleManager.ViewResourceProjects(module);
            parent = target;
            hasParent = !ReferenceEquals(target, null);
            targetScene = hasParent ? target.gameObject.scene : SceneManager.GetActiveScene();
            for (var ancestor = target; ancestor; ancestor = ancestor.parent)
            {
                var view = ancestor.GetComponent<View>();
                if (view && view.Session != null) parentSessions.Add(new KeyValuePair<View, object>(view, view.Session));
            }
            args = claimedArgs;
            param = parameters;
            directPrefab = prefab;
            hasDirectPrefab = !ReferenceEquals(prefab, null);
            provider = hasDirectPrefab ? null : CoreResourceAccess.Provider;
            Validate();
            owner.pendingViews.Add(this);
            Application.quitting += OnExecutionEnded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        // Unity 停止原生 Coroutine 不保证执行迭代器的 finally；业务请求必须自行提交退出终态。
        private void OnExecutionEnded()
            => Finish(null, new InvalidOperationException("View execution session has ended."), LoadViewStatus.Invalidated);

#if UNITY_EDITOR
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) OnExecutionEnded();
        }
#endif

        private void Validate()
        {
            if (interruption != null) throw interruption;
            if (terminal) throw new InvalidOperationException("View request has already completed.");
            if (hasDirectPrefab && !directPrefab) throw new InvalidOperationException("View prefab has been destroyed.");
            ViewManager.EnsureOwner(owner);
            if (!ReferenceEquals(owner.ViewLifecycle, lifecycle)) throw new InvalidOperationException("View owner lifecycle has ended.");
            if (hasParent)
            {
                if (!parent) throw new InvalidOperationException("View parent has been destroyed.");
                foreach (var entry in parentSessions)
                    if (!entry.Key || !ReferenceEquals(entry.Key.Session, entry.Value))
                        throw new InvalidOperationException("View parent session has ended.");
                // 存活parent的合法Scene/DDOL迁移遵循C；无parent则固定接受时的Scene。
                targetScene = parent.gameObject.scene;
            }
            if (!targetScene.IsValid() || !targetScene.isLoaded) throw new InvalidOperationException("View target scene has unloaded.");
        }

        /// <summary>等待期立即结束；同步初始化中的重入取消在该事务回滚后通知。</summary>
        public override void Abort()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("View request Abort must run on its accepting thread.");
            if (terminal || interruption != null) return;
            interruption = new OperationCanceledException("View request canceled.");
            interruptedStatus = LoadViewStatus.Canceled;
            if (!executing) Finish(null, interruption, interruptedStatus);
        }

        void IPendingViewRequest.Invalidate()
        {
            if (terminal) return;
            closingOwner = true;
            interruption = new InvalidOperationException("View owner lifecycle has ended.");
            interruptedStatus = LoadViewStatus.Invalidated;
            try { ReleaseInputs(); }
            catch (Exception cleanupError)
            {
                StartUpModuleRequest.AttachCleanupError(interruption, cleanupError);
                throw;
            }
        }

        void IPendingViewRequest.PublishInvalidation()
        {
            closingOwner = false;
            Finish(null, interruption, interruptedStatus);
        }

        private void ReleaseInputs()
        {
            Application.quitting -= OnExecutionEnded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
            if (owner != null) owner.pendingViews.Remove(this);
            var previousArgs = args;
            var previousRequest = resourceRequest;
            var previousPrefab = prefabLease;
            resourceRequest = null;
            prefabLease = null;
            provider = null;
            args = null;
            owner = null;
            lifecycle = null;
            parent = null;
            parentSessions.Clear();
            projects = null;
            assetName = null;
            param = null;
            directPrefab = null;
            targetScene = default;
            try { previousRequest?.Dispose(); }
            finally
            {
                try { previousPrefab?.Dispose(); }
                finally { if (previousArgs != null) previousArgs.Release(); }
            }
        }

        private void Finish(T result, Exception failure, LoadViewStatus status)
        {
            if (terminal || closingOwner) return;
            terminal = true;
            try { ReleaseInputs(); }
            catch (Exception cleanupError)
            {
                if (failure != null) StartUpModuleRequest.AttachCleanupError(failure, cleanupError);
                else { failure = cleanupError; status = LoadViewStatus.Failed; }
            }
            view = result;
            Exception = failure;
            Status = status;
            try { Completed(failure?.ToString()); }
            catch (Exception notificationError) { Debug.LogException(notificationError); }
        }

        internal IEnumerator Run()
        {
            var routine = Load();
            Exception failure = null;
            try
            {
                while (!terminal && !closingOwner)
                {
                    bool next;
                    executing = true;
                    try { next = routine.MoveNext(); }
                    catch (Exception error) { failure = error; break; }
                    finally { executing = false; }
                    if (!next) break;
                    yield return routine.Current;
                }
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
            }
            if (!terminal && !closingOwner)
                Finish(null, failure ?? interruption ?? new InvalidOperationException("View producer stopped without a result."),
                    interruption != null && (failure == null || ReferenceEquals(failure, interruption))
                        ? interruptedStatus : LoadViewStatus.Failed);
        }

        private IEnumerator Load()
        {
            GameObject prefab = directPrefab;
            Exception resourceError = null;
            // 即时完成的后端与直接 Prefab 同样保留接受/取消边界。
            yield return null;
            ValidateWaiting();
            if (!hasDirectPrefab) CoreResourceAccess.RequireProvider(provider);
            for (int i = 0; !prefab && i < projects.Count; i++)
            {
                var project = projects[i];
                Validate();
                CoreResourceRequest<CorePrefabLease> acquired;
                try { acquired = provider.AcquirePrefabAsync(project, assetName); }
                catch (Exception error) when (CoreResourceAccess.CanFallback(error)) { resourceError = error; continue; }
                // Provider/Source 可同步重入关闭 owner，返回值仍须由本调用接管或归还。
                try { ValidateWaiting(); }
                catch (Exception error)
                {
                    try { acquired?.Dispose(); }
                    catch (Exception cleanup) { StartUpModuleRequest.AttachCleanupError(error, cleanup); }
                    throw;
                }
                resourceRequest = acquired;
                var request = resourceRequest ?? throw new InvalidOperationException("View asset request is null.");
                while (request.Status == CoreResourceStatus.Pending)
                {
                    ValidateWaiting();
                    progress = request.Progress;
                    yield return null;
                }
                ValidateWaiting();
                if (request.Status != CoreResourceStatus.Succeeded)
                {
                    resourceError = request.Error ?? new CoreResourceException(CoreResourceError.LoadFailed, $"View asset load failed: {project}/{assetName}.");
                    request.Dispose();
                    resourceRequest = null;
                    if (request.Status == CoreResourceStatus.Canceled || request.Status == CoreResourceStatus.Invalidated ||
                        !CoreResourceAccess.CanFallback(resourceError)) throw resourceError;
                    continue;
                }
                prefabLease = request.TakeResult();
                request.Dispose();
                resourceRequest = null;
                prefab = prefabLease.Prefab;
                if (!prefab) throw new InvalidOperationException($"View prefab is no longer valid: {project}/{assetName}.");
            }
            if (!prefab) throw new InvalidOperationException($"View prefab not found: {assetName}.", resourceError);
            ValidateWaiting();
            var result = ViewManager.CompleteAsync<T>(owner, prefab, assetName, parent, targetScene, args, param, ValidateWaiting, prefabLease);
            // 初始化已结束再提交；完成通知可以关闭或开启下一笔会话。
            executing = false;
            Finish(result, null, LoadViewStatus.Succeeded);
        }

        private void ValidateWaiting()
        {
            try { Validate(); }
            catch (Exception error)
            {
                if (interruption == null) { interruption = error; interruptedStatus = LoadViewStatus.Invalidated; }
                throw;
            }
        }

        internal void FailToStart(Exception error) => Finish(null, error, LoadViewStatus.Failed);
    }
}
