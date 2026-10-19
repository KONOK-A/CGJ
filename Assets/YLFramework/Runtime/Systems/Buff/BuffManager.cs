// Copyright (c) 2024 弦风课堂. MIT License, see LICENSE.txt.
using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;
using YLFramework.Events;
using YLFramework.Foundation;
using YLFramework.Services.Resource;

namespace YLFramework.BuffSystem
{
    /// <summary>保留Single/Additive、时长与效果查询。宿主拥有Buff集合，退出显式CloseHost。</summary>
    public static class BuffManager
    {
        private static readonly List<BuffsConfig> Configs = new();
        private static readonly Dictionary<BuffsConfig, IDisposable> Leases = new();
        private static readonly List<IAddableBuff> Hosts = new();
        private static readonly Dictionary<IAddableBuff, HashSet<int>> Effects = new();
        private static readonly HashSet<IAddableBuff> Closing = new();
        private static readonly HashSet<(IAddableBuff, int)> Adding = new();
        private static readonly Dictionary<Type, ObjectPool<BuffBase>> Pools = new();
        private static readonly List<(BuffBase buff, ObjectPool<BuffBase> pool)> Pending = new();
        private static IUnRegister _update, _fixed, _late;
        private static int _depth;
        private static bool _stopping;
        public static int ConfigCount => Configs.Count;
        public static int HostCount => Hosts.Count;
        public static int EffectOwnerCount => Effects.Count;

        static BuffManager()
        {
            SingletonSessionState.RegisterSessionReset(Shutdown);
            Application.quitting += Shutdown;
        }

        public static void AddConfig(BuffsConfig config)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            if (_stopping) throw new InvalidOperationException("Buff会话正在关闭。");
            if (Configs.Contains(config)) return;
            var ids = new HashSet<int>();
            foreach (var data in config.buffs)
            {
                if (!data) throw new InvalidOperationException($"Buff配表存在空项: {config.name}");
                if (!ids.Add(data.buff_id) || GetBuffData(data.buff_id) != null) throw new InvalidOperationException($"Buff ID重复: {data.buff_id}, 配表={config.name}");
                ResolveType(data.buff_class_full_name, data.buff_id, config.name);
                for (var i = 0; i < data.effects.Count; i++)
                    if (!data.effects[i]) throw new InvalidOperationException($"Buff效果缺失: ID={data.buff_id}, 配表={config.name}, effect[{i}]");
            }
            Configs.Add(config);
        }

        public static void LoadConfig(ResourceSystem resources, ResourceAddress address)
        {
            var lease = resources.Acquire<BuffsConfig>(address);
            try { AddConfig(lease.Asset); if (Leases.ContainsKey(lease.Asset)) lease.Dispose(); else Leases.Add(lease.Asset, lease); }
            catch { lease.Dispose(); throw; }
        }
        public static BuffsConfig GetConfig(string name) => Configs.Find(config => config && config.name == name);
        public static void RemoveConfig(BuffsConfig config)
        {
            if (!Configs.Remove(config)) return;
            foreach (var host in Hosts.ToArray())
                foreach (var buff in host.Buffs.ToArray()) if (buff.BuffData && buff.BuffData.parent == config) RemoveBuff(host, buff);
            if (Leases.TryGetValue(config, out var lease)) { Leases.Remove(config); lease.Dispose(); }
        }
        private static BuffConfigData GetBuffData(int id)
        {
            foreach (var config in Configs) foreach (var data in config.buffs) if (data && data.buff_id == id) return data;
            return null;
        }
        private static Type ResolveType(string name, int id, string config)
        {
            var type = AssemblyTool.GetType(name);
            if (type == null || type.IsAbstract || !typeof(BuffBase).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"Buff类型无效: ID={id}, 配表={config}, class={name}");
            return type;
        }

        public static BuffBase AddBuff(IAddableBuff host, int id, object userData = null)
        {
            var data = GetBuffData(id);
            if (!data) throw new InvalidOperationException($"Buff配置不存在: {id}");
            return AddBuff(host, id, data.duration, userData);
        }
        public static BuffBase AddBuff(IAddableBuff host, int id, float duration, object userData = null)
        {
            if (host == null) return null;
            if (host.Buffs == null) throw new ArgumentException("请初始化IAddableBuff.Buffs。", nameof(host));
            if (_stopping || Closing.Contains(host)) throw new InvalidOperationException("Buff宿主正在退出。");
            if (host is UnityEngine.Object obj && !obj || host is Component component && !component.gameObject.activeInHierarchy) return null;
            if (!host.IsCanAdd(id)) return null;
            if (!Adding.Add((host, id))) throw new InvalidOperationException($"Buff同宿主同ID重入添加: {id}");
            _depth++;
            BuffBase buff = null;
            ObjectPool<BuffBase> pool = null;
            try
            {
                var definition = GetBuffData(id);
                if (!definition) throw new InvalidOperationException($"Buff配置不存在: {id}");
                var type = ResolveType(definition.buff_class_full_name, id, definition.parent ? definition.parent.name : "(unparented)");
                if (!Pools.TryGetValue(type, out pool))
                {
                    pool = new ObjectPool<BuffBase>(() => (BuffBase)Activator.CreateInstance(type), reset: value => value.ClearRuntime());
                    Pools.Add(type, pool);
                }
                buff = pool.Allocate();
                buff.BuffData = UnityEngine.Object.Instantiate(definition);
                foreach (var config in Configs) if (config.buffs.Contains(definition)) { buff.BuffData.parent = config; break; }
                buff.BuffData.effects = new List<EffectData>();
                foreach (var effect in definition.effects) buff.BuffData.effects.Add(UnityEngine.Object.Instantiate(effect));
                buff.ResetData();
                buff.Owner = host;
                buff.Duration = duration;
                buff.userData = userData;
                if (!buff.IsCanAdd()) { Pending.Add((buff, pool)); return null; }
                if (definition.mode == BuffMode.Single)
                    foreach (var old in host.Buffs.ToArray()) if (old.BuffData.buff_id == id) RemoveBuff(host, old);
                if (_stopping || Closing.Contains(host)) { Pending.Add((buff, pool)); return null; }
                host.Buffs.Add(buff);
                if (!Hosts.Contains(host)) Hosts.Add(host);
                UpdateEffects(host);
                EnsureUpdates();
                var version = buff.Version;
                buff.OnAdd();
                if (version == buff.Version && host.Buffs.Contains(buff)) host.OnAddBuff(buff);
                return version == buff.Version && host.Buffs.Contains(buff) ? buff : null;
            }
            catch
            {
                if (buff != null && !Pending.Exists(entry => ReferenceEquals(entry.buff, buff)))
                {
                    host.Buffs.Remove(buff);
                    UpdateEffects(host);
                    Pending.Add((buff, pool));
                }
                throw;
            }
            finally { Adding.Remove((host, id)); if (--_depth == 0) FlushReturns(); }
        }
        public static void AddBuff(IAddableBuff host, IList<int> ids, object userData = null)
        { foreach (var id in new List<int>(ids)) AddBuff(host, id, userData); }
        public static void AddBuff(IAddableBuff host, IList<int> ids, float duration, object userData = null)
        { foreach (var id in new List<int>(ids)) AddBuff(host, id, duration, userData); }

        public static void RemoveBuff(IAddableBuff host, BuffBase buff)
        {
            if (host?.Buffs == null || buff == null || !ReferenceEquals(buff.Owner, host) || !host.Buffs.Remove(buff)) return;
            buff.Detach();
            UpdateEffects(host);
            _depth++;
            try { try { buff.OnRemove(); } finally { host.OnRemoveBuff(buff); } }
            finally { Pending.Add((buff, Pools[buff.GetType()])); if (--_depth == 0) FlushReturns(); }
        }
        public static void RemoveBuff(IAddableBuff host, IList<BuffBase> buffs)
        { foreach (var buff in new List<BuffBase>(buffs)) RemoveBuff(host, buff); }
        public static void RemoveBuff(IAddableBuff host, int id)
        { if (host?.Buffs != null) foreach (var buff in host.Buffs.ToArray()) if (buff.BuffData.buff_id == id) RemoveBuff(host, buff); }
        public static void RemoveBuff(IAddableBuff host, Type type)
        { if (host?.Buffs != null) foreach (var buff in host.Buffs.ToArray()) if (buff.GetType() == type) RemoveBuff(host, buff); }
        public static void RemoveBuff<T>(IAddableBuff host) where T : BuffBase
        { if (host?.Buffs != null) foreach (var buff in host.Buffs.ToArray()) if (buff is T) RemoveBuff(host, buff); }
        public static void RemoveBuff(IAddableBuff host) => CloseHost(host);
        public static void CloseHost(IAddableBuff host)
        {
            if (host == null || !Closing.Add(host)) return;
            var errors = new List<Exception>();
            try { if (host.Buffs != null) foreach (var buff in host.Buffs.ToArray()) try { RemoveBuff(host, buff); } catch (Exception error) { errors.Add(error); } }
            finally { Effects.Remove(host); Hosts.Remove(host); Closing.Remove(host); ReleaseIdleUpdates(); }
            if (errors.Count != 0) throw new AggregateException(errors);
        }

        public static bool IsContainEffect(IAddableBuff host, int id) => host != null && Effects.TryGetValue(host, out var effects) && effects.Contains(id);
        public static void GetEffects<T>(IAddableBuff host, List<T> results) where T : EffectData => QueryEffects(host, results, null, null);
        public static void GetEffects<T>(IAddableBuff host, List<T> results, int id) where T : EffectData => QueryEffects(host, results, id, null);
        public static void GetEffectsByType<T>(IAddableBuff host, List<T> results, int type) where T : EffectData => QueryEffects(host, results, null, type);
        private static void QueryEffects<T>(IAddableBuff host, List<T> results, int? id, int? type) where T : EffectData
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (host?.Buffs == null) return;
            foreach (var buff in host.Buffs)
                foreach (var effect in buff.BuffData.effects)
                    if (effect is T value && (!id.HasValue || effect.id == id) && (!type.HasValue || effect.type == type)) results.Add(value);
        }
        private static void UpdateEffects(IAddableBuff host)
        {
            if (host.Buffs.Count == 0) { Effects.Remove(host); Hosts.Remove(host); return; }
            if (!Effects.TryGetValue(host, out var effects)) { effects = new HashSet<int>(); Effects.Add(host, effects); }
            effects.Clear();
            foreach (var buff in host.Buffs) foreach (var effect in buff.BuffData.effects) effects.Add(effect.id);
        }
        private static void EnsureUpdates()
        {
            if (_update != null) return;
            _update = UpdateManager.AddUpdateEvent(() => Dispatch(0));
            _fixed = UpdateManager.AddFixedUpdateEvent(() => Dispatch(1));
            _late = UpdateManager.AddLateUpdateEvent(() => Dispatch(2));
        }
        private static void Dispatch(int phase)
        {
            if (_stopping) return;
            _depth++;
            try
            {
                foreach (var host in Hosts.ToArray())
                {
                    if (host is UnityEngine.Object obj && !obj) { CloseHost(host); continue; }
                    if (host is Component component && !component.gameObject.activeInHierarchy) continue;
                    var snapshot = new List<(BuffBase buff, int version)>();
                    foreach (var buff in host.Buffs) snapshot.Add((buff, buff.Version));
                    foreach (var entry in snapshot)
                    {
                        var buff = entry.buff;
                        if (_stopping || entry.version != buff.Version || !host.Buffs.Contains(buff)) continue;
                        if (phase == 0)
                        {
                            buff.Update();
                            if (entry.version == buff.Version && buff.Progress >= 1) RemoveBuff(host, buff);
                        }
                        else if (phase == 1) buff.FixedUpdate();
                        else buff.OnLateUpdate();
                    }
                }
            }
            finally { if (--_depth == 0) FlushReturns(); }
        }
        private static void FlushReturns()
        {
            foreach (var entry in Pending)
            {
                var data = entry.buff.BuffData;
                entry.pool.Recycle(entry.buff);
                if (data)
                {
                    foreach (var effect in data.effects) if (effect) UnityEngine.Object.Destroy(effect);
                    UnityEngine.Object.Destroy(data);
                }
            }
            Pending.Clear();
            ReleaseIdleUpdates();
            if (_stopping)
            {
                foreach (var pool in Pools.Values) pool.Dispose();
                Pools.Clear();
                foreach (var lease in Leases.Values) lease.Dispose();
                Leases.Clear(); Configs.Clear(); Effects.Clear();
                _stopping = false;
            }
        }
        private static void ReleaseIdleUpdates()
        {
            if (Hosts.Count != 0) return;
            _update?.UnRegister(); _fixed?.UnRegister(); _late?.UnRegister();
            _update = _fixed = _late = null;
        }
        public static void Shutdown()
        {
            if (_stopping) return;
            _stopping = true;
            _depth++;
            var errors = new List<Exception>();
            try { foreach (var host in Hosts.ToArray()) try { CloseHost(host); } catch (Exception error) { errors.Add(error); } }
            finally { if (--_depth == 0) FlushReturns(); }
            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }
}
