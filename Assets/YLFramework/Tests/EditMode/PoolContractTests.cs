using System;
using NUnit.Framework;
using YLFramework.Core.Pool;

namespace YLFramework.Tests
{
    public class PoolContractTests
    {
        private sealed class TestPool : Pool<object>
        {
            internal Func<object, bool, bool> Prepare;
            internal TestPool(PoolCacheOrder order = PoolCacheOrder.Fifo) : base(order) { }
            protected override bool PrepareRecycle(ref object item, bool willCache) =>
                Prepare == null || Prepare(item, willCache);
        }

        public sealed class SafeItem : IOfPool
        {
            public bool IsRecycled { get; set; }
            internal Action Recycled;
            internal Action Allocated;
            internal int Hooks;
            public void OnRecycle() { Hooks++; Recycled?.Invoke(); }
            public void OnAllocate() => Allocated?.Invoke();
        }

        private SafeObjectPool<SafeItem> _safe;

        [SetUp]
        public void SetUp()
        {
            _safe = SafeObjectPool<SafeItem>.Instance;
            _safe.Clear();
            _safe.MaxPoolSize = 20;
            _safe.SetFactoryMethod(() => new SafeItem());
        }

        [TearDown]
        public void TearDown() => _safe.Dispose();

        [Test]
        public void SimpleDefaultsToUnboundedFifoAndResetsDuringPrewarmOnlyOnce()
        {
            var resets = 0;
            var pool = new SimpleObjectPool<object>(() => new object(), item => resets++, 25);
            Assert.That(pool.CurStoreCount, Is.EqualTo(25));
            Assert.That(resets, Is.EqualTo(25));
            pool.Clear();
            var first = new object();
            var second = new object();
            pool.Recycle(first);
            pool.Recycle(second);
            Assert.That(pool.Allocate(), Is.SameAs(first));
            Assert.That(pool.Allocate(), Is.SameAs(second));
            Assert.That(resets, Is.EqualTo(27));
            Assert.That(pool.Recycle(null), Is.False);
            Assert.That(resets, Is.EqualTo(27));
        }

        [Test]
        public void SimpleAllowsDuplicatesButDoesNotCacheFailedReset()
        {
            var fail = false;
            var pool = new SimpleObjectPool<object>(() => new object(), item =>
            {
                if (fail) throw new InvalidOperationException();
            });
            var value = pool.Allocate();
            pool.Recycle(value);
            pool.Recycle(value);
            Assert.That(pool.Allocate(), Is.SameAs(value));
            Assert.That(pool.Allocate(), Is.SameAs(value));
            fail = true;
            Assert.Throws<InvalidOperationException>(() => pool.Recycle(value));
            Assert.That(pool.CurStoreCount, Is.Zero);
        }

        [Test]
        public void SimpleSupportsBoundedLifoAndResetsDiscardedItems()
        {
            var resets = 0;
            var pool = new SimpleObjectPool<object>(() => new object(), _ => resets++, 0, PoolCacheOrder.Lifo)
                { MaxPoolSize = 2 };
            var first = pool.Allocate();
            var second = pool.Allocate();
            var discarded = pool.Allocate();
            Assert.That(pool.Recycle(first), Is.True);
            Assert.That(pool.Recycle(second), Is.True);
            Assert.That(pool.Recycle(discarded), Is.False);
            Assert.That(resets, Is.EqualTo(3));
            Assert.That(pool.CurStoreCount, Is.EqualTo(2));
            Assert.That(pool.Allocate(), Is.SameAs(second));
            Assert.That(pool.Allocate(), Is.SameAs(first));
            Assert.That(pool.Allocate(), Is.Not.SameAs(discarded));
        }

        [TestCase(PoolCacheOrder.Fifo)]
        [TestCase(PoolCacheOrder.Lifo)]
        public void AllocationAndShrinkUseTheSelectedOrderWithoutCleaning(PoolCacheOrder order)
        {
            var hooks = 0;
            var pool = new TestPool(order) { Prepare = (item, cache) => { hooks++; return true; } };
            var first = new object();
            var second = new object();
            pool.Recycle(first);
            pool.Recycle(second);
            pool.MaxPoolSize = 1;
            Assert.That(pool.Allocate(), Is.SameAs(order == PoolCacheOrder.Fifo ? second : first));
            Assert.That(hooks, Is.EqualTo(2));
        }

        [Test]
        public void RawPrewarmCachesNullWithoutResetAndRespectsTargetAndCapacity()
        {
            var calls = 0;
            var pool = new TestPool(PoolCacheOrder.Lifo) { MaxPoolSize = 2,
                Prepare = (item, cache) => throw new InvalidOperationException() };
            pool.SetFactoryMethod(() => { calls++; return null; });
            Assert.That(pool.Prewarm(-1, PoolPrewarmMode.Raw), Is.Zero);
            Assert.That(pool.Prewarm(int.MinValue, PoolPrewarmMode.Raw), Is.Zero);
            Assert.That(pool.Prewarm(5, PoolPrewarmMode.Raw), Is.EqualTo(2));
            Assert.That(pool.Prewarm(2, PoolPrewarmMode.Raw), Is.Zero);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(pool.Allocate(), Is.Null);
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void RejectedPrewarmUsesFiniteFactoryCallsAndFactoryFailuresPropagate()
        {
            var calls = 0;
            var pool = new TestPool { Prepare = (item, cache) => false };
            pool.SetFactoryMethod(() => { calls++; return new object(); });
            Assert.That(pool.Prewarm(3), Is.Zero);
            Assert.That(calls, Is.EqualTo(3));
            pool.SetFactoryMethod(() => throw new InvalidOperationException());
            Assert.Throws<InvalidOperationException>(() => pool.Allocate());
            Assert.Throws<InvalidOperationException>(() => pool.Prewarm(1));
            pool.SetObjectFactory(null);
            Assert.That(pool.Prewarm(0), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => pool.Allocate());
        }

        [TestCase(PoolCacheOrder.Fifo)]
        [TestCase(PoolCacheOrder.Lifo)]
        public void ClearSnapshotsBeforeCallbacksAndLeavesReentrantCacheIntact(PoolCacheOrder order)
        {
            var pool = new TestPool(order);
            var first = new object();
            var second = new object();
            var replacement = new object();
            pool.Recycle(first);
            pool.Recycle(second);
            var callbacks = 0;
            pool.Clear(item =>
            {
                if (callbacks++ == 0)
                {
                    Assert.That(pool.CurStoreCount, Is.Zero);
                    pool.Recycle(replacement);
                }
            });
            Assert.That(callbacks, Is.EqualTo(2));
            Assert.That(pool.Allocate(), Is.SameAs(replacement));
        }

        [Test]
        public void CapacityDecisionPrecedesCleanupEvenWhenCleanupReenters()
        {
            var pool = new TestPool { MaxPoolSize = 1 };
            pool.Recycle(new object());
            pool.Prepare = (item, cache) =>
            {
                Assert.That(cache, Is.False);
                pool.Allocate();
                return true;
            };
            Assert.That(pool.Recycle(new object()), Is.False);
            Assert.That(pool.CurStoreCount, Is.Zero);
        }

        [Test]
        public void SafeMarksBeforeHooksRejectsReentrancyAndCleansFullCacheOnlyOnce()
        {
            _safe.MaxPoolSize = 1;
            var cached = new SafeItem();
            cached.Recycled = () =>
            {
                Assert.That(cached.IsRecycled, Is.True);
                Assert.That(_safe.Recycle(cached), Is.False);
            };
            Assert.That(_safe.Recycle(cached), Is.True);
            var discarded = new SafeItem();
            Assert.That(_safe.Recycle(discarded), Is.False);
            Assert.That(discarded.IsRecycled, Is.True);
            Assert.That(_safe.Recycle(discarded), Is.False);
            Assert.That(discarded.Hooks, Is.EqualTo(1));
            cached.Allocated = () => Assert.That(cached.IsRecycled, Is.False);
            Assert.That(_safe.Allocate(), Is.SameAs(cached));
            Assert.That(_safe.Recycle(null), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SafeRollsBackFailedHooksWithOrWithoutCapacity(bool full)
        {
            _safe.MaxPoolSize = 1;
            if (full) _safe.Recycle(new SafeItem());
            var item = new SafeItem { Recycled = () => throw new InvalidOperationException() };
            Assert.Throws<InvalidOperationException>(() => _safe.Recycle(item));
            Assert.That(item.IsRecycled, Is.False);
            Assert.That(_safe.CurStoreCount, Is.EqualTo(full ? 1 : 0));
        }

        [Test]
        public void SafePrewarmUnmarksFactoryObjectsAndIgnoresNullWithFiniteAttempts()
        {
            var calls = 0;
            _safe.SetFactoryMethod(() => ++calls == 1 ? null : new SafeItem { IsRecycled = true });
            Assert.That(_safe.Prewarm(3), Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(3));
            var item = _safe.Allocate();
            Assert.That(item.Hooks, Is.EqualTo(1));
            Assert.That(item.IsRecycled, Is.False);
            _safe.Clear();
            _safe.SetFactoryMethod(() => null);
            Assert.That(_safe.TryAllocate(out item), Is.False);
        }

        [Test]
        public void SafeClearAndDisposeDoNotTouchBorrowedObjectsOrRepeatHooks()
        {
            var borrowed = _safe.Allocate();
            var cached = _safe.Allocate();
            _safe.Recycle(cached);
            _safe.Clear();
            Assert.That(cached.IsRecycled, Is.True);
            Assert.That(cached.Hooks, Is.EqualTo(1));
            Assert.That(borrowed.IsRecycled, Is.False);
            _safe.Dispose();
            Assert.That(SafeObjectPool<SafeItem>.Instance, Is.Not.SameAs(_safe));
            Assert.That(SafeObjectPool<SafeItem>.Instance.CurStoreCount, Is.Zero);
        }
    }
}
