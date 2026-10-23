using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;
using YLFramework.Foundation;
using YLFramework.Services.Resource;

namespace YLFramework.MissionSystem
{
    /// <summary>保留多配表、参数约束、任务状态与奖励配置；不拥有存档权威。</summary>
    public static class MissionManager
    {
        private sealed class RuntimeConfig
        {
            internal string Name;
            internal MissionsConfig Source;
            internal IDisposable Lease;
            internal readonly Dictionary<int, MissionBase> Missions = new();
            internal readonly Dictionary<int, MissionParameter> Parameters = new();
            internal readonly Dictionary<int, List<MissionBase>> References = new();
        }
        private static readonly Dictionary<int, RuntimeConfig> _configs = new();
        private static readonly Dictionary<int, string> _keys = new();
        private static readonly Dictionary<Type, ObjectPool<MissionBase>> _pools = new();
        private static bool _isStopping;
        public static int ConfigCount => _configs.Count;
        static MissionManager()
        {
            SingletonSessionState.RegisterSessionReset(Shutdown);
            Application.quitting += Shutdown;
        }

        /// <summary>确定性的Ordinal FNV-1a；不是旧Animator hash/存档ID。注册时显式拒绝碰撞。</summary>
        public static int StringToHash(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            unchecked
            {
                var hash = 2166136261u;
                foreach (var value in text)
                {
                    hash ^= value;
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
        private static int RememberKey(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("任务配表/参数名称不能为空。", nameof(text));
            }

            var key = StringToHash(text);
            if (_keys.TryGetValue(key, out var existing) && !string.Equals(existing, text, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"任务键碰撞: '{existing}' / '{text}', hash={key}。请更名，不能混用状态。");
            }

            _keys[key] = text;
            return key;
        }
        private static RuntimeConfig GetRuntime(string name)
        {
            if (name == null || !_configs.TryGetValue(StringToHash(name), out var state) || !string.Equals(state.Name, name, StringComparison.Ordinal))
            {
                return null;
            }

            return state;
        }
        private static RuntimeConfig GetRuntime(int hash) => _configs.TryGetValue(hash, out var state) ? state : null;

        public static void AddConfig(MissionsConfig source)
        {
            if (!source)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (_isStopping)
            {
                throw new InvalidOperationException("任务会话正在退出。");
            }

            var key = RememberKey(source.name);
            if (_configs.ContainsKey(key))
            {
                throw new InvalidOperationException($"任务配表重复: {source.name}");
            }

            var state = new RuntimeConfig { Name = source.name, Source = source };
            _configs.Add(key, state);
            try
            {
                foreach (var parameter in source.parameters)
                {
                    if (parameter == null)
                    {
                        throw new InvalidOperationException($"任务参数为空: {source.name}");
                    }

                    var id = RememberKey(parameter.name);
                    if (state.Parameters.ContainsKey(id))
                    {
                        throw new InvalidOperationException($"任务参数名称重复: {source.name}/{parameter.name}");
                    }

                    state.Parameters.Add(id, parameter.CloneConfiguration());
                    state.References.Add(id, new List<MissionBase>());
                }
                foreach (var data in source.missions)
                {
                    if (!data)
                    {
                        throw new InvalidOperationException($"任务条目为空: {source.name}");
                    }

                    if (state.Missions.ContainsKey(data.id))
                    {
                        throw new InvalidOperationException($"任务ID重复: {source.name}/{data.id}");
                    }

                    foreach (var condition in data.conditions)
                    {
                        if (condition == null || !state.Parameters.TryGetValue(StringToHash(condition.paramName ?? string.Empty), out var parameter) || !string.Equals(parameter.name, condition.paramName, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException($"任务条件参数不存在: 配表={source.name}, ID={data.id}, 参数={condition?.paramName}");
                        }
                    }

                    var type = string.IsNullOrWhiteSpace(data.missionTypeClassFullName) ? null : AssemblyTool.GetType(data.missionTypeClassFullName);
                    if (type == null || type.IsAbstract || !typeof(MissionBase).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                    {
                        throw new InvalidOperationException($"任务类型无效: 配表={source.name}, ID={data.id}, class={data.missionTypeClassFullName}");
                    }

                    if (!_pools.TryGetValue(type, out var pool))
                    {
                        pool = new ObjectPool<MissionBase>(() => (MissionBase)Activator.CreateInstance(type), reset: mission => mission.Reset());
                        _pools.Add(type, pool);
                    }
                    var mission = pool.Allocate();
                    try
                    {
                        mission.MissionConfig = UnityEngine.Object.Instantiate(data);
                        mission.MissionConfig.parent = source;
                        mission.ConfigNameHash = key;
                        state.Missions.Add(data.id, mission);
                        mission.Init();
                        foreach (var condition in mission.MissionConfig.conditions)
                        {
                            var references = state.References[condition.ParamNameHash];
                            if (!references.Contains(mission))
                            {
                                references.Add(mission);
                            }
                        }
                    }
                    catch
                    {
                        if (!state.Missions.ContainsKey(data.id))
                        {
                            pool.Recycle(mission);
                        }

                        throw;
                    }
                }
            }
            catch
            {
                _configs.Remove(key);
                try
                {
                    Release(state);
                }
                finally
                {
                    RebuildKeys();
                }
                throw;
            }
        }

        public static void LoadConfig(ResourceSystem resources, ResourceAddress address)
        {
            var lease = resources.Acquire<MissionsConfig>(address);
            try
            {
                AddConfig(lease.Asset);
                GetRuntime(lease.Asset.name).Lease = lease;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
        public static MissionsConfig GetConfig(string name) => GetRuntime(name)?.Source;
        public static MissionsConfig GetConfig(int hash) => GetRuntime(hash)?.Source;
        public static void RemoveConfig(string name)
        {
            var state = GetRuntime(name);
            if (state != null)
            {
                RemoveConfig(StringToHash(state.Name));
            }
        }
        public static void RemoveConfig(int hash)
        {
            if (_configs.TryGetValue(hash, out var state))
            {
                _configs.Remove(hash);
                try
                {
                    Release(state);
                }
                finally
                {
                    RebuildKeys();
                }
            }
        }

        private static void RebuildKeys()
        {
            _keys.Clear();
            foreach (var state in _configs.Values)
            {
                RememberKey(state.Name);
                foreach (var parameter in state.Parameters.Values)
                {
                    RememberKey(parameter.name);
                }
            }
        }

        private static void Release(RuntimeConfig state)
        {
            foreach (var mission in state.Missions.Values)
            {
                var data = mission.MissionConfig;
                mission.RemoveListener();
                if (data)
                {
                    foreach (var condition in data.conditions)
                    {
                        condition.Dispose();
                    }
                }

                _pools[mission.GetType()].Recycle(mission);
                mission.MissionConfig = null;
                mission.UserData = null;
                if (data)
                {
                    UnityEngine.Object.Destroy(data);
                }
            }
            foreach (var parameter in state.Parameters.Values)
            {
                parameter.Reset();
            }

            state.Missions.Clear();
            state.Parameters.Clear();
            state.References.Clear();
            state.Lease?.Dispose();
            state.Lease = null;
        }

        public static MissionBase GetMission(string config, int id) => GetMission(GetRuntime(config), id);
        public static MissionBase GetMission(int config, int id) => GetMission(GetRuntime(config), id);
        private static MissionBase GetMission(RuntimeConfig state, int id) => state != null && state.Missions.TryGetValue(id, out var mission) ? mission : null;
        public static List<MissionBase> GetAllMissions(string config) => GetRuntime(config) is RuntimeConfig state ? new List<MissionBase>(state.Missions.Values) : new List<MissionBase>();
        public static List<MissionBase> GetAllMissions(int config) => GetRuntime(config) is RuntimeConfig state ? new List<MissionBase>(state.Missions.Values) : new List<MissionBase>();
        public static void GetAllMissionsNonAlloc(string config, List<MissionBase> results)
        {
            results.Clear();
            if (GetRuntime(config) is RuntimeConfig state)
            {
                results.AddRange(state.Missions.Values);
            }
        }
        public static void GetAllMissionsNonAlloc(int config, List<MissionBase> results)
        {
            results.Clear();
            if (GetRuntime(config) is RuntimeConfig state)
            {
                results.AddRange(state.Missions.Values);
            }
        }
        public static List<MissionParameter> GetAllParameters(string config) => GetRuntime(config) is RuntimeConfig state ? new List<MissionParameter>(state.Parameters.Values) : new List<MissionParameter>();
        public static List<MissionParameter> GetAllParameters(int config) => GetRuntime(config) is RuntimeConfig state ? new List<MissionParameter>(state.Parameters.Values) : new List<MissionParameter>();
        public static void GetAllParametersNonAlloc(string config, List<MissionParameter> results)
        {
            results.Clear();
            if (GetRuntime(config) is RuntimeConfig state)
            {
                results.AddRange(state.Parameters.Values);
            }
        }
        public static void GetAllParametersNonAlloc(int config, List<MissionParameter> results)
        {
            results.Clear();
            if (GetRuntime(config) is RuntimeConfig state)
            {
                results.AddRange(state.Parameters.Values);
            }
        }
        internal static List<MissionBase> GetParamRefMission(int config, int parameter) => GetRuntime(config) is RuntimeConfig state && state.References.TryGetValue(parameter, out var list) ? new List<MissionBase>(list) : new List<MissionBase>();
        internal static MissionParameter GetMissionParam(int config, int parameter) => GetRuntime(config) is RuntimeConfig state && state.Parameters.TryGetValue(parameter, out var value) ? value : null;
        private static MissionParameter GetMissionParam(string config, string parameter)
        {
            var state = GetRuntime(config);
            if (state == null || parameter == null || !state.Parameters.TryGetValue(StringToHash(parameter), out var value) || !string.Equals(value.name, parameter, StringComparison.Ordinal))
            {
                return null;
            }

            return value;
        }
        private static void Set(MissionParameter parameter, RuntimeConfig state, MissionParamType type, float value)
        {
            if (parameter == null || state == null)
            {
                throw new InvalidOperationException("任务配表/参数尚未登记或已经移除。");
            }

            if (parameter.type != type)
            {
                throw new InvalidOperationException($"任务参数类型不匹配: {state.Name}/{parameter.name}, actual={parameter.type}, requested={type}");
            }

            var references = state.References[StringToHash(parameter.name)];
            if (parameter.onlySetInProgress && !references.Exists(mission => mission.State == MissionState.InProgress))
            {
                return;
            }

            if (!parameter.onlySetLarger || value > parameter.Value)
            {
                parameter.Value = value;
            }
        }
        public static void SetBool(string config, string parameter, bool value) => Set(GetMissionParam(config, parameter), GetRuntime(config), MissionParamType.Bool, value ? 1 : 0);
        public static void SetInt(string config, string parameter, int value) => Set(GetMissionParam(config, parameter), GetRuntime(config), MissionParamType.Int, value);
        public static void SetFloat(string config, string parameter, float value) => Set(GetMissionParam(config, parameter), GetRuntime(config), MissionParamType.Float, value);
        public static void SetBool(int config, int parameter, bool value) => Set(GetMissionParam(config, parameter), GetRuntime(config), MissionParamType.Bool, value ? 1 : 0);
        public static void SetInt(int config, int parameter, int value) => Set(GetMissionParam(config, parameter), GetRuntime(config), MissionParamType.Int, value);
        public static void SetFloat(int config, int parameter, float value) => Set(GetMissionParam(config, parameter), GetRuntime(config), MissionParamType.Float, value);
        private static float Read(MissionParameter parameter, MissionParamType type) => parameter != null && parameter.type == type ? parameter.Value : 0;
        public static bool GetBool(string config, string parameter) => Read(GetMissionParam(config, parameter), MissionParamType.Bool) == 1;
        public static int GetInt(string config, string parameter) => (int)Math.Round(Read(GetMissionParam(config, parameter), MissionParamType.Int));
        public static float GetFloat(string config, string parameter) => Read(GetMissionParam(config, parameter), MissionParamType.Float);
        public static bool GetBool(int config, int parameter) => Read(GetMissionParam(config, parameter), MissionParamType.Bool) == 1;
        public static int GetInt(int config, int parameter) => (int)Math.Round(Read(GetMissionParam(config, parameter), MissionParamType.Int));
        public static float GetFloat(int config, int parameter) => Read(GetMissionParam(config, parameter), MissionParamType.Float);
        public static void AddInt(string config, string parameter, int value) => SetInt(config, parameter, GetInt(config, parameter) + value);
        public static void AddFloat(string config, string parameter, float value) => SetFloat(config, parameter, GetFloat(config, parameter) + value);
        public static void AddInt(int config, int parameter, int value) => SetInt(config, parameter, GetInt(config, parameter) + value);
        public static void AddFloat(int config, int parameter, float value) => SetFloat(config, parameter, GetFloat(config, parameter) + value);

        public static void Reset()
        {
            foreach (var key in new List<int>(_configs.Keys))
            {
                Reset(key);
            }
        }
        public static void Reset(string name)
        {
            if (GetRuntime(name) is RuntimeConfig state)
            {
                Reset(StringToHash(state.Name));
            }
        }
        public static void Reset(int hash)
        {
            var state = GetRuntime(hash);
            if (state == null)
            {
                return;
            }

            foreach (var mission in state.Missions.Values)
            {
                mission.Reset();
            }

            foreach (var parameter in state.Parameters.Values)
            {
                parameter.Reset();
            }

            foreach (var mission in state.Missions.Values)
            {
                mission.Init();
            }
        }
        public static void Shutdown()
        {
            if (_isStopping)
            {
                return;
            }

            _isStopping = true;
            try
            {
                foreach (var key in new List<int>(_configs.Keys))
                {
                    RemoveConfig(key);
                }

                foreach (var pool in _pools.Values)
                {
                    pool.Dispose();
                }

                _pools.Clear();
                _keys.Clear();
            }
            finally
            {
                _isStopping = false;
            }
        }
    }
}
