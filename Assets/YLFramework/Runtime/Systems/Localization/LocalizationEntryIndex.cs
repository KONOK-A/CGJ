using System;
using System.Collections.Generic;

namespace YLFramework.Localization
{
    // 比较真实引用和键而不是数量/hash；列表同长度替换、原条目改ID均使缓存失效。
    internal sealed class LocalizationEntryIndex
    {
        private readonly Dictionary<int, LanguageInfo> index = new();
        private LanguageInfo[] entries = Array.Empty<LanguageInfo>();
        private int[] ids = Array.Empty<int>();
        private bool valid;

        public LanguageInfo Find(IList<LanguageInfo> source, int id, string context)
        {
            if (!Matches(source)) Rebuild(source, context);
            return index.TryGetValue(id, out var value) ? value : null;
        }

        public void Clear()
        {
            valid = false;
            index.Clear();
            entries = Array.Empty<LanguageInfo>();
            ids = Array.Empty<int>();
        }

        private bool Matches(IList<LanguageInfo> source)
        {
            if (!valid || source == null || entries.Length != source.Count) return false;
            for (var i = 0; i < entries.Length; i++)
                if (!ReferenceEquals(entries[i], source[i]) || source[i] == null || ids[i] != source[i].id) return false;
            return true;
        }

        private void Rebuild(IList<LanguageInfo> source, string context)
        {
            Clear();
            if (source == null) throw new InvalidOperationException($"Localization '{context}': entry list is null.");
            var next = new LanguageInfo[source.Count];
            var keys = new int[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null) throw new InvalidOperationException($"Localization '{context}': null entry at index {i}.");
                if (index.ContainsKey(entry.id)) throw new InvalidOperationException($"Localization '{context}': duplicate ID {entry.id} at index {i}.");
                index.Add(entry.id, entry);
                next[i] = entry;
                keys[i] = entry.id;
            }
            entries = next;
            ids = keys;
            valid = true;
        }
    }
}
