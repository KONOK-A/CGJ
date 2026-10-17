using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace YLFramework.Services.Resource
{
    internal interface IAssetLeaseDiagnostics
    {
        IAssetLeaseDiagnostics Previous { get; set; }
        IAssetLeaseDiagnostics Next { get; set; }
        ResourceAddress Address { get; }
        string OwnerLabel { get; }
        bool IsInstancePin { get; }
    }

    /// <summary>按需复制的不可变诊断数据。不持有 Unity 对象、可变 records 或任何资源使用权。</summary>
    public sealed class ResourceDiagnosticsReport
    {
        // 诊断集合来自实际责任数量，不能误套单个 Catalog 的条目上限。
        internal static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values) =>
            (values == null ? new List<T>() : new List<T>(values)).AsReadOnly();

        public DateTime CapturedAtUtc { get; }
        public ResourceSystemState State { get; }
        public ResourceDiagnosticsSnapshot Summary { get; }
        /// <summary>本次只读采样的显式数据预算与进程CNG成本；不保活输入、不代表进程总内存。</summary>
        public ResourceContentDiagnosticsSnapshot Content { get; }
        public IReadOnlyList<ResourceProjectDiagnostics> Projects { get; }
        public IReadOnlyList<ResourceAssetDiagnostics> Assets { get; }
        public IReadOnlyList<ResourceBundleDiagnostics> Bundles { get; }
        public IReadOnlyList<ResourceInstanceCacheDiagnostics> InstanceCaches { get; }
        public IReadOnlyList<ResourceSceneDiagnostics> Scenes { get; }
        public IReadOnlyList<ResourceUpdateDiagnostics> Updates { get; }
        public IReadOnlyList<string> RecoveryNotes { get; }
        public IReadOnlyList<ResourceDiagnosticEvent> History { get; }
        public int HistoryCapacity { get; }

        internal ResourceDiagnosticsReport(ResourceSystemState state, ResourceDiagnosticsSnapshot summary,
            IEnumerable<ResourceProjectDiagnostics> projects, IEnumerable<ResourceAssetDiagnostics> assets,
            IEnumerable<ResourceBundleDiagnostics> bundles, IEnumerable<ResourceInstanceCacheDiagnostics> caches,
            IEnumerable<ResourceSceneDiagnostics> scenes, IEnumerable<ResourceUpdateDiagnostics> updates,
            IEnumerable<string> recoveryNotes, IEnumerable<ResourceDiagnosticEvent> history, int capacity, ResourceContentDiagnosticsSnapshot content = default)
        {
            CapturedAtUtc = DateTime.UtcNow; State = state; Summary = summary;
            Content = content;
            Projects = Copy(projects); Assets = Copy(assets); Bundles = Copy(bundles);
            InstanceCaches = Copy(caches); Scenes = Copy(scenes); Updates = Copy(updates);
            RecoveryNotes = Copy(recoveryNotes); History = Copy(history); HistoryCapacity = capacity;
        }
    }

    public sealed class ResourceProjectDiagnostics
    {
        public string ProjectId { get; }
        public Guid SnapshotId { get; }
        public string CatalogDigest { get; }
        public int? SchemaVersion { get; }
        public string BuildId { get; }
        public string Platform { get; }
        public string UnityCompatibility { get; }
        public string AppContentCompatibilityId { get; }
        public bool ActivationBlocked { get; }
        public IReadOnlyList<ResourceSourceId> Sources { get; }

        internal ResourceProjectDiagnostics(ResourceCatalog catalog, bool blocked, IEnumerable<ResourceSourceId> activeSources)
        {
            ProjectId = catalog.ProjectId; SnapshotId = catalog.SnapshotId; CatalogDigest = catalog.CatalogDigest; ActivationBlocked = blocked;
            var document = catalog.Snapshot?.Document;
            SchemaVersion = document?.SchemaVersion; BuildId = document?.BuildId; Platform = document?.Platform;
            UnityCompatibility = document?.UnityCompatibility; AppContentCompatibilityId = document?.AppContentCompatibilityId;
            Sources = ResourceDiagnosticsReport.Copy(catalog.Entries.Select(x => x.Location.Source).Concat(activeSources)
                .Concat(document == null ? Array.Empty<ResourceSourceId>() : new[] { ResourceSourceId.AssetBundle }).Distinct());
        }
    }

    public sealed class ResourceLeaseOwnerDiagnostics
    {
        public ResourceAddress Address { get; }
        public ResourceAddress RequestedAddress { get; }
        public string OwnerLabel { get; }
        public bool IsLabeled => !string.IsNullOrWhiteSpace(OwnerLabel);
        public bool IsInstancePin { get; }
        internal ResourceLeaseOwnerDiagnostics(IAssetLeaseDiagnostics lease, ResourceAddress canonicalAddress)
        { Address = canonicalAddress; RequestedAddress = lease.Address; OwnerLabel = lease.OwnerLabel; IsInstancePin = lease.IsInstancePin; }
    }

    public sealed class ResourceAssetDiagnostics
    {
        public string AssetId { get; }
        public ResourceAddress Address { get; }
        public Guid SnapshotId { get; }
        public string CatalogDigest { get; }
        public ResourceSourceId Source { get; }
        public string Locator { get; }
        public Type DeclaredType { get; }
        public bool IsLoading { get; }
        /// <summary>尚未启动图片IO；仍属于同一记录及独立等待者，不是native操作。</summary>
        public bool IsQueued { get; }
        public bool CancellationRequested { get; }
        public float Progress { get; }
        public int WaiterCount { get; }
        public int LeaseCount { get; }
        public int InstancePins { get; }
        public bool ObjectAlive { get; }
        public int ObjectInstanceId { get; }
        public bool ImageCacheEligible { get; }
        public bool IsImageIdle { get; }
        /// <summary>图片缓存的估计字节，不是 Unity native memory 的测量值。</summary>
        public long ImageBytes { get; }
        public double IdleSince { get; }
        public IReadOnlyList<ResourceLeaseOwnerDiagnostics> Owners { get; }

        internal ResourceAssetDiagnostics(ResourceSystem.AssetRecord record)
        {
            AssetId = record.Entry.AssetId; Address = record.Entry.Address; SnapshotId = record.Key.Item1.SnapshotId;
            CatalogDigest = record.Key.Item1.CatalogDigest; Source = record.Entry.Location.Source; Locator = record.Entry.Location.DisplayLocator;
            DeclaredType = record.Entry.Location.AssetType; IsLoading = record.Loading; CancellationRequested = record.CancelRequested;
            IsQueued = record.ImageQueueNode != null;
            Progress = record.Loading ? record.Operation?.Progress ?? record.LastProgress : 1;
            WaiterCount = record.Waiters.Count; LeaseCount = record.LeaseCount; InstancePins = record.InstancePins;
            ObjectAlive = record.Asset; ObjectInstanceId = record.Asset ? record.Asset.GetInstanceID() : 0;
            ImageCacheEligible = record.ImageCacheEligible; IsImageIdle = record.ImageCacheNode != null;
            ImageBytes = record.ImageBytes; IdleSince = record.IdleSince;
            var owners = new List<ResourceLeaseOwnerDiagnostics>();
            for (var lease = record.LeaseHead; lease != null; lease = lease.Next)
                if (record.Owner.IsSessionActive || lease.IsInstancePin) owners.Add(new ResourceLeaseOwnerDiagnostics(lease, record.Entry.Address));
            Owners = ResourceDiagnosticsReport.Copy(owners);
        }
    }

    /// <summary>Count 为真实图根或依赖入边数；native pending 的 Count 为0，不能混入 RootPins。</summary>
    public sealed class ResourceRetentionReason
    {
        public string Kind { get; }
        public string OwnerLabel { get; }
        public ResourceAddress? Address { get; }
        public string ParentBundleId { get; }
        public int Count { get; }
        public string Detail { get; }

        internal ResourceRetentionReason(string kind, string label, ResourceAddress? address, string parent, int count, string detail)
        { Kind = kind; OwnerLabel = label; Address = address; ParentBundleId = parent; Count = count; Detail = detail; }
    }

    public sealed class ResourceBundleDiagnostics
    {
        public string ProjectId { get; }
        public string CatalogDigest { get; }
        public string BundleId { get; }
        public string NativeName { get; }
        public string State { get; }
        public bool NativePending { get; }
        public bool ContentWorkPending { get; }
        public bool IsEncrypted { get; }
        public bool ContentFaulted { get; }
        public bool ContentCleanupFailed { get; }
        public float Progress { get; }
        public bool ObjectAlive { get; }
        public int WaiterCount { get; }
        public int RootPins { get; }
        public int DependencyPins { get; }
        /// <summary>Catalog交付文件字节，可能是密文；不是native memory。</summary>
        public long FileBytes { get; }
        public string Cause { get; }
        public IReadOnlyList<ResourceRetentionReason> RetentionReasons { get; }

        internal ResourceBundleDiagnostics(ResourceSystem.BundleRecord record, IEnumerable<ResourceRetentionReason> reasons)
        {
            ProjectId = record.Snapshot.Document.ProjectId; CatalogDigest = record.Snapshot.Document.CatalogDigest;
            BundleId = record.Descriptor.LogicalId; NativeName = record.Descriptor.NativeName; State = record.State.ToString();
            NativePending = record.Native != null; Progress = record.Native?.progress ?? (record.IsDone ? 1 : 0); ObjectAlive = record.Bundle;
            ContentWorkPending = record.InputWork != null; IsEncrypted = record.Header != null;
            ContentFaulted = record.Input?.Fault != null; ContentCleanupFailed = record.CleanupError != null;
            WaiterCount = record.Waiters.Count; RootPins = record.RootPins; DependencyPins = record.References - record.RootPins;
            FileBytes = record.Descriptor.Length; Cause = (record.Error ?? record.CleanupError)?.ToString(); RetentionReasons = ResourceDiagnosticsReport.Copy(reasons);
        }
    }

    public sealed class ResourceInstanceCacheDiagnostics
    {
        public ResourceAddress Address { get; }
        public Guid SnapshotId { get; }
        public string CatalogDigest { get; }
        public InstanceCachePolicy Policy { get; }
        public int SceneHandle { get; }
        public bool IsRetired { get; }
        public int ActiveCount { get; }
        public int IdleCount { get; }
        public int DestroyPendingCount { get; }
        public int DeadAwaitingPruneCount { get; }
        public int TransitionCount { get; }
        public int OperationCount { get; }

        internal ResourceInstanceCacheDiagnostics(ResourceSystem.AssetRecord asset, InstanceCachePolicy policy, int sceneHandle,
            bool retired, int active, int idle, int destroying, int dead, int transitions, int operations)
        {
            Address = asset.Entry.Address; SnapshotId = asset.Key.Item1.SnapshotId; CatalogDigest = asset.Key.Item1.CatalogDigest;
            Policy = policy; SceneHandle = sceneHandle; IsRetired = retired; ActiveCount = active; IdleCount = idle;
            DestroyPendingCount = destroying; DeadAwaitingPruneCount = dead; OperationCount = operations;
            TransitionCount = transitions;
        }
    }

    public sealed class ResourceSceneDiagnostics
    {
        public ResourceAddress Address { get; }
        public Guid SnapshotId { get; }
        public string CatalogDigest { get; }
        public string Path { get; }
        public long LoadSequence { get; }
        public int? Handle { get; }
        public string State { get; }
        public bool NativePending { get; }
        public bool IsLoaded { get; }
        public bool IsAmbiguous { get; }
        public bool WaitingForActivation { get; }
        public bool HasRecord { get; }
        public bool NativeStarted { get; }
        public bool NativeCompleted { get; }
        public ResourceRequestStatus? RequestStatus { get; }

        internal ResourceSceneDiagnostics(ResourceAddress address, Guid snapshot, string digest, string path, long sequence,
            int? handle, string state, bool native, bool loaded, bool ambiguous, bool activation, ResourceRequestStatus? status,
            bool hasRecord, bool nativeStarted, bool nativeCompleted)
        {
            Address = address; SnapshotId = snapshot; CatalogDigest = digest; Path = path; LoadSequence = sequence;
            Handle = handle; State = state; NativePending = native; IsLoaded = loaded; IsAmbiguous = ambiguous;
            WaitingForActivation = activation; RequestStatus = status;
            HasRecord = hasRecord; NativeStarted = nativeStarted; NativeCompleted = nativeCompleted;
        }
    }

    public sealed class ResourceUpdateDiagnostics
    {
        public string Phase { get; }
        public int WaiterCount { get; }
        public float Progress { get; }
        public long? TransferredBytes { get; }
        public long? ExpectedBytes { get; }
        public int? RetryCount { get; }
        public bool WaitingToRetry { get; }
        public double? DurationMilliseconds { get; }
        public IReadOnlyList<string> ActivationProjects { get; }

        internal ResourceUpdateDiagnostics(string phase, int waiters, float progress, long? bytes, long? expected,
            int? retries, bool waiting, double? duration, IEnumerable<string> projects = null)
        {
            Phase = phase; WaiterCount = waiters; Progress = progress; TransferredBytes = bytes; ExpectedBytes = expected;
            RetryCount = retries; WaitingToRetry = waiting; DurationMilliseconds = duration; ActivationProjects = ResourceDiagnosticsReport.Copy(projects);
        }
    }

    public sealed class ResourceDiagnosticEvent
    {
        public DateTime AtUtc { get; }
        public string Kind { get; }
        public ResourceRequestStatus? Status { get; }
        public ResourceAddress? Address { get; }
        public string Cause { get; }
        public double DurationMilliseconds { get; }
        public bool HasDuration { get; }

        internal ResourceDiagnosticEvent(string kind, ResourceRequestStatus? status, ResourceAddress? address, string cause, double? duration)
        {
            AtUtc = DateTime.UtcNow; Kind = kind; Status = status; Address = address; Cause = cause;
            HasDuration = duration.HasValue; DurationMilliseconds = duration ?? 0;
        }
    }
}
