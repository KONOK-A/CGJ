// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/AsyncOperation/LoadViewRequest.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private Transform parent;
        private bool hasParent;
        private Scene targetScene;
        private readonly List<KeyValuePair<View, object>> parentSessions = new List<KeyValuePair<View, object>>();
        private LoadViewArgs args;
        private object[] param;
        private UIType? panelType;
        private bool executing;
        private bool closingOwner;
        private bool terminal;
        private Exception interruption;
        private LoadViewStatus interruptedStatus;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;

        internal LoadViewRequest(Module module, string name, Transform target, LoadViewArgs claimedArgs, object[] parameters, UIType? uiType = null)
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
            panelType = uiType;
            Validate();
            owner.pendingViews.Add(this);
        }

        private void Validate()
        {
            if (interruption != null) throw interruption;
            if (terminal) throw new InvalidOperationException("View request has already completed.");
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
            if (owner != null) owner.pendingViews.Remove(this);
            var previousArgs = args;
            args = null;
            owner = null;
            lifecycle = null;
            parent = null;
            parentSessions.Clear();
            projects = null;
            assetName = null;
            param = null;
            panelType = null;
            targetScene = default;
            if (previousArgs != null) previousArgs.Release();
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
            GameObject prefab = null;
            Exception resourceError = null;
            foreach (var project in projects)
            {
                Validate();
                XFABManager.LoadAssetRequest request = null;
                try { request = XFABManager.AssetBundleManager.LoadAssetAsync<GameObject>(project, assetName); }
                catch (Exception error) { resourceError = error; }
                if (request == null) { resourceError = resourceError ?? new InvalidOperationException("View asset request is null."); continue; }
                while (!request.isDone)
                {
                    ValidateWaiting();
                    progress = request.progress;
                    yield return null;
                }
                ValidateWaiting();
                if (!string.IsNullOrEmpty(request.error))
                {
                    resourceError = new InvalidOperationException($"View asset load failed: {project}/{assetName}: {request.error}");
                    continue;
                }
                prefab = request.asset as GameObject;
                if (prefab) break;
                if (request.asset) throw new InvalidCastException("View asset is not a GameObject prefab.");
                resourceError = new InvalidOperationException($"View prefab not found: {project}/{assetName}.");
            }
            if (!prefab) throw new InvalidOperationException($"View prefab not found: {assetName}.", resourceError);
            ValidateWaiting();
            var result = ViewManager.CompleteAsync<T>(owner, prefab, assetName, parent, targetScene, args, param, ValidateWaiting, panelType);
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
