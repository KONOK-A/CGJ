using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.DataAssets;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;
using ResourceLocation = YLFramework.Services.Resource.ResourceLocation;

namespace YLFramework.Tests.DataAssets
{
    public sealed class DataAssetSingletonFixture : SingletonScriptableObjectConfigs<DataAssetSingletonFixture> { }

    public class DataAssetResourceTests
    {
        private sealed class Operation : ResourceSourceOperation
        {
            public void Finish(Object asset, Exception error = null) => Complete(asset, error);
        }

        private sealed class Source : IResourceSource
        {
            public ResourceSourceId Id => new ResourceSourceId("data-assets-test");
            public bool SupportsSynchronousLoad => true;
            internal Object Asset;
            internal bool Immediate;
            internal Action OnLoad;
            internal int Releases;
            internal readonly List<Operation> Operations = new List<Operation>();
            public void Validate(ResourceCatalogEntry entry) { }
            public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type)
            {
                OnLoad?.Invoke();
                return new ResourceSourceResult(Asset);
            }
            public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type)
            {
                OnLoad?.Invoke();
                var operation = new Operation();
                Operations.Add(operation);
                if (Immediate) operation.Finish(Asset);
                return operation;
            }
            public void Release(Object asset) => Releases++;
        }

        private Source source;
        private ScriptableObjectConfigs root;
        private ScriptableObjectData child;
        private ResourceSystem resource;
        private readonly List<IDisposable> owned = new List<IDisposable>();
        private readonly ResourceAddress address = new ResourceAddress("data-assets", "items");

        [SetUp]
        public void SetUp()
        {
            Assert.That(ResourceSystem.Current, Is.Null, "Do not take over another Resource authority.");
            Assert.That(ScriptableObjectManager.RegistrationCount, Is.Zero, "Do not clear another owner's registrations.");
            root = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            root.name = "DataAssets-Owned-Items";
            child = ScriptableObject.CreateInstance<ScriptableObjectData>();
            child.id = 17;
            child.config_name = "Potion";
            child.parent = root;
            root.configs.Add(child);
            source = new Source { Asset = root };
        }

        private ResourceSystem Initialize(Type type = null)
        {
            var config = new ResourceConfiguration(new[] { new ResourceCatalog("data-assets", new[]
            {
                new ResourceCatalogEntry("items", address, new ResourceLocation(source.Id, "controlled", type ?? typeof(ScriptableObjectConfigs)))
            }) }, new IResourceSource[] { source });
            using var initialized = ResourceSystem.InitializeAsync(config);
            Assert.That(initialized.Status, Is.EqualTo(ResourceRequestStatus.Succeeded), initialized.Error?.ToString());
            return resource = initialized.TakeResult();
        }

        private T Own<T>(T value) where T : IDisposable { owned.Add(value); return value; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var owner in owned) owner.Dispose();
            owned.Clear();
            DataAssetSingletonFixture.Release();
            foreach (var operation in source.Operations) if (!operation.IsDone) operation.Finish(source.Asset);
            if (resource != null && resource.State == ResourceSystemState.Ready)
            {
                using var shutdown = resource.ShutdownAsync();
                yield return shutdown;
                Assert.That(shutdown.Status, Is.EqualTo(ResourceRequestStatus.Succeeded), shutdown.Error?.ToString());
            }
            if (source.Asset && !ReferenceEquals(source.Asset, root)) Object.DestroyImmediate(source.Asset);
            Object.DestroyImmediate(child);
            Object.DestroyImmediate(root);
            Assert.That(ScriptableObjectManager.RegistrationCount, Is.Zero);
        }

        [Test]
        public void TwoOwnersKeepRootAndChildAliveUntilLastLeaseReturns()
        {
            source.Immediate = true;
            var system = Initialize();
            var first = Own(ScriptableObjectManager.LoadAsync(system, address));
            var second = Own(ScriptableObjectManager.LoadAsync(system, address));
            Assert.That(first.Status, Is.EqualTo(ResourceRequestStatus.Succeeded));
            Assert.That(ScriptableObjectManager.GetConfig(root.name).GetScriptableObjectData(17), Is.SameAs(child));
            Assert.That(system.GetDiagnosticsSnapshot().LeaseCount, Is.EqualTo(2));
            first.Dispose();
            Assert.That(second.Asset, Is.SameAs(root));
            Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.SameAs(root));
            Assert.That(source.Releases, Is.Zero);
            second.Dispose();
            Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.Null);
            Assert.That(system.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void ClosingBeforeCompletionCannotPublishLateRoot()
        {
            var system = Initialize();
            var load = Own(ScriptableObjectManager.LoadAsync(system, address));
            load.Dispose();
            Assert.That(load.Status, Is.EqualTo(ResourceRequestStatus.Canceled));
            source.Operations[0].Finish(root);
            Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.Null);
            Assert.That(system.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantCloseDuringAsyncStartReleasesUnclaimedImmediateResult()
        {
            source.Immediate = true;
            source.OnLoad = ScriptableObjectManager.Clear;
            var system = Initialize();
            var load = Own(ScriptableObjectManager.LoadAsync(system, address));
            Assert.That(load.IsDisposed, Is.True);
            Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.Null);
            Assert.That(system.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantCloseDuringSynchronousLoadCannotRegisterAfterClear()
        {
            source.OnLoad = ScriptableObjectManager.Clear;
            var system = Initialize();
            Assert.Throws<OperationCanceledException>(() => ScriptableObjectManager.Load(system, address));
            Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.Null);
            Assert.That(system.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateRootNameFailureReleasesOnlyRejectedLoad()
        {
            var original = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            original.name = root.name;
            try
            {
                using var registered = ScriptableObjectManager.AddConfig(original);
                source.Immediate = true;
                var system = Initialize();
                var rejected = Own(ScriptableObjectManager.LoadAsync(system, address));
                Assert.That(rejected.Status, Is.EqualTo(ResourceRequestStatus.Failed));
                Assert.That(rejected.Error, Is.InstanceOf<InvalidOperationException>());
                Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.SameAs(original));
                Assert.That(system.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
                Assert.That(source.Releases, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(original); }
        }

        [Test]
        public void RemovingRenamedRootByReferenceReleasesOriginalRegistrationAndLease()
        {
            var system = Initialize();
            var owner = Own(ScriptableObjectManager.Load(system, address));
            root.name = "Renamed";
            ScriptableObjectManager.RemoveConfig(root);
            Assert.That(owner.IsDisposed, Is.True);
            Assert.That(ScriptableObjectManager.GetConfig("DataAssets-Owned-Items"), Is.Null);
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void RootRemovalClosesBothOwnersButNotOtherRoots()
        {
            var otherRoot = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            otherRoot.name = "DataAssets-Owned-Other";
            try
            {
                var first = Own(ScriptableObjectManager.AddConfig(root));
                var second = Own(ScriptableObjectManager.AddConfig(root));
                using var other = ScriptableObjectManager.AddConfig(otherRoot);
                Assert.That(typeof(ScriptableObjectManager).GetMethod("StringToHash"), Is.Null);
                Assert.That(typeof(ScriptableObjectManager).GetMethod("GetConfig", new[] { typeof(int) }), Is.Null);
                ScriptableObjectManager.RemoveConfig(root);
                Assert.That(first.IsDisposed && second.IsDisposed, Is.True);
                Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.Null);
                Assert.That(other.Config, Is.SameAs(otherRoot));
                Assert.That(ScriptableObjectManager.RegistrationCount, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(otherRoot); }
        }

        [Test]
        public void ClearCancelsPendingSingletonAndAllowsExplicitReload()
        {
            source.Asset = ScriptableObject.CreateInstance<DataAssetSingletonFixture>();
            var system = Initialize(typeof(DataAssetSingletonFixture));
            var pending = Own(DataAssetSingletonFixture.LoadAsync(system, address));
            Assert.Throws<InvalidOperationException>(() => DataAssetSingletonFixture.LoadAsync(system, address));
            ScriptableObjectManager.Clear();
            source.Operations[0].Finish(source.Asset);
            Assert.That(pending.IsDisposed, Is.True);
            Assert.That(DataAssetSingletonFixture.Instance, Is.Null);
            source.Immediate = true;
            var replacement = Own(DataAssetSingletonFixture.LoadAsync(system, address));
            Assert.That(DataAssetSingletonFixture.Instance, Is.SameAs(source.Asset));
            DataAssetSingletonFixture.Release();
            Assert.That(replacement.IsDisposed, Is.True);
            Assert.That(DataAssetSingletonFixture.Instance, Is.Null);
            Assert.That(system.GetDiagnosticsSnapshot().LeaseCount, Is.Zero);
        }

        [Test]
        public void ExplicitConsecutiveResourceSessionsDoNotReuseDisposedRegistrations()
        {
            var first = Initialize();
            var firstOwner = Own(ScriptableObjectManager.Load(first, address));
            firstOwner.Dispose();
            using (var shutdown = first.ShutdownAsync()) Assert.That(shutdown.TakeResult(), Is.True);
            var second = Initialize();
            using var secondOwner = ScriptableObjectManager.Load(second, address);
            Assert.That(secondOwner.Config, Is.SameAs(root));
            firstOwner.Dispose();
            Assert.That(ScriptableObjectManager.GetConfig(root.name), Is.SameAs(root));
        }
    }
}
