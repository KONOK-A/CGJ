using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Foundation
{

#if UNITY_2020_1_OR_NEWER

    [Serializable]
    internal class DictionaryItem<TKey, TValue>
    {
        public TKey Key;
        public TValue Value;
    }

    /// <summary>
    /// 以 datas 条目列表为事实源的可序列化字典；运行时使用默认键比较器，拒绝 CLR null 键。
    /// Unity 原始无效行会保留，仅首个有效键参与查询、视图和枚举；不保证线程安全。
    /// </summary>
    [Serializable]
    public class SerializedDictionary<TKey, TValue> : IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<DictionaryItem<TKey, TValue>> datas;

        [NonSerialized] private Dictionary<TKey, int> _index;
        [NonSerialized] private int _invalidEntryCount;
        [NonSerialized] private int _version;
        [NonSerialized] private DictionaryView<TKey> _keys;
        [NonSerialized] private DictionaryView<TValue> _values;

        public SerializedDictionary()
        {
        }

        /// <summary>复制有效条目，不借用外部集合；null 来源或重复键会抛出异常。</summary>
        public SerializedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            foreach (var pair in source)
            {
                Add(pair.Key, pair.Value);
            }
        }

        public int Count => Index.Count;

        /// <summary>原始序列化行中被忽略的 null 行、null 键及重复键数量；读取不会删除行。</summary>
        public int InvalidEntryCount
        {
            get
            {
                EnsureIndex();
                return _invalidEntryCount;
            }
        }

        /// <summary>有效键的只读实时视图，顺序与首个有效序列化行一致。</summary>
        public ICollection<TKey> Keys => _keys ?? (_keys = new DictionaryView<TKey>(this, pair => pair.Key));
        public ICollection<TValue> Values => _values ?? (_values = new DictionaryView<TValue>(this, pair => pair.Value));
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        /// <summary>读取缺失键抛出 KeyNotFoundException；写入更新首个有效行或追加新行。</summary>
        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }

                throw new KeyNotFoundException();
            }
            set
            {
                ValidateKey(key);
                if (Index.TryGetValue(key, out var row))
                {
                    datas[row].Value = value;
                    _version++;
                }
                else
                {
                    Add(key, value);
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (!TryAdd(key, value))
            {
                throw new ArgumentException("已存在相同的键。", nameof(key));
            }
        }

        /// <summary>合法新键写穿序列化列表；重复键返回 false，null 键抛出异常。</summary>
        public bool TryAdd(TKey key, TValue value)
        {
            ValidateKey(key);
            var current = Index;
            if (current.ContainsKey(key))
            {
                return false;
            }

            if (datas == null)
            {
                datas = new List<DictionaryItem<TKey, TValue>>();
            }

            current.Add(key, datas.Count);
            datas.Add(new DictionaryItem<TKey, TValue> { Key = key, Value = value });
            _version++;
            return true;
        }

        public bool ContainsKey(TKey key)
        {
            ValidateKey(key);
            return Index.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            ValidateKey(key);
            if (Index.TryGetValue(key, out var row))
            {
                value = datas[row].Value;
                return true;
            }
            value = default;
            return false;
        }

        public bool ContainsValue(TValue value)
        {
            foreach (var pair in this)
            {
                if (EqualityComparer<TValue>.Default.Equals(pair.Value, value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>删除该键的全部原始行，防止隐藏的重复值重新成为有效值。</summary>
        public bool Remove(TKey key)
        {
            ValidateKey(key);
            if (!Index.ContainsKey(key))
            {
                return false;
            }

            datas.RemoveAll(item => item != null && !(item.Key is null) &&
                EqualityComparer<TKey>.Default.Equals(item.Key, key));
            Invalidate();
            return true;
        }

        /// <summary>清空全部原始行，包括不参与运行时投影的无效行。</summary>
        public void Clear()
        {
            if (datas == null || datas.Count == 0)
            {
                return;
            }

            datas.Clear();
            Invalidate();
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

        public bool Remove(KeyValuePair<TKey, TValue> item) => Contains(item) && Remove(item.Key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ValidateCopyTo(array, arrayIndex, Count);
            foreach (var pair in this)
            {
                array[arrayIndex++] = pair;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            new DictionaryEnumerator<KeyValuePair<TKey, TValue>>(this, pair => pair);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // 回调可能在工作线程执行：不遍历键，不查询 Unity 对象，不重写原始行。
        public void OnBeforeSerialize()
        {
        }
        public void OnAfterDeserialize() => Invalidate();

        private Dictionary<TKey, int> Index
        {
            get
            {
                EnsureIndex();
                return _index;
            }
        }

        private void EnsureIndex()
        {
            if (_index != null)
            {
                return;
            }

            var rebuilt = new Dictionary<TKey, int>(datas?.Count ?? 0);
            var invalid = 0;
            if (datas != null)
            {
                for (var row = 0; row < datas.Count; row++)
                {
                    var item = datas[row];
                    if (item == null || item.Key is null || rebuilt.ContainsKey(item.Key))
                    {
                        invalid++;
                    }
                    else
                    {
                        rebuilt.Add(item.Key, row);
                    }
                }
            }
            // 比较器抛错时，不发布半建缓存。
            _invalidEntryCount = invalid;
            _index = rebuilt;
        }

        private void Invalidate()
        {
            _index = null;
            _invalidEntryCount = 0;
            _version++;
        }

        private void CheckVersion(int expectedVersion)
        {
            if (_version != expectedVersion)
            {
                throw new InvalidOperationException("枚举期间字典已修改或反序列化。");
            }
        }

        private static void ValidateKey(TKey key)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }

        private static void ValidateCopyTo<T>(T[] array, int arrayIndex, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            if (array.Length - arrayIndex < count)
            {
                throw new ArgumentException("目标数组空间不足。", nameof(array));
            }
        }

        private sealed class DictionaryView<T> : ICollection<T>, IReadOnlyCollection<T>
        {
            private readonly SerializedDictionary<TKey, TValue> _owner;
            private readonly Func<KeyValuePair<TKey, TValue>, T> _select;

            public DictionaryView(SerializedDictionary<TKey, TValue> owner, Func<KeyValuePair<TKey, TValue>, T> select)
            {
                this._owner = owner;
                this._select = select;
            }

            public int Count => _owner.Count;
            public bool IsReadOnly => true;
            public void Add(T item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Remove(T item) => throw new NotSupportedException();

            public bool Contains(T item)
            {
                foreach (var value in this)
                {
                    if (EqualityComparer<T>.Default.Equals(value, item))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                ValidateCopyTo(array, arrayIndex, Count);
                foreach (var item in this)
                {
                    array[arrayIndex++] = item;
                }
            }

            public IEnumerator<T> GetEnumerator() => new DictionaryEnumerator<T>(_owner, _select);
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class DictionaryEnumerator<T> : IEnumerator<T>
        {
            private readonly SerializedDictionary<TKey, TValue> _owner;
            private readonly Func<KeyValuePair<TKey, TValue>, T> _select;
            private readonly int _expectedVersion;
            private int _row = -1;
            private bool _hasCurrent;

            public DictionaryEnumerator(SerializedDictionary<TKey, TValue> owner, Func<KeyValuePair<TKey, TValue>, T> select)
            {
                owner.EnsureIndex();
                this._owner = owner;
                this._select = select;
                _expectedVersion = owner._version;
            }

            public T Current
            {
                get; private set;
            }
            object IEnumerator.Current
            {
                get
                {
                    if (!_hasCurrent)
                    {
                        throw new InvalidOperationException("枚举器不在有效条目上。");
                    }

                    return Current;
                }
            }

            public bool MoveNext()
            {
                // 即使已枚举结束，也必须检查后续修改；yield 的结束状态会跳过此检查。
                _owner.CheckVersion(_expectedVersion);
                while (_row + 1 < (_owner.datas?.Count ?? 0))
                {
                    var item = _owner.datas[++_row];
                    if (item == null || item.Key is null || !_owner._index.TryGetValue(item.Key, out var first) || first != _row)
                    {
                        continue;
                    }

                    Current = _select(new KeyValuePair<TKey, TValue>(item.Key, item.Value));
                    _hasCurrent = true;
                    return true;
                }
                Current = default;
                _hasCurrent = false;
                return false;
            }

            public void Reset()
            {
                _owner.CheckVersion(_expectedVersion);
                _row = -1;
                Current = default;
                _hasCurrent = false;
            }

            public void Dispose()
            {
            }
        }
    }
#endif
}
