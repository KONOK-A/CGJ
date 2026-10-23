using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private readonly Dictionary<(AssetRecord, InstanceCachePolicy, int), InstanceCache> _openInstanceCaches = new();
        private readonly HashSet<InstanceCache> _instanceCaches = new();
        private Transform _instanceRoot;
        private static readonly List<InstanceCache> _instanceCleanup = new();
        private static int _instanceCleanupIndex;
        private static bool _isPruningInstances;

        /// <summary>默认按目标场景缓存。返回独立实例责任，Dispose/Return回桶，Discard销毁。</summary>
        public InstanceLease Instantiate(ResourceAddress address, Transform parent = null)
        {
            using var prefab = Acquire<GameObject>(address);
            return Instantiate(prefab, parent);
        }

        /// <summary>不消费输入 Lease；实例缓存取得自己的 Prefab pin。裸 Prefab/Attach 不由此入口纳管。</summary>
        public InstanceLease Instantiate(AssetLease<GameObject> prefab, Transform parent = null)
            => Instantiate(prefab, parent, SceneManager.GetActiveScene());

        internal InstanceLease Instantiate(AssetLease<GameObject> prefab, Transform parent, Scene targetScene)
        {
            CheckThread();
            var scene = InstanceCache.TargetScene(parent, targetScene);
            var cache = GetInstanceCacheCore(prefab, InstanceCachePolicy.SceneScoped, scene);
            var lease = cache.Instantiate(parent, scene);
            cache.Commit();
            return lease;
        }

        public ResourceRequest<InstanceLease> InstantiateAsync(ResourceAddress address, Transform parent = null)
        {
            CheckThread();
            var request = new ResourceRequest<InstanceLease>(value => value.Dispose());
            if (!IsAvailable)
            {
                return Fail(request, ResourceError.SessionUnavailable, "ResourceSystem is not available.", address);
            }

            Scene scene;
            try
            {
                scene = InstanceCache.TargetScene(parent, SceneManager.GetActiveScene());
            }
            catch (ArgumentException error)
            {
                return Fail(request, ResourceError.LoadFailed, "Invalid instance destination.", address, cause: error);
            }
            ResourceRequest<AssetLease<GameObject>> child = null;
            Track(request);
            request.Attach(() => child?.Dispose(), () => child?.Progress ?? 0);
            child = AcquireAsync<GameObject>(address);
            if (request.IsDone)
            {
                child.Dispose();
                return request;
            }
            child.Completed += result =>
            {
                if (request.IsDone)
                {
                    result.Dispose();
                    return;
                }
                try
                {
                    using var prefab = result.TakeResult();
                    var target = InstanceCache.TargetScene(parent, scene);
                    var cache = GetInstanceCacheCore(prefab, InstanceCachePolicy.SceneScoped, target);
                    var lease = cache.Instantiate(parent, target);
                    if (request.IsDone)
                    {
                        lease.Discard();
                    }
                    else
                    {
                        cache.Commit();
                        request.SetSuccess(lease);
                    }
                }
                catch (Exception cause)
                {
                    if (!request.IsDone)
                    {
                        request.SetFailure(ResourceRequestStatus.Failed, cause as ResourceException ??
                        new ResourceException(ResourceError.LoadFailed, "Prefab instance creation failed.", cause, address));
                    }
                }
                finally
                {
                    result.Dispose();
                }
                request.Notify();
            };
            return request;
        }

        /// <summary>取得本 authority 的正式缓存。SceneScoped 默认绑定当前活动场景，Persistent仅缓存跨场景，不自动DDOL借出对象。</summary>
        public InstanceCache GetInstanceCache(ResourceAddress address, InstanceCachePolicy policy = InstanceCachePolicy.SceneScoped, Scene scene = default)
        {
            using var prefab = Acquire<GameObject>(address);
            return GetInstanceCache(prefab, policy, scene);
        }

        public InstanceCache GetInstanceCache(AssetLease<GameObject> prefab, InstanceCachePolicy policy = InstanceCachePolicy.SceneScoped, Scene scene = default)
        {
            var cache = GetInstanceCacheCore(prefab, policy, scene);
            cache.Commit();
            return cache;
        }

        private InstanceCache GetInstanceCacheCore(AssetLease<GameObject> prefab, InstanceCachePolicy policy, Scene scene)
        {
            CheckThread();
            if (!IsAvailable)
            {
                throw new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            }

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            RequireContentAdmission(prefab.Record.Entry);
            if (IsProjectBlocked(prefab.Address.ProjectId))
            {
                throw new ResourceException(ResourceError.Busy, "The project is inside its activation barrier.");
            }

            var asset = prefab.Asset;
            if (!ReferenceEquals(prefab.Record.Owner, this) || asset.scene.IsValid())
            {
                throw new ArgumentException("An asset Prefab lease from this authority is required.", nameof(prefab));
            }

            if (policy != InstanceCachePolicy.SceneScoped && policy != InstanceCachePolicy.Persistent)
            {
                throw new ArgumentOutOfRangeException(nameof(policy));
            }

            if (policy == InstanceCachePolicy.SceneScoped)
            {
                if (scene == default)
                {
                    scene = SceneManager.GetActiveScene();
                }

                InstanceCache.TargetScene(null, scene);
            }
            var key = (prefab.Record, policy, policy == InstanceCachePolicy.Persistent ? 0 : scene.handle);
            if (_openInstanceCaches.TryGetValue(key, out var cache))
            {
                cache.Prune(8);
                if (!cache.IsRetired)
                {
                    return cache;
                }
            }
            var pin = new AssetLease<GameObject>(prefab.Record, prefab.Address, true);
            try
            {
                if (!_instanceRoot)
                {
                    _instanceRoot = new GameObject("YL Resource Instance Cache").transform;
                    UnityEngine.Object.DontDestroyOnLoad(_instanceRoot.gameObject);
                }
                cache = new InstanceCache(this, pin, policy, scene, _instanceRoot);
                _openInstanceCaches.Add(key, cache);
                if (_instanceCaches.Count == 0)
                {
                    SceneManager.sceneUnloaded += OnInstanceSceneUnloaded;
                }

                _instanceCaches.Add(cache);
                _instanceCleanup.Add(cache);
                InstallInstanceCleanup();
                return cache;
            }
            catch
            {
                pin.Dispose();
                throw;
            }
        }

        /// <summary>退休当前缓存快照；不销毁借出实例。回调新建的缓存不属于本次快照。</summary>
        public void ClearInstanceCaches()
        {
            CheckThread();
            foreach (var cache in new List<InstanceCache>(_openInstanceCaches.Values))
            {
                cache.Dispose();
            }
        }

        private void OnInstanceSceneUnloaded(Scene scene)
        {
            foreach (var cache in new List<InstanceCache>(_openInstanceCaches.Values))
            {
                if (cache.Policy == InstanceCachePolicy.SceneScoped && cache.ScopeScene == scene)
                {
                    cache.Dispose();
                }
            }
        }

        internal void RetireInstanceCache(InstanceCache cache)
        {
            var key = (cache.AssetRecord, cache.Policy, cache.Policy == InstanceCachePolicy.Persistent ? 0 : cache.ScopeScene.handle);
            if (_openInstanceCaches.TryGetValue(key, out var current) && ReferenceEquals(current, cache))
            {
                _openInstanceCaches.Remove(key);
            }
        }

        internal void ReleaseInstanceCache(InstanceCache cache)
        {
            _instanceCaches.Remove(cache);
            _instanceCleanup.Remove(cache);
            if (_instanceCaches.Count != 0)
            {
                return;
            }

            SceneManager.sceneUnloaded -= OnInstanceSceneUnloaded;
            if (_instanceRoot)
            {
                UnityEngine.Object.Destroy(_instanceRoot.gameObject);
            }

            _instanceRoot = null;
        }

        // 无协程/MonoBehaviour宿主，退出不新建对象；每帧最多8个桶、每桶8项fake-null检查。
        private static void TickInstanceCleanup()
        {
            if (_isPruningInstances)
            {
                return;
            }

            _isPruningInstances = true;
            try
            {
                TickRemoteUpdates();
                TickContentWork();
                TickSceneCleanup();
                _authority?.TrimImageCache();
                var count = Math.Min(8, _instanceCleanup.Count);
                for (var i = 0; i < count && _instanceCleanup.Count != 0; i++)
                {
                    if (_instanceCleanupIndex >= _instanceCleanup.Count)
                    {
                        _instanceCleanupIndex = 0;
                    }

                    var cache = _instanceCleanup[_instanceCleanupIndex];
                    cache.Prune(8);
                    if (_instanceCleanupIndex < _instanceCleanup.Count && ReferenceEquals(_instanceCleanup[_instanceCleanupIndex], cache))
                    {
                        _instanceCleanupIndex++;
                    }
                }
            }
            finally
            {
                _isPruningInstances = false;
            }
        }

        private static void InstallInstanceCleanup()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var children = new List<PlayerLoopSystem>(loop.subSystemList);
            if (children.Exists(x => x.type == typeof(InstanceCache)))
            {
                return;
            }

            children.Add(new PlayerLoopSystem { type = typeof(InstanceCache), updateDelegate = TickInstanceCleanup });
            loop.subSystemList = children.ToArray();
            PlayerLoop.SetPlayerLoop(loop);
        }
    }
}
