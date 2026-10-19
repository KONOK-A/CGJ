// Derived from XF Localization 1.0.9. Copyright (c) 2024 弦风课堂. MIT; see LICENSE.txt in this directory.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using YLFramework.Core.Singleton;
using YLFramework.Events;
using YLFramework.Services.Resource;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Localization
{
    /// <summary>显式配置登记与文本查询；只在Unity主线程使用，不扫描资产或自动创建宿主。</summary>
    public static class LocalizationManager
    {
        public const string LanguagePreferenceKey = "YLFramework.Localization.Language";
        private static readonly List<LocalizationConfigHandle> registrations = new();
        private static readonly HashSet<LocalizationConfigHandle> owned = new();
        private static readonly EventChannel changed = new();
        private static SystemLanguage requested = SystemLanguage.Unknown;
        private static bool hasRequested;
        public static IEventSource Changed => changed;

        static LocalizationManager()
        {
            SingletonSessionState.RegisterSessionReset(Shutdown);
            Application.quitting += Shutdown;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) Shutdown();
            };
#endif
        }

        public static IReadOnlyList<MultiLanguageConfigs> Configs
        {
            get
            {
                var result = new List<MultiLanguageConfigs>();
                foreach (var handle in registrations)
                    if (handle.Config && !result.Contains(handle.Config)) result.Add(handle.Config);
                return result;
            }
        }

        public static IReadOnlyDictionary<SystemLanguage, string> Languages
        {
            get
            {
                var result = new Dictionary<SystemLanguage, string>();
                foreach (var root in Configs)
                    foreach (var item in root.Languages)
                        if (item && !result.ContainsKey(item.language)) result.Add(item.language, string.IsNullOrEmpty(item.displayName) ? item.language.ToString() : item.displayName);
                return result;
            }
        }

        public static SystemLanguage RequestedLanguage
        {
            get
            {
                if (!hasRequested)
                {
                    var stored = PlayerPrefs.GetInt(LanguagePreferenceKey, -1);
                    requested = stored >= 0 && Enum.IsDefined(typeof(SystemLanguage), stored) ? (SystemLanguage)stored : Application.systemLanguage;
                    hasRequested = true;
                }
                return requested;
            }
        }

        /// <summary>当前可用语言；设置保存请求语言，实际条目仍可逐项回退。</summary>
        public static SystemLanguage Language
        {
            get
            {
                var available = RegisteredLanguages();
                foreach (var candidate in LocalizationFallback.GetCandidates(RequestedLanguage, available))
                    if (available.Contains(candidate)) return candidate;
                return SystemLanguage.Unknown;
            }
            set
            {
                if (!Enum.IsDefined(typeof(SystemLanguage), value)) throw new ArgumentOutOfRangeException(nameof(value));
                if (hasRequested && requested == value) return;
                requested = value;
                hasRequested = value != SystemLanguage.Unknown;
                if (value == SystemLanguage.Unknown) PlayerPrefs.DeleteKey(LanguagePreferenceKey);
                else PlayerPrefs.SetInt(LanguagePreferenceKey, (int)value);
                PlayerPrefs.Save();
                Refresh();
            }
        }

        public static LocalizationConfigHandle AddConfig(MultiLanguageConfigs config)
        {
            if (!config) throw new ArgumentNullException(nameof(config));
            var handle = new LocalizationConfigHandle();
            owned.Add(handle);
            try { handle.Attach(config); }
            catch { handle.Dispose(); throw; }
            return handle;
        }

        /// <summary>句柄拥有请求和根Lease；Dispose取消在途并移除自身登记，不影响共享根的其他owner。</summary>
        public static LocalizationConfigHandle LoadConfigAsync(ResourceSystem resources, ResourceAddress address)
        {
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            var handle = new LocalizationConfigHandle();
            owned.Add(handle);
            try { handle.Load(resources, address); }
            catch { handle.Dispose(); throw; }
            return handle;
        }

        internal static void Register(LocalizationConfigHandle handle)
        {
            var roots = new List<MultiLanguageConfigs>(Configs);
            if (!roots.Contains(handle.Config)) roots.Add(handle.Config);
            Validate(roots);
            registrations.Add(handle);
            Refresh();
        }

        internal static void Unregister(LocalizationConfigHandle handle)
        {
            owned.Remove(handle);
            if (registrations.Remove(handle)) Refresh();
        }

        public static MultiLanguageConfigs GetConfig(string name)
        {
            foreach (var root in Configs) if (root.name == name) return root;
            return null;
        }

        public static void RemoveConfig(MultiLanguageConfigs config)
        {
            foreach (var handle in registrations.ToArray()) if (handle.Config == config) handle.Dispose();
        }

        public static void RemoveConfig(string name) => RemoveConfig(GetConfig(name));

        public static LanguageInfo GetLanguageInfo(int id) => GetLanguageInfo(id, RequestedLanguage);

        public static LanguageInfo GetLanguageInfo(int id, SystemLanguage language)
        {
            var roots = Configs;
            return LocalizationFallback.Find(language, RegisteredLanguages(), candidate =>
            {
                LanguageInfo found = null;
                foreach (var root in roots)
                {
                    var entry = root.GetLanguageConfigData(candidate)?.GetLanguageInfo(id);
                    if (entry == null) continue;
                    if (found != null) throw new InvalidOperationException($"Localization '{root.name}', language={candidate}: duplicate ID {id} across configs.");
                    found = entry;
                }
                return IsUsable(found) ? found : null;
            });
        }

        private static bool IsUsable(LanguageInfo entry)
        {
            if (entry == null) return false;
            switch (entry.type)
            {
                case MultiLanguageType.String: return entry.str != null;
                case MultiLanguageType.Image: return entry.image != null && !string.IsNullOrWhiteSpace(entry.image.projectName) && !string.IsNullOrWhiteSpace(entry.image.imageName);
                case MultiLanguageType.NetworkImage: return !string.IsNullOrWhiteSpace(entry.url);
                default: return false;
            }
        }

        public static string GetString(int id, params object[] args)
        {
            var entry = GetLanguageInfo(id);
            if (entry == null || entry.type != MultiLanguageType.String)
            {
                Debug.LogWarning($"Localization language={RequestedLanguage}, ID={id}: missing text; configs={ConfigNames()}.");
                return string.Empty;
            }
            try { return args == null || args.Length == 0 ? entry.str : string.Format(CultureInfo.CurrentCulture, entry.str, args); }
            catch (FormatException error)
            { throw new FormatException($"Localization language={RequestedLanguage}, ID={id}, configs={ConfigNames()}: invalid text format.", error); }
        }

        public static void Validate(IEnumerable<MultiLanguageConfigs> roots)
        {
            var keys = new HashSet<(SystemLanguage, int)>();
            foreach (var root in roots)
            {
                if (!root) throw new InvalidOperationException("Localization: missing root config.");
                if (root.Languages == null) throw new InvalidOperationException($"Localization '{root.name}': language list is null.");
                foreach (var language in root.Languages)
                {
                    if (!language) throw new InvalidOperationException($"Localization '{root.name}': missing language config.");
                    if (language.language == SystemLanguage.Unknown || !Enum.IsDefined(typeof(SystemLanguage), language.language))
                        throw new InvalidOperationException($"Localization '{root.name}/{language.name}': invalid language {language.language}.");
                    root.GetLanguageConfigData(language.language);
                    language.GetLanguageInfo(int.MinValue);
                    foreach (var entry in language.languagesInfo)
                        if (!keys.Add((language.language, entry.id)))
                            throw new InvalidOperationException($"Localization '{root.name}/{language.name}', language={language.language}: duplicate ID {entry.id} across configs.");
                }
            }
        }

        public static void Refresh()
        {
            try { changed.TriggerAll(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public static void Shutdown()
        {
            changed.ResetOwner();
            foreach (var handle in new List<LocalizationConfigHandle>(owned)) handle.Dispose();
            hasRequested = false;
            requested = SystemLanguage.Unknown;
        }

        private static List<SystemLanguage> RegisteredLanguages()
        {
            var result = new List<SystemLanguage>();
            foreach (var root in Configs)
                foreach (var item in root.Languages)
                    if (item && !result.Contains(item.language)) result.Add(item.language);
            return result;
        }

        private static string ConfigNames()
        {
            var names = new List<string>();
            foreach (var root in Configs) names.Add(root.name);
            return string.Join(", ", names);
        }
    }
}
