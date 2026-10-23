using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.DataAssets
{
    /// <summary>按根名称/hash查询的显式入口，不扫描资产、不自动加载、不接管其他系统配置。</summary>
    public static class ScriptableObjectManager
    {
        private static readonly DataAssetRegistry<ScriptableObjectConfigs> _registry = new DataAssetRegistry<ScriptableObjectConfigs>(
            root =>
            {
                if (root)
                {
                    root.ClearCache();
                }
            });
        private static readonly HashSet<ScriptableObjectRegistration> _registrations = new HashSet<ScriptableObjectRegistration>();

        /// <summary>当前有效owner登记数（同一根的两个owner计两次），用于定位未归还责任。</summary>
        public static int RegistrationCount
        {
            get
            {
                PruneInvalid();
                return _registrations.Count;
            }
        }

        /// <summary>借用已加载根；返回值归调用owner Dispose。多个owner可登记同一根。</summary>
        public static ScriptableObjectRegistration AddConfig(ScriptableObjectConfigs config) => Register(config, null, null);

        private static ScriptableObjectRegistration Register(ScriptableObjectConfigs config, Func<bool> valid, Action release)
        {
            if (!config)
            {
                throw new ArgumentNullException(nameof(config));
            }

            PruneInvalid();
            config.TryGetIdRange(out _, out _);
            var key = config.name;
            var entry = _registry.Register(key, StringToHash(key), config);
            var owner = new ScriptableObjectRegistration(config, key, entry, valid, release);
            try
            {
                DataAssetLifetime.Track(owner);
                _registrations.Add(owner);
            }
            catch
            {
                entry.Dispose();
                throw;
            }
            return owner;
        }

        /// <summary>同步取得根Lease并登记；登记失败自动归还Lease。</summary>
        public static ScriptableObjectRegistration Load(ResourceSystem resource, ResourceAddress address)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            var generation = DataAssetLifetime.Generation;
            var lease = resource.Acquire<ScriptableObjectConfigs>(address);
            try
            {
                if (generation != DataAssetLifetime.Generation)
                {
                    throw new OperationCanceledException("DataAssets closed during the synchronous load.");
                }

                return Register(lease.Asset, () => lease.IsValid, lease.Dispose);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
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
            var root = _registry.Get(name);
            return root ? root : null;
        }

        public static ScriptableObjectConfigs GetConfig(int nameHash)
        {
            PruneInvalid();
            var root = _registry.Get(nameHash);
            return root ? root : null;
        }

        public static int StringToHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A root name is required.", nameof(value));
            }

            return Animator.StringToHash(value);
        }

        public static void RemoveConfig(ScriptableObjectConfigs config)
        {
            foreach (var owner in new List<ScriptableObjectRegistration>(_registrations))
            {
                if (ReferenceEquals(owner.Root, config))
                {
                    owner.Dispose();
                }
            }
        }

        public static void RemoveConfig(string name)
        {
            foreach (var owner in new List<ScriptableObjectRegistration>(_registrations))
            {
                if (StringComparer.Ordinal.Equals(owner.Name, name))
                {
                    owner.Dispose();
                }
            }
        }

        public static void RemoveConfig(int nameHash)
        {
            PruneInvalid();
            var root = _registry.Get(nameHash);
            if (root)
            {
                RemoveConfig(root);
            }
        }

        private static void PruneInvalid()
        {
            List<ScriptableObjectRegistration> invalid = null;
            foreach (var owner in _registrations)
            {
                if (!owner.IsValid)
                {
                    (invalid ??= new List<ScriptableObjectRegistration>()).Add(owner);
                }
            }

            if (invalid != null)
            {
                foreach (var owner in invalid)
                {
                    owner.Dispose();
                }
            }
        }

        /// <summary>关闭本域全部配置加载/登记（包含Singleton配置），不关闭ResourceSystem。</summary>
        public static void Clear() => DataAssetLifetime.Clear();

        internal static void Forget(ScriptableObjectRegistration owner) => _registrations.Remove(owner);
    }

    public sealed class ScriptableObjectRegistration : IDisposable
    {
        private IDisposable _entry;
        private Func<bool> _valid;
        private Action _release;
        internal ScriptableObjectConfigs Root
        {
            get; private set;
        }
        internal string Name
        {
            get;
        }
        public bool IsDisposed => _entry == null;
        public bool IsValid => !IsDisposed && Root && (_valid == null || _valid());
        public ScriptableObjectConfigs Config => IsValid ? Root : throw new InvalidOperationException("The DataAssets registration is no longer valid.");

        internal ScriptableObjectRegistration(ScriptableObjectConfigs root, string name, IDisposable entry, Func<bool> valid, Action release)
        {
            Root = root;
            Name = name;
            this._entry = entry;
            this._valid = valid;
            this._release = release;
        }

        public void Dispose()
        {
            if (_entry == null)
            {
                return;
            }

            var owned = _entry;
            var onRelease = _release;
            _entry = null;
            _valid = null;
            _release = null;
            ScriptableObjectManager.Forget(this);
            DataAssetLifetime.Untrack(this);
            Root = null;
            try
            {
                owned.Dispose();
            }
            finally
            {
                onRelease?.Invoke();
            }
        }
    }
}
