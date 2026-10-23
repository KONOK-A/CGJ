using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core.Singleton
{
    /// <summary>
    /// Singleton 模块的运行期会话状态。
    /// 每次进入 Play 时重置属于单次 Play Session 的静态缓存，
    /// 保证 Domain Reload disabled 时与会重新加载域的行为一致。
    /// </summary>
    internal static class SingletonSessionState
    {
        private static readonly List<Action> _sessionResets = new();
        private static bool _isApplicationQuitting;
        private static bool _isExecutionStopping;
        internal static object ExecutionSession { get; private set; } = new();

        // 只约束基础执行宿主，不改变 ApplicationTool 或其他领域单例的既有语义。
        internal static bool CanRunExecution => Application.isPlaying &&
#if UNITY_EDITOR
            // 请求退出后 isPlaying 暂时仍为 true；不依赖 ExitingPlayMode 监听的注册顺序。
            EditorApplication.isPlayingOrWillChangePlaymode &&
#endif
            !_isExecutionStopping && !_isApplicationQuitting;

        internal static void StopExecution() => _isExecutionStopping = true;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InstallExecutionGate()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                StopExecution();
            }
        }
#endif

        /// <summary>
        /// 是否处于退出流程中，退出阶段禁止再创建 MonoBehaviour 单例对象
        /// </summary>
        public static bool IsApplicationQuitting => _isApplicationQuitting;

        /// <summary>
        /// 注册每次进入 Play 时需要重置的会话状态
        /// </summary>
        public static void RegisterSessionReset(Action reset)
        {
            if (reset == null)
            {
                return;
            }

            lock (_sessionResets)
            {
                _sessionResets.Add(reset);
            }
        }

        /// <summary>
        /// 标记应用进入退出流程
        /// </summary>
        public static void MarkApplicationQuitting()
        {
            _isApplicationQuitting = true;
            StopExecution();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewSession()
        {
            StopExecution();
            ExecutionSession = new object();
            _isApplicationQuitting = false;
#if UNITY_EDITOR
            InstallExecutionGate();
#endif

            //Domain Reload disabled 时静态事件订阅会残留，先减后加保证只有一份
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;

            lock (_sessionResets)
            {
                for (var i = 0; i < _sessionResets.Count; i++)
                {
                    _sessionResets[i]?.Invoke();
                }
            }
            _isExecutionStopping = false;
        }

        private static void OnApplicationQuitting()
        {
            MarkApplicationQuitting();
        }
    }
}
