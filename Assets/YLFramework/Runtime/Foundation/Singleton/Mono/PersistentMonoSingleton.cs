using UnityEngine;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Core.Singleton
{
    /// <summary>
    /// 常驻 MonoBehaviour 单例：Instance 首次访问时优先从场景查找，找不到才创建。
    /// 场景中存在多个实例时保留先激活（先登记）的，后激活者自动销毁自身 GameObject。
    /// 实例会 DontDestroyOnLoad 跨场景常驻；退出流程中 Instance 返回 null。
    /// </summary>
    /// <typeparam name="T">单例类型（继承时传入自身）。</typeparam>
    public class PersistentMonoSingleton<T> : MonoBehaviour where T : PersistentMonoSingleton<T>
    {
        private static T _instance;

        static PersistentMonoSingleton()
        {
            SingletonSessionState.RegisterSessionReset(ResetSessionState);
        }

        /// <summary>
        /// 单例实例；首次访问时从场景查找或创建，退出流程中返回 null。
        /// </summary>
        public static T Instance
        {
            get
            {
                //有实例直接返回
                if (_instance != null)
                {
                    return _instance;
                }
                //退出流程中不再创建
                if (SingletonSessionState.IsApplicationQuitting)
                {
                    return null;
                }
                //没有则先在场景中找这个类的实例
                _instance = FindFirstObjectByType<T>();
                if (_instance != null)
                {
                    return _instance;
                }
                //再没有则创建
                var obj = new GameObject();
                _instance = obj.AddComponent<T>();
                return _instance;
            }
        }

        /// <summary>
        /// 当前是否已经持有单例实例（不会触发查找或创建）
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// 尝试获取已经持有的单例实例（不会触发查找或创建）
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            instance = _instance;
            return instance != null;
        }

        /// <summary>
        /// 登记实例：已有实例时销毁自身；成为当前实例后常驻（同时覆盖在 Awake 之前被 Instance 创建的对象）。
        /// </summary>
        protected virtual void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_instance == null)
            {
                _instance = this as T;
            }
            else
            {
                if (this != _instance)
                {
                    this.DestroyGameObjGracefully();
                    return;
                }
            }

            //在 Awake 前被 Instance 获取的实例同样需要持久化 (QF v1.0.197 修复)
            DontDestroyOnLoad(gameObject);
        }

        private static void ResetSessionState()
        {
            _instance = null;
        }
    }
}
