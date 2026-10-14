using System;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Services.Audio
{
    /// <summary>默认关闭的运行时音频诊断窗口，支持 F1、Android 返回键和 iOS 四指入口。</summary>
    /// <remarks>运行初始化只创建隐藏的入口宿主，不访问 AudioKit；显式打开后才绘制诊断。宿主跨场景保留。快捷键入口和绘制仅在 Editor 或 Development Build 编译。</remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("YLFramework/Audio/AudioKit Debug Window")]
    public sealed class AudioKitDebugWindow : MonoSingletonHost<AudioKitDebugWindow>
    {
        private bool mVisible;
#if UNITY_IOS && !UNITY_EDITOR
        private bool mTouching;
#endif
        private Vector2 mScrollPosition;
        private Rect mWindowRect = new Rect(12, 12, 640, 560);

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
        // 诊断 UI 只读取当前输入后端；反射避免 Runtime 程序集新增可选 Input System 包依赖。
        private static readonly System.Reflection.PropertyInfo ButtonReleased =
            Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem", true).GetProperty("wasReleasedThisFrame");
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID
        private static readonly Type KeyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem", true);
        private static readonly System.Reflection.PropertyInfo KeyboardCurrent = KeyboardType.GetProperty("current");
        private static readonly System.Reflection.PropertyInfo ShortcutKey = KeyboardType.GetProperty(
#if UNITY_ANDROID && !UNITY_EDITOR
            "escapeKey");
#else
            "f1Key");
#endif
#elif UNITY_IOS
        private static readonly Type TouchscreenType = Type.GetType("UnityEngine.InputSystem.Touchscreen, Unity.InputSystem", true);
        private static readonly System.Reflection.PropertyInfo TouchscreenCurrent = TouchscreenType.GetProperty("current");
        private static readonly System.Reflection.PropertyInfo Touches = TouchscreenType.GetProperty("touches");
        private static readonly System.Reflection.PropertyInfo TouchPress =
            Type.GetType("UnityEngine.InputSystem.Controls.TouchControl, Unity.InputSystem", true).GetProperty("press");
        private static readonly System.Reflection.PropertyInfo ButtonPressed =
            Type.GetType("UnityEngine.InputSystem.Controls.ButtonControl, Unity.InputSystem", true).GetProperty("isPressed");
#endif
#endif

        /// <summary>当前运行时窗口是否可见；读取不会创建宿主或初始化音频。</summary>
        public static bool IsVisible => TryGetInstance(out var instance) && instance.mVisible;

        /// <summary>在播放模式显示诊断窗口，必要时创建宿主。</summary>
        /// <remarks>运行时诊断仅在 Editor 或 Development Build 可用。</remarks>
        /// <exception cref="InvalidOperationException">当前不在播放模式，或当前构建不包含运行时诊断。</exception>
        public static void Show() => EnsureInstance().mVisible = true;

        /// <summary>隐藏现有诊断窗口，不停止音频，也不创建宿主。</summary>
        public static void Hide()
        {
            if (TryGetInstance(out var instance)) instance.mVisible = false;
        }

        /// <summary>在播放模式切换诊断窗口的显示状态。</summary>
        /// <remarks>运行时诊断仅在 Editor 或 Development Build 可用。</remarks>
        /// <exception cref="InvalidOperationException">当前不在播放模式，或当前构建不包含运行时诊断。</exception>
        public static void Toggle()
        {
            var instance = EnsureInstance();
            instance.mVisible = !instance.mVisible;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallShortcuts() => EnsureInstance().mVisible = false;
#endif

        private static AudioKitDebugWindow EnsureInstance()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            throw new InvalidOperationException("AudioKit diagnostics are available only in development builds.");
#else
            if (!Application.isPlaying)
                throw new InvalidOperationException("AudioKit 运行时诊断窗口只能在播放模式打开。");
            var instance = GetInstance(includeInactive: false);
            if (!instance) throw new InvalidOperationException("AudioKit diagnostics are shutting down.");
            return instance;
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Input.GetKeyUp(KeyCode.F1)) Toggle();
#elif UNITY_ANDROID
            if (Input.GetKeyUp(KeyCode.Escape)) Toggle();
#elif UNITY_IOS
            if (!mTouching && Input.touchCount == 4)
            {
                mTouching = true;
                Toggle();
            }
            else if (Input.touchCount == 0) mTouching = false;
#endif
#elif ENABLE_INPUT_SYSTEM
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID
            var keyboard = KeyboardCurrent.GetValue(null);
            if (keyboard != null && (bool)ButtonReleased.GetValue(ShortcutKey.GetValue(keyboard))) Toggle();
#elif UNITY_IOS
            var touchscreen = TouchscreenCurrent.GetValue(null);
            int count = 0;
            if (touchscreen != null)
                foreach (var touch in (System.Collections.IEnumerable)Touches.GetValue(touchscreen))
                    if ((bool)ButtonPressed.GetValue(TouchPress.GetValue(touch))) count++;
            if (!mTouching && count == 4) { mTouching = true; Toggle(); }
            else if (count == 0) mTouching = false;
#endif
#endif
        }

        private void OnGUI()
        {
            if (!mVisible) return;
            mWindowRect.width = Mathf.Min(640, Mathf.Max(1, Screen.width - 24));
            mWindowRect.height = Mathf.Min(560, Mathf.Max(1, Screen.height - 24));
            mWindowRect.x = Mathf.Clamp(mWindowRect.x, 0, Mathf.Max(0, Screen.width - mWindowRect.width));
            mWindowRect.y = Mathf.Clamp(mWindowRect.y, 0, Mathf.Max(0, Screen.height - mWindowRect.height));
            mWindowRect = GUILayout.Window(GetInstanceID(), mWindowRect, DrawWindow, "AudioKit Debug");
        }

        private void DrawWindow(int windowId)
        {
            if (GUILayout.Button("关闭")) Hide();
            AudioKitDiagnostics.Draw(ref mScrollPosition);
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
#endif
    }
}
