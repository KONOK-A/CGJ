#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.EquipmentSystem;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;
using ResourceLocation = YLFramework.Services.Resource.ResourceLocation;

namespace YLFramework.Tests.Extensions.Equipment
{
    public class EquipmentResourceTests
    {
        private string folder, prefix;
        private ResourceSystem resources;
        private EquipmentsConfig root;
        private readonly ResourceAddress configAddress = new("eq-fixture", "config");
        private readonly ResourceAddress prefabAddress = new("eq-fixture", "prefab");

        private void Prepare(int components = 1)
        {
            Assert.That(ResourceSystem.Current, Is.Null, "Do not take over another Resource authority.");
            EquipmentManager.Shutdown();
            prefix = "Eq" + Guid.NewGuid().ToString("N");
            var parent = "Assets/YLFramework/Tests/Fixtures/Extensions";
            folder = parent + "/" + prefix;
            AssetDatabase.CreateFolder(parent, prefix);
            AssetDatabase.CreateFolder(folder, "Resources");
            var go = new GameObject("Equipment Resource Probe");
            try
            {
                for (var i = 0; i < components; i++) go.AddComponent<EquipmentResourceProbe>();
                PrefabUtility.SaveAsPrefabAsset(go, folder + "/Resources/" + prefix + "Prefab.prefab");
            }
            finally { Object.DestroyImmediate(go); }
            root = ScriptableObject.CreateInstance<EquipmentsConfig>(); root.name = prefix;
            var data = ScriptableObject.CreateInstance<EquipmentConfigData>();
            data.parent = root; data.id = 1001; data.equipmentType = EquipmentType.Asset;
            data.projectName = "eq-fixture"; data.assetName = "prefab"; root.Equipments.Add(data);
            AssetDatabase.CreateAsset(root, folder + "/Resources/" + prefix + "Config.asset");
            AssetDatabase.AddObjectToAsset(data, root); EditorUtility.SetDirty(root); AssetDatabase.SaveAssetIfDirty(root);
            var entries = new[]
            {
                new ResourceCatalogEntry("config", configAddress, new ResourceLocation(ResourceSourceId.Resources, prefix + "Config", typeof(EquipmentsConfig))),
                new ResourceCatalogEntry("prefab", prefabAddress, new ResourceLocation(ResourceSourceId.Resources, prefix + "Prefab", typeof(GameObject)))
            };
            using var initialized = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[] { new ResourceCatalog("eq-fixture", entries) }));
            resources = initialized.TakeResult();
            EquipmentManager.LoadConfig(resources, configAddress);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EquipmentResourceProbe.FailLoad = false;
            EquipmentManager.Shutdown();
            if (resources != null && ReferenceEquals(ResourceSystem.Current, resources))
            {
                using var shutdown = resources.ShutdownAsync();
                while (!shutdown.IsDone) yield return null;
                Assert.That(shutdown.Status, Is.EqualTo(ResourceRequestStatus.Succeeded));
            }
            yield return null;
            if (folder != null) AssetDatabase.DeleteAsset(folder);
            EquipmentResourceProbe.LastLoaded = null;
        }

        [Test]
        public void PrefabUsesResourceCacheAndRootLeaseIsIndependent()
        {
            Prepare();
            using var independent = resources.Acquire<EquipmentsConfig>(configAddress);
            EquipmentManager.LoadConfig(resources, configAddress);
            var first = (EquipmentResourceProbe)EquipmentManager.CreateEquipment(1001);
            EquipmentManager.DestroyEquipment(first);
            var cache = resources.GetInstanceCache(prefabAddress);
            Assert.That(cache.CachedCount, Is.EqualTo(1)); Assert.That(first.EquipmentData, Is.Null);
            var second = (EquipmentResourceProbe)EquipmentManager.CreateEquipment(1001);
            Assert.That(second, Is.SameAs(first)); Assert.That(second.Loads, Is.EqualTo(2));
            EquipmentManager.Shutdown();
            Assert.That(independent.Asset, Is.SameAs(root)); Assert.That(second.Unloads, Is.EqualTo(2));
            Assert.That(ResourceSystem.Current, Is.SameAs(resources)); Assert.That(cache.CachedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingRootComponentIsDiscarded()
        {
            Prepare(0);
            Assert.Throws<InvalidOperationException>(() => EquipmentManager.CreateEquipment(1001));
            yield return null;
            Assert.That(resources.GetInstanceCache(prefabAddress).CachedCount, Is.Zero);
            Assert.That(Object.FindObjectsOfType<EquipmentResourceProbe>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AmbiguousRootComponentsAreDiscarded()
        {
            Prepare(2);
            Assert.Throws<InvalidOperationException>(() => EquipmentManager.CreateEquipment(1001));
            yield return null;
            Assert.That(resources.GetInstanceCache(prefabAddress).CachedCount, Is.Zero);
            Assert.That(Object.FindObjectsOfType<EquipmentResourceProbe>(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator FailedOnLoadedDoesNotEnterIdleCache()
        {
            Prepare(); EquipmentResourceProbe.FailLoad = true;
            Assert.Throws<InvalidOperationException>(() => EquipmentManager.CreateEquipment(1001));
            var failed = EquipmentResourceProbe.LastLoaded;
            Assert.That(failed.Unloads, Is.EqualTo(1));
            yield return null;
            Assert.That(failed == null, Is.True);
            EquipmentResourceProbe.FailLoad = false;
            var replacement = EquipmentManager.CreateEquipment(1001);
            Assert.That(ReferenceEquals(replacement, failed), Is.False);
        }

        [UnityTest]
        public IEnumerator ExternallyDestroyedEquipmentIsRemovedFromHost()
        {
            Prepare();
            var host = new Host();
            var equipment = (EquipmentResourceProbe)EquipmentManager.CreateAndEquip(host, 1001);
            Object.Destroy(equipment.gameObject); yield return null;
            EquipmentManager.CloseHost(host);
            Assert.That(host.Equipments, Is.Empty);
            Assert.That(EquipmentManager.GetEquipmentInfo(1001), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RemovingConfigDetachesDestroyedBorrowerUsingRecordedDefinition()
        {
            Prepare(); using var independent = resources.Acquire<EquipmentsConfig>(configAddress);
            var host = new Host(); var go = new GameObject("External borrower");
            var borrowed = go.AddComponent<EquipmentResourceProbe>(); borrowed.EquipmentData = independent.Asset.Equipments[0];
            EquipmentManager.Equip(host, borrowed); Object.Destroy(go); yield return null;
            EquipmentManager.RemoveConfig(root);
            Assert.That(host.Equipments, Is.Empty);
            Assert.That(independent.Asset, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ResourceShutdownRespectsLiveEquipmentBarrier()
        {
            Prepare();
            var host = new Host();
            EquipmentManager.CreateAndEquip(host, 1001);
            using var busy = resources.ShutdownAsync();
            Assert.That(busy.Status, Is.EqualTo(ResourceRequestStatus.Failed));
            Assert.That(busy.Error.Kind, Is.EqualTo(ResourceError.Busy));
            EquipmentManager.Shutdown();
            using var shutdown = resources.ShutdownAsync();
            while (!shutdown.IsDone) yield return null;
            Assert.That(shutdown.Status, Is.EqualTo(ResourceRequestStatus.Succeeded));
            Assert.That(host.Equipments, Is.Empty); Assert.That(EquipmentManager.GetConfig(prefix), Is.Null);
        }

        [UnityTest]
        public IEnumerator SessionInvalidationBeforeEquipmentResetStillReleasesRecordedLeases()
        {
            Prepare();
            var host = new Host(); EquipmentManager.CreateAndEquip(host, 1001);
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                typeof(ResourceSystem).GetMethod("EndSession", flags).Invoke(null, null);
                Assert.That(ResourceSystem.Current, Is.Null);
                yield return null;
                EquipmentManager.Shutdown();
                Assert.That(host.Equipments, Is.Empty); Assert.That(EquipmentManager.GetConfig(prefix), Is.Null);
            }
            finally
            {
                // 本用例只终结自己创建的 authority，恢复其 admission 门以隔离后续测试。
                typeof(ResourceSystem).GetMethod("ResetSession", flags).Invoke(null, null);
            }
        }

        private sealed class Host : IEquippable
        {
            public List<IEquipment> Equipments { get; } = new();
            public bool IsCanEquip(IEquipment equipment) => true;
        }

        private sealed class ReleasingSource : IResourceSource
        {
            public ResourceSourceId Id => new("equipment-release-fixture");
            public bool SupportsSynchronousLoad => true;
            internal EquipmentsConfig Root;
            internal Action Releasing;
            public void Validate(ResourceCatalogEntry entry) { }
            public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type) => new(Root);
            public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type) => throw new NotSupportedException();
            public void Release(Object asset) => Releasing?.Invoke();
        }
        public sealed class ReleaseScript : IEquipment
        {
            public IEquippable Equiper { get; set; }
            public EquipmentConfigData EquipmentData { get; set; }
            public void OnLoaded() { } public void OnUnloaded() { } public void OnEquip() { } public void OnUnEquip() { }
        }
        [Test]
        public void RootRetirementRemainsActiveInsideSourceReleaseCallback()
        {
            Assert.That(ResourceSystem.Current, Is.Null); EquipmentManager.Shutdown();
            var config = ScriptableObject.CreateInstance<EquipmentsConfig>(); config.name = "RetiringRoot";
            var row = ScriptableObject.CreateInstance<EquipmentConfigData>(); row.id = 1001; row.parent = config;
            row.equipmentType = EquipmentType.Script; row.equipment_class_full_name = typeof(ReleaseScript).FullName; config.Equipments.Add(row);
            var source = new ReleasingSource { Root = config }; var address = new ResourceAddress("equipment-release", "root");
            try
            {
                using var init = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[] { new ResourceCatalog("equipment-release", new[] {
                    new ResourceCatalogEntry("root", address, new ResourceLocation(source.Id, "root", typeof(EquipmentsConfig))) }) }, new IResourceSource[] { source }));
                resources = init.TakeResult(); EquipmentManager.LoadConfig(resources, address);
                var rejected = false;
                source.Releasing = () => { try { EquipmentManager.AddConfig(config); } catch (InvalidOperationException) { rejected = true; } };
                EquipmentManager.RemoveConfig(config);
                Assert.That(rejected, Is.True); Assert.That(EquipmentManager.GetConfig("RetiringRoot"), Is.Null);
            }
            finally { EquipmentManager.Shutdown(); Object.DestroyImmediate(row); Object.DestroyImmediate(config); }
        }
    }
}
#endif
