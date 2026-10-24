using System;
using UnityEngine;

namespace YLFramework.Audio
{
    /// <summary>Audio 专用资源后端。所有调用、完成和进度投递均须在 Unity 主线程；不提供物理取消承诺。</summary>
    public interface IAudioClipProvider
    {
        /// <summary>是否具有可用于闲置缓存的真实保活责任。共享借用后端应返回 false。</summary>
        bool CanCache
        {
            get;
        }
        /// <summary>同步取得独立资源责任；失败抛异常，不返回未持有的资产。</summary>
        AudioClipLease Load(string key);
        /// <summary>取得资产后投递租约或错误。允许同步完成；即使所有等待者取消，后端仍须完成自己的收尾。</summary>
        /// <remarks>完成只应投递一次，每个成功租约必须独立；Audio 只逻辑取消等待者，不中止共享 producer。</remarks>
        void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress);
    }

    /// <summary>完整资源身份为 Provider 实例与该实例内的完整 key；不以 Clip 名称或对象合并所有权。</summary>
    public readonly struct AudioClipAddress
    {
        /// <summary>明确指定的资源后端，不隐式查 Module 或全局注册。</summary>
        public IAudioClipProvider Provider
        {
            get;
        }
        /// <summary>后端内的完整资源定位；Bundle/Project 等上下文应包含于 key 或固定 Provider 配置。</summary>
        public string Key
        {
            get;
        }
        public AudioClipAddress(IAudioClipProvider provider, string key)
        {
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A complete resource key is required.", nameof(key));
            }

            Key = key;
        }
        internal void Validate()
        {
            if (Provider == null || string.IsNullOrWhiteSpace(Key))
            {
                throw new ArgumentException("Invalid AudioClip address.");
            }
        }
    }

    /// <summary>后端或 Audio 缓存交付的单次 Clip 保活责任。Dispose 恰一次归还 owner，不自行卸载或 Destroy 资产。</summary>
    public sealed class AudioClipLease : IDisposable
    {
        private IDisposable _owner;
        private Action _validateAcquisition;
        /// <summary>由租约保活的资产；归还后不得再使用。</summary>
        public AudioClip Clip
        {
            get; private set;
        }
        /// <summary>责任是否已归还。</summary>
        public bool IsReleased
        {
            get; private set;
        }
        public AudioClipLease(AudioClip clip, IDisposable owner)
            : this(clip, owner, null) { }

        internal AudioClipLease(AudioClip clip, IDisposable owner, Action validateAcquisition)
        {
            if (!clip)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            Clip = clip;
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _validateAcquisition = validateAcquisition;
        }
        internal void ValidateAcquisition()
        {
            if (IsReleased || !Clip)
            {
                throw new InvalidOperationException("The acquired AudioClip is no longer available.");
            }

            _validateAcquisition?.Invoke();
        }
        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            Clip = null;
            _validateAcquisition = null;
            var owner = _owner;
            _owner = null;
            owner.Dispose();
        }
        internal void ReleaseSafely()
        {
            try
            {
                Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
