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
    public enum LoadViewStatus
    {
        Pending,
        Succeeded,
        Failed,
        Canceled,
        Invalidated
    }

    internal interface IPendingViewRequest
    {
        void Invalidate();
        void PublishInvalidation();
    }

    /// <summary>主线程表现请求；Abort仅取消Core业务结果，不停止或卸载共享后端加载。</summary>
    public sealed class LoadViewRequest<T> : YLFramework.Foundation.CustomAsyncOperation<LoadViewRequest<T>>, IPendingViewRequest where T : View
    {
        public T View
        {
            get; private set;
        }
        public LoadViewStatus Status
        {
            get; private set;
        }
        /// <summary>原始失败原因；附加收尾错误保存在Data["YLFramework.Core.CleanupException"]。</summary>
        public Exception Exception
        {
            get; private set;
        }

        private Module _owner;
        private object _lifecycle;
        private string _assetName;
        private List<string> _projects;
        private ICoreResourceProvider _provider;
        private CoreResourceRequest<CorePrefabLease> _resourceRequest;
        private CorePrefabLease _prefabLease;
        private Transform _parent;
        private bool _hasParent;
        private Scene _targetScene;
        private readonly List<KeyValuePair<View, object>> _parentSessions = new List<KeyValuePair<View, object>>();
        private LoadViewArgs _args;
        private object[] _param;
        private GameObject _directPrefab;
        private bool _hasDirectPrefab;
        private bool _isExecuting;
        private bool _isClosingOwner;
        private bool _isTerminal;
        private Exception _interruption;
        private LoadViewStatus _interruptedStatus;
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;

        internal LoadViewRequest(Module module, string name, Transform target, LoadViewArgs claimedArgs, object[] parameters, GameObject prefab = null)
        {
            _owner = module;
            _lifecycle = module.ViewLifecycle;
            _assetName = name;
            _projects = ModuleManager.ViewResourceProjects(module);
            _parent = target;
            _hasParent = !ReferenceEquals(target, null);
            _targetScene = _hasParent ? target.gameObject.scene : SceneManager.GetActiveScene();
            for (var ancestor = target; ancestor; ancestor = ancestor.parent)
            {
                var view = ancestor.GetComponent<View>();
                if (view && view.Session != null)
                {
                    _parentSessions.Add(new KeyValuePair<View, object>(view, view.Session));
                }
            }
            _args = claimedArgs;
            _param = parameters;
            _directPrefab = prefab;
            _hasDirectPrefab = !ReferenceEquals(prefab, null);
            _provider = _hasDirectPrefab ? null : CoreResourceAccess.Provider;
            Validate();
            _owner.PendingViews.Add(this);
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
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                OnExecutionEnded();
            }
        }
#endif

        private void Validate()
        {
            if (_interruption != null)
            {
                throw _interruption;
            }

            if (_isTerminal)
            {
                throw new InvalidOperationException("View request has already completed.");
            }

            if (_hasDirectPrefab && !_directPrefab)
            {
                throw new InvalidOperationException("View prefab has been destroyed.");
            }

            ViewManager.EnsureOwner(_owner);
            if (!ReferenceEquals(_owner.ViewLifecycle, _lifecycle))
            {
                throw new InvalidOperationException("View owner lifecycle has ended.");
            }

            if (_hasParent)
            {
                if (!_parent)
                {
                    throw new InvalidOperationException("View parent has been destroyed.");
                }

                foreach (var entry in _parentSessions)
                {
                    if (!entry.Key || !ReferenceEquals(entry.Key.Session, entry.Value))
                    {
                        throw new InvalidOperationException("View parent session has ended.");
                    }
                }
                // 存活parent的合法Scene/DDOL迁移遵循C；无parent则固定接受时的Scene。
                _targetScene = _parent.gameObject.scene;
            }
            if (!_targetScene.IsValid() || !_targetScene.isLoaded)
            {
                throw new InvalidOperationException("View target scene has unloaded.");
            }
        }

        /// <summary>等待期立即结束；同步初始化中的重入取消在该事务回滚后通知。</summary>
        public override void Abort()
        {
            if (Thread.CurrentThread.ManagedThreadId != _threadId)
            {
                throw new InvalidOperationException("View request Abort must run on its accepting thread.");
            }

            if (_isTerminal || _interruption != null)
            {
                return;
            }

            _interruption = new OperationCanceledException("View request canceled.");
            _interruptedStatus = LoadViewStatus.Canceled;
            if (!_isExecuting)
            {
                Finish(null, _interruption, _interruptedStatus);
            }
        }

        void IPendingViewRequest.Invalidate()
        {
            if (_isTerminal)
            {
                return;
            }

            _isClosingOwner = true;
            _interruption = new InvalidOperationException("View owner lifecycle has ended.");
            _interruptedStatus = LoadViewStatus.Invalidated;
            try
            {
                ReleaseInputs();
            }
            catch (Exception cleanupError)
            {
                StartUpModuleRequest.AttachCleanupError(_interruption, cleanupError);
                throw;
            }
        }

        void IPendingViewRequest.PublishInvalidation()
        {
            _isClosingOwner = false;
            Finish(null, _interruption, _interruptedStatus);
        }

        private void ReleaseInputs()
        {
            Application.quitting -= OnExecutionEnded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
            if (_owner != null)
            {
                _owner.PendingViews.Remove(this);
            }

            var previousArgs = _args;
            var previousRequest = _resourceRequest;
            var previousPrefab = _prefabLease;
            _resourceRequest = null;
            _prefabLease = null;
            _provider = null;
            _args = null;
            _owner = null;
            _lifecycle = null;
            _parent = null;
            _parentSessions.Clear();
            _projects = null;
            _assetName = null;
            _param = null;
            _directPrefab = null;
            _targetScene = default;
            try
            {
                previousRequest?.Dispose();
            }
            finally
            {
                try
                {
                    previousPrefab?.Dispose();
                }
                finally
                {
                    if (previousArgs != null)
                    {
                        previousArgs.Release();
                    }
                }
            }
        }

        private void Finish(T result, Exception failure, LoadViewStatus status)
        {
            if (_isTerminal || _isClosingOwner)
            {
                return;
            }

            _isTerminal = true;
            try
            {
                ReleaseInputs();
            }
            catch (Exception cleanupError)
            {
                if (failure != null)
                {
                    StartUpModuleRequest.AttachCleanupError(failure, cleanupError);
                }
                else
                {
                    failure = cleanupError;
                    status = LoadViewStatus.Failed;
                }
            }
            View = result;
            Exception = failure;
            Status = status;
            try
            {
                Complete(failure?.ToString());
            }
            catch (Exception notificationError)
            {
                Debug.LogException(notificationError);
            }
        }

        internal IEnumerator Run()
        {
            var routine = Load();
            Exception failure = null;
            try
            {
                while (!_isTerminal && !_isClosingOwner)
                {
                    bool next;
                    _isExecuting = true;
                    try
                    {
                        next = routine.MoveNext();
                    }
                    catch (Exception error)
                    {
                        failure = error;
                        break;
                    }
                    finally
                    {
                        _isExecuting = false;
                    }
                    if (!next)
                    {
                        break;
                    }

                    yield return routine.Current;
                }
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
            }
            if (!_isTerminal && !_isClosingOwner)
            {
                Finish(null, failure ?? _interruption ?? new InvalidOperationException("View producer stopped without a result."),
                    _interruption != null && (failure == null || ReferenceEquals(failure, _interruption))
                        ? _interruptedStatus : LoadViewStatus.Failed);
            }
        }

        private IEnumerator Load()
        {
            GameObject prefab = _directPrefab;
            Exception resourceError = null;
            // 即时完成的后端与直接 Prefab 同样保留接受/取消边界。
            yield return null;
            ValidateWaiting();
            if (!_hasDirectPrefab)
            {
                CoreResourceAccess.RequireProvider(_provider);
            }

            for (int i = 0; !prefab && i < _projects.Count; i++)
            {
                var project = _projects[i];
                Validate();
                CoreResourceRequest<CorePrefabLease> acquired;
                try
                {
                    acquired = _provider.AcquirePrefabAsync(project, _assetName);
                }
                catch (Exception error) when (CoreResourceAccess.CanFallback(error))
                {
                    resourceError = error;
                    continue;
                }
                // Provider/Source 可同步重入关闭 owner，返回值仍须由本调用接管或归还。
                try
                {
                    ValidateWaiting();
                }
                catch (Exception error)
                {
                    try
                    {
                        acquired?.Dispose();
                    }
                    catch (Exception cleanup)
                    {
                        StartUpModuleRequest.AttachCleanupError(error, cleanup);
                    }
                    throw;
                }
                _resourceRequest = acquired;
                var request = _resourceRequest ?? throw new InvalidOperationException("View asset request is null.");
                while (request.Status == CoreResourceStatus.Pending)
                {
                    ValidateWaiting();
                    Progress = request.Progress;
                    yield return null;
                }
                ValidateWaiting();
                if (request.Status != CoreResourceStatus.Succeeded)
                {
                    resourceError = request.Error ?? new CoreResourceException(CoreResourceError.LoadFailed, $"View asset load failed: {project}/{_assetName}.");
                    request.Dispose();
                    _resourceRequest = null;
                    if (request.Status == CoreResourceStatus.Canceled || request.Status == CoreResourceStatus.Invalidated ||
                        !CoreResourceAccess.CanFallback(resourceError))
                    {
                        throw resourceError;
                    }

                    continue;
                }
                _prefabLease = request.TakeResult();
                request.Dispose();
                _resourceRequest = null;
                prefab = _prefabLease.Prefab;
                if (!prefab)
                {
                    throw new InvalidOperationException($"View prefab is no longer valid: {project}/{_assetName}.");
                }
            }
            if (!prefab)
            {
                throw new InvalidOperationException($"View prefab not found: {_assetName}.", resourceError);
            }

            ValidateWaiting();
            var result = ViewManager.CompleteAsync<T>(_owner, prefab, _assetName, _parent, _targetScene, _args, _param, ValidateWaiting, _prefabLease);
            // 初始化已结束再提交；完成通知可以关闭或开启下一笔会话。
            _isExecuting = false;
            Finish(result, null, LoadViewStatus.Succeeded);
        }

        private void ValidateWaiting()
        {
            try
            {
                Validate();
            }
            catch (Exception error)
            {
                if (_interruption == null)
                {
                    _interruption = error;
                    _interruptedStatus = LoadViewStatus.Invalidated;
                }
                throw;
            }
        }

        internal void FailToStart(Exception error) => Finish(null, error, LoadViewStatus.Failed);
    }
}
