using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace YLFramework.Tests.ResourceBuild
{
    /// <summary>显式启动的自有验证回执；逐叶记录与suite终态分开，超时后只回收同一运行。</summary>
    [InitializeOnLoad]
    public static class ResourceCostEvidence
    {
        private const string StateKey = "YL.Resource.B02F03.Evidence";
        private const string EnvironmentKey = "YL.Resource.B02F03.LoopbackEnvironment";
        private static readonly Observer _callback = new();
        public static string DirectoryPath => SessionState.GetString(StateKey, "");

        /// <summary>仅配置本批自有非生产回环凭据；终态回调恢复原进程环境，原值从不进入证据。</summary>
        public static void UseLoopbackEnvironment(string ownedRoot)
        {
            if (SessionState.GetString(EnvironmentKey, "").Length != 0)
            {
                throw new InvalidOperationException("Loopback environment is already owned.");
            }

            var owner = JObject.Parse(File.ReadAllText(Path.Combine(ownedRoot, "owner.json")));
            if ((string)owner["scope"] != "B02+F03-20261017-0902" ||
                !string.Equals(Path.GetFullPath(ownedRoot), Path.GetFullPath((string)owner["ownedRoot"]), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Loopback owner mismatch.");
            }

            var pfx = Path.Combine(ownedRoot, "loopback.pfx");
            if (!File.Exists(pfx))
            {
                throw new FileNotFoundException("Generate the owned loopback credential before measuring.");
            }

            var previous = new Dictionary<string, string>
            {
                ["YL_RESOURCE_TEST_PFX"] = Environment.GetEnvironmentVariable("YL_RESOURCE_TEST_PFX"),
                ["YL_RESOURCE_TEST_PASSWORD"] = Environment.GetEnvironmentVariable("YL_RESOURCE_TEST_PASSWORD")
            };
            SessionState.SetString(EnvironmentKey, JsonConvert.SerializeObject(previous));
            Environment.SetEnvironmentVariable("YL_RESOURCE_TEST_PFX", pfx);
            Environment.SetEnvironmentVariable("YL_RESOURCE_TEST_PASSWORD", "b02f03-loopback-only");
        }

        public static void RestoreLoopbackEnvironment()
        {
            var json = SessionState.GetString(EnvironmentKey, "");
            if (json.Length == 0)
            {
                return;
            }

            var previous = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            foreach (var item in previous)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }

            if (previous.Any(item => !string.Equals(Environment.GetEnvironmentVariable(item.Key), item.Value, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The original loopback environment was not restored exactly.");
            }

            SessionState.EraseString(EnvironmentKey);
        }

        public static bool OwnsLoopbackEnvironment => SessionState.GetString(EnvironmentKey, "").Length != 0;

        static ResourceCostEvidence()
        {
            if (DirectoryPath.Length != 0 && !File.Exists(Path.Combine(DirectoryPath, "finished.json")))
            {
                Register();
            }

            AssemblyReloadEvents.beforeAssemblyReload += () => TestRunnerApi.UnregisterTestCallback(_callback);
        }

        /// <summary>不保存场景、不覆盖已有run，不并发测试；路径须属于明确B02/F03 owner。</summary>
        public static string Run(string ownedRoot, string runId, string[] testNames)
        {
            if (DirectoryPath.Length != 0 && !File.Exists(Path.Combine(DirectoryPath, "finished.json")))
            {
                throw new InvalidOperationException("An owned run is still active; recover it instead of restarting.");
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed ||
                Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) ||
                !string.IsNullOrEmpty(SessionState.GetString("Unity_MCP_TestRunner_TestCallRequestID", "")) ||
                !string.IsNullOrEmpty(SessionState.GetString("MCP_PendingTestRun", "")))
            {
                throw new InvalidOperationException("A settled Editor with saved scenes and no active tests is required.");
            }

            if (string.IsNullOrEmpty(runId) || runId.Any(x => !(x >= 'a' && x <= 'z' || x >= '0' && x <= '9' || x == '-')) || testNames == null || testNames.Length == 0)
            {
                throw new ArgumentException("An explicit run id and test selection are required.");
            }

            ownedRoot = Path.GetFullPath(ownedRoot);
            var owner = JObject.Parse(File.ReadAllText(Path.Combine(ownedRoot, "owner.json")));
            if ((string)owner["scope"] != "B02+F03-20261017-0902" ||
                !string.Equals(ownedRoot, Path.GetFullPath((string)owner["ownedRoot"]), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Evidence owner mismatch.");
            }

            var path = Path.Combine(ownedRoot, runId);
            if (Directory.Exists(path))
            {
                throw new IOException("Refusing an existing run directory.");
            }

            for (var cursor = new DirectoryInfo(ownedRoot); cursor != null; cursor = cursor.Parent)
            {
                if (cursor.Exists && (cursor.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Linked evidence root refused.");
                }
            }

            Directory.CreateDirectory(path);
            SessionState.SetString(StateKey, path);
            Write("request", new
            {
                StartedAtUtc = DateTime.UtcNow,
                Unity = Application.unityVersion,
                TestNames = testNames
            });
            using (var sha = SHA256.Create())
            {
                var files = new[] { "Assets/YLFramework/Runtime", "Assets/YLFramework/Editor", "Assets/YLFramework/Samples", "Assets/YLFramework/Tests" }
                    .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".asmdef", StringComparison.Ordinal))
                    .Concat(new[] { "Assets/YLFramework/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll", "ProjectSettings/ProjectVersion.txt", "Packages/manifest.json", "Packages/packages-lock.json" })
                    .OrderBy(file => file, StringComparer.Ordinal).Select(file =>
                    {
                        using var stream = File.OpenRead(file);
                        return new
                        {
                            Path = file.Replace('\\', '/'),
                            Length = stream.Length,
                            Sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()
                        };
                    }).ToArray();
                var binaries = new[] { typeof(YLFramework.Services.Resource.ResourceSystem).Assembly,
                    typeof(YLFramework.Editor.Resource.ResourceEditorOperations).Assembly, typeof(ResourceCostEvidence).Assembly,
                    typeof(YLFramework.Samples.Resource.Editor.ResourceBuildRecipeExample).Assembly }.Select(assembly =>
                    {
                        using var stream = File.OpenRead(assembly.Location);
                        return new
                        {
                            Name = assembly.GetName().Name,
                            Length = stream.Length,
                            Sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant()
                        };
                    }).ToArray();
                Write("identity", new
                {
                    CapturedAtUtc = DateTime.UtcNow,
                    SourceFiles = files,
                    LoadedAssemblies = binaries,
                    SystemInfo.operatingSystem,
                    SystemInfo.processorType,
                    SystemInfo.processorCount,
                    SystemInfo.processorFrequency,
                    SystemInfo.systemMemorySize,
                    SystemInfo.graphicsDeviceName,
                    SystemInfo.graphicsMemorySize,
                    Backend = "Windows Editor / Mono",
                    Target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    AllocationCounter = "Unavailable unless a separately recorded capability probe succeeds",
                    CacheCondition = "Existing Editor/OS caches; no system cache, Library or user data cleared"
                });
            }
            Register();
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                return api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = testNames }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(api);
            }
        }

        public static void Write(string name, object value)
        {
            if (DirectoryPath.Length == 0 || name.Any(x => !(char.IsLetterOrDigit(x) || x == '-')))
            {
                throw new InvalidOperationException("No explicit evidence run, or invalid evidence name.");
            }

            var bytes = new System.Text.UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(value, Formatting.Indented) + "\n");
            var path = Path.Combine(DirectoryPath, name + ".json");
            if (File.Exists(path))
            {
                throw new IOException("Refusing to replace existing evidence.");
            }

            using (var stream = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(path + ".pending", path);
        }

        private static void Register()
        {
            TestRunnerApi.UnregisterTestCallback(_callback);
            TestRunnerApi.RegisterTestCallback(_callback);
        }

        private sealed class Observer : ICallbacks
        {
            public void RunStarted(ITestAdaptor test)
            {
            }
            public void TestStarted(ITestAdaptor test)
            {
            }
            public void TestFinished(ITestResultAdaptor result)
            {
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    var stack = new Stack<ITestResultAdaptor>();
                    stack.Push(result);
                    var leaves = new List<object>();
                    var failures = new List<object>();
                    while (stack.Count != 0)
                    {
                        var item = stack.Pop();
                        var row = new
                        {
                            Name = item.FullName,
                            Status = item.TestStatus.ToString(),
                            item.ResultState,
                            item.Duration,
                            item.Message,
                            item.StackTrace,
                            item.Output
                        };
                        if (!item.Test.IsSuite)
                        {
                            leaves.Add(row);
                        }
                        else
                        {
                            if (item.TestStatus.ToString() == "Failed")
                            {
                                failures.Add(row);
                            }

                            foreach (var child in item.Children)
                            {
                                stack.Push(child);
                            }
                        }
                    }
                    Exception environmentFailure = null;
                    try
                    {
                        RestoreLoopbackEnvironment();
                    }
                    catch (Exception error)
                    {
                        environmentFailure = error;
                    }
                    Write("finished", new
                    {
                        FinishedAtUtc = DateTime.UtcNow,
                        PassedGate = result.TestStatus.ToString() == "Passed" && leaves.Count > 0 && failures.Count == 0 && environmentFailure == null,
                        result.ResultState,
                        Leaves = leaves,
                        SuiteFailures = failures,
                        EnvironmentRestored = environmentFailure == null,
                        EnvironmentFailure = environmentFailure == null ? null : new
                        {
                            Type = environmentFailure.GetType().FullName,
                            environmentFailure.HResult
                        }
                    });
                }
                finally
                {
                    TestRunnerApi.UnregisterTestCallback(this);
                }
            }
        }
    }
}
