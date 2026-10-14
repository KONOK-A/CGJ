using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// AssetBundle 相关的路径与平台工具。
    /// <para>
    /// 目录约定：热更后的资源位于 persistentDataPath/AssetBundles/&lt;Platform&gt;/，
    /// 随包资源位于 StreamingAssets/AssetBundles/&lt;Platform&gt;/；本类提供两者的根路径与拼接工具。
    /// </para>
    /// </summary>
    public class AssetBundleHelper
    {
#if UNITY_EDITOR
        // 编辑器构建流程按 BuildTarget 取平台名；运行时用下方 RuntimePlatform 版本。
        private static string GetPlatformForAssetBundles(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.Android => "Android",
                BuildTarget.WSAPlayer => "WSAPlayer",
                BuildTarget.iOS => "iOS",
                BuildTarget.WebGL => "WebGL",
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneLinux64 => "Linux",
                BuildTarget.StandaloneOSX => "OSX",
                _ => null
            };
        }
#endif
        
        /// <summary>优先返回 persistentDataPath/Res 下存在的文件路径，不存在时返回 StreamingAssets 下的路径。</summary>
        public static string GetResPathInPersistentOrStream(string relativePath)
        {
            var resPersistentPath = $"{PersistentDataPathForRes}{relativePath}";
            if (File.Exists(resPersistentPath))
            {
                return resPersistentPath;
            }
            return StreamingAssetsPath + relativePath;
        }

        
        private static string _persistentDataPath;
        private static string _streamingAssetsPath;
        private static string _persistentDataPathForRes;
        private static string _persistentDataPathForPhoto;

        // 外部目录  
        /// <summary>persistentDataPath 加尾部斜杠（首次访问后缓存）。</summary>
        public static string PersistentDataPath => _persistentDataPath ??= Application.persistentDataPath + "/";

        // 内部目录
        /// <summary>StreamingAssets 路径加尾部斜杠（首次访问后缓存）。</summary>
        public static string StreamingAssetsPath
        {
            get
            {
                if (null == _streamingAssetsPath)
                {
#if UNITY_IPHONE && !UNITY_EDITOR
					_streamingAssetsPath = Application.streamingAssetsPath + "/";
#elif UNITY_ANDROID && !UNITY_EDITOR
					_streamingAssetsPath = Application.streamingAssetsPath + "/";
#elif (UNITY_STANDALONE_WIN) && !UNITY_EDITOR
					_streamingAssetsPath =
 Application.streamingAssetsPath + "/";//GetParentDir(Application.dataPath, 2) + "/BuildRes/";
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
					_streamingAssetsPath = Application.streamingAssetsPath + "/";
#else
                    _streamingAssetsPath = Application.streamingAssetsPath + "/";
#endif
                }
                return _streamingAssetsPath;
            }
        }


        // 外部头像缓存目录
        /// <summary>图片本地缓存目录（persistentDataPath/Photos/），首次访问时自动创建。</summary>
        public static string PersistentDataPathForPhoto
        {
            get
            {
                if (null == _persistentDataPathForPhoto)
                {
                    _persistentDataPathForPhoto = PersistentDataPath + "Photos\\";

                    if (!Directory.Exists(_persistentDataPathForPhoto))
                    {
                        Directory.CreateDirectory(_persistentDataPathForPhoto);
                    }
                }

                return _persistentDataPathForPhoto;
            }
        }

        // 外部资源目录
        /// <summary>
        /// 热更资源根目录（persistentDataPath/Res/），首次访问时自动创建；
        /// iOS 非编辑器环境下同时把该目录标记为不参与 iCloud 备份。
        /// </summary>
        public static string PersistentDataPathForRes
        {
            get
            {
                if (null == _persistentDataPathForRes)
                {
                    _persistentDataPathForRes = PersistentDataPath + "Res/";

                    if (!Directory.Exists(_persistentDataPathForRes))
                    {
                        Directory.CreateDirectory(_persistentDataPathForRes);
#if UNITY_IPHONE && !UNITY_EDITOR
						UnityEngine.iOS.Device.SetNoBackupFlag(mPersistentDataPath4Res);
#endif
                    }
                }

                return _persistentDataPathForRes;
            }
        }
        /// <summary>
        /// 当前平台目录名：编辑器下取自 activeBuildTarget，运行时取自 Application.platform；未支持平台返回 null。
        /// </summary>
        public static string GetPlatformName()
        {
#if UNITY_EDITOR
            return GetPlatformForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
#else
			return GetPlatformForAssetBundles(UnityEngine.Application.platform);
#endif
        }
        
        /// <summary>把运行时平台映射为平台目录名（Windows / OSX / Linux / Android / iOS / WebGL / WSAPlayer）；未支持平台返回 null。</summary>
        public static string GetPlatformForAssetBundles(RuntimePlatform platform)
        {
            return platform switch
            {
                RuntimePlatform.Android => "Android",
                RuntimePlatform.WSAPlayerARM or RuntimePlatform.WSAPlayerX64
                    or RuntimePlatform.WSAPlayerX86 => "WSAPlayer",
                RuntimePlatform.IPhonePlayer => "iOS",
                RuntimePlatform.WebGLPlayer => "WebGL",
                RuntimePlatform.WindowsPlayer => "Windows",
                RuntimePlatform.OSXPlayer => "OSX",
                RuntimePlatform.LinuxPlayer => "Linux",
                _ => null
            };
        }
        
        /// <summary>通过 AssetDatabase 查询指定 Bundle 中某资产名的工程路径；仅编辑器可用，其他环境返回 null。</summary>
        public static string[] GetAssetPathsFromAssetBundleAndAssetName(string abRAssetName, string assetName)
        {
#if UNITY_EDITOR
            return AssetDatabase.GetAssetPathsFromAssetBundleAndAssetName(abRAssetName, assetName);
#else
            return null;
#endif
        }

        /// <summary>AssetDatabase 加载资产；仅编辑器可用，其他环境返回 null。</summary>
        public static Object LoadAssetAtPath(string assetPath, Type assetType)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath(assetPath, assetType);
#else
            return null;
#endif
        }

        /// <summary>AssetDatabase 加载资产；仅编辑器可用，其他环境返回 null。</summary>
        public static T LoadAssetAtPath<T>(string assetPath) where T : Object
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
#else
            return null;
#endif
        }
        
        
        
// 上一级目录
        /// <summary>返回目录的上层目录（floor 层，按 '/' 分割）。</summary>
        public static string GetParentDir(string dir, int floor = 1)
        {
            var subDir = dir;

            for (var i = 0; i < floor; ++i)
            {
                var last = subDir.LastIndexOf('/');
                subDir = subDir[..last];
            }

            return subDir;
        }

        /// <summary>
        /// 在 dirName 下递归查找文件名匹配的文件，把绝对路径追加到 outResult（跳过系统目录；outResult 为 null 时忽略）。
        /// </summary>
        public static void GetFileInFolder(string dirName, string fileName, List<string> outResult)
        {
            if (outResult == null)
            {
                return;
            }

            var dir = new DirectoryInfo(dirName);

            if (null != dir.Parent && dir.Attributes.ToString().IndexOf("System", StringComparison.Ordinal) > -1)
            {
                return;
            }

            var fileInfos = dir.GetFiles(fileName);
            outResult.AddRange(fileInfos.Select(fileInfo => fileInfo.FullName));

            var dirInfos = dir.GetDirectories();
            foreach (var dirInfo in dirInfos)
            {
                GetFileInFolder(dirInfo.FullName, fileName, outResult);
            }
        }

        /// <summary>文件 URL 前缀：编辑器与 iOS 平台为 <c>file://</c>，其他平台为空。</summary>
        public static string PathPrefix
        {
            get
            {
#if UNITY_EDITOR || UNITY_IOS
                return "file://";
#else
                return string.Empty;
#endif
            }
        }
#if UNITY_EDITOR
        private const string KSimulateAssetBundles = "SimulateAssetBundles"; //此处跟editor中保持统一，不能随意更改

        /// <summary>
        /// 编辑器模拟模式开关：通过 EditorPrefs（key <c>SimulateAssetBundles</c>）持久化，未设置时默认 true。
        /// 开启后资源从 AssetDatabase 加载而不读取真实 Bundle 文件。
        /// </summary>
        public static bool SimulationMode
        {
            get => EditorPrefs.GetBool(KSimulateAssetBundles, true);
            set => EditorPrefs.SetBool(KSimulateAssetBundles, value);
        }
#else
        /// <summary>非编辑器环境恒为 false，setter 无效果。</summary>
         public static bool SimulationMode
         {
             get { return false; }
             set {  }
         }
#endif
    }
}