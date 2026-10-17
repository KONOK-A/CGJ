using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using YLFramework.Core;
using YLFramework.Services.Audio;

namespace YLFramework.Services.Resource
{
    /// <summary>应用显式持有的接入责任。构造不初始化；StartAsync复用唯一ResourceSystem，StopAsync只结算本入口的责任。</summary>
    public sealed class ResourceStartup
    {
        private static readonly IReadOnlyDictionary<string, ResourceAudioClipProvider> EmptyAudio =
            new ReadOnlyDictionary<string, ResourceAudioClipProvider>(new Dictionary<string, ResourceAudioClipProvider>());
        private readonly ResourceConfiguration configuration;
        private readonly bool bindCore;
        private readonly IReadOnlyList<string> audioProjects;
        private readonly List<ResourceRequest<ResourceStartup>> starting = new();
        private bool startingResources;
        private bool stopping;
        private bool registered;

        public ResourceSystem Resources { get; private set; }
        public bool OwnsResourceSystem { get; private set; }
        public ICoreResourceProvider CoreProvider { get; private set; }
        public IReadOnlyDictionary<string, ResourceAudioClipProvider> AudioProviders { get; private set; } = EmptyAudio;

        public ResourceStartup(ResourceConfiguration configuration, bool bindCore = false, IEnumerable<string> audioProjects = null)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.bindCore = bindCore;
            this.audioProjects = ResourceCatalog.Copy(audioProjects);
            foreach (var project in this.audioProjects) ResourceAddress.ValidateId(project, nameof(audioProjects));
            if (this.audioProjects.Distinct(StringComparer.Ordinal).Count() != this.audioProjects.Count)
                throw new ArgumentException("Audio projects must be distinct.", nameof(audioProjects));
        }

        /// <summary>同一入口的等待者独立取消；成功结果借读本入口，Dispose请求不等价Stop。配置冲突沿用ResourceSystem规则。</summary>
        public ResourceRequest<ResourceStartup> StartAsync()
        {
            ResourceSystem.CheckStartupThread();
            var request = new ResourceRequest<ResourceStartup>();
            if (stopping) return Failure(request, new ResourceException(ResourceError.Busy, "The startup is stopping."));
            if (registered)
            {
                if (!Resources.IsAvailable) return Failure(request, new ResourceException(ResourceError.SessionUnavailable, "The resource session ended."));
                request.SetSuccess(this);
                return request;
            }
            starting.Add(request);
            request.Attach(() => starting.Remove(request), null);
            if (startingResources) return request;
            startingResources = true;
            var initialized = ResourceSystem.InitializeStartup(configuration, out var created);
            if (initialized.IsDisposed)
            {
                CleanupFailedStart(initialized.Error ?? new ResourceException(ResourceError.SessionUnavailable, "The session ended before startup assembly."));
                return request;
            }
            initialized.Completed += done =>
            {
                ResourceException error = null;
                try
                {
                    if (done.Status != ResourceRequestStatus.Succeeded) error = done.Error;
                    else
                    {
                        var acquired = done.TakeResult();
                        OwnsResourceSystem = created || (ReferenceEquals(Resources, acquired) && OwnsResourceSystem);
                        Resources = acquired;
                        if (starting.Count == 0) error = new ResourceException(ResourceError.Canceled, "Every startup consumer canceled.");
                        else
                        {
                            var providers = audioProjects.ToDictionary(id => id, id => Resources.GetAudioClipProvider(id), StringComparer.Ordinal);
                            CoreProvider = Resources.RegisterStartup(this, bindCore);
                            registered = true;
                            AudioProviders = new ReadOnlyDictionary<string, ResourceAudioClipProvider>(providers);
                        }
                    }
                }
                catch (ResourceException failure) { error = failure; }
                catch (Exception failure) { error = new ResourceException(ResourceError.LoadFailed, "Resource startup assembly failed.", failure); }
                finally { done.Dispose(); }
                if (error == null) CompleteStart(null);
                else CleanupFailedStart(error);
            };
            return request;
        }

        private void CompleteStart(ResourceException error)
        {
            startingResources = false;
            var requests = starting.ToArray();
            starting.Clear();
            foreach (var request in requests)
                if (error == null) request.SetSuccess(this);
                else request.SetFailure(error.Kind == ResourceError.SessionUnavailable ? ResourceRequestStatus.Invalidated : ResourceRequestStatus.Failed, error);
            foreach (var request in requests) request.Notify();
        }

        private void CleanupFailedStart(ResourceException error)
        {
            if (registered) { Resources.UnregisterStartup(this); registered = false; }
            CoreProvider = null;
            AudioProviders = EmptyAudio;
            if (Resources == null || !OwnsResourceSystem) { ClearLocal(); CompleteStart(error); return; }
            if (Resources.HasOtherStartups(this))
            {
                CompleteStart(new ResourceException(error.Kind, "Startup assembly failed; its created resource session remains owned for StopAsync retry because another startup joined.", error));
                return;
            }
            var cleanup = Resources.ShutdownAsync();
            cleanup.Completed += done =>
            {
                var failure = done.Error;
                if (done.Status == ResourceRequestStatus.Succeeded) ClearLocal();
                done.Dispose();
                CompleteStart(failure == null ? error : new ResourceException(error.Kind,
                    "Startup assembly failed; created resources could not close. Release independent owners and retry StopAsync.", new AggregateException(error, failure)));
            };
        }

        /// <summary>借用会话只归还本入口的绑定；自建会话遇其他接入者或live owner返回Busy，保持可恢复，不关闭AudioKit或外部业务。</summary>
        /// <remarks>取消关闭请求只退出本次等待，已开始的native排空与入口责任收尾仍继续。</remarks>
        public ResourceRequest<bool> StopAsync()
        {
            ResourceSystem.CheckStartupThread();
            var request = new ResourceRequest<bool>();
            if (startingResources) return Failure(request, new ResourceException(ResourceError.Busy, "Startup initialization or failure cleanup is still in progress."));
            if (stopping) return Resources.ShutdownAsync();
            if (Resources == null || !OwnsResourceSystem || !Resources.IsSessionActive)
            {
                ClearLocal();
                request.SetSuccess(true);
                return request;
            }
            if (Resources.HasOtherStartups(this)) return Failure(request, new ResourceException(ResourceError.Busy, "Other application startups still borrow the resource session."));
            stopping = true;
            ResourceRequest<bool> shutdown;
            try { shutdown = Resources.ShutdownAsync(); }
            catch (Exception error)
            {
                stopping = false;
                return Failure(request, error as ResourceException ?? new ResourceException(ResourceError.LoadFailed, "Resource shutdown failed.", error));
            }
            shutdown.Completed += done =>
            {
                if (done.Status == ResourceRequestStatus.Succeeded) { ClearLocal(); request.SetSuccess(true); }
                else request.SetFailure(done.Status, done.Error);
                stopping = false;
                done.Dispose();
                request.Notify();
            };
            return request;
        }

        private void ClearLocal()
        {
            if (registered) Resources.UnregisterStartup(this);
            registered = false;
            Resources = null;
            OwnsResourceSystem = false;
            CoreProvider = null;
            AudioProviders = EmptyAudio;
        }

        private static ResourceRequest<T> Failure<T>(ResourceRequest<T> request, ResourceException error)
        {
            request.SetFailure(ResourceRequestStatus.Failed, error);
            return request;
        }
    }

    public sealed partial class ResourceSystem
    {
        private readonly HashSet<ResourceStartup> startups = new();
        private readonly HashSet<ResourceStartup> startupCoreUsers = new();
        private ICoreResourceProvider startupCoreBinding;
        private bool ownsStartupCoreBinding;

        internal static void CheckStartupThread() => AssertThread();

        internal static ResourceRequest<ResourceSystem> InitializeStartup(ResourceConfiguration config, out bool created)
        {
            created = authority == null;
            return InitializeAsync(config);
        }

        internal ICoreResourceProvider RegisterStartup(ResourceStartup startup, bool bindCore)
        {
            CheckThread();
            if (!IsAvailable) throw new ResourceException(ResourceError.SessionUnavailable, "The resource session ended during startup assembly.");
            if (bindCore)
            {
                var current = CoreResourceAccess.Provider;
                if (current != null && !ReferenceEquals(current, coreProvider))
                    throw new ResourceException(ResourceError.ConfigurationConflict, "A different Core resource provider is already bound; startup does not replace external bindings.");
                if (startupCoreUsers.Count == 0 || current == null)
                {
                    startupCoreBinding = current ?? BindCoreResourceProvider();
                    ownsStartupCoreBinding = current == null;
                }
                startupCoreUsers.Add(startup);
            }
            startups.Add(startup);
            return bindCore ? startupCoreBinding : null;
        }

        internal bool HasOtherStartups(ResourceStartup startup) => startups.Any(x => !ReferenceEquals(x, startup));

        internal void UnregisterStartup(ResourceStartup startup)
        {
            CheckThread();
            startups.Remove(startup);
            if (!startupCoreUsers.Remove(startup) || startupCoreUsers.Count != 0) return;
            if (ownsStartupCoreBinding) CoreResourceAccess.Unbind(startupCoreBinding);
            startupCoreBinding = null;
            ownsStartupCoreBinding = false;
        }
    }
}
