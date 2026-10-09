using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using YLFramework.Core.Pool;

namespace YLFramework.Core.Tests
{
    public class CoreTemporaryCollectionTests
    {
        private sealed class ProbeModule : Module
        {
            public override void OnStart() { }
        }

        private sealed class ProbeModel : Model
        {
            public Exception ExitError;
            public Action ExitAction;
            public int ExitCount;

            public override void OnExit()
            {
                ExitCount++;
                ExitAction?.Invoke();
                if (ExitError != null) throw ExitError;
            }
        }

        private static ObjectPool<List<T>> Pool<T>() => (ObjectPool<List<T>>)typeof(ListPool<T>)
            .GetField("Pool", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        [Test]
        public void RemoveAllEventsReturnsEmptySnapshotAndKeepsExistingKeySemantics()
        {
            var instance = typeof(EventManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            var manager = Activator.CreateInstance(typeof(EventManager), true);
            var snapshot = ListPool<int>.Allocate();
            ListPool<int>.Recycle(snapshot);
            instance.SetValue(null, manager);
            try
            {
                int calls = 0;
                EventManager.AddEvent(101, (XFEvent)(() => calls++));
                EventManager.AddEvent(102, (XFEvent)(() => calls++));
                EventManager.RemoveAllEvent();
                EventManager.TriggerEvent(101);
                EventManager.TriggerEvent(102);

                Assert.That(calls, Is.Zero);
                var events = (Dictionary<int, Delegate>)typeof(EventManager)
                    .GetField("_events", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                CollectionAssert.AreEquivalent(new[] { 101, 102 }, events.Keys);
                Assert.That(events[101], Is.Null);
                Assert.That(events[102], Is.Null);
                Assert.That(snapshot, Is.Empty);
                var returned = ListPool<int>.Allocate();
                try { Assert.That(returned, Is.SameAs(snapshot)); }
                finally { ListPool<int>.Recycle(returned); }
            }
            finally { instance.SetValue(null, previous); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ModelExitFailureReturnsEmptySnapshotsWithoutChangingCleanup(bool typed)
        {
            var module = new ProbeModule();
            var failure = new InvalidOperationException("model exit failure");
            var model = new ProbeModel { ExitError = failure };
            module.AddModel(model);
            var models = ListPool<Model>.Allocate();
            ListPool<Model>.Recycle(models);
            var keys = ListPool<Type>.Allocate();
            ListPool<Type>.Recycle(keys);
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                {
                    if (typed) module.ClearModel(typeof(ProbeModel));
                    else module.ClearModel();
                });
                Assert.That(error, Is.SameAs(failure));
                Assert.That(model.ExitCount, Is.EqualTo(1));
                Assert.That(model.Module, Is.Null);
                Assert.That(module.GetModels<ProbeModel>(), Is.Empty);
                Assert.That(models, Is.Empty);
                Assert.That(keys, Is.Empty);
                var returnedModels = ListPool<Model>.Allocate();
                try { Assert.That(returnedModels, Is.SameAs(models)); }
                finally { ListPool<Model>.Recycle(returnedModels); }
                if (typed)
                {
                    var returnedKeys = ListPool<Type>.Allocate();
                    try { Assert.That(returnedKeys, Is.SameAs(keys)); }
                    finally { ListPool<Type>.Recycle(returnedKeys); }
                }
            }
            finally
            {
                model.ExitError = null;
                module.RemoveModel(model);
            }
        }

        [Test]
        public void NestedModelClearUsesIndependentSnapshotsAndExitsEachModelOnce()
        {
            var module = new ProbeModule();
            var first = new ProbeModel();
            var second = new ProbeModel();
            module.AddModel(first);
            module.AddModel(second);
            var inner = ListPool<Model>.Allocate();
            var outer = ListPool<Model>.Allocate();
            ListPool<Model>.Recycle(inner);
            ListPool<Model>.Recycle(outer);
            var reentered = false;
            first.ExitAction = second.ExitAction = () =>
            {
                if (!reentered)
                {
                    reentered = true;
                    module.ClearModel();
                }
                else
                {
                    Assert.That(outer, Has.Count.EqualTo(2));
                    Assert.That(inner, Has.Count.EqualTo(2), "Nested cleanup must not overwrite the outer borrowed snapshot.");
                }
            };
            try
            {
                module.ClearModel();
                Assert.That(reentered, Is.True);
                Assert.That(first.ExitCount, Is.EqualTo(1));
                Assert.That(second.ExitCount, Is.EqualTo(1));
                Assert.That(module.GetModels<ProbeModel>(), Is.Empty);
                Assert.That(first.Module, Is.Null);
                Assert.That(second.Module, Is.Null);
                Assert.That(outer, Is.Empty);
                Assert.That(inner, Is.Empty);
            }
            finally
            {
                first.ExitAction = second.ExitAction = null;
                module.RemoveModel(first);
                module.RemoveModel(second);
            }
        }

        [Test]
        public void SecondModelSnapshotAllocationFailureReturnsFirstSnapshot()
        {
            var module = new ProbeModule();
            var model = new ProbeModel();
            module.AddModel(model);
            var keys = ListPool<Type>.Allocate();
            ListPool<Type>.Recycle(keys);
            var pool = Pool<Model>();
            var factoryField = pool.GetType().GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic);
            var previousFactory = (IObjectFactory<List<Model>>)factoryField.GetValue(pool);
            var held = new List<List<Model>>();
            var failure = new InvalidOperationException("second snapshot allocation failure");
            try
            {
                // 暂借已有空闲项以走冷创建，结束后原样归还，不清空其他消费者的缓存。
                while (pool.CurStoreCount > 0) held.Add(ListPool<Model>.Allocate());
                pool.SetFactoryMethod(() => throw failure);
                var error = Assert.Throws<InvalidOperationException>(() => module.ClearModel(typeof(ProbeModel)));
                Assert.That(error, Is.SameAs(failure));
                Assert.That(keys, Is.Empty);
                var returnedKeys = ListPool<Type>.Allocate();
                try { Assert.That(returnedKeys, Is.SameAs(keys)); }
                finally { ListPool<Type>.Recycle(returnedKeys); }
                Assert.That(model.ExitCount, Is.Zero);
                Assert.That(model.Module, Is.SameAs(module));
                Assert.That(module.GetModel<ProbeModel>(), Is.SameAs(model));
            }
            finally
            {
                pool.SetObjectFactory(previousFactory);
                for (int i = held.Count - 1; i >= 0; i--) ListPool<Model>.Recycle(held[i]);
                module.RemoveModel(model);
            }
        }
    }
}
