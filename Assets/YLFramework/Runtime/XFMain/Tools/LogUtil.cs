// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Tools/LogUtil.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.

using System;
using System.Collections.Generic; 
using System.IO; 
using System.Text;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace YLFramework.Core
{
  
    public class LogUtil
    {


        #region 单例

        private static LogUtil _instance;

        internal static LogUtil Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new LogUtil();
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif

                    Application.logMessageReceived -= OnReceiveLog;
                    Application.logMessageReceived += OnReceiveLog;
                    CheckLogFileDate();
                     
#if !LOG_UTIL_WRITE_TO_FILE_DISABLE
                    LogFilePath = string.Format("{0}/player_log_{1}.txt", LogFileDirectory, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
#endif

                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


#if UNITY_EDITOR

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            // Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 
            // 该选项如果禁用掉,重新运行时静态数据不会清空,可能会导致异常,所以这里置空,防止异常
            switch (obj)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _instance = null;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _instance = null;
                    break;
            }
        }
#endif
         
        private LogUtil()
        {
        }

        #endregion

        #region 字段

        private StringBuilder tempStringBuilder = new StringBuilder();
        private Queue<string> waitToWriteFileLogs = new Queue<string>();
        private bool writingFile = false;
        private Task writeToFileTask = null;
        private bool _isStopWriteToFile = false;
        private StreamWriter _writer = null;
        private Action onException;
        private string logFilePath;
        #endregion

        #region 静态属性

        private static StringBuilder TempStringBuilder => Instance.tempStringBuilder;
        private static Queue<string> WaitToWriteFileLogs => Instance.waitToWriteFileLogs;
        private static bool WritingFile
        {
            get 
            {
                return Instance.writingFile;
            }
            set {
                Instance.writingFile = value;
            }
        }
        private static Task WriteToFileTask
        {
            get {
                return Instance.writeToFileTask;
            }
            set {
                Instance.writeToFileTask = value;
            }
        }
        private static bool isStopWriteToFile
        {
            get {
                return Instance._isStopWriteToFile;
            }
            set {
                Instance._isStopWriteToFile = value;
            }
        }
        private static StreamWriter writer
        {
            get 
            {
                return Instance._writer;
            }
            set 
            {
                Instance._writer = value;
            }
        }


        /// <summary>
        /// 日志文件所在的目录
        /// </summary>
        public static string LogFileDirectory => string.Format("{0}/YLFramework/Core/Logs", Application.temporaryCachePath);

        /// <summary>
        /// 当前进程写入的日志文件路径
        /// </summary>
        public static string LogFilePath
        {
            get 
            { 
                return Instance.logFilePath;
            } 
            private set
            {
                Instance.logFilePath = value;
            }
        }

        #endregion

        #region 静态事件

        /// <summary>
        /// 当有异常时触发(Debug.unityLogger.logEnabled需为打开状态，否则该事件不会触发)
        /// </summary>
        public static Action OnException => Instance.onException;
        #endregion

        #region 静态方法

        [Obsolete("请使用拓展包: 日志系统(LoggerSystem) 代替! 拓展包管理窗口: Window/XFKT/XFGameFramwork/拓展包")]
        public static void Log(string message, params object[] args)
        {

#if !LOG_UTIL_WRITE_TO_CONSOLE_DISABLE
            if (args == null || args.Length == 0)
                Debug.Log(message);
            else
                Debug.LogFormat(message, args);
#endif

#if !LOG_UTIL_WRITE_TO_FILE_DISABLE
            if (isStopWriteToFile) return;
            TempStringBuilder.Clear();
            TempStringBuilder.Append("[").Append(DateTime.Now.ToString()).Append("]").Append("[Log]").Append(string.Format(message,args)).AppendLine();
            ReadyWriteToFile(TempStringBuilder.ToString());
#endif

        }

        [Obsolete("请使用拓展包: 日志系统(LoggerSystem) 代替! 拓展包管理窗口: Window/XFKT/XFGameFramwork/拓展包")]
        public static void LogWarning(string message, params object[] args)
        {

#if !LOG_UTIL_WRITE_TO_CONSOLE_DISABLE
            if (args == null || args.Length == 0)
                Debug.LogWarning(message);
            else
                Debug.LogWarningFormat(message, args);
#endif

#if !LOG_UTIL_WRITE_TO_FILE_DISABLE
            if (isStopWriteToFile) return;
            TempStringBuilder.Clear();
            TempStringBuilder.Append("[").Append(DateTime.Now.ToString()).Append("]").Append("[Warning]").Append(string.Format(message, args)).AppendLine();
            ReadyWriteToFile(TempStringBuilder.ToString());
#endif
        }

        [Obsolete("请使用拓展包: 日志系统(LoggerSystem) 代替! 拓展包管理窗口: Window/XFKT/XFGameFramwork/拓展包")]
        public static void LogError(string message, params object[] args)
        {

#if !LOG_UTIL_WRITE_TO_CONSOLE_DISABLE
            if (args == null || args.Length == 0)
                Debug.LogError(message);
            else
                Debug.LogErrorFormat(message, args);
#endif

#if !LOG_UTIL_WRITE_TO_FILE_DISABLE
            if (isStopWriteToFile) return;
            TempStringBuilder.Clear();
            TempStringBuilder.Append("[").Append(DateTime.Now.ToString()).Append("]").Append("[Error]").Append(string.Format(message, args)).AppendLine();
            ReadyWriteToFile(TempStringBuilder.ToString());
#endif

        }

        public static void StopWriteToFile()
        {
            try
            {
                isStopWriteToFile = true;
                WaitToWriteFileLogs.Clear();
                if (writer != null)
                {
                    writer.Close();
                    writer = null;
                }
            }
            catch (Exception)
            {
            }

        }


        /// <summary>
        /// 检测日志文件是否超过三天，如果超过三天就清理掉
        /// </summary>
        private static void CheckLogFileDate()
        {
            if (!Directory.Exists(LogFileDirectory)) return;

            // 异步检测 防止影响游戏启动时间 
            Task.Run(() =>
            {
                string[] files = Directory.GetFiles(LogFileDirectory);
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        DateTime createTime = File.GetCreationTime(files[i]);
                        if ((DateTime.Now - createTime).Days > 3)
                        {
                            File.Delete(files[i]);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            });
        }

        private static void OnReceiveLog(string condition, string stackTrace, LogType type)
        {
            // 这里只关心报错信息
            if (type != LogType.Exception) return;

#if !LOG_UTIL_WRITE_TO_FILE_DISABLE
            if (isStopWriteToFile) return;
            TempStringBuilder.Clear();
            TempStringBuilder.Append("[").Append(DateTime.Now.ToString()).Append("]").Append("[").Append(type.ToString()).Append("]").Append(condition).AppendLine();
            TempStringBuilder.AppendLine(stackTrace);
            ReadyWriteToFile(TempStringBuilder.ToString());
#endif

#if !UNITY_EDITOR
            OnException?.Invoke(); // 如果抛异常并且非Editor模式，触发事件
#endif

        }

        private static void StartWriteToFile()
        {

#if !LOG_UTIL_WRITE_TO_FILE_DISABLE

            if (WritingFile) return;
            WritingFile = true;
            // 需要监听编辑器状态 关掉任务
            WriteToFileTask = Task.Run(() =>
            { 
                try
                {
                    string dir = Path.GetDirectoryName(LogFilePath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    writer = File.AppendText(LogFilePath);
                    while (WaitToWriteFileLogs.Count > 0)
                    {
                        writer.WriteLine(WaitToWriteFileLogs.Dequeue());
                        writer.Flush();
                    }
                    writer.Close();
                }
                catch (Exception)
                {
                }

                writer = null;
                WritingFile = false;
                WriteToFileTask = null;
            });

#endif

        }

        private static void ReadyWriteToFile(string message)
        {
            if (isStopWriteToFile) return;
#if !LOG_UTIL_WRITE_TO_FILE_DISABLE

            try
            {
                // 写入文件
                WaitToWriteFileLogs.Enqueue(message);
                // 开始写入
                StartWriteToFile();
            }
            catch (Exception)
            {
            }
#endif 
        }
        #endregion


    } 
}


