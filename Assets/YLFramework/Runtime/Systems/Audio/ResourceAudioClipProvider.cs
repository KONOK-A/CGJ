using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Events;
using YLFramework.Foundation;
using YLFramework.Services.Resource;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private readonly Dictionary<string, Audio.ResourceAudioClipProvider> _audioProviders = new(StringComparer.Ordinal);

        /// <summary>显式取得本 authority、指定 ProjectId 的 Audio 后端；不初始化资源、不建立额外的资产缓存。</summary>
        public Audio.ResourceAudioClipProvider GetAudioClipProvider(string projectId)
        {
            CheckThread();
            if (!IsAvailable)
            {
                throw new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            }

            ResourceAddress.ValidateId(projectId, nameof(projectId));
            if (!_catalogs.ContainsKey(projectId))
            {
                throw new ResourceException(ResourceError.AddressNotFound, "Unknown resource project: " + projectId);
            }

            if (!_audioProviders.TryGetValue(projectId, out var provider))
            {
                _audioProviders.Add(projectId, provider = new Audio.ResourceAudioClipProvider(this, projectId));
            }

            return provider;
        }
    }
}

namespace YLFramework.Services.Audio
{
    /// <summary>正式 Audio 资源后端。key 为所属 ProjectId 内的逻辑地址，不是 Bundle 名或 Resources 物理路径。</summary>
    /// <remarks>由显式初始化的 ResourceSystem.GetAudioClipProvider 装配。每次成功交付独立 AssetLease，
    /// Audio 只管理播放/闲置使用；取消等待者、Audio Shutdown 不关闭 Resource，会话退出由原 Resource 请求完成收尾。</remarks>
    public sealed class ResourceAudioClipProvider : IAudioClipProvider
    {
        private readonly ResourceSystem _resources;
        public string ProjectId
        {
            get;
        }
        public bool CanCache => _resources.IsAvailable;
        internal ResourceAudioClipProvider(ResourceSystem resources, string projectId)
        {
            this._resources = resources;
            ProjectId = projectId;
        }

        public AudioClipLease Load(string key) => Wrap(_resources.Acquire<AudioClip>(new ResourceAddress(ProjectId, key)));

        public void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            var request = _resources.AcquireAsync<AudioClip>(new ResourceAddress(ProjectId, key));
            IUnregister observer = null;
            var finished = false;
            var lastProgress = -1f;
            void ReportProgress(float value, bool terminal = false)
            {
                if (progress == null || (!terminal && value == lastProgress))
                {
                    return;
                }

                lastProgress = value;
                try
                {
                    progress(value);
                }
                catch (Exception failure)
                {
                    Debug.LogException(failure);
                }
            }
            // 原 ResourceRequest 持有尚未交割的结果；不依赖 Audio 宿主或等待协程观察 native 完成。
            request.Completed += done =>
            {
                finished = true;
                observer?.Unregister();
                observer = null;
                AudioClipLease lease = null;
                Exception error = null;
                try
                {
                    if (done.Status != ResourceRequestStatus.Succeeded)
                    {
                        error = done.Error;
                    }
                    else if (done.IsDisposed)
                    {
                        error = new ResourceException(ResourceError.SessionUnavailable, "The Audio resource session ended.");
                    }
                    else
                    {
                        lease = Wrap(done.TakeResult());
                    }
                }
                catch (Exception failure)
                {
                    error = failure;
                }
                finally
                {
                    done.Dispose();
                }
                // 进度观察者的异常不能阻断资源交付和缓存 producer 收尾。
                ReportProgress(done.Progress, true);
                // 进度回调可能重入会话退出；必须在最后一个用户回调之后重新确认交付责任仍有效。
                if (lease != null && (!_resources.IsSessionActive || !lease.Clip))
                {
                    lease.ReleaseSafely();
                    lease = null;
                    error = new ResourceException(_resources.IsSessionActive ? ResourceError.LoadFailed : ResourceError.SessionUnavailable,
                        "The Audio resource became unavailable before delivery.");
                }
                if (lease != null)
                {
                    try
                    {
                        lease.ValidateAcquisition();
                    }
                    catch (Exception failure)
                    {
                        lease.ReleaseSafely();
                        lease = null;
                        error = failure;
                    }
                }
                try
                {
                    completed(lease, error);
                }
                catch
                {
                    lease?.ReleaseSafely();
                    throw;
                }
            };
            if (!finished && progress != null)
            {
                try
                {
                    // UpdateManager 仅观察进度，不拥有完成/资源清理；关闭观察宿主也不会遗失 native 结果。
                    observer = UpdateManager.AddUpdateEvent(() =>
                    {
                        if (!finished)
                        {
                            ReportProgress(request.Progress);
                        }
                    });
                    if (finished)
                    {
                        observer.Unregister();
                        observer = null;
                    }
                    else
                    {
                        ReportProgress(request.Progress);
                    }
                }
                catch
                {
                    request.Dispose();
                    throw;
                }
            }
        }

        private static AudioClipLease Wrap(AssetLease<AudioClip> lease)
        {
            try
            {
                return new AudioClipLease(lease.Asset, lease, () => lease.Record.Owner.RequireContentAdmission(lease.Record.Entry));
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }
    }
}
