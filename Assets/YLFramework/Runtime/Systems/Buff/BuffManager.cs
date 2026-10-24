using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Pooling;
using YLFramework.Singletons;
using YLFramework.Events;
using YLFramework.Foundation;
using YLFramework.Resource;
using YLFramework.Update;

namespace YLFramework.Buff
{
    /// <summary>保留Single/Additive、时长与效果查询。宿主拥有Buff集合，退出显式CloseHost。</summary>
    public static class BuffManager
    {
        private static readonly List<BuffsConfig> _configs = new();
        private static readonly Dictionary<BuffsConfig, IDisposable> _leases = new();
        private static readonly List<IAddableBuff> _hosts = new();
        private static readonly Dictionary<IAddableBuff, HashSet<int>> _effects = new();
        private static readonly HashSet<IAddableBuff> _closing = new();
        private static readonly HashSet<(IAddableBuff, int)> _adding = new();
        private static readonly HashSet<BuffsConfig> _removingConfigs = new();
        private static readonly Dictionary<IAddableBuff, int> _addingDepth = new();
        private static readonly HashSet<IAddableBuff> _cancelledAdds = new();
        private static readonly Dictionary<Type, ObjectPool<BuffBase>> _pools = new();
        private static readonly List<(BuffBase buff, ObjectPool<BuffBase> pool)> _pending = new();
        private static IUnregister _update;
        private static IUnregister _fixed;
        private static IUnregister _late;
        private static int _depth;
        private static bool _isStopping;
        public static int ConfigCount => _configs.Count;
        public static int HostCount => _hosts.Count;
        public static int EffectOwnerCount => _effects.Count;

        static BuffManager()
        {
            SingletonSessionState.RegisterSessionReset(Shutdown);
            Application.quitting += Shutdown;
        }

        public static void AddConfig(BuffsConfig config)
        {
            if (!config)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (_isStopping || _removingConfigs.Contains(config))
            {
                throw new InvalidOperationException("Buff配表正在关闭。");
            }

            if (_configs.Contains(config))
            {
                return;
            }

            var ids = new HashSet<int>();
            foreach (var data in config.buffs)
            {
                if (!data)
                {
                    throw new InvalidOperationException($"Buff配表存在空项: {config.name}");
                }

                if (!ids.Add(data.id) || GetBuffData(data.id) != null)
                {
                    throw new InvalidOperationException($"Buff ID重复: {data.id}, 配表={config.name}");
                }

                ResolveType(data.buffClassFullName, data.id, config.name);
                for (var i = 0; i < data.effects.Count; i++)
                {
                    if (!data.effects[i])
                    {
                        throw new InvalidOperationException($"Buff效果缺失: ID={data.id}, 配表={config.name}, effect[{i}]");
                    }
                }
            }
            _configs.Add(config);
        }

        public static void LoadConfig(ResourceSystem resources, ResourceAddress address)
        {
            var lease = resources.Acquire<BuffsConfig>(address);
            try
            {
                AddConfig(lease.Asset);
                if (_leases.ContainsKey(lease.Asset))
                {
                    lease.Dispose();
                }
                else
                {
                    _leases.Add(lease.Asset, lease);
                }
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
        public static BuffsConfig GetConfig(string name) => _configs.Find(config => config && config.name == name);
        public static void RemoveConfig(BuffsConfig config)
        {
            if (!config || !_configs.Contains(config) && !_leases.ContainsKey(config) || !_removingConfigs.Add(config))
            {
                return;
            }

            _configs.Remove(config);
            var errors = new List<Exception>();
            _depth++;
            try
            {
                foreach (var host in _hosts.ToArray())
                {
                    try
                    {
                        RemoveMatching(host, buff => buff.BuffData && buff.BuffData.parent == config);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }
            finally
            {
                try
                {
                    if (--_depth == 0)
                    {
                        FlushReturns();
                    }
                }
                finally
                {
                    if (_leases.TryGetValue(config, out var lease))
                    {
                        _leases.Remove(config);
                        try
                        {
                            lease.Dispose();
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                        }
                    }
                    _removingConfigs.Remove(config);
                }
            }
            if (errors.Count != 0)
            {
                throw new AggregateException(errors);
            }
        }
        private static BuffConfigData GetBuffData(int id)
        {
            foreach (var config in _configs)
            {
                foreach (var data in config.buffs)
                {
                    if (data && data.id == id)
                    {
                        return data;
                    }
                }
            }

            return null;
        }
        private static Type ResolveType(string name, int id, string config)
        {
            var type = AssemblyTool.GetType(name);
            if (type == null || type.IsAbstract || !typeof(BuffBase).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException($"Buff类型无效: ID={id}, 配表={config}, class={name}");
            }

            return type;
        }

        public static BuffBase AddBuff(IAddableBuff host, int id, object userData = null)
        {
            var data = GetBuffData(id);
            if (!data)
            {
                throw new InvalidOperationException($"Buff配置不存在: {id}");
            }

            return AddBuff(host, id, data.duration, userData);
        }
        public static BuffBase AddBuff(IAddableBuff host, int id, float duration, object userData = null)
        {
            if (host == null)
            {
                return null;
            }

            if (host.Buffs == null)
            {
                throw new ArgumentException("请初始化IAddableBuff.Buffs。", nameof(host));
            }

            if (_isStopping || _closing.Contains(host) || _cancelledAdds.Contains(host))
            {
                throw new InvalidOperationException("Buff宿主正在退出。");
            }

            if (host is UnityEngine.Object obj && !obj || host is Component component && !component.gameObject.activeInHierarchy)
            {
                return null;
            }

            if (!_adding.Add((host, id)))
            {
                throw new InvalidOperationException($"Buff同宿主同ID重入添加: {id}");
            }

            BeginAdd(host);
            _depth++;
            BuffBase buff = null;
            ObjectPool<BuffBase> pool = null;
            try
            {
                if (!host.CanAdd(id) || _cancelledAdds.Contains(host))
                {
                    return null;
                }

                var definition = GetBuffData(id);
                if (!definition)
                {
                    throw new InvalidOperationException($"Buff配置不存在: {id}");
                }

                var owner = _configs.Find(config => config.buffs.Contains(definition));
                var type = ResolveType(definition.buffClassFullName, id, definition.parent ? definition.parent.name : "(unparented)");
                if (!_pools.TryGetValue(type, out pool))
                {
                    pool = new ObjectPool<BuffBase>(() => (BuffBase)Activator.CreateInstance(type), reset: value => value.ClearRuntime());
                    _pools.Add(type, pool);
                }
                buff = pool.Allocate();
                if (_isStopping || _cancelledAdds.Contains(host) || !_configs.Contains(owner))
                {
                    _pending.Add((buff, pool));
                    return null;
                }
                buff.BuffData = UnityEngine.Object.Instantiate(definition);
                buff.BuffData.parent = owner;
                buff.BuffData.effects = new List<EffectData>();
                foreach (var effect in definition.effects)
                {
                    buff.BuffData.effects.Add(UnityEngine.Object.Instantiate(effect));
                }

                buff.ResetData();
                buff.Owner = host;
                buff.Duration = duration;
                buff.UserData = userData;
                if (_isStopping || _cancelledAdds.Contains(host) || !_configs.Contains(owner))
                {
                    _pending.Add((buff, pool));
                    return null;
                }
                if (!buff.CanAdd())
                {
                    _pending.Add((buff, pool));
                    return null;
                }
                if (definition.mode == BuffMode.Single)
                {
                    RemoveBuff(host, id);
                }

                if (_isStopping || _closing.Contains(host) || _cancelledAdds.Contains(host) || !_configs.Contains(owner))
                {
                    _pending.Add((buff, pool));
                    return null;
                }
                host.Buffs.Add(buff);
                if (!_hosts.Contains(host))
                {
                    _hosts.Add(host);
                }

                UpdateEffects(host);
                EnsureUpdates();
                var version = buff.Version;
                buff.OnAdd();
                if (version == buff.Version && host.Buffs.Contains(buff))
                {
                    host.OnAddBuff(buff);
                }

                return version == buff.Version && host.Buffs.Contains(buff) ? buff : null;
            }
            catch
            {
                if (buff != null && !_pending.Exists(entry => ReferenceEquals(entry.buff, buff)))
                {
                    host.Buffs.Remove(buff);
                    UpdateEffects(host);
                    _pending.Add((buff, pool));
                }
                throw;
            }
            finally
            {
                _adding.Remove((host, id));
                EndAdd(host);
                if (--_depth == 0)
                {
                    FlushReturns();
                }
            }
        }
        public static void AddBuff(IAddableBuff host, IList<int> ids, object userData = null)
        {
            AddBatch(host, ids, id => AddBuff(host, id, userData));
        }
        public static void AddBuff(IAddableBuff host, IList<int> ids, float duration, object userData = null)
        {
            AddBatch(host, ids, id => AddBuff(host, id, duration, userData));
        }

        private static void BeginAdd(IAddableBuff host)
        {
            _addingDepth.TryGetValue(host, out var depth);
            _addingDepth[host] = depth + 1;
        }
        private static void EndAdd(IAddableBuff host)
        {
            if (_addingDepth[host] == 1)
            {
                _addingDepth.Remove(host);
                _cancelledAdds.Remove(host);
            }
            else
            {
                _addingDepth[host]--;
            }
        }
        private static void AddBatch(IAddableBuff host, IList<int> ids, Action<int> add)
        {
            if (host == null)
            {
                return;
            }

            BeginAdd(host);
            _depth++;
            try
            {
                foreach (var id in new List<int>(ids))
                {
                    if (_cancelledAdds.Contains(host))
                    {
                        break;
                    }
                    add(id);
                }
            }
            finally
            {
                EndAdd(host);
                if (--_depth == 0)
                {
                    FlushReturns();
                }
            }
        }

        public static void RemoveBuff(IAddableBuff host, BuffBase buff)
        {
            if (host?.Buffs == null || buff == null || !ReferenceEquals(buff.Owner, host) || !host.Buffs.Remove(buff))
            {
                return;
            }

            buff.Detach();
            UpdateEffects(host);
            _depth++;
            try
            {
                try
                {
                    buff.OnRemove();
                }
                finally
                {
                    host.OnRemoveBuff(buff);
                }
            }
            finally
            {
                _pending.Add((buff, _pools[buff.GetType()]));
                if (--_depth == 0)
                {
                    FlushReturns();
                }
            }
        }
        public static void RemoveBuff(IAddableBuff host, IList<BuffBase> buffs)
        {
            var selected = new List<BuffBase>(buffs);
            RemoveMatching(host, buff => selected.Exists(value => ReferenceEquals(value, buff)));
        }
        public static void RemoveBuff(IAddableBuff host, int id)
        {
            RemoveMatching(host, buff => buff.BuffData && buff.BuffData.id == id);
        }
        public static void RemoveBuff(IAddableBuff host, Type type)
        {
            RemoveMatching(host, buff => buff.GetType() == type);
        }
        public static void RemoveBuff<T>(IAddableBuff host) where T : BuffBase
        {
            RemoveMatching(host, buff => buff is T);
        }

        private static void RemoveMatching(IAddableBuff host, Predicate<BuffBase> predicate)
        {
            if (host?.Buffs == null)
            {
                return;
            }

            var snapshot = new List<(BuffBase buff, int version)>();
            foreach (var buff in host.Buffs)
            {
                snapshot.Add((buff, buff.Version));
            }

            var errors = new List<Exception>();
            _depth++;
            try
            {
                foreach (var entry in snapshot)
                {
                    if (entry.version != entry.buff.Version || !ReferenceEquals(entry.buff.Owner, host) || !host.Buffs.Contains(entry.buff))
                    {
                        continue;
                    }

                    try
                    {
                        if (predicate(entry.buff))
                        {
                            RemoveBuff(host, entry.buff);
                        }
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }
            finally
            {
                if (--_depth == 0)
                {
                    FlushReturns();
                }
            }
            if (errors.Count != 0)
            {
                throw new AggregateException(errors);
            }
        }
        public static void CloseHost(IAddableBuff host)
        {
            if (host == null || !_closing.Add(host))
            {
                return;
            }

            if (_addingDepth.ContainsKey(host))
            {
                _cancelledAdds.Add(host);
            }

            try
            {
                RemoveMatching(host, _ => true);
            }
            finally
            {
                _effects.Remove(host);
                _hosts.Remove(host);
                _closing.Remove(host);
                ReleaseIdleUpdates();
            }
        }

        public static bool ContainsEffect(IAddableBuff host, int id) => host != null && _effects.TryGetValue(host, out var effects) && effects.Contains(id);
        public static void GetEffects<T>(IAddableBuff host, List<T> results) where T : EffectData => QueryEffects(host, results, null, null);
        public static void GetEffects<T>(IAddableBuff host, List<T> results, int id) where T : EffectData => QueryEffects(host, results, id, null);
        public static void GetEffectsByType<T>(IAddableBuff host, List<T> results, int type) where T : EffectData => QueryEffects(host, results, null, type);
        private static void QueryEffects<T>(IAddableBuff host, List<T> results, int? id, int? type) where T : EffectData
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (host?.Buffs == null)
            {
                return;
            }

            foreach (var buff in host.Buffs)
            {
                foreach (var effect in buff.BuffData.effects)
                {
                    if (effect is T value && (!id.HasValue || effect.id == id) && (!type.HasValue || effect.type == type))
                    {
                        results.Add(value);
                    }
                }
            }
        }
        private static void UpdateEffects(IAddableBuff host)
        {
            if (host.Buffs.Count == 0)
            {
                _effects.Remove(host);
                _hosts.Remove(host);
                return;
            }
            if (!_effects.TryGetValue(host, out var effects))
            {
                effects = new HashSet<int>();
                _effects.Add(host, effects);
            }
            effects.Clear();
            foreach (var buff in host.Buffs)
            {
                foreach (var effect in buff.BuffData.effects)
                {
                    effects.Add(effect.id);
                }
            }
        }
        private static void EnsureUpdates()
        {
            if (_update != null)
            {
                return;
            }

            _update = UpdateManager.AddUpdateEvent(() => Dispatch(0));
            _fixed = UpdateManager.AddFixedUpdateEvent(() => Dispatch(1));
            _late = UpdateManager.AddLateUpdateEvent(() => Dispatch(2));
        }
        private static void Dispatch(int phase)
        {
            if (_isStopping)
            {
                return;
            }

            _depth++;
            try
            {
                foreach (var host in _hosts.ToArray())
                {
                    if (host is UnityEngine.Object obj && !obj)
                    {
                        CloseHost(host);
                        continue;
                    }
                    if (host is Component component && !component.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var snapshot = new List<(BuffBase buff, int version)>();
                    foreach (var buff in host.Buffs)
                    {
                        snapshot.Add((buff, buff.Version));
                    }

                    foreach (var entry in snapshot)
                    {
                        var buff = entry.buff;
                        if (_isStopping || entry.version != buff.Version || !host.Buffs.Contains(buff))
                        {
                            continue;
                        }

                        if (phase == 0)
                        {
                            buff.Update();
                            if (entry.version == buff.Version && buff.Progress >= 1)
                            {
                                RemoveBuff(host, buff);
                            }
                        }
                        else if (phase == 1)
                        {
                            buff.FixedUpdate();
                        }
                        else
                        {
                            buff.OnLateUpdate();
                        }
                    }
                }
            }
            finally
            {
                if (--_depth == 0)
                {
                    FlushReturns();
                }
            }
        }
        private static void FlushReturns()
        {
            foreach (var entry in _pending)
            {
                var data = entry.buff.BuffData;
                entry.pool.Recycle(entry.buff);
                if (data)
                {
                    foreach (var effect in data.effects)
                    {
                        if (effect)
                        {
                            UnityEngine.Object.Destroy(effect);
                        }
                    }

                    UnityEngine.Object.Destroy(data);
                }
            }
            _pending.Clear();
            ReleaseIdleUpdates();
            if (_isStopping)
            {
                foreach (var pool in _pools.Values)
                {
                    pool.Dispose();
                }

                _pools.Clear();
                foreach (var lease in _leases.Values)
                {
                    lease.Dispose();
                }

                _leases.Clear();
                _configs.Clear();
                _effects.Clear();
                _isStopping = false;
            }
        }
        private static void ReleaseIdleUpdates()
        {
            if (_hosts.Count != 0)
            {
                return;
            }

            _update?.Unregister();
            _fixed?.Unregister();
            _late?.Unregister();
            _update = _fixed = _late = null;
        }
        public static void Shutdown()
        {
            if (_isStopping)
            {
                return;
            }

            _isStopping = true;
            _depth++;
            var errors = new List<Exception>();
            try
            {
                foreach (var host in _hosts.ToArray())
                {
                    try
                    {
                        CloseHost(host);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }
            finally
            {
                if (--_depth == 0)
                {
                    FlushReturns();
                }
            }
            if (errors.Count != 0)
            {
                throw new AggregateException(errors);
            }
        }
    }
}
