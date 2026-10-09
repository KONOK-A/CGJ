using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class ResourcePoolTests
    {
        private static readonly FieldInfo DataField = typeof(AssetBundleSetting).GetField(
            "_assetBundleDataFile", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo DataFactoryField = typeof(AssetBundleSetting).GetField(
            "_assetBundleDataFileFactory", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo UnloadObjectsField = typeof(ResLoader).GetField(
            "_objectsPreToUnload", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly List<IDisposable> _pools = new();
        private IResData _previousData;
        private Func<IResData> _previousFactory;

        private sealed class ThrowingResetAssetRes : AssetRes
        {
            internal readonly Exception CleanupError = new InvalidOperationException("reset failed");
            internal int ResetCalls;
            public override void OnRecycle()
            {
                ResetCalls++;
                throw CleanupError;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _previousData = (IResData)DataField.GetValue(null);
            _previousFactory = (Func<IResData>)DataFactoryField.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            AssetBundleSetting.AssetBundleDataFile = _previousData;
            AssetBundleSetting.AssetBundleDataFileFactory = _previousFactory;
            foreach (var pool in _pools) pool.Dispose();
            _pools.Clear();
        }

        private SafeObjectPool<T> UsePool<T>() where T : class, IOfPool, new()
        {
            var pool = SafeObjectPool<T>.Instance;
            pool.Clear();
            _pools.Add(pool);
            return pool;
        }

        [Test]
        public void RejectedOldLoaderDoesNotDetachOrDestroyRegisteredObjects()
        {
            var oldPool = UsePool<ResLoader>();
            var loader = ResLoader.Allocate();
            var instance = new GameObject("ResourcePoolTests.Instance");
            loader.AddObjectToDestroyWhenRecycle(instance);
            var objects = (List<Object>)UnloadObjectsField.GetValue(loader);
            oldPool.Dispose();
            var newPool = UsePool<ResLoader>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => loader.RecycleToCache());
                Assert.That(UnloadObjectsField.GetValue(loader), Is.SameAs(objects));
                Assert.That(objects, Has.Count.EqualTo(1));
                Assert.That(instance != null, Is.True);
                Assert.That(loader.IsRecycled, Is.False);
                Assert.That(newPool.CurStoreCount, Is.Zero);
            }
            finally
            {
                // EditMode 不执行领域的延迟 Destroy，仅清理本测试创建的实例。
                objects.Clear();
                Object.DestroyImmediate(instance);
                oldPool.Recycle(loader);
            }
            Assert.That(UnloadObjectsField.GetValue(loader), Is.Null);
            Assert.That(oldPool.CurStoreCount, Is.Zero);
        }

        [Test]
        public void InvalidResourcesPathFailsBeforeBorrowingAnObject()
        {
            var pool = UsePool<ResourcesRes>();
            var creates = 0;
            pool.SetFactoryMethod(() => { creates++; return new ResourcesRes(); });
            Assert.Throws<ArgumentOutOfRangeException>(() => ResourcesRes.Allocate("short", ResNamePrefixType.Url));
            Assert.That(creates, Is.Zero);
            Assert.That(pool.CurStoreCount, Is.Zero);
        }

        [Test]
        public void GameObjectLoaderAllocationFailureDiscardsThroughOriginalPool()
        {
            var pool = UsePool<GameObjectLoader>();
            var loaders = UsePool<ResLoader>();
            GameObjectLoader failed = null;
            pool.SetFactoryMethod(() => failed = Activator.CreateInstance<GameObjectLoader>());
            var error = new InvalidOperationException("nested allocation failed");
            loaders.SetFactoryMethod(() =>
            {
                pool.Dispose();
                UsePool<GameObjectLoader>();
                throw error;
            });

            Assert.That(Assert.Throws<InvalidOperationException>(() => GameObjectLoader.Allocate()), Is.SameAs(error));
            Assert.That(failed, Is.Not.Null);
            Assert.That(failed.IsRecycled, Is.True);
            Assert.That(pool.Recycle(failed), Is.False);
            Assert.That(pool.CurStoreCount, Is.Zero);
            Assert.That(SafeObjectPool<GameObjectLoader>.Instance.CurStoreCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ConfigurationFailureDiscardsResourceAndReturnsTemporaryKey(int resourceKind)
        {
            var error = new InvalidOperationException("configuration failed");
            AssetBundleSetting.AssetBundleDataFile = null;
            AssetBundleSetting.AssetBundleDataFileFactory = () => throw error;
            var keys = UsePool<ResKey>();

            if (resourceKind == 0)
                AssertConfigurationFailure(() => AssetRes.Allocate("asset", "bundle", typeof(GameObject)), error);
            else if (resourceKind == 1)
                AssertConfigurationFailure(() => AssetBundleSceneRes.Allocate("scene"), error);
            else
                AssertConfigurationFailure(() => AssetBundleRes.Allocate("bundle"), error);

            Assert.That(keys.CurStoreCount, Is.EqualTo(resourceKind == 2 ? 0 : 1));
            if (resourceKind != 2)
            {
                var key = keys.Allocate();
                Assert.That(key.AssetName, Is.Null);
                Assert.That(key.OwnerBundleName, Is.Null);
                keys.Recycle(key);
            }
        }

        private void AssertConfigurationFailure<T>(Func<T> allocate, Exception error) where T : Res, new()
        {
            var pool = UsePool<T>();
            T failed = null;
            pool.SetFactoryMethod(() => failed = new T());
            Assert.That(Assert.Throws<InvalidOperationException>(() => allocate()), Is.SameAs(error));
            Assert.That(failed, Is.Not.Null);
            Assert.That(failed.IsRecycled, Is.True);
            Assert.That(pool.Recycle(failed), Is.False);
            Assert.That(pool.CurStoreCount, Is.Zero);
        }

        [Test]
        public void FailedResourceResetPreservesBothErrorsAndIsNotReplayed()
        {
            var pool = UsePool<AssetRes>();
            UsePool<ResKey>();
            var failed = new ThrowingResetAssetRes();
            pool.SetFactoryMethod(() => failed);
            var error = new InvalidOperationException("configuration failed");
            AssetBundleSetting.AssetBundleDataFile = null;
            AssetBundleSetting.AssetBundleDataFileFactory = () => throw error;

            var aggregate = Assert.Throws<AggregateException>(() => AssetRes.Allocate("asset", null, null));
            Assert.That(aggregate.InnerExceptions, Is.EqualTo(new[] { error, failed.CleanupError }));
            Assert.That(failed.ResetCalls, Is.EqualTo(1));
            Assert.That(pool.Recycle(failed), Is.False);
            Assert.That(failed.ResetCalls, Is.EqualTo(1));
            Assert.That(pool.CurStoreCount, Is.Zero);
        }
    }
}
