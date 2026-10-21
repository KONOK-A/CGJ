using System;
using System.IO;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Logging
{
    /// <summary>显式主线程启用。仅持有自己的handler和宿主，默认无自动创建/写盘/接管。</summary>
    public sealed class LoggerManager : MonoBehaviour
    {
        private static LoggerManager _host;
        private static LoggerSession _current;
        private LoggerSession _session;
        private ILogHandler _previous;

        static LoggerManager() => SingletonSessionState.RegisterSessionReset(Disable);
        public static bool IsEnabled => _current != null;
        public static LoggerSession Current => _current;

        /// <summary>当前会话重复启用返回同一对象，不重复安装。更换配置先Disable。</summary>
        public static LoggerSession Enable(LoggerOptions options = null)
        {
            if (!SingletonSessionState.CanRunExecution) throw new InvalidOperationException("Logger只能在有效Play会话中显式启用。");
            if (_current != null) return _current;
            options ??= new LoggerOptions();
            var resolved = new LoggerOptions
            {
                WriteToFile = options.WriteToFile,
                Directory = options.Directory ?? Path.Combine(Application.persistentDataPath, "YLFramework", "Logs"),
                ConsoleFilter = options.ConsoleFilter,
                FileFilter = options.FileFilter
            };
            var previous = Debug.unityLogger.logHandler;
            var session = new LoggerSession(previous, resolved);
            GameObject obj = null;
            try
            {
                obj = new GameObject("YLFramework.Logging.LoggerManager");
                var host = obj.AddComponent<LoggerManager>();
                host._previous = previous;
                host._session = session;
                DontDestroyOnLoad(obj);
                _host = host;
                _current = session;
                Debug.unityLogger.logHandler = session;
                return session;
            }
            catch
            {
                session.Dispose();
                if (obj) Destroy(obj);
                throw;
            }
        }

        public static void Disable()
        {
            var host = _host;
            _host = null;
            if (host)
            {
                host.Release();
                Destroy(host.gameObject);
            }
            else
            {
                _current?.Dispose();
                _current = null;
            }
        }

        private void Release()
        {
            var session = _session;
            if (session == null) return;
            _session = null;
            if (ReferenceEquals(Debug.unityLogger.logHandler, session)) Debug.unityLogger.logHandler = _previous;
            session.Dispose();
            if (ReferenceEquals(_current, session)) _current = null;
            if (ReferenceEquals(_host, this)) _host = null;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();
        private void OnApplicationQuit() => Release();
    }
}
