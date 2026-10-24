using System;
using System.Collections;
using UnityEngine;
using YLFramework.Audio;
using YLFramework.Resource;

namespace YLFramework.Samples.Resource
{
    /// <summary>显式调用Begin/Run，不在Awake/Start/属性读取时启动。Editor配套入口只负责配置选择，业务共用Run。</summary>
    public sealed class ResourceStartupExample : MonoBehaviour
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("Mode")]
        public ResourceLoadMode mode = ResourceLoadMode.EditorSimulation;
        [global::UnityEngine.Serialization.FormerlySerializedAs("ProjectId")]
        public string projectId;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Platform")]
        public string platform = "StandaloneWindows64";
        [global::UnityEngine.Serialization.FormerlySerializedAs("AppCompatibility")]
        public string appCompatibility = "startup-sample-v1";
        [global::UnityEngine.Serialization.FormerlySerializedAs("SnapshotDirectory")]
        public string snapshotDirectory;
        [global::UnityEngine.Serialization.FormerlySerializedAs("CatalogDigest")]
        public string catalogDigest;
        [global::UnityEngine.Serialization.FormerlySerializedAs("UseBuiltIn")]
        public bool useBuiltIn;
        [global::UnityEngine.Serialization.FormerlySerializedAs("BindCore")]
        public bool bindCore = true;
        [global::UnityEngine.Serialization.FormerlySerializedAs("UseAudio")]
        public bool useAudio = true;

        /// <summary>应用在运行前注入并拥有其生命周期；不保存到Scene，生产密钥来源由应用提供。</summary>
        [NonSerialized] public IResourceContentKeyProvider ContentKeyProvider;

        public ResourceStartup Startup
        {
            get; private set;
        }
        public string Result { get; private set; } = "Not started";
        private Coroutine _running;
        private ResourceRequest<ResourceStartup> _initializing;
        private ResourceScope _scope;
        private bool _isClosing;
        private bool _isBusinessCompleted;
        private string _message;

        public ResourceProjectMount CreateMount() => new(projectId, platform, appCompatibility,
            useBuiltIn ? ResourceLocalSnapshot.OpenBuiltIn(projectId, catalogDigest, platform, appCompatibility) :
                ResourceLocalSnapshot.Open(snapshotDirectory, projectId, catalogDigest, platform, appCompatibility),
            new[] { typeof(ResourceStartupExampleData) });

        public ResourceConfiguration CreateBundleConfiguration() => ResourceConfiguration.Create(mode, new[] { CreateMount() },
            decryption: ContentKeyProvider == null ? null : new ResourceDecryptionOptions(ContentKeyProvider));

        /// <summary>Player显式入口；模拟模式在此明确拒绝，不因为Bundle错误切换来源。</summary>
        [ContextMenu("Run Bundles Example")]
        public void BeginBundles()
        {
            if (mode != ResourceLoadMode.Bundles)
            {
                throw new InvalidOperationException("Player startup requires Bundles mode; use the Editor sample inspector for simulation.");
            }

            Begin(CreateBundleConfiguration());
        }

        public void Begin(ResourceConfiguration configuration)
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Run the example in Play Mode.");
            }

            if (_running != null || _isClosing || Startup?.Resources != null)
            {
                throw new InvalidOperationException("Stop the previous startup before running again.");
            }

            _running = StartCoroutine(Run(configuration));
        }

        /// <summary>同一业务消费Text、自定义Data及可选Core/Audio；示例只归还自己的Scope、请求和播放。</summary>
        public IEnumerator Run(ResourceConfiguration configuration)
        {
            if (_isClosing || Startup?.Resources != null)
            {
                throw new InvalidOperationException("The previous startup still owns resources.");
            }

            var projectId = this.projectId;
            var useCore = bindCore;
            var useAudio = this.useAudio;
            _isBusinessCompleted = false;
            Result = "Running";
            Startup = new ResourceStartup(configuration, useCore, useAudio ? new[] { projectId } : null);
            try
            {
                _initializing = Startup.StartAsync();
                yield return _initializing;
                _initializing.TakeResult();
                _initializing.Dispose();
                _initializing = null;
                _scope = Startup.Resources.CreateScope();
                var data = _scope.AcquireAsync<ResourceStartupExampleData>(new ResourceAddress(projectId, "data"));
                yield return data;
                var caption = _scope.TakeResult(data).Asset.text;
                if (useCore)
                {
                    var text = _scope.Own(Startup.CoreProvider.ReadTextAsync(projectId, "message"));
                    yield return text;
                    _message = text.TakeResult() + " / " + caption;
                }
                else
                {
                    var text = _scope.AcquireAsync<TextAsset>(new ResourceAddress(projectId, "message"));
                    yield return text;
                    _message = _scope.TakeResult(text).Asset.text + " / " + caption;
                }
                if (useAudio)
                {
                    var sound = _scope.Own(AudioKit.PlaySoundAsync(new AudioClipAddress(Startup.AudioProviders[projectId], "tone")));
                    var until = Time.realtimeSinceStartup + 10;
                    while (!sound.HasStarted && !sound.IsDone && Time.realtimeSinceStartup < until)
                    {
                        yield return null;
                    }

                    if (!sound.HasStarted)
                    {
                        throw sound.Error ?? new InvalidOperationException(
                        "The sample Audio request did not start: " + sound.EndReason +
                        ". Check application Sound settings/admission, or disable UseAudio; this example does not override shared Audio preferences.");
                    }
                }
                _isBusinessCompleted = true;
            }
            finally
            {
                CloseOwnedResponsibilities();
            }
            while (_isClosing)
            {
                yield return null;
            }
        }

        [ContextMenu("Stop Or Retry Example Shutdown")]
        public void StopExample()
        {
            if (_running != null)
            {
                var current = _running;
                _running = null;
                StopCoroutine(current);
            }
            // Unity停协程不保证执行iterator finally；字段中的Scope仍由显式关闭接点结算。
            CloseOwnedResponsibilities();
        }

        private void CloseOwnedResponsibilities()
        {
            _running = null;
            _initializing?.Dispose();
            _initializing = null;
            try
            {
                _scope?.Dispose();
            }
            finally
            {
                _scope = null;
                if (!_isClosing && Startup != null)
                {
                    _isClosing = true;
                    var shutdown = Startup.StopAsync();
                    shutdown.Completed += done =>
                    {
                        _isClosing = false;
                        Result = done.Status == ResourceRequestStatus.Succeeded
                            ? _isBusinessCompleted ? "Passed: " + _message : "Stopped or failed; inspect the original error."
                            : "Shutdown requires retry: " + done.Error;
                        done.Dispose();
                    };
                }
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                StopExample();
            }
        }
    }
}
