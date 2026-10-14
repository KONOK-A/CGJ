using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YLFramework.Events;

namespace YLFramework.Binding
{
    /// <summary>
    /// 可监听变化的列表，基于 <see cref="Collection{T}"/>：所有增删改与 Move 都会先更新数据再触发事件。
    /// 除按操作类型划分的事件外，还提供统一事件 <see cref="OnChanged"/>；
    /// 批量作用域（<see cref="BeginBatch"/> / <see cref="BeginBatchScope"/>）内的变化被抑制，退出时最多补发
    /// 一次 OnCountChanged 与一次 OnChanged(Reset)。
    /// 通过构造传入的 List 会被直接包裹（不复制），绕过本类修改该 List 不会触发任何事件。
    /// 非线程安全；事件回调中增删元素请先取快照或使用批量作用域。
    /// </summary>
    /// <typeparam name="T">列表元素类型。</typeparam>
    public class BindableList<T> : Collection<T>
    {
        #region 构造函数

        /// <summary>
        /// 创建空列表。
        /// </summary>
        public BindableList(){}

        /// <summary>
        /// 用序列中的元素逐个添加创建一个列表。
        /// </summary>
        /// <param name="collection">初始元素序列。</param>
        /// <exception cref="ArgumentNullException">collection 为 null。</exception>
        public BindableList(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            foreach (var item in collection)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 直接包装一个已有 List（持有引用、不复制）；<paramref name="list"/> 为 null 时创建空列表。
        /// 之后绕过本类修改该 List 不会触发任何事件，也不会同步 OnCountChanged。
        /// </summary>
        /// <param name="list">要包装的列表。</param>
        public BindableList(List<T> list) : base(list ?? new List<T>())
        {
            
        }

        #endregion

        #region 监听事件

        [NonSerialized]private EasyEvent<int> _onCountChanged;
        /// <summary>
        /// 数量变化事件，参数为变化后的数量
        /// </summary>
        public EasyEvent<int> OnCountChanged => _onCountChanged ??= new EasyEvent<int>();
        [NonSerialized]private EasyEvent _onClear;
        /// <summary>
        /// 清空事件；参数无意义。
        /// </summary>
        public EasyEvent OnClear => _onClear ??= new EasyEvent();
        [NonSerialized]private EasyEvent<int,T> _onAdd;
        /// <summary>
        /// 添加事件，参数依次为 Index、Value。
        /// </summary>
        public EasyEvent<int,T> OnAdd => _onAdd ??= new EasyEvent<int,T>();
        [NonSerialized]private EasyEvent<int,T> _onRemove;
        /// <summary>
        /// 移除事件，参数依次为 Index、被移除的 Value。
        /// </summary>
        public EasyEvent<int,T> OnRemove => _onRemove ??= new EasyEvent<int,T>();
        [NonSerialized]private EasyEvent<int,int,T> _onMove;
        /// <summary>
        /// 三个参数依次为 OldIndex、NewIndex、Value
        /// </summary>
        public EasyEvent<int,int,T> OnMove => _onMove ??= new EasyEvent<int,int,T>();
        
        [NonSerialized]private EasyEvent<int,T,T> _onReplace;
        /// <summary>
        /// 三个参数依次为 Index、OldValue、NewValue
        /// </summary>
        public EasyEvent<int,T,T> OnReplace => _onReplace ??= new EasyEvent<int,T,T>();

        [NonSerialized]private EasyEvent<BindableListChange<T>> _onChanged;
        /// <summary>
        /// 统一变化事件：Add/Remove/Replace/Move/Clear 都会触发；批量通知结束时以 Reset 触发一次
        /// </summary>
        public EasyEvent<BindableListChange<T>> OnChanged => _onChanged ??= new EasyEvent<BindableListChange<T>>();

        #endregion

        #region 批量通知

        [NonSerialized]private int _batchDepth;
        [NonSerialized]private int _batchStartCount;
        [NonSerialized]private bool _batchDirty;

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
                _batchDirty = false;
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
            if (_batchDepth > 0 || !_batchDirty) return;

            _batchDirty = false;
            if (Count != _batchStartCount)
            {
                _onCountChanged?.Trigger(Count);
            }
            _onChanged?.Trigger(BindableListChange<T>.Reset());
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

        #region 集合操作

        /// <summary>
        /// 逐个添加序列中的元素（每个元素触发各自的 Add 通知）；大量添加建议配合 <see cref="BeginBatchScope"/> 合并通知。
        /// </summary>
        /// <param name="items">要添加的元素序列。</param>
        /// <exception cref="ArgumentNullException">items 为 null。</exception>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 从后向前移除所有满足条件的元素并返回移除数量；
        /// 回调中增删集合时按收缩后的边界继续处理，不会因集合缩短而越界。
        /// </summary>
        /// <param name="match">匹配条件。</param>
        /// <returns>被移除的元素数量。</returns>
        /// <exception cref="ArgumentNullException">match 为 null。</exception>
        public int RemoveAll(Predicate<T> match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            var removedCount = 0;
            for (var i = Count - 1; i >= 0; i--)
            {
                // 回调里可能增删集合，收缩后把游标钳回边界，避免索引越界
                if (i >= Count)
                {
                    i = Count - 1;
                }
                if (i < 0) break;
                if (!match(this[i])) continue;
                RemoveAt(i);
                removedCount++;
            }
            return removedCount;
        }

        /// <summary>
        /// 尝试按下标取值；下标越界时返回 false 且 value 为 default。
        /// </summary>
        /// <param name="index">元素下标。</param>
        /// <param name="value">取到的值；失败时为 default。</param>
        public bool TryGet(int index, out T value)
        {
            if (index < 0 || index >= Count)
            {
                value = default;
                return false;
            }

            value = this[index];
            return true;
        }

        /// <summary>
        /// 把元素从 <paramref name="oldIndex"/> 移动到 <paramref name="newIndex"/>（目标下标必须已存在，不支持移动到末尾之后），
        /// 触发一次 OnMove 与 OnChanged(Move)。
        /// </summary>
        /// <param name="oldIndex">移动前下标。</param>
        /// <param name="newIndex">目标下标。</param>
        /// <exception cref="ArgumentOutOfRangeException">oldIndex 或 newIndex 越界。</exception>
        public void Move(int oldIndex, int newIndex) => MoveItem(oldIndex, newIndex);

        /// <summary>
        /// 移动实现：默认先移除再插入，只触发一次 Move 通知；派生类可重写为更高效的实现。
        /// </summary>
        protected virtual void MoveItem(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            }
            if (newIndex < 0 || newIndex >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            }

            var item = this[oldIndex];
            // 直接调用基类方法，避免触发 Remove/Add 通知；整次移动只发一次 Move 通知
            base.RemoveItem(oldIndex);
            base.InsertItem(newIndex, item);
            NotifyMove(oldIndex, newIndex, item);
        }

        #endregion

        #region 变化通知

        /// <summary>
        /// 清空数据后触发 Clear 通知；本来为空时不触发 OnCountChanged。
        /// </summary>
        protected override void ClearItems()
        {
            var beforeCount = Count;
            base.ClearItems();
            NotifyClear(beforeCount);
        }

        /// <summary>
        /// 在指定位置插入数据后触发 Add 通知。
        /// </summary>
        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            NotifyAdd(index, item);
        }

        /// <summary>
        /// 移除指定位置的数据后触发 Remove 通知。
        /// </summary>
        protected override void RemoveItem(int index)
        {
            var beforeItem = this[index];
            base.RemoveItem(index);
            NotifyRemove(index, beforeItem);
        }

        /// <summary>
        /// 替换指定位置的数据后触发 Replace 通知（数量不变）。
        /// </summary>
        protected override void SetItem(int index, T newItem)
        {
            var oldItem = this[index];
            base.SetItem(index, newItem);
            NotifyReplace(index, oldItem, newItem);
        }

        private void NotifyAdd(int index, T item)
        {
            if (_batchDepth > 0)
            {
                _batchDirty = true;
                return;
            }

            _onAdd?.Trigger(index, item);
            _onCountChanged?.Trigger(Count);
            _onChanged?.Trigger(BindableListChange<T>.Add(index, item));
        }

        private void NotifyRemove(int index, T item)
        {
            if (_batchDepth > 0)
            {
                _batchDirty = true;
                return;
            }

            _onRemove?.Trigger(index, item);
            _onCountChanged?.Trigger(Count);
            _onChanged?.Trigger(BindableListChange<T>.Remove(index, item));
        }

        private void NotifyReplace(int index, T oldItem, T newItem)
        {
            if (_batchDepth > 0)
            {
                _batchDirty = true;
                return;
            }

            _onReplace?.Trigger(index, oldItem, newItem);
            _onChanged?.Trigger(BindableListChange<T>.Replace(index, oldItem, newItem));
        }

        private void NotifyMove(int oldIndex, int newIndex, T item)
        {
            if (_batchDepth > 0)
            {
                _batchDirty = true;
                return;
            }

            _onMove?.Trigger(oldIndex, newIndex, item);
            _onChanged?.Trigger(BindableListChange<T>.Move(oldIndex, newIndex, item));
        }

        private void NotifyClear(int beforeCount)
        {
            if (_batchDepth > 0)
            {
                _batchDirty = true;
                return;
            }

            _onClear?.Trigger();
            if (beforeCount > 0)
            {
                // 本来为空时清空不改变数量，不触发 OnCountChanged
                _onCountChanged?.Trigger(Count);
            }
            _onChanged?.Trigger(BindableListChange<T>.Clear());
        }

        #endregion
    }
    /// <summary>
    /// <see cref="BindableList{T}"/> 的创建扩展。
    /// </summary>
    public static class BindableListExtensions
    {
        /// <summary>
        /// 用序列元素创建 BindableList（逐个添加）。
        /// </summary>
        /// <param name="self">初始元素序列。</param>
        /// <exception cref="ArgumentNullException">self 为 null。</exception>
        public static BindableList<T> ToBindableList<T>(this IEnumerable<T> self) => new(self);
    }
}
