using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        /// <summary>只读复制数据预算及分列CNG成本，不取key、不认证、不创建native。</summary>
        public ResourceContentDiagnosticsSnapshot GetContentDiagnosticsSnapshot()
        {
            CheckThread();
            var budget = contentBudget.Capture();
            long nativeBuffers = 0;
            foreach (var bundle in bundleRecords.Values)
                if (bundle.Input?.IsEncrypted == true && (bundle.Native != null || bundle.Bundle))
                    nativeBuffers += ResourceBundleEnvelope.NativeReadBufferSize;
            return new ResourceContentDiagnosticsSnapshot(contentBudget.MaximumBytes, budget.Allocated, budget.Reserved, budget.Peak,
                budget.Inputs, contentWork.Count, nativeBuffers, retainedContentCleanup.Count);
        }

        private BundleLease diagnosticBundleLeases;
        private ResourceDiagnosticEvent[] diagnosticHistory;
        private int diagnosticHistoryCount;
        private int diagnosticHistoryNext;
        private int diagnosticHistoryGeneration;

        internal void RegisterBundleDiagnostics(BundleLease lease)
        {
            lease.Next = diagnosticBundleLeases;
            if (lease.Next != null) lease.Next.Previous = lease;
            diagnosticBundleLeases = lease;
        }

        internal void UnregisterBundleDiagnostics(BundleLease lease)
        {
            if (lease.Previous == null) diagnosticBundleLeases = lease.Next;
            else lease.Previous.Next = lease.Next;
            if (lease.Next != null) lease.Next.Previous = lease.Previous;
            lease.Previous = null;
            lease.Next = null;
        }

        /// <summary>显式开启有界历史；0关闭并清空。默认不订阅请求、不抓栈、不做额外逐帧扫描。</summary>
        public void ConfigureDiagnosticsHistory(int capacity)
        {
            CheckThread();
            if (capacity < 0 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity), "History capacity must be between 0 and 4096.");
            diagnosticHistoryGeneration++;
            diagnosticHistory = capacity == 0 ? null : new ResourceDiagnosticEvent[capacity];
            diagnosticHistoryCount = 0;
            diagnosticHistoryNext = 0;
        }

        private void TrackDiagnostics<T>(ResourceRequest<T> request)
        {
            if (diagnosticHistory == null) return;
            ObserveRequestDiagnostics(request);
        }

        private void ObserveRequestDiagnostics<T>(ResourceRequest<T> request)
        {
            // 单独方法确保关闭历史时连编译器生成的 closure 也不会创建。
            var generation = diagnosticHistoryGeneration;
            var started = Stopwatch.GetTimestamp();
            request.Completed += result =>
            {
                if (generation != diagnosticHistoryGeneration || diagnosticHistory == null) return;
                AddDiagnosticEvent(new ResourceDiagnosticEvent("RequestCompleted", result.Status, result.Error?.Address,
                    result.Error?.ToString(), (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency));
            };
        }

        private void RecordProducerCompletion(AssetRecord record, ResourceException failure, int waiterCount)
        {
            if (diagnosticHistory == null || waiterCount != 0 && failure == null) return;
            AddDiagnosticEvent(new ResourceDiagnosticEvent(waiterCount == 0 ? "LateResult" : "ProducerFailed",
                failure == null ? ResourceRequestStatus.Succeeded : ResourceRequestStatus.Failed,
                record.Entry.Address, failure?.ToString(), null));
        }

        private void AddDiagnosticEvent(ResourceDiagnosticEvent item)
        {
            diagnosticHistory[diagnosticHistoryNext] = item;
            diagnosticHistoryNext = (diagnosticHistoryNext + 1) % diagnosticHistory.Length;
            if (diagnosticHistoryCount < diagnosticHistory.Length) diagnosticHistoryCount++;
        }

        /// <summary>只按需复制本 authority 的实际记录和租约，不搜索全场景、不触发加载、卸载、恢复或磁盘清理。</summary>
        public ResourceDiagnosticsReport GetDiagnosticsReport()
        {
            CheckThread();
            var assetRecords = records.Values.ToArray();
            var bundles = bundleRecords.Values.ToArray();
            var caches = instanceCaches.ToDictionary(x => x, x => x.CaptureDiagnostics());
            var reasons = bundles.ToDictionary(x => x, _ => new List<ResourceRetentionReason>());
            var assetRetentions = assetRecords.Where(x => x.Retention is BundleLease).ToDictionary(x => (BundleLease)x.Retention);
            var sceneRetentions = sceneRecords.Where(x => x.Retention != null).ToDictionary(x => x.Retention);
            var scenePendingRetentions = sceneLoads.Where(x => x.Retention != null).ToDictionary(x => x.Retention);
            for (var lease = diagnosticBundleLeases; lease != null; lease = lease.Next)
            {
                if (lease.Record == null || !reasons.TryGetValue(lease.Record, out var list)) continue;
                string kind;
                string detail;
                ResourceAddress? address = null;
                if (assetRetentions.TryGetValue(lease, out var asset))
                {
                    kind = "Asset";
                    address = asset.Entry.Address;
                    var text = new StringBuilder("Asset leases=").Append(asset.LeaseCount).Append("; instance pins=").Append(asset.InstancePins);
                    foreach (var cache in caches.Where(x => ReferenceEquals(x.Key.AssetRecord, asset)).Select(x => x.Value))
                        text.Append("; cache ").Append(cache.Policy).Append(" scene=").Append(cache.SceneHandle).Append(" active=").Append(cache.ActiveCount)
                            .Append(" idle=").Append(cache.IdleCount).Append(" destroy-pending=").Append(cache.DestroyPendingCount)
                            .Append(" dead-awaiting-prune=").Append(cache.DeadAwaitingPruneCount).Append(" transitions=").Append(cache.TransitionCount).Append(" retired=").Append(cache.IsRetired);
                    for (var owner = asset.LeaseHead; owner != null; owner = owner.Next)
                        if (asset.Owner.IsSessionActive || owner.IsInstancePin)
                            text.Append("; owner=").Append(string.IsNullOrWhiteSpace(owner.OwnerLabel) ? "(unlabeled)" : owner.OwnerLabel);
                    detail = text.ToString();
                }
                else if (sceneRetentions.TryGetValue(lease, out var scene))
                {
                    kind = "SceneRecord";
                    address = scene.Address;
                    detail = "Scene handle=" + scene.Handle + "; sequence=" + scene.LoadSequence + "; loaded=" + scene.IsLoaded +
                        "; unload-pending=" + (scene.UnloadOperation != null) + "; ambiguous=" + scene.IsAmbiguous;
                }
                else if (scenePendingRetentions.TryGetValue(lease, out var pendingScene))
                {
                    kind = "SceneLoad";
                    address = pendingScene.Address;
                    detail = "Scene sequence=" + pendingScene.Request.LoadSequence + "; native-started=" + pendingScene.Request.NativeStarted +
                        "; native-pending=" + (pendingScene.Request.Native != null && !pendingScene.Request.Native.isDone) + "; request=" + pendingScene.Request.Status;
                }
                else if (lease.Origin.SceneSequence.HasValue)
                {
                    kind = "ScenePreparing";
                    address = lease.Origin.Address;
                    detail = "Scene sequence=" + lease.Origin.SceneSequence + "; preparing its Bundle; no native Scene started yet.";
                }
                else if (lease.Origin.GroupId != null)
                { kind = "GroupPreload"; detail = "Group=" + lease.Origin.GroupId + "; Scene Bundle preparation only, not a loaded Scene."; }
                else if (lease.Public)
                { kind = "PublicBundleLease"; detail = "Explicit public BundleLease; unlabeled owner remains unknown."; }
                else if (lease.Origin.Address.HasValue)
                { kind = "AssetProducer"; address = lease.Origin.Address; detail = "Source retains the graph until its native asset result transfers or is cleaned up."; }
                else
                { kind = "UnknownInternal"; detail = "A real internal BundleLease is holding this root; its owner has not been labeled."; }
                list.Add(new ResourceRetentionReason(kind, lease.OwnerLabel, address, null, 1, detail));
            }
            foreach (var parent in bundles)
                foreach (var dependency in parent.Dependencies)
                    if (reasons.TryGetValue(dependency, out var list))
                        list.Add(new ResourceRetentionReason("ParentDependency", null, null, parent.Descriptor.LogicalId, 1,
                            "Held by parent Bundle " + parent.Descriptor.LogicalId + " (" + parent.State + ")."));
            foreach (var bundle in bundles)
                if (bundle.InputWork != null)
                    reasons[bundle].Add(new ResourceRetentionReason("ContentIO", null, null, null, 0,
                        "Cancelable verification/input work still owns its input and reservation until completion is observed."));
            foreach (var bundle in bundles)
                if (bundle.Native != null)
                    reasons[bundle].Add(new ResourceRetentionReason("NativePending", null, null, null, 0,
                        "AssetBundleCreateRequest cannot be aborted; the graph keeps cleanup responsibility until native completion."));
            var scenes = new List<ResourceSceneDiagnostics>();
            foreach (var load in sceneLoads)
                scenes.Add(new ResourceSceneDiagnostics(load.Address, load.Catalog.SnapshotId, load.Catalog.CatalogDigest, load.Path,
                    load.Request.LoadSequence, null, load.Request.NativeCompleted ? "AwaitingIdentity" : load.Request.NativeStarted ? "NativeLoading" : load.Started ? "Preparing" : "Queued",
                    load.Request.Native != null && !load.Request.Native.isDone, false, false, load.Request.IsWaitingForActivation,
                    load.Request.Status, false, load.Request.NativeStarted, load.Request.NativeCompleted));
            foreach (var scene in sceneRecords)
                scenes.Add(new ResourceSceneDiagnostics(scene.Address, scene.SnapshotId, scene.CatalogDigest, scene.Path,
                    scene.LoadSequence, scene.Handle, scene.UnloadOperation != null ? "Unloading" : scene.IsLoaded ? "Loaded" : "AwaitingCleanup",
                    scene.UnloadOperation != null && !scene.UnloadOperation.isDone, scene.IsLoaded, scene.IsAmbiguous, false, null, true, true, true));
            var updates = remoteJobs.Values.Select(x => x.CaptureDiagnostics()).ToList();
            if (contentWork.Count != 0) updates.Add(new ResourceUpdateDiagnostics("ContentIO", 0, 0, null, null, null, false, null,
                contentWork.SelectMany(x => x.Snapshots).Select(x => x.Document.ProjectId).Distinct().OrderBy(x => x, StringComparer.Ordinal)));
            if (updating) updates.Add(new ResourceUpdateDiagnostics(activationProjects.Count != 0 ? "Activation" : "LocalVersionTransaction",
                0, 0, null, null, null, false, null, activationProjects.OrderBy(x => x, StringComparer.Ordinal)));
            var history = new List<ResourceDiagnosticEvent>();
            if (diagnosticHistory != null)
                for (var i = 0; i < diagnosticHistoryCount; i++)
                    history.Add(diagnosticHistory[(diagnosticHistoryNext - diagnosticHistoryCount + i + diagnosticHistory.Length) % diagnosticHistory.Length]);
            return new ResourceDiagnosticsReport(State, GetDiagnosticsSnapshot(),
                catalogs.Values.Concat(assetRecords.Select(x => x.Key.Item1)).Distinct().OrderBy(x => x.ProjectId, StringComparer.Ordinal)
                    .Select(x => new ResourceProjectDiagnostics(x, IsProjectBlocked(x.ProjectId),
                        assetRecords.Where(record => ReferenceEquals(record.Key.Item1, x)).Select(record => record.Entry.Location.Source))),
                assetRecords.Select(x => new ResourceAssetDiagnostics(x)), bundles.Select(x => new ResourceBundleDiagnostics(x, reasons[x])),
                caches.Values, scenes, updates, configuration.ArtifactStore?.RecoveryNotes, history, diagnosticHistory?.Length ?? 0, GetContentDiagnosticsSnapshot());
        }
    }
}
