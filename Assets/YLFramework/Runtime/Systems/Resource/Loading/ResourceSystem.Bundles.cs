using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private const int BundleConcurrency = 4;
        private readonly Dictionary<(string, string), BundleRecord> bundleRecords = new();
        private readonly HashSet<BundleLease> publicBundleLeases = new();
        private readonly Queue<BundleRecord> bundleQueue = new();
        private int runningBundles;
        private bool pumpingBundles;
        private bool pumpAgain;
        private int nativeBundleLoads;
        private int nativeBundleUnloads;

        internal enum BundleState { Waiting, Queued, Loading, Ready, Failed, Released }
        internal sealed class BundleWaiter
        {
            internal Action Prepare;
            internal Action Notify;
        }
        internal sealed class BundleRecord
        {
            internal readonly ResourceSystem Owner;
            internal readonly ResourceLocalSnapshot Snapshot;
            internal readonly ResourceCatalogBundle Descriptor;
            internal readonly List<BundleRecord> Dependencies = new();
            internal BundleState State;
            internal AssetBundle Bundle;
            internal AssetBundleCreateRequest Native;
            internal FileStream Input;
            internal ResourceException Error;
            internal int References;
            internal int RootPins;
            internal Action<AsyncOperation> NativeCompletion;
            internal readonly List<BundleWaiter> Waiters = new();
            internal bool IsDone => State == BundleState.Ready || State == BundleState.Failed;
            internal BundleRecord(ResourceSystem owner, ResourceLocalSnapshot snapshot, ResourceCatalogBundle descriptor)
            { Owner = owner; Snapshot = snapshot; Descriptor = descriptor; }
        }

        /// <summary>取得明确 Catalog BundleId 的低层图租约，不加载 Scene 或创建实例。</summary>
        public BundleLease AcquireBundle(string projectId, string bundleId)
        {
            using var request = AcquireBundleRequest(projectId, bundleId, true, true);
            return request.TakeResult();
        }
        public ResourceRequest<BundleLease> AcquireBundleAsync(string projectId, string bundleId) => AcquireBundleRequest(projectId, bundleId, false, true);

        internal ResourceRequest<BundleLease> AcquireBundleRequest(string projectId, string bundleId, bool synchronous, bool publicLease)
        {
            CheckThread();
            var request = new ResourceRequest<BundleLease>(value => value.Dispose());
            if (!IsAvailable) return Fail(request, ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            if (IsProjectBlocked(projectId)) return Fail(request, ResourceError.Busy, "The project is inside its activation barrier.");
            if (projectId == null || !catalogs.TryGetValue(projectId, out var catalog) || catalog.Snapshot == null ||
                bundleId == null || !catalog.Snapshot.Bundles.ContainsKey(bundleId))
                return Fail(request, ResourceError.AddressNotFound, "Unknown mounted project/Bundle identity.");
            var snapshot = catalog.Snapshot;
            var closure = new List<ResourceCatalogBundle>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(bundleId);
            while (pending.Count != 0)
            {
                var id = pending.Pop();
                if (!visited.Add(id)) continue;
                var descriptor = snapshot.Bundles[id];
                closure.Add(descriptor);
                foreach (var dependency in descriptor.Dependencies) pending.Push(dependency);
            }
            if (synchronous && closure.Any(x => bundleRecords.TryGetValue((snapshot.Document.CatalogDigest, x.LogicalId), out var existing) && !existing.IsDone))
                return Fail(request, ResourceError.InProgress, "The Bundle graph is already in flight; use AcquireBundleAsync.");

            // 先建立全部节点和依赖入边，再运行任何可能即时失败/通知的 IO。
            var added = new List<BundleRecord>();
            foreach (var descriptor in closure)
            {
                var key = (snapshot.Document.CatalogDigest, descriptor.LogicalId);
                if (bundleRecords.ContainsKey(key)) continue;
                var value = new BundleRecord(this, snapshot, descriptor);
                bundleRecords.Add(key, value);
                added.Add(value);
            }
            foreach (var value in added)
                foreach (var dependency in value.Descriptor.Dependencies)
                {
                    var child = bundleRecords[(snapshot.Document.CatalogDigest, dependency)];
                    child.References++;
                    value.Dependencies.Add(child);
                }
            var record = bundleRecords[(snapshot.Document.CatalogDigest, bundleId)];
            var lease = new BundleLease(record, publicLease);
            if (publicLease) publicBundleLeases.Add(lease);
            Track(request);
            var waiter = new BundleWaiter();
            waiter.Prepare = () =>
            {
                if (request.IsDone) return;
                if (record.State == BundleState.Ready && IsSessionActive) request.SetSuccess(lease);
                else
                {
                    request.SetFailure(IsSessionActive ? ResourceRequestStatus.Failed : ResourceRequestStatus.Invalidated,
                        record.Error ?? new ResourceException(ResourceError.SessionUnavailable, "The Bundle producing session ended."));
                    lease.Dispose();
                }
            };
            waiter.Notify = request.Notify;
            request.Attach(() => { record.Waiters.Remove(waiter); lease.Dispose(); }, () => record.Native?.progress ?? (record.IsDone ? 1 : 0));
            if (record.IsDone) { waiter.Prepare(); waiter.Notify(); }
            else
            {
                record.Waiters.Add(waiter);
                if (synchronous) DriveSynchronousBundles(added);
                else PumpBundles();
            }
            return request;
        }

        private void DriveSynchronousBundles(List<BundleRecord> added)
        {
            // 只驱动本次冷图，不等待/接管异步队列；观察者重入也复用同一 records 和 native 权威。
            var pending = new HashSet<BundleRecord>(added);
            while (pending.Count != 0)
            {
                var advanced = false;
                foreach (var record in pending.ToArray())
                {
                    if (record.State != BundleState.Released && !record.Dependencies.All(x => x.IsDone)) continue;
                    pending.Remove(record);
                    advanced = true;
                    if (record.State == BundleState.Released) continue;
                    var failed = record.Dependencies.FirstOrDefault(x => x.State == BundleState.Failed);
                    if (failed != null) FinishBundle(record, null, DependencyFailure(record, failed));
                    else StartBundle(record, true);
                }
                if (!advanced) throw new InvalidOperationException("A validated cold Bundle DAG could not make synchronous progress.");
            }
            CheckDrain();
        }

        private static ResourceException DependencyFailure(BundleRecord record, BundleRecord failed) => new(ResourceError.LoadFailed,
            $"Bundle '{record.Descriptor.LogicalId}' dependency '{failed.Descriptor.LogicalId}' failed.", failed.Error);

        private void PumpBundles()
        {
            if (pumpingBundles) { pumpAgain = true; return; }
            pumpingBundles = true;
            try
            {
                do
                {
                    pumpAgain = false;
                    foreach (var record in bundleRecords.Values.ToArray())
                    {
                        if (record.State != BundleState.Waiting) continue;
                        var failed = record.Dependencies.FirstOrDefault(x => x.State == BundleState.Failed);
                        if (failed != null)
                        {
                            FinishBundle(record, null, DependencyFailure(record, failed));
                            pumpAgain = true;
                        }
                        else if (record.Dependencies.All(x => x.State == BundleState.Ready))
                        { record.State = BundleState.Queued; bundleQueue.Enqueue(record); }
                    }
                    while (bundleQueue.Count != 0 && runningBundles < BundleConcurrency)
                    {
                        var record = bundleQueue.Dequeue();
                        if (record.State != BundleState.Queued) continue;
                        StartBundle(record, false);
                    }
                } while (pumpAgain);
            }
            finally { pumpingBundles = false; }
            CheckDrain();
        }

        private void StartBundle(BundleRecord record, bool synchronous)
        {
            if (!IsSessionActive)
            { FinishBundle(record, null, new ResourceException(ResourceError.SessionUnavailable, "The Bundle producing session ended.")); pumpAgain = true; return; }
            try
            {
                if (AssetBundle.GetAllLoadedAssetBundles().Any(x => x && x.name == record.Descriptor.NativeName))
                    throw new ResourceException(ResourceError.Busy, "The native Bundle identity is owned outside this graph: " + record.Descriptor.NativeName);
                record.Input = record.Snapshot.OpenVerified(record.Descriptor);
                var path = Path.Combine(record.Snapshot.DirectoryPath, record.Descriptor.RelativeFile);
                record.State = BundleState.Loading;
                if (synchronous)
                {
                    nativeBundleLoads++;
                    FinishBundle(record, AssetBundle.LoadFromFile(path), null);
                    pumpAgain = true;
                }
                else
                {
                    record.Native = AssetBundle.LoadFromFileAsync(path);
                    if (record.Native == null) throw new InvalidOperationException("Unity did not return an AssetBundleCreateRequest.");
                    nativeBundleLoads++;
                    runningBundles++;
                    record.NativeCompletion = _ => CompleteNativeBundle(record);
                    record.Native.completed += record.NativeCompletion;
                    if (record.Native.isDone) CompleteNativeBundle(record);
                }
            }
            catch (Exception error)
            {
                FinishBundle(record, null, error);
                pumpAgain = true;
            }
        }

        private void CompleteNativeBundle(BundleRecord record)
        {
            if (record.Native == null) return;
            record.Native.completed -= record.NativeCompletion;
            record.NativeCompletion = null;
            AssetBundle bundle = null;
            Exception error = null;
            try { bundle = record.Native.assetBundle; }
            catch (Exception cause) { error = cause; }
            record.Native = null;
            runningBundles--;
            FinishBundle(record, bundle, error);
            PumpBundles();
        }

        private void FinishBundle(BundleRecord record, AssetBundle bundle, Exception cause)
        {
            record.Bundle = bundle;
            if (cause == null && (!bundle || bundle.name != record.Descriptor.NativeName || bundle.isStreamedSceneAssetBundle != record.Descriptor.IsScene))
                cause = new InvalidDataException("Unity Bundle result/native identity mismatch: " + record.Descriptor.LogicalId);
            if (cause == null && !IsSessionActive) cause = new ResourceException(ResourceError.SessionUnavailable, "The Bundle producing session ended.");
            record.Error = cause as ResourceException ?? (cause == null ? null : new ResourceException(ResourceError.LoadFailed,
                $"Bundle '{record.Descriptor.LogicalId}' failed in '{record.Snapshot.Document.ProjectId}'/{record.Snapshot.Document.CatalogDigest}.", cause,
                source: ResourceSourceId.AssetBundle));
            record.State = record.Error == null ? BundleState.Ready : BundleState.Failed;
            var waiters = record.Waiters.ToArray();
            record.Waiters.Clear();
            foreach (var waiter in waiters) waiter.Prepare();
            CollectBundle(record);
            foreach (var waiter in waiters) waiter.Notify();
        }

        internal void ReleaseBundle(BundleLease lease, BundleRecord record)
        {
            if (lease.Public) publicBundleLeases.Remove(lease);
            record.RootPins--;
            record.References--;
            CollectBundle(record);
            CheckDrain();
        }

        private void CollectBundle(BundleRecord first)
        {
            var collect = new Stack<BundleRecord>();
            collect.Push(first);
            while (collect.Count != 0)
            {
                var record = collect.Pop();
                if (record.References != 0 || record.Native != null || record.State == BundleState.Released) continue;
                bundleRecords.Remove((record.Snapshot.Document.CatalogDigest, record.Descriptor.LogicalId));
                record.State = BundleState.Released;
                // 先卸载依赖者，再归还依赖入边；不能用 Unload(false) 留下无主对象。
                if (record.Bundle) { record.Bundle.Unload(true); nativeBundleUnloads++; }
                record.Bundle = null;
                record.Input?.Dispose();
                record.Input = null;
                foreach (var dependency in record.Dependencies) { dependency.References--; collect.Push(dependency); }
                record.Dependencies.Clear();
            }
        }

        private static void ValidateSnapshotPlatform(ResourceLocalSnapshot snapshot)
        {
#if UNITY_EDITOR
            var platform = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
#else
            var platform = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer => IntPtr.Size == 8 ? "StandaloneWindows64" : "StandaloneWindows",
                RuntimePlatform.LinuxPlayer => "StandaloneLinux64", RuntimePlatform.OSXPlayer => "StandaloneOSX",
                RuntimePlatform.Android => "Android", RuntimePlatform.IPhonePlayer => "iOS", _ => null
            };
#endif
            if (platform == null || snapshot.Document.Platform != platform || snapshot.Document.UnityCompatibility != Application.unityVersion)
                throw new ResourceException(ResourceError.Unsupported, "The explicit local snapshot does not match this Unity runtime/platform.");
        }
    }
}
