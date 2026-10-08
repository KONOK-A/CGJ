using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// GameObjectPool 中单个 key 对应的池数据：缓存同一种 GameObject。
    /// 池数据自身也由 SafeObjectPool 复用，<see cref="OnRecycle"/> 会销毁池根节点与缓存对象。
    /// </summary>
    public class GameObjectPoolData : IOfPool, IPoolType
    {
        #region GameObjectPoolData持有的数据及初始化方法

        /// <summary>
        /// 缓存对象在池中的父节点；回收时对象挂到该节点下。
        /// </summary>
        public Transform RootTransform;

        /// <summary>
        /// 空闲 GameObject 缓存队列。
        /// </summary>
        public readonly Queue<GameObject> PoolQueue;

        /// <summary>
        /// 缓存容量上限，-1 表示不限制；超出时对象会被直接销毁。
        /// </summary>
        public int MaxCapacity = -1;

        // 已在缓存中的对象集合，用于拒绝重复回收导致同一对象被多次分配
        private readonly HashSet<GameObject> _pooledObjects = new();

        /// <summary>
        /// 创建空池数据，队列使用默认容量。
        /// </summary>
        public GameObjectPoolData()
        {
            PoolQueue = new Queue<GameObject>();
        }

        /// <summary>
        /// 创建池数据。
        /// </summary>
        /// <param name="capacity">队列初始容量，-1 表示使用默认容量。</param>
        public GameObjectPoolData(int capacity = -1)
        {
            PoolQueue = capacity == -1 ? new Queue<GameObject>() : new Queue<GameObject>(capacity);
        }

        /// <summary>
        /// 初始化池数据：在池根节点下创建以 assetPath 命名的父节点。
        /// </summary>
        /// <param name="assetPath">缓存类型名，同时作为池根节点名称。</param>
        /// <param name="poolRootObj">GameObjectPool 的根节点。</param>
        /// <param name="capacity">容量上限，-1 表示不限制。</param>
        public void Init(string assetPath, Transform poolRootObj, int capacity = -1)
        {
            // 复用前确保没有残留缓存（正常路径已由 OnRecycle 清空，这里做二次防御）
            PoolQueue.Clear();
            _pooledObjects.Clear();

            //创建父节点 并设置到对象池根节点下方
            var go = new GameObject();
            go.Parent(poolRootObj);
            RootTransform = go.transform;
            RootTransform.name = assetPath;
            MaxCapacity = capacity;
        }

        #endregion

        #region GameObjectPool数据相关操作

        /// <summary>
        /// 将对象放入缓存：空对象与重复回收会被拒绝，超出容量或池节点已销毁时对象会被直接销毁。
        /// 入池后对象失活并挂到池根节点下。
        /// </summary>
        /// <param name="obj">待回收对象。</param>
        /// <returns>对象成功进入缓存时返回 true。</returns>
        public bool Recycle(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogError("您正在将Null放置对象池!");
                return false;
            }

            // 拒绝重复回收：同一对象已在缓存中时再次入队会导致它被多次分配出去
            if (!_pooledObjects.Add(obj))
            {
                Debug.LogWarning($"对象 {obj.name} 已被回收，重复回收被忽略。");
                return false;
            }

            // 检测是不是超过容量
            if (MaxCapacity != -1 && PoolQueue.Count >= MaxCapacity)
            {
                _pooledObjects.Remove(obj);
                //超容量了则直接销毁
                obj.DestroySelfGracefully();
                return false;
            }

            // 池数据已销毁（池节点失效）时直接销毁对象，避免挂回已销毁的层级
            if (RootTransform == null)
            {
                _pooledObjects.Remove(obj);
                obj.DestroySelfGracefully();
                return false;
            }

            // 对象进容器
            PoolQueue.Enqueue(obj);
            // 设置父物体
            obj.transform.SetParent(RootTransform);
            // 设置隐藏
            obj.SetActive(false);
            return true;
        }

        /// <summary>
        /// 从缓存取出对象：先挂到 parent 下再激活；parent 为空时移回当前活动场景。
        /// 缓存为空或缓存对象已被外部销毁（fake-null）时返回 null。
        /// </summary>
        /// <param name="parent">分配后的父节点，可为 null。</param>
        public GameObject Allocate(Transform parent = null)
        {
            // 跳过已被外部销毁的缓存对象
            while (PoolQueue.Count > 0)
            {
                var obj = PoolQueue.Dequeue();
                if (obj == null)
                {
                    _pooledObjects.Remove(obj);
                    continue;
                }

                _pooledObjects.Remove(obj);
                // 先设置父物体，保证 OnEnable 在最终层级下触发
                obj.transform.SetParent(parent);
                if (parent == null)
                {
                    //父物体为空 回归默认场景
                    SceneManager.MoveGameObjectToScene(obj, SceneManager.GetActiveScene());
                }

                // 显示对象
                obj.SetActive(true);
                return obj;
            }

            return null;
        }

        // 销毁池数据：销毁缓存中的全部对象与池根节点，并清空容器
        private void Destroy()
        {
            //重置容量
            MaxCapacity = -1;

            // 销毁缓存对象：销毁父节点不会级联销毁 detach 的子对象，必须逐个销毁
            while (PoolQueue.Count > 0)
            {
                var obj = PoolQueue.Dequeue();
                obj.DestroySelfGracefully();
            }

            _pooledObjects.Clear();

            RootTransform.DestroyGameObjGracefully();
            RootTransform = null;
        }

        #endregion

        /// <summary>
        /// 池数据被 SafeObjectPool 回收时调用：销毁池节点与缓存对象。
        /// </summary>
        public void OnRecycle()
        {
            Destroy();
        }

        /// <summary>
        /// 池数据自身的回收标记，由 SafeObjectPool 维护。
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 将池数据回收到 SafeObjectPool。
        /// </summary>
        public void RecycleToCache()
        {
            SafeObjectPool<GameObjectPoolData>.Instance.Recycle(this);
        }
    }
}
