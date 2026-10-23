using System;
using System.Collections.Generic;

namespace YLFramework.DataAssets
{
    internal sealed class DataAssetRegistry<T> where T : class
    {
        private readonly Dictionary<string, Entry> _names = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<int, Entry> _hashes = new Dictionary<int, Entry>();
        private readonly Action<T> _removed;

        private sealed class Entry
        {
            internal string Name;
            internal int Hash;
            internal T Value;
            internal int Owners;
        }

        private sealed class Registration : IDisposable
        {
            private Action _release;
            internal Registration(Action release) => this._release = release;
            public void Dispose()
            {
                var action = _release;
                _release = null;
                action?.Invoke();
            }
        }

        internal DataAssetRegistry(Action<T> removed = null) => this._removed = removed;

        internal IDisposable Register(string name, int hash, T value)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("A root name is required.", nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (_hashes.TryGetValue(hash, out var collision) && !StringComparer.Ordinal.Equals(collision.Name, name))
            {
                throw new InvalidOperationException($"DataAssets name hash collision: '{collision.Name}' and '{name}' ({hash}).");
            }

            if (_names.TryGetValue(name, out var entry))
            {
                if (!ReferenceEquals(entry.Value, value) || entry.Hash != hash)
                {
                    throw new InvalidOperationException($"Another DataAssets root is already registered as '{name}'.");
                }
            }
            else
            {
                foreach (var existing in _names.Values)
                {
                    if (ReferenceEquals(existing.Value, value))
                    {
                        throw new InvalidOperationException($"Release DataAssets root '{existing.Name}' before registering its new name '{name}'.");
                    }
                }

                entry = new Entry { Name = name, Hash = hash, Value = value };
                _names.Add(name, entry);
                _hashes.Add(hash, entry);
            }
            entry.Owners++;
            var owned = entry;
            return new Registration(() => Release(owned));
        }

        private void Release(Entry entry)
        {
            if (!_names.TryGetValue(entry.Name, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            if (--entry.Owners != 0)
            {
                return;
            }

            _names.Remove(entry.Name);
            _hashes.Remove(entry.Hash);
            _removed?.Invoke(entry.Value);
        }

        internal T Get(string name) => name != null && _names.TryGetValue(name, out var entry) ? entry.Value : null;
        internal T Get(int hash) => _hashes.TryGetValue(hash, out var entry) ? entry.Value : null;

        internal void Clear()
        {
            var values = new List<Entry>(_names.Values);
            _names.Clear();
            _hashes.Clear();
            foreach (var entry in values)
            {
                _removed?.Invoke(entry.Value);
            }
        }
    }
}
