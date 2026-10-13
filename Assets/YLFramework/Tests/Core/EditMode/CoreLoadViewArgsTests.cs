using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using YLFramework.Core.Pool;

namespace YLFramework.Core.Tests
{
    public class CoreLoadViewArgsTests
    {
#pragma warning disable YL0005 // 只为检查真实参数池的回收契约，通过反射借用池而不提供业务持有入口。
        private static ObjectPool<LoadViewArgs> ArgsPool => (ObjectPool<LoadViewArgs>)typeof(LoadViewArgs)
            .GetField("Pool", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
#pragma warning restore YL0005
        private static readonly Action<LoadViewArgs> Claim = Lifecycle("Claim");
        private static readonly Action<LoadViewArgs> Release = Lifecycle("Release");
        private static readonly Action<LoadViewArgs> BeginRead = Lifecycle("BeginRead");
        private static readonly Action<LoadViewArgs> EndRead = Lifecycle("EndRead");
        private static readonly Action<LoadViewArgs> Recycle = (Action<LoadViewArgs>)Delegate.CreateDelegate(
            typeof(Action<LoadViewArgs>), typeof(LoadViewArgs).GetMethod("Recycle", BindingFlags.Static | BindingFlags.NonPublic));
#pragma warning disable YL0005 // Fixture 故意跟踪借用对象，TearDown 验证生产路径已归还并清引用。
        private readonly HashSet<LoadViewArgs> arguments = new HashSet<LoadViewArgs>();
#pragma warning restore YL0005

        private static Action<LoadViewArgs> Lifecycle(string name) => (Action<LoadViewArgs>)Delegate.CreateDelegate(
            typeof(Action<LoadViewArgs>), typeof(LoadViewArgs).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic));

        [SetUp]
        public void CapturePool()
        {
            arguments.Clear();
        }

        [TearDown]
        public void RestoreReturnedFixtureObjects()
        {
            foreach (var args in arguments)
            {
                // 只检查生产路径已完成归还，不用 reset 或补回收掩盖 lease/owner 残留。
                Assert.That(typeof(LoadViewArgs).GetField("leased", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(args), Is.False);
                Assert.That(typeof(LoadViewArgs).GetField("owned", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(args), Is.False);
                AssertValuesCleared(args);
            }
            // 空闲参数不含 fixture 引用且容量有界；不改共享池结构或补回收掩盖残留。
            arguments.Clear();
        }

        private LoadViewArgs NewArgs()
        {
            var args = LoadViewArgs.New();
            arguments.Add(args);
            return args;
        }

        private static void AssertValuesCleared(LoadViewArgs args)
        {
            foreach (var name in new[] { "strings", "ints", "bools", "floats", "longs", "doubles", "objects" })
                Assert.That(((IDictionary)typeof(LoadViewArgs).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(args)).Count, Is.Zero, name + " retained values after return");
        }

        private static Action[] Reads(LoadViewArgs args) => new Action[]
        {
            () => args.GetString("value"), () => args.GetInt("value"), () => args.GetBool("value"),
            () => args.GetFloat("value"), () => args.GetLong("value"), () => args.GetDouble("value"),
            () => args.GetObject("value")
        };

        private static Action[] Writes(LoadViewArgs args) => new Action[]
        {
            () => args.SetString("value", "changed"), () => args.SetInt("value", 9), () => args.SetBool("value", false),
            () => args.SetFloat("value", 9), () => args.SetLong("value", 9), () => args.SetDouble("value", 9),
            () => args.SetObject("value", new object())
        };

        [TestCase(false)]
        [TestCase(true)]
        public void ReturnedArgumentsImmediatelyClearEveryCategoryAndReuseEmpty(bool claimed)
        {
            var first = NewArgs();
            var returnArgs = Recycle;
            try
            {
                var marker = new object();
                first.SetString("value", "fixture");
                first.SetInt("value", 7);
                first.SetBool("value", true);
                first.SetFloat("value", 2.5f);
                first.SetLong("value", 1234567890123);
                first.SetDouble("value", 3.25);
                first.SetObject("value", marker);
                Assert.That(first.GetString("value"), Is.EqualTo("fixture"));
                Assert.That(first.GetInt("value"), Is.EqualTo(7));
                Assert.That(first.GetBool("value"), Is.True);
                Assert.That(first.GetFloat("value"), Is.EqualTo(2.5f));
                Assert.That(first.GetLong("value"), Is.EqualTo(1234567890123));
                Assert.That(first.GetDouble("value"), Is.EqualTo(3.25));
                Assert.That(first.GetObject("value"), Is.SameAs(marker));
                if (claimed)
                {
                    Claim(first);
                    returnArgs = Release;
                }
            }
            finally
            {
                returnArgs(first);
            }
            AssertValuesCleared(first);
            foreach (var read in Reads(first)) Assert.Throws<ObjectDisposedException>(() => read());
            foreach (var write in Writes(first)) Assert.Throws<ObjectDisposedException>(() => write());
            Assert.Throws<ObjectDisposedException>(() => Claim(first));

            var reused = NewArgs();
            try
            {
                Assert.That(reused, Is.SameAs(first));
                Assert.That(reused.GetString("value"), Is.Empty);
                Assert.That(reused.GetInt("value"), Is.Zero);
                Assert.That(reused.GetBool("value"), Is.False);
                Assert.That(reused.GetFloat("value"), Is.Zero);
                Assert.That(reused.GetLong("value"), Is.Zero);
                Assert.That(reused.GetDouble("value"), Is.Zero);
                Assert.That(reused.GetObject("value"), Is.Null);
            }
            finally
            {
                Recycle(reused);
            }
        }

        [Test]
        public void ClaimFreezesCallerAccessAndRepeatedClaimDoesNotChangeTheLease()
        {
            var args = NewArgs();
            args.SetObject("value", args);
            Claim(args);
            try
            {
                Assert.Throws<InvalidOperationException>(() => Claim(args));
                Assert.Throws<InvalidOperationException>(() => Recycle(args));
                foreach (var read in Reads(args)) Assert.Throws<InvalidOperationException>(() => read());
                foreach (var write in Writes(args)) Assert.Throws<InvalidOperationException>(() => write());
                BeginRead(args);
                try
                {
                    Assert.Throws<InvalidOperationException>(() => Claim(args));
                    Assert.Throws<InvalidOperationException>(() => Recycle(args));
                    Assert.Throws<InvalidOperationException>(() => BeginRead(args));
                    foreach (var write in Writes(args)) Assert.Throws<InvalidOperationException>(() => write());
                    Assert.That(args.GetObject("value"), Is.SameAs(args));
                    foreach (var read in Reads(args)) Assert.DoesNotThrow(() => read());
                }
                finally
                {
                    EndRead(args);
                }
                foreach (var read in Reads(args)) Assert.Throws<InvalidOperationException>(() => read());
                Assert.Throws<InvalidOperationException>(() => EndRead(args));
            }
            finally
            {
                Release(args);
            }
            AssertValuesCleared(args);
        }

        [Test]
        public void CallbackExceptionEndsBorrowingWithoutReturningTheOwnersLease()
        {
            var args = NewArgs();
            Claim(args);
            try
            {
                Assert.Throws<FormatException>(() =>
                {
                    BeginRead(args);
                    try { throw new FormatException("fixture callback"); }
                    finally { EndRead(args); }
                });
                Assert.Throws<InvalidOperationException>(() => args.GetObject("value"));
                Assert.Throws<InvalidOperationException>(() => Recycle(args));
            }
            finally
            {
                Release(args);
            }
            Assert.Throws<ObjectDisposedException>(() => args.GetObject("value"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedReturnDoesNotAddAnotherPoolEntry(bool claimed)
        {
            var args = NewArgs();
            if (claimed) Claim(args);
            var returnArgs = claimed ? Release : Recycle;
            returnArgs(args);
            var count = ArgsPool.CurStoreCount;
            returnArgs(args);
            Assert.That(ArgsPool.CurStoreCount, Is.EqualTo(count));
            AssertValuesCleared(args);
            var first = NewArgs();
            LoadViewArgs second = null;
            try
            {
                second = NewArgs();
                Assert.That(second, Is.Not.SameAs(first), "Repeated return must not publish the same reference twice.");
            }
            finally
            {
                Recycle(first);
                Recycle(second);
            }
        }

        [Test]
        public void UnclaimedArgumentsRejectOwnerOperationsWithoutLosingValues()
        {
            var args = NewArgs();
            try
            {
                args.SetInt("value", 7);
                Assert.Throws<InvalidOperationException>(() => Release(args));
                Assert.Throws<InvalidOperationException>(() => BeginRead(args));
                Assert.Throws<InvalidOperationException>(() => EndRead(args));
                Assert.That(args.GetInt("value"), Is.EqualTo(7));
            }
            finally
            {
                Recycle(args);
            }
        }

        [Test]
        public void GenericPoolCannotResetAnActiveLeaseOrReviveAReturnedAlias()
        {
            var args = NewArgs();
#pragma warning disable YL0004 // 构造故意的外来池工厂，验证通用池不能接管真实参数 lease。
            using var foreignPool = new ObjectPool<LoadViewArgs>(() => new LoadViewArgs());
#pragma warning restore YL0004
            try
            {
                Assert.That(typeof(LoadViewArgs).GetMethod("OnLoaded"), Is.Null);
                Assert.That(typeof(LoadViewArgs).GetMethod("OnUnloaded"), Is.Null);
                args.SetInt("value", 7);
                Assert.Throws<InvalidOperationException>(() => foreignPool.Recycle(args));
                Assert.Throws<InvalidOperationException>(() => foreignPool.Discard(args));
                Assert.That(args.GetInt("value"), Is.EqualTo(7));
            }
            finally
            {
                Recycle(args);
            }
            Assert.Throws<InvalidOperationException>(() => foreignPool.Recycle(args));
            Assert.Throws<InvalidOperationException>(() => foreignPool.Discard(args));
            Assert.Throws<ObjectDisposedException>(() => args.GetInt("value"));
        }

        [Test]
        public void GenericPoolCannotClearOwnedBorrowedData()
        {
            var args = NewArgs();
            args.SetInt("value", 7);
            Claim(args);
            try
            {
#pragma warning disable YL0004 // 同一外来工厂用于验证 owned 借用数据不会被通用池清理。
                using var foreignPool = new ObjectPool<LoadViewArgs>(() => new LoadViewArgs());
#pragma warning restore YL0004
                Assert.Throws<InvalidOperationException>(() => foreignPool.Recycle(args));
                Assert.Throws<InvalidOperationException>(() => foreignPool.Discard(args));
                BeginRead(args);
                try { Assert.That(args.GetInt("value"), Is.EqualTo(7)); }
                finally { EndRead(args); }
            }
            finally
            {
                Release(args);
            }
        }

        [Test]
        public void RegisteredSessionResetClearsIdleCacheWithoutEndingOwnedLeaseOrRevivingReturnedArguments()
        {
            var idle = NewArgs();
            var ownedArgs = NewArgs();
            ownedArgs.SetInt("value", 7);
            Claim(ownedArgs);
            Recycle(idle);
            try
            {
                var state = typeof(ObjectPool<>).Assembly.GetType("YLFramework.Core.Singleton.SingletonSessionState");
                var resets = (List<Action>)state.GetField("SessionResets", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var callbacks = resets.FindAll(reset => ReferenceEquals(reset.Target, ArgsPool));
                Assert.That(callbacks, Has.Count.EqualTo(1), "The parameter pool must register exactly one real session reset.");
                callbacks[0]();
                Assert.That(ArgsPool.CurStoreCount, Is.Zero);
                Assert.Throws<ObjectDisposedException>(() => idle.GetInt("value"));
                var next = NewArgs();
                try
                {
                    Assert.That(next, Is.Not.SameAs(idle).And.Not.SameAs(ownedArgs));
                    BeginRead(ownedArgs);
                    try { Assert.That(ownedArgs.GetInt("value"), Is.EqualTo(7)); }
                    finally { EndRead(ownedArgs); }
                    Assert.That(next.GetInt("value"), Is.Zero);
                }
                finally { Recycle(next); }
            }
            finally { Release(ownedArgs); }
        }

        [Test]
        public void AnotherThreadCannotAccessClaimOrReturnAnActiveLease()
        {
            var args = NewArgs();
            args.SetInt("value", 7);
            try
            {
                var operations = new Action[]
                {
                    () => args.GetInt("value"), () => args.SetInt("value", 9), () => Claim(args),
                    () => Recycle(args), () => Release(args), () => BeginRead(args), () => EndRead(args)
                };
                var failures = new Exception[operations.Length];
                var worker = new Thread(() =>
                {
                    for (var i = 0; i < operations.Length; i++)
                        try { operations[i](); }
                        catch (Exception exception) { failures[i] = exception; }
                });
                worker.Start();
                Assert.That(worker.Join(5000), Is.True, "fixture worker did not terminate");
                foreach (var failure in failures) Assert.That(failure, Is.TypeOf<InvalidOperationException>());
                Assert.That(args.GetInt("value"), Is.EqualTo(7));
            }
            finally
            {
                Recycle(args);
            }
        }
    }
}
