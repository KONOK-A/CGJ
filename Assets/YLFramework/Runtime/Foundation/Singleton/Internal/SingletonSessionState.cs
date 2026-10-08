using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core.Singleton
{
    /// <summary>
    /// Singleton 模块的运行期会话状态。
    /// 每次进入 Play 时重置属于单次 Play Session 的静态缓存，
    /// 保证 Domain Reload disabled 时与会重新加载域的行为一致。
    /// </summary>
    internal static class SingletonSessionState
    {
        private static readonly List<Action> SessionResets = new();
        private static bool _isApplicationQuitting;

        /// <summary>
        /// 是否处于退出流程中，退出阶段禁止再创建 MonoBehaviour 单例对象
        /// </summary>
        public static bool IsApplicationQuitting => _isApplicationQuitting;

        /// <summary>
        /// 注册每次进入 Play 时需要重置的会话状态
        /// </summary>
        public static void RegisterSessionReset(Action reset)
        {
            if (reset == null) return;
            lock (SessionResets)
            {
                SessionResets.Add(reset);
            }
        }

        /// <summary>
        /// 标记应用进入退出流程
        /// </summary>
        public static void MarkApplicationQuitting()
        {
            _isApplicationQuitting = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewSession()
        {
            _isApplicationQuitting = false;

            //Domain Reload disabled 时静态事件订阅会残留，先减后加保证只有一份
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;

            lock (SessionResets)
            {
                for (var i = 0; i < SessionResets.Count; i++)
                {
                    SessionResets[i]?.Invoke();
                }
            }
        }

        private static void OnApplicationQuitting()
        {
            MarkApplicationQuitting();
        }
    }
}
