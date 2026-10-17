using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private readonly List<SceneLoad> sceneLoads = new();
        private readonly HashSet<SceneRecord> sceneRecords = new();
        private static readonly HashSet<ResourceSystem> sceneCleanup = new();
        private long nextSceneSequence;
        private bool pumpingScenes;

        private sealed class SceneLoad
        {
            internal SceneRequest Request;
            internal ResourceCatalog Catalog;
            internal ResourceAddress Address;
            internal string Path;
            internal string BundleId;
            internal IResourceSceneSource Source;
            internal ResourceRequest<BundleLease> Preparing;
            internal BundleLease Retention;
            internal bool Started;
            internal float Deadline;
            internal float ActivationTimeout;
            internal int CompletedFrame = -1;
            internal readonly HashSet<int> Before = new();
            internal readonly Dictionary<int, Scene> Observed = new();
        }

        /// <summary>按冻结目录加载真实 Scene。框架内部逐次关联 native 操作，不合并重复 Additive 意图。</summary>
        /// <param name="activationTimeoutSeconds">从 native 开始计时的有限门控期限，范围 (0, 300]；到期失败并放行激活，不承诺无限后台预加载。</param>
        public SceneRequest LoadSceneAsync(ResourceAddress address, LoadSceneMode mode = LoadSceneMode.Single,
            bool activateOnLoad = true, float activationTimeoutSeconds = 30)
        {
            CheckThread();
            var request = new SceneRequest(this, mode, ++nextSceneSequence, activateOnLoad);
            if (!IsAvailable) { Fail(request, ResourceError.SessionUnavailable, "ResourceSystem is not available.", address); return request; }
            if (IsProjectBlocked(address.ProjectId)) { Fail(request, ResourceError.Busy, "The project is inside its activation barrier.", address); return request; }
            if (mode != LoadSceneMode.Single && mode != LoadSceneMode.Additive ||
                float.IsNaN(activationTimeoutSeconds) || activationTimeoutSeconds <= 0 || activationTimeoutSeconds > 300)
            { Fail(request, ResourceError.InvalidConfiguration, "Scene mode and a finite activation deadline are required.", address); return request; }
            var load = new SceneLoad { Request = request, Address = address, ActivationTimeout = activationTimeoutSeconds };
            try
            {
                if (!address.IsValid || address.SubAsset != null || !catalogs.TryGetValue(address.ProjectId, out load.Catalog))
                    throw new ResourceException(ResourceError.AddressNotFound, "Unknown Scene project/address.");
                if (load.Catalog.Snapshot != null)
                {
                    var descriptor = load.Catalog.Snapshot.Document.Assets.SingleOrDefault(x => x.Kind == "Scene" &&
                        (x.Address == address.Path || x.Aliases.Contains(address.Path)));
                    if (descriptor == null) throw new ResourceException(ResourceError.AddressNotFound, "No Scene at this Catalog address.");
                    load.Path = descriptor.LoadName;
                    load.BundleId = descriptor.BundleId;
                    RequireHealthyBundle(load.Catalog.Snapshot, load.BundleId);
                }
                else
                {
                    var entry = load.Catalog.Resolve(address);
                    if (sources[entry.Location.Source] is not IResourceSceneSource source)
                        throw new ResourceException(ResourceError.Unsupported, "This source does not support Scene loading.");
                    load.Source = source;
                    load.Path = source.ResolveScenePath(entry);
                }
            }
            catch (Exception error)
            { Fail(request, error is ResourceException failure ? failure.Kind : ResourceError.AddressNotFound, "Cannot resolve Scene: " + address, address, cause: error); return request; }
            Track(request);
            request.Attach(() => CancelSceneLoad(load), () => request.Native?.progress ?? (load.Preparing?.Progress ?? 0) * 0.1f);
            sceneLoads.Add(load);
            RegisterSceneCleanup();
            PumpScenes();
            return request;
        }

        /// <summary>只读身份快照，包含已释放 caller Lease 的场景、取消 Single 的真实副作用和保守保活的歧义候选。</summary>
        public IReadOnlyList<SceneRecord> GetSceneRecords()
        {
            CheckThread();
            return sceneRecords.ToArray();
        }

        private void RegisterSceneCleanup()
        {
            if (sceneCleanup.Add(this))
            {
                SceneManager.sceneLoaded += OnResourceSceneLoaded;
                SceneManager.sceneUnloaded += OnResourceSceneUnloaded;
                InstallInstanceCleanup();
            }
        }

        private void UnregisterSceneCleanup()
        {
            if (sceneLoads.Count != 0 || sceneRecords.Count != 0) return;
            sceneCleanup.Remove(this);
            SceneManager.sceneLoaded -= OnResourceSceneLoaded;
            SceneManager.sceneUnloaded -= OnResourceSceneUnloaded;
            CheckDrain();
        }

        private void PumpScenes()
        {
            if (pumpingScenes) return;
            pumpingScenes = true;
            try
            {
                while (sceneLoads.Count != 0)
                {
                    var load = sceneLoads[0];
                    if (load.Started) break;
                    if (load.Request.IsDone || !IsSessionActive) { RemoveSceneLoad(load); continue; }
                    load.Started = true;
                    if (load.Source != null) StartNativeScene(load);
                    else
                    {
                        load.Preparing = AcquireBundleRequest(load.Address.ProjectId, load.BundleId, false, false,
                            new BundleLeaseOrigin(load.Address, sceneSequence: load.Request.LoadSequence));
                        load.Preparing.Completed += result =>
                        {
                            try
                            {
                                if (load.Request.IsDone) return;
                                load.Retention = result.TakeResult();
                                var paths = load.Retention.Bundle.GetAllScenePaths().Where(x => string.Equals(x, load.Path, StringComparison.OrdinalIgnoreCase)).ToArray();
                                if (paths.Length != 1) throw new ResourceException(ResourceError.LoadFailed, "Catalog Scene has no unique native Bundle path.");
                                load.Path = paths[0];
                                StartNativeScene(load);
                            }
                            catch (Exception error) { FailSceneLoad(load, error); }
                            finally
                            {
                                result.Dispose();
                                load.Preparing = null;
                                if (!load.Request.NativeStarted) { RemoveSceneLoad(load); PumpScenes(); }
                            }
                        };
                    }
                    if (sceneLoads.Contains(load)) break;
                }
            }
            finally { pumpingScenes = false; }
            UnregisterSceneCleanup();
        }

        private void StartNativeScene(SceneLoad load)
        {
            try
            {
                if (load.Retention != null) RequireHealthyBundle(load.Retention);
                if (load.Request.IsDone || !IsSessionActive) { RemoveSceneLoad(load); return; }
                for (var i = 0; i < SceneManager.sceneCount; i++) load.Before.Add(SceneManager.GetSceneAt(i).handle);
                // 准备 Bundle 期间已完成的外部 Scene 属于 Before，不能残留为本次 native 的新身份候选。
                load.Observed.Clear();
                var parameters = new LoadSceneParameters(load.Request.Mode);
                var native = load.Source == null ? SceneManager.LoadSceneAsync(load.Path, parameters) : load.Source.LoadSceneAsync(load.Path, parameters);
                if (native == null) throw new InvalidOperationException("Unity did not return a Scene AsyncOperation.");
                load.Request.Native = native;
                load.Request.NativeStarted = true;
                load.Deadline = Time.realtimeSinceStartup + load.ActivationTimeout;
                native.allowSceneActivation = load.Request.ActivationRequested;
                native.completed += _ => { load.CompletedFrame = Time.frameCount; load.Request.NativeCompleted = true; };
            }
            catch (Exception error) { FailSceneLoad(load, error); RemoveSceneLoad(load); }
        }

        private void CancelSceneLoad(SceneLoad load)
        {
            if (load.Request.NativeStarted)
            {
                load.Request.ActivationRequested = true;
                if (load.Request.Native != null) load.Request.Native.allowSceneActivation = true;
                return; // 不撤销 native、不释放 pending Scene 图。
            }
            load.Preparing?.Dispose();
            RemoveSceneLoad(load);
            PumpScenes();
        }

        private void FailSceneLoad(SceneLoad load, Exception error)
        {
            if (load.Request.IsDone) return;
            load.Request.SetFailure(IsSessionActive ? ResourceRequestStatus.Failed : ResourceRequestStatus.Invalidated,
                error as ResourceException ?? new ResourceException(ResourceError.LoadFailed, "Scene load failed: " + load.Address, error, load.Address));
            load.Request.Notify();
        }

        private void RemoveSceneLoad(SceneLoad load)
        {
            sceneLoads.Remove(load);
            load.Retention?.Dispose();
            load.Retention = null;
            UnregisterSceneCleanup();
        }

        private void OnResourceSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (sceneLoads.Count == 0) return;
            var load = sceneLoads[0];
            if (!load.Started || load.Before.Contains(scene.handle) || !string.Equals(scene.path, load.Path, StringComparison.OrdinalIgnoreCase)) return;
            load.Observed[scene.handle] = scene;
        }

        private void OnResourceSceneUnloaded(Scene scene)
        {
            foreach (var record in sceneRecords)
                if (record.Handle == scene.handle && record.UnloadedFrame < 0) record.UnloadedFrame = Time.frameCount;
        }

        private void CompleteSceneLoad(SceneLoad load)
        {
            // completed 后留一帧事件结算窗，不能取 GetSceneByName/Path 的第一项。
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!load.Before.Contains(scene.handle) && string.Equals(scene.path, load.Path, StringComparison.OrdinalIgnoreCase)) load.Observed[scene.handle] = scene;
            }
            var candidates = load.Observed.Values.ToArray();
            var ambiguous = candidates.Length > 1;
            var results = new List<SceneRecord>();
            foreach (var scene in candidates)
            {
                BundleLease retention = null;
                if (load.Retention != null)
                    retention = new BundleLease(bundleRecords[(load.Catalog.CatalogDigest, load.BundleId)], false);
                var record = new SceneRecord(this, scene, load.Catalog, load.Request, load.Address, scene.path,
                    ambiguous, retention);
                sceneRecords.Add(record);
                results.Add(record);
            }
            load.Request.Scenes = results.AsReadOnly();
            if (ambiguous) FailSceneLoad(load, new ResourceException(ResourceError.SceneIdentityAmbiguous,
                "Concurrent external same-path Scene loads cannot be attributed uniquely; candidates remain pinned until real unload.", address: load.Address));
            else if (results.Count != 1 || !results[0].IsLoaded)
                FailSceneLoad(load, new ResourceException(ResourceError.SceneUnloaded, "The native Scene completed without a surviving unique Scene.", address: load.Address));
            else if (!load.Request.IsDone)
            {
                try { if (load.Retention != null) RequireHealthyBundle(load.Retention); }
                catch (Exception error) { FailSceneLoad(load, error); }
                if (!load.Request.IsDone) load.Request.SetSuccess(new SceneLease(results[0]));
            }
            var abandonedAdditive = load.Request.Status != ResourceRequestStatus.Succeeded && load.Request.Mode == LoadSceneMode.Additive && !ambiguous;
            load.Request.Native = null;
            RemoveSceneLoad(load);
            if (abandonedAdditive)
                foreach (var record in results) TryUnloadScene(record);
            load.Request.Notify();
        }

        /// <summary>显式卸载当前 Lease 的真实身份。已 Dispose 的 Lease 拒绝，不按路径或复用 handle 查找替身。</summary>
        public ResourceRequest<bool> UnloadSceneAsync(SceneLease lease)
        {
            CheckThread();
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            return UnloadSceneAsync(lease.Record);
        }

        /// <summary>对独立 SceneRecord 显式卸载；用于 caller Lease 已释放或取消 Single 后的生命周期交接。</summary>
        public ResourceRequest<bool> UnloadSceneAsync(SceneRecord record)
        {
            CheckThread();
            var request = new ResourceRequest<bool>();
            if (!IsAvailable) return Fail(request, ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            if (record == null || !ReferenceEquals(record.Owner, this) || record.IsUnloaded || !sceneRecords.Contains(record))
                return Fail(request, ResourceError.SceneUnloaded, "The Scene identity is stale or belongs to another authority.");
            if (record.IsAmbiguous) return Fail(request, ResourceError.SceneIdentityAmbiguous, "An ambiguous external Scene candidate must be unloaded by its actual owner.");
            var error = TryUnloadScene(record);
            if (error != null) return Fail(request, error.Kind, error.Message, cause: error);
            Track(request);
            record.UnloadWaiters.Add(request);
            request.Attach(() => record.UnloadWaiters.Remove(request), () => record.UnloadOperation?.progress ?? 0);
            return request;
        }

        private ResourceException TryUnloadScene(SceneRecord record)
        {
            if (record.UnloadOperation != null || !record.IsLoaded) return null;
            if (sceneLoads.Any(x => x.Request.Native != null && !x.Request.ActivationRequested))
                return new ResourceException(ResourceError.Busy, "Activate or cancel the gated Scene before unloading.");
            var loaded = 0;
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isLoaded) loaded++;
            if (loaded <= 1) return new ResourceException(ResourceError.Busy, "Unity cannot unload the last Scene; load a neutral Scene first. The Scene remains pinned.");
            try
            {
                record.UnloadOperation = SceneManager.UnloadSceneAsync(record.Scene);
                if (record.UnloadOperation == null) return new ResourceException(ResourceError.LoadFailed, "Unity refused the Scene unload; ownership is retained.");
            }
            catch (Exception error) { return new ResourceException(ResourceError.LoadFailed, "Unity Scene unload failed; ownership is retained.", error); }
            return null;
        }

        private void CollectScene(SceneRecord record)
        {
            record.IsUnloaded = true;
            sceneRecords.Remove(record);
            var waiters = record.UnloadWaiters.ToArray();
            record.UnloadWaiters.Clear();
            foreach (var request in waiters) request.SetSuccess(true);
            record.UnloadOperation = null;
            record.Retention?.Dispose();
            record.Retention = null;
            UnregisterSceneCleanup();
            foreach (var request in waiters) request.Notify();
        }

        private static void TickSceneCleanup()
        {
            foreach (var system in sceneCleanup.ToArray())
            {
                if (system.sceneLoads.Count != 0)
                {
                    var load = system.sceneLoads[0];
                    var native = load.Request.Native;
                    if (native != null)
                    {
                        if (!load.Request.ActivationRequested && Time.realtimeSinceStartup >= load.Deadline)
                        {
                            load.Request.ActivationRequested = true;
                            native.allowSceneActivation = true;
                            system.FailSceneLoad(load, new ResourceException(ResourceError.ActivationTimedOut,
                                "Scene activation deadline expired; native activation was released. Single replacement cannot be rolled back.", address: load.Address));
                        }
                        if (native.isDone && load.CompletedFrame < 0) { load.CompletedFrame = Time.frameCount; load.Request.NativeCompleted = true; }
                        if (load.CompletedFrame >= 0 && (!Application.isPlaying || Time.frameCount > load.CompletedFrame)) system.CompleteSceneLoad(load);
                    }
                }
                foreach (var record in system.sceneRecords.ToArray())
                {
                    if (record.IsLoaded) continue;
                    if (record.UnloadedFrame < 0) record.UnloadedFrame = Time.frameCount;
                    if (record.UnloadOperation != null && !record.UnloadOperation.isDone) continue;
                    if (Application.isPlaying && Time.frameCount <= record.UnloadedFrame) continue;
                    system.CollectScene(record);
                }
                system.PumpScenes();
                system.UnregisterSceneCleanup();
            }
        }

        private void EndSceneSession()
        {
            foreach (var load in sceneLoads.ToArray()) CancelSceneLoad(load);
            // 不卸载歧义候选或其他 owner 的 Scene；最后场景等待 Unity 真正退出卸载，不制造替身场景。
            foreach (var record in sceneRecords.ToArray()) if (!record.IsAmbiguous) TryUnloadScene(record);
        }
    }
}
