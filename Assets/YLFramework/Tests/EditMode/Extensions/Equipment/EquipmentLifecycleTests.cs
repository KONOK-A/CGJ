using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using YLFramework.EquipmentSystem;

namespace YLFramework.Tests.Extensions.Equipment
{
    public class EquipmentLifecycleTests
    {
        public class Probe : IEquipment
        {
            public static Action<Probe> Loading;
            public IEquippable Equiper { get; set; }
            public EquipmentConfigData EquipmentData { get; set; }
            public int Loads, Unloads, Equips, UnEquips;
            public Action Equipping, Unequipping, Unloading;
            public void OnLoaded() { Loads++; Assert.That(EquipmentData, Is.Not.Null); Assert.That(Equiper, Is.Null); Loading?.Invoke(this); }
            public void OnUnloaded() { Unloads++; try { Unloading?.Invoke(); } finally { Equipping = Unequipping = Unloading = null; } }
            public void OnEquip() { Equips++; Equipping?.Invoke(); }
            public void OnUnEquip() { UnEquips++; Unequipping?.Invoke(); }
        }

        public sealed class EqualProbe : Probe
        {
            public override bool Equals(object obj) => obj is EqualProbe;
            public override int GetHashCode() => 1;
        }

        public class Host : IEquippable
        {
            public List<IEquipment> Equipments { get; } = new();
            public Func<IEquipment, bool> Allow = _ => true;
            public bool IsCanEquip(IEquipment equipment) => Allow(equipment);
        }

        private EquipmentsConfig root;
        private EquipmentConfigData data;
        private static Type Manager => typeof(IEquipment).Assembly.GetType("YLFramework.EquipmentSystem.EquipmentManager");

        private static object Call(string name, params object[] args)
        {
            Assert.That(Manager, Is.Not.Null, "YL EquipmentManager has not been migrated.");
            var method = Manager.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == name && m.GetParameters().Length == args.Length &&
                m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(valid => valid));
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

        private static Probe Create() => (Probe)Call("CreateEquipment", 1001);
        private static EquipState Equip(Host host, IEquipment equipment) => (EquipState)Call("Equip", host, equipment);
        private static void Destroy(IEquipment equipment) => Call("DestroyEquipment", new object[] { equipment });
        private static void Close(Host host) => Call("CloseHost", host);

        [SetUp]
        public void SetUp()
        {
            Assert.That(Manager, Is.Not.Null, "YL EquipmentManager has not been migrated.");
            Call("Shutdown");
            Probe.Loading = null;
            root = ScriptableObject.CreateInstance<EquipmentsConfig>(); root.name = "EquipmentTests";
            data = ScriptableObject.CreateInstance<EquipmentConfigData>();
            data.parent = root; data.id = 1001; data.equipmentType = EquipmentType.Script;
            data.equipment_class_full_name = typeof(Probe).FullName;
            root.Equipments.Add(data);
            Call("AddConfig", root);
        }

        [TearDown]
        public void TearDown()
        {
            Probe.Loading = null;
            try { if (Manager != null) Call("Shutdown"); }
            finally { if (root) UnityEngine.Object.DestroyImmediate(root); if (data) UnityEngine.Object.DestroyImmediate(data); }
        }

        [Test]
        public void FactoryAssignsDefinitionBeforeCallbackAndReturnsDetachedInstances()
        {
            var first = Create();
            Assert.That(first.EquipmentData, Is.SameAs(data));
            Destroy(first);
            Assert.That(first.EquipmentData, Is.Null);
            Assert.That(first.Unloads, Is.EqualTo(1));
            var second = Create();
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Loads, Is.EqualTo(2));
            root.name = "Renamed";
            Call("RemoveConfig", root);
            Assert.That(second.Unloads, Is.EqualTo(2));
            Assert.That(Call("GetConfig", "EquipmentTests"), Is.Null);
        }

        [Test]
        public void SameInstanceCannotBelongToTwoHostsButSameIdInstancesCan()
        {
            var first = Create(); var second = Create(); var a = new Host(); var b = new Host();
            Assert.That(Equip(a, first), Is.EqualTo(EquipState.Success));
            Assert.That(Equip(a, first), Is.EqualTo(EquipState.AlreadyEquipped));
            Assert.That(Equip(b, first), Is.EqualTo(EquipState.AlreadyEquipped));
            Assert.That(Equip(a, second), Is.EqualTo(EquipState.Success));
            Assert.That(first.Equiper, Is.SameAs(a)); Assert.That(a.Equipments.Count, Is.EqualTo(2)); Assert.That(b.Equipments, Is.Empty);
        }

        [Test]
        public void RejectedCreateAndEquipReturnsItsOwnedInstance()
        {
            Probe created = null; Probe.Loading = value => created = value;
            var host = new Host { Allow = _ => false };
            Assert.That(Call("CreateAndEquip", host, 1001), Is.Null);
            Assert.That(host.Equipments, Is.Empty); Assert.That(created.Unloads, Is.EqualTo(1)); Assert.That(created.Equips, Is.Zero);
        }

        [Test]
        public void ThrowingEquipCompensatesAttachmentAndCreatedOwnership()
        {
            Probe created = null;
            Probe.Loading = value => { created = value; value.Equipping = () => throw new InvalidOperationException("equip-fail"); };
            var host = new Host();
            Assert.That(Assert.Throws<InvalidOperationException>(() => Call("CreateAndEquip", host, 1001)).Message, Is.EqualTo("equip-fail"));
            Assert.That(host.Equipments, Is.Empty); Assert.That(created.Equiper, Is.Null);
            Assert.That(created.UnEquips, Is.EqualTo(1)); Assert.That(created.Unloads, Is.EqualTo(1));
        }

        [Test]
        public void BatchUnEquipContinuesAfterCallbackThrows()
        {
            var host = new Host(); var a = Create(); var b = Create(); Equip(host, a); Equip(host, b);
            b.Unequipping = () => throw new InvalidOperationException("un-equip-fail");
            Assert.Throws<AggregateException>(() => Call("UnEquipAll", host, true));
            Assert.That(host.Equipments, Is.Empty); Assert.That(a.Equiper, Is.Null); Assert.That(b.Equiper, Is.Null);
            Assert.That(a.Unloads, Is.EqualTo(1)); Assert.That(b.Unloads, Is.EqualTo(1));
        }

        [Test]
        public void DetachedOwnedInstanceCanBeEquippedAgainBeforeExplicitReturn()
        {
            var host = new Host(); var value = Create(); Equip(host, value);
            Call("UnEquip", value, false);
            Assert.That(value.Equiper, Is.Null); Assert.That(value.Unloads, Is.Zero); Assert.That(value.EquipmentData, Is.SameAs(data));
            Assert.That(Equip(host, value), Is.EqualTo(EquipState.Success));
            Destroy(value); Destroy(value);
            Assert.That(value.Unloads, Is.EqualTo(1)); Assert.That(value.UnEquips, Is.EqualTo(2));
            Destroy(null);
        }

        [Test]
        public void BorrowedObjectsAreOnlyDetachedAndUseReferenceIdentity()
        {
            var host = new Host(); var a = new EqualProbe { EquipmentData = data }; var b = new EqualProbe { EquipmentData = data };
            Assert.That(Equip(host, a), Is.EqualTo(EquipState.Success)); Assert.That(Equip(host, b), Is.EqualTo(EquipState.Success));
            Call("UnEquip", a, true);
            Assert.That(host.Equipments.Count, Is.EqualTo(1)); Assert.That(ReferenceEquals(host.Equipments[0], b), Is.True);
            Assert.Throws<InvalidOperationException>(() => Destroy(a));
            Close(host);
            Assert.That(host.Equipments, Is.Empty); Assert.That(a.Unloads + b.Unloads, Is.Zero); Assert.That(b.EquipmentData, Is.SameAs(data));
        }

        [Test]
        public void ClosingInsidePermissionCallbackCancelsIncomingAttachment()
        {
            var host = new Host(); var value = Create();
            host.Allow = _ => { Close(host); return true; };
            Assert.That(Equip(host, value), Is.EqualTo(EquipState.CannotEquip));
            Assert.That(host.Equipments, Is.Empty); Assert.That(value.Equips, Is.Zero);
        }

        [Test]
        public void ReentrantEquipIsRejectedBeforeMutatingHost()
        {
            var host = new Host(); var value = Create();
            host.Allow = _ => { Assert.Throws<InvalidOperationException>(() => Equip(host, value)); return true; };
            Assert.That(Equip(host, value), Is.EqualTo(EquipState.Success)); Assert.That(host.Equipments.Count, Is.EqualTo(1));
        }

        [Test]
        public void SelfDestroyDuringEquipDoesNotReturnSuccessOrDoubleUnload()
        {
            var host = new Host(); Probe value = null;
            Probe.Loading = created => { value = created; created.Equipping = () => Destroy(created); };
            Assert.That(Call("CreateAndEquip", host, 1001), Is.Null);
            Assert.That(host.Equipments, Is.Empty); Assert.That(value.UnEquips, Is.EqualTo(1)); Assert.That(value.Unloads, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosingDomainDuringEquipCannotPublishAStaleResult(bool shutdown)
        {
            var host = new Host(); Probe value = null;
            Probe.Loading = created => { value = created; created.Equipping = () => { if (shutdown) Call("Shutdown"); else Call("RemoveConfig", root); }; };
            Assert.That(Call("CreateAndEquip", host, 1001), Is.Null);
            Assert.That(host.Equipments, Is.Empty); Assert.That(value.Unloads, Is.EqualTo(1));
            Assert.That(Call("GetConfig", "EquipmentTests"), Is.Null);
        }

        [Test]
        public void UnloadCallbackCannotReacquireItsStillExecutingObject()
        {
            var first = Create(); Probe second = null;
            first.Unloading = () => second = Create();
            Destroy(first);
            Assert.That(second, Is.Not.Null); Assert.That(ReferenceEquals(first, second), Is.False);
            Assert.That(second.EquipmentData, Is.SameAs(data)); Assert.That(second.Unloads, Is.Zero);
        }

        [Test]
        public void FailedLoadedObjectIsDiscardedRatherThanReused()
        {
            Probe failed = null; Probe.Loading = value => { failed = value; throw new InvalidOperationException("load-fail"); };
            Assert.Throws<InvalidOperationException>(() => Create());
            Probe.Loading = null;
            Assert.That(Create(), Is.Not.SameAs(failed)); Assert.That(failed.Unloads, Is.EqualTo(1));
        }

        [Test]
        public void InvalidConfigDoesNotPublishEarlierValidRows()
        {
            var rejected = ScriptableObject.CreateInstance<EquipmentsConfig>(); rejected.name = "Rejected";
            var valid = ScriptableObject.CreateInstance<EquipmentConfigData>();
            var invalid = ScriptableObject.CreateInstance<EquipmentConfigData>();
            try
            {
                valid.id = 1002; valid.equipmentType = EquipmentType.Script; valid.equipment_class_full_name = typeof(Probe).FullName;
                invalid.id = 1003; invalid.equipmentType = (EquipmentType)99;
                rejected.Equipments.Add(valid); rejected.Equipments.Add(invalid);
                Assert.Throws<InvalidOperationException>(() => Call("AddConfig", rejected));
                Assert.That(Call("GetConfig", "Rejected"), Is.Null);
                Assert.That(Call("GetEquipmentInfo", 1002), Is.Null);
                Assert.That(Call("GetEquipmentInfo", 1001), Is.SameAs(data));
            }
            finally { UnityEngine.Object.DestroyImmediate(rejected); UnityEngine.Object.DestroyImmediate(valid); UnityEngine.Object.DestroyImmediate(invalid); }
        }

        [Test]
        public void ShutdownContinuesAfterUnloadFailureAndCanBeReconfigured()
        {
            var first = Create(); var second = Create();
            first.Unloading = () => throw new InvalidOperationException("unload-fail");
            Assert.Throws<AggregateException>(() => Call("Shutdown"));
            Assert.That(first.Unloads, Is.EqualTo(1)); Assert.That(second.Unloads, Is.EqualTo(1));
            Assert.That(Call("GetEquipmentInfo", 1001), Is.Null);
            Call("AddConfig", root); Assert.That(Create().EquipmentData, Is.SameAs(data));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void BatchSnapshotDoesNotDetachANewAttachment(bool borrowed, bool sameHost)
        {
            var host = new Host(); var next = sameHost ? host : new Host();
            var a = borrowed ? new Probe { EquipmentData = data } : Create(); var b = borrowed ? new Probe { EquipmentData = data } : Create();
            Equip(host, a); Equip(host, b);
            Probe moved = null;
            a.Unequipping = () => { if (moved != null) return; moved = b; Call("UnEquip", b, false); Equip(next, b); };
            b.Unequipping = () => { if (moved != null) return; moved = a; Call("UnEquip", a, false); Equip(next, a); };
            Call("UnEquipAll", host, false);
            Assert.That(moved, Is.Not.Null); Assert.That(moved.Equiper, Is.SameAs(next)); Assert.That(next.Equipments.Any(e => ReferenceEquals(e, moved)), Is.True);
            Assert.That(moved.UnEquips, Is.EqualTo(1));
            Close(next); Assert.That(next.Equipments, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CloseHostCannotRemoveAnInstanceTransferredByEarlierCallback(bool borrowed)
        {
            var host = new Host(); var next = new Host();
            var a = borrowed ? new Probe { EquipmentData = data } : Create(); var b = borrowed ? new Probe { EquipmentData = data } : Create();
            Equip(host, a); Equip(host, b);
            Probe moved = null;
            a.Unequipping = () => { if (moved != null) return; moved = b; Call("UnEquip", b, false); Equip(next, b); };
            b.Unequipping = () => { if (moved != null) return; moved = a; Call("UnEquip", a, false); Equip(next, a); };
            Close(host);
            Assert.That(moved, Is.Not.Null); Assert.That(moved.Equiper, Is.SameAs(next)); Assert.That(moved.Unloads, Is.Zero);
            Close(next); Assert.That(next.Equipments, Is.Empty);
        }

        [Test]
        public void RemovingConfigRejectsNewBorrowerOfItsClosingDefinitions()
        {
            var host = new Host(); var a = Create(); Equip(host, a);
            var borrowed = new Probe { EquipmentData = data };
            a.Unequipping = () => Assert.Throws<InvalidOperationException>(() => Equip(host, borrowed));
            Call("RemoveConfig", root);
            Assert.That(host.Equipments, Is.Empty); Assert.That(borrowed.Unloads, Is.Zero);
        }

        [Test]
        public void CloseHostKeepsItsBarrierUntilDeferredUnloadCallbackCompletes()
        {
            var host = new Host(); var a = Create(); Equip(host, a);
            var borrowed = new Probe { EquipmentData = data };
            a.Unloading = () => Assert.Throws<InvalidOperationException>(() => Equip(host, borrowed));
            Close(host); Assert.That(host.Equipments, Is.Empty);
        }
    }
}
