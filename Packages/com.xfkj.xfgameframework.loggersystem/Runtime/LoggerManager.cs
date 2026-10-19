using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace XFGameFramework.LoggerSystem
{
  
    public class LoggerHandler : ILogHandler
    { 

        public void LogException(Exception exception, UnityEngine.Object context)
        {

            bool isOpen = false;
            // 判断 Exception 日志是否开启
            if (LoggerManager.FilterLogType.TryGetValue(LogType.Exception, out isOpen) && isOpen )
                LoggerManager.UnityLogHandler.LogException(exception, context);

            isOpen = false;

            // 判断 Exception 日志是否输出到文件
            if (LoggerManager.FilterLogFileType.TryGetValue(LogType.Exception,out isOpen) && isOpen)
                LoggerManager.WirteToFile(LogType.Exception, exception.ToString());

            // 触发事件
            LoggerManager.OnLogEvent?.Invoke(LogType.Exception, exception.ToString());
        }
         
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {

            bool isOpen = false;

            // 判断该类型的日志是否开启
            if (LoggerManager.FilterLogType.TryGetValue(logType,out isOpen) && isOpen )
                LoggerManager.UnityLogHandler.LogFormat(logType, context, format,args);
             
            isOpen = false;

            // 判断该类型的日志是否开启
            if (LoggerManager.FilterLogFileType.TryGetValue(logType,out isOpen) && isOpen )
                LoggerManager.WirteToFile(logType, string.Format(format,args));

            // 触发事件
            LoggerManager.OnLogEvent?.Invoke(logType, string.Format(format, args));
        } 

    }


    // Fixed 编码

    public class LoggerManager : MonoBehaviour
    {

        #region 单例

        private static LoggerManager _instance;

        internal static LoggerManager Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (!_instance)
                {
                    Init();
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }
         
        #endregion


        #region 字段
         
        internal ILogHandler unityLogHandler;
        private LoggerHandler installedLogHandler;
         
        [Tooltip("需要输出的日志类型(默认所有的日志都会输出)")]
        private Dictionary<LogType,bool> filterLogType = null;

        [Tooltip("需要输出到文件的日志类型(默认所有的日志都会输出)")]
        private Dictionary<LogType,bool> filterLogFileType = null;

        #endregion

        #region 初始化

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitOnLoad() 
        {
            Init();
        }
        
        private static void Init()
        {
#if UNITY_EDITOR 
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return; 
#endif
            if (_instance) return;
              
            GameObject instance = new GameObject(typeof(LoggerManager).Name);
            DontDestroyOnLoad(instance);
            _instance = instance.AddComponent<LoggerManager>();

            if (Debug.unityLogger.logHandler.GetType() != typeof(LoggerHandler))
            {
                _instance.unityLogHandler = Debug.unityLogger.logHandler;
                _instance.installedLogHandler = new LoggerHandler();
                Debug.unityLogger.logHandler = _instance.installedLogHandler;
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 日志输出事件(抛出异常时也会触发),即使把所有类型的日志输出都关闭,只要监听该事件,当输出日志时就会触发,如果不希望事件触发,置为空即可!(默认为空)
        /// </summary>
        [Tooltip("日志输出事件(抛出异常时也会触发),即使把所有类型的日志输出都关闭,只要监听该事件,当输出日志时就会触发,如果不希望事件触发,置为空即可!(默认为空)")]
        private Action<LogType, string> onLogEvent = null;

        #endregion

        #region 属性

        /// <summary>
        /// 需要输出的日志类型(默认所有的日志都会输出)
        /// </summary>

        public static Dictionary<LogType,bool> FilterLogType
        {
            get 
            {
                if (Instance.filterLogType == null) 
                {
                    Instance.filterLogType = new Dictionary<LogType, bool>()
                    {
                        // 默认所有的日志都会输出
                        { LogType.Log, true }, 
                        { LogType.Warning, true }, 
                        { LogType.Error, true }, 
                        { LogType.Exception, true }, 
                        { LogType.Assert, true },
                    }; 
                }

                return Instance.filterLogType;
            }
            
            set 
            {
                Instance.filterLogType = value;
            } 
        }

        /// <summary>
        /// 需要输出到文件的日志类型(默认所有的日志都会输出)
        /// </summary>
        public static Dictionary<LogType,bool> FilterLogFileType
        {
            get
            {

                if (Instance.filterLogFileType == null)
                {
                    Instance.filterLogFileType = new Dictionary<LogType, bool>()
                    {
                        // 默认所有的日志都会输出
                        { LogType.Log, true },
                        { LogType.Warning, true },
                        { LogType.Error, true },
                        { LogType.Exception, true },
                        { LogType.Assert, true },
                    };
                }
                 
                return Instance.filterLogFileType;
            }

            set
            {
                Instance.filterLogFileType = value;
            }

        }


        internal static ILogHandler UnityLogHandler => Instance.unityLogHandler;

        /// <summary>
        /// 日志输出事件(抛出异常时也会触发),即使把所有类型的日志输出都关闭,只要监听该事件,当输出日志时就会触发,如果不希望事件触发,置为空即可!(默认为空)
        /// </summary>
        public static Action<LogType, string> OnLogEvent
        {
            get
            {
                return Instance.onLogEvent;
            }
            set 
            {
                Instance.onLogEvent = value;
            }
        }

        private static StreamWriter streamWriter;

        /// <summary>
        /// 日志文件所在的目录
        /// </summary>
        public static string LogFileDirectory { get;private set; } = string.Format("{0}/Logs", Application.temporaryCachePath);

        /// <summary>
        /// 日志文件路径
        /// </summary>
        public static string LogFilePath
        {
            get;
            private set;
        }

        #endregion

        #region 方法

        internal static void WirteToFile(LogType logType,string message)
        { 
            try
            {
                if (streamWriter == null)
                {
                    if (!Directory.Exists(LogFileDirectory))
                        Directory.CreateDirectory(LogFileDirectory);

                    LogFilePath = string.Format("{0}/player_log_{1}.txt", LogFileDirectory, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
                    streamWriter = File.AppendText(LogFilePath);
                }

                streamWriter.WriteLine(string.Format("[{0}]\t[{1}]\t{2}",DateTime.Now.ToString(), logType, message));
                streamWriter.WriteLine(); // 换行
                streamWriter.Flush();
            }
            catch (Exception)
            {
                 // 写入文件异常 这里就不处理了 防止循环调用
            }
            
        }

        #endregion


        #region 生命周期

        private void OnEnable()
        {
            if (installedLogHandler != null && ReferenceEquals(Debug.unityLogger.logHandler, unityLogHandler))
                Debug.unityLogger.logHandler = installedLogHandler;
        }

        private void OnDisable()
        {
            // 在场景销毁前归还接管的 handler，后续销毁日志不能再惰性创建 LoggerManager。
            if (installedLogHandler != null && ReferenceEquals(Debug.unityLogger.logHandler, installedLogHandler))
                Debug.unityLogger.logHandler = unityLogHandler;
        }

        private void OnDestroy()
        {
            OnDisable();
            if (!ReferenceEquals(_instance, this)) return;
            _instance = null;
            // 禁用 Domain Reload 时静态字段仍在，不能把已释放 writer 留给下一执行代次。
            var writer = streamWriter;
            streamWriter = null;
            if (writer != null) writer.Dispose();
        }

        #endregion


    }

}

