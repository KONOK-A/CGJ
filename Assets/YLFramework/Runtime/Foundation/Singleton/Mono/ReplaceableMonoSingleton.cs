using UnityEngine;

namespace YLFramework.Core.Singleton
{
    /// <summary>
    /// 可替换 MonoBehaviour 单例：场景中存在多个实例时保留最后创建的，先创建者被销毁（同一帧内按创建序号区分先后）。
    /// Instance 首次访问时优先从场景查找，找不到才创建（创建的对象 HideAndDontSave）；
    /// 所有实例都会 DontDestroyOnLoad；退出流程中 Instance 返回 null。
    /// </summary>
    /// <typeparam name="T">单例类型（继承时传入自身）。</typeparam>
    public class ReplaceableMonoSingleton<T> : MonoBehaviour where T : ReplaceableMonoSingleton<T>
    {
        private static T _instance;
        private static long _creationCounter;

        /// <summary>
        /// Awake 时的 Time.time，可用于外部判断实例创建时间。
        /// </summary>
        protected float InitializationTime;

        /// <summary>
        /// 当前实例的创建顺序，同一帧内也能区分先后
        /// </summary>
        private long _creationOrder;

        static ReplaceableMonoSingleton()
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
                if (_instance == null)
                {
                    if (SingletonSessionState.IsApplicationQuitting)
                    {
                        return null;
                    }

                    _instance = FindFirstObjectByType<T>();
                    if (_instance == null)
                    {
                        var obj = new GameObject
                        {
                            hideFlags = HideFlags.HideAndDontSave
                        };
                        _instance = obj.AddComponent<T>();
                    }
                }

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
        /// 按创建顺序竞争：销毁所有比自身更早创建的实例，最后创建者成为当前单例。
        /// </summary>
        protected virtual void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            InitializationTime = Time.time;
            _creationOrder = ++_creationCounter;

            DontDestroyOnLoad(this.gameObject);

            var check = FindObjectsByType<T>(FindObjectsSortMode.InstanceID);
            foreach (var searched in check)
            {
                if (searched == this) continue;
                //如果搜索到的物体创建顺序早于当前实例，则销毁
                if (searched._creationOrder < _creationOrder)
                {
                    Destroy(searched.gameObject);
                }
            }

            //最后创建的实例成为当前单例
            _instance = this as T;
        }

        private static void ResetSessionState()
        {
            _instance = null;
            _creationCounter = 0;
        }
    }
}
