using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YLFramework.Pooling
{
    /// <summary>
    /// 普通引用对象的实例池。沿用工厂、FIFO/LIFO、空闲容量与目标数预热，
    /// 以引用身份维护借用状态；不提供线程安全、Unity fake-null 或租用代次隔离。
    /// </summary>
    /// <typeparam name="T">不要求实现领域接口的引用类型。</typeparam>
    public sealed class ObjectPool<T> : IPool<T>, IDisposable where T : class
    {
        private enum State
        {
            Allocating,
            Leased,
            Returning,
            Idle,
            Retired
        }
        private sealed class Entry
        {
            internal State State;
        }

        // Entry 不持有键：调用方失联的借出对象不会被身份跟踪强制保活。
        private readonly ConditionalWeakTable<T, Entry> _entries = new();
        private readonly Queue<T> _queue;
        private readonly Stack<T> _stack;
        private readonly Action<T> _onAllocate;
        private readonly Action<T> _reset;
        private readonly Action<T> _onDiscard;
        private IObjectFactory<T> _factory;
        private int _maxPoolSize;
        private bool _isDisposed;

        /// <summary>
        /// 创建实例池。工厂必须提供独占的新引用；回收 Reset 清租用数据，
        /// onDiscard 释放永久附属资源，两者均不由池猜测或自动反射执行。
        /// </summary>
        public ObjectPool(Func<T> factory, Action<T> onAllocate = null, Action<T> reset = null,
            Action<T> onDiscard = null, int maxPoolSize = 20, PoolCacheOrder cacheOrder = PoolCacheOrder.Fifo)
        {
            if (cacheOrder != PoolCacheOrder.Fifo && cacheOrder != PoolCacheOrder.Lifo)
            {
                throw new ArgumentOutOfRangeException(nameof(cacheOrder));
            }

            _factory = new CustomObjectFactory<T>(factory);
            _onAllocate = onAllocate;
            _reset = reset;
            _onDiscard = onDiscard;
            _maxPoolSize = maxPoolSize;
            if (cacheOrder == PoolCacheOrder.Fifo)
            {
                _queue = new Queue<T>();
            }
            else
            {
                _stack = new Stack<T>();
            }
        }

        /// <summary>当前空闲对象数量，不包含借出及转换期对象。</summary>
        public int CurStoreCount => _stack == null ? _queue.Count : _stack.Count;

        /// <summary>
        /// 空闲容量上限，非正值不限；缩容按分配顺序摘除对象，不重复 Reset，
        /// 逐项尝试永久清理后汇总异常。不是活动对象配额。
        /// </summary>
        public int MaxPoolSize
        {
            get => _maxPoolSize;
            set
            {
                _maxPoolSize = value;
                if (value > 0 && CurStoreCount > value)
                {
                    RetireCached(CurStoreCount - value);
                }
            }
        }

        /// <summary>替换后续冷创建的工厂；不改变已登记对象的清理责任。null 表示未配置。</summary>
        public void SetObjectFactory(IObjectFactory<T> factory)
        {
            ThrowIfDisposed();
            _factory = factory;
        }

        /// <summary>用既有委托工厂配置后续冷创建。</summary>
        public void SetFactoryMethod(Func<T> factoryMethod) => SetObjectFactory(new CustomObjectFactory<T>(factoryMethod));

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private T TakeCached() => _stack == null ? _queue.Dequeue() : _stack.Pop();

        private void Cache(T item, Entry entry)
        {
            if (_stack == null)
            {
                _queue.Enqueue(item);
            }
            else
            {
                _stack.Push(item);
            }

            entry.State = State.Idle;
        }

        private T Create()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException($"[Pool] {typeof(T).FullName} 尚未设置对象工厂。");
            }

            var item = _factory.Create();
            if (ReferenceEquals(item, null))
            {
                ThrowIfDisposed();
                return null;
            }
            if (_entries.TryGetValue(item, out _))
            {
                throw new InvalidOperationException("[Pool] 工厂必须返回本池尚未登记的独占新对象。");
            }

            _entries.Add(item, new Entry { State = State.Allocating });
            return item;
        }

        /// <summary>
        /// 冷/热取得均先执行取得回调，成功后才交付。工厂返回 null 时返回 null；
        /// 取得失败对象永久隔离，清理追加异常与根因一同报告。
        /// </summary>
        public T Allocate()
        {
            ThrowIfDisposed();
            var item = CurStoreCount > 0 ? TakeCached() : Create();
            if (ReferenceEquals(item, null))
            {
                return null;
            }

            _entries.TryGetValue(item, out var entry);
            entry.State = State.Allocating;
            try
            {
                ThrowIfDisposed();
                _onAllocate?.Invoke(item);
                ThrowIfDisposed();
            }
            catch (Exception error)
            {
                RetireFailed(item, entry, error);
                throw;
            }
            entry.State = State.Leased;
            return item;
        }

        /// <summary>工厂返回 null 时为 false；回调/工厂错误仍传播，不静默回退。</summary>
        public bool TryAllocate(out T item)
        {
            item = Allocate();
            return !ReferenceEquals(item, null);
        }

        /// <summary>
        /// 仅归还本池借出对象，先 Reset 后缓存。返回值仅表示是否入空闲缓存：
        /// 满池或关闭池的 false 仍已消费引用，不得继续使用或重试。
        /// null、重复及同对象转换期归还为 false；foreign 抛异常且不调用 Reset。
        /// 归还后的旧别名无法与重新借出的同一原始引用区分，调用方不得保留旧引用。
        /// </summary>
        public bool Recycle(T item) => Return(item, true);

        /// <summary>结束本池借用，执行一次 Reset 及永久清理，绝不缓存；重复调用不重复清理。</summary>
        public void Discard(T item) => Return(item, false);

        private bool Return(T item, bool allowCache, int cacheTarget = int.MaxValue)
        {
            if (ReferenceEquals(item, null))
            {
                return false;
            }

            if (!_entries.TryGetValue(item, out var entry))
            {
                throw new InvalidOperationException("[Pool] 不能归还不属于本池的对象。");
            }

            if (entry.State != State.Leased)
            {
                return false;
            }

            var willCache = allowCache && !_isDisposed && CurStoreCount < cacheTarget &&
                (_maxPoolSize <= 0 || CurStoreCount < _maxPoolSize);
            entry.State = State.Returning;
            try
            {
                _reset?.Invoke(item);
            }
            catch (Exception error)
            {
                RetireFailed(item, entry, error);
                throw;
            }
            // 保留初判满池的保守丢弃语义，同时复核嵌套归还/缩容/关闭后的真实上限。
            if (willCache && !_isDisposed && CurStoreCount < cacheTarget &&
                (_maxPoolSize <= 0 || CurStoreCount < _maxPoolSize))
            {
                Cache(item, entry);
                return true;
            }
            entry.State = State.Retired;
            _onDiscard?.Invoke(item);
            return false;
        }

        private void RetireFailed(T item, Entry entry, Exception error)
        {
            entry.State = State.Retired;
            try
            {
                _onDiscard?.Invoke(item);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(error, cleanupError);
            }
        }

        /// <summary>按目标空闲数量补齐并执行 Reset，返回本次新增缓存数。</summary>
        public int Prewarm(int count) => Prewarm(count, PoolPrewarmMode.Recycle);

        /// <summary>
        /// 最多创建目标缺口次数，受正容量裁剪；null 不缓存、不无限重试。
        /// Raw 不调用取得/Reset，但仍登记身份；失败前已缓存的对象保留。
        /// </summary>
        public int Prewarm(int count, PoolPrewarmMode mode) => Prewarm(count, mode, null);

        // 领域适配只标记 Raw 空闲状态，不暴露额外公共预热模式或重复缓存机制。
        internal int Prewarm(int count, PoolPrewarmMode mode, Action<T> prepareRaw)
        {
            ThrowIfDisposed();
            if (mode != PoolPrewarmMode.Recycle && mode != PoolPrewarmMode.Raw)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (_maxPoolSize > 0)
            {
                count = Math.Min(count, _maxPoolSize);
            }

            var missing = Math.Max(0, count > CurStoreCount ? count - CurStoreCount : 0);
            var cached = 0;
            for (var i = 0; i < missing; i++)
            {
                ThrowIfDisposed();
                if (CurStoreCount >= count)
                {
                    break;
                }

                var item = Create();
                if (ReferenceEquals(item, null))
                {
                    continue;
                }

                _entries.TryGetValue(item, out var entry);
                if (_isDisposed)
                {
                    var error = new ObjectDisposedException(GetType().FullName);
                    RetireFailed(item, entry, error);
                    throw error;
                }
                if (mode == PoolPrewarmMode.Recycle)
                {
                    entry.State = State.Leased;
                    if (Return(item, true, count))
                    {
                        cached++;
                    }
                }
                else
                {
                    try
                    {
                        prepareRaw?.Invoke(item);
                        ThrowIfDisposed();
                    }
                    catch (Exception error)
                    {
                        RetireFailed(item, entry, error);
                        throw;
                    }
                    if (CurStoreCount < count && (_maxPoolSize <= 0 || CurStoreCount < _maxPoolSize))
                    {
                        Cache(item, entry);
                        cached++;
                    }
                    else
                    {
                        entry.State = State.Retired;
                        _onDiscard?.Invoke(item);
                    }
                }
                ThrowIfDisposed();
            }
            return cached;
        }

        private void RetireCached(int count, Action<T> itemClearCallback = null)
        {
            if (count == 0)
            {
                return;
            }

            var snapshot = new T[count];
            for (var i = 0; i < count; i++)
            {
                var item = TakeCached();
                _entries.TryGetValue(item, out var entry);
                entry.State = State.Retired;
                snapshot[i] = item;
            }
            List<Exception> errors = null;
            foreach (var item in snapshot)
            {
                try
                {
                    _onDiscard?.Invoke(item);
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
                try
                {
                    itemClearCallback?.Invoke(item);
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
            }
            if (errors != null)
            {
                throw new AggregateException(errors);
            }
        }

        /// <summary>摘除当前空闲快照再逐项永久清理，不再次 Reset；回调中新归还的缓存保留。</summary>
        public void Clear() => RetireCached(CurStoreCount);

        /// <summary>清理空闲快照，并逐项执行附加回调；永久清理与附加回调均尽力完成后汇总异常。</summary>
        public void Clear(Action<T> itemClearCallback) => RetireCached(CurStoreCount, itemClearCallback);

        /// <summary>
        /// 先关闭再清空闲缓存，重复关闭无操作；不强制清理借出对象。
        /// 旧持有者晚归还仍执行 Reset/永久清理，但不复活缓存。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _factory = null;
            Clear();
        }
    }
}
