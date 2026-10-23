using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YLFramework.Services.Resource
{
    /// <summary>沿用 Request 的单次领取协议。取消终态不代表 native 操作撤销；Scenes 可在迟到完成后更新。</summary>
    public sealed class SceneRequest : ResourceRequest<SceneLease>
    {
        private readonly ResourceSystem _owner;
        internal AsyncOperation Native;
        internal bool IsActivationRequested;
        public LoadSceneMode Mode
        {
            get;
        }
        public long LoadSequence
        {
            get;
        }
        /// <summary>一旦为 true，Single 可能替换旧场景，Cancel/Dispose 均不承诺回滚。</summary>
        public bool HasNativeStarted
        {
            get; internal set;
        }
        public bool IsNativeCompleted
        {
            get; internal set;
        }
        public bool IsWaitingForActivation => Native != null && !IsActivationRequested && Native.progress >= 0.9f;
        /// <summary>本次实际观察到的身份。歧义结果只保活候选，不授予卸载它们的权利。</summary>
        public IReadOnlyList<SceneRecord> Scenes { get; internal set; } = Array.Empty<SceneRecord>();

        internal SceneRequest(ResourceSystem owner, LoadSceneMode mode, long sequence, bool activate) : base(value => value.Dispose())
        {
            this._owner = owner;
            Mode = mode;
            LoadSequence = sequence;
            IsActivationRequested = activate;
        }

        /// <summary>允许激活，不等同 SetActiveScene。幂等；门控超时、取消和退出也会强制放行 native queue。</summary>
        public void Activate()
        {
            _owner.CheckThread();
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SceneRequest));
            }

            if (IsDone)
            {
                return;
            }

            IsActivationRequested = true;
            if (Native != null)
            {
                Native.allowSceneActivation = true;
            }
        }
    }

    // 只为 Editor 的真实场景加载提供窄接点，不建立另一份 Scene/Bundle authority。
    internal interface IResourceSceneSource
    {
        bool IsScene(ResourceCatalogEntry entry);
        string ResolveScenePath(ResourceCatalogEntry entry);
        AsyncOperation LoadSceneAsync(string path, LoadSceneParameters parameters);
    }
}
