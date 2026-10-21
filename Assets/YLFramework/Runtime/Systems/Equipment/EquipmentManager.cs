using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;
using YLFramework.Foundation;
using YLFramework.Services.Resource;

namespace YLFramework.EquipmentSystem
{
    /// <summary>主线程装备入口。创建结果由本域回收，直接 Equip 的外部对象只借用。</summary>
    public static class EquipmentManager
    {
        private sealed class Identity<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly Identity<T> Instance = new();
            public bool Equals(T a, T b) => ReferenceEquals(a, b);
            public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
        }
        private sealed class Config
        {
            internal EquipmentsConfig Root;
            internal string Name;
            internal IDisposable Lease;
            internal bool Closing;
        }
        private sealed class Record
        {
            internal IEquipment Value;
            internal Config Config;
            internal EquipmentConfigData Definition;
            internal EquipmentsConfig DefinitionRoot;
            internal ObjectPool<IEquipment> Pool;
            internal InstanceLease Lease;
            internal IEquippable Host, PendingHost;
            internal List<IEquipment> List;
            internal bool Owned, Loaded, Attached, Notified, Equipping, Unequipping, CancelEquip;
            internal bool ReleaseRequested, Returning, Discard;
            internal long Revision;
        }

        private static readonly Dictionary<string, Config> Configs = new(StringComparer.Ordinal);
        private static readonly Dictionary<IEquipment, Record> Records = new(Identity<IEquipment>.Instance);
        private static readonly Dictionary<Type, ObjectPool<IEquipment>> Pools = new();
        private static readonly HashSet<IEquippable> Closing = new(Identity<IEquippable>.Instance);
        private static readonly ConditionalWeakTable<IEquipment, object> Returned = new();
        private static readonly object ReturnedMarker = new();
        private static readonly Queue<Record> Pending = new();
        private static readonly List<Config> Retired = new();
        private static int depth;
        private static bool flushing, stopping, quitting, aggregateReturns;

        static EquipmentManager()
        {
            SingletonSessionState.RegisterSessionReset(() => { quitting = false; Shutdown(); });
            Application.quitting += () => { quitting = true; Shutdown(); };
        }

        private static bool Alive(object value) => value != null && (!(value is UnityEngine.Object obj) || obj);
        private static void Admit()
        {
            if (stopping || quitting && Application.isPlaying) throw new InvalidOperationException("Equipment system is closing.");
        }
        private static void Attempt(List<Exception> errors, Action action)
        {
            try { action(); } catch (Exception error) { errors.Add(error); }
        }
        private static void ThrowErrors(List<Exception> errors, bool aggregate = false)
        {
            if (errors.Count == 0) return;
            if (aggregate || errors.Count > 1) throw new AggregateException(errors);
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        }
        private static T Run<T>(Func<T> operation)
        {
            depth++;
            var errors = new List<Exception>();
            T result = default;
            try { result = operation(); }
            catch (Exception error) { errors.Add(error); }
            finally { if (--depth == 0 && !flushing) Attempt(errors, Flush); }
            ThrowErrors(errors);
            return result;
        }
        private static void Run(Action operation) => Run(() => { operation(); return 0; });
        private static bool Current(Record record) => Records.TryGetValue(record.Value, out var current) && ReferenceEquals(current, record);
        private static bool DependsOn(Record record, Config config) => ReferenceEquals(record.Config, config) ||
            ReferenceEquals(record.DefinitionRoot, config.Root) || !ReferenceEquals(record.Definition, null) && config.Root && config.Root.Equipments.Any(d => ReferenceEquals(d, record.Definition));
        private static void CaptureDefinition(Record record)
        {
            if (record.Owned) return;
            record.Definition = record.Value.EquipmentData;
            record.DefinitionRoot = record.Definition ? record.Definition.parent : null;
        }
        private static bool DefinitionClosing(Record record) => record.Config != null && record.Config.Closing || Retired.Any(config => DependsOn(record, config));

        public static void AddConfig(EquipmentsConfig config) => Run(() => AddConfigCore(config));
        private static void AddConfigCore(EquipmentsConfig root)
        {
            Admit();
            if (!root) throw new ArgumentNullException(nameof(root));
            if (Retired.Any(c => ReferenceEquals(c.Root, root))) throw new InvalidOperationException("Equipment config is closing.");
            if (Configs.Values.Any(c => ReferenceEquals(c.Root, root))) return;
            if (Configs.ContainsKey(root.name)) throw new InvalidOperationException($"Equipment config name already registered: {root.name}");
            var ids = new HashSet<int>();
            foreach (var data in root.Equipments)
            {
                if (!data) throw new InvalidOperationException($"Equipment '{root.name}' contains a missing row.");
                if (!ids.Add(data.id) || Find(data.id, out _) != null) throw new InvalidOperationException($"Equipment '{root.name}': duplicate ID {data.id}.");
                Validate(data);
            }
            Configs.Add(root.name, new Config { Root = root, Name = root.name });
        }

        public static void LoadConfig(ResourceSystem resources, ResourceAddress address) => Run(() =>
        {
            Admit();
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            var lease = resources.Acquire<EquipmentsConfig>(address);
            try
            {
                AddConfigCore(lease.Asset);
                var config = Configs.Values.First(c => ReferenceEquals(c.Root, lease.Asset));
                if (config.Lease == null) config.Lease = lease;
                else lease.Dispose();
            }
            catch { lease.Dispose(); throw; }
        });

        public static EquipmentsConfig GetConfig(string name) => name != null && Configs.TryGetValue(name, out var config) && config.Root ? config.Root : null;
        public static EquipmentConfigData GetEquipmentInfo(int id) => Find(id, out _);
        private static EquipmentConfigData Find(int id, out Config owner)
        {
            foreach (var config in Configs.Values)
            {
                if (!config.Root) continue;
                var data = config.Root.GetEquipmentConfigData(id);
                if (!data) continue;
                owner = config;
                return data;
            }
            owner = null;
            return null;
        }

        public static void RemoveConfig(string name) => RemoveConfig(GetConfig(name));
        public static void RemoveConfig(EquipmentsConfig root) => Run(() =>
        {
            var config = Configs.Values.FirstOrDefault(c => ReferenceEquals(c.Root, root));
            if (config == null) return;
            aggregateReturns = true;
            config.Closing = true;
            Configs.Remove(config.Name);
            Retired.Add(config);
            var errors = new List<Exception>();
            foreach (var record in Records.Values.ToArray()) Attempt(errors, () =>
            {
                if (Current(record) && DependsOn(record, config)) { record.CancelEquip = true; UnEquipCore(record, true); }
            });
            ThrowErrors(errors, true);
        });

        private static Type Validate(EquipmentConfigData data)
        {
            if (data.equipmentType == EquipmentType.Asset)
            {
                _ = new ResourceAddress(data.projectName, data.assetName);
                return null;
            }
            var type = AssemblyTool.GetType(data.equipment_class_full_name);
            if (data.equipmentType != EquipmentType.Script || type == null || !type.IsClass || type.IsAbstract || type.ContainsGenericParameters ||
                !typeof(IEquipment).IsAssignableFrom(type) || typeof(UnityEngine.Object).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"Invalid equipment type: ID={data.id}, mode={data.equipmentType}, class={data.equipment_class_full_name}");
            return type;
        }

        public static IEquipment CreateEquipment(int equipmentId) => Run(() =>
        {
            Admit();
            var data = Find(equipmentId, out var config);
            if (!data) throw new InvalidOperationException($"Equipment definition not found: {equipmentId}");
            var type = Validate(data);
            var record = new Record { Config = config, Owned = true, Definition = data, DefinitionRoot = config.Root };
            try
            {
                if (data.equipmentType == EquipmentType.Asset)
                {
                    var resources = ResourceSystem.Current ?? throw new InvalidOperationException("Initialize ResourceSystem before creating Asset equipment.");
                    record.Lease = resources.Instantiate(new ResourceAddress(data.projectName, data.assetName));
                    var components = record.Lease.GameObject.GetComponents<MonoBehaviour>().OfType<IEquipment>().ToArray();
                    if (components.Length != 1) throw new InvalidOperationException($"Equipment {equipmentId} requires exactly one IEquipment component on its Prefab root.");
                    record.Value = components[0];
                }
                else
                {
                    if (!Pools.TryGetValue(type, out var pool))
                    {
                        pool = new ObjectPool<IEquipment>(() => (IEquipment)Activator.CreateInstance(type));
                        Pools.Add(type, pool);
                    }
                    record.Pool = pool;
                    record.Value = pool.Allocate();
                }
                Returned.Remove(record.Value);
                Records.Add(record.Value, record);
                if (config.Closing || stopping) throw new InvalidOperationException("Equipment creation was cancelled by domain closure.");
                record.Value.Equiper = null;
                record.Value.EquipmentData = data;
                record.Loaded = true;
                record.Value.OnLoaded();
                if (config.Closing || record.ReleaseRequested || stopping || !Alive(record.Value)) throw new InvalidOperationException("Equipment creation was cancelled during OnLoaded.");
                return record.Value;
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                if (record.Value != null)
                {
                    record.Discard = true;
                    Attempt(errors, () => UnEquipCore(record, true));
                }
                else if (record.Lease != null) Attempt(errors, record.Lease.Discard);
                ThrowErrors(errors);
                throw;
            }
        });

        public static EquipState Equip(IEquippable equipper, IEquipment equipment) => Run(() =>
        {
            Admit();
            if (!Alive(equipper)) throw new ArgumentException("A live equipment host is required.", nameof(equipper));
            if (!Alive(equipment)) throw new ArgumentException("A live equipment instance is required.", nameof(equipment));
            if (Closing.Contains(equipper)) throw new InvalidOperationException("Equipment host is closing.");
            var list = equipper.Equipments ?? throw new ArgumentException("Initialize IEquippable.Equipments.", nameof(equipper));
            Records.TryGetValue(equipment, out var record);
            if (record != null && (record.Equipping || record.Unequipping || record.ReleaseRequested)) throw new InvalidOperationException("Equipment is inside a lifecycle transition.");
            if (record != null && record.Attached || equipment.Equiper != null || list.Any(e => ReferenceEquals(e, equipment))) return EquipState.AlreadyEquipped;
            if (Returned.TryGetValue(equipment, out _)) throw new InvalidOperationException("Returned equipment references cannot be equipped.");
            var external = record == null;
            if (external) record = new Record { Value = equipment };
            CaptureDefinition(record);
            if (DefinitionClosing(record)) throw new InvalidOperationException("Equipment definition is closing.");
            if (external) Records.Add(equipment, record);
            record.CancelEquip = false;
            record.Equipping = true;
            record.PendingHost = equipper;
            try
            {
                var permitted = equipper.IsCanEquip(equipment);
                if (Alive(equipment)) CaptureDefinition(record);
                if (!permitted || record.CancelEquip || record.ReleaseRequested || stopping || !Alive(equipper) || !Alive(equipment) || DefinitionClosing(record))
                    return EquipState.CannotEquip;
                if (!ReferenceEquals(list, equipper.Equipments)) throw new InvalidOperationException("The equipment host replaced its list during Equip.");
                record.Host = equipper;
                record.List = list;
                record.Attached = true;
                record.Revision++;
                list.Add(equipment);
                equipment.Equiper = equipper;
                if (!record.Attached || record.CancelEquip) return EquipState.CannotEquip;
                record.Notified = true;
                equipment.OnEquip();
                return record.Attached && !record.ReleaseRequested && !record.CancelEquip ? EquipState.Success : EquipState.CannotEquip;
            }
            catch (Exception error)
            {
                record.Discard = true;
                var errors = new List<Exception> { error };
                Attempt(errors, () => UnEquipCore(record, false));
                ThrowErrors(errors);
                throw;
            }
            finally
            {
                record.Equipping = false;
                record.PendingHost = null;
                ForgetBorrowed(record);
            }
        });

        public static IEquipment CreateAndEquip(IEquippable equipper, int equipmentId) => Run(() =>
        {
            var equipment = CreateEquipment(equipmentId);
            try
            {
                if (Equip(equipper, equipment) == EquipState.Success) return equipment;
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                Attempt(errors, () => DestroyEquipment(equipment));
                ThrowErrors(errors);
            }
            DestroyEquipment(equipment);
            return null;
        });

        public static void UnEquip(IEquipment equipment, bool destroy = true) => Run(() =>
        {
            if (equipment != null && Records.TryGetValue(equipment, out var record)) UnEquipCore(record, destroy);
        });

        private static void UnEquipCore(Record record, bool destroy)
        {
            if (!Current(record)) return;
            var errors = new List<Exception>();
            if (record.Attached)
            {
                record.Attached = false;
                record.List?.RemoveAll(e => ReferenceEquals(e, record.Value));
                var notified = record.Notified;
                record.Notified = false;
                record.Unequipping = true;
                if (record.Equipping) record.CancelEquip = true;
                try { if (notified && Alive(record.Value)) Attempt(errors, record.Value.OnUnEquip); }
                finally
                {
                    if (Alive(record.Value)) Attempt(errors, () => record.Value.Equiper = null);
                    record.Host = null;
                    record.List = null;
                    record.Unequipping = false;
                }
            }
            if (errors.Count != 0) record.Discard = true;
            if (destroy && record.Owned && !record.ReleaseRequested)
            {
                record.ReleaseRequested = true;
                Pending.Enqueue(record);
            }
            ForgetBorrowed(record);
            ThrowErrors(errors);
        }

        private static void ForgetBorrowed(Record record)
        {
            if (!record.Owned && !record.Attached && !record.Equipping && !record.Unequipping && Current(record)) Records.Remove(record.Value);
        }

        public static void DestroyEquipment(IEquipment equipment) => Run(() =>
        {
            if (equipment == null) return;
            if (Records.TryGetValue(equipment, out var record) && record.Owned) { UnEquipCore(record, true); return; }
            if (Returned.TryGetValue(equipment, out _) || !Alive(equipment)) return;
            throw new InvalidOperationException("Cannot destroy equipment owned by an external caller.");
        });

        public static void UnEquipAll(IEquippable equipper, bool destroy = true) => Run(() =>
        {
            if (equipper == null) return;
            aggregateReturns = true;
            var errors = new List<Exception>();
            var records = Records.Values.Where(r => ReferenceEquals(r.Host, equipper)).Select(r => (Record: r, r.Revision)).ToArray();
            foreach (var snapshot in records)
                if (Current(snapshot.Record) && snapshot.Record.Revision == snapshot.Revision && ReferenceEquals(snapshot.Record.Host, equipper))
                    Attempt(errors, () => UnEquipCore(snapshot.Record, destroy));
            if (Alive(equipper)) equipper.Equipments?.RemoveAll(e => !Alive(e));
            ThrowErrors(errors, true);
        });

        public static void CloseHost(IEquippable equipper) => Run(() =>
        {
            if (equipper == null || !Closing.Add(equipper)) return;
            aggregateReturns = true;
            var errors = new List<Exception>();
            foreach (var snapshot in Records.Values.Where(r => ReferenceEquals(r.Host, equipper) || ReferenceEquals(r.PendingHost, equipper)).Select(r => (Record: r, r.Revision)).ToArray())
            {
                var record = snapshot.Record;
                if (!Current(record) || record.Revision != snapshot.Revision || !(ReferenceEquals(record.Host, equipper) || ReferenceEquals(record.PendingHost, equipper))) continue;
                record.CancelEquip = true;
                if (record.Attached || record.Unequipping) Attempt(errors, () => UnEquipCore(record, true));
            }
            if (Alive(equipper)) equipper.Equipments?.RemoveAll(e => !Alive(e));
            ThrowErrors(errors, true);
        });

        public static void Shutdown() => Run(() =>
        {
            if (stopping) return;
            stopping = true;
            aggregateReturns = true;
            foreach (var config in Configs.Values) { config.Closing = true; Retired.Add(config); }
            Configs.Clear();
            var errors = new List<Exception>();
            foreach (var record in Records.Values.ToArray()) { record.CancelEquip = true; Attempt(errors, () => UnEquipCore(record, true)); }
            foreach (var pool in Pools.Values) Attempt(errors, pool.Dispose);
            Pools.Clear();
            ThrowErrors(errors, true);
        });

        // 所有外层回调退出后才真正归还，防止旧回调继续访问已被下一次创建复用的实例。
        private static void Flush()
        {
            flushing = true;
            depth++;
            var errors = new List<Exception>();
            var aggregate = false;
            try
            {
                while (Pending.Count != 0 || Retired.Count != 0)
                {
                    while (Pending.Count != 0)
                    {
                        var record = Pending.Dequeue();
                        if (record.Returning) continue;
                        record.Returning = true;
                        var cleanup = new List<Exception>();
                        if (record.Loaded && Alive(record.Value)) Attempt(cleanup, record.Value.OnUnloaded);
                        if (Alive(record.Value))
                        {
                            Attempt(cleanup, () => record.Value.Equiper = null);
                            Attempt(cleanup, () => record.Value.EquipmentData = null);
                        }
                        Records.Remove(record.Value);
                        Returned.Remove(record.Value);
                        Returned.Add(record.Value, ReturnedMarker);
                        var discard = record.Discard || cleanup.Count != 0;
                        if (record.Lease != null) Attempt(cleanup, () => { if (discard) record.Lease.Discard(); else record.Lease.Return(); });
                        if (record.Pool != null) Attempt(cleanup, () => { if (discard) record.Pool.Discard(record.Value); else record.Pool.Recycle(record.Value); });
                        errors.AddRange(cleanup);
                    }
                    if (Retired.Count != 0)
                    {
                        var config = Retired[0];
                        try { if (config.Lease != null) Attempt(errors, config.Lease.Dispose); }
                        finally { config.Lease = null; Retired.Remove(config); }
                    }
                }
            }
            finally { aggregate = aggregateReturns; aggregateReturns = false; depth--; flushing = false; stopping = false; Closing.Clear(); }
            ThrowErrors(errors, aggregate);
        }
    }
}
