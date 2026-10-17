using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    // 缓存只持有真实后端租约；等待者/播放保活与闲置计时不依赖 Source 池。
    internal sealed class AudioClipCache : IDisposable
    {
        private readonly struct Key : IEquatable<Key>
        {
            internal readonly AudioClipAddress Address;
            internal Key(AudioClipAddress address) { Address = address; }
            public bool Equals(Key other) => ReferenceEquals(Address.Provider, other.Address.Provider) && Address.Key == other.Address.Key;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => RuntimeHelpers.GetHashCode(Address.Provider) * 397 ^ Address.Key.GetHashCode();
        }
        private sealed class Entry
        {
            internal Key Key;
            internal AudioClipLease Resource;
            internal Exception Error;
            internal bool Done;
            internal int Users;
            internal float Progress;
            internal double IdleAt;
            internal readonly List<Waiter> Waiters = new List<Waiter>();
        }
        private sealed class Use : IDisposable
        {
            private AudioClipCache mCache;
            private readonly Entry mEntry;
            internal Use(AudioClipCache cache, Entry entry) { mCache = cache; mEntry = entry; }
            public void Dispose() { var cache = mCache; mCache = null; cache?.Return(mEntry); }
        }
        private sealed class Waiter : IDisposable
        {
            private AudioClipCache mCache;
            private readonly Entry mEntry;
            private Action<AudioClipLease, Exception> mCompleted;
            internal readonly Action<float> Progress;
            internal Waiter(AudioClipCache cache, Entry entry, Action<AudioClipLease, Exception> completed, Action<float> progress)
            { mCache = cache; mEntry = entry; mCompleted = completed; Progress = progress; }
            internal void Deliver()
            {
                if (mCache == null) return;
                var cache = mCache; var callback = mCompleted;
                mCache = null; mCompleted = null; mEntry.Waiters.Remove(this);
                ValidateResource(mEntry);
                var lease = mEntry.Error == null ? cache.HandOff(mEntry) : null;
                if (lease == null) cache.Return(mEntry);
                try { callback(lease, mEntry.Error); }
                catch { lease?.ReleaseSafely(); throw; }
            }
            public void Dispose()
            {
                if (mCache == null) return;
                var cache = mCache; mCache = null; mCompleted = null;
                mEntry.Waiters.Remove(this); cache.Return(mEntry);
            }
        }
        private readonly Dictionary<Key, Entry> mEntries = new Dictionary<Key, Entry>();
        private bool mClosed;
        internal AudioClipLease Load(AudioClipAddress address)
        {
            var key = new Key(address);
            if (mEntries.TryGetValue(key, out var existing))
            {
                if (!existing.Done) throw new InvalidOperationException("This resource is loading asynchronously; use an asynchronous waiter.");
                ValidateResource(existing);
                if (existing.Error != null) { if (existing.Users == 0) Remove(existing); throw existing.Error; }
                existing.Users++; return HandOff(existing);
            }
            var entry = new Entry { Key = key, Users = 1 };
            mEntries.Add(key, entry);
            try
            {
                Complete(entry, address.Provider.Load(address.Key), null);
                if (entry.Error != null) throw entry.Error;
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
            var fresh = !mEntries.TryGetValue(key, out var entry);
            if (fresh) { entry = new Entry { Key = key }; mEntries.Add(key, entry); }
            entry.Users++;
            var waiter = new Waiter(this, entry, completed, progress); entry.Waiters.Add(waiter);
            if (entry.Done) waiter.Deliver();
            else if (fresh)
            {
                try { address.Provider.LoadAsync(address.Key, (lease, error) => Complete(entry, lease, error), value => Report(entry, value)); }
                catch (Exception exception) { Complete(entry, null, exception); }
            }
            else progress?.Invoke(entry.Progress);
            return waiter;
        }
        private void Report(Entry entry, float value)
        {
            if (mClosed || entry.Done) return;
            entry.Progress = Mathf.Clamp01(value);
            foreach (var waiter in entry.Waiters.ToArray()) waiter.Progress?.Invoke(entry.Progress);
        }
        private void Complete(Entry entry, AudioClipLease lease, Exception error)
        {
            if (entry.Done)
            {
                if (!ReferenceEquals(lease, entry.Resource)) lease?.ReleaseSafely();
                return;
            }
            entry.Done = true; entry.Resource = lease;
            entry.Error = error ?? (lease == null || lease.IsReleased || !lease.Clip ? new InvalidOperationException("Provider returned no live resource lease.") : null);
            if (mClosed)
            {
                if (entry.Users == 0) lease?.ReleaseSafely();
                return;
            }
            foreach (var waiter in entry.Waiters.ToArray()) waiter.Deliver();
            if (entry.Users == 0) RetireOrCache(entry);
        }
        private AudioClipLease HandOff(Entry entry) => new AudioClipLease(entry.Resource.Clip, new Use(this, entry));
        private static void ValidateResource(Entry entry)
        {
            if (entry.Error != null) return;
            try
            {
                if (entry.Resource == null) throw new InvalidOperationException("The acquired AudioClip is no longer available: " + entry.Key.Address.Key);
                entry.Resource.ValidateAcquisition();
            }
            catch (Exception error) { entry.Error = error; }
        }
        private void Return(Entry entry)
        {
            entry.Users--;
            if (entry.Users == 0)
            {
                entry.IdleAt = Time.realtimeSinceStartupAsDouble;
                if (entry.Done) RetireOrCache(entry);
            }
        }
        private void RetireOrCache(Entry entry)
        {
            if (mClosed || entry.Error != null || !entry.Key.Address.Provider.CanCache || AudioKit.ClipCacheCapacity == 0 || AudioKit.ClipCacheIdleSeconds == 0)
                Remove(entry);
            else Tick(Time.realtimeSinceStartupAsDouble);
        }
        private void Remove(Entry entry)
        {
            if (entry.Users != 0) return;
            if (mEntries.TryGetValue(entry.Key, out var current) && ReferenceEquals(entry, current)) mEntries.Remove(entry.Key);
            entry.Resource?.ReleaseSafely();
        }
        internal void Tick(double now)
        {
            if (mClosed || mEntries.Count == 0) return;
            var count = 0; var expired = false;
            foreach (var entry in mEntries.Values)
                if (entry.Done && entry.Users == 0) { count++; expired |= now - entry.IdleAt >= AudioKit.ClipCacheIdleSeconds; }
            if (!expired && count <= AudioKit.ClipCacheCapacity) return;
            var idle = new List<Entry>();
            foreach (var entry in mEntries.Values)
                if (entry.Done && entry.Users == 0) idle.Add(entry);
            idle.Sort((a, b) => a.IdleAt.CompareTo(b.IdleAt));
            var excess = idle.Count - AudioKit.ClipCacheCapacity;
            for (var i = 0; i < idle.Count; i++)
                if (i < excess || now - idle[i].IdleAt >= AudioKit.ClipCacheIdleSeconds) Remove(idle[i]);
        }
        internal void ClearIdle()
        {
            foreach (var entry in new List<Entry>(mEntries.Values)) if (entry.Done && entry.Users == 0) Remove(entry);
        }
        public void Dispose()
        {
            if (mClosed) return;
            mClosed = true;
            // 在途 producer 不能被物理取消；迟到完成仍捕获原 Entry 并归还它自己的责任。
            foreach (var entry in new List<Entry>(mEntries.Values)) if (entry.Done && entry.Users == 0) Remove(entry);
            mEntries.Clear();
        }
    }
}
