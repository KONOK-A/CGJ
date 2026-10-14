using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>素材定位方式；ExternalProvider 必须由调用方显式提供后端，不自动注册 XFAB。</summary>
    public enum AudioClipSource { Clip, Resources, AssetBundle, ExternalProvider }
    /// <summary>组内抽选规则；Weighted 的零权重排除条目。</summary>
    public enum AudioClipSelectionMode { Uniform, Weighted }

    /// <summary>直接借用的 Clip 或完整命名资源定位。BundleName 是 YL owner Bundle，不是 XFAB ProjectName。</summary>
    [Serializable]
    public sealed class AudioClipEntry
    {
        /// <summary>明确的素材定位方式；默认直接 Clip。</summary>
        public AudioClipSource Source;
        /// <summary>仅 Clip 模式使用，调用方负责保活，不自动卸载。</summary>
        public AudioClip Clip;
        /// <summary>命名模式的完整后端 key；Resources 必须包含明确前缀。</summary>
        public string Key;
        /// <summary>仅 AssetBundle 模式使用的 YL owner Bundle 名，不是 ProjectName。</summary>
        public string BundleName;
        /// <summary>Weighted 模式的有限非负相对权重；0 排除，Uniform 忽略。</summary>
        public float Weight = 1f;
    }

    /// <summary>仅负责选曲，不声明音量控制通道或播放所有权。</summary>
    [Serializable]
    public sealed class AudioClipGroup
    {
        /// <summary>区分大小写的组名；同库内重名使该组选择失败。</summary>
        public string Name;
        /// <summary>均匀或加权抽选一次。</summary>
        public AudioClipSelectionMode Selection;
        /// <summary>直接 Clip 与明确命名定位可混用的候选。</summary>
        public List<AudioClipEntry> Entries = new List<AudioClipEntry>();
    }

    /// <summary>一次抽选的不可变定位快照；不会在加载完成后再次随机。</summary>
    public readonly struct AudioClipSelection
    {
        /// <summary>直接素材快照；命名模式为 null。</summary>
        public AudioClip Clip { get; }
        /// <summary>命名资源身份快照；直接模式为 default。</summary>
        public AudioClipAddress Address { get; }
        /// <summary>本次选择是否使用显式 provider。</summary>
        public bool IsNamed => Address.Provider != null;
        internal AudioClipSelection(AudioClip clip, AudioClipAddress address) { Clip = clip; Address = address; }
    }

    /// <summary>可共享的具名素材组。直接 Clip 永远借用，命名定位沿用正式 Audio provider 与租约。</summary>
    [CreateAssetMenu(menuName = "YLFramework/Audio/Clip Library")]
    public sealed class AudioClipLibrary : ScriptableObject
    {
        /// <summary>序列化素材组；不包含控制通道、音源或播放资源租约。</summary>
        public List<AudioClipGroup> Groups = new List<AudioClipGroup>();
        private readonly Dictionary<string, ResLoaderAudioClipProvider> mBundles = new Dictionary<string, ResLoaderAudioClipProvider>(StringComparer.Ordinal);

        /// <summary>精确按组名抽选一次。缺组、重名、非法权重或定位失败返回 false，不创建播放/通道。</summary>
        /// <remarks>均匀模式忽略 Weight；空 Clip/空 key 为不可选条目。非法枚举、负数/非有限加权权重使整组失败。
        /// ExternalProvider 只使用显式传入的实例；Resources 要求 resources:// 或 resources/ 前缀。</remarks>
        public bool TrySelect(string groupName, out AudioClipSelection selection, IAudioClipProvider externalProvider = null)
        {
            selection = default;
            if (string.IsNullOrWhiteSpace(groupName) || Groups == null) return false;
            AudioClipGroup group = null;
            foreach (var candidate in Groups)
            {
                if (candidate == null || !string.Equals(candidate.Name, groupName, StringComparison.Ordinal)) continue;
                if (group != null) return false;
                group = candidate;
            }
            if (group == null || group.Entries == null || (uint)group.Selection > (uint)AudioClipSelectionMode.Weighted) return false;
            var weighted = group.Selection == AudioClipSelectionMode.Weighted;
            double total = 0;
            foreach (var entry in group.Entries)
            {
                if (entry == null) continue;
                if ((uint)entry.Source > (uint)AudioClipSource.ExternalProvider ||
                    weighted && (!AudioSoundOptions.IsFinite(entry.Weight) || entry.Weight < 0)) return false;
                if (CanSelect(entry, externalProvider)) total += weighted ? entry.Weight : 1;
            }
            if (total <= 0) return false;
            var point = UnityEngine.Random.value * total;
            AudioClipEntry selected = null;
            foreach (var entry in group.Entries)
            {
                if (entry == null || !CanSelect(entry, externalProvider)) continue;
                var weight = weighted ? entry.Weight : 1;
                if (weight <= 0) continue;
                selected = entry;
                point -= weight;
                if (point < 0) break;
            }
            if (selected == null) return false;
            if (selected.Source == AudioClipSource.Clip) selection = new AudioClipSelection(selected.Clip, default);
            else
            {
                IAudioClipProvider provider = externalProvider;
                if (selected.Source == AudioClipSource.Resources) provider = ResLoaderAudioClipProvider.Resources;
                else if (selected.Source == AudioClipSource.AssetBundle)
                {
                    if (!mBundles.TryGetValue(selected.BundleName, out var bundle))
                        mBundles.Add(selected.BundleName, bundle = new ResLoaderAudioClipProvider(selected.BundleName));
                    provider = bundle;
                }
                selection = new AudioClipSelection(null, new AudioClipAddress(provider, selected.Key));
            }
            return true;
        }

        private static bool CanSelect(AudioClipEntry entry, IAudioClipProvider externalProvider)
        {
            if (entry.Source == AudioClipSource.Clip) return entry.Clip;
            if (string.IsNullOrWhiteSpace(entry.Key)) return false;
            var resources = entry.Key.StartsWith("resources://", StringComparison.OrdinalIgnoreCase) || entry.Key.StartsWith("resources/", StringComparison.OrdinalIgnoreCase);
            if (entry.Source == AudioClipSource.Resources) return resources && string.IsNullOrEmpty(entry.BundleName);
            if (entry.Source == AudioClipSource.AssetBundle) return !resources && !string.IsNullOrWhiteSpace(entry.BundleName);
            return externalProvider != null;
        }
    }
}
