using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using YLFramework.Core;
using YLFramework.Foundation;
using YLFramework.Services.Resource;
using Module = YLFramework.Core.Module;

namespace YLFramework.Integrations.HybridCLR
{
    internal sealed class HybridCLRRuntimeCalls
    {
        internal bool Simulation;
        internal Func<byte[], int> LoadMetadata;
        internal Func<byte[], Assembly> LoadAssembly;
        internal Func<Assembly[]> LoadedAssemblies;
    }

    internal sealed class HybridCLRProcessState
    {
        internal sealed class Loaded
        {
            internal string Identity, Hash, Mvid;
            internal bool Metadata;
            internal Assembly Assembly;
        }
        internal readonly Dictionary<string, Loaded> Assemblies = new Dictionary<string, Loaded>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, string> Projects = new Dictionary<string, string>(StringComparer.Ordinal);
        internal object ActiveJob;
        internal bool RequiresRestart;
    }

    public sealed class HybridCLRLoader : IDisposable
    {
        private static HybridCLRProcessState processState = new HybridCLRProcessState();
        private static int mainThread;
        private static int generation;
        private static bool executionStopping;
        private static readonly HashSet<HybridCLRLoader> pendingLoaders = new HashSet<HybridCLRLoader>();
        private readonly ResourceSystem resources;
        private readonly string compatibilityId;
        private readonly HybridCLRRuntimeCalls calls;
        private readonly HybridCLRProcessState process;
        private readonly int session;
        private readonly Dictionary<ResourceAddress, Session> sessions = new Dictionary<ResourceAddress, Session>();
        private readonly Dictionary<(ResourceAddress, string, string), HybridCLRModuleRequest> moduleRequests = new Dictionary<(ResourceAddress, string, string), HybridCLRModuleRequest>();
#if UNITY_EDITOR
        private readonly Dictionary<(ResourceAddress, string, string), HybridCLRModuleRequest> editorModuleRequests = new Dictionary<(ResourceAddress, string, string), HybridCLRModuleRequest>();
#endif
        private readonly HashSet<Module> modules = new HashSet<Module>();
        private Job active;
        private bool disposed;
        private bool sessionEnded;

        private sealed class Session
        {
            internal AssetLease<TextAsset> Manifest;
            internal HybridCLRLoadResult Result;
        }

        private sealed class Job
        {
            internal ResourceAddress Address;
            internal HybridCLRLoadRequest Request;
            internal ResourceRequest<AssetLease<TextAsset>> Pending;
            internal AssetLease<TextAsset> ManifestLease;
            internal HybridCLRManifest Manifest;
            internal HybridCLRPayload[] Payloads;
            internal readonly Dictionary<string, byte[]> Bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            internal int Index;
            internal bool Irreversible;
            internal bool Restart;
            internal Coroutine ProgressRoutine;
        }

        private IEnumerable<HybridCLRModuleRequest> PendingModuleRequests =>
#if UNITY_EDITOR
            moduleRequests.Values.Concat(editorModuleRequests.Values);
#else
            moduleRequests.Values;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginSession()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            InvalidatePendingSessions();
            generation++;
            executionStopping = false;
            Application.quitting -= InvalidatePendingSessions;
            Application.quitting += InvalidatePendingSessions;
#if UNITY_EDITOR
            // Editor 仅模拟；重建会话引用，不声称卸载 AppDomain 中的程序集。
            processState = new HybridCLRProcessState();
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeEditorThread()
        {
            mainThread = Thread.CurrentThread.ManagedThreadId;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode || state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                InvalidatePendingSessions();
        }
#endif

        private static void InvalidatePendingSessions()
        {
            executionStopping = true;
            var pending = pendingLoaders.ToArray();
            foreach (var loader in pending) loader.sessionEnded = true;
            foreach (var loader in pending)
            {
                var error = new InvalidOperationException("The accepted HybridCLR request's Play/Resource session ended before completion.");
                if (loader.active != null) loader.Finish(loader.active, null, error);
                foreach (var request in loader.PendingModuleRequests.ToArray())
                {
                    request.Stage = "SessionUnavailable";
                    request.Invalidate?.Invoke(error);
                }
                foreach (var owned in loader.sessions.Values) owned.Manifest.Dispose();
                loader.sessions.Clear(); loader.modules.Clear();
                pendingLoaders.Remove(loader);
            }
        }

        private void UpdatePendingRegistration()
        {
            if (active == null && !PendingModuleRequests.Any()) pendingLoaders.Remove(this);
            else pendingLoaders.Add(this);
        }

        public HybridCLRLoader(ResourceSystem resources, string appContentCompatibilityId)
            : this(resources, appContentCompatibilityId, DefaultCalls(), processState) { }

        internal HybridCLRLoader(ResourceSystem resources, string appContentCompatibilityId, HybridCLRRuntimeCalls calls, HybridCLRProcessState process)
        {
            AssertThread();
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            _ = new ResourceAddress(appContentCompatibilityId, "compatibility");
            if (executionStopping || !resources.IsAvailable) throw new InvalidOperationException("Start the ResourceSystem in an active session before creating a HybridCLR loader.");
            this.resources = resources; compatibilityId = appContentCompatibilityId;
            this.calls = calls ?? throw new ArgumentNullException(nameof(calls));
            this.process = process ?? throw new ArgumentNullException(nameof(process));
            if (calls.LoadAssembly == null || calls.LoadMetadata == null || calls.LoadedAssemblies == null) throw new ArgumentException("Incomplete runtime call set.", nameof(calls));
            session = generation;
        }

        private static HybridCLRRuntimeCalls DefaultCalls()
        {
            AssertThread();
#if UNITY_EDITOR
            const bool simulation = true;
#elif ENABLE_IL2CPP && UNITY_STANDALONE_WIN
            const bool simulation = false;
#else
            throw new PlatformNotSupportedException("Native HybridCLR loading is supported only by the validated Windows64 IL2CPP base; Editor is Simulation only.");
#endif
#if UNITY_EDITOR || (ENABLE_IL2CPP && UNITY_STANDALONE_WIN)
            return new HybridCLRRuntimeCalls
            {
                Simulation = simulation, LoadAssembly = Assembly.Load,
                LoadMetadata = bytes => (int)global::HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(bytes, global::HybridCLR.HomologousImageMode.SuperSet),
                LoadedAssemblies = () => AppDomain.CurrentDomain.GetAssemblies()
            };
#endif
        }

        public HybridCLRLoadRequest LoadAsync(ResourceAddress manifestAddress)
        {
            RequireAvailable(manifestAddress);
            if (active != null && active.Address == manifestAddress) return active.Request;
            var request = new HybridCLRLoadRequest { ProjectId = manifestAddress.ProjectId };
            if (process.RequiresRestart)
            {
                request.Finish(null, new InvalidOperationException("An earlier irreversible native load failed. Restart this process before loading code."), true); return request;
            }
            if (sessions.TryGetValue(manifestAddress, out var previous))
            {
                if (!previous.Manifest.IsValid) request.Finish(null, new InvalidOperationException("The Resource session owning this code is no longer available."));
                else { request.Stage = "Complete"; request.Finish(previous.Result); }
                return request;
            }
            if (process.ActiveJob != null)
            {
                request.Stage = "Busy"; request.Finish(null, new InvalidOperationException("Another HybridCLR code load is in progress. Retry after it completes.")); return request;
            }
            var job = new Job { Address = manifestAddress, Request = request };
            active = job; process.ActiveJob = job;
            UpdatePendingRegistration();
            try
            {
                var pending = resources.AcquireAsync<TextAsset>(manifestAddress);
                job.Pending = pending;
                pending.Completed += r => ReadManifest(job, r);
                if (!request.isDone)
                {
                    var routine = CoroutineStarter.Start(ObserveLoad(job));
                    if (request.isDone) CoroutineStarter.Stop(routine);
                    else if (routine == null) Finish(job, null, new InvalidOperationException("Code loading requires an active coroutine host."));
                    else job.ProgressRoutine = routine;
                }
            }
            catch (Exception exception) { Finish(job, null, exception); }
            return request;
        }

        private IEnumerator ObserveLoad(Job job)
        {
            while (!job.Request.isDone)
            {
                try
                {
                    RequireAvailable(job.Address);
                    var pending = job.Pending?.Progress ?? 0;
                    job.Request.Progress(job.Payloads == null ? 0.1f * pending :
                        0.1f + 0.6f * (job.Index + pending) / job.Payloads.Length);
                }
                catch (Exception error) { Finish(job, null, error); }
                if (job.Request.isDone) yield break;
                yield return null;
            }
        }

        private void ReadManifest(Job job, ResourceRequest<AssetLease<TextAsset>> pending)
        {
            if (!ReferenceEquals(job.Pending, pending) || job.Request.isDone) return;
            job.Pending = null;
            try
            {
                if (pending.Status != ResourceRequestStatus.Succeeded) throw (Exception)pending.Error ?? new InvalidOperationException("Manifest resource request did not succeed.");
                job.ManifestLease = pending.TakeResult();
                var bytes = job.ManifestLease.Asset.bytes;
                if (bytes.Length > HybridCLRManifest.MaximumManifestBytes) throw new InvalidDataException("Manifest byte budget exceeded.");
                job.Manifest = HybridCLRManifest.Parse(new UTF8Encoding(false, true).GetString(bytes));
                job.Manifest.Validate(job.Address.ProjectId, compatibilityId, Application.unityVersion, Debug.isDebugBuild);
                job.Payloads = job.Manifest.AotMetadata.Concat(job.Manifest.HotUpdateAssemblies).ToArray();
                pending.Dispose();
                job.Request.Stage = "Payloads";
                job.Request.Progress(0.1f);
                Pump(job);
            }
            catch (Exception exception) { pending.Dispose(); Finish(job, null, exception); }
        }

        private void Pump(Job job)
        {
            try
            {
                RequireAvailable(job.Address);
                while (job.Index < job.Payloads.Length)
                {
                    var payload = job.Payloads[job.Index]; job.Request.AssemblyName = payload.Name;
                    job.Request.Progress(0.1f + 0.6f * job.Index / job.Payloads.Length);
                    var pending = resources.AcquireAsync<TextAsset>(new ResourceAddress(job.Manifest.ProjectId, payload.Address));
                    job.Pending = pending;
                    if (pending.IsDone) { ReadPayload(job, pending); continue; }
                    pending.Completed += r =>
                    {
                        if (!ReferenceEquals(job.Pending, r) || job.Request.isDone) return;
                        try { ReadPayload(job, r); Pump(job); }
                        catch (Exception exception) { Finish(job, null, exception); }
                    };
                    return;
                }
                LoadValidated(job);
            }
            catch (Exception exception) { Finish(job, null, exception); }
        }

        private static void ReadPayload(Job job, ResourceRequest<AssetLease<TextAsset>> pending)
        {
            job.Pending = null;
            using (pending)
            {
                if (pending.Status != ResourceRequestStatus.Succeeded) throw (Exception)pending.Error ?? new InvalidOperationException("Code payload resource request did not succeed.");
                using var lease = pending.TakeResult();
                var bytes = lease.Asset.bytes;
                var payload = job.Payloads[job.Index];
                HybridCLRManifest.ValidatePayload(payload, bytes);
                if (HybridCLRMetadata.ReadMvid(bytes) != Guid.ParseExact(payload.Mvid, "D"))
                    throw new InvalidDataException("Payload PE metadata MVID does not match the checked descriptor: " + payload.Name);
                job.Bytes.Add(payload.Name, bytes);
                job.Index++;
                job.Request.Progress(0.1f + 0.6f * job.Index / job.Payloads.Length);
            }
        }

        private void LoadValidated(Job job)
        {
            RequireAvailable(job.Address);
            var manifest = job.Manifest;
            job.Request.Stage = calls.Simulation ? "Simulation" : "NativePreflight";
            job.Request.Progress(0.7f);
            if (process.Projects.TryGetValue(manifest.ProjectId, out var existingPackage) && existingPackage != manifest.CodePackageId)
            { job.Restart = true; throw new InvalidOperationException("Different code bytes/descriptors are already registered for this project. Restart to change code packages."); }
            var domain = calls.LoadedAssemblies();
            foreach (var payload in job.Payloads)
            {
                if (process.Assemblies.TryGetValue(payload.Name, out var record))
                {
                    if (record.Identity != payload.Identity || record.Hash != payload.Sha256 || record.Mvid != payload.Mvid ||
                        record.Metadata != manifest.AotMetadata.Contains(payload) || !record.Metadata && record.Assembly == null)
                    { job.Restart = true; throw new InvalidOperationException("A different or incomplete assembly is already registered: " + payload.Name); }
                }
                else if (!calls.Simulation && manifest.HotUpdateAssemblies.Contains(payload) && domain.Any(a => string.Equals(a.GetName().Name, payload.Name, StringComparison.OrdinalIgnoreCase)))
                { job.Restart = true; throw new InvalidOperationException("An untracked hot-update assembly was already loaded by another owner: " + payload.Name); }
            }
            var loaded = new Dictionary<string, Assembly>(StringComparer.Ordinal);
            if (calls.Simulation)
            {
                foreach (var payload in manifest.HotUpdateAssemblies)
                {
                    var matches = domain.Where(a => HybridCLRManifest.CanonicalIdentity(a.GetName()) == payload.Identity).ToArray();
                    if (matches.Length != 1) throw new NotSupportedException("Editor Simulation requires exactly one already-loaded assembly; external-only DLLs need native Player validation: " + payload.Name);
                    loaded.Add(payload.Name, matches[0]);
                    job.Request.AssemblyName = payload.Name;
                    job.Request.Progress(0.7f + 0.3f * loaded.Count / manifest.HotUpdateAssemblies.Length);
                }
            }
            else
            {
                var completedCount = 0;
                foreach (var payload in manifest.AotMetadata)
                {
                    job.Request.Stage = "Metadata"; job.Request.AssemblyName = payload.Name;
                    job.Request.Progress(0.7f + 0.3f * completedCount / job.Payloads.Length);
                    if (!process.Assemblies.ContainsKey(payload.Name))
                    {
                        job.Irreversible = true;
                        var code = calls.LoadMetadata(job.Bytes[payload.Name]);
                        if (code != (int)global::HybridCLR.LoadImageErrorCode.OK)
                        {
                            job.Request.NativeErrorCode = code;
                            throw new InvalidOperationException("LoadMetadataForAOTAssembly failed for " + payload.Name + ": " + code + ". Unknown-owner already-loaded codes are not success.");
                        }
                        process.Assemblies.Add(payload.Name, Record(payload, true, null));
                    }
                    job.Request.Progress(0.7f + 0.3f * ++completedCount / job.Payloads.Length);
                }
                foreach (var payload in manifest.HotUpdateAssemblies)
                {
                    job.Request.Stage = "AssemblyLoad"; job.Request.AssemblyName = payload.Name;
                    job.Request.Progress(0.7f + 0.3f * completedCount / job.Payloads.Length);
                    if (process.Assemblies.TryGetValue(payload.Name, out var existing)) loaded.Add(payload.Name, existing.Assembly);
                    else
                    {
                        job.Irreversible = true;
                        var assembly = calls.LoadAssembly(job.Bytes[payload.Name]);
                        if (assembly == null || HybridCLRManifest.CanonicalIdentity(assembly.GetName()) != payload.Identity)
                            throw new InvalidDataException("Loaded assembly identity did not match its checked PE/manifest: " + payload.Name);
                        process.Assemblies.Add(payload.Name, Record(payload, false, assembly)); loaded.Add(payload.Name, assembly);
                    }
                    job.Request.Progress(0.7f + 0.3f * ++completedCount / job.Payloads.Length);
                }
            }
            process.Projects[manifest.ProjectId] = manifest.CodePackageId;
            var result = new HybridCLRLoadResult { ProjectId = manifest.ProjectId, CodePackageId = manifest.CodePackageId,
                Mode = calls.Simulation ? HybridCLRLoadMode.EditorSimulation : HybridCLRLoadMode.Native,
                Assemblies = new ReadOnlyDictionary<string, Assembly>(loaded) };
            sessions.Add(job.Address, new Session { Manifest = job.ManifestLease, Result = result });
            job.ManifestLease = null;
            job.Request.Stage = "Complete"; job.Request.AssemblyName = null;
            Finish(job, result, null);
        }

        private static HybridCLRProcessState.Loaded Record(HybridCLRPayload payload, bool metadata, Assembly assembly) =>
            new HybridCLRProcessState.Loaded { Identity = payload.Identity, Hash = payload.Sha256, Mvid = payload.Mvid, Metadata = metadata, Assembly = assembly };

        private void Finish(Job job, HybridCLRLoadResult result, Exception error)
        {
            if (job.Request.isDone) return;
            if (error != null && job.Irreversible) process.RequiresRestart = true;
            var routine = job.ProgressRoutine; job.ProgressRoutine = null;
            CoroutineStarter.Stop(routine);
            var pending = job.Pending; job.Pending = null;
            pending?.Dispose();
            job.ManifestLease?.Dispose(); job.ManifestLease = null;
            job.Bytes.Clear();
            if (ReferenceEquals(active, job)) active = null;
            if (ReferenceEquals(process.ActiveJob, job)) process.ActiveJob = null;
            UpdatePendingRegistration();
            // 所有责任和去重记录先提交；完成回调可以同步重入并创建后来请求。
            job.Request.Finish(result, error, job.Restart || process.RequiresRestart);
        }

        public HybridCLRModuleRequest StartModuleAsync(ResourceAddress manifestAddress, string entryAssemblyName, string moduleTypeFullName)
        {
            RequireAvailable(manifestAddress);
            RequireModuleArguments(entryAssemblyName, moduleTypeFullName);
            return StartModuleRequest(moduleRequests, manifestAddress, entryAssemblyName, moduleTypeFullName, false);
        }

#if UNITY_EDITOR
        /// <summary>显式使用 Editor 已加载的程序集启动 Module；不校验代码包，也不读取或回退到代码资源。</summary>
        public HybridCLRModuleRequest StartEditorModuleAsync(string projectId, string entryAssemblyName, string moduleTypeFullName)
        {
            AssertThread();
            var address = new ResourceAddress(projectId, HybridCLRManifest.ManifestPath);
            RequireAvailable(address);
            RequireModuleArguments(entryAssemblyName, moduleTypeFullName);
            if (CoreResourceAccess.Provider == null)
                throw new InvalidOperationException("Bind the Core resource provider before starting an EditorFast Module.");
            return StartModuleRequest(editorModuleRequests, address, entryAssemblyName, moduleTypeFullName, true);
        }
#endif

        private static void RequireModuleArguments(string entryAssemblyName, string moduleTypeFullName)
        {
            if (string.IsNullOrWhiteSpace(entryAssemblyName)) throw new ArgumentException("An exact entry assembly is required.", nameof(entryAssemblyName));
            if (string.IsNullOrWhiteSpace(moduleTypeFullName)) throw new ArgumentException("An exact Module type name is required.", nameof(moduleTypeFullName));
        }

        private HybridCLRModuleRequest StartModuleRequest(Dictionary<(ResourceAddress, string, string), HybridCLRModuleRequest> requests,
            ResourceAddress manifestAddress, string entryAssemblyName, string moduleTypeFullName, bool editorFast)
        {
            var key = (manifestAddress, entryAssemblyName, moduleTypeFullName);
            if (requests.TryGetValue(key, out var existing) && !existing.isDone) return existing;
            var request = new HybridCLRModuleRequest { ProjectId = manifestAddress.ProjectId, AssemblyName = entryAssemblyName };
            requests[key] = request;
            UpdatePendingRegistration();
            void FinishModule(Exception error, bool restart = false)
            {
                if (request.isDone) return;
                var detach = request.Detach; request.Detach = null; request.Invalidate = null;
                var routine = request.ProgressRoutine; request.ProgressRoutine = null;
                CoroutineStarter.Stop(routine);
                detach?.Invoke();
                if (requests.TryGetValue(key, out var current) && ReferenceEquals(current, request)) requests.Remove(key);
                UpdatePendingRegistration();
                request.Finish(error, restart);
            }
            request.Invalidate = error =>
            {
                request.Stage = "SessionUnavailable";
                FinishModule(error, !editorFast && process.RequiresRestart);
            };
            try
            {
#if UNITY_EDITOR
                if (editorFast)
                {
                    request.Stage = "EditorFast";
                    var matches = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == entryAssemblyName).ToArray();
                    if (matches.Length != 1)
                        throw new TypeLoadException("EditorFast requires exactly one already-loaded entry assembly: " + entryAssemblyName + "; matches=" + matches.Length);
                    request.Code = new HybridCLRLoadResult
                    {
                        ProjectId = manifestAddress.ProjectId, Mode = HybridCLRLoadMode.EditorFast,
                        Assemblies = new ReadOnlyDictionary<string, Assembly>(new Dictionary<string, Assembly>(StringComparer.Ordinal)
                            { { entryAssemblyName, matches[0] } })
                    };
                    StartCoreModule(manifestAddress, entryAssemblyName, moduleTypeFullName, request, FinishModule);
                    return request;
                }
#endif
                var load = LoadAsync(manifestAddress);
                void Progressed(HybridCLRLoadRequest source)
                {
                    if (request.isDone) return;
                    request.Stage = source.Stage; request.AssemblyName = source.AssemblyName;
                    request.Progress(source.progress * 0.8f);
                }
                Action<HybridCLRLoadRequest> continuation = completed =>
                {
                    if (request.isDone) return;
                    var detach = request.Detach; request.Detach = null;
                    detach?.Invoke();
                    request.Code = completed.Result;
                    if (completed.Exception != null)
                    {
                        request.Stage = completed.Stage; request.NativeErrorCode = completed.NativeErrorCode;
                        FinishModule(completed.Exception, completed.RequiresRestart); return;
                    }
                    try
                    {
                        StartCoreModule(manifestAddress, entryAssemblyName, moduleTypeFullName, request, FinishModule);
                    }
                    catch (Exception exception) { FinishModule(exception); }
                };
                request.Detach = () => { load.ProgressChanged -= Progressed; load.RemoveContinuation(continuation); };
                load.ProgressChanged += Progressed;
                Progressed(load);
                load.ContinueWith(continuation);
            }
            catch (Exception exception) { FinishModule(exception); }
            return request;
        }

        private void StartCoreModule(ResourceAddress address, string entryAssemblyName, string moduleTypeFullName,
            HybridCLRModuleRequest request, Action<Exception, bool> finish)
        {
            RequireAvailable(address);
            request.Stage = "Module"; request.AssemblyName = entryAssemblyName;
            var codeWeight = request.Code.Mode == HybridCLRLoadMode.EditorFast ? 0 : 0.8f;
            request.Progress(codeWeight);
            if (!request.Code.Assemblies.TryGetValue(entryAssemblyName, out var entry))
                throw new TypeLoadException("Entry assembly is not registered in this code package: " + entryAssemblyName);
            var type = entry.GetType(moduleTypeFullName, true, false);
            if (type.Assembly != entry || !typeof(Module).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                throw new TypeLoadException("A concrete YL Module type in the exact entry assembly is required: " + moduleTypeFullName);
            var registered = ModuleManager.GetModule(type);
            if (registered != null && !modules.Contains(registered)) throw new InvalidOperationException("This Module is already owned by another startup entry.");
            var startup = ModuleManager.StartUpModule(type);
            request.Module = startup.Module; modules.Add(startup.Module);
            void Started(StartUpModuleRequest done)
            {
                if (request.isDone) return;
                if (done.Exception != null) modules.Remove(done.Module);
                request.Stage = done.Exception == null ? "Complete" : "Module/" + done.Step;
                finish(done.Exception, false);
            }
            IEnumerator ObserveCore()
            {
                while (!request.isDone)
                {
                    try
                    {
                        RequireAvailable(address);
                        // 轮询真实 Core 请求并写入基类属性，不依赖调用方 yield/keepWaiting。
                        if (startup.isDone) Started(startup);
                        else
                        {
                            request.Stage = "Module/" + startup.Step;
                            request.Progress(codeWeight + (1 - codeWeight) * startup.progress);
                        }
                    }
                    catch (Exception error) { request.Invalidate?.Invoke(error); }
                    if (request.isDone) yield break;
                    yield return null;
                }
            }
            request.Detach = () => startup.RemoveCompleteEvent(Started);
            startup.AddCompleteEvent(Started);
            if (request.isDone) return;
            var routine = CoroutineStarter.Start(ObserveCore());
            if (request.isDone) CoroutineStarter.Stop(routine);
            else if (routine == null) finish(new InvalidOperationException("Module startup requires an active coroutine host."), false);
            else request.ProgressRoutine = routine;
        }

        public void Dispose()
        {
            AssertThread();
            if (disposed) return;
            if (active != null || PendingModuleRequests.Any(r => !r.isDone) || !executionStopping && !sessionEnded && session == generation && modules.Any(m =>
                m.State == ModuleState.Starting || m.State == ModuleState.Running || m.State == ModuleState.Closing))
                throw new InvalidOperationException("Finish pending code/Module startup and close this entry's Modules before disposing its manifest barrier.");
            foreach (var owned in sessions.Values) owned.Manifest.Dispose();
            sessions.Clear(); modules.Clear(); disposed = true;
            pendingLoaders.Remove(this);
        }

        private void RequireAvailable(ResourceAddress address)
        {
            AssertThread();
            if (disposed) throw new ObjectDisposedException(nameof(HybridCLRLoader));
            if (executionStopping || sessionEnded || session != generation || !resources.IsAvailable) throw new InvalidOperationException("This HybridCLR loader's Play/Resource session is unavailable.");
            if (address.Path != HybridCLRManifest.ManifestPath || string.IsNullOrEmpty(address.ProjectId)) throw new ArgumentException("Use the explicit project's canonical HybridCLR manifest address.", nameof(address));
        }

        private static void AssertThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThread) throw new InvalidOperationException("HybridCLR/Resource/Module operations require the Unity main thread.");
        }
    }
}
