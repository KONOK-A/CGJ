using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private readonly HashSet<string> activationProjects = new(StringComparer.Ordinal);
        private bool updating;

        internal bool IsProjectBlocked(string project) => project != null && activationProjects.Contains(project);

        private IEnumerable<ResourceLocalSnapshot> StoreSnapshots() => configuration.ArtifactStore.Projects.Select(x => catalogs[x.ProjectId].Snapshot);

        /// <summary>本地只读检查。targetClosure 必须明确列出 store 的全部项目及目标摘要，未变化项目也不可省略。</summary>
        public ResourceRequest<UpdatePlan> CheckUpdatesAsync(IEnumerable<ResourceLocalSnapshot> targetClosure)
            => UpdateRequest(() => configuration.ArtifactStore.Check(StoreSnapshots(), targetClosure));

        /// <summary>复制到自有 staging，完整验证后安装。取消和失败都不写 active；不提供 HTTP/签名降级入口。</summary>
        public ResourceRequest<PreparedUpdate> PrepareUpdateAsync(UpdatePlan plan, CancellationToken cancellation = default)
        {
            CheckThread();
            return plan?.IsRemote == true ? PrepareRemoteUpdate(plan, cancellation) : UpdateRequest(() => configuration.ArtifactStore.Prepare(plan, cancellation));
        }

        public IReadOnlyList<PreparedUpdate> GetPreparedUpdates()
        {
            CheckThread();
            if (!IsAvailable || configuration.ArtifactStore == null) throw new ResourceException(ResourceError.Unsupported, "No available ArtifactStore is configured.");
            return configuration.ArtifactStore.GetPreparedUpdates();
        }

        /// <summary>显式请求 last-known-good，走与普通激活相同的 native 安全边界；不是旧 PreparedUpdate 的降级通道。</summary>
        public ResourceRequest<bool> RollbackAsync()
        {
            CheckThread();
            if (!CanUpdate(out var error)) return Fail(new ResourceRequest<bool>(), error.Kind, error.Message);
            PreparedUpdate rollback;
            try { rollback = configuration.ArtifactStore.Rollback(StoreSnapshots()); }
            catch (Exception cause) { return UpdateFailure<bool>(cause); }
            return ActivatePreparedUpdateAsync(rollback);
        }

        /// <summary>
        /// 默认不热共存同项目 native Bundle。任何 live/pending 责任返回 Busy；idle 退休后延迟 Destroy 尚未完成也须重试。
        /// 不取消别人的请求，不归还外部 lease，不修改应用场景。所有失败路径都恢复旧项目的取得入口。
        /// </summary>
        public ResourceRequest<bool> ActivatePreparedUpdateAsync(PreparedUpdate update)
        {
            CheckThread();
            if (!CanUpdate(out var error)) return Fail(new ResourceRequest<bool>(), error.Kind, error.Message);
            var request = new ResourceRequest<bool>();
            var store = configuration.ArtifactStore;
            if (update == null || update.Root != store.RootPath || update.Projects.Count != store.Projects.Count ||
                update.Projects.Select(x => x.ProjectId).Distinct(StringComparer.Ordinal).Count() != store.Projects.Count ||
                update.Projects.Any(x => !catalogs.TryGetValue(x.ProjectId, out var catalog) || catalog.Snapshot == null))
                return Fail(request, ResourceError.InvalidConfiguration, "PreparedUpdate does not match this store's explicit closure.");
            if (update.Projects.All(x => catalogs[x.ProjectId].CatalogDigest == x.ToDigest) && store.HasConfirmedReleases(update))
            { request.SetSuccess(true); return request; }
            if (update.Projects.Any(x => catalogs[x.ProjectId].CatalogDigest != x.FromDigest))
                return Fail(request, ResourceError.StaleBase, "PreparedUpdate's source version changed; Check and Prepare again, or explicitly Rollback.");
            updating = true;
            foreach (var project in update.Projects) activationProjects.Add(project.ProjectId);
            try
            {
                foreach (var cache in instanceCaches.ToArray())
                    if (IsProjectBlocked(cache.AssetRecord.Entry.Address.ProjectId)) cache.Dispose();
                TickInstanceCleanup();
                var busy = records.Values.Any(x => IsProjectBlocked(x.Entry.Address.ProjectId)) ||
                    bundleRecords.Values.Any(x => IsProjectBlocked(x.Snapshot.Document.ProjectId)) ||
                    instanceCaches.Any(x => IsProjectBlocked(x.AssetRecord.Entry.Address.ProjectId)) ||
                    sceneLoads.Any(x => IsProjectBlocked(x.Address.ProjectId)) || sceneRecords.Any(x => IsProjectBlocked(x.Address.ProjectId));
                if (busy) throw new ResourceException(ResourceError.Busy, "Activation is blocked by live leases, instances, Scenes or native operations. The old version remains available.");
                // v1 的项目 native namespace 也覆盖已移除的旧 Bundle；不接管其他代码已加载的同项目原生资源。
                var nativePrefixes = update.Projects.Select(x => "ylr_" + ResourceCatalogCodec.Digest(System.Text.Encoding.UTF8.GetBytes(x.ProjectId)).Substring(0, 32) + "_").ToArray();
                if (AssetBundle.GetAllLoadedAssetBundles().Any(bundle => bundle && nativePrefixes.Any(prefix => bundle.name.StartsWith(prefix, StringComparison.Ordinal))))
                    throw new ResourceException(ResourceError.Busy, "The project's native Bundle namespace is still loaded outside the managed graph.");
                // 目标类型和平台先通过；持久提交后只做无回调的字典替换，不再开始新的可失败资源生产。
                var replacements = update.Projects.ToDictionary(x => x.ProjectId, x => CreateStoreCatalog(x.Target), StringComparer.Ordinal);
                if (!IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "Activation session ended before commit.");
                Exception commitFailure = null;
                try
                {
                    var selected = store.Commit(update);
                    foreach (var snapshot in selected)
                        if (replacements[snapshot.Document.ProjectId].CatalogDigest != snapshot.Document.CatalogDigest)
                            replacements[snapshot.Document.ProjectId] = CreateStoreCatalog(snapshot);
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
                if (!IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "The activation session ended; startup will mount the committed closure.");
                foreach (var pair in replacements) catalogs[pair.Key] = pair.Value;
                // 恢复到 From/LKG 也必须同步内存，但不能把它报告为目标激活成功。
                if (replacements.Any(x => x.Value.CatalogDigest != update.Projects.Single(y => y.ProjectId == x.Key).ToDigest))
                    request.SetFailure(ResourceRequestStatus.Failed, commitFailure is ResourceException { Kind: ResourceError.StaleBase } stale ? stale :
                        new ResourceException(ResourceError.UpdateFailed, "Activation did not reach its target; memory and disk recovered the same complete closure.", commitFailure));
                else request.SetSuccess(true);
            }
            catch (Exception cause)
            { request.SetFailure(ResourceRequestStatus.Failed, AsUpdateError(cause)); }
            finally { activationProjects.Clear(); updating = false; }
            request.Notify();
            return request;
        }

        public ResourceRequest<VersionCleanupResult> CleanupVersionsAsync() => UpdateRequest(() => configuration.ArtifactStore.Cleanup(
            StoreSnapshots().Concat(bundleRecords.Values.Select(x => x.Snapshot)).Distinct()));

        public string GetActiveCatalogDigest(string projectId)
        {
            CheckThread();
            if (!catalogs.TryGetValue(projectId, out var catalog)) throw new ArgumentException("Unknown resource project.", nameof(projectId));
            return catalog.CatalogDigest;
        }

        private ResourceCatalog CreateStoreCatalog(ResourceLocalSnapshot snapshot)
        {
            ValidateSnapshotPlatform(snapshot);
            return snapshot.CreateCatalog(configuration.ArtifactStore.Projects.Single(x => x.ProjectId == snapshot.Document.ProjectId).AssetTypes);
        }

        private bool CanUpdate(out ResourceException error)
        {
            error = !IsAvailable ? new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.") :
                configuration.ArtifactStore == null ? new ResourceException(ResourceError.Unsupported, "No local ArtifactStore is configured.") :
                updating || remoteJobs.Count != 0 ? new ResourceException(ResourceError.Busy, "A resource version operation is already in progress.") : null;
            return error == null;
        }

        private ResourceRequest<T> UpdateRequest<T>(Func<T> operation)
        {
            CheckThread();
            if (!CanUpdate(out var error)) return Fail(new ResourceRequest<T>(), error.Kind, error.Message);
            var request = new ResourceRequest<T>();
            updating = true;
            try
            {
                var result = operation();
                if (!IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "The update session ended.");
                request.SetSuccess(result);
            }
            catch (OperationCanceledException cause) { request.SetFailure(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "Local preparation was canceled; active is unchanged.", cause)); }
            catch (Exception cause) { request.SetFailure(ResourceRequestStatus.Failed, AsUpdateError(cause)); }
            finally { updating = false; }
            request.Notify();
            return request;
        }

        private static ResourceException AsUpdateError(Exception cause) => cause as ResourceException ?? new ResourceException(ResourceError.UpdateFailed, "Resource version operation failed.", cause);
        private static ResourceRequest<T> UpdateFailure<T>(Exception cause) => Fail(new ResourceRequest<T>(),
            cause is ResourceException resource ? resource.Kind : ResourceError.UpdateFailed, "Local resource version operation failed.", cause: cause);
    }
}
