using System;
using System.Collections.Generic;

namespace YLFramework.Localization
{
    // 比较真实引用和键而不是数量/hash；列表同长度替换、原条目改ID均使缓存失效。
    internal sealed class LocalizationEntryIndex
    {
        private readonly Dictionary<int, LanguageInfo> _index = new();
        private LanguageInfo[] _entries = Array.Empty<LanguageInfo>();
        private int[] _ids = Array.Empty<int>();
        private bool _isValid;

        public LanguageInfo Find(IList<LanguageInfo> source, int id, string context)
        {
            if (!Matches(source))
            {
                Rebuild(source, context);
            }

            return _index.TryGetValue(id, out var value) ? value : null;
        }

        public void Clear()
        {
            _isValid = false;
            _index.Clear();
            _entries = Array.Empty<LanguageInfo>();
            _ids = Array.Empty<int>();
        }

        private bool Matches(IList<LanguageInfo> source)
        {
            if (!_isValid || source == null || _entries.Length != source.Count)
            {
                return false;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                if (!ReferenceEquals(_entries[i], source[i]) || source[i] == null || _ids[i] != source[i].id)
                {
                    return false;
                }
            }

            return true;
        }

        private void Rebuild(IList<LanguageInfo> source, string context)
        {
            Clear();
            if (source == null)
            {
                throw new InvalidOperationException($"Localization '{context}': entry list is null.");
            }

            var next = new LanguageInfo[source.Count];
            var keys = new int[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null)
                {
                    throw new InvalidOperationException($"Localization '{context}': null entry at index {i}.");
                }

                if (_index.ContainsKey(entry.id))
                {
                    throw new InvalidOperationException($"Localization '{context}': duplicate ID {entry.id} at index {i}.");
                }

                _index.Add(entry.id, entry);
                next[i] = entry;
                keys[i] = entry.id;
            }
            _entries = next;
            _ids = keys;
            _isValid = true;
        }
    }
}
