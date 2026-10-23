using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private readonly HashSet<string> _activationProjects = new(StringComparer.Ordinal);
        private bool _isUpdating;

        internal bool IsProjectBlocked(string project) => project != null && _activationProjects.Contains(project);

        private IEnumerable<ResourceLocalSnapshot> StoreSnapshots() => _configuration.ArtifactStore.Projects.Select(x => _catalogs[x.ProjectId].Snapshot);
        private IEnumerable<(ResourceLocalSnapshot snapshot, ArtifactProtection reason)> HeldStoreVersions()
        {
            foreach (var mount in _configuration.ArtifactStore.Projects)
            {
                if (_catalogs.TryGetValue(mount.ProjectId, out var catalog) && catalog.Snapshot != null)
                {
                    yield return (catalog.Snapshot, ArtifactProtection.Mounted);
                }
            }

            foreach (var bundle in _bundleRecords.Values)
            {
                yield return (bundle.Snapshot, ArtifactProtection.Held |
                    (bundle.Native != null || bundle.State is BundleState.Waiting or BundleState.Queued or BundleState.Loading ? ArtifactProtection.InFlight : ArtifactProtection.None));
            }

            foreach (var work in _contentWork)
            {
                foreach (var snapshot in work.Snapshots)
                {
                    yield return (snapshot, ArtifactProtection.Held | ArtifactProtection.InFlight);
                }
            }

            foreach (var cleanup in _retainedContentCleanup)
            {
                if (cleanup.Owner is ArtifactStore.ValidationReceipt receipt)
                {
                    foreach (var snapshot in receipt.Snapshots)
                    {
                        yield return (snapshot, ArtifactProtection.Held | ArtifactProtection.InFlight);
                    }
                }
            }
        }

        /// <summary>本地只读检查。targetClosure 必须明确列出 store 的全部项目及目标摘要，未变化项目也不可省略。</summary>
        public ResourceRequest<UpdatePlan> CheckUpdatesAsync(IEnumerable<ResourceLocalSnapshot> targetClosure)
        {
            CheckThread();
            if (!CanUpdate(out var error))
            {
                return Fail(new ResourceRequest<UpdatePlan>(), error.Kind, error.Message);
            }

            var request = new ResourceRequest<UpdatePlan>();
            var store = _configuration.ArtifactStore;
            ContentWork work = null;
            _isUpdating = true;
            Track(request);
            request.Attach(() => work?.Cancel(), null);

            void Complete(UpdatePlan plan, Exception failure)
            {
                RetainContentFailureOwners(failure);
                try
                {
                    if (!request.IsDone)
                    {
                        if (!IsSessionActive)
                        {
                            request.SetFailure(ResourceRequestStatus.Invalidated, new ResourceException(ResourceError.SessionUnavailable, "The check session ended."));
                        }
                        else if (failure is OperationCanceledException || failure is ResourceException { Kind: ResourceError.Canceled })
                        {
                            request.SetFailure(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "The read-only update check was canceled.", failure));
                        }
                        else if (failure != null)
                        {
                            request.SetFailure(ResourceRequestStatus.Failed, AsUpdateError(failure));
                        }
                        else
                        {
                            RequireCheckedFrom(plan);
                            request.SetSuccess(plan);
                        }
                    }
                }
                catch (Exception cause)
                {
                    request.SetFailure(ResourceRequestStatus.Failed, AsUpdateError(cause));
                }
                finally
                {
                    _isUpdating = false;
                }
                request.Notify();
                CheckDrain();
            }

            try
            {
                var current = StoreSnapshots().ToArray();
                // 调用方枚举器也只在主线程展开，worker只收到固定快照输入。
                var targets = (targetClosure ?? throw new ArgumentNullException(nameof(targetClosure))).ToArray();
                if (!IsSessionActive || request.IsDone)
                {
                    throw new OperationCanceledException("The check no longer has an active consumer.");
                }

                work = StartContentWork(token => store.CheckForContent(current, targets, token), Complete,
                    snapshots: current.Concat(targets));
            }
            catch (Exception failure)
            {
                Complete(null, failure);
            }
            return request;
        }

        private void RequireCheckedFrom(UpdatePlan plan)
        {
            if (!IsSessionActive)
            {
                throw new ResourceException(ResourceError.SessionUnavailable, "The update check session ended before handoff.");
            }

            _configuration.ArtifactStore.RequireCheckedPlan(plan);
            if (plan.Projects.Any(x =>
                !_catalogs.TryGetValue(x.ProjectId, out var current) || current.CatalogDigest != x.FromDigest))
            {
                throw new ResourceException(ResourceError.StaleBase, "The mounted source closure changed during the update check.");
            }
        }

        /// <summary>复制到自有 staging，完整验证后安装。取消和失败都不写 active；不提供 HTTP/签名降级入口。</summary>
        public ResourceRequest<PreparedUpdate> PrepareUpdateAsync(UpdatePlan plan, CancellationToken cancellation = default)
        {
            CheckThread();
            if (plan?.IsRemote == true)
            {
                return PrepareRemoteUpdate(plan, cancellation);
            }

            if (!CanUpdate(out var error))
            {
                return Fail(new ResourceRequest<PreparedUpdate>(), error.Kind, error.Message);
            }

            var request = new ResourceRequest<PreparedUpdate>();
            var store = _configuration.ArtifactStore;
            return RunUpdateContent(request, () => store.CapturePreparation(plan, _configuration.Decryption, _contentBudget),
                receipt => store.CompletePreparation(plan, receipt, cancellation), cancellation,
                token => store.InstallPreparationForContent(plan, token),
                plan?.Projects.SelectMany(x => new[] { x.From, x.Target }));
        }

        public IReadOnlyList<PreparedUpdate> GetPreparedUpdates()
        {
            CheckThread();
            if (!IsAvailable || _configuration.ArtifactStore == null)
            {
                throw new ResourceException(ResourceError.Unsupported, "No available ArtifactStore is configured.");
            }

            return _configuration.ArtifactStore.GetPreparedUpdates();
        }

        /// <summary>显式请求 last-known-good，走与普通激活相同的 native 安全边界；不是旧 PreparedUpdate 的降级通道。</summary>
        public ResourceRequest<bool> RollbackAsync()
        {
            CheckThread();
            if (!CanUpdate(out var error))
            {
                return Fail(new ResourceRequest<bool>(), error.Kind, error.Message);
            }

            PreparedUpdate rollback;
            try
            {
                rollback = _configuration.ArtifactStore.Rollback(StoreSnapshots());
            }
            catch (Exception cause)
            {
                return UpdateFailure<bool>(cause);
            }
            return ActivatePreparedUpdateAsync(rollback);
        }

        /// <summary>
        /// 默认不热共存同项目 native Bundle。任何 live/pending 责任返回 Busy；idle 退休后延迟 Destroy 尚未完成也须重试。
        /// 不取消别人的请求，不归还外部 lease，不修改应用场景。所有失败路径都恢复旧项目的取得入口。
        /// </summary>
        public ResourceRequest<bool> ActivatePreparedUpdateAsync(PreparedUpdate update)
        {
            CheckThread();
            if (!CanUpdate(out var error))
            {
                return Fail(new ResourceRequest<bool>(), error.Kind, error.Message);
            }

            var request = new ResourceRequest<bool>();
            var store = _configuration.ArtifactStore;
            if (update == null || update.Root != store.RootPath || update.Projects.Count != store.Projects.Count ||
                update.Projects.Select(x => x.ProjectId).Distinct(StringComparer.Ordinal).Count() != store.Projects.Count ||
                update.Projects.Any(x => !_catalogs.TryGetValue(x.ProjectId, out var catalog) || catalog.Snapshot == null))
            {
                return Fail(request, ResourceError.InvalidConfiguration, "PreparedUpdate does not match this store's explicit closure.");
            }

            if (update.Projects.All(x => _catalogs[x.ProjectId].CatalogDigest == x.ToDigest) && store.HasConfirmedReleases(update))
            {
                request.SetSuccess(true);
                return request;
            }
            if (update.Projects.Any(x => _catalogs[x.ProjectId].CatalogDigest != x.FromDigest))
            {
                return Fail(request, ResourceError.StaleBase, "PreparedUpdate's source version changed; Check and Prepare again, or explicitly Rollback.");
            }

            return RunUpdateContent(request, () => store.CaptureCommit(update, _configuration.Decryption, _contentBudget),
                receipt => CommitContentUpdate(update, receipt));
        }

        private bool CommitContentUpdate(PreparedUpdate update, ArtifactStore.ValidationReceipt receipt)
        {
            var store = _configuration.ArtifactStore;
            if (update.Projects.Any(x => _catalogs[x.ProjectId].CatalogDigest != x.FromDigest))
            {
                throw new ResourceException(ResourceError.StaleBase, "The mounted activation base changed during content validation.");
            }

            receipt.Require(store, update);
            var replacements = receipt.Targets.ToDictionary(x => x.Document.ProjectId, CreateStoreCatalog, StringComparer.Ordinal);
            // 全部To认证完成后才进入native屏障；预验失败不退休仍有效的旧输入。
            foreach (var project in update.Projects)
            {
                _activationProjects.Add(project.ProjectId);
            }

            foreach (var cache in _instanceCaches.ToArray())
            {
                if (IsProjectBlocked(cache.AssetRecord.Entry.Address.ProjectId))
                {
                    cache.Dispose();
                }
            }

            TickInstanceCleanup();
            var busy = _records.Values.Any(x => IsProjectBlocked(x.Entry.Address.ProjectId)) ||
                _bundleRecords.Values.Any(x => IsProjectBlocked(x.Snapshot.Document.ProjectId)) ||
                _instanceCaches.Any(x => IsProjectBlocked(x.AssetRecord.Entry.Address.ProjectId)) ||
                _sceneLoads.Any(x => IsProjectBlocked(x.Address.ProjectId)) || _sceneRecords.Any(x => IsProjectBlocked(x.Address.ProjectId));
            if (busy)
            {
                throw new ResourceException(ResourceError.Busy, "Activation is blocked by live leases, instances, Scenes or native operations. The old version remains available.");
            }
            // v1 的项目 native namespace 也覆盖已移除的旧 Bundle；不接管其他代码已加载的同项目原生资源。
            var nativePrefixes = update.Projects.Select(x => "ylr_" + ResourceCatalogCodec.Digest(System.Text.Encoding.UTF8.GetBytes(x.ProjectId)).Substring(0, 32) + "_").ToArray();
            if (AssetBundle.GetAllLoadedAssetBundles().Any(bundle => bundle && nativePrefixes.Any(prefix => bundle.name.StartsWith(prefix, StringComparison.Ordinal))))
            {
                throw new ResourceException(ResourceError.Busy, "The project's native Bundle namespace is still loaded outside the managed graph.");
            }
            // 目标内容、类型和平台已通过；持久提交后只做无回调的字典替换。
            if (!IsSessionActive)
            {
                throw new ResourceException(ResourceError.SessionUnavailable, "Activation session ended before commit.");
            }

            Exception commitFailure = null;
            try
            {
                var selected = store.CommitValidated(update, receipt);
                foreach (var snapshot in selected)
                {
                    if (replacements[snapshot.Document.ProjectId].CatalogDigest != snapshot.Document.CatalogDigest)
                    {
                        replacements[snapshot.Document.ProjectId] = CreateStoreCatalog(snapshot);
                    }
                }
            }
            catch (Exception commitError)
            {
                commitFailure = commitError;
                try
                {
                    var recovered = store.Mount();
                    replacements = recovered.ToDictionary(x => x.Document.ProjectId, CreateStoreCatalog, StringComparer.Ordinal);
                }
                catch (Exception recoveryError)
                {
                    throw new ResourceException(ResourceError.UpdateFailed, "Commit recovery is pending; the old in-memory snapshot remains usable. Reinitialize after resolving the IO failure.",
                        new AggregateException(commitError, recoveryError));
                }
            }
            if (!IsSessionActive)
            {
                throw new ResourceException(ResourceError.SessionUnavailable, "The activation session ended; startup will mount the committed closure.");
            }

            foreach (var pair in replacements)
            {
                _catalogs[pair.Key] = pair.Value;
            }
            // 恢复到 From/LKG 也必须同步内存，但不能把它报告为目标激活成功。
            if (replacements.Any(x => x.Value.CatalogDigest != update.Projects.Single(y => y.ProjectId == x.Key).ToDigest))
            {
                throw commitFailure is ResourceException { Kind: ResourceError.StaleBase } stale ? stale :
                    new ResourceException(ResourceError.UpdateFailed, "Activation did not reach its target; memory and disk recovered the same complete closure.", commitFailure);
            }

            return true;
        }

        private ResourceRequest<T> RunUpdateContent<T>(ResourceRequest<T> request, Func<ArtifactStore.ValidationReceipt> capture,
            Func<ArtifactStore.ValidationReceipt, T> complete, CancellationToken cancellation = default,
            Action<CancellationToken> install = null, IEnumerable<ResourceLocalSnapshot> installationSnapshots = null)
        {
            ArtifactStore.ValidationReceipt receipt = null;
            ContentWork work = null;
            var finished = false;
            _isUpdating = true;
            Track(request);
            request.Attach(() => work?.Cancel(), null);

            void Finish(Exception failure)
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                RetainContentFailureOwners(failure);
                T result = default;
                try
                {
                    if (!IsSessionActive)
                    {
                        throw new ResourceException(ResourceError.SessionUnavailable, "The update session ended before content handoff.");
                    }

                    if (!request.IsDone)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (failure != null)
                        {
                            throw failure;
                        }

                        result = complete(receipt);
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                }
                finally
                {
                    failure = ReleaseUpdateValidation(receipt, failure);
                    _activationProjects.Clear();
                    _isUpdating = false;
                }
                if (!request.IsDone)
                {
                    if (!IsSessionActive)
                    {
                        request.SetFailure(ResourceRequestStatus.Invalidated, new ResourceException(ResourceError.SessionUnavailable, "The update session ended."));
                    }
                    else if (failure is OperationCanceledException || failure is ResourceException { Kind: ResourceError.Canceled })
                    {
                        request.SetFailure(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "Content validation was canceled; no new transaction was committed.", failure));
                    }
                    else if (failure != null)
                    {
                        request.SetFailure(ResourceRequestStatus.Failed, AsUpdateError(failure));
                    }
                    else
                    {
                        request.SetSuccess(result);
                    }
                }
                request.Notify();
                CheckDrain();
            }

            void Validate()
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!IsSessionActive || request.IsDone)
                    {
                        throw new OperationCanceledException("The update no longer has an active consumer.");
                    }

                    receipt = capture();
                    if (!IsSessionActive || request.IsDone)
                    {
                        throw new OperationCanceledException("The update no longer has an active consumer.");
                    }

                    var captured = receipt;
                    work = StartContentWork(token =>
                    {
                        captured.Validate(token);
                        return true;
                    }, (_, failure) => Finish(failure),
                        cancellation, receipt.Snapshots);
                }
                catch (Exception failure)
                {
                    Finish(failure);
                }
            }

            if (install == null)
            {
                Validate();
            }
            else
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    work = StartContentWork(token =>
                    {
                        install(token);
                        return true;
                    }, (_, failure) =>
                    {
                        if (failure != null || !IsSessionActive || request.IsDone)
                        {
                            Finish(failure);
                        }
                        else
                        {
                            Validate();
                        }
                    }, cancellation, installationSnapshots);
                }
                catch (Exception failure)
                {
                    Finish(failure);
                }
            }

            return request;
        }

        private Exception ReleaseUpdateValidation(ArtifactStore.ValidationReceipt receipt, Exception failure)
        {
            RetainContentFailureOwners(failure);
            if (receipt != null)
            {
                try
                {
                    receipt.Dispose();
                }
                catch (Exception cleanup)
                {
                    RetainContentCleanup(receipt, cleanup);
                    RetainContentFailureOwners(cleanup);
                    return new ResourceException(ResourceError.UpdateFailed, "Update validation cleanup failed; unfinished inputs remain owned.",
                        failure == null ? cleanup : new AggregateException(failure, cleanup));
                }
            }

            return failure;
        }

        public ResourceRequest<VersionCleanupResult> CleanupVersionsAsync() => UpdateRequest(() => _configuration.ArtifactStore.Cleanup(HeldStoreVersions().Select(x => x.snapshot).Distinct()));

        public string GetActiveCatalogDigest(string projectId)
        {
            CheckThread();
            if (!_catalogs.TryGetValue(projectId, out var catalog))
            {
                throw new ArgumentException("Unknown resource project.", nameof(projectId));
            }

            return catalog.CatalogDigest;
        }

        private ResourceCatalog CreateStoreCatalog(ResourceLocalSnapshot snapshot)
        {
            ValidateSnapshotPlatform(snapshot);
            return snapshot.CreateCatalog(_configuration.ArtifactStore.Projects.Single(x => x.ProjectId == snapshot.Document.ProjectId).AssetTypes);
        }

        private bool CanUpdate(out ResourceException error)
        {
            error = !IsAvailable ? new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.") :
                _configuration.ArtifactStore == null ? new ResourceException(ResourceError.Unsupported, "No local ArtifactStore is configured.") :
                _isUpdating || _remoteJobs.Count != 0 ? new ResourceException(ResourceError.Busy, "A resource version operation is already in progress.") : null;
            return error == null;
        }

        private ResourceRequest<T> UpdateRequest<T>(Func<T> operation)
        {
            CheckThread();
            if (!CanUpdate(out var error))
            {
                return Fail(new ResourceRequest<T>(), error.Kind, error.Message);
            }

            var request = new ResourceRequest<T>();
            _isUpdating = true;
            try
            {
                var result = operation();
                if (!IsSessionActive)
                {
                    throw new ResourceException(ResourceError.SessionUnavailable, "The update session ended.");
                }

                request.SetSuccess(result);
            }
            catch (OperationCanceledException cause)
            {
                request.SetFailure(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "Local preparation was canceled; active is unchanged.", cause));
            }
            catch (Exception cause)
            {
                request.SetFailure(ResourceRequestStatus.Failed, AsUpdateError(cause));
            }
            finally
            {
                _isUpdating = false;
            }
            request.Notify();
            return request;
        }

        private static ResourceException AsUpdateError(Exception cause) => cause as ResourceException ?? new ResourceException(ResourceError.UpdateFailed, "Resource version operation failed.", cause);
        private static ResourceRequest<T> UpdateFailure<T>(Exception cause) => Fail(new ResourceRequest<T>(),
            cause is ResourceException resource ? resource.Kind : ResourceError.UpdateFailed, "Local resource version operation failed.", cause: cause);
    }
}
