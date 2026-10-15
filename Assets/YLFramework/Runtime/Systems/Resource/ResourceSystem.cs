using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using YLFramework.Core.Singleton;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Services.Resource
{
    public enum ResourceSystemState { Initializing, Ready, ShuttingDown, Closed, Failed, Invalidated }

    /// <summary>逻辑记账快照，不代表 Unity native memory 用量或物理卸载。</summary>
    public readonly struct ResourceDiagnosticsSnapshot
    {
        public int RecordCount { get; }
        public int ProducerCount { get; }
        public int WaiterCount { get; }
        public int LeaseCount { get; }
        public int BundleRecordCount { get; }
        public int BundleRootPinCount { get; }
        public int BundleDependencyPinCount { get; }
        public int RunningBundleCount { get; }
        public int QueuedBundleCount { get; }
        public int NativeBundleLoadCount { get; }
        public int NativeBundleUnloadCount { get; }
        internal ResourceDiagnosticsSnapshot(int records, int producers, int waiters, int leases, int bundles,
            int roots, int edges, int running, int queued, int loads, int unloads)
        {
            RecordCount = records; ProducerCount = producers; WaiterCount = waiters; LeaseCount = leases;
            BundleRecordCount = bundles; BundleRootPinCount = roots; BundleDependencyPinCount = edges;
            RunningBundleCount = running; QueuedBundleCount = queued; NativeBundleLoadCount = loads; NativeBundleUnloadCount = unloads;
        }
    }

    /// <summary>每会话唯一、显式初始化的资源权威。所有入口仅用于 Unity 主线程，不创建协程宿主。</summary>
    public sealed partial class ResourceSystem
    {
        private static ResourceSystem authority;
        private static readonly HashSet<ResourceSystem> draining = new();
        private static bool ending;
        private static int mainThreadId;
        private readonly ResourceConfiguration configuration;
        private readonly object session = SingletonSessionState.ExecutionSession;
        private readonly Dictionary<string, ResourceCatalog> catalogs = new(StringComparer.Ordinal);
        private readonly Dictionary<ResourceSourceId, IResourceSource> sources = new();
        private readonly Dictionary<(ResourceCatalog, string, Type), AssetRecord> records = new();
        private readonly HashSet<IResourceRequest> ownedRequests = new();
        private readonly List<ResourceRequest<ResourceSystem>> initialization = new();
        private readonly List<ResourceRequest<bool>> shutdowns = new();
        private bool checkingShutdown;

        /// <summary>读取不加载资源、不初始化服务、不创建宿主；初始化失败或关闭后为 null。</summary>
        public static ResourceSystem Current => authority != null &&
            (authority.State == ResourceSystemState.Ready || authority.State == ResourceSystemState.ShuttingDown) ? authority : null;
        public ResourceSystemState State { get; private set; } = ResourceSystemState.Initializing;
        public bool IsAvailable => State == ResourceSystemState.Ready && IsSessionActive;
        internal bool IsSessionActive => !ending && SingletonSessionState.CanRunExecution && ReferenceEquals(session, SingletonSessionState.ExecutionSession) &&
            (State == ResourceSystemState.Ready || State == ResourceSystemState.Initializing || State == ResourceSystemState.ShuttingDown);

        static ResourceSystem()
        {
            SingletonSessionState.RegisterSessionReset(ResetSession);
            Application.quitting += EndSession;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += () => { if (!Application.isPlaying) TickInstanceCleanup(); };
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeThread() => mainThreadId = Thread.CurrentThread.ManagedThreadId;
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditorThread() => InitializeThread();
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredEditMode) EndSession();
        }
#endif

        private ResourceSystem(ResourceConfiguration configuration) => this.configuration = configuration;

        /// <summary>8.1 内存验证可即时完成。同一配置对象的在途初始化可 join；不同配置须先成功 Shutdown。</summary>
        /// <remarks>结果为借用的唯一服务，不因初始化 Request Dispose 而关闭；不提供自动领取 await。</remarks>
        public static ResourceRequest<ResourceSystem> InitializeAsync(ResourceConfiguration config)
        {
            AssertThread();
            var request = new ResourceRequest<ResourceSystem>();
            if (ending || !SingletonSessionState.CanRunExecution)
                return Fail(request, ResourceError.SessionUnavailable, "Resource initialization requires an active Play/Player session.");
            TickInstanceCleanup();
            if (draining.Count != 0)
                return Fail(request, ResourceError.Busy, "An old session still owns unfinished native producers.");
            if (authority != null)
            {
                if (!ReferenceEquals(authority.configuration, config))
                    return Fail(request, ResourceError.ConfigurationConflict, "Another resource configuration is active or initializing.");
                if (authority.State == ResourceSystemState.Initializing) authority.initialization.Add(request);
                else if (authority.IsAvailable) { request.SetSuccess(authority); request.Notify(); }
                else return Fail(request, ResourceError.Busy, "The resource authority is closing.");
                return request;
            }
            var system = new ResourceSystem(config);
            authority = system;
            system.initialization.Add(request);
            ResourceException failure = null;
            try
            {
                system.ValidateConfiguration();
                if (!system.IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "Initialization was invalidated by session exit.");
            }
            catch (Exception error)
            {
                failure = error as ResourceException ?? new ResourceException(ResourceError.InvalidConfiguration, "Resource configuration validation failed.", error);
            }
            var joined = system.initialization.ToArray();
            system.initialization.Clear();
            if (failure == null)
            {
                system.State = ResourceSystemState.Ready;
                foreach (var item in joined) item.SetSuccess(system);
            }
            else
            {
                if (system.State != ResourceSystemState.Invalidated) system.State = ResourceSystemState.Failed;
                if (ReferenceEquals(authority, system)) authority = null;
                foreach (var item in joined) item.SetFailure(ResourceRequestStatus.Failed, failure);
            }
            foreach (var item in joined) item.Notify();
            return request;
        }

        private void ValidateConfiguration()
        {
            if (configuration == null || configuration.Catalogs.Count == 0 && configuration.ArtifactStore == null)
                throw new ArgumentException("An explicit nonempty catalog configuration is required.");
            foreach (var source in configuration.Sources)
            {
                if (source == null || !source.Id.IsValid || sources.ContainsKey(source.Id)) throw new ArgumentException("Null, invalid or duplicate source identity.");
                sources.Add(source.Id, source);
            }
            sources.Add(ResourceSourceId.AssetBundle, new AssetBundleSource(this));
            var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configuredCatalogs = new List<ResourceCatalog>(configuration.Catalogs);
            if (configuration.ArtifactStore != null)
                foreach (var snapshot in configuration.ArtifactStore.Mount()) configuredCatalogs.Add(CreateStoreCatalog(snapshot));
            foreach (var catalog in configuredCatalogs)
            {
                if (catalog == null || !projects.Add(catalog.ProjectId)) throw new ArgumentException("Null or case-conflicting project catalog.");
                catalogs.Add(catalog.ProjectId, catalog);
                if (catalog.Snapshot != null) ValidateSnapshotPlatform(catalog.Snapshot);
                var resourcePaths = new HashSet<(string, Type)>(new ResourcesPathComparer());
                foreach (var entry in catalog.Entries)
                {
                    if (!sources.TryGetValue(entry.Location.Source, out var source))
                        throw new ArgumentException($"Unconfigured source '{entry.Location.Source}' for '{entry.Address}'.");
                    source.Validate(entry);
                    if (entry.Location.Source.Equals(ResourceSourceId.Resources) && !resourcePaths.Add((entry.Location.Locator, entry.Location.AssetType)))
                        throw new ArgumentException($"Case-conflicting Resources locator: '{entry.Location.Locator}'.");
                    if (!IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "The session ended during source validation.");
                }
            }
        }

        private sealed class ResourcesPathComparer : IEqualityComparer<(string, Type)>
        {
            public bool Equals((string, Type) left, (string, Type) right) => left.Item2 == right.Item2 && StringComparer.OrdinalIgnoreCase.Equals(left.Item1, right.Item1);
            public int GetHashCode((string, Type) value) => StringComparer.OrdinalIgnoreCase.GetHashCode(value.Item1) ^ value.Item2.GetHashCode();
        }

        public AssetLease<T> Acquire<T>(ResourceAddress address) where T : UnityEngine.Object
        {
            using var request = AcquireRequest<T>(address, true);
            if (request.Status != ResourceRequestStatus.Succeeded) throw request.Error;
            return request.TakeResult();
        }

        public ResourceRequest<AssetLease<T>> AcquireAsync<T>(ResourceAddress address) where T : UnityEngine.Object => AcquireRequest<T>(address, false);
        public ResourceScope CreateScope()
        {
            CheckThread();
            if (!IsAvailable) throw new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            return new ResourceScope(this);
        }

        private ResourceRequest<AssetLease<T>> AcquireRequest<T>(ResourceAddress address, bool synchronous) where T : UnityEngine.Object
        {
            CheckThread();
            var request = new ResourceRequest<AssetLease<T>>(lease => lease.Dispose());
            if (!IsAvailable) return Fail(request, ResourceError.SessionUnavailable, "ResourceSystem is not available.", address);
            if (!address.IsValid) return Fail(request, ResourceError.InvalidAddress, "A valid resource address is required.", address);
            if (IsProjectBlocked(address.ProjectId)) return Fail(request, ResourceError.Busy, "The project is inside its activation barrier.", address);
            ResourceCatalogEntry entry;
            ResourceCatalog catalog;
            try
            {
                if (!catalogs.TryGetValue(address.ProjectId, out catalog)) throw new ArgumentException("Unknown resource project.");
                entry = catalog.Resolve(address);
            }
            catch (ArgumentException error)
            { return Fail(request, ResourceError.AddressNotFound, $"Unknown resource address: {address}.", address, cause: error); }
            if (!typeof(T).IsAssignableFrom(entry.Location.AssetType))
                return Fail(request, ResourceError.TypeMismatch, $"'{address}' declares {entry.Location.AssetType.FullName}, not {typeof(T).FullName}.", address, entry.Location.Source);
            // 当前投影只是可赋值借读；同一目录声明类型的基类请求不重复建立 native producer。
            var key = (catalog, entry.AssetId, entry.Location.AssetType);
            if (records.TryGetValue(key, out var record))
            {
                if (record.Loading && synchronous) return Fail(request, ResourceError.InProgress, $"'{address}' is already loading; use AcquireAsync.", address, entry.Location.Source);
                if (!record.Loading)
                {
                    if (!record.Asset) return Fail(request, ResourceError.LoadFailed, "The cached Unity object was destroyed externally.", address, entry.Location.Source);
                    Track(request);
                    request.SetSuccess(new AssetLease<T>(record, address));
                    request.Notify();
                    return request;
                }
            }
            else
            {
                var source = sources[entry.Location.Source];
                if (synchronous && !source.SupportsSynchronousLoad)
                    return Fail(request, ResourceError.Unsupported, "This source cannot produce synchronously.", address, entry.Location.Source);
                record = new AssetRecord(this, key, entry, source);
                records.Add(key, record);
            }
            Track(request);
            var waiter = new Waiter<T>(request, address);
            record.Waiters.Add(waiter);
            var loading = record;
            request.Attach(() =>
            {
                loading.Waiters.Remove(waiter);
                CancelUnobservedProducer(loading);
            }, () => loading.Operation?.Progress ?? loading.LastProgress);
            if (record.Started) return request;
            record.Started = true;
            try
            {
                if (synchronous) CompleteRecord(record, record.Source.Load(entry, entry.Location.AssetType), null);
                else
                {
                    StartAsyncProducer(record);
                }
            }
            catch (Exception cause)
            {
                // 只捕获来源生产失败；观察者异常已在 Request 中隔离，不伪装成加载失败。
                if (record.Loading) CompleteRecord(record, default, cause);
                else throw;
            }
            return request;
        }

        private void StartAsyncProducer(AssetRecord record)
        {
            var operation = record.Source.LoadAsync(record.Entry, record.Entry.Location.AssetType) ?? throw new InvalidOperationException("The source did not return a producer operation.");
            record.Operation = operation;
            record.Completion = () => FinishOperation(record);
            operation.Completed += record.Completion;
            if (operation.IsDone) FinishOperation(record);
            else CancelUnobservedProducer(record);
        }

        private static void CancelUnobservedProducer(AssetRecord record)
        {
            if (record.Waiters.Count != 0 || record.CancelRequested || record.Operation?.SupportsCancellation != true) return;
            // 先标记这一代已进入取消收尾，再调用可能同步完成/重入的私有 IO 取消。
            record.CancelRequested = true;
            record.Operation.Cancel();
        }

        internal void Track<T>(ResourceRequest<T> request)
        {
            ownedRequests.Add(request);
            request.Terminal = () =>
            {
                if (request.Status == ResourceRequestStatus.Succeeded) return;
                ownedRequests.Remove(request);
                request.OwnershipEnded = null;
            };
            request.OwnershipEnded = () => ownedRequests.Remove(request);
        }

        private void FinishOperation(AssetRecord record)
        {
            if (!record.Loading) return;
            var operation = record.Operation;
            record.LastProgress = operation.Progress;
            var result = operation.TakeResult();
            var cause = operation.Error;
            operation.Completed -= record.Completion;
            record.Completion = null;
            record.Operation = null;
            operation.Dispose();
            if (record.CancelRequested && record.Waiters.Count != 0 && IsAvailable)
            {
                // 新消费者等待旧 IO 收尾，不接收旧 Abort 结果，也不与它重叠生产。
                try
                {
                    ReleaseProduction(record.Source, result);
                    record.CancelRequested = false;
                    record.LastProgress = 0;
                    StartAsyncProducer(record);
                }
                catch (Exception error) { if (record.Loading) CompleteRecord(record, default, error); else throw; }
                return;
            }
            CompleteRecord(record, result, cause);
        }

        private void CompleteRecord(AssetRecord record, ResourceSourceResult result, Exception cause)
        {
            var asset = result.Asset;
            record.Retention = result.Retention;
            record.Loading = false;
            var waiters = record.Waiters.ToArray();
            record.Waiters.Clear();
            ResourceException failure = null;
            if (!IsSessionActive)
                failure = new ResourceException(ResourceError.SessionUnavailable, "The producing session ended.", cause, record.Entry.Address, record.Entry.Location.Source);
            else if (cause != null || !asset)
                failure = new ResourceException(cause is ResourceException resourceError ? resourceError.Kind : ResourceError.LoadFailed,
                    $"Source failed to load '{record.Entry.Address}' from {record.Entry.Location}.", cause, record.Entry.Address, record.Entry.Location.Source);
            else if (!record.Entry.Location.AssetType.IsInstanceOfType(asset))
                failure = new ResourceException(ResourceError.TypeMismatch, $"Source returned {asset.GetType().FullName} for {record.Entry.Location}.", address: record.Entry.Address, source: record.Entry.Location.Source);
            record.Asset = asset;
            // 全部消费者先登记独立 lease 和终态，清生产责任后才允许业务观察重入。
            foreach (var waiter in waiters) waiter.Prepare(record, failure);
            if (failure != null || record.LeaseCount == 0) ReleaseRecord(record);
            CheckDrain();
            foreach (var waiter in waiters) waiter.Notify();
        }

        internal sealed class AssetRecord
        {
            internal readonly ResourceSystem Owner;
            internal readonly (ResourceCatalog, string, Type) Key;
            internal readonly ResourceCatalogEntry Entry;
            internal readonly IResourceSource Source;
            internal readonly List<IWaiter> Waiters = new();
            internal UnityEngine.Object Asset;
            internal IDisposable Retention;
            internal int LeaseCount;
            internal int InstancePins;
            internal float LastProgress;
            internal bool Loading = true;
            internal bool CancelRequested;
            internal bool Started;
            internal ResourceSourceOperation Operation;
            internal Action Completion;
            internal AssetRecord(ResourceSystem owner, (ResourceCatalog, string, Type) key, ResourceCatalogEntry entry, IResourceSource source)
            { Owner = owner; Key = key; Entry = entry; Source = source; }
        }

        internal interface IWaiter
        {
            void Prepare(AssetRecord record, ResourceException failure);
            void Notify();
        }

        private sealed class Waiter<T> : IWaiter where T : UnityEngine.Object
        {
            private readonly ResourceRequest<AssetLease<T>> request;
            private readonly ResourceAddress address;
            internal Waiter(ResourceRequest<AssetLease<T>> request, ResourceAddress address) { this.request = request; this.address = address; }
            public void Prepare(AssetRecord record, ResourceException failure)
            {
                if (request.IsDone) return;
                if (failure == null) request.SetSuccess(new AssetLease<T>(record, address));
                else request.SetFailure(failure.Kind == ResourceError.SessionUnavailable ? ResourceRequestStatus.Invalidated : ResourceRequestStatus.Failed, failure);
            }
            public void Notify() => request.Notify();
        }

        internal void ReleaseLease(AssetRecord record, bool instancePin = false)
        {
            if (!instancePin && !IsSessionActive) return; // 退出已结算public责任，不能扣仍在等待Destroy的实例pin。
            if (record.LeaseCount == 0) return; // 会话强制退出已结算；旧 lease 不得影响新 record。
            if (instancePin) record.InstancePins--;
            if (--record.LeaseCount == 0 && !record.Loading) ReleaseRecord(record);
            CheckDrain();
        }

        private void ReleaseRecord(AssetRecord record)
        {
            if (records.TryGetValue(record.Key, out var current) && ReferenceEquals(current, record)) records.Remove(record.Key);
            var asset = record.Asset;
            record.Asset = null;
            record.LeaseCount = 0;
            var retention = record.Retention;
            record.Retention = null;
            ReleaseProduction(record.Source, new ResourceSourceResult(asset, retention));
        }

        private static void ReleaseProduction(IResourceSource source, ResourceSourceResult result)
        {
            if (!ReferenceEquals(result.Asset, null))
                try { source.Release(result.Asset); }
                catch (Exception error) { Debug.LogException(error); }
            result.Retention?.Dispose();
        }

        /// <summary>live Lease/等待者返回 Busy 并保持可用；零 owner 原生操作由同一 cleanup owner 排空后关闭。</summary>
        public ResourceRequest<bool> ShutdownAsync()
        {
            CheckThread();
            var request = new ResourceRequest<bool>();
            if (State == ResourceSystemState.Closed) { request.SetSuccess(true); return request; }
            if (!IsSessionActive) return Fail(request, ResourceError.SessionUnavailable, "The resource session is unavailable.");
            if (updating) return Fail(request, ResourceError.Busy, "A resource version operation is in progress.");
            if (checkingShutdown) return Fail(request, ResourceError.Busy, "Resource shutdown is checking its ownership barrier.");
            if (State == ResourceSystemState.ShuttingDown) { shutdowns.Add(request); CheckDrain(); return request; }
            State = ResourceSystemState.ShuttingDown;
            checkingShutdown = true;
            try
            {
                ClearInstanceCaches();
                var busy = publicBundleLeases.Count != 0 || sceneLoads.Count != 0 || sceneRecords.Count != 0;
                foreach (var cache in instanceCaches) if (cache.HasLiveOwners) busy = true;
                foreach (var record in records.Values)
                    if (record.LeaseCount > record.InstancePins || record.Waiters.Count != 0) busy = true;
                if (busy)
                {
                    State = ResourceSystemState.Ready;
                    return Fail(request, ResourceError.Busy, "Resource shutdown is blocked by live leases, instances, Scenes or consumers.");
                }
            }
            catch
            {
                State = ResourceSystemState.Ready;
                throw;
            }
            finally { checkingShutdown = false; }
            shutdowns.Add(request);
            CheckDrain();
            return request;
        }

        private void CheckDrain()
        {
            if (checkingShutdown) return;
            if (records.Count != 0 || bundleRecords.Count != 0 || instanceCaches.Count != 0 || sceneLoads.Count != 0 || sceneRecords.Count != 0) return;
            bundleQueue.Clear();
            draining.Remove(this);
            if (State != ResourceSystemState.ShuttingDown) return;
            State = ResourceSystemState.Closed;
            UnbindCoreResourceProvider();
            if (ReferenceEquals(authority, this)) authority = null;
            var completed = shutdowns.ToArray();
            shutdowns.Clear();
            foreach (var request in completed) request.SetSuccess(true);
            foreach (var request in completed) request.Notify();
        }

        private static void EndSession()
        {
            ending = true;
            var system = authority;
            authority = null;
            if (system == null) return;
            system.State = ResourceSystemState.Invalidated;
            system.UnbindCoreResourceProvider();
            draining.Add(system);
            var requests = new List<IResourceRequest>(system.ownedRequests);
            system.ownedRequests.Clear();
            foreach (var request in system.initialization) requests.Add(request);
            system.initialization.Clear();
            foreach (var request in system.shutdowns) requests.Add(request);
            system.shutdowns.Clear();
            // 整批先冻结终态和解绑等待，再释放/通知；观察者取消兄弟或 Dispose Scope 不改变退出原因。
            foreach (var request in requests) request.PrepareInvalidation();
            foreach (var request in requests)
                try { request.ReleaseInvalidatedResult(); }
                catch (Exception error) { Debug.LogException(error); }
            system.EndSceneSession();
            foreach (var cache in new List<InstanceCache>(system.instanceCaches)) cache.EndSession();
            foreach (var record in new List<AssetRecord>(system.records.Values))
                if (!record.Loading)
                {
                    if (record.InstancePins == 0) system.ReleaseRecord(record);
                    else record.LeaseCount = record.InstancePins;
                }
            foreach (var lease in new List<BundleLease>(system.publicBundleLeases)) lease.Dispose();
            system.CheckDrain();
            foreach (var request in requests) request.NotifyInvalidation();
        }

        private static void ResetSession() { EndSession(); ending = false; }

        public ResourceDiagnosticsSnapshot GetDiagnosticsSnapshot()
        {
            CheckThread();
            var producers = 0;
            var waiters = 0;
            var leases = 0;
            foreach (var record in records.Values)
            { if (record.Loading) producers++; waiters += record.Waiters.Count; leases += record.LeaseCount; }
            var roots = 0;
            var edges = 0;
            var queued = 0;
            foreach (var record in bundleRecords.Values)
            {
                roots += record.RootPins;
                edges += record.References - record.RootPins;
                if (record.State == BundleState.Queued) queued++;
            }
            return new ResourceDiagnosticsSnapshot(records.Count, producers, waiters, leases, bundleRecords.Count, roots, edges,
                runningBundles, queued, nativeBundleLoads, nativeBundleUnloads);
        }

        private static ResourceRequest<T> Fail<T>(ResourceRequest<T> request, ResourceError kind, string message,
            ResourceAddress? address = null, ResourceSourceId? source = null, Exception cause = null)
        {
            request.SetFailure(ResourceRequestStatus.Failed, new ResourceException(kind, message, cause, address, source));
            request.Notify();
            return request;
        }

        internal void CheckThread() => AssertThread();
        private static void AssertThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
                throw new InvalidOperationException("ResourceSystem must be used on the Unity main thread.");
        }
    }
}
