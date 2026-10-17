using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Core.Pool;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{
    public enum InstanceCachePolicy { SceneScoped, Persistent }

    /// <summary>
    /// Resource 拥有 Prefab pin 和实例责任，Foundation 桶拥有 FIFO/容量/复用机制。
    /// Dispose 退休缓存但不销毁借出对象；晚归还销毁，全部实例真实死亡后才释放 pin/Data。
    /// </summary>
    public sealed partial class InstanceCache : IDisposable
    {
        internal sealed class Entry
        {
            internal GameObject Object;
            internal InstanceLease Checkout;
            internal bool Destroying;
        }

        internal readonly ResourceSystem Owner;
        internal readonly ResourceSystem.AssetRecord AssetRecord;
        internal readonly Scene ScopeScene;
        private readonly AssetLease<GameObject> prefab;
        private readonly SafeObjectPool<GameObjectPoolData> dataOwner;
        private GameObjectPoolData data;
        private readonly List<Entry> instances = new();
        private int pruneIndex;
        private int operations;
        private bool released;
        private bool committed;
        public InstanceCachePolicy Policy { get; }
        public bool IsRetired { get; private set; }
        // 显式取得缓存或成功交付后才保留空桶；失败/交付前取消不能留下无主的creation pin。
        internal void Commit() => committed = true;

        internal InstanceCache(ResourceSystem owner, AssetLease<GameObject> pin, InstanceCachePolicy policy, Scene scene, Transform root)
        {
            Owner = owner; prefab = pin; AssetRecord = pin.Record; Policy = policy; ScopeScene = scene;
            dataOwner = SafeObjectPool<GameObjectPoolData>.Instance;
            data = dataOwner.Allocate();
            try { data.Init("Resource Prefab Cache", root, 32); }
            catch { dataOwner.Discard(data); data = null; throw; }
        }

        /// <summary>空闲上限，默认32；-1无限，0不缓存。缩容由 Foundation 退休多余实例，pin仍等真实销毁。</summary>
        public int MaxCapacity
        {
            get { CheckOpen(); return data.MaxCapacity; }
            set
            {
                CheckOpen();
                operations++;
                try { data.MaxCapacity = value; }
                finally { operations--; Prune(8); }
            }
        }
        public int CachedCount { get { Owner.CheckThread(); return IsRetired ? 0 : data.Count; } }
        internal bool HasLiveOwners
        {
            get
            {
                if (operations != 0) return true;
                foreach (var entry in instances) if (entry.Object && entry.Checkout != null) return true;
                return false;
            }
        }

        private void CheckOpen()
        {
            Owner.CheckThread();
            if (!Owner.IsAvailable) throw new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            if (Owner.IsProjectBlocked(AssetRecord.Entry.Address.ProjectId)) throw new ResourceException(ResourceError.Busy, "The project is inside its activation barrier.");
            if (IsRetired) throw new ObjectDisposedException(nameof(InstanceCache));
            if (!data.RootTransform) { Dispose(); throw new ObjectDisposedException(nameof(InstanceCache)); }
            if (Policy == InstanceCachePolicy.SceneScoped && (!ScopeScene.IsValid() || !ScopeScene.isLoaded))
                throw new ObjectDisposedException(nameof(InstanceCache));
        }

        internal static Scene TargetScene(Transform parent, Scene fallback)
        {
            if (!ReferenceEquals(parent, null) && !parent) throw new ArgumentException("The instance parent was destroyed.", nameof(parent));
            if (parent && GameObjectPoolData.IsCacheHierarchy(parent)) throw new ArgumentException("Instances cannot be checked out below a cache root.", nameof(parent));
            var scene = parent ? parent.gameObject.scene : fallback;
            if (!scene.IsValid() || !scene.isLoaded) throw new ArgumentException("The target scene must still be loaded.");
            return scene;
        }

        /// <summary>从本缓存创建/取得。SceneScoped 无父节点时使用捕获的 scope scene；Persistent 使用当前活动场景。</summary>
        public InstanceLease Instantiate(Transform parent = null) => Instantiate(parent,
            Policy == InstanceCachePolicy.SceneScoped ? ScopeScene : SceneManager.GetActiveScene());

        internal InstanceLease Instantiate(Transform parent, Scene scene)
        {
            CheckOpen();
            Owner.RequireContentAdmission(AssetRecord.Entry);
            scene = TargetScene(parent, scene);
            operations++;
            Entry entry = null;
            try
            {
                var reused = data.Allocate(parent, scene);
                if (IsRetired || !Owner.IsAvailable) throw new ResourceException(ResourceError.SessionUnavailable, "The cache retired during checkout.");
                CheckOpen();
                if (reused)
                {
                    entry = instances.Find(x => ReferenceEquals(x.Object, reused));
                    if (entry == null) throw new InvalidOperationException("The Foundation bucket returned an unowned instance.");
                }
                else
                {
                    entry = CreateInactive();
                    var obj = entry.Object;
                    obj.transform.SetParent(parent, false);
                    if (!parent) SceneManager.MoveGameObjectToScene(obj, scene);
                    if (!IsRetired && Owner.IsAvailable && obj) obj.SetActive(true);
                }
                if (!entry.Object || IsRetired || !Owner.IsAvailable || !entry.Object.activeSelf || entry.Object.transform.parent != parent ||
                    GameObjectPoolData.IsCacheHierarchy(entry.Object.transform.parent))
                    throw new ResourceException(ResourceError.LoadFailed, "A lifecycle callback invalidated the instance checkout.");
                Owner.RequireContentAdmission(AssetRecord.Entry);
                return new InstanceLease(this, entry);
            }
            catch
            {
                if (entry != null) Destroy(entry);
                throw;
            }
            finally { operations--; Prune(8); }
        }

        private Entry CreateInactive()
        {
            CheckOpen();
            // pin及操作责任在 Instantiate 之前建立；失活根避免 Awake/OnEnable 早于实例登记。
            var obj = Object.Instantiate(prefab.Asset, data.RootTransform, false);
            var entry = new Entry { Object = obj };
            instances.Add(entry);
            if (!obj) throw new ResourceException(ResourceError.LoadFailed, "Prefab creation did not produce a live instance.");
            obj.SetActive(false);
            return entry;
        }

        /// <summary>补至目标空闲数量，逐个登记从未激活的实例；不使用不可观察实例集合的隐式预热。</summary>
        public int Prewarm(int target)
        {
            CheckOpen();
            Owner.RequireContentAdmission(AssetRecord.Entry);
            if (target < 0 || (data.MaxCapacity >= 0 && target > data.MaxCapacity)) throw new ArgumentOutOfRangeException(nameof(target));
            var added = 0;
            var missing = Math.Max(0, target - data.Count);
            operations++;
            try
            {
                for (var i = 0; i < missing && data.Count < target; i++)
                {
                    CheckOpen();
                    var entry = CreateInactive();
                    try { Owner.RequireContentAdmission(AssetRecord.Entry); if (data.Recycle(entry.Object)) added++; }
                    catch { Destroy(entry); throw; }
                }
                return added;
            }
            finally { operations--; Prune(8); }
        }

        internal void Return(Entry entry, bool discard)
        {
            operations++;
            try
            {
                if (!entry.Object) return;
                if (discard || IsRetired || !Owner.IsSessionActive || entry.Destroying) Destroy(entry);
                else
                    try { data.Recycle(entry.Object); }
                    catch { Destroy(entry); throw; }
            }
            finally { operations--; Prune(8); }
        }

        private static void Destroy(Entry entry)
        {
            if (entry.Destroying) return;
            entry.Destroying = true;
            if (!entry.Object) return;
            try { entry.Object.SetActive(false); }
            finally { if (entry.Object) Object.Destroy(entry.Object); }
        }

        public void Dispose()
        {
            Owner.CheckThread();
            if (IsRetired) return;
            IsRetired = true;
            Owner.RetireInstanceCache(this);
            operations++;
            try { data.OnRecycle(); }
            finally { operations--; Prune(8); }
        }

        internal void EndSession()
        {
            Dispose();
            operations++;
            try { foreach (var entry in instances.ToArray()) Destroy(entry); }
            finally { operations--; Prune(8); }
        }

        internal void Prune(int budget)
        {
            if (released) return;
            if (!IsRetired && (!data.RootTransform || (!committed && !HasLiveOwners))) { Dispose(); return; }
            for (var i = 0; i < budget && instances.Count != 0; i++)
            {
                if (pruneIndex >= instances.Count) pruneIndex = 0;
                var entry = instances[pruneIndex];
                if (entry.Object) { pruneIndex++; continue; }
                entry.Checkout = null;
                instances[pruneIndex] = instances[instances.Count - 1];
                instances.RemoveAt(instances.Count - 1);
            }
            if (!IsRetired || operations != 0 || instances.Count != 0) return;
            released = true;
            var owned = data;
            data = null;
            try { dataOwner.Recycle(owned); }
            finally { Owner.ReleaseInstanceCache(this); prefab.Dispose(); }
        }
    }
}
