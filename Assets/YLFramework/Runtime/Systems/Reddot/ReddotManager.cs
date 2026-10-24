using System;
using System.Collections.Generic;
using YLFramework.Singletons;
using YLFramework.Events;

namespace YLFramework.RedDots
{
    /// <summary>主线程路径红点。普通路径为集合，独立来源持有自己的释放凭据。</summary>
    public static class ReddotManager
    {
        private static readonly HashSet<string> _paths = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> _sources = new(StringComparer.Ordinal);
        private static EventChannel _changed = new();
        private static long _generation;

        static ReddotManager() => SingletonSessionState.RegisterSessionReset(ResetSession);

        /// <summary>只读快照，不能绕过通知修改内部集合。</summary>
        public static IReadOnlyList<string> ReddotPath
        {
            get
            {
                var paths = new HashSet<string>(_paths, StringComparer.Ordinal);
                paths.UnionWith(_sources.Keys);
                var result = new List<string>(paths);
                result.Sort(StringComparer.Ordinal);
                return result.AsReadOnly();
            }
        }

        public static IUnregister RegisterChanged(Action callback) => _changed.Register(callback);

        /// <summary>斜杠分段，忽略空段与段外空白；空路径不表示根节点。</summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var segments = path.Split('/');
            var result = new List<string>(segments.Length);
            foreach (var segment in segments)
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    result.Add(segment.Trim());
                }
            }

            return result.Count == 0 ? string.Empty : string.Join("/", result) + "/";
        }

        public static void AddPath(string path)
        {
            path = NormalizePath(path);
            if (path.Length != 0 && _paths.Add(path))
            {
                _changed.TriggerAll();
            }
        }

        public static void AddPath(IList<string> paths)
        {
            if (paths == null)
            {
                return;
            }

            var changed = false;
            foreach (var value in paths)
            {
                var path = NormalizePath(value);
                if (path.Length != 0)
                {
                    changed |= _paths.Add(path);
                }
            }
            if (changed)
            {
                _changed.TriggerAll();
            }
        }

        /// <summary>只撤销AddPath的集合来源，不夺取其他owner的登记。</summary>
        public static void RemovePath(string path)
        {
            if (_paths.Remove(NormalizePath(path)))
            {
                _changed.TriggerAll();
            }
        }

        /// <summary>同路径可以独立登记多次；owner退出必须Dispose，Reset会使旧凭据失效。</summary>
        public static IDisposable RegisterPath(string path)
        {
            path = NormalizePath(path);
            if (path.Length == 0)
            {
                throw new ArgumentException("红点路径至少需要一个非空段。", nameof(path));
            }

            _sources.TryGetValue(path, out var count);
            _sources[path] = count + 1;
            var registration = new Registration(path, _generation);
            try
            {
                _changed.TriggerAll();
            }
            catch (Exception error)
            {
                // 登记未能交付给owner，不能遗留无法归还的来源。
                try
                {
                    registration.Dispose();
                }
                catch (Exception correction)
                {
                    throw new AggregateException("红点登记及回滚通知失败。", error, correction);
                }
                throw;
            }
            return registration;
        }

        public static bool ShouldBeDisplayed(string path)
        {
            path = NormalizePath(path);
            if (path.Length == 0)
            {
                return false;
            }

            foreach (var item in _paths)
            {
                if (item.StartsWith(path, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (var item in _sources.Keys)
            {
                if (item.StartsWith(path, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Reset()
        {
            var changed = _paths.Count != 0 || _sources.Count != 0;
            _generation++;
            _paths.Clear();
            _sources.Clear();
            if (changed)
            {
                _changed.TriggerAll();
            }
        }

        private static void ResetSession()
        {
            _generation++;
            _paths.Clear();
            _sources.Clear();
            _changed.Dispose();
            _changed = new EventChannel();
        }

        private sealed class Registration : IDisposable
        {
            private string _path;
            private readonly long _generation;
            internal Registration(string path, long generation)
            {
                _path = path;
                _generation = generation;
            }
            public void Dispose()
            {
                var path = _path;
                _path = null;
                if (path == null || _generation != ReddotManager._generation)
                {
                    return;
                }

                if (_sources[path] == 1)
                {
                    _sources.Remove(path);
                }
                else
                {
                    _sources[path]--;
                }

                _changed.TriggerAll();
            }
        }
    }
}
