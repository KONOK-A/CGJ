using System;
using UnityEngine;

namespace YLFramework.Core.Singleton
{
    /// <summary>
    /// 通过 ScriptableObject 资产创建并持有单例。
    /// 默认按类型名从 Resources 加载资产，可通过 InstanceWithLoader 替换加载方式。
    /// InstanceWithLoader 属于单次 Play Session 的运行期配置，进入 Play 时会恢复为默认加载方式。
    /// </summary>
    public class ScriptableSingletonProperty<T> where T : ScriptableObject
    {
        private static readonly Func<string, T> DefaultLoader = Resources.Load<T>;

        /// <summary>
        /// 资产加载方式，可替换（例如接入 ResService）
        /// </summary>
        public static Func<string, T> ScriptableLoader = DefaultLoader;

        private static T _instance;
        private static bool _loadedFromDefaultLoader;
        private static bool _loadFailedWarned;
        private static bool _isCreating;

        static ScriptableSingletonProperty()
        {
            SingletonSessionState.RegisterSessionReset(ResetSessionState);
        }

        /// <summary>
        /// 临时替换资产加载方式并返回实例：配置仅对本次 Play 会话有效，进入下一次 Play 会恢复默认 Resources 加载。
        /// </summary>
        /// <param name="loader">资产加载委托，参数为类型名；为 null 时本次加载会失败并输出一次警告。</param>
        /// <returns>加载到的资产实例；加载失败时返回 null 并输出一次警告。</returns>
        public static T InstanceWithLoader(Func<string, T> loader)
        {
            ScriptableLoader = loader;
            return Instance;
        }

        /// <summary>
        /// 单例资产；首次访问时按 <see cref="ScriptableLoader"/> 加载（默认按类型名从 Resources 加载）。
        /// 加载失败时返回 null 并输出一次警告（重复访问只警告一次），不会抛异常；稍后可重试。
        /// 加载器内部重入 Instance 时不会递归加载。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_instance == null && !_isCreating)
                {
                    _isCreating = true;
                    try
                    {
                        _loadedFromDefaultLoader = ReferenceEquals(ScriptableLoader, DefaultLoader);
                        _instance = ScriptableLoader?.Invoke(typeof(T).Name);
                    }
                    finally
                    {
                        _isCreating = false;
                    }

                    if (_instance != null)
                    {
                        _loadFailedWarned = false;
                    }
                    else
                    {
                        WarnLoadFailed();
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 当前是否已经持有单例实例（不会触发加载）
        /// </summary>
        public static bool HasInstance => _instance != null;

        /// <summary>
        /// 尝试获取已经持有的单例实例（不会触发加载）
        /// </summary>
        public static bool TryGetInstance(out T instance)
        {
            instance = _instance;
            return instance != null;
        }

        /// <summary>
        /// 释放单例，下次访问 Instance 会重新加载。
        /// 仅当资产由默认 Resources 加载方式载入时才卸载资产，自定义 Loader 载入的资产由调用方自行管理。
        /// </summary>
        public static void Dispose()
        {
            if (_instance != null && _loadedFromDefaultLoader)
            {
                Resources.UnloadAsset(_instance);
            }

            _instance = null;
            _loadedFromDefaultLoader = false;
        }

        private static void WarnLoadFailed()
        {
            if (_loadFailedWarned)
            {
                return;
            }

            _loadFailedWarned = true;
            Debug.LogWarning(
                $"[ScriptableSingletonProperty<{typeof(T).Name}>] 未能加载单例资产：ScriptableLoader 返回空。" +
                $"默认按类型名从 Resources 加载 \"{typeof(T).Name}\"，请确认该资产存在，或使用 InstanceWithLoader 提供加载方式。");
        }

        private static void ResetSessionState()
        {
            _instance = null;
            _loadedFromDefaultLoader = false;
            _loadFailedWarned = false;
            _isCreating = false;
            ScriptableLoader = DefaultLoader;
        }
    }
}
