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
            internal bool IsClosing;
        }

        private sealed class Record
        {
            internal IEquipment Value;
            internal Config Config;
            internal EquipmentConfigData Definition;
            internal EquipmentsConfig DefinitionRoot;
            internal ObjectPool<IEquipment> Pool;
            internal InstanceLease Lease;
            internal IEquippable Host;
            internal IEquippable PendingHost;
            internal List<IEquipment> List;
            internal bool IsOwned;
            internal bool IsLoaded;
            internal bool IsAttached;
            internal bool IsNotified;
            internal bool IsEquipping;
            internal bool IsUnequipping;
            internal bool IsEquipCancelled;
            internal bool IsReleaseRequested;
            internal bool IsReturning;
            internal bool ShouldDiscard;
            internal long Revision;
        }

        private static readonly Dictionary<string, Config> _configs = new(StringComparer.Ordinal);
        private static readonly Dictionary<IEquipment, Record> _records = new(Identity<IEquipment>.Instance);
        private static readonly Dictionary<Type, ObjectPool<IEquipment>> _pools = new();
        private static readonly HashSet<IEquippable> _closingHosts = new(Identity<IEquippable>.Instance);
        private static readonly ConditionalWeakTable<IEquipment, object> _returnedEquipment = new();
        private static readonly object _returnedMarker = new();
        private static readonly Queue<Record> _pendingReturns = new();
        private static readonly List<Config> _retiredConfigs = new();
        private static int _depth;
        private static bool _isFlushing;
        private static bool _isStopping;
        private static bool _isQuitting;
        private static bool _aggregateReturns;

        static EquipmentManager()
        {
            SingletonSessionState.RegisterSessionReset(() =>
            {
                _isQuitting = false;
                Shutdown();
            });
            Application.quitting += () =>
            {
                _isQuitting = true;
                Shutdown();
            };
        }

        private static bool IsAlive(object value) => value != null && (!(value is UnityEngine.Object obj) || obj);

        private static void Admit()
        {
            if (_isStopping || _isQuitting && Application.isPlaying)
            {
                throw new InvalidOperationException("Equipment system is closing.");
            }
        }

        private static void Attempt(List<Exception> errors, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        private static void ThrowErrors(List<Exception> errors, bool aggregate = false)
        {
            if (errors.Count == 0)
            {
                return;
            }

            if (aggregate || errors.Count > 1)
            {
                throw new AggregateException(errors);
            }

            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        }

        private static T Run<T>(Func<T> operation)
        {
            _depth++;
            var errors = new List<Exception>();
            T result = default;
            try
            {
                result = operation();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            finally
            {
                if (--_depth == 0 && !_isFlushing)
                {
                    Attempt(errors, Flush);
                }
            }

            ThrowErrors(errors);
            return result;
        }

        private static void Run(Action operation) => Run(() =>
        {
            operation();
            return 0;
        });

        private static bool IsCurrent(Record record) =>
            _records.TryGetValue(record.Value, out var current) && ReferenceEquals(current, record);

        private static bool DependsOn(Record record, Config config) =>
            ReferenceEquals(record.Config, config) ||
            ReferenceEquals(record.DefinitionRoot, config.Root) ||
            !ReferenceEquals(record.Definition, null) && config.Root &&
            config.Root.Equipments.Any(data => ReferenceEquals(data, record.Definition));

        private static void CaptureDefinition(Record record)
        {
            if (record.IsOwned)
            {
                return;
            }

            record.Definition = record.Value.EquipmentData;
            record.DefinitionRoot = record.Definition ? record.Definition.parent : null;
        }

        private static bool IsDefinitionClosing(Record record) =>
            record.Config != null && record.Config.IsClosing || _retiredConfigs.Any(config => DependsOn(record, config));

        public static void AddConfig(EquipmentsConfig config) => Run(() => AddConfigCore(config));

        private static void AddConfigCore(EquipmentsConfig root)
        {
            Admit();
            if (!root)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (_retiredConfigs.Any(config => ReferenceEquals(config.Root, root)))
            {
                throw new InvalidOperationException("Equipment config is closing.");
            }

            if (_configs.Values.Any(config => ReferenceEquals(config.Root, root)))
            {
                return;
            }

            if (_configs.ContainsKey(root.name))
            {
                throw new InvalidOperationException($"Equipment config name already registered: {root.name}");
            }

            var ids = new HashSet<int>();
            foreach (var data in root.Equipments)
            {
                if (!data)
                {
                    throw new InvalidOperationException($"Equipment '{root.name}' contains a missing row.");
                }

                if (!ids.Add(data.id) || Find(data.id, out _) != null)
                {
                    throw new InvalidOperationException($"Equipment '{root.name}': duplicate ID {data.id}.");
                }

                Validate(data);
            }

            _configs.Add(root.name, new Config { Root = root, Name = root.name });
        }

        public static void LoadConfig(ResourceSystem resources, ResourceAddress address) => Run(() =>
        {
            Admit();
            if (resources == null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            var lease = resources.Acquire<EquipmentsConfig>(address);
            try
            {
                AddConfigCore(lease.Asset);
                var config = _configs.Values.First(candidate => ReferenceEquals(candidate.Root, lease.Asset));
                if (config.Lease == null)
                {
                    config.Lease = lease;
                }
                else
                {
                    lease.Dispose();
                }
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        });

        public static EquipmentsConfig GetConfig(string name) =>
            name != null && _configs.TryGetValue(name, out var config) && config.Root ? config.Root : null;

        public static EquipmentConfigData GetEquipmentInfo(int id) => Find(id, out _);

        private static EquipmentConfigData Find(int id, out Config owner)
        {
            foreach (var config in _configs.Values)
            {
                if (!config.Root)
                {
                    continue;
                }

                var data = config.Root.GetEquipmentConfigData(id);
                if (!data)
                {
                    continue;
                }

                owner = config;
                return data;
            }

            owner = null;
            return null;
        }

        public static void RemoveConfig(string name) => RemoveConfig(GetConfig(name));

        public static void RemoveConfig(EquipmentsConfig root) => Run(() =>
        {
            var config = _configs.Values.FirstOrDefault(candidate => ReferenceEquals(candidate.Root, root));
            if (config == null)
            {
                return;
            }

            _aggregateReturns = true;
            config.IsClosing = true;
            _configs.Remove(config.Name);
            _retiredConfigs.Add(config);
            var errors = new List<Exception>();
            foreach (var record in _records.Values.ToArray())
            {
                Attempt(errors, () =>
                {
                    if (IsCurrent(record) && DependsOn(record, config))
                    {
                        record.IsEquipCancelled = true;
                        UnequipCore(record, true);
                    }
                });
            }

            ThrowErrors(errors, true);
        });

        private static Type Validate(EquipmentConfigData data)
        {
            if (data.equipmentType == EquipmentType.Asset)
            {
                _ = new ResourceAddress(data.projectName, data.assetName);
                return null;
            }

            var type = AssemblyTool.GetType(data.equipmentClassFullName);
            if (data.equipmentType != EquipmentType.Script || type == null || !type.IsClass || type.IsAbstract ||
                type.ContainsGenericParameters || !typeof(IEquipment).IsAssignableFrom(type) ||
                typeof(UnityEngine.Object).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException($"Invalid equipment type: ID={data.id}, mode={data.equipmentType}, class={data.equipmentClassFullName}");
            }

            return type;
        }

        public static IEquipment CreateEquipment(int equipmentId) => Run(() =>
        {
            Admit();
            var data = Find(equipmentId, out var config);
            if (!data)
            {
                throw new InvalidOperationException($"Equipment definition not found: {equipmentId}");
            }

            var type = Validate(data);
            var record = new Record
            {
                Config = config,
                IsOwned = true,
                Definition = data,
                DefinitionRoot = config.Root
            };
            try
            {
                if (data.equipmentType == EquipmentType.Asset)
                {
                    var resources = ResourceSystem.Current ??
                        throw new InvalidOperationException("Initialize ResourceSystem before creating Asset equipment.");
                    record.Lease = resources.Instantiate(new ResourceAddress(data.projectName, data.assetName));
                    var components = record.Lease.GameObject.GetComponents<MonoBehaviour>().OfType<IEquipment>().ToArray();
                    if (components.Length != 1)
                    {
                        throw new InvalidOperationException($"Equipment {equipmentId} requires exactly one IEquipment component on its Prefab root.");
                    }

                    record.Value = components[0];
                }
                else
                {
                    if (!_pools.TryGetValue(type, out var pool))
                    {
                        pool = new ObjectPool<IEquipment>(() => (IEquipment)Activator.CreateInstance(type));
                        _pools.Add(type, pool);
                    }

                    record.Pool = pool;
                    record.Value = pool.Allocate();
                }

                _returnedEquipment.Remove(record.Value);
                _records.Add(record.Value, record);
                if (config.IsClosing || _isStopping)
                {
                    throw new InvalidOperationException("Equipment creation was cancelled by domain closure.");
                }

                record.Value.Equipper = null;
                record.Value.EquipmentData = data;
                record.IsLoaded = true;
                record.Value.OnLoaded();
                if (config.IsClosing || record.IsReleaseRequested || _isStopping || !IsAlive(record.Value))
                {
                    throw new InvalidOperationException("Equipment creation was cancelled during OnLoaded.");
                }

                return record.Value;
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                if (record.Value != null)
                {
                    record.ShouldDiscard = true;
                    Attempt(errors, () => UnequipCore(record, true));
                }
                else if (record.Lease != null)
                {
                    Attempt(errors, record.Lease.Discard);
                }

                ThrowErrors(errors);
                throw;
            }
        });

        public static EquipState Equip(IEquippable equipper, IEquipment equipment) => Run(() =>
        {
            Admit();
            if (!IsAlive(equipper))
            {
                throw new ArgumentException("A live equipment host is required.", nameof(equipper));
            }

            if (!IsAlive(equipment))
            {
                throw new ArgumentException("A live equipment instance is required.", nameof(equipment));
            }

            if (_closingHosts.Contains(equipper))
            {
                throw new InvalidOperationException("Equipment host is closing.");
            }

            var list = equipper.Equipments ?? throw new ArgumentException("Initialize IEquippable.Equipments.", nameof(equipper));
            _records.TryGetValue(equipment, out var record);
            if (record != null && (record.IsEquipping || record.IsUnequipping || record.IsReleaseRequested))
            {
                throw new InvalidOperationException("Equipment is inside a lifecycle transition.");
            }

            if (record != null && record.IsAttached || equipment.Equipper != null ||
                list.Any(value => ReferenceEquals(value, equipment)))
            {
                return EquipState.AlreadyEquipped;
            }

            if (_returnedEquipment.TryGetValue(equipment, out _))
            {
                throw new InvalidOperationException("Returned equipment references cannot be equipped.");
            }

            var isExternal = record == null;
            if (isExternal)
            {
                record = new Record { Value = equipment };
            }

            CaptureDefinition(record);
            if (IsDefinitionClosing(record))
            {
                throw new InvalidOperationException("Equipment definition is closing.");
            }

            if (isExternal)
            {
                _records.Add(equipment, record);
            }

            record.IsEquipCancelled = false;
            record.IsEquipping = true;
            record.PendingHost = equipper;
            try
            {
                var canEquip = equipper.CanEquip(equipment);
                if (IsAlive(equipment))
                {
                    CaptureDefinition(record);
                }

                if (!canEquip || record.IsEquipCancelled || record.IsReleaseRequested || _isStopping ||
                    !IsAlive(equipper) || !IsAlive(equipment) || IsDefinitionClosing(record))
                {
                    return EquipState.CannotEquip;
                }

                if (!ReferenceEquals(list, equipper.Equipments))
                {
                    throw new InvalidOperationException("The equipment host replaced its list during Equip.");
                }

                record.Host = equipper;
                record.List = list;
                record.IsAttached = true;
                record.Revision++;
                list.Add(equipment);
                equipment.Equipper = equipper;
                if (!record.IsAttached || record.IsEquipCancelled)
                {
                    return EquipState.CannotEquip;
                }

                record.IsNotified = true;
                equipment.OnEquip();
                return record.IsAttached && !record.IsReleaseRequested && !record.IsEquipCancelled
                    ? EquipState.Success
                    : EquipState.CannotEquip;
            }
            catch (Exception error)
            {
                record.ShouldDiscard = true;
                var errors = new List<Exception> { error };
                Attempt(errors, () => UnequipCore(record, false));
                ThrowErrors(errors);
                throw;
            }
            finally
            {
                record.IsEquipping = false;
                record.PendingHost = null;
                ForgetBorrowed(record);
            }
        });

        public static IEquipment CreateAndEquip(IEquippable equipper, int equipmentId) => Run(() =>
        {
            var equipment = CreateEquipment(equipmentId);
            try
            {
                if (Equip(equipper, equipment) == EquipState.Success)
                {
                    return equipment;
                }
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

        public static void Unequip(IEquipment equipment, bool destroy = true) => Run(() =>
        {
            if (equipment != null && _records.TryGetValue(equipment, out var record))
            {
                UnequipCore(record, destroy);
            }
        });

        private static void UnequipCore(Record record, bool destroy)
        {
            if (!IsCurrent(record))
            {
                return;
            }

            var errors = new List<Exception>();
            if (record.IsAttached)
            {
                record.IsAttached = false;
                record.List?.RemoveAll(value => ReferenceEquals(value, record.Value));
                var wasNotified = record.IsNotified;
                record.IsNotified = false;
                record.IsUnequipping = true;
                if (record.IsEquipping)
                {
                    record.IsEquipCancelled = true;
                }

                try
                {
                    if (wasNotified && IsAlive(record.Value))
                    {
                        Attempt(errors, record.Value.OnUnequip);
                    }
                }
                finally
                {
                    if (IsAlive(record.Value))
                    {
                        Attempt(errors, () => record.Value.Equipper = null);
                    }

                    record.Host = null;
                    record.List = null;
                    record.IsUnequipping = false;
                }
            }

            if (errors.Count != 0)
            {
                record.ShouldDiscard = true;
            }

            if (destroy && record.IsOwned && !record.IsReleaseRequested)
            {
                record.IsReleaseRequested = true;
                _pendingReturns.Enqueue(record);
            }

            ForgetBorrowed(record);
            ThrowErrors(errors);
        }

        private static void ForgetBorrowed(Record record)
        {
            if (!record.IsOwned && !record.IsAttached && !record.IsEquipping && !record.IsUnequipping && IsCurrent(record))
            {
                _records.Remove(record.Value);
            }
        }

        public static void DestroyEquipment(IEquipment equipment) => Run(() =>
        {
            if (equipment == null)
            {
                return;
            }

            if (_records.TryGetValue(equipment, out var record) && record.IsOwned)
            {
                UnequipCore(record, true);
                return;
            }

            if (_returnedEquipment.TryGetValue(equipment, out _) || !IsAlive(equipment))
            {
                return;
            }

            throw new InvalidOperationException("Cannot destroy equipment owned by an external caller.");
        });

        public static void UnequipAll(IEquippable equipper, bool destroy = true) => Run(() =>
        {
            if (equipper == null)
            {
                return;
            }

            _aggregateReturns = true;
            var errors = new List<Exception>();
            var records = _records.Values
                .Where(record => ReferenceEquals(record.Host, equipper))
                .Select(record => (Record: record, record.Revision))
                .ToArray();
            foreach (var snapshot in records)
            {
                if (IsCurrent(snapshot.Record) && snapshot.Record.Revision == snapshot.Revision &&
                    ReferenceEquals(snapshot.Record.Host, equipper))
                {
                    Attempt(errors, () => UnequipCore(snapshot.Record, destroy));
                }
            }

            if (IsAlive(equipper))
            {
                equipper.Equipments?.RemoveAll(value => !IsAlive(value));
            }

            ThrowErrors(errors, true);
        });

        public static void CloseHost(IEquippable equipper) => Run(() =>
        {
            if (equipper == null || !_closingHosts.Add(equipper))
            {
                return;
            }

            _aggregateReturns = true;
            var errors = new List<Exception>();
            foreach (var snapshot in _records.Values
                .Where(record =>
                    ReferenceEquals(record.Host, equipper) || ReferenceEquals(record.PendingHost, equipper))
                .Select(record => (Record: record, record.Revision))
                .ToArray())
            {
                var record = snapshot.Record;
                if (!IsCurrent(record) || record.Revision != snapshot.Revision ||
                    !(ReferenceEquals(record.Host, equipper) || ReferenceEquals(record.PendingHost, equipper)))
                {
                    continue;
                }

                record.IsEquipCancelled = true;
                if (record.IsAttached || record.IsUnequipping)
                {
                    Attempt(errors, () => UnequipCore(record, true));
                }
            }

            if (IsAlive(equipper))
            {
                equipper.Equipments?.RemoveAll(value => !IsAlive(value));
            }

            ThrowErrors(errors, true);
        });

        public static void Shutdown() => Run(() =>
        {
            if (_isStopping)
            {
                return;
            }

            _isStopping = true;
            _aggregateReturns = true;
            foreach (var config in _configs.Values)
            {
                config.IsClosing = true;
                _retiredConfigs.Add(config);
            }

            _configs.Clear();
            var errors = new List<Exception>();
            foreach (var record in _records.Values.ToArray())
            {
                record.IsEquipCancelled = true;
                Attempt(errors, () => UnequipCore(record, true));
            }

            foreach (var pool in _pools.Values)
            {
                Attempt(errors, pool.Dispose);
            }

            _pools.Clear();
            ThrowErrors(errors, true);
        });

        // 所有外层回调退出后才真正归还，防止旧回调继续访问已被下一次创建复用的实例。
        private static void Flush()
        {
            _isFlushing = true;
            _depth++;
            var errors = new List<Exception>();
            var aggregate = false;
            try
            {
                while (_pendingReturns.Count != 0 || _retiredConfigs.Count != 0)
                {
                    while (_pendingReturns.Count != 0)
                    {
                        var record = _pendingReturns.Dequeue();
                        if (record.IsReturning)
                        {
                            continue;
                        }

                        record.IsReturning = true;
                        var cleanup = new List<Exception>();
                        if (record.IsLoaded && IsAlive(record.Value))
                        {
                            Attempt(cleanup, record.Value.OnUnloaded);
                        }

                        if (IsAlive(record.Value))
                        {
                            Attempt(cleanup, () => record.Value.Equipper = null);
                            Attempt(cleanup, () => record.Value.EquipmentData = null);
                        }

                        _records.Remove(record.Value);
                        _returnedEquipment.Remove(record.Value);
                        _returnedEquipment.Add(record.Value, _returnedMarker);
                        var shouldDiscard = record.ShouldDiscard || cleanup.Count != 0;
                        if (record.Lease != null)
                        {
                            Attempt(cleanup, () =>
                            {
                                if (shouldDiscard)
                                {
                                    record.Lease.Discard();
                                }
                                else
                                {
                                    record.Lease.Return();
                                }
                            });
                        }

                        if (record.Pool != null)
                        {
                            Attempt(cleanup, () =>
                            {
                                if (shouldDiscard)
                                {
                                    record.Pool.Discard(record.Value);
                                }
                                else
                                {
                                    record.Pool.Recycle(record.Value);
                                }
                            });
                        }

                        errors.AddRange(cleanup);
                    }

                    if (_retiredConfigs.Count != 0)
                    {
                        var config = _retiredConfigs[0];
                        try
                        {
                            if (config.Lease != null)
                            {
                                Attempt(errors, config.Lease.Dispose);
                            }
                        }
                        finally
                        {
                            config.Lease = null;
                            _retiredConfigs.Remove(config);
                        }
                    }
                }
            }
            finally
            {
                aggregate = _aggregateReturns;
                _aggregateReturns = false;
                _depth--;
                _isFlushing = false;
                _isStopping = false;
                _closingHosts.Clear();
            }

            ThrowErrors(errors, aggregate);
        }
    }
}
