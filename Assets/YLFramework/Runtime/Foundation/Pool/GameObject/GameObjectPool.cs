using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 按字符串 key 复用场景实例的主线程单例；空池 Allocate 返回 null，不自动加载或创建。
    /// 缓存跟随 DontDestroyOnLoad 宿主；不拥有 Prefab/AssetBundle 资产及其引用计数。
    /// </summary>
    [MonoSingletonPath("YLFramework/Pool/GameObjectPool")]
    public class GameObjectPool : MonoBehaviour, ISingleton
    {
        public static GameObjectPool Instance => MonoSingletonProperty<GameObjectPool>.Instance;

        private readonly Dictionary<string, GameObjectPoolData> _pools = new();
        private readonly Dictionary<string, bool> _creating = new();
        private readonly object _identity = new();
        private static readonly List<WeakReference<GameObjectPool>> _sessionHosts = new();
        private SafeObjectPool<GameObjectPoolData> _dataPool;
        private bool _isDestroyed;
        private bool _isResetting;

        static GameObjectPool()
        {
            SingletonSessionState.RegisterSessionReset(ResetHosts);
        }

        private static void ResetHosts()
        {
            List<Exception> errors = null;
            for (var i = _sessionHosts.Count - 1; i >= 0; i--)
            {
                if (!_sessionHosts[i].TryGetTarget(out var host) || !host)
                {
                    _sessionHosts.RemoveAt(i);
                    continue;
                }
                host._isResetting = true;
                try
                {
                    host.ClearAll();
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
                finally
                {
                    host._dataPool = null;
                    host._isResetting = false;
                }
            }
            if (errors != null)
            {
                throw new AggregateException(errors);
            }
        }

        public void OnSingletonInit()
        {
        }

        private void Awake()
        {
            // 路径根被预先建立或宿主被直接收养时，也保证缓存跨场景存活。
            DontDestroyOnLoad(transform.root.gameObject);
            _sessionHosts.Add(new WeakReference<GameObjectPool>(this));
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
            try
            {
                ClearAll();
            }
            finally
            {
                MonoSingletonProperty<GameObjectPool>.NotifyDestroyed(this);
            }
        }

        private void ThrowIfDestroyed()
        {
            if (_isDestroyed || _isResetting || !this)
            {
                throw new ObjectDisposedException(nameof(GameObjectPool));
            }
        }

        private static void ValidateInitialization(string key, int capacity, int target)
        {
            GameObjectPoolData.ValidateKey(key);
            GameObjectPoolData.ValidateCapacity(capacity);
            if (target < 0 || (capacity >= 0 && target > capacity))
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }
        }

        private void ValidateInput(string key, GameObject obj)
        {
            GameObjectPoolData.ValidateInstance(obj);
            GameObjectPoolData.ValidateOwner(obj, _identity, key);
            if (this && transform.IsChildOf(obj.transform))
            {
                throw new ArgumentException("不能将池宿主或其祖先归还到对象池。", nameof(obj));
            }
        }

        private GameObjectPoolData GetOrCreate(string key, int capacity = -1)
        {
            ThrowIfDestroyed();
            if (_creating.ContainsKey(key))
            {
                throw new InvalidOperationException("不能在层级回调中重入创建同一 key 的桶。");
            }

            if (_pools.TryGetValue(key, out var data))
            {
                if (data.IsLive(data.CurrentLifetime))
                {
                    return data;
                }

                _pools.Remove(key);
                _dataPool.Recycle(data);
            }
            var owner = _dataPool ??= SafeObjectPool<GameObjectPoolData>.Instance;
            _creating.Add(key, false);
            data = null;
            try
            {
                data = owner.Allocate();
                data.CacheOwner = owner;
                data.Init(key, transform, capacity, _identity);
                ThrowIfDestroyed();
                if (_creating[key])
                {
                    throw new ObjectDisposedException(nameof(GameObjectPoolData));
                }

                _pools.Add(key, data);
                return data;
            }
            catch
            {
                if (data != null)
                {
                    owner.Discard(data);
                }

                throw;
            }
            finally
            {
                _creating.Remove(key);
            }
        }

        /// <summary>
        /// 设置空闲容量并按目标存活数量补齐；-1 无限，0 禁缓存。非法输入在修改桶之前抛错。
        /// Prefab 仅在本次调用中使用，不保留资产引用；预热在失活根下创建。
        /// </summary>
        public void InitOneGoData(string keyName, int maxCapacity = -1, GameObject prefab = null, int defaultQuantity = 0)
        {
            ValidateInitialization(keyName, maxCapacity, defaultQuantity);
            if (defaultQuantity > 0 && !prefab)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            var data = GetOrCreate(keyName, maxCapacity);
            data.MaxCapacity = maxCapacity;
            if (defaultQuantity > 0)
            {
                data.Prewarm(prefab, defaultQuantity);
            }
        }

        /// <summary>使用 Prefab 名称作为首次注册 key；取得后改名不改变归属。</summary>
        public void InitOneGoData(GameObject prefab, int maxCapacity = -1, int defaultQuantity = 0)
        {
            if (!prefab)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            InitOneGoData(prefab.name, maxCapacity, prefab, defaultQuantity);
        }

        /// <summary>
        /// 数组长度作为目标空闲总量；已有缓存计入目标，null/失效/重复输入不占接纳名额。
        /// 只接纳当前桶或未登记的场景实例，重复/已空闲输入不销毁；超目标的独占输入退休。
        /// 外来归属和非法参数在修改桶/输入前拒绝；生命周期回调失败不提供整批事务回滚。
        /// </summary>
        public void InitOneGoData(string keyName, int maxCapacity, GameObject[] gameObjects)
        {
            GameObjectPoolData.ValidateKey(keyName);
            GameObjectPoolData.ValidateCapacity(maxCapacity);
            var unique = new List<GameObject>();
            var seen = new HashSet<GameObject>();
            if (gameObjects != null)
            {
                foreach (var obj in gameObjects)
                {
                    if (!obj || !seen.Add(obj))
                    {
                        continue;
                    }

                    ValidateInput(keyName, obj);
                    unique.Add(obj);
                }
            }
            var target = gameObjects?.Length ?? 0;
            ValidateInitialization(keyName, maxCapacity, target);
            var data = GetOrCreate(keyName, maxCapacity);
            var lifetime = data.CurrentLifetime;
            data.MaxCapacity = maxCapacity;
            foreach (var obj in unique)
            {
                if (!data.IsLive(lifetime))
                {
                    throw new ObjectDisposedException(nameof(GameObjectPoolData));
                }
                // 名称仅供层级识别；实例路由由不可变归属决定。
                if (obj)
                {
                    obj.name = keyName;
                }

                data.Recycle(obj, target, lifetime);
            }
        }

        /// <summary>用已有实例初始化无限容量桶；显式数组入口避免与 Prefab 可选参数重载歧义。</summary>
        public void InitOneGoData(string keyName, GameObject[] gameObjects) => InitOneGoData(keyName, -1, gameObjects);

        /// <summary>从指定桶取得，空桶返回 null；parent 为 null 时回到当前活动场景。</summary>
        public GameObject Allocate(string keyName, Transform parent = null)
        {
            GameObjectPoolData.ValidateKey(keyName);
            ThrowIfDestroyed();
            return _pools.TryGetValue(keyName, out var data) ? data.Allocate(parent) : null;
        }

        /// <summary>按稳定归属归还；首次接纳未登记实例时才使用对象名称作为 key。</summary>
        public void Recycle(GameObject go)
        {
            if (!go)
            {
                return;
            }

            Recycle(GameObjectPoolData.TryGetLifetime(go, out var lifetime) ? lifetime.Key : go.name, go);
        }

        /// <summary>
        /// 接纳未登记实例或归还本 key 借出实例；跨 key/宿主抛错且不修改对象。
        /// false 仅表示未缓存，满池/已关闭桶的晚归还会销毁，不得继续使用。
        /// 原始引用不区分租用代次，归还后禁止保留旧别名。
        /// </summary>
        public bool Recycle(string keyName, GameObject obj)
        {
            GameObjectPoolData.ValidateKey(keyName);
            if (!obj)
            {
                return false;
            }

            ValidateInput(keyName, obj);
            if (GameObjectPoolData.TryGetLifetime(obj, out var lifetime) && lifetime.IsClosed)
            {
                return GameObjectPoolData.ReturnToClosedLifetime(obj);
            }

            return GetOrCreate(keyName).Recycle(obj);
        }

        /// <summary>存活的空闲实例数量；查询不创建桶或宿主。</summary>
        public int GetCachedCount(string keyName)
        {
            GameObjectPoolData.ValidateKey(keyName);
            return _pools.TryGetValue(keyName, out var data) ? data.Count : 0;
        }

        /// <summary>先摘桶，再销毁空闲实例与缓存节点；借出对象保留，晚归还时退休。</summary>
        public void Clear(string keyName)
        {
            GameObjectPoolData.ValidateKey(keyName);
            if (_creating.ContainsKey(keyName))
            {
                _creating[keyName] = true;
            }

            if (!_pools.TryGetValue(keyName, out var data))
            {
                return;
            }

            _pools.Remove(keyName);
            _dataPool.Recycle(data);
        }

        /// <summary>摘除所有当前桶再逐项关闭；回调新建的桶保留，宿主销毁时禁止新建。</summary>
        public void ClearAll()
        {
            foreach (var key in new List<string>(_creating.Keys))
            {
                _creating[key] = true;
            }

            if (_pools.Count == 0)
            {
                return;
            }

            var snapshot = new List<GameObjectPoolData>(_pools.Values);
            _pools.Clear();
            List<Exception> errors = null;
            foreach (var data in snapshot)
            {
                try
                {
                    _dataPool.Recycle(data);
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
            }
            if (errors != null)
            {
                throw new AggregateException(errors);
            }
        }
    }
}
