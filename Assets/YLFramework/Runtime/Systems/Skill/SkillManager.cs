using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;
using YLFramework.Events;
using YLFramework.Foundation;
using YLFramework.Services.Resource;

namespace YLFramework.SkillSystem
{
    /// <summary>保留技能装备/释放流程。CLR宿主退出显式CloseHost，整个会话由Shutdown归还。</summary>
    public static class SkillManager
    {
        private static readonly Dictionary<string, SkillsConfig> Configs = new(StringComparer.Ordinal);
        private static readonly Dictionary<SkillsConfig, IDisposable> Leases = new();
        private static readonly List<IEquippableSkill> Hosts = new();
        private static readonly HashSet<IEquippableSkill> Closing = new();
        private static readonly HashSet<SkillsConfig> RemovingConfigs = new();
        private static readonly Dictionary<IEquippableSkill, int> AddingDepth = new();
        private static readonly HashSet<IEquippableSkill> CancelledAdds = new();
        private static readonly Dictionary<Type, ObjectPool<SkillBase>> Pools = new();
        private static readonly List<(SkillBase skill, ObjectPool<SkillBase> pool)> Pending = new();
        private static IUnRegister _update, _fixed, _late;
        private static int _depth;
        private static bool _stopping;
        public static int ConfigCount => Configs.Count;
        public static int HostCount => Hosts.Count;

        static SkillManager()
        {
            SingletonSessionState.RegisterSessionReset(Shutdown);
            Application.quitting += Shutdown;
        }

        public static void AddConfig(SkillsConfig config)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            if (_stopping || RemovingConfigs.Contains(config)) throw new InvalidOperationException("技能配表正在关闭。");
            if (Configs.TryGetValue(config.name, out var existing))
            {
                if (existing == config) return;
                throw new InvalidOperationException($"技能配表名称重复: {config.name}");
            }
            var ids = new HashSet<int>();
            foreach (var data in config.skills)
            {
                if (!data) throw new InvalidOperationException($"技能配表 {config.name} 存在空条目。");
                if (!ids.Add(data.id) || GetSkillInfo(data.id) != null) throw new InvalidOperationException($"技能ID重复: {data.id}, 配表: {config.name}");
                ResolveType(data.skill_class_full_name, data.id, config.name);
            }
            Configs.Add(config.name, config);
        }

        /// <summary>Acquire的根Lease由Manager接管，直到RemoveConfig或Shutdown。</summary>
        public static void LoadConfig(ResourceSystem resources, ResourceAddress address)
        {
            var lease = resources.Acquire<SkillsConfig>(address);
            try
            {
                AddConfig(lease.Asset);
                if (Leases.ContainsKey(lease.Asset)) lease.Dispose();
                else Leases.Add(lease.Asset, lease);
            }
            catch { lease.Dispose(); throw; }
        }

        public static SkillsConfig GetConfig(string name) => name != null && Configs.TryGetValue(name, out var config) ? config : null;
        public static void RemoveConfig(SkillsConfig config)
        {
            if (!config) return;
            string key = null;
            foreach (var pair in Configs) if (ReferenceEquals(pair.Value, config)) { key = pair.Key; break; }
            if (key == null && !Leases.ContainsKey(config) || !RemovingConfigs.Add(config)) return;
            if (key != null) Configs.Remove(key);
            var errors = new List<Exception>();
            _depth++;
            try
            {
                foreach (var host in Hosts.ToArray())
                    try { ForEachOwnedSkill(host, (id, skill) => { if (skill.SkillData && skill.SkillData.parent == config) RemoveSkills(host, id); }); }
                    catch (Exception error) { errors.Add(error); }
            }
            finally
            {
                try { if (--_depth == 0) FlushReturns(); }
                finally
                {
                    if (Leases.TryGetValue(config, out var lease))
                    {
                        Leases.Remove(config);
                        try { lease.Dispose(); } catch (Exception error) { errors.Add(error); }
                    }
                    RemovingConfigs.Remove(config);
                }
            }
            if (errors.Count != 0) throw new AggregateException(errors);
        }

        private static SkillConfigData GetSkillInfo(int id)
        {
            foreach (var config in Configs.Values)
                foreach (var data in config.skills) if (data && data.id == id) return data;
            return null;
        }

        private static Type ResolveType(string name, int id, string config)
        {
            var type = AssemblyTool.GetType(name);
            if (type == null || type.IsAbstract || !typeof(SkillBase).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"技能类型无效: ID={id}, 配表={config}, class={name}");
            return type;
        }

        public static SkillBase AddSkills(IEquippableSkill host, int skillId)
        {
            if (host == null || host.Skills == null) throw new ArgumentException("请初始化IEquippableSkill.Skills。", nameof(host));
            if (_stopping || Closing.Contains(host) || CancelledAdds.Contains(host)) throw new InvalidOperationException("技能宿主正在退出。");
            if (host.Skills.ContainsKey(skillId)) throw new InvalidOperationException($"技能已装备: {skillId}");
            var definition = GetSkillInfo(skillId);
            if (!definition) throw new InvalidOperationException($"技能配置不存在: {skillId}");
            SkillsConfig owner = null;
            foreach (var config in Configs.Values) if (config.skills.Contains(definition)) { owner = config; break; }
            var type = ResolveType(definition.skill_class_full_name, skillId, definition.parent ? definition.parent.name : "(unparented)");
            if (!Pools.TryGetValue(type, out var pool))
            {
                pool = new ObjectPool<SkillBase>(() => (SkillBase)Activator.CreateInstance(type), reset: skill => skill.ClearRuntime());
                Pools.Add(type, pool);
            }
            BeginAdd(host);
            SkillBase skill = null;
            _depth++;
            try
            {
                skill = pool.Allocate();
                if (CancelledAdds.Contains(host) || !Configs.ContainsValue(owner)) { Pending.Add((skill, pool)); return null; }
                skill.SkillData = UnityEngine.Object.Instantiate(definition);
                skill.SkillData.parent = owner;
                skill.Releaser = host;
                skill.ResetData();
                if (_stopping || CancelledAdds.Contains(host) || !Configs.ContainsValue(owner)) { Pending.Add((skill, pool)); return null; }
                host.Skills.Add(skillId, skill);
                if (!Hosts.Contains(host)) Hosts.Add(host);
                EnsureUpdates();
                skill.OnAdd();
                return host.Skills.TryGetValue(skillId, out var current) && ReferenceEquals(current, skill) ? skill : null;
            }
            catch
            {
                if (skill != null)
                {
                    if (host.Skills.TryGetValue(skillId, out var current) && ReferenceEquals(current, skill)) host.Skills.Remove(skillId);
                    if (!Pending.Exists(entry => ReferenceEquals(entry.skill, skill))) Pending.Add((skill, pool));
                }
                if (host.Skills.Count == 0) Hosts.Remove(host);
                throw;
            }
            finally { EndAdd(host); if (--_depth == 0) FlushReturns(); }
        }

        public static void AddSkills(IEquippableSkill host, IList<int> skills)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            BeginAdd(host); _depth++;
            try { foreach (var id in new List<int>(skills)) { if (CancelledAdds.Contains(host)) break; AddSkills(host, id); } }
            finally { EndAdd(host); if (--_depth == 0) FlushReturns(); }
        }
        public static void RemoveSkills(IEquippableSkill host, IList<int> skills)
        {
            var selected = new HashSet<int>(skills);
            ForEachOwnedSkill(host, (id, _) => { if (selected.Contains(id)) RemoveSkills(host, id); });
        }

        private static void BeginAdd(IEquippableSkill host)
        { AddingDepth.TryGetValue(host, out var depth); AddingDepth[host] = depth + 1; }
        private static void EndAdd(IEquippableSkill host)
        {
            if (AddingDepth[host] == 1) { AddingDepth.Remove(host); CancelledAdds.Remove(host); }
            else AddingDepth[host]--;
        }

        private static void ForEachOwnedSkill(IEquippableSkill host, Action<int, SkillBase> action)
        {
            if (host?.Skills == null) return;
            var snapshot = new List<(int id, SkillBase skill, int version)>();
            foreach (var pair in host.Skills) snapshot.Add((pair.Key, pair.Value, pair.Value.Version));
            var errors = new List<Exception>();
            _depth++;
            try
            {
                foreach (var entry in snapshot)
                {
                    if (entry.version != entry.skill.Version || !ReferenceEquals(entry.skill.Releaser, host) ||
                        !host.Skills.TryGetValue(entry.id, out var current) || !ReferenceEquals(current, entry.skill)) continue;
                    try { action(entry.id, entry.skill); } catch (Exception error) { errors.Add(error); }
                }
            }
            finally { if (--_depth == 0) FlushReturns(); }
            if (errors.Count != 0) throw new AggregateException(errors);
        }

        public static void RemoveSkills(IEquippableSkill host, int id)
        {
            if (host == null || host.Skills == null || !host.Skills.TryGetValue(id, out var skill)) return;
            host.Skills.Remove(id);
            if (host.Skills.Count == 0) Hosts.Remove(host);
            skill.BeginRemove();
            _depth++;
            try
            {
                try { skill.Interrupt(); }
                finally { skill.OnRemove(); }
            }
            finally
            {
                Pending.Add((skill, Pools[skill.GetType()]));
                if (--_depth == 0) FlushReturns();
            }
        }

        public static void CloseHost(IEquippableSkill host)
        {
            if (host == null || !Closing.Add(host)) return;
            if (AddingDepth.ContainsKey(host)) CancelledAdds.Add(host);
            try { ForEachOwnedSkill(host, (id, _) => RemoveSkills(host, id)); }
            finally { Hosts.Remove(host); Closing.Remove(host); ReleaseIdleUpdates(); }
        }

        public static void InterruptAllSkill(IEquippableSkill host)
        { ForEachOwnedSkill(host, (_, skill) => skill.Interrupt()); }
        public static bool IsReleasingSkills(IEquippableSkill host)
        { if (host?.Skills != null) foreach (var skill in host.Skills.Values) if (skill.IsReleasing) return true; return false; }

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
                    var snapshot = new List<(SkillBase skill, int version)>();
                    foreach (var skill in host.Skills.Values) snapshot.Add((skill, skill.Version));
                    foreach (var entry in snapshot)
                    {
                        if (_stopping || !entry.skill.IsAttached || entry.version != entry.skill.Version) continue;
                        if (phase == 0) entry.skill.Update();
                        else if (entry.skill.IsReleasing && phase == 1) entry.skill.FixedUpdate();
                        else if (entry.skill.IsReleasing) entry.skill.OnLateUpdate();
                    }
                }
            }
            finally { if (--_depth == 0) FlushReturns(); }
        }

        private static void FlushReturns()
        {
            foreach (var entry in Pending)
            {
                var data = entry.skill.SkillData;
                entry.pool.Recycle(entry.skill);
                if (data) UnityEngine.Object.Destroy(data);
            }
            Pending.Clear();
            ReleaseIdleUpdates();
            if (_stopping)
            {
                foreach (var pool in Pools.Values) pool.Dispose();
                Pools.Clear();
                foreach (var lease in Leases.Values) lease.Dispose();
                Leases.Clear(); Configs.Clear();
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
            try
            {
                foreach (var host in Hosts.ToArray()) try { CloseHost(host); } catch (Exception error) { errors.Add(error); }
            }
            finally { if (--_depth == 0) FlushReturns(); }
            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }
}
