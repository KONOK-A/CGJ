using System;
using System.Collections.Generic;
using NUnit.Framework;
using YLFramework.Events;

namespace YLFramework.Foundation.Tests
{
    public class EventCancellationAndOrTests
    {
        private sealed class TestSource : IEventSource
        {
            public readonly EventChannel Channel = new();
            public Action OnRegister;
            public Action OnCancel;
            public int Registrations;
            public int Cancellations;

            public IUnRegister Register(Action callback)
            {
                Registrations++;
                OnRegister?.Invoke();
                var token = Channel.Register(callback);
                try { callback?.Invoke(); }
                catch
                {
                    token.UnRegister();
                    throw;
                }
                return new CustomUnRegister(() =>
                {
                    Cancellations++;
                    OnCancel?.Invoke();
                    token.UnRegister();
                });
            }
        }

        // 首次连接在同步通知之后失败且未保留订阅；其后可正常连接。
        private sealed class NotifyThenFailOnceSource : IEventSource
        {
            public readonly EventChannel Channel = new();
            public readonly InvalidOperationException Failure = new InvalidOperationException("connect after notify");
            public int Registrations;

            public IUnRegister Register(Action callback)
            {
                var attempt = ++Registrations;
                callback();
                if (attempt == 1) throw Failure;
                return Channel.Register(callback);
            }
        }

        [Test]
        public void CustomCancellationSuppressesReentryButRetainsFailedCallback()
        {
            var failure = new InvalidOperationException("cancel");
            var calls = 0;
            CustomUnRegister token = null;
            token = new CustomUnRegister(() =>
            {
                calls++;
                token.UnRegister();
                if (calls == 1) throw failure;
            });
            Assert.That(Assert.Throws<InvalidOperationException>(token.UnRegister), Is.SameAs(failure));
            token.UnRegister();
            token.UnRegister();
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void NecessaryCleanupTriesAllAndRetainsFailuresAndNewItems()
        {
            var failure = new InvalidOperationException("cancel");
            var items = new List<IUnRegister>();
            var failedCalls = 0;
            var laterCalls = 0;
            var added = new CustomUnRegister(() => laterCalls++);
            var failed = new CustomUnRegister(() =>
            {
                if (++failedCalls == 1)
                {
                    items.Add(added);
                    throw failure;
                }
            });
            items.Add(failed);
            items.Add(new CustomUnRegister(() => laterCalls++));
            Assert.That(Assert.Throws<InvalidOperationException>(() =>
                SubscriptionCleanup.ReleaseAll(items, token => token.UnRegister())), Is.SameAs(failure));
            CollectionAssert.AreEquivalent(new IUnRegister[] { failed, added }, items);
            Assert.That(laterCalls, Is.EqualTo(1));
            SubscriptionCleanup.ReleaseAll(items, token => token.UnRegister());
            Assert.That(items, Is.Empty);
            Assert.That(failedCalls, Is.EqualTo(2));
            Assert.That(laterCalls, Is.EqualTo(2));
        }

        [Test]
        public void MultipleCleanupFailuresAreAggregatedAfterEveryAttempt()
        {
            var first = new InvalidOperationException("first");
            var second = new ArgumentException("second");
            var calls = 0;
            var items = new List<IUnRegister>
            {
                new CustomUnRegister(() => throw first),
                new CustomUnRegister(() => throw second),
                new CustomUnRegister(() => calls++)
            };
            var error = Assert.Throws<AggregateException>(() =>
                SubscriptionCleanup.ReleaseAll(items, token => token.UnRegister()));
            CollectionAssert.AreEqual(new Exception[] { first, second }, error.InnerExceptions);
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void CleanupReentryDoesNotRetryEarlierFailureInTheSameRound()
        {
            var items = new List<IUnRegister>();
            var attempts = 0;
            items.Add(new CustomUnRegister(() => { attempts++; throw new InvalidOperationException(); }));
            items.Add(new CustomUnRegister(() =>
                SubscriptionCleanup.ReleaseAll(items, token => token.UnRegister())));
            Assert.Throws<InvalidOperationException>(() =>
                SubscriptionCleanup.ReleaseAll(items, token => token.UnRegister()));
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(items.Count, Is.EqualTo(1));
        }

        [Test]
        public void ScopeClosesBeforeCallbacksAndRetainsFailedLateAdd()
        {
            var scope = new SubscriptionScope();
            var failure = new InvalidOperationException("late cancel");
            var attempts = 0;
            var late = new CustomUnRegister(() => { if (++attempts == 1) throw failure; });
            var calls = 0;
            scope.Add(new CustomUnRegister(() =>
            {
                Assert.That(scope.IsDisposed, Is.True);
                scope.Dispose();
                Assert.That(Assert.Throws<InvalidOperationException>(() => scope.Add(late)), Is.SameAs(failure));
            }));
            scope.Add(new CustomUnRegister(() => calls++));
            scope.Dispose();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(scope.Count, Is.EqualTo(1));
            scope.Dispose();
            Assert.That(scope.Count, Is.Zero);
            scope.Add(new CustomUnRegister(() => calls++));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(scope.Count, Is.Zero);
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public void OrIsLazyReconnectsAndPreservesRepeatedAndAppendedSources()
        {
            var source = new EventChannel();
            var appended = new EventChannel();
            var combined = new OrEvent().Or(source).Or(source);
            Assert.That(source.ListenerCount, Is.Zero);
            var calls = 0;
            var old = combined.Register(() => calls++);
            Assert.That(source.ListenerCount, Is.EqualTo(2));
            combined.Or(appended);
            source.Trigger();
            appended.Trigger();
            Assert.That(calls, Is.EqualTo(3));
            old.UnRegister();
            Assert.That(source.HasListener || appended.HasListener, Is.False);
            var next = combined.Register(() => calls++);
            old.UnRegister();
            source.Trigger();
            Assert.That(calls, Is.EqualTo(5));
            Assert.That(combined.ListenerCount, Is.EqualTo(1));
            next.UnRegister();
            combined.Dispose();
            Assert.Throws<ObjectDisposedException>(() => combined.Register(() => { }));
            Assert.Throws<ObjectDisposedException>(() => combined.Or(source));
            Assert.DoesNotThrow(combined.Dispose);
        }

        [Test]
        public void OrImmediateCallFailureDoesNotConnectSources()
        {
            var source = new EventChannel();
            var combined = new OrEvent().Or(source);
            var failure = new InvalidOperationException("immediate");
            Assert.That(Assert.Throws<InvalidOperationException>(() =>
                combined.RegisterWithACall(() => throw failure)), Is.SameAs(failure));
            Assert.That(source.HasListener || combined.HasListener, Is.False);
            combined.Dispose();
        }

        [Test]
        public void OrConnectionFailureRollsBackPartialSourcesAndListener()
        {
            var source = new EventChannel();
            var failure = new InvalidOperationException("connect");
            var failing = new TestSource { OnRegister = () => throw failure };
            var combined = new OrEvent().Or(source).Or(failing);
            Assert.That(Assert.Throws<InvalidOperationException>(() => combined.Register(() => { })), Is.SameAs(failure));
            Assert.That(source.HasListener || combined.HasListener, Is.False);
            Assert.That(combined.UnRegisterList, Is.Empty);
            failing.OnRegister = null;
            combined.Register(() => { });
            Assert.That(source.HasListener, Is.True);
            combined.Dispose();
        }

        [Test]
        public void FailedAppendRollsBackOnlyItsDescriptor()
        {
            var source = new EventChannel();
            var failing = new TestSource { OnRegister = () => throw new InvalidOperationException() };
            var combined = new OrEvent().Or(source);
            var calls = 0;
            var first = combined.Register(() => calls++);
            Assert.Throws<InvalidOperationException>(() => combined.Or(failing));
            source.Trigger();
            Assert.That(calls, Is.EqualTo(1));
            first.UnRegister();
            combined.Register(() => calls++);
            Assert.That(failing.Registrations, Is.EqualTo(1));
            source.Trigger();
            Assert.That(calls, Is.EqualTo(2));
            combined.Dispose();
        }

        [Test]
        public void ConnectionRollbackRetainsCancellationFailureAndContinuesOtherSources()
        {
            var registrationFailure = new InvalidOperationException("connect");
            var cancellationFailure = new ArgumentException("cancel");
            var registered = new TestSource { OnCancel = () => throw cancellationFailure };
            var other = new EventChannel();
            var failing = new TestSource { OnRegister = () => throw registrationFailure };
            var combined = new OrEvent().Or(registered).Or(other).Or(failing);
            var error = Assert.Throws<AggregateException>(() => combined.Register(() => { }));
            CollectionAssert.AreEquivalent(new Exception[] { registrationFailure, cancellationFailure }, error.InnerExceptions);
            Assert.That(combined.HasListener || other.HasListener, Is.False);
            Assert.That(combined.UnRegisterList.Count, Is.EqualTo(1));
            registered.OnCancel = null;
            combined.Dispose();
            Assert.That(registered.Channel.HasListener, Is.False);
            Assert.That(combined.UnRegisterList, Is.Empty);
        }

        [Test]
        public void FailedAppendAlsoRemovesItsConnectionCreatedBySynchronousReentry()
        {
            var source = new EventChannel();
            var appended = new TestSource();
            var failure = new InvalidOperationException("append notification");
            var combined = new OrEvent().Or(source);
            var calls = 0;
            combined.Register(() =>
            {
                combined.ClearListeners();
                combined.Register(() => calls++);
                throw failure;
            });
            Assert.That(Assert.Throws<InvalidOperationException>(() => combined.Or(appended)), Is.SameAs(failure));
            Assert.That(appended.Registrations, Is.EqualTo(2));
            Assert.That(appended.Channel.HasListener, Is.False);
            Assert.That(source.ListenerCount, Is.EqualTo(1));
            Assert.That(combined.ListenerCount, Is.EqualTo(1));
            var round = typeof(OrEvent).GetField("_round", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(combined);
            var connections = (System.Collections.ICollection)round.GetType().GetField("Connections").GetValue(round);
            Assert.That(connections.Count, Is.EqualTo(1), "成功回滚不能继续保留已撤回的 source 连接。");
            Assert.That(calls, Is.EqualTo(1));
            source.Trigger();
            Assert.That(calls, Is.EqualTo(2));
            combined.ClearListeners();
            combined.Register(() => calls++);
            Assert.That(appended.Registrations, Is.EqualTo(2));
            combined.Dispose();
        }

        [Test]
        public void OldSourceSnapshotCannotNotifyReconnectedRound()
        {
            var source = new EventChannel();
            var combined = new OrEvent().Or(source);
            var calls = 0;
            IUnRegister old = null;
            var reconnect = source.Register(() =>
            {
                old.UnRegister();
                combined.Register(() => calls++);
            });
            old = combined.Register(() => Assert.Fail("old round"));
            source.Trigger();
            Assert.That(calls, Is.Zero);
            reconnect.UnRegister();
            source.Trigger();
            Assert.That(calls, Is.EqualTo(1));
            combined.Dispose();
        }

        [Test]
        public void OrdinaryOrCancellationKeepsSnapshotWhileDisposeTruncatesIt()
        {
            var source = new EventChannel();
            var combined = new OrEvent().Or(source);
            var trace = new List<int>();
            IUnRegister first = null, second = null;
            first = combined.Register(() => { trace.Add(1); first.UnRegister(); second.UnRegister(); });
            second = combined.Register(() => trace.Add(2));
            source.Trigger();
            CollectionAssert.AreEqual(new[] { 1, 2 }, trace);
            Assert.That(source.HasListener, Is.False);
            combined.Register(() => { trace.Add(3); combined.Dispose(); });
            combined.Register(() => trace.Add(4));
            source.Trigger();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, trace);
        }

        [Test]
        public void SynchronousOnceNotificationCancelsLateSourceToken()
        {
            var source = new TestSource();
            var combined = new OrEvent().Or(source);
            var calls = 0;
            combined.RegisterOnce(() => calls++);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(source.Cancellations, Is.EqualTo(1));
            Assert.That(source.Channel.HasListener || combined.HasListener, Is.False);
            combined.Register(() => calls++);
            Assert.That(calls, Is.EqualTo(2));
            source.Channel.Trigger();
            Assert.That(calls, Is.EqualTo(3));
            combined.Dispose();
        }

        [Test]
        public void SynchronousDisposeCancelsLateTokenAndRemainsPermanent()
        {
            var source = new TestSource();
            var combined = new OrEvent().Or(source);
            combined.Register(combined.Dispose);
            Assert.That(source.Cancellations, Is.EqualTo(1));
            Assert.That(source.Channel.HasListener, Is.False);
            Assert.Throws<ObjectDisposedException>(() => combined.Register(() => { }));
            combined.Dispose();
        }

        [Test]
        public void SynchronousOnceReconnectionDoesNotAttachOldTokenToNewRound()
        {
            var source = new TestSource();
            var combined = new OrEvent().Or(source);
            var calls = 0;
            combined.RegisterOnce(() => combined.Register(() => calls++));
            Assert.That(source.Registrations, Is.EqualTo(2));
            Assert.That(source.Cancellations, Is.EqualTo(1));
            Assert.That(source.Channel.ListenerCount, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
            source.Channel.Trigger();
            Assert.That(calls, Is.EqualTo(2));
            combined.Dispose();
            Assert.That(source.Channel.HasListener, Is.False);
        }

        [Test]
        public void OrFailedCancellationKeepsResponsibilityAndAttemptsOtherSources()
        {
            var failure = new InvalidOperationException("cancel");
            var failing = new TestSource();
            failing.OnCancel = () => { if (failing.Cancellations == 1) throw failure; };
            var other = new EventChannel();
            var combined = new OrEvent().Or(failing).Or(other);
            var calls = 0;
            var token = combined.Register(() => calls++);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Assert.Throws<InvalidOperationException>(token.UnRegister), Is.SameAs(failure));
            Assert.That(other.HasListener || combined.HasListener, Is.False);
            Assert.That(combined.UnRegisterList.Count, Is.EqualTo(1));
            failing.Channel.Trigger();
            Assert.That(calls, Is.EqualTo(1));
            combined.Dispose();
            Assert.That(failing.Cancellations, Is.EqualTo(2));
            Assert.That(failing.Channel.HasListener, Is.False);
            Assert.That(combined.UnRegisterList, Is.Empty);
        }

        [Test]
        public void LateTokenFailureIsRetainedWithoutImplicitRetry()
        {
            var failure = new InvalidOperationException("late cancel");
            var source = new TestSource { OnCancel = () => throw failure };
            var combined = new OrEvent().Or(source);
            Assert.That(Assert.Throws<InvalidOperationException>(() => combined.RegisterOnce(() => { })), Is.SameAs(failure));
            Assert.That(source.Cancellations, Is.EqualTo(1));
            Assert.That(combined.UnRegisterList.Count, Is.EqualTo(1));
            source.OnCancel = null;
            combined.Dispose();
            Assert.That(source.Cancellations, Is.EqualTo(2));
            Assert.That(source.Channel.HasListener, Is.False);
        }

        [Test]
        public void CancelCallbackReentrantDisposeCannotLoseItsFailureResponsibility()
        {
            var failure = new InvalidOperationException("reentrant cancel");
            var source = new TestSource();
            var other = new EventChannel();
            var combined = new OrEvent().Or(source).Or(other);
            source.OnCancel = () =>
            {
                combined.Dispose();
                throw failure;
            };
            var token = combined.Register(() => { });
            Assert.That(Assert.Throws<InvalidOperationException>(token.UnRegister), Is.SameAs(failure));
            Assert.That(source.Cancellations, Is.EqualTo(1));
            Assert.That(other.HasListener, Is.False);
            Assert.That(combined.UnRegisterList.Count, Is.EqualTo(1));
            source.OnCancel = null;
            combined.Dispose();
            Assert.That(source.Cancellations, Is.EqualTo(2));
            Assert.That(combined.UnRegisterList, Is.Empty);
            Assert.That(source.Channel.HasListener, Is.False);
        }

        [Test]
        public void DirectOrDisposeRetainsOnlyOneFailedResponsibilityForEachAttempt()
        {
            var failure = new InvalidOperationException("cancel");
            var source = new TestSource { OnCancel = () => throw failure };
            var combined = new OrEvent().Or(source);
            combined.Register(() => { });
            Assert.That(Assert.Throws<InvalidOperationException>(combined.Dispose), Is.SameAs(failure));
            Assert.That(source.Cancellations, Is.EqualTo(1));
            Assert.That(combined.UnRegisterList.Count, Is.EqualTo(1));
            Assert.That(Assert.Throws<InvalidOperationException>(combined.Dispose), Is.SameAs(failure));
            Assert.That(source.Cancellations, Is.EqualTo(2));
            Assert.That(combined.UnRegisterList.Count, Is.EqualTo(1));
            source.OnCancel = null;
            combined.Dispose();
            Assert.That(source.Cancellations, Is.EqualTo(3));
            Assert.That(combined.UnRegisterList, Is.Empty);
        }

        [Test]
        public void LaterSourceCancelReentryDoesNotRetryEarlierFailureInTheSameRound()
        {
            var failure = new InvalidOperationException("cancel");
            var first = new TestSource { OnCancel = () => throw failure };
            var later = new TestSource();
            var combined = new OrEvent().Or(first).Or(later);
            later.OnCancel = combined.Dispose;
            var token = combined.Register(() => { });
            Assert.That(Assert.Throws<InvalidOperationException>(token.UnRegister), Is.SameAs(failure));
            Assert.That(first.Cancellations, Is.EqualTo(1));
            Assert.That(later.Cancellations, Is.EqualTo(1));
            Assert.That(combined.UnRegisterList.Count, Is.EqualTo(1));
            Assert.That(later.Channel.HasListener, Is.False);
            first.OnCancel = null;
            combined.Dispose();
            Assert.That(first.Cancellations, Is.EqualTo(2));
            Assert.That(combined.UnRegisterList, Is.Empty);
        }

        [Test]
        public void PendingRoundConnectionFailureRollsBackReentrantRegistrationsAtomically()
        {
            var healthy = new EventChannel();
            var failing = new NotifyThenFailOnceSource();
            var combined = new OrEvent().Or(healthy).Or(failing);
            IUnRegister nested = null;
            var thrown = Assert.Throws<InvalidOperationException>(() =>
                combined.Register(() => nested = combined.Register(() => { })));
            Assert.That(thrown, Is.SameAs(failing.Failure));
            Assert.That(failing.Registrations, Is.EqualTo(1));
            Assert.That(nested, Is.Not.Null);
            Assert.That(combined.ListenerCount, Is.Zero);
            Assert.That(healthy.HasListener, Is.False);
            Assert.DoesNotThrow(nested.UnRegister);

            var calls = 0;
            var rebound = combined.Register(() => calls++);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(combined.ListenerCount, Is.EqualTo(1));
            Assert.That(healthy.ListenerCount, Is.EqualTo(1));
            nested.UnRegister();
            healthy.Trigger();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(combined.ListenerCount, Is.EqualTo(1));
            rebound.UnRegister();
            Assert.That(combined.ListenerCount, Is.Zero);
            Assert.That(healthy.HasListener, Is.False);
            combined.Dispose();
        }

        [Test]
        public void FailedRoundRollbackDoesNotTouchRoundRecreatedAfterExplicitDisconnect()
        {
            var source = new EventChannel();
            var failing = new NotifyThenFailOnceSource();
            var combined = new OrEvent().Or(source).Or(failing);
            var calls = 0;
            IUnRegister rebind = null;
            var thrown = Assert.Throws<InvalidOperationException>(() => combined.Register(() =>
            {
                combined.ClearListeners();
                rebind = combined.Register(() => calls++);
            }));
            Assert.That(thrown, Is.SameAs(failing.Failure));
            Assert.That(rebind, Is.Not.Null);
            Assert.That(failing.Registrations, Is.EqualTo(2));
            Assert.That(combined.ListenerCount, Is.EqualTo(1));
            Assert.That(source.ListenerCount, Is.EqualTo(1));
            Assert.That(failing.Channel.ListenerCount, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
            source.Trigger();
            failing.Channel.Trigger();
            Assert.That(calls, Is.EqualTo(3));
            rebind.UnRegister();
            Assert.That(source.HasListener || failing.Channel.HasListener, Is.False);
            combined.Dispose();
        }
    }
}
