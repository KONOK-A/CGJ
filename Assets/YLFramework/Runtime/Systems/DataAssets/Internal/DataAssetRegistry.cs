using System;
using System.Collections.Generic;

namespace YLFramework.DataAssets
{
    internal sealed class DataAssetRegistry<T> where T : class
    {
        private readonly Dictionary<string, Entry> names = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Action<T> removed;

        private sealed class Entry
        {
            internal string Name;
            internal T Value;
            internal int Owners;
        }

        private sealed class Registration : IDisposable
        {
            private Action release;
            internal Registration(Action release) => this.release = release;
            public void Dispose()
            {
                var action = release;
                release = null;
                action?.Invoke();
            }
        }

        internal DataAssetRegistry(Action<T> removed = null) => this.removed = removed;

        internal IDisposable Register(string name, T value)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A root name is required.", nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (names.TryGetValue(name, out var entry))
            {
                if (!ReferenceEquals(entry.Value, value))
                    throw new InvalidOperationException($"Another DataAssets root is already registered as '{name}'.");
            }
            else
            {
                foreach (var existing in names.Values)
                    if (ReferenceEquals(existing.Value, value))
                        throw new InvalidOperationException($"Release DataAssets root '{existing.Name}' before registering its new name '{name}'.");
                entry = new Entry { Name = name, Value = value };
                names.Add(name, entry);
            }
            entry.Owners++;
            var owned = entry;
            return new Registration(() => Release(owned));
        }

        private void Release(Entry entry)
        {
            if (!names.TryGetValue(entry.Name, out var current) || !ReferenceEquals(current, entry)) return;
            if (--entry.Owners != 0) return;
            names.Remove(entry.Name);
            removed?.Invoke(entry.Value);
        }

        internal T Get(string name) => name != null && names.TryGetValue(name, out var entry) ? entry.Value : null;

        internal void Clear()
        {
            var values = new List<Entry>(names.Values);
            names.Clear();
            foreach (var entry in values) removed?.Invoke(entry.Value);
        }
    }
}
