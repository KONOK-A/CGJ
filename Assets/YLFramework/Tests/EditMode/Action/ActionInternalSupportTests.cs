using YLFramework.Actions;
using System;
using NUnit.Framework;

namespace YLFramework.Tests.Actions
{
    public class ActionInternalSupportTests
    {
        [SetUp]
        public void SetUp() => ActionQueue.Drain();

        [TearDown]
        public void TearDown() => ActionQueue.Drain();

        private sealed class PrivateConstruction
        {
            private PrivateConstruction() { }
            internal static PrivateConstruction Create() => new PrivateConstruction();
        }

        private sealed class Callback : IActionQueueCallback
        {
            private readonly Action mCallback;
            internal Callback(Action callback) => mCallback = callback;
            public void Call() => mCallback();
        }

        [Test]
        public void PoolSupportsPrivateFactoriesWarmCountAndResetOnRecycleOnly()
        {
            var created = 0;
            var resets = 0;
            var pool = new ActionPool<PrivateConstruction>(() =>
            {
                created++;
                return PrivateConstruction.Create();
            }, item => resets++, 2);
            Assert.That(created, Is.EqualTo(2));
            Assert.That(resets, Is.Zero);
            var first = pool.Allocate();
            pool.Allocate();
            Assert.That(created, Is.EqualTo(2));
            pool.Allocate();
            Assert.That(created, Is.EqualTo(3));
            Assert.That(pool.Recycle(first), Is.True);
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(pool.Allocate(), Is.SameAs(first));
            Assert.That(resets, Is.EqualTo(1));
        }

        [Test]
        public void PoolUsesLifoForWarmObjectsAndInterfaceFactories()
        {
            var first = new Callback(() => { });
            var second = new Callback(() => { });
            var calls = 0;
            var pool = new ActionPool<IActionQueueCallback>(() => ++calls == 1 ? first : second, initCount: 2);
            Assert.That(pool.Allocate(), Is.SameAs(second));
            Assert.That(pool.Allocate(), Is.SameAs(first));
            pool.Recycle(first);
            pool.Recycle(second);
            Assert.That(pool.Allocate(), Is.SameAs(second));
            Assert.That(pool.Allocate(), Is.SameAs(first));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void PoolRetainsNullAndDuplicateRecycleAndDoesNotResetCachedNullOnAllocate()
        {
            var created = 0;
            var resets = 0;
            var pool = new ActionPool<object>(() => { created++; return null; }, item => resets++, 2);
            Assert.That(resets, Is.Zero);
            Assert.That(pool.Allocate(), Is.Null);
            Assert.That(pool.Allocate(), Is.Null);
            Assert.That(created, Is.EqualTo(2));
            Assert.That(pool.Recycle(null), Is.True);
            Assert.That(pool.Allocate(), Is.Null);
            Assert.That(resets, Is.EqualTo(1));
            var item = new object();
            pool.Recycle(item);
            pool.Recycle(item);
            Assert.That(pool.Allocate(), Is.SameAs(item));
            Assert.That(pool.Allocate(), Is.SameAs(item));
        }

        [Test]
        public void PoolPropagatesFactoryAndResetExceptionsWithoutCachingTheFailedRecycle()
        {
            Assert.Throws<ArgumentNullException>(() => new ActionPool<object>(null));
            Func<object> failure = () => throw new InvalidOperationException("expected");
            Assert.Throws<InvalidOperationException>(() => new ActionPool<object>(failure, initCount: 1));
            var empty = new ActionPool<object>(failure, initCount: -1);
            Assert.Throws<InvalidOperationException>(() => empty.Allocate());
            var calls = 0;
            var pool = new ActionPool<object>(() => { calls++; return new object(); },
                item => throw new InvalidOperationException("expected"));
            var value = pool.Allocate();
            Assert.Throws<InvalidOperationException>(() => pool.Recycle(value));
            Assert.That(pool.Allocate(), Is.Not.SameAs(value));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void QueueDefersReentrantEnqueuesAndIgnoresNestedDrain()
        {
            var calls = 0;
            ActionQueue.AddCallback(new Callback(() =>
            {
                calls++;
                ActionQueue.AddCallback(new Callback(() => calls++));
                ActionQueue.Drain();
            }));
            Assert.That(calls, Is.Zero);
            ActionQueue.Drain();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(ActionQueue.PendingCount, Is.EqualTo(1));
            ActionQueue.Drain();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(ActionQueue.PendingCount, Is.Zero);
        }

        [Test]
        public void QueuePropagatesFailureWithoutReplayingTheConsumedCallback()
        {
            var calls = 0;
            ActionQueue.AddCallback(new Callback(() => throw new InvalidOperationException("expected")));
            ActionQueue.AddCallback(new Callback(() => calls++));
            Assert.Throws<InvalidOperationException>(() => ActionQueue.Drain());
            Assert.That(ActionQueue.PendingCount, Is.EqualTo(1));
            ActionQueue.Drain();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(ActionQueue.PendingCount, Is.Zero);
        }
    }
}
