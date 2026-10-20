using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.StateMachine;

namespace YLFramework.Behavior.Acceptance
{
    // 只服务本轮主工程接收，不复用或削弱 A Batch 的路径、退出及证据约束。
    [InitializeOnLoad]
    public static class BehaviorMainAcceptance
    {
        public const string Evidence = "C:/Users/Kano/AppData/Local/Temp/kilo/BM26-20261020T042500Z";
        private const string Project = "G:/Unity/Projects/CGJ2024";
        private const string Key = "YLFramework.Behavior.MainAcceptance";
        private const string TestKey = Key + ".Tests";
        private const string TestSaveGuardKey = Key + ".ProtectTestSaves";
        private const string Host = "YL.Behavior.MainReloadHost";
        private static readonly Results TestResults = new Results();
        private static Progress progress;

        [Serializable] private sealed class Round { public int mode, round, pid, cloneId; public bool passed, afterExitNoClone; }
        [Serializable] private sealed class Progress
        {
            public string kind, output, phase, scene, assetPath, scriptPath, sourceHash, sourceName, error;
            public string assetGuid, scriptGuid, typeName;
            public int pid, mode, round, entryFrame, reloadCount, originalOptions;
            public bool originalEnabled, complete, passed;
            public long deadline;
            public SceneSetup[] scenes;
            public List<Round> rounds = new List<Round>();
        }

        static BehaviorMainAcceptance()
        {
            TestRunnerApi.RegisterTestCallback(TestResults);
            string json = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            progress = JsonUtility.FromJson<Progress>(json);
            progress.reloadCount++;
            SaveProgress();
            Hook();
        }

        private static void Identity()
        {
            if (Application.dataPath != Project + "/Assets" || Application.unityVersion != "2022.3.62f3c1" ||
                System.Diagnostics.Process.GetCurrentProcess().Id != 4708 || !File.Exists(Evidence + "/preflight.json"))
                throw new InvalidOperationException("Wrong main project, Unity version, Editor PID or evidence binding.");
        }

        public static object Status()
        {
            Identity();
            return new { project = Project, unity = Application.unityVersion, pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                playing = EditorApplication.isPlaying, transition = EditorApplication.isPlayingOrWillChangePlaymode,
                compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating,
                building = BuildPipeline.isBuildingPlayer,
                compileFailed = EditorUtility.scriptCompilationFailed, scenes = EditorSceneManager.GetSceneManagerSetup(),
                reloadEnabled = EditorSettings.enterPlayModeOptionsEnabled, reloadOptions = (int)EditorSettings.enterPlayModeOptions,
                saveProtection = SessionState.GetBool(TestSaveGuardKey, false),
                pendingTests = SessionState.GetString(TestKey, string.Empty), pendingOperation = SessionState.GetString(Key, string.Empty) };
        }

        private static void Guard()
        {
            Identity();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Main Editor is not ready in EditMode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty || string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Do not replace, save or close a user's dirty/untitled scene.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Leave the user's prefab stage untouched.");
        }

        private static string Gate(string name)
        {
            Guard();
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9-]+$")) throw new ArgumentException("Invalid gate name.");
            string path = Evidence + "/" + name;
            if (Directory.Exists(path)) throw new InvalidOperationException("Never overwrite an earlier gate: " + path);
            Directory.CreateDirectory(path);
            Write(path + "/editor-before.json", Status());
            return path;
        }

        private static void Write(string path, object value) => File.WriteAllText(path, JsonConvert.SerializeObject(value, Formatting.Indented));
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool Owned(string path)
        {
            path = path.Replace('\\', '/');
            return new[] { "Overlap", "CharacterController2D", "MoveablePlatform", "StateMachine" }.Any(module =>
                path.StartsWith("Assets/YLFramework/Runtime/Systems/" + module + "/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/YLFramework/Editor/Systems/" + module + "/", StringComparison.Ordinal)) ||
                path.StartsWith("Assets/YLFramework/Tests/Fixtures/Extensions/Behavior/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/YLFramework/Tests/EditMode/Extensions/Behavior/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/YLFramework/Tests/PlayMode/Extensions/Behavior/", StringComparison.Ordinal);
        }

        public static void Audit(string name)
        {
            string output = Gate(name);
            var errors = new List<string>();
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            string[] names = { "YLFramework.Runtime", "YLFramework.Editor", "YLFramework.Tests.EditMode", "YLFramework.Tests.PlayMode", "YLFramework.Behavior.Acceptance", "YLFramework.Behavior.Acceptance.Editor" };
            foreach (string expected in names) if (assemblies.All(item => item.name != expected)) errors.Add("Missing assembly: " + expected);
            var files = Directory.GetFiles("Assets/YLFramework", "*.cs", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/')).Where(Owned).ToArray();
            foreach (string path in files)
            {
                string expected = path.Contains("/Runtime/") ? names[0] : path.Contains("/Editor/Systems/") ? names[1] :
                    path.Contains("/Tests/EditMode/") ? names[2] : path.Contains("/Tests/PlayMode/") ? names[3] : path.Contains("/Behavior/Editor/") ? names[5] : names[4];
                var owner = assemblies.SingleOrDefault(item => item.sourceFiles.Any(file => Path.GetFullPath(file) == Path.GetFullPath(path)));
                if (owner == null || owner.name != expected) errors.Add(path + " -> " + owner?.name + "; expected " + expected);
            }
            var metas = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(File.ReadAllText(Evidence + "/candidate-metas.json"));
            foreach (var meta in metas)
                if (AssetDatabase.AssetPathToGUID(meta["path"].Substring(0, meta["path"].Length - 5)) != meta["guid"]) errors.Add("AssetDatabase GUID mismatch: " + meta["path"]);
            var assets = AssetDatabase.GetAllAssetPaths().Where(Owned).ToArray();
            foreach (string path in assets) if (!File.Exists(path + ".meta")) errors.Add("Missing meta: " + path);
            int missingScripts = 0;
            foreach (string path in assets.Where(path => path.EndsWith(".prefab", StringComparison.Ordinal)))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true)) missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            }
            var binary = AppDomain.CurrentDomain.GetAssemblies().Where(item => names.Contains(item.GetName().Name)).Select(item => new {
                name = item.GetName().Name, path = item.Location, sha256 = Hash(item.Location), references = item.GetReferencedAssemblies().Select(reference => reference.FullName).ToArray() }).ToArray();
            foreach (var item in binary.Where(item => item.name == names[0] || item.name == names[4]))
                foreach (string reference in item.references)
                    if (new[] { "XFFSM", "XFGameFramework", "XFAB", "EPPlus" }.Any(part => reference.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)) errors.Add(item.name + " -> " + reference);
            var playerGraph = CompilationPipeline.GetAssemblies(AssembliesType.Player).Where(item => names.Contains(item.name)).ToArray();
            foreach (var item in playerGraph)
                foreach (var reference in item.assemblyReferences)
                    if (reference.name.StartsWith("UnityEditor", StringComparison.Ordinal) || reference.name == names[1] || reference.name == names[5]) errors.Add("Player graph: " + item.name + " -> " + reference.name);
            Directory.CreateDirectory(output + "/assembly-inputs");
            int number = 0;
            foreach (string path in Directory.GetFiles("Library/Bee", "*.rsp", SearchOption.AllDirectories).Where(path => names.Any(item => Path.GetFileName(path).StartsWith(item + ".", StringComparison.Ordinal))))
                File.Copy(path, output + "/assembly-inputs/" + (++number).ToString("D3") + "-" + Path.GetFileName(path));
            Write(output + "/import-audit.json", new { state = Status(), errors, missingScripts, candidateMetaCount = metas.Count,
                sources = files.Select(path => new { path, sha256 = Hash(path) }).ToArray(), binaryEditorProfile = binary,
                assemblies = assemblies.Where(item => names.Contains(item.name)).Select(item => new { item.name, item.outputPath, item.sourceFiles, item.compiledAssemblyReferences, item.defines }),
                playerAssemblies = playerGraph.Select(item => new { item.name, item.sourceFiles, references = item.assemblyReferences.Select(reference => reference.name) }),
                packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(item => new { item.name, item.version, source = item.source.ToString(), item.resolvedPath }) });
            if (errors.Count != 0 || missingScripts != 0) throw new InvalidOperationException("Main import audit failed: " + string.Join("; ", errors));
            Debug.Log("Main A import audit PASS: " + files.Length + " owned sources; " + metas.Count + " frozen GUID mappings.");
        }

        public static void RunTests(string mode, string name, bool boundary = false)
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(TestKey, string.Empty))) throw new InvalidOperationException("A main test run is active.");
            string output = Gate(name);
            SessionState.SetString(TestKey, output);
            SessionState.SetBool(TestSaveGuardKey, mode == "PlayMode");
            var filter = new Filter { testMode = mode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode,
                groupNames = boundary ? new[] { "^YLFramework\\.Foundation\\.Tests\\.FoundationBoundaryTests\\.", "^YLFramework\\.Core\\.Tests\\.CoreArchitectureBoundaryTests\\.", "^YLFramework\\.Tests\\.Resource\\.FrameworkAssemblyIndependenceTests\\." } : new[] { "^YLFramework\\.Tests\\.Extensions\\.Behavior\\." } };
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            bool started = false;
            try { string job = api.Execute(new ExecutionSettings(filter)); started = true; Write(output + "/test-request.json", new { mode, boundary, job, filter }); }
            catch
            {
                if (!started) { SessionState.EraseString(TestKey); SessionState.EraseBool(TestSaveGuardKey); }
                throw;
            }
            finally { UnityEngine.Object.DestroyImmediate(api); }
        }

        internal static string[] FilterTestSaves(string[] paths)
        {
            if (!SessionState.GetBool(TestSaveGuardKey, false)) return paths;
            Identity();
            // UTF 1.3.9创建bootstrap时会SaveAssets，仅放行其新场景，不落盘其他用户脏资产。
            var allowed = paths.Where(path => System.Text.RegularExpressions.Regex.IsMatch(path, "^Assets/InitTestScene[0-9]+[.]unity$")).ToArray();
            var deferred = paths.Except(allowed).ToArray();
            string output = SessionState.GetString(TestKey, string.Empty);
            if (deferred.Length != 0 && !string.IsNullOrEmpty(output))
                File.AppendAllText(output + "/deferred-asset-saves.jsonl", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, deferred }) + Environment.NewLine);
            return allowed;
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string output = SessionState.GetString(TestKey, string.Empty);
                if (string.IsNullOrEmpty(output)) return;
                Identity();
                try
                {
                    File.WriteAllText(output + "/results.xml", result.ToXml().OuterXml);
                    Write(output + "/summary.json", new { result.ResultState, result.PassCount, result.FailCount, result.SkipCount, result.InconclusiveCount, result.Duration, result.StartTime, result.EndTime, pid = System.Diagnostics.Process.GetCurrentProcess().Id });
                }
                finally { SessionState.EraseString(TestKey); SessionState.EraseBool(TestSaveGuardKey); }
                Debug.Log($"Main A tests: pass={result.PassCount}; fail={result.FailCount}; skip={result.SkipCount}; XML={output}/results.xml");
            }
        }

        private static string Generate(string output)
        {
            string scene = BehaviorAcceptanceBuilder.Generate();
            Write(output + "/owned-fixture.json", new { scene, folder = Path.GetDirectoryName(scene).Replace('\\', '/') });
            return scene;
        }

        private static void SaveProgress() => SessionState.SetString(Key, JsonUtility.ToJson(progress));
        private static void Hook()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= PlayState; EditorApplication.playModeStateChanged += PlayState;
        }

        private static Progress Begin(string kind, string name)
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, string.Empty))) throw new InvalidOperationException("Main async gate already active.");
            string output = Gate(name);
            var state = new Progress { kind = kind, output = output, pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                scenes = EditorSceneManager.GetSceneManagerSetup(), originalEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                originalOptions = (int)EditorSettings.enterPlayModeOptions, deadline = DateTime.UtcNow.AddMinutes(3).Ticks };
            state.scene = Generate(output);
            state.assetPath = Path.GetDirectoryName(state.scene).Replace('\\', '/') + "/BehaviorAcceptanceFSM.asset";
            state.assetGuid = AssetDatabase.AssetPathToGUID(state.assetPath);
            return state;
        }

        public static void Reload(string name)
        {
            progress = Begin("reload", name);
            EditorSceneManager.OpenScene(progress.scene, OpenSceneMode.Single);
            var player = UnityEngine.Object.FindObjectOfType<BehaviorPlayerProbe>();
            player.enabled = false;
            var obj = new GameObject(Host);
            var asset = obj.AddComponent<FSMController>();
            asset.AddRuntimeFSMController(player.source, "MainReload");
            var probe = obj.AddComponent<BehaviorReloadProbe>();
            probe.asset = asset; probe.source = player.source;
            progress.sourceName = player.source.name;
            progress.sourceHash = Hash(progress.assetPath);
            progress.phase = "prepare";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            SaveProgress(); Hook();
        }

        public static void Binding(string name)
        {
            progress = Begin("binding", name);
            var asset = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(progress.assetPath);
            var node = new FSMStateNodeData { name = "AutoBound" };
            asset.AddState(node); AssetDatabase.SaveAssetIfDirty(asset);
            progress.scriptPath = Path.GetDirectoryName(progress.scene).Replace('\\', '/') + "/MainBoundState" + Guid.NewGuid().ToString("N") + ".cs";
            SaveProgress(); Hook();
            var creator = Type.GetType("YLFramework.StateMachine.FSMStateCreator, YLFramework.Editor", true);
            var create = creator.GetMethod("CreateFSMState", BindingFlags.Static | BindingFlags.NonPublic);
            Require((bool)create.Invoke(null, new object[] { progress.scriptPath, false, asset, node }), "Product Create And Add rejected the owned source path.");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static BehaviorReloadProbe Probe() { var obj = GameObject.Find(Host); return obj ? obj.GetComponent<BehaviorReloadProbe>() : null; }

        private static void PlayState(PlayModeStateChange change)
        {
            if (progress == null || progress.kind != "reload") return;
            Identity();
            if (change == PlayModeStateChange.EnteredPlayMode) { progress.phase = "verify"; progress.entryFrame = Time.frameCount; }
            if (change == PlayModeStateChange.EnteredEditMode) progress.phase = "exited";
            SaveProgress();
        }

        private static void Tick()
        {
            if (progress == null || progress.complete || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                Identity();
                Require(progress.pid == System.Diagnostics.Process.GetCurrentProcess().Id, "Editor process changed.");
                Require(DateTime.UtcNow.Ticks < progress.deadline, "Timed out waiting for real Editor transitions.");
                if (!string.IsNullOrEmpty(progress.error)) { if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish(false); return; }
                if (progress.kind == "binding") { CheckBinding(); return; }
                if (progress.phase == "prepare")
                {
                    if (progress.round == 0 && progress.mode > 0) EditorSceneManager.OpenScene(progress.scene, OpenSceneMode.Single);
                    EditorSettings.enterPlayModeOptionsEnabled = progress.mode != 0;
                    EditorSettings.enterPlayModeOptions = progress.mode == 2 ? EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload : EnterPlayModeOptions.DisableDomainReload;
                    progress.phase = "entering"; SaveProgress(); EditorApplication.isPlaying = true;
                }
                else if (progress.phase == "verify" && EditorApplication.isPlaying && Time.frameCount - progress.entryFrame >= 5)
                {
                    var probe = Probe();
                    Require(probe && probe.asset && probe.source, "Scene-authored dual FSM consumer missing.");
                    Require(probe.LightweightAllocated && probe.LightweightState == 1, "Lightweight FSM did not start independently.");
                    Require(probe.asset.Initialized && probe.asset.GetCurrentStateInfo(0)?.data.name == "Idle", "Asset FSM did not start fresh.");
                    Require(probe.asset.RuntimeFSMController.Count == 1 && probe.asset.RuntimeFSMController[0] && probe.asset.RuntimeFSMController[0] != probe.source, "Owned native clone missing.");
                    probe.asset.SetBool("Go", true);
                    Require(probe.asset.GetCurrentStateInfo(0)?.data.name == "Active" && probe.LightweightState == 1, "Asset transition is not isolated.");
                    Require(probe.ChangeLightweight() && probe.LightweightState == 2, "Lightweight transition failed.");
                    probe.asset.SetTrigger("Fire"); probe.asset.SetTrigger("Fire"); probe.asset.ResetTrigger("Fire");
                    Require(!probe.asset.GetTrigger("Fire") && probe.source.parameters[0].Value == 0 && Hash(progress.assetPath) == progress.sourceHash, "Trigger or source isolation failed.");
                    progress.rounds.Add(new Round { mode = progress.mode, round = progress.round, pid = progress.pid, cloneId = probe.asset.RuntimeFSMController[0].GetInstanceID(), passed = true });
                    progress.phase = "leaving"; SaveProgress(); EditorApplication.isPlaying = false;
                }
                else if (progress.phase == "exited")
                {
                    var probe = Probe();
                    Require(!probe || !probe.LightweightAllocated, "Lightweight pool state retained after exit.");
                    Require(Resources.FindObjectsOfTypeAll<RuntimeFSMController>().All(asset => !asset || asset.name != progress.sourceName || EditorUtility.IsPersistent(asset)), "Owned runtime clone leaked.");
                    Require(Hash(progress.assetPath) == progress.sourceHash, "Source bytes changed after exit.");
                    progress.rounds.Last().afterExitNoClone = true;
                    if (++progress.round == 2) { progress.round = 0; progress.mode++; }
                    if (progress.mode == 3) { Finish(true); return; }
                    progress.phase = "prepare"; SaveProgress();
                }
            }
            catch (Exception error)
            {
                progress.error = error.ToString(); SaveProgress(); Debug.LogException(error);
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                else if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish(false);
            }
        }

        private static void CheckBinding()
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(progress.scriptPath);
            var type = script ? script.GetClass() : null;
            if (type == null || progress.reloadCount == 0) return;
            var asset = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(progress.assetPath);
            string guid = AssetDatabase.AssetPathToGUID(progress.scriptPath);
            var node = asset.GetStateNodeData("AutoBound");
            if (!node.StateScripts.Any(info => info.guid == guid && info.className == type.FullName)) return;
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.ImportAsset(progress.assetPath, ImportAssetOptions.ForceUpdate);
            asset = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(progress.assetPath); asset.ClearCache();
            Require(asset.GetStateNodeData("AutoBound").StateScripts.Any(info => info.guid == guid && info.className == type.FullName), "Product binding did not survive native reimport.");
            Require(asset.ValidateConfiguration(out string error), error);
            progress.typeName = type.FullName; progress.scriptGuid = guid;
            Finish(true);
        }

        private static void Finish(bool passed)
        {
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayState;
            try
            {
                Identity();
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)progress.originalOptions;
                EditorSettings.enterPlayModeOptionsEnabled = progress.originalEnabled;
                if (progress.kind == "reload") EditorSceneManager.RestoreSceneManagerSetup(progress.scenes);
            }
            catch (Exception error) { progress.error += "\nRestore: " + error; passed = false; }
            progress.complete = true; progress.passed = passed;
            Write(progress.output + "/results.json", progress);
            string output = progress.output;
            SessionState.EraseString(Key); progress = null;
            Write(output + "/editor-after.json", Status());
            Debug.Log("Main A async gate finished: " + (passed ? "PASS" : "FAIL") + "; main Editor remains open.");
        }

        public static void BuildMono(string name)
        {
            string output = Gate(name);
            var actual = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            Require(actual == ScriptingImplementation.Mono2x && EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64, "Existing build target/backend does not allow Release Mono.");
            string scene = Generate(output);
            var ownedScene = EditorSceneManager.OpenScene(scene, OpenSceneMode.Additive);
            try
            {
                var probe = ownedScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BehaviorPlayerProbe>(true)).Single();
                Require(probe.eventReceiver && probe.eventMotor && probe.eventPlatform && probe.eventQuery && probe.eventSphere && probe.eventTarget && probe.eventTarget3D, "Saved scene lost event references.");
                Require(ownedScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).All(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0), "Owned scene has Missing Script.");
            }
            finally { EditorSceneManager.CloseScene(ownedScene, true); }
            string directory = output + "/player";
            Directory.CreateDirectory(directory);
            string company = PlayerSettings.companyName, product = PlayerSettings.productName;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, target = BuildTarget.StandaloneWindows64,
                locationPathName = directory + "/BehaviorMainAcceptance.exe", options = BuildOptions.None });
            var summary = report.summary;
            var data = new { actualBackend = actual.ToString(), stripping = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Standalone).ToString(),
                development = (summary.options & BuildOptions.Development) != 0, options = summary.options.ToString(), status = summary.result.ToString(),
                output = Path.GetFullPath(summary.outputPath), guid = summary.guid.ToString(), errors = summary.totalErrors, warnings = summary.totalWarnings,
                company, product,
                size = summary.totalSize, seconds = summary.totalTime.TotalSeconds, unity = Application.unityVersion, pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                steps = report.steps.Select(step => new { step.name, seconds = step.duration.TotalSeconds, messages = step.messages.Select(message => new { message.content, type = message.type.ToString() }) }) };
            Write(output + "/build-report.json", data);
            Write(Evidence + "/build-mono.json", data);
            Require(summary.result == BuildResult.Succeeded && !data.development, "Release Mono build failed.");
        }

        public static void IL2CPPPreflight(string name)
        {
            string output = Gate(name);
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            var stripping = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Standalone);
            Write(output + "/preflight.json", new { backend = backend.ToString(), stripping = stripping.ToString(),
                status = backend == ScriptingImplementation.IL2CPP && stripping == ManagedStrippingLevel.Low ? "ConfigurationAllowsFurtherToolchainCheck" : "BlockedBeforeBuild",
                reason = "Read-only: no backend/stripping setter, dependency installation or ProjectSettings overwrite is authorized.",
                il2cppDirectoryExists = Directory.Exists(EditorApplication.applicationContentsPath + "/il2cpp"),
                windowsSupport = Directory.Exists(EditorApplication.applicationContentsPath + "/PlaybackEngines/windowsstandalonesupport"), state = Status() });
        }

        public static void Cleanup(string name)
        {
            string output = Gate(name);
            var records = new List<object>();
            foreach (string receipt in Directory.GetFiles(Evidence, "owned-fixture.json", SearchOption.AllDirectories))
            {
                var item = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(receipt));
                string folder = item["folder"];
                Require(System.Text.RegularExpressions.Regex.IsMatch(folder, "^Assets/YLFramework/Tests/Fixtures/Extensions/Behavior/GeneratedAcceptance_[0-9a-f]{32}$"), "Unowned cleanup path.");
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                Require(!Enumerable.Range(0, SceneManager.sceneCount).Any(index => SceneManager.GetSceneAt(index).path.StartsWith(folder + "/", StringComparison.Ordinal)), "Owned scene is still open.");
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Concat(new[] { folder + ".meta" }))
                {
                    string relative = path == folder + ".meta" ? Path.GetFileName(folder) + ".meta" : Path.GetFileName(folder) + "/" + path.Substring(folder.Length + 1);
                    string archive = output + "/archive/" + relative;
                    Directory.CreateDirectory(Path.GetDirectoryName(archive)); File.Copy(path, archive, false);
                    Require(Hash(path) == Hash(archive), "Archive hash mismatch.");
                    records.Add(new { path, sha256 = Hash(path), archive });
                }
                Require(AssetDatabase.DeleteAsset(folder), "Owned cleanup failed.");
            }
            Write(output + "/cleanup.json", records);
        }
    }

    internal sealed class BehaviorMainSaveFence : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths) => BehaviorMainAcceptance.FilterTestSaves(paths);
    }
}
