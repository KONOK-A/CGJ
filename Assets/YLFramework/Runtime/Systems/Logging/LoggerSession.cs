// Copyright (c) 2025 弦风课堂. MIT License, see LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace YLFramework.Logging
{
    /// <summary>
    /// 同步过滤/转发/事件/文件会话，不自行安装Unity handler。
    /// 回调在原日志线程顺序执行，不可等待另一条会记录日志的线程；嵌套日志丢弃并计数。
    /// </summary>
    public sealed class LoggerSession : ILogHandler, IDisposable
    {
        [ThreadStatic] private static int _depth;
        private readonly object _gate = new();
        private readonly ILogHandler _destination;
        private readonly bool[] _console;
        private readonly bool[] _file;
        private readonly bool _writeToFile;
        private readonly string _directory;
        private StreamWriter _writer;
        private bool _fileFailed;
        private bool _disposed;
        private Action<LogType, string> _onLog;
        private Exception _lastError;
        private long _recursiveCount;
        private string _logFilePath;

        public LoggerSession(ILogHandler destination, LoggerOptions options)
        {
            _destination = destination ?? throw new ArgumentNullException(nameof(destination));
            if (options == null) throw new ArgumentNullException(nameof(options));
            _console = Snapshot(options.ConsoleFilter);
            _file = Snapshot(options.FileFilter);
            _writeToFile = options.WriteToFile;
            if (_writeToFile && string.IsNullOrWhiteSpace(options.Directory))
                throw new ArgumentException("文件日志需要显式输出目录。", nameof(options));
            _directory = options.Directory;
        }

        public string LogFilePath { get { lock (_gate) return _logFilePath; } }
        public Exception LastError { get { lock (_gate) return _lastError; } }
        public long SuppressedRecursiveCount => Interlocked.Read(ref _recursiveCount);
        public bool IsDisposed { get { lock (_gate) return _disposed; } }
        public event Action<LogType, string> OnLogEvent
        {
            add { lock (_gate) { if (_disposed) throw new ObjectDisposedException(nameof(LoggerSession)); _onLog += value; } }
            remove { lock (_gate) _onLog -= value; }
        }

        private static bool[] Snapshot(Dictionary<LogType, bool> filter)
        {
            var result = new bool[5];
            for (var i = 0; i < result.Length; i++) result[i] = filter == null || filter.TryGetValue((LogType)i, out var enabled) && enabled;
            return result;
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            Dispatch(LogType.Exception, context, null, null, exception);
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            Dispatch(logType, context, format, args, null);
        }

        private void Dispatch(LogType type, UnityEngine.Object context, string format, object[] args, Exception exception)
        {
            if (_depth != 0) { Interlocked.Increment(ref _recursiveCount); return; }
            _depth++;
            try
            {
                lock (_gate)
                {
                    string message;
                    try { message = exception != null ? exception.ToString() : string.Format(CultureInfo.CurrentCulture, format ?? string.Empty, args ?? Array.Empty<object>()); }
                    catch (FormatException error)
                    {
                        _lastError = error;
                        message = (format ?? string.Empty) + " [Logger: invalid format]";
                    }
                    var index = (int)type;
                    var knownType = index >= 0 && index < _console.Length;
                    // 后续handler可能仍串联本对象。关闭后只透明转发，不重开writer/事件。
                    if (_disposed || knownType && _console[index])
                    {
                        try
                        {
                            if (exception != null) _destination.LogException(exception, context);
                            else _destination.LogFormat(type, context, "{0}", message);
                        }
                        catch (Exception error) { _lastError = error; }
                    }
                    if (_disposed) return;
                    if (knownType && _writeToFile && _file[index] && !_fileFailed) WriteToFile(type, message);
                    var listeners = _onLog;
                    if (listeners == null) return;
                    foreach (Action<LogType, string> listener in listeners.GetInvocationList())
                    {
                        if (_disposed) break;
                        try { listener(type, message); }
                        catch (Exception error) { _lastError = error; }
                    }
                }
            }
            finally { _depth--; }
        }

        private void WriteToFile(LogType type, string message)
        {
            try
            {
                if (_writer == null)
                {
                    System.IO.Directory.CreateDirectory(_directory);
                    var path = Path.Combine(_directory, "player_log_" + DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss-fffffff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N") + ".txt");
                    _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                    _logFilePath = path;
                }
                _writer.WriteLine("[{0:O}]\t[{1}]\t{2}", DateTime.UtcNow, type, message);
                _writer.Flush();
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                _lastError = error;
                _fileFailed = true;
                ReleaseWriter();
            }
        }

        private void ReleaseWriter()
        {
            var writer = _writer;
            _writer = null;
            try { writer?.Dispose(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { _lastError = error; }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _onLog = null;
                ReleaseWriter();
            }
        }
    }
}
