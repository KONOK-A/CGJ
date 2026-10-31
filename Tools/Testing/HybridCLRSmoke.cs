using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using YLFramework.Core;
using YLFramework.Integrations.HybridCLR;
using YLFramework.Resource;
using YLFramework.Samples.HybridCLR;
using Debug = UnityEngine.Debug;

namespace YLFramework.Testing
{
    /// <summary>产品根外的显式原生消费者，不静态引用热更类型，不采用 Editor 模拟。</summary>
    public sealed class HybridCLRSmoke : MonoBehaviour
    {
        public string resultId;
        public string hostId;
        private readonly List<string> _stages = new();
        private string _output;
        private string _runtimeError = string.Empty;
        private HybridCLRExample _sample;
        private ResourceSystem _resources;
        private Detail _detail;
        private int _firstFrame;

        private void Awake()
        {
            _firstFrame = Time.frameCount;
            Application.runInBackground = true;
            Application.logMessageReceived += ObserveLog;
        }

        private void OnDestroy() => Application.logMessageReceived -= ObserveLog;

        private void ObserveLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
            {
                _runtimeError += message + Environment.NewLine;
            }
        }

        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            Exception failure = null;
            while (stack.Count != 0 && failure == null)
            {
                bool next = false;
                object current = null;
                try
                {
                    next = stack.Peek().MoveNext();
                    if (next)
                    {
                        current = stack.Peek().Current;
                    }
                }
                catch (Exception error)
                {
                    failure = error;
                }
                if (failure != null)
                {
                    break;
                }
                if (!next)
                {
                    ((IDisposable)stack.Pop()).Dispose();
                }
                else if (current is IEnumerator nested && !(current is CustomYieldInstruction))
                {
                    stack.Push(nested);
                }
                else
                {
                    yield return current;
                }
            }
            foreach (var remaining in stack)
            {
                try
                {
                    ((IDisposable)remaining).Dispose();
                }
                catch (Exception error)
                {
                    failure ??= error;
                    Debug.LogException(error);
                }
            }
            if (failure == null && _runtimeError.Length != 0)
            {
                failure = new InvalidOperationException(_runtimeError);
            }
            if (failure != null)
            {
                Debug.LogException(failure);
            }
            try
            {
                if (_output != null)
                {
                    var diagnostics = _resources?.GetDiagnosticsSnapshot();
                    var report = new Report
                    {
                        resultId = resultId,
                        hostId = hostId,
                        status = failure == null ? "Passed" : "Failed",
                        error = failure == null ? string.Empty : failure.ToString() + _runtimeError,
                        unityVersion = Application.unityVersion,
                        pid = Process.GetCurrentProcess().Id,
                        isEditor = Application.isEditor,
                        platform = Application.platform.ToString(),
                        graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                        frames = Time.frameCount - _firstFrame,
                        recordCount = diagnostics?.RecordCount ?? -1,
                        leaseCount = diagnostics?.LeaseCount ?? -1,
                        stages = _stages.ToArray(),
                        hybridClr = _detail
                    };
                    using var stream = new FileStream(Path.Combine(_output, "result.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                    writer.Write(JsonUtility.ToJson(report, true));
                }
            }
            catch (Exception error)
            {
                failure ??= error;
                Debug.LogException(error);
            }
            if (failure == null)
            {
                Debug.Log("[YLPlayerSmoke] PASS " + resultId);
            }
            Application.Quit(failure == null ? 0 : 1);
        }

        private IEnumerator Run()
        {
#if !UNITY_EDITOR && ENABLE_IL2CPP
            bool native = true;
#else
            bool native = false;
#endif
            Check(native && Application.isPlaying && Application.platform == RuntimePlatform.WindowsPlayer, "A real Windows64 IL2CPP Player is required.");
            var output = Path.GetFullPath(Argument("-ylSmokeOutput"));
            Check(Directory.Exists(output), "An existing owned output is required.");
            CheckNoLinks(output);
            _output = output;
            var receiptPath = Path.GetFullPath(Argument("-ylHybridCLRReceipt"));
            CheckNoLinks(receiptPath);
            Check(receiptPath.StartsWith(Path.GetDirectoryName(output) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                "The runtime receipt must belong to this result root.");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
            Check(receipt.resultId == resultId && receipt.hostId == hostId && receipt.unityVersion == Application.unityVersion,
                "A foreign native build receipt is refused.");
            CheckNoLinks(receipt.cacheRoot);
            var parent = Path.GetDirectoryName(Path.GetFullPath(receipt.cacheRoot));
            var owner = JsonUtility.FromJson<CacheOwner>(File.ReadAllText(Path.Combine(parent, ".yl-owner.json")));
            Check(owner.purpose == "YLTesting.Directory" && owner.kind == "Cache" && owner.id == receipt.cacheOwnerId &&
                string.Equals(owner.path, parent, StringComparison.OrdinalIgnoreCase), "An explicit leased runtime cache owner is required.");
            Check(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && ResourceSystem.Current == null,
                "A fresh native process and real graphics device are required for the Bundle sample.");
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(assembly => receipt.assemblies.Contains(assembly.GetName().Name)),
                "Hot assemblies must not be statically linked or preloaded.");
            _stages.Add("Started");
            var snapshot = ResourceLocalSnapshot.OpenBuiltIn(receipt.projectId, receipt.catalogDigest, "StandaloneWindows64", receipt.compatibilityId);
            var store = new ArtifactStore(receipt.cacheRoot, new[]
            {
                new ResourceProjectMount(receipt.projectId, "StandaloneWindows64", receipt.compatibilityId, snapshot)
            });
            _sample = new HybridCLRExample();
            yield return _sample.Run(new ResourceConfiguration(store), receipt.projectId, receipt.compatibilityId);
            _resources = _sample.Startup.Resources;
            var code = _sample.ModuleRequest.Code;
            Check(_sample.ModuleRequest.Exception == null && code.Mode == HybridCLRLoadMode.Native && !code.IsSimulation &&
                code.CodePackageId == receipt.codePackageId, "Real native code loading did not complete.");
            HybridCLRManifest manifest;
            using (var request = _sample.Startup.Resources.AcquireAsync<TextAsset>(new ResourceAddress(receipt.projectId, HybridCLRManifest.ManifestPath)))
            {
                yield return request;
                using var lease = request.TakeResult();
                manifest = HybridCLRManifest.Parse(lease.Asset.text);
            }
            Check(manifest.CodePackageId == receipt.codePackageId && manifest.AotMetadata.Length > 0 &&
                manifest.AotMetadata.Select(payload => payload.Name).SequenceEqual(receipt.metadataAssemblies), "The loaded metadata manifest differs from the final base.");
            _stages.Add("HybridCLRMetadata");
            _detail = new Detail
            {
                mode = code.Mode.ToString(),
                isSimulation = code.IsSimulation,
                codePackageId = code.CodePackageId,
                nativeVariant = receipt.nativeVariant,
                metadataAssemblies = manifest.AotMetadata.Select(payload => payload.Name).ToArray(),
                assemblies = code.Assemblies.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                message = HybridCLRExampleState.Message,
                asyncResult = HybridCLRExampleState.Async.GetAwaiter().GetResult(),
                prefabMarker = _sample.PrefabMarker
            };
            Check(_sample.ModuleRequest.Module.State == ModuleState.Running && _detail.message == "v1: YL Resource configuration; dependency-count=2" &&
                _detail.asyncResult == "v1-async" && _detail.prefabMarker == "v1-prefab", "The real hot Module, async or prefab did not execute.");
            _stages.Add("HybridCLRModule");
            var support = code.Assemblies["YLFramework.HybridCLR.Sample.Support"].GetType("YLFramework.Samples.HybridCLR.HotUpdate.VersionInfo", true);
            _detail.reflectionCount = (int)support.GetMethod("Count").MakeGenericMethod(typeof(string)).Invoke(null,
                new object[] { new List<string> { "a", "b", "c" } });
            Check(_detail.reflectionCount == 3, "The interpreted generic method was not callable by reflection.");
            _stages.Add("HybridCLRReflection");
            var repeated = _sample.Loader.LoadAsync(new ResourceAddress(receipt.projectId, HybridCLRManifest.ManifestPath));
            yield return repeated;
            _detail.repeatedLoadReused = repeated.Exception == null && ReferenceEquals(repeated.Result, code);
            using (var wrongBase = new HybridCLRLoader(_sample.Startup.Resources, "wrong-base"))
            {
                var rejected = wrongBase.LoadAsync(new ResourceAddress(receipt.projectId, HybridCLRManifest.ManifestPath));
                yield return rejected;
                _detail.wrongBaseRejected = rejected.Exception != null && rejected.Stage == "Manifest" && !rejected.RequiresRestart;
            }
            Check(_detail.repeatedLoadReused && _detail.wrongBaseRejected, "Code identity reuse or wrong-base protection did not hold.");
            using var shutdown = _sample.CloseAsync();
            yield return shutdown;
            Check(shutdown.TakeResult() && ResourceSystem.Current == null, "The owned native sample did not close.");
            var final = _resources.GetDiagnosticsSnapshot();
            Check(final.RecordCount == 0 && final.LeaseCount == 0 && _runtimeError.Length == 0, "Native resources or runtime errors remain.");
            _stages.Add("HybridCLRShutdown");
        }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            Check(index >= 0 && index + 1 < args.Length && index == Array.LastIndexOf(args, name), "One explicit " + name + " is required.");
            return args[index + 1];
        }

        private static void CheckNoLinks(string path)
        {
            for (var cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
            {
                Check(!File.Exists(cursor) && !Directory.Exists(cursor) || (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) == 0,
                    "Linked runtime paths are refused.");
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        [Serializable]
        private sealed class CacheOwner
        {
            public string purpose;
            public string kind;
            public string id;
            public string path;
        }

        [Serializable]
        private sealed class Receipt
        {
            public string resultId;
            public string hostId;
            public string unityVersion;
            public string projectId;
            public string compatibilityId;
            public string catalogDigest;
            public string codePackageId;
            public string nativeVariant;
            public string cacheRoot;
            public string cacheOwnerId;
            public string[] metadataAssemblies;
            public string[] assemblies;
        }

        [Serializable]
        private sealed class Detail
        {
            public string mode;
            public bool isSimulation;
            public string codePackageId;
            public string nativeVariant;
            public string[] metadataAssemblies;
            public string[] assemblies;
            public string message;
            public string asyncResult;
            public string prefabMarker;
            public int reflectionCount;
            public bool repeatedLoadReused;
            public bool wrongBaseRejected;
        }

        [Serializable]
        private sealed class Report
        {
            public int schemaVersion = 1;
            public string purpose = "YLTesting.PlayerSmoke";
#if ENABLE_IL2CPP
            public string backend = "IL2CPP";
#else
            public string backend = "Mono";
#endif
            public string resultId;
            public string hostId;
            public string status;
            public string error;
            public string unityVersion;
            public int pid;
            public bool isEditor;
            public string platform;
            public string graphicsDevice;
            public int frames;
            public int recordCount;
            public int leaseCount;
            public string[] stages;
            public Detail hybridClr;
        }
    }
}
