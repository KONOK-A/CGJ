using System;
using System.Collections.Generic;

namespace YLFramework.Events
{
    internal interface IEventInvocation<TCallback> where TCallback : Delegate
    {
        void Invoke(TCallback callback);
    }

    // 注册/注销时复制数组；发布只读取捕获的数组，不借池也不为每次通知创建闭包。
    internal sealed class EventRegistrations<TCallback> where TCallback : Delegate
    {
        private readonly EventChannelBase _owner;
        private Entry[] _entries = Array.Empty<Entry>();

        internal EventRegistrations(EventChannelBase owner) => _owner = owner;
        internal int Count => _entries.Length;

        internal IUnRegister Register(TCallback callback, bool once = false)
        {
            _owner.ThrowIfDisposed();
            if (callback == null) return EmptyUnRegister.Instance;
            var registration = new Registration(this, once);
            var targets = once ? new Delegate[] { callback } : callback.GetInvocationList();
            var next = new Entry[_entries.Length + targets.Length];
            Array.Copy(_entries, next, _entries.Length);
            for (var i = 0; i < targets.Length; i++)
                next[_entries.Length + i] = new Entry((TCallback)targets[i], registration);
            _entries = next;
            _owner.NotifyListenersChanged();
            return registration;
        }

        internal void UnRegister(TCallback callback)
        {
            if (callback == null || _entries.Length == 0) return;
            var targets = callback.GetInvocationList();
            // 与 Delegate.Remove 一致，只删除最后一个相等的普通 target 子序列。
            for (var start = _entries.Length - targets.Length; start >= 0; start--)
            {
                var matches = true;
                for (var i = 0; i < targets.Length; i++)
                    if (_entries[start + i].Registration.Once || !_entries[start + i].Callback.Equals(targets[i]))
                    {
                        matches = false;
                        break;
                    }
                if (!matches) continue;
                var previous = _entries;
                var next = new Entry[previous.Length - targets.Length];
                Array.Copy(previous, next, start);
                Array.Copy(previous, start + targets.Length, next, start, next.Length - start);
                _entries = next;
                for (var i = start; i < start + targets.Length; i++)
                {
                    var registration = previous[i].Registration;
                    if (!Contains(registration)) registration.Invalidate();
                }
                _owner.NotifyListenersChanged();
                return;
            }
        }

        private bool Contains(Registration registration)
        {
            foreach (var entry in _entries)
                if (ReferenceEquals(entry.Registration, registration)) return true;
            return false;
        }

        private void Remove(Registration registration)
        {
            var count = 0;
            foreach (var entry in _entries)
                if (!ReferenceEquals(entry.Registration, registration)) count++;
            registration.Invalidate();
            if (count == _entries.Length) return;
            var next = count == 0 ? Array.Empty<Entry>() : new Entry[count];
            var index = 0;
            foreach (var entry in _entries)
                if (!ReferenceEquals(entry.Registration, registration)) next[index++] = entry;
            _entries = next;
            _owner.NotifyListenersChanged();
        }

        internal void Clear()
        {
            var previous = _entries;
            _entries = Array.Empty<Entry>();
            foreach (var entry in previous) entry.Registration.Invalidate();
        }

        internal Snapshot Capture()
        {
            _owner.ThrowIfDisposed();
            return new Snapshot(_owner, _owner.Incarnation, _entries);
        }

        internal void Dispatch<TInvocation>(TInvocation invocation, bool continueOnError)
            where TInvocation : struct, IEventInvocation<TCallback>
        {
            var snapshot = Capture();
            List<Exception> errors = null;
            for (var i = 0; i < snapshot.Count && snapshot.IsCurrent; i++)
            {
                if (!continueOnError)
                {
                    if (snapshot.TryGetCallback(i, out var callback)) invocation.Invoke(callback);
                    continue;
                }
                try
                {
                    if (snapshot.TryGetCallback(i, out var callback)) invocation.Invoke(callback);
                }
                catch (Exception error)
                {
                    (errors ??= new List<Exception>()).Add(error);
                }
            }
            SubscriptionCleanup.ThrowErrors(errors);
        }

        internal readonly struct Snapshot
        {
            private readonly EventChannelBase _owner;
            private readonly object _incarnation;
            private readonly Entry[] _entries;
            internal Snapshot(EventChannelBase owner, object incarnation, Entry[] entries)
            {
                _owner = owner;
                _incarnation = incarnation;
                _entries = entries;
            }
            internal int Count => _entries.Length;
            internal bool IsCurrent => _owner.IsCurrent(_incarnation);
            internal bool TryGetCallback(int index, out TCallback callback)
            {
                callback = null;
                if (!IsCurrent) return false;
                var entry = _entries[index];
                var registration = entry.Registration;
                if (registration.Once)
                {
                    if (registration.Consumed) return false;
                    registration.Consumed = true;
                    registration.UnRegister();
                    if (!IsCurrent) return false;
                }
                callback = entry.Callback;
                return true;
            }
        }

        internal readonly struct Entry
        {
            internal readonly TCallback Callback;
            internal readonly Registration Registration;
            internal Entry(TCallback callback, Registration registration)
            {
                Callback = callback;
                Registration = registration;
            }
        }

        internal sealed class Registration : IUnRegister, IDisposable
        {
            private EventRegistrations<TCallback> _owner;
            internal readonly bool Once;
            internal bool Consumed;
            internal Registration(EventRegistrations<TCallback> owner, bool once)
            {
                _owner = owner;
                Once = once;
            }
            internal void Invalidate() => _owner = null;
            public void UnRegister() => _owner?.Remove(this);
            public void Dispose() => UnRegister();
        }
    }

}
