using System;
using System.Collections;
using UnityEngine;
using YLFramework.Services.Audio;
using YLFramework.Services.Resource;

namespace YLFramework.Samples.Resource
{
    /// <summary>显式调用Begin/Run，不在Awake/Start/属性读取时启动。Editor配套入口只负责配置选择，业务共用Run。</summary>
    public sealed class ResourceStartupExample : MonoBehaviour
    {
        public ResourceLoadMode Mode = ResourceLoadMode.EditorSimulation;
        public string ProjectId;
        public string Platform = "StandaloneWindows64";
        public string AppCompatibility = "startup-sample-v1";
        public string SnapshotDirectory;
        public string CatalogDigest;
        public bool UseBuiltIn;
        public bool BindCore = true;
        public bool UseAudio = true;

        /// <summary>应用在运行前注入并拥有其生命周期；不保存到Scene，生产密钥来源由应用提供。</summary>
        [NonSerialized] public IResourceContentKeyProvider ContentKeyProvider;

        public ResourceStartup Startup { get; private set; }
        public string Result { get; private set; } = "Not started";
        private Coroutine running;
        private ResourceRequest<ResourceStartup> initializing;
        private ResourceScope scope;
        private bool closing;
        private bool businessCompleted;
        private string message;

        public ResourceProjectMount CreateMount() => new(ProjectId, Platform, AppCompatibility,
            UseBuiltIn ? ResourceLocalSnapshot.OpenBuiltIn(ProjectId, CatalogDigest, Platform, AppCompatibility) :
                ResourceLocalSnapshot.Open(SnapshotDirectory, ProjectId, CatalogDigest, Platform, AppCompatibility),
            new[] { typeof(ResourceStartupExampleData) });

        public ResourceConfiguration CreateBundleConfiguration() => ResourceConfiguration.Create(Mode, new[] { CreateMount() },
            decryption: ContentKeyProvider == null ? null : new ResourceDecryptionOptions(ContentKeyProvider));

        /// <summary>Player显式入口；模拟模式在此明确拒绝，不因为Bundle错误切换来源。</summary>
        [ContextMenu("Run Bundles Example")]
        public void BeginBundles()
        {
            if (Mode != ResourceLoadMode.Bundles) throw new InvalidOperationException("Player startup requires Bundles mode; use the Editor sample inspector for simulation.");
            Begin(CreateBundleConfiguration());
        }

        public void Begin(ResourceConfiguration configuration)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run the example in Play Mode.");
            if (running != null || closing || Startup?.Resources != null) throw new InvalidOperationException("Stop the previous startup before running again.");
            running = StartCoroutine(Run(configuration));
        }

        /// <summary>同一业务消费Text、自定义Data及可选Core/Audio；示例只归还自己的Scope、请求和播放。</summary>
        public IEnumerator Run(ResourceConfiguration configuration)
        {
            if (closing || Startup?.Resources != null) throw new InvalidOperationException("The previous startup still owns resources.");
            var projectId = ProjectId;
            var useCore = BindCore;
            var useAudio = UseAudio;
            businessCompleted = false;
            Result = "Running";
            Startup = new ResourceStartup(configuration, useCore, useAudio ? new[] { projectId } : null);
            try
            {
                initializing = Startup.StartAsync();
                yield return initializing;
                initializing.TakeResult();
                initializing.Dispose();
                initializing = null;
                scope = Startup.Resources.CreateScope();
                var data = scope.AcquireAsync<ResourceStartupExampleData>(new ResourceAddress(projectId, "data"));
                yield return data;
                var caption = scope.TakeResult(data).Asset.Text;
                if (useCore)
                {
                    var text = scope.Own(Startup.CoreProvider.ReadTextAsync(projectId, "message"));
                    yield return text;
                    message = text.TakeResult() + " / " + caption;
                }
                else
                {
                    var text = scope.AcquireAsync<TextAsset>(new ResourceAddress(projectId, "message"));
                    yield return text;
                    message = scope.TakeResult(text).Asset.text + " / " + caption;
                }
                if (useAudio)
                {
                    var sound = scope.Own(AudioKit.PlaySoundAsync(new AudioClipAddress(Startup.AudioProviders[projectId], "tone")));
                    var until = Time.realtimeSinceStartup + 10;
                    while (!sound.HasStarted && !sound.IsDone && Time.realtimeSinceStartup < until) yield return null;
                    if (!sound.HasStarted) throw sound.Error ?? new InvalidOperationException(
                        "The sample Audio request did not start: " + sound.EndReason +
                        ". Check application Sound settings/admission, or disable UseAudio; this example does not override shared Audio preferences.");
                }
                businessCompleted = true;
            }
            finally { CloseOwnedResponsibilities(); }
            while (closing) yield return null;
        }

        [ContextMenu("Stop Or Retry Example Shutdown")]
        public void StopExample()
        {
            if (running != null) { var current = running; running = null; StopCoroutine(current); }
            // Unity停协程不保证执行iterator finally；字段中的Scope仍由显式关闭接点结算。
            CloseOwnedResponsibilities();
        }

        private void CloseOwnedResponsibilities()
        {
            running = null;
            initializing?.Dispose();
            initializing = null;
            try { scope?.Dispose(); }
            finally
            {
                scope = null;
                if (!closing && Startup != null)
                {
                    closing = true;
                    var shutdown = Startup.StopAsync();
                    shutdown.Completed += done =>
                    {
                        closing = false;
                        Result = done.Status == ResourceRequestStatus.Succeeded
                            ? businessCompleted ? "Passed: " + message : "Stopped or failed; inspect the original error."
                            : "Shutdown requires retry: " + done.Error;
                        done.Dispose();
                    };
                }
            }
        }

        private void OnDisable() { if (Application.isPlaying) StopExample(); }
    }
}
