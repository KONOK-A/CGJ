// Adapted from ScriptableObjectSystem 1.1.4 (MIT). Copyright (c) 2024 弦风课堂.
using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.DataAssets
{
    /// <summary>按精确根名称查询的显式入口，不扫描资产、不自动加载、不接管其他系统配置。</summary>
    public static class ScriptableObjectManager
    {
        private static readonly DataAssetRegistry<ScriptableObjectConfigs> registry = new DataAssetRegistry<ScriptableObjectConfigs>(
            root => { if (root) root.ClearCache(); });
        private static readonly HashSet<ScriptableObjectRegistration> registrations = new HashSet<ScriptableObjectRegistration>();

        /// <summary>当前有效owner登记数（同一根的两个owner计两次），用于定位未归还责任。</summary>
        public static int RegistrationCount { get { PruneInvalid(); return registrations.Count; } }

        /// <summary>借用已加载根；返回值归调用owner Dispose。多个owner可登记同一根。</summary>
        public static ScriptableObjectRegistration AddConfig(ScriptableObjectConfigs config) => Register(config, null, null);

        private static ScriptableObjectRegistration Register(ScriptableObjectConfigs config, Func<bool> valid, Action release)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            PruneInvalid();
            config.TryGetIDRange(out _, out _);
            var key = config.name;
            var entry = registry.Register(key, config);
            var owner = new ScriptableObjectRegistration(config, entry, valid, release);
            try { DataAssetLifetime.Track(owner); registrations.Add(owner); }
            catch { entry.Dispose(); throw; }
            return owner;
        }

        /// <summary>同步取得根Lease并登记；登记失败自动归还Lease。</summary>
        public static ScriptableObjectRegistration Load(ResourceSystem resource, ResourceAddress address)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            var generation = DataAssetLifetime.Generation;
            var lease = resource.Acquire<ScriptableObjectConfigs>(address);
            try
            {
                if (generation != DataAssetLifetime.Generation) throw new OperationCanceledException("DataAssets closed during the synchronous load.");
                return Register(lease.Asset, () => lease.IsValid, lease.Dispose);
            }
            catch { lease.Dispose(); throw; }
        }

        public static ScriptableObjectAsset<ScriptableObjectConfigs> LoadAsync(ResourceSystem resource, ResourceAddress address)
        {
            ScriptableObjectRegistration registration = null;
            var owner = new ScriptableObjectAsset<ScriptableObjectConfigs>();
            owner.Start(resource, address,
                root => registration = Register(root, () => owner.IsValid, owner.Dispose),
                () => registration?.Dispose());
            return owner;
        }

        public static ScriptableObjectConfigs GetConfig(string name)
        {
            PruneInvalid();
            var root = registry.Get(name);
            return root ? root : null;
        }

        public static void RemoveConfig(ScriptableObjectConfigs config)
        {
            foreach (var owner in new List<ScriptableObjectRegistration>(registrations))
                if (ReferenceEquals(owner.Root, config)) owner.Dispose();
        }

        private static void PruneInvalid()
        {
            List<ScriptableObjectRegistration> invalid = null;
            foreach (var owner in registrations)
                if (!owner.IsValid) (invalid ??= new List<ScriptableObjectRegistration>()).Add(owner);
            if (invalid != null) foreach (var owner in invalid) owner.Dispose();
        }

        /// <summary>关闭本域全部配置加载/登记（包含Singleton配置），不关闭ResourceSystem。</summary>
        public static void Clear() => DataAssetLifetime.Clear();

        internal static void Forget(ScriptableObjectRegistration owner) => registrations.Remove(owner);
    }

    public sealed class ScriptableObjectRegistration : IDisposable
    {
        private IDisposable entry;
        private Func<bool> valid;
        private Action release;
        internal ScriptableObjectConfigs Root { get; private set; }
        public bool IsDisposed => entry == null;
        public bool IsValid => !IsDisposed && Root && (valid == null || valid());
        public ScriptableObjectConfigs Config => IsValid ? Root : throw new InvalidOperationException("The DataAssets registration is no longer valid.");

        internal ScriptableObjectRegistration(ScriptableObjectConfigs root, IDisposable entry, Func<bool> valid, Action release)
        {
            Root = root;
            this.entry = entry;
            this.valid = valid;
            this.release = release;
        }

        public void Dispose()
        {
            if (entry == null) return;
            var owned = entry;
            var onRelease = release;
            entry = null;
            valid = null;
            release = null;
            ScriptableObjectManager.Forget(this);
            DataAssetLifetime.Untrack(this);
            Root = null;
            try { owned.Dispose(); }
            finally { onRelease?.Invoke(); }
        }
    }
}
