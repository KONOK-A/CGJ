using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace YLFramework.Audio
{
    // 缓存只持有真实后端租约；等待者/播放保活与闲置计时不依赖 Source 池。
    internal sealed class AudioClipCache : IDisposable
    {
        private readonly struct Key : IEquatable<Key>
        {
            internal readonly AudioClipAddress Address;
            internal Key(AudioClipAddress address)
            {
                Address = address;
            }
            public bool Equals(Key other) => ReferenceEquals(Address.Provider, other.Address.Provider) && Address.Key == other.Address.Key;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => RuntimeHelpers.GetHashCode(Address.Provider) * 397 ^ Address.Key.GetHashCode();
        }
        private sealed class Entry
        {
            internal Key Key;
            internal AudioClipLease Resource;
            internal Exception Error;
            internal bool IsDone;
            internal int Users;
            internal float Progress;
            internal double IdleAt;
            internal readonly List<Waiter> Waiters = new List<Waiter>();
        }
        private sealed class Use : IDisposable
        {
            private AudioClipCache _cache;
            private readonly Entry _entry;
            internal Use(AudioClipCache cache, Entry entry)
            {
                _cache = cache;
                _entry = entry;
            }
            public void Dispose()
            {
                var cache = _cache;
                _cache = null;
                cache?.Return(_entry);
            }
        }
        private sealed class Waiter : IDisposable
        {
            private AudioClipCache _cache;
            private readonly Entry _entry;
            private Action<AudioClipLease, Exception> _completed;
            internal readonly Action<float> Progress;
            internal Waiter(AudioClipCache cache, Entry entry, Action<AudioClipLease, Exception> completed, Action<float> progress)
            {
                _cache = cache;
                _entry = entry;
                _completed = completed;
                Progress = progress;
            }
            internal void Deliver()
            {
                if (_cache == null)
                {
                    return;
                }

                var cache = _cache;
                var callback = _completed;
                _cache = null;
                _completed = null;
                _entry.Waiters.Remove(this);
                ValidateResource(_entry);
                var lease = _entry.Error == null ? cache.HandOff(_entry) : null;
                if (lease == null)
                {
                    cache.Return(_entry);
                }

                try
                {
                    callback(lease, _entry.Error);
                }
                catch
                {
                    lease?.ReleaseSafely();
                    throw;
                }
            }
            public void Dispose()
            {
                if (_cache == null)
                {
                    return;
                }

                var cache = _cache;
                _cache = null;
                _completed = null;
                _entry.Waiters.Remove(this);
                cache.Return(_entry);
            }
        }
        private readonly Dictionary<Key, Entry> _entries = new Dictionary<Key, Entry>();
        private bool _isClosed;
        internal AudioClipLease Load(AudioClipAddress address)
        {
            var key = new Key(address);
            if (_entries.TryGetValue(key, out var existing))
            {
                if (!existing.IsDone)
                {
                    throw new InvalidOperationException("This resource is loading asynchronously; use an asynchronous waiter.");
                }

                ValidateResource(existing);
                if (existing.Error != null)
                {
                    if (existing.Users == 0)
                    {
                        Remove(existing);
                    }
                    throw existing.Error;
                }
                existing.Users++;
                return HandOff(existing);
            }
            var entry = new Entry { Key = key, Users = 1 };
            _entries.Add(key, entry);
            try
            {
                Complete(entry, address.Provider.Load(address.Key), null);
                if (entry.Error != null)
                {
                    throw entry.Error;
                }

                return HandOff(entry);
            }
            catch (Exception exception)
            {
                Complete(entry, null, exception);
                Return(entry);
                throw;
            }
        }
        internal IDisposable LoadAsync(AudioClipAddress address, Action<AudioClipLease, Exception> completed, Action<float> progress)
        {
            var key = new Key(address);
            var fresh = !_entries.TryGetValue(key, out var entry);
            if (fresh)
            {
                entry = new Entry { Key = key };
                _entries.Add(key, entry);
            }
            entry.Users++;
            var waiter = new Waiter(this, entry, completed, progress);
            entry.Waiters.Add(waiter);
            if (entry.IsDone)
            {
                waiter.Deliver();
            }
            else if (fresh)
            {
                try
                {
                    address.Provider.LoadAsync(address.Key, (lease, error) => Complete(entry, lease, error), value => Report(entry, value));
                }
                catch (Exception exception)
                {
                    Complete(entry, null, exception);
                }
            }
            else
            {
                progress?.Invoke(entry.Progress);
            }

            return waiter;
        }
        private void Report(Entry entry, float value)
        {
            if (_isClosed || entry.IsDone)
            {
                return;
            }

            entry.Progress = Mathf.Clamp01(value);
            foreach (var waiter in entry.Waiters.ToArray())
            {
                waiter.Progress?.Invoke(entry.Progress);
            }
        }
        private void Complete(Entry entry, AudioClipLease lease, Exception error)
        {
            if (entry.IsDone)
            {
                if (!ReferenceEquals(lease, entry.Resource))
                {
                    lease?.ReleaseSafely();
                }

                return;
            }
            entry.IsDone = true;
            entry.Resource = lease;
            entry.Error = error ?? (lease == null || lease.IsReleased || !lease.Clip ? new InvalidOperationException("Provider returned no live resource lease.") : null);
            if (_isClosed)
            {
                if (entry.Users == 0)
                {
                    lease?.ReleaseSafely();
                }

                return;
            }
            foreach (var waiter in entry.Waiters.ToArray())
            {
                waiter.Deliver();
            }

            if (entry.Users == 0)
            {
                RetireOrCache(entry);
            }
        }
        private AudioClipLease HandOff(Entry entry) => new AudioClipLease(entry.Resource.Clip, new Use(this, entry));
        private static void ValidateResource(Entry entry)
        {
            if (entry.Error != null)
            {
                return;
            }

            try
            {
                if (entry.Resource == null)
                {
                    throw new InvalidOperationException("The acquired AudioClip is no longer available: " + entry.Key.Address.Key);
                }

                entry.Resource.ValidateAcquisition();
            }
            catch (Exception error)
            {
                entry.Error = error;
            }
        }
        private void Return(Entry entry)
        {
            entry.Users--;
            if (entry.Users == 0)
            {
                entry.IdleAt = Time.realtimeSinceStartupAsDouble;
                if (entry.IsDone)
                {
                    RetireOrCache(entry);
                }
            }
        }
        private void RetireOrCache(Entry entry)
        {
            if (_isClosed || entry.Error != null || !entry.Key.Address.Provider.CanCache || AudioKit.ClipCacheCapacity == 0 || AudioKit.ClipCacheIdleSeconds == 0)
            {
                Remove(entry);
            }
            else
            {
                Tick(Time.realtimeSinceStartupAsDouble);
            }
        }
        private void Remove(Entry entry)
        {
            if (entry.Users != 0)
            {
                return;
            }

            if (_entries.TryGetValue(entry.Key, out var current) && ReferenceEquals(entry, current))
            {
                _entries.Remove(entry.Key);
            }

            entry.Resource?.ReleaseSafely();
        }
        internal void Tick(double now)
        {
            if (_isClosed || _entries.Count == 0)
            {
                return;
            }

            var count = 0;
            var expired = false;
            foreach (var entry in _entries.Values)
            {
                if (entry.IsDone && entry.Users == 0)
                {
                    count++;
                    expired |= now - entry.IdleAt >= AudioKit.ClipCacheIdleSeconds;
                }
            }

            if (!expired && count <= AudioKit.ClipCacheCapacity)
            {
                return;
            }

            var idle = new List<Entry>();
            foreach (var entry in _entries.Values)
            {
                if (entry.IsDone && entry.Users == 0)
                {
                    idle.Add(entry);
                }
            }

            idle.Sort((a, b) => a.IdleAt.CompareTo(b.IdleAt));
            var excess = idle.Count - AudioKit.ClipCacheCapacity;
            for (var i = 0; i < idle.Count; i++)
            {
                if (i < excess || now - idle[i].IdleAt >= AudioKit.ClipCacheIdleSeconds)
                {
                    Remove(idle[i]);
                }
            }
        }
        internal void ClearIdle()
        {
            foreach (var entry in new List<Entry>(_entries.Values))
            {
                if (entry.IsDone && entry.Users == 0)
                {
                    Remove(entry);
                }
            }
        }
        public void Dispose()
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            // 在途 producer 不能被物理取消；迟到完成仍捕获原 Entry 并归还它自己的责任。
            foreach (var entry in new List<Entry>(_entries.Values))
            {
                if (entry.IsDone && entry.Users == 0)
                {
                    Remove(entry);
                }
            }

            _entries.Clear();
        }
    }
}
