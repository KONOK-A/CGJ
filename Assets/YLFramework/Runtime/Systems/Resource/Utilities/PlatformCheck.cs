using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 平台判断工具。
    /// <para>
    /// 除 <see cref="IsWebGL"/> 外均基于编译宏：结果取决于编译时定义的平台符号，
    /// 编辑器下跟随当前构建目标（例如把构建目标切到 Android 时 <see cref="IsAndroid"/> 同样为 true）。
    /// <see cref="IsWebGL"/> 基于运行时 <see cref="Application.platform"/>，编辑器下恒为 false。
    /// </para>
    /// </summary>
    public static class PlatformCheck
    {
        /// <summary>是否为 Android 平台（编译宏 UNITY_ANDROID）。</summary>
        public static bool IsAndroid
        {
            get
            {
                bool retValue = false;
#if UNITY_ANDROID
                retValue = true;
#endif
                return retValue;
            }
        }

        /// <summary>是否在 Unity 编辑器环境（编译宏 UNITY_EDITOR）。</summary>
        public static bool IsEditor
        {
            get
            {
                bool retValue = false;
#if UNITY_EDITOR
                retValue = true;
#endif
                return retValue;
            }
        }

        /// <summary>是否为 iOS 平台（编译宏 UNITY_IOS）。</summary>
        public static bool IsiOS
        {
            get
            {
                bool retValue = false;
#if UNITY_IOS
				retValue = true;
#endif
                return retValue;
            }
        }

        /// <summary>是否为 Standalone（桌面）平台（编译宏 UNITY_STANDALONE）。</summary>
        public static bool IsStandardAlone
        {
            get
            {
                bool retValue = false;
#if UNITY_STANDALONE
                retValue = true;
#endif
                return retValue;
            }
        }

        /// <summary>是否为 Windows（含 Windows 编辑器，编译宏 UNITY_EDITOR_WIN / UNITY_STANDALONE_WIN）。</summary>
        public static bool IsWin
        {
            get
            {
                bool retValue = false;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                retValue = true;
#endif
                return retValue;
            }
        }

        /// <summary>是否为 macOS（含 macOS 编辑器，编译宏 UNITY_EDITOR_OSX / UNITY_STANDALONE_OSX）。</summary>
        public static bool IsOSX
        {
            get
            {
                bool retValue = false;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                retValue = true;
#endif
                return retValue;
            }
        }

        /// <summary>是否运行在 WebGL 播放器（运行时判断，编辑器下恒为 false）。</summary>
        public static bool IsWebGL => Application.platform == RuntimePlatform.WebGLPlayer;
    }
}