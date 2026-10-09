using System;
using System.Collections.Generic;
using NUnit.Framework;
using YLFramework.Events;
using YLFramework.Core.Pool;

namespace YLFramework.Tests
{
    public class EventsContractTests
    {
        private sealed class PooledValue { }

        [Test]
        public void PooledEventClearsListenersReusesIdentityAndRejectsOrdinaryInstance()
        {
            var e = EasyEvent<PooledValue>.Allocate();
            var pool = SafeObjectPool<EasyEvent<PooledValue>>.Instance;
            try
            {
                Assert.That(pool.MaxPoolSize, Is.EqualTo(50));
                var calls = 0;
                e.Register(_ => calls++);
                e.RecycleToCache();
                Assert.That(e.IsRecycled, Is.True);
                Assert.That(e.HasListener, Is.False);
                Assert.That(pool.CurStoreCount, Is.EqualTo(1));
                e.RecycleToCache();
                Assert.That(pool.CurStoreCount, Is.EqualTo(1));
                var next = EasyEvent<PooledValue>.Allocate();
                Assert.That(next, Is.SameAs(e));
                next.Trigger(new PooledValue());
                Assert.That(calls, Is.Zero);
                var ordinary = new EasyEvent<PooledValue>();
                ordinary.Register(_ => calls++);
                Assert.Throws<InvalidOperationException>(ordinary.RecycleToCache);
                Assert.That(ordinary.HasListener, Is.True);
                Assert.That(ordinary.IsRecycled, Is.False);
                next.RecycleToCache();
            }
            finally { pool.Dispose(); }
        }

        [Test]
        public void DispatchUsesSnapshotWhileListenersUnregisterAndRegister()
        {
            var e = new EasyEvent<int>();
            var trace = new List<int>();
            IUnRegister first = null, second = null;
            first = e.Register(_ =>
            {
                trace.Add(1);
                first.UnRegister();
                second.UnRegister();
                e.Register(value => trace.Add(value));
            });
            second = e.Register(_ => trace.Add(2));
            e.Trigger(3);
            e.Trigger(4);
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, trace);
        }

        [Test]
        public void DuplicateRegistrationsAndExceptionsKeepDelegateSemantics()
        {
            var e = new EasyEvent();
            var calls = 0;
            Action listener = () => calls++;
            var first = e.Register(listener);
            e.Register(listener);
            e.Trigger();
            first.UnRegister();
            first.UnRegister();
            e.Trigger();
            Assert.That(calls, Is.EqualTo(3));
            e.UnRegisterAll();
            e.Register(() => throw new InvalidOperationException());
            e.Register(listener);
            Assert.Throws<InvalidOperationException>(e.Trigger);
            Assert.That(calls, Is.EqualTo(3));
        }

        [Test]
        public void ImmediateCallPrecedesRegistration()
        {
            var e = new EasyEvent();
            var calls = 0;
            e.RegisterWithACall(() => { calls++; if (calls == 1) e.Trigger(); });
            Assert.That(calls, Is.EqualTo(1));
            e.Trigger();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void SingleUseTokenIsConsumedBeforeReentryAndFailure()
        {
            var calls = 0;
            SingleUseUnRegister token = null;
            token = new SingleUseUnRegister(() =>
            {
                calls++;
                token.Dispose();
                throw new InvalidOperationException();
            });
            Assert.Throws<InvalidOperationException>(token.UnRegister);
            Assert.DoesNotThrow(token.Dispose);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ExistingTokenRetainsCallbackUntilSuccess()
        {
            var calls = 0;
            var token = new CustomUnRegister(() => { if (++calls == 1) throw new InvalidOperationException(); });
            Assert.Throws<InvalidOperationException>(token.UnRegister);
            token.UnRegister();
            token.UnRegister();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void StrictCallbackCopiesKeepIndependentStateAndRepeatFailure()
        {
            var calls = 0;
            var token = new CallbackUnRegister(() => calls++, CallbackReleaseMode.StrictAfterSuccess);
            var copy = token;
            token.UnRegister();
            Assert.Throws<NullReferenceException>(() => token.UnRegister());
            copy.UnRegister();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void DetachSnapshotAllowsReentryAndPreservesNewSubscriptions()
        {
            var items = new SubscriptionSet<IUnRegister>();
            var later = new SingleUseUnRegister(() => { });
            var token = new SingleUseUnRegister(() =>
            {
                Assert.That(items.Count, Is.Zero);
                Assert.That(items.DetachAll(), Is.Empty);
                items.Add(later);
            });
            items.Add(token);
            items.Add(token);
            Assert.That(items.Count, Is.EqualTo(1));
            foreach (var item in items.DetachAll()) item.UnRegister();
            Assert.That(items.Count, Is.EqualTo(1));
            items.Remove(later);
            Assert.That(items.Count, Is.Zero);
        }

        [Test]
        public void LegacyReleaseRetainsCollectionOnFailure()
        {
            var laterCalls = 0;
            var items = new List<IUnRegister>
            {
                new CustomUnRegister(() => throw new InvalidOperationException()),
                new CustomUnRegister(() => laterCalls++)
            };
            Assert.Throws<InvalidOperationException>(() =>
                SubscriptionCleanup.ReleaseAfterSuccess(items, item => item.UnRegister()));
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(laterCalls, Is.Zero);
        }

        [Test]
        public void ClearListenersInvalidatesOldEventsButClearOnlyDropsMappings()
        {
            var events = new EasyEvents();
            var old = events.GetOrAddEvent<EasyEvent<int>>();
            var calls = 0;
            var token = old.Register(_ => calls++);
            events.ClearListeners();
            old.Trigger(1);
            token.UnRegister();
            Assert.That(calls, Is.Zero);
            Assert.That(events.GetEvent<EasyEvent<int>>(), Is.Null);
            var next = events.GetOrAddEvent<EasyEvent<int>>();
            next.Register(_ => calls++);
            events.Clear();
            next.Trigger(2);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DefaultOrKeepsOtherListenersUntilLastUnregister()
        {
            var source = new EasyEvent();
            var combined = new OrEvent().Or(source);
            var calls = 0;
            var first = combined.Register(() => calls++);
            var second = combined.Register(() => calls++);
            first.UnRegister();
            source.Trigger();
            Assert.That(calls, Is.EqualTo(1));
            second.UnRegister();
            Assert.That(source.HasListener, Is.False);
            combined.Dispose();
        }

        [Test]
        public void GenericEventNoArgumentInterfaceDropsTheValue()
        {
            var value = new EasyEvent<int>();
            var observed = new List<int>();
            var notifications = 0;
            var typed = value.Register(observed.Add);
            var untyped = ((IEasyEvent)value).Register(() => notifications++);
            value.Trigger(7);
            CollectionAssert.AreEqual(new[] { 7 }, observed);
            Assert.That(notifications, Is.EqualTo(1));
            typed.UnRegister();
            untyped.UnRegister();
            value.Trigger(9);
            Assert.That(observed.Count, Is.EqualTo(1));
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void OrUnregisterInsideCallbackKeepsRemainingListenersAndSources()
        {
            var source = new EasyEvent();
            var combined = source.Or(new EasyEvent());
            var trace = new List<int>();
            IUnRegister first = null;
            first = combined.Register(() => { trace.Add(1); first.UnRegister(); });
            var second = combined.Register(() => trace.Add(2));
            source.Trigger();
            source.Trigger();
            CollectionAssert.AreEqual(new[] { 1, 2, 2 }, trace);
            second.UnRegister();
            Assert.That(source.HasListener, Is.False);
            Assert.DoesNotThrow(() => combined.Dispose());
        }

        private sealed class SubscriptionList : IUnRegisterList
        {
            public List<IUnRegister> UnRegisterList { get; } = new List<IUnRegister>();
        }

        [Test]
        public void RegisterListCollectsAndClearsEveryOwnedSubscription()
        {
            var first = new EasyEvent();
            var second = new EasyEvent();
            var calls = 0;
            var list = new SubscriptionList();
            first.Register(() => calls++).AddToUnRegisterList(list);
            second.Register(() => calls++).AddToUnRegisterList(list);
            Assert.That(list.UnRegisterList.Count, Is.EqualTo(2));
            first.Trigger();
            second.Trigger();
            Assert.That(calls, Is.EqualTo(2));
            list.UnRegisterAll();
            Assert.That(list.UnRegisterList, Is.Empty);
            list.UnRegisterAll();
            first.Trigger();
            second.Trigger();
            Assert.That(calls, Is.EqualTo(2));
        }
    }
}
