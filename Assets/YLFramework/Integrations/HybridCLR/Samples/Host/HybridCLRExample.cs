using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using YLFramework.Core;
using YLFramework.Integrations.HybridCLR;
using YLFramework.Resource;

namespace YLFramework.Samples.HybridCLR
{
    [Preserve]
    public static class HybridCLRExampleState
    {
        public static string ProjectId
        {
            get; set;
        }
        public static string Message
        {
            get; set;
        }
        public static Task<string> Async
        {
            get; set;
        }
    }

    /// <summary>显式、应用持有的样例入口。没有自动 Awake/Start，也没有 AOT 到热更类型的静态引用。</summary>
    public sealed class HybridCLRExample
    {
        public const string EntryAssembly = "YLFramework.HybridCLR.Sample.Entry";
        public const string ModuleType = "YLFramework.Samples.HybridCLR.HotUpdate.ExampleModule";
        public ResourceStartup Startup
        {
            get; private set;
        }
        public HybridCLRLoader Loader
        {
            get; private set;
        }
        public HybridCLRModuleRequest ModuleRequest
        {
            get; private set;
        }
        public string Result { get; private set; } = "Not started";
        public string PrefabMarker
        {
            get; private set;
        }
        private InstanceLease _instance;
        private bool _isStarting;

        public IEnumerator Run(ResourceConfiguration configuration, string projectId, string compatibilityId)
        {
            if (_isStarting || Startup?.Resources != null || Loader != null)
            {
                throw new InvalidOperationException("Close this sample's prior responsibilities first.");
            }

            _isStarting = true;
            Result = "Starting";
            try
            {
                HybridCLRExampleState.ProjectId = projectId;
                HybridCLRExampleState.Message = null;
                HybridCLRExampleState.Async = null;
                Startup = new ResourceStartup(configuration, bindCore: true);
                using var initialized = Startup.StartAsync();
                yield return initialized;
                initialized.TakeResult();
                Loader = new HybridCLRLoader(Startup.Resources, compatibilityId);
                ModuleRequest = Loader.StartModuleAsync(new ResourceAddress(projectId, HybridCLRManifest.ManifestPath), EntryAssembly, ModuleType);
                yield return ModuleRequest;
                if (ModuleRequest.Exception != null)
                {
                    throw ModuleRequest.Exception;
                }

                var task = HybridCLRExampleState.Async ?? throw new InvalidOperationException("The hot Module did not start its async sample.");
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                var asyncResult = task.GetAwaiter().GetResult();
                using var prefab = Startup.Resources.InstantiateAsync(new ResourceAddress(projectId, "sample/hot-view"));
                yield return prefab;
                _instance = prefab.TakeResult();
                var behaviour = _instance.GameObject.GetComponents<Component>().SingleOrDefault(c => c && c.GetType().FullName == "YLFramework.Samples.HybridCLR.HotUpdate.ExampleBehaviour");
                if (!behaviour)
                {
                    throw new InvalidOperationException("The instantiated prefab did not resolve its loaded hot-update script.");
                }

                PrefabMarker = (string)behaviour.GetType().GetProperty("Marker").GetValue(behaviour);
                Result = (ModuleRequest.Code.IsSimulation ? "Simulation: " : "Native: ") + HybridCLRExampleState.Message + "; async=" + asyncResult + "; prefab=" + PrefabMarker;
            }
            finally
            {
                _isStarting = false;
            }
        }

        public void CloseCode()
        {
            if (_isStarting || ModuleRequest != null && !ModuleRequest.IsDone)
            {
                throw new InvalidOperationException("Module startup cannot be canceled; wait for its terminal result.");
            }

            if (Startup == null)
            {
                throw new InvalidOperationException("The sample has not started.");
            }

            if (ModuleRequest?.Module != null && ModuleRequest.Module.State == ModuleState.Running)
            {
                ModuleRequest.Module.Close();
            }

            _instance?.Dispose();
            _instance = null;
            Loader?.Dispose();
            Loader = null;
        }

        public ResourceRequest<bool> CloseAsync()
        {
            CloseCode();
            return Startup.StopAsync();
        }
    }
}
