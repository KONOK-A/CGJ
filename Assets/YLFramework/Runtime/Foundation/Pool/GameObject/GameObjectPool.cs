using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Singleton;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// GameObject 对象池单例，按 key 分桶缓存同种物体。
    /// 层级路径固定为 "YLFramework/Pool/GameObjectPool"（DontDestroyOnLoad），
    /// 池只负责缓存与分配：池中没有对象时 <see cref="Allocate"/> 返回 null，不会实例化预制体。
    /// </summary>
    [MonoSingletonPath("YLFramework/Pool/GameObjectPool")]
    public class GameObjectPool : MonoBehaviour, ISingleton
    {
        #region 单例相关

        /// <summary>
        /// GameObjectPool 单例；仅在运行时可用（编辑器非播放模式返回 null）。
        /// </summary>
        public static GameObjectPool Instance => MonoSingletonProperty<GameObjectPool>.Instance;

        /// <summary>
        /// 单例初始化（当前无需额外初始化逻辑）。
        /// </summary>
        public void OnSingletonInit()
        {
            //初始化
        }

        // 宿主销毁（场景卸载 / 手动 Dispose / 退出）时清理所有池，
        // 否则池中对象会从池节点 detach 后残留在场景中
        private void OnDestroy()
        {
            ClearAll();
        }

        #endregion

        #region GameObjectPoolModule持有的数据及初始化方法

        /// <summary>
        /// 存储各个 key 对应的池数据。
        /// </summary>
        private Dictionary<string, GameObjectPoolData> PoolDic { get; set; } = new();

        /// <summary>
        /// 初始化一个 key 的对象池数据并设置容量。
        /// </summary>
        /// <param name="keyName">对象索引名称</param>
        /// <param name="maxCapacity">容量限制，超出时会销毁而不是进入对象池，-1代表无限</param>
        /// <param name="prefab">填写默认容量时预先放入的对象</param>
        /// <param name="defaultQuantity">默认容量，填写会向池子中放入对应数量的对象，0代表不预先放入</param>
        public void InitOneGoData(string keyName, int maxCapacity = -1, GameObject prefab = null, int defaultQuantity = 0)
        {
            if (defaultQuantity > maxCapacity && maxCapacity != -1)
            {
                Debug.LogError("默认容量超出最大容量限制");
                return;
            }

            //该对象的对象数据已经存在
            if (PoolDic.TryGetValue(keyName, out var poolData))
            {
                //更新容量限制
                poolData.MaxCapacity = maxCapacity;
                //在指定默认容量和默认对象时才进行默认对象填充
                if (defaultQuantity <= 0) return;
                if (prefab != null)
                {
                    var nowCapacity = poolData.PoolQueue.Count;
                    // 生成差值容量个数的物体放入对象池
                    for (var i = 0; i < defaultQuantity - nowCapacity; i++)
                    {
                        var go = Instantiate(prefab);
                        // 统一使用 keyName 命名，保证 Recycle(go) 能按名字找回该池
                        go.name = keyName;
                        poolData.Recycle(go);
                    }
                }
                else
                {
                    Debug.LogWarning("默认对象生成预制体未指定");
                }
            }
            //设置的对象池不存在
            else
            {
                //创建对象池数据
                poolData = CreateGameObjectPoolData(keyName, maxCapacity);
                //在指定默认容量和默认对象时才有意义
                if (defaultQuantity <= 0) return;
                if (prefab != null)
                {
                    // 生成容量个数的物体放入对象池
                    for (var i = 0; i < defaultQuantity; i++)
                    {
                        var go = Instantiate(prefab);
                        go.name = keyName;
                        poolData.Recycle(go);
                    }
                }
                else
                {
                    Debug.LogWarning("默认对象生成预制体未指定");
                }
            }
        }

        /// <summary>
        /// 初始化对象池并设置容量，数据的索引值为预制体的名称。
        /// </summary>
        /// <param name="prefab">提供 key 与默认对象的预制体</param>
        /// <param name="maxCapacity">容量限制，超出时会销毁而不是进入对象池，-1代表无限</param>
        /// <param name="defaultQuantity">默认容量，填写会向池子中放入对应数量的对象，0代表不预先放入</param>
        public void InitOneGoData(GameObject prefab, int maxCapacity = -1, int defaultQuantity = 0)
        {
            InitOneGoData(prefab.name, maxCapacity, prefab, defaultQuantity);
        }

        /// <summary>
        /// 用一批对象初始化指定 key 的对象池：数组长度视为目标缓存总量，
        /// 池中已有 nowCapacity 个时，只补入前 (数组长度 - nowCapacity) 个，其余直接销毁。
        /// </summary>
        /// <param name="keyName">对象索引名称</param>
        /// <param name="maxCapacity">最大容量，-1代表无限</param>
        /// <param name="gameObjects">默认要放进来的对象数组</param>
        public void InitOneGoData(string keyName, int maxCapacity = -1, GameObject[] gameObjects = null)
        {
            if (gameObjects != null && gameObjects.Length > maxCapacity && maxCapacity != -1)
            {
                Debug.LogError("默认容量超出最大容量限制");
                return;
            }

            //设置的对象池已经存在
            if (PoolDic.TryGetValue(keyName, out var poolData))
            {
                //更新容量限制
                poolData.MaxCapacity = maxCapacity;
            }
            //设置的对象池不存在
            else
            {
                //创建对象池
                poolData = CreateGameObjectPoolData(keyName, maxCapacity);
            }

            //数组内容作为默认对象置入对象池
            if (gameObjects is { Length: > 0 })
            {
                var nowCapacity = poolData.PoolQueue.Count;
                // 生成差值容量个数的物体放入对象池
                for (var i = 0; i < gameObjects.Length; i++)
                {
                    var go = gameObjects[i];
                    if (go == null) continue;
                    if (i < gameObjects.Length - nowCapacity)
                    {
                        go.name = keyName;
                        poolData.Recycle(go);
                    }
                    else
                    {
                        go.DestroySelfGracefully();
                    }
                }
            }
        }

        /// <summary>
        /// 创建一条新的对象池数据（复用 SafeObjectPool 中的池数据实例）。
        /// </summary>
        private GameObjectPoolData CreateGameObjectPoolData(string keyName, int maxCapacity = -1)
        {
            //交由Object对象池拿到poolData的类
            var poolData = SafeObjectPool<GameObjectPoolData>.Instance.Allocate();
            //对拿到的poolData副本进行初始化（覆盖之前的数据）
            poolData.Init(keyName, transform, maxCapacity);
            PoolDic.Add(keyName, poolData);
            return poolData;
        }

        #endregion

        #region GameObjectPool相关功能

        /// <summary>
        /// 从指定 key 的池中分配对象；池不存在或缓存为空时返回 null。
        /// </summary>
        /// <param name="keyName">对象索引名称</param>
        /// <param name="parent">分配后的父节点，可为 null（此时对象移回当前活动场景）</param>
        public GameObject Allocate(string keyName, Transform parent = null)
        {
            return PoolDic.TryGetValue(keyName, out var poolData) ? poolData.Allocate(parent) : null;
        }

        /// <summary>
        /// 按对象名称回收 GameObject（名称需与注册时的 key 一致）。
        /// </summary>
        public void Recycle(GameObject go)
        {
            if (go == null)
            {
                Debug.LogError("您正在将Null放置对象池!");
                return;
            }

            Recycle(go.name, go);
        }

        /// <summary>
        /// 按指定 key 回收 GameObject；key 不存在时会自动创建一条池数据。
        /// </summary>
        /// <param name="keyName">对象索引名称</param>
        /// <param name="obj">待回收对象</param>
        /// <returns>对象成功进入缓存时返回 true</returns>
        public bool Recycle(string keyName, GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogError("您正在将Null放置对象池!");
                return false;
            }

            if (!PoolDic.TryGetValue(keyName, out var poolData))
            {
                poolData = CreateGameObjectPoolData(keyName);
            }

            return poolData.Recycle(obj);
        }

        /// <summary>
        /// 清除指定 key 的池：销毁缓存对象与池节点，并移除该 key 的数据。
        /// </summary>
        public void Clear(string keyName)
        {
            if (!PoolDic.TryGetValue(keyName, out var poolData)) return;
            poolData.RecycleToCache();
            PoolDic.Remove(keyName);
        }

        /// <summary>
        /// 清除所有池：销毁全部缓存对象与池节点。
        /// </summary>
        public void ClearAll()
        {
            if (PoolDic.Count == 0) return;

            var enumerator = PoolDic.GetEnumerator();
            while (enumerator.MoveNext())
            {
                enumerator.Current.Value.RecycleToCache();
            }

            PoolDic.Clear();
        }

        #endregion
    }
}
