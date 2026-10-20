using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.StateMachine;

namespace YLFramework.Behavior.Acceptance
{
    [InitializeOnLoad]
    public static class BehaviorReloadBatch
    {
        private const string Key = "YLFramework.Behavior.ReloadBatch";
        private const string HostName = "YL.Behavior.ReloadHost";
        [Serializable] private sealed class Round { public int mode, round, frame, cloneId; public bool passed, afterExitNoClone; public string utc; }
        [Serializable] private sealed class SavedScene { public string path; public bool loaded, active; }
        [Serializable] private sealed class State
        {
            public string output, scene, sourceName, phase, error;
            public int mode, round, entryFrame;
            public bool originalEnabled, complete;
            public int originalOptions;
            public double deadline;
            public List<Round> results = new List<Round>();
            public SavedScene[] originalScenes;
        }
        private static State state;

        static BehaviorReloadBatch()
        {
            string json = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            state = JsonUtility.FromJson<State>(json);
            if (!state.complete) Hook();
        }

        private static void Save() => SessionState.SetString(Key, JsonUtility.ToJson(state));
        private static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying) throw new InvalidOperationException("Use the isolated A BatchMode in EditMode.");
            if (!string.IsNullOrEmpty(SessionState.GetString(Key, string.Empty))) throw new InvalidOperationException("A reload run is already recorded in this Editor session.");
            state = new State
            {
                output = BehaviorUnityBatch.Output(), originalEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                originalOptions = (int)EditorSettings.enterPlayModeOptions,
                originalScenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SavedScene { path = scene.path, loaded = scene.isLoaded, active = scene.isActive }).ToArray(),
                phase = "prepare", deadline = EditorApplication.timeSinceStartup + 60
            };
            state.scene = BehaviorAcceptanceBuilder.Generate();
            EditorSceneManager.OpenScene(state.scene, OpenSceneMode.Single);
            var player = UnityEngine.Object.FindObjectOfType<BehaviorPlayerProbe>();
            if (!player || !player.source) throw new InvalidOperationException("Owned fixture source is missing.");
            player.enabled = false;
            var obj = new GameObject(HostName);
            var asset = obj.AddComponent<FSMController>();
            asset.AddRuntimeFSMController(player.source, "Reload");
            var probe = obj.AddComponent<BehaviorReloadProbe>();
            probe.asset = asset;
            probe.source = player.source;
            state.sourceName = player.source.name;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), state.scene);
            Save();
            Hook();
        }

        private static BehaviorReloadProbe Probe()
        {
            var obj = GameObject.Find(HostName);
            return obj ? obj.GetComponent<BehaviorReloadProbe>() : null;
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException($"mode={state.mode}, round={state.round}, phase={state.phase}: {message}");
        }

        private static void OnPlayState(PlayModeStateChange change)
        {
            if (state == null || state.complete) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                state.phase = "verify";
                state.entryFrame = Time.frameCount;
                state.deadline = EditorApplication.timeSinceStartup + 30;
                Save();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (!string.IsNullOrEmpty(state.error)) { RestoreAndExit(1); return; }
                state.phase = "exited";
                state.deadline = EditorApplication.timeSinceStartup + 30;
                Save();
            }
        }

        private static void Tick()
        {
            if (state == null || state.complete || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                Require(EditorApplication.timeSinceStartup <= state.deadline, "Timed out waiting for a real play-state transition.");
                if (state.phase == "prepare")
                {
                    if (state.round == 0 && state.mode > 0) EditorSceneManager.OpenScene(state.scene, OpenSceneMode.Single);
                    EditorSettings.enterPlayModeOptionsEnabled = state.mode != 0;
                    EditorSettings.enterPlayModeOptions = state.mode == 2 ? EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload : EnterPlayModeOptions.DisableDomainReload;
                    state.phase = "entering";
                    state.deadline = EditorApplication.timeSinceStartup + 30;
                    Save();
                    EditorApplication.isPlaying = true;
                }
                else if (state.phase == "verify" && EditorApplication.isPlaying && Time.frameCount - state.entryFrame >= 5)
                {
                    var probe = Probe();
                    Require(probe && probe.asset && probe.source, "Scene-authored dual FSM probe is missing.");
                    Require(probe.LightweightAllocated && probe.LightweightState == 1, "Lightweight FSM did not start fresh.");
                    Require(probe.asset.Initialized && probe.asset.GetCurrentStateInfo(0)?.data.name == "Idle", "Scene-authored asset FSM did not start at Idle.");
                    Require(probe.asset.RuntimeFSMController.Count == 1 && probe.asset.RuntimeFSMController[0] && probe.asset.RuntimeFSMController[0] != probe.source, "Owned native clone is missing or source was reused.");
                    probe.asset.SetBool("Go", true);
                    Require(probe.asset.GetCurrentStateInfo(0)?.data.name == "Active" && probe.LightweightState == 1, "Asset transition affected lightweight state.");
                    Require(probe.ChangeLightweight() && probe.LightweightState == 2 && probe.asset.GetCurrentStateInfo(0)?.data.name == "Active", "Lightweight transition affected the asset FSM.");
                    probe.asset.SetTrigger("Fire"); probe.asset.SetTrigger("Fire"); probe.asset.ResetTrigger("Fire");
                    Require(!probe.asset.GetTrigger("Fire") && probe.source.parameters[0].Value == 0, "Trigger reset/source isolation failed.");
                    state.results.Add(new Round { mode = state.mode, round = state.round, frame = Time.frameCount, cloneId = probe.asset.RuntimeFSMController[0].GetInstanceID(), passed = true, utc = DateTime.UtcNow.ToString("o") });
                    state.phase = "leaving";
                    state.deadline = EditorApplication.timeSinceStartup + 30;
                    Save();
                    EditorApplication.isPlaying = false;
                }
                else if (state.phase == "exited")
                {
                    var probe = Probe();
                    Require(!probe || !probe.LightweightAllocated, "Lightweight state was retained after exiting Play.");
                    Require(Resources.FindObjectsOfTypeAll<RuntimeFSMController>().All(asset => !asset || asset.name != state.sourceName || EditorUtility.IsPersistent(asset)), "A native configuration clone leaked after exiting Play.");
                    state.results[state.results.Count - 1].afterExitNoClone = true;
                    state.round++;
                    if (state.round == 2) { state.round = 0; state.mode++; }
                    if (state.mode == 3) { RestoreAndExit(0); return; }
                    state.phase = "prepare";
                    state.deadline = EditorApplication.timeSinceStartup + 30;
                    Save();
                }
            }
            catch (Exception error)
            {
                state.error = error.ToString();
                Save();
                Debug.LogException(error);
                if (EditorApplication.isPlaying) { state.phase = "failedLeaving"; Save(); EditorApplication.isPlaying = false; }
                else RestoreAndExit(1);
            }
        }

        private static void RestoreAndExit(int code)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            try
            {
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)state.originalOptions;
                EditorSettings.enterPlayModeOptionsEnabled = state.originalEnabled;
                var originals = state.originalScenes.Where(scene => !string.IsNullOrEmpty(scene.path)).Select(scene => new SceneSetup { path = scene.path, isActive = scene.active, isLoaded = scene.loaded }).ToArray();
                if (originals.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(originals);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            catch (Exception error) { state.error += "\nRestore: " + error; code = 1; }
            state.complete = true;
            File.WriteAllText(Path.Combine(state.output, "reload-results.json"), JsonUtility.ToJson(state, true), new UTF8Encoding(false));
            SessionState.EraseString(Key);
            Debug.Log($"T3-A Reload matrix finished: rounds={state.results.Count}; result={(code == 0 ? "PASS" : "FAIL")}; original settings restored.");
            EditorApplication.Exit(code);
        }
    }
}
