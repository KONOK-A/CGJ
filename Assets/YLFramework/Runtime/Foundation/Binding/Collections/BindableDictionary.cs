using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using YLFramework.Events;

namespace YLFramework.Binding
{
    /// <summary>
    /// 可监听变化的字典，包装内部 <see cref="Dictionary{TKey,TValue}"/>：
    /// 索引器赋值、Add、Remove、Clear 等操作会触发对应事件，并支持批量合并通知（语义与 <see cref="BindableList{T}"/> 一致）。
    /// 通过构造传入的字典会被直接使用（不复制），绕过本类修改它不会触发任何事件。
    /// 同时实现泛型与非泛型 IDictionary 接口，非泛型入口会做类型转换。
    /// 非线程安全；事件回调中增删元素请先取快照或使用批量作用域。
    /// </summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">值类型。</typeparam>
    [Serializable]
    public class BindableDictionary<TKey, TValue> : IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>, IDictionary, ISerializable, IDeserializationCallback
    {
        private readonly Dictionary<TKey, TValue> _inner;

        /// <summary>
        /// 创建空字典（使用默认键相等比较器）。
        /// </summary>
        public BindableDictionary() => _inner = new Dictionary<TKey, TValue>();

        /// <summary>
        /// 创建空字典并指定键比较器。
        /// </summary>
        /// <param name="comparer">键相等比较器。</param>
        public BindableDictionary(IEqualityComparer<TKey> comparer) => _inner = new Dictionary<TKey, TValue>(comparer);

        /// <summary>
        /// 直接使用已有字典作为内部存储（不复制）；之后绕过本类修改该字典不会触发事件。
        /// </summary>
        /// <param name="innerDictionary">内部字典，不能为 null。</param>
        public BindableDictionary(Dictionary<TKey, TValue> innerDictionary) => _inner = innerDictionary;

        #region 监听事件

        [NonSerialized] private EasyEvent<int> _onCountChanged;
        /// <summary>
        /// 数量变化事件，参数为变化后的数量
        /// </summary>
        public EasyEvent<int> OnCountChanged => _onCountChanged ??= new EasyEvent<int>();

        [NonSerialized] private EasyEvent _onClear;
        /// <summary>
        /// 清空事件；参数无意义。
        /// </summary>
        public EasyEvent OnClear => _onClear ??= new EasyEvent();

        [NonSerialized] private EasyEvent<TKey, TValue> _onAdd;
        /// <summary>
        /// 添加事件，参数依次为 Key、Value。
        /// </summary>
        public EasyEvent<TKey, TValue> OnAdd => _onAdd ??= new EasyEvent<TKey, TValue>();

        [NonSerialized] private EasyEvent<TKey, TValue> _onRemove;
        /// <summary>
        /// 移除事件，参数依次为 Key、被移除的 Value。
        /// </summary>
        public EasyEvent<TKey, TValue> OnRemove => _onRemove ??= new EasyEvent<TKey, TValue>();

        [NonSerialized] private EasyEvent<TKey, TValue, TValue> _onReplace;
        /// <summary>
        /// 三个参数依次为 Key、OldValue、NewValue
        /// </summary>
        public EasyEvent<TKey, TValue, TValue> OnReplace => _onReplace ??= new EasyEvent<TKey, TValue, TValue>();

        [NonSerialized] private EasyEvent<BindableDictionaryChange<TKey, TValue>> _onChanged;
        /// <summary>
        /// 统一变化事件：Add/Remove/Replace/Clear 都会触发；批量通知结束时以 Reset 触发一次
        /// </summary>
        public EasyEvent<BindableDictionaryChange<TKey, TValue>> OnChanged =>
            _onChanged ??= new EasyEvent<BindableDictionaryChange<TKey, TValue>>();

        #endregion

        #region 批量通知

        [NonSerialized] private int _batchDepth;
        [NonSerialized] private int _batchStartCount;
        [NonSerialized] private bool _isBatchDirty;

        /// <summary>
        /// 是否处于批量通知状态，期间不会触发任何事件
        /// </summary>
        public bool IsNotificationSuspended => _batchDepth > 0;

        /// <summary>
        /// 开始批量修改。期间所有事件被抑制，EndBatch 时最多触发一次 OnCountChanged 和一次 OnChanged(Reset)
        /// </summary>
        public void BeginBatch()
        {
            if (_batchDepth == 0)
            {
                _batchStartCount = Count;
                _isBatchDirty = false;
            }
            _batchDepth++;
        }

        /// <summary>
        /// 结束一层批量修改：仍在嵌套中时只减少层级；到达最外层且期间发生过变化时，
        /// 数量变化则补发一次 OnCountChanged，随后补发一次 OnChanged(Reset)。
        /// </summary>
        /// <exception cref="InvalidOperationException">调用次数多于 BeginBatch（当前不在批量状态）。</exception>
        public void EndBatch()
        {
            if (_batchDepth == 0)
            {
                throw new InvalidOperationException("EndBatch 调用次数多于 BeginBatch。");
            }

            _batchDepth--;
            if (_batchDepth > 0 || !_isBatchDirty)
            {
                return;
            }

            _isBatchDirty = false;
            if (Count != _batchStartCount)
            {
                _onCountChanged?.Trigger(Count);
            }
            _onChanged?.Trigger(BindableDictionaryChange<TKey, TValue>.Reset());
        }

        /// <summary>
        /// 配合 using 使用的批量作用域：即使中途抛异常，退出作用域时也会结束批量并补发合并通知
        /// </summary>
        public IDisposable BeginBatchScope()
        {
            BeginBatch();
            return new BatchScope(EndBatch);
        }

        #endregion

        #region 字典操作

        /// <summary>
        /// 按键读写值：写入时 key 不存在为新增（OnAdd + OnCountChanged），已存在为替换（OnReplace，数量不变）；
        /// 读取时 key 不存在抛出 KeyNotFoundException（与 Dictionary 一致）。
        /// </summary>
        /// <param name="key">键。</param>
        /// <exception cref="KeyNotFoundException">读取时 key 不存在。</exception>
        public TValue this[TKey key]
        {
            get => _inner[key];
            set
            {
                if (TryGetValue(key, out var oldValue))
                {
                    _inner[key] = value;
                    NotifyReplace(key, oldValue, value);
                }
                else
                {
                    _inner[key] = value;
                    NotifyAdd(key, value);
                }
            }
        }

        /// <summary>
        /// 当前键值对数量。
        /// </summary>
        public int Count => _inner.Count;

        /// <summary>
        /// 内部字典的键集合（实时视图，非快照）。
        /// </summary>
        public Dictionary<TKey, TValue>.KeyCollection Keys => _inner.Keys;

        /// <summary>
        /// 内部字典的值集合（实时视图，非快照）。
        /// </summary>
        public Dictionary<TKey, TValue>.ValueCollection Values => _inner.Values;

        /// <summary>
        /// 添加键值对，成功后触发 OnAdd/OnCountChanged/OnChanged。
        /// </summary>
        /// <param name="key">键。</param>
        /// <param name="value">值。</param>
        /// <exception cref="ArgumentException">key 已存在。</exception>
        public void Add(TKey key, TValue value)
        {
            _inner.Add(key, value);

            NotifyAdd(key, value);
        }

        /// <summary>
        /// 仅在 key 不存在时添加，返回是否添加成功；key 已存在时不触发任何事件
        /// </summary>
        public bool TryAdd(TKey key, TValue value)
        {
            if (_inner.ContainsKey(key))
            {
                return false;
            }

            Add(key, value);
            return true;
        }

        /// <summary>
        /// key 不存在时添加（OnAdd），已存在时覆盖（OnReplace），返回是否为新增
        /// </summary>
        public bool AddOrUpdate(TKey key, TValue value)
        {
            var isAdded = !_inner.ContainsKey(key);
            this[key] = value;
            return isAdded;
        }

        /// <summary>
        /// 清空字典；与 BindableList 一致，本来为空时不触发 OnCountChanged，但仍触发 OnClear/OnChanged。
        /// </summary>
        public void Clear()
        {
            var beforeCount = Count;
            _inner.Clear();

            NotifyClear(beforeCount);
        }

        /// <summary>
        /// 按键移除，返回是否移除成功（key 不存在时返回 false 且不触发事件）。
        /// </summary>
        /// <param name="key">键。</param>
        public bool Remove(TKey key) => Remove(key, out _);

        /// <summary>
        /// 按键移除并输出被移除的值；key 不存在时返回 false 且 value 为 default，不触发事件。
        /// </summary>
        /// <param name="key">键。</param>
        /// <param name="value">被移除的值；失败时为 default。</param>
        public bool Remove(TKey key, out TValue value)
        {
            if (_inner.TryGetValue(key, out value))
            {
                _inner.Remove(key);
                NotifyRemove(key, value);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 按键取值；key 不存在时返回 default。
        /// </summary>
        public TValue GetValueOrDefault(TKey key) =>
            _inner.TryGetValue(key, out var value) ? value : default;

        /// <summary>
        /// 按键取值；key 不存在时返回 <paramref name="defaultValue"/>。
        /// </summary>
        public TValue GetValueOrDefault(TKey key, TValue defaultValue) =>
            _inner.TryGetValue(key, out var value) ? value : defaultValue;

        /// <summary>
        /// 是否包含指定 key。
        /// </summary>
        public bool ContainsKey(TKey key) => _inner.ContainsKey(key);

        /// <summary>
        /// 尝试按键取值；key 不存在时返回 false 且 value 为 default。
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value) => _inner.TryGetValue(key, out value);

        /// <summary>
        /// 返回内部字典的枚举器（实时枚举，非快照）。
        /// </summary>
        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _inner.GetEnumerator();

        #endregion

        #region 变化通知

        private void NotifyAdd(TKey key, TValue value)
        {
            if (_batchDepth > 0)
            {
                _isBatchDirty = true;
                return;
            }

            _onAdd?.Trigger(key, value);
            _onCountChanged?.Trigger(Count);
            _onChanged?.Trigger(BindableDictionaryChange<TKey, TValue>.Add(key, value));
        }

        private void NotifyRemove(TKey key, TValue value)
        {
            if (_batchDepth > 0)
            {
                _isBatchDirty = true;
                return;
            }

            _onRemove?.Trigger(key, value);
            _onCountChanged?.Trigger(Count);
            _onChanged?.Trigger(BindableDictionaryChange<TKey, TValue>.Remove(key, value));
        }

        private void NotifyReplace(TKey key, TValue oldValue, TValue newValue)
        {
            if (_batchDepth > 0)
            {
                _isBatchDirty = true;
                return;
            }

            _onReplace?.Trigger(key, oldValue, newValue);
            _onChanged?.Trigger(BindableDictionaryChange<TKey, TValue>.Replace(key, oldValue, newValue));
        }

        private void NotifyClear(int beforeCount)
        {
            if (_batchDepth > 0)
            {
                _isBatchDirty = true;
                return;
            }

            _onClear?.Trigger();
            if (beforeCount > 0)
            {
                // 本来为空时清空不改变数量，不触发 OnCountChanged
                _onCountChanged?.Trigger(Count);
            }
            _onChanged?.Trigger(BindableDictionaryChange<TKey, TValue>.Clear());
        }

        #endregion

        #region 显式接口实现

        object IDictionary.this[object key]
        {
            get => this[(TKey)key];
            set => this[(TKey)key] = (TValue)value;
        }

        bool IDictionary.IsFixedSize => ((IDictionary)_inner).IsFixedSize;

        bool IDictionary.IsReadOnly => ((IDictionary)_inner).IsReadOnly;

        bool ICollection.IsSynchronized => ((ICollection)_inner).IsSynchronized;

        ICollection IDictionary.Keys => ((IDictionary)_inner).Keys;

        object ICollection.SyncRoot => ((ICollection)_inner).SyncRoot;

        ICollection IDictionary.Values => ((IDictionary)_inner).Values;

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly =>
            ((ICollection<KeyValuePair<TKey, TValue>>)_inner).IsReadOnly;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => _inner.Keys;

        ICollection<TValue> IDictionary<TKey, TValue>.Values => _inner.Values;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _inner.Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _inner.Values;

        void IDictionary.Add(object key, object value) => Add((TKey)key, (TValue)value);

        bool IDictionary.Contains(object key) => ((IDictionary)_inner).Contains(key);

        void ICollection.CopyTo(Array array, int index) => ((IDictionary)_inner).CopyTo(array, index);

        /// <summary>
        /// 序列化支持：委托内部字典写入数据。
        /// </summary>
        public void GetObjectData(SerializationInfo info, StreamingContext context) =>
            ((ISerializable)_inner).GetObjectData(info, context);

        /// <summary>
        /// 反序列化完成后委托内部字典重建查找结构。
        /// </summary>
        public void OnDeserialization(object sender) => ((IDeserializationCallback)_inner).OnDeserialization(sender);

        void IDictionary.Remove(object key) => Remove((TKey)key);

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)_inner).Contains(item);

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)_inner).CopyTo(array, arrayIndex);

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
            ((ICollection<KeyValuePair<TKey, TValue>>)_inner).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            if (TryGetValue(item.Key, out var v))
            {
                if (EqualityComparer<TValue>.Default.Equals(v, item.Value))
                {
                    Remove(item.Key);
                    return true;
                }
            }

            return false;
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)_inner).GetEnumerator();

        #endregion
    }

    /// <summary>
    /// <see cref="BindableDictionary{TKey,TValue}"/> 的创建扩展。
    /// </summary>
    public static class BindableDictionaryExtensions
    {
        /// <summary>
        /// 直接包装已有字典创建 BindableDictionary（不复制，内部使用同一个字典实例）。
        /// </summary>
        /// <param name="self">要包装的字典。</param>
        public static BindableDictionary<TKey, TValue> ToBindableDictionary<TKey, TValue>(
            this Dictionary<TKey, TValue> self) =>
            new(self);
    }
}
