using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEditor;

namespace YLFramework.Core.Tests
{
    public class CoreLoadViewArgsTests
    {
        private static readonly FieldInfo Instance = typeof(ClassObjectPoolManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly Action<LoadViewArgs> Claim = Lifecycle("Claim");
        private static readonly Action<LoadViewArgs> Release = Lifecycle("Release");
        private static readonly Action<LoadViewArgs> BeginRead = Lifecycle("BeginRead");
        private static readonly Action<LoadViewArgs> EndRead = Lifecycle("EndRead");
        private static readonly Action<LoadViewArgs> Recycle = (Action<LoadViewArgs>)Delegate.CreateDelegate(
            typeof(Action<LoadViewArgs>), typeof(LoadViewArgs).GetMethod("Recycle", BindingFlags.Static | BindingFlags.NonPublic));
        private readonly HashSet<LoadViewArgs> arguments = new HashSet<LoadViewArgs>();
        private readonly HashSet<object> previousArgs = new HashSet<object>();
        private object previousPool;

        private static Action<LoadViewArgs> Lifecycle(string name) => (Action<LoadViewArgs>)Delegate.CreateDelegate(
            typeof(Action<LoadViewArgs>), typeof(LoadViewArgs).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic));

        [SetUp]
        public void CapturePool()
        {
            arguments.Clear();
            previousArgs.Clear();
            previousPool = Instance.GetValue(null);
            if (previousPool != null)
            {
                var previousObjects = PoolDictionary(previousPool, "objects");
                if (previousObjects.Contains(typeof(LoadViewArgs)))
                    foreach (var value in (IList)previousObjects[typeof(LoadViewArgs)]) previousArgs.Add(value);
            }
        }

        [TearDown]
        public void RestoreReturnedFixtureObjects()
        {
            var pool = Instance.GetValue(null);
            if (pool == null) return;
            var objects = PoolDictionary(pool, "objects");
            var times = PoolDictionary(pool, "unloaded_times");
            foreach (var args in arguments)
            {
                // 只检查生产路径已完成归还，不用 reset 或补回收掩盖 lease/owner 残留。
                Assert.That(typeof(LoadViewArgs).GetField("leased", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(args), Is.False);
                Assert.That(typeof(LoadViewArgs).GetField("owned", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(args), Is.False);
                AssertValuesCleared(args);
                Assert.That(times.Contains(args), Is.True);
                var entries = (IList)objects[typeof(LoadViewArgs)];
                Assert.That(entries.Contains(args), Is.True);
                if (previousArgs.Contains(args)) continue;
                entries.Remove(args);
                times.Remove(args);
                if (entries.Count == 0) objects.Remove(typeof(LoadViewArgs));
            }
            if (previousPool == null)
            {
                // EditMode 不创建 checker 协程；只撤销本 fixture 新建的空池及其事件订阅。
                Assert.That(objects.Count, Is.Zero);
                Assert.That(times.Count, Is.Zero);
                var callback = (Action<PlayModeStateChange>)Delegate.CreateDelegate(typeof(Action<PlayModeStateChange>),
                    typeof(ClassObjectPoolManager).GetMethod("EditorApplication_playModeStateChanged", BindingFlags.Static | BindingFlags.NonPublic));
                EditorApplication.playModeStateChanged -= callback;
                Instance.SetValue(null, null);
            }
        }

        private static IDictionary PoolDictionary(object pool, string name) => (IDictionary)pool.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);

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
            returnArgs(args);
            var entries = (IList)PoolDictionary(Instance.GetValue(null), "objects")[typeof(LoadViewArgs)];
            var count = 0;
            foreach (var entry in entries) if (ReferenceEquals(entry, args)) count++;
            Assert.That(count, Is.EqualTo(1));
            AssertValuesCleared(args);
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
        public void ReuseableHooksCannotResetAnActiveLeaseOrReviveAReturnedAlias()
        {
            var args = NewArgs();
            var reuseable = (IReuseable)args;
            try
            {
                Assert.That(typeof(LoadViewArgs).GetMethod("OnLoaded"), Is.Null);
                Assert.That(typeof(LoadViewArgs).GetMethod("OnUnloaded"), Is.Null);
                args.SetInt("value", 7);
                Assert.Throws<InvalidOperationException>(() => reuseable.OnLoaded());
                Assert.Throws<InvalidOperationException>(() => reuseable.OnUnloaded());
                Assert.That(args.GetInt("value"), Is.EqualTo(7));
            }
            finally
            {
                Recycle(args);
            }
            reuseable.OnLoaded();
            reuseable.OnUnloaded();
            Assert.Throws<ObjectDisposedException>(() => args.GetInt("value"));
        }

        [Test]
        public void OwnedReuseableHooksCannotClearBorrowedData()
        {
            var args = NewArgs();
            args.SetInt("value", 7);
            Claim(args);
            try
            {
                var reuseable = (IReuseable)args;
                Assert.Throws<InvalidOperationException>(() => reuseable.OnLoaded());
                Assert.Throws<InvalidOperationException>(() => reuseable.OnUnloaded());
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
