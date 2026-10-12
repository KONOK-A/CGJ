#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YLFramework.Core.Tests
{
    /// <summary>5.5 同域真实双 Play 的临时状态 owner；调用前必须单独获得 Logger 隔离授权。</summary>
    public static class RuntimeAcceptanceDriver
    {
        public const string ScenePath = "Assets/YLFramework/Tests/Core/Acceptance/Runtime55.unity";
        private static SceneSetup[] scenes;
        private static bool enabled, failed, armed;
        private static EnterPlayModeOptions options;
        private static ILogHandler handler, native;
        private static int enters, exits, verified, initialRound;
        private static double deadline, next;
        private static string log;
        private static string phase;

        public static string Begin(string url, string logPath)
        {
            if (armed || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Acceptance requires an idle Editor.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Do not overwrite a dirty scene.");
            scenes = EditorSceneManager.GetSceneManagerSetup();
            enabled = EditorSettings.enterPlayModeOptionsEnabled;
            options = EditorSettings.enterPlayModeOptions;
            handler = Debug.unityLogger.logHandler;
            native = (ILogHandler)Activator.CreateInstance(typeof(Debug).Assembly.GetType("UnityEngine.DebugLogHandler"), true);
            enters = exits = verified = 0; initialRound = RuntimeAcceptanceEntry.Round;
            deadline = EditorApplication.timeSinceStartup + 60; next = 0;
            failed = false; log = logPath; armed = true; phase = "entering";
            Debug.unityLogger.logHandler = native;
            try
            {
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Runtime55.Acceptance");
                go.AddComponent<AudioListener>();
                var entry = go.AddComponent<RuntimeAcceptanceEntry>();
                entry.ColorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YLFramework/Prefabs/ColorSelector.prefab");
                entry.ServerUrl = url;
                if (!entry.ColorPrefab || !EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Temporary acceptance scene creation failed.");
                File.WriteAllText(log, "BEGIN domain=" + AppDomain.CurrentDomain.Id + " options=" +
                    EditorSettings.enterPlayModeOptions + Environment.NewLine);
                RuntimeAcceptanceEntry.Result = null;
                EditorApplication.playModeStateChanged += State;
                EditorApplication.update += Tick;
                // 由真实 Editor Play 入口发起首轮；不在 Roslyn/MCP 的调用栈内触发切换。
                return log;
            }
            catch { failed = true; Restore(); throw; }
        }

        private static void State(PlayModeStateChange state)
        {
            Debug.unityLogger.logHandler = native;
            Append("STATE " + state + " domain=" + AppDomain.CurrentDomain.Id +
                " result=" + RuntimeAcceptanceEntry.Result + Environment.NewLine);
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                enters++; phase = "running"; deadline = EditorApplication.timeSinceStartup + 35;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            exits++;
            if (enters != exits || RuntimeAcceptanceEntry.Result != "READY round=" + (initialRound + enters)) failed = true;
            if (!failed)
            {
                try
                {
                    RuntimeAcceptanceEntry.VerifyExit(initialRound + enters);
                    if (UnityEngine.Object.FindObjectsOfType<YLFramework.Foundation.CoroutineStarter>(true).Length != 0)
                        throw new InvalidOperationException("Real exit left a Foundation coroutine host.");
                    verified++;
                    Append("EXIT_PASS round=" + exits + Environment.NewLine);
                }
                catch (Exception error)
                {
                    failed = true;
                    Append("FAIL " + error + Environment.NewLine);
                }
            }
            if (exits == 1 && !failed) { phase = "between"; next = EditorApplication.timeSinceStartup + .2; }
            else Restore();
        }

        private static void Tick()
        {
            if (phase != "between" && EditorApplication.timeSinceStartup > deadline)
            {
                failed = true;
                Append("FAIL timeout phase=" + phase + Environment.NewLine);
                if (!EditorApplication.isPlayingOrWillChangePlaymode) { Restore(); return; }
                phase = "leaving"; deadline = EditorApplication.timeSinceStartup + 15;
                EditorApplication.isPlaying = false;
                return;
            }
            if (EditorApplication.isPlaying)
            {
                var result = RuntimeAcceptanceEntry.Result;
                if (phase == "running" && result != null && (result.StartsWith("READY", StringComparison.Ordinal) || result.StartsWith("FAILED", StringComparison.Ordinal)))
                {
                    failed |= result.StartsWith("FAILED", StringComparison.Ordinal);
                    phase = "leaving"; deadline = EditorApplication.timeSinceStartup + 15;
                    EditorApplication.isPlaying = false;
                }
            }
            else if (next > 0 && EditorApplication.timeSinceStartup >= next)
            {
                next = 0;
                phase = "entering"; deadline = EditorApplication.timeSinceStartup + 15;
                RuntimeAcceptanceEntry.Result = null;
                EditorApplication.isPlaying = true;
            }
        }

        public static void Restore()
        {
            if (!armed) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                failed = true; phase = "leaving"; deadline = EditorApplication.timeSinceStartup + 15;
                EditorApplication.isPlaying = false;
                return;
            }
            failed |= enters != 2 || exits != 2 || verified != 2;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= State;
            var errors = new List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); failed = true; } }
            try
            {
                Attempt(() => EditorSettings.enterPlayModeOptionsEnabled = enabled);
                Attempt(() => EditorSettings.enterPlayModeOptions = options);
                Attempt(() => EditorSceneManager.RestoreSceneManagerSetup(scenes));
                Append((failed ? "FAILED" : "DOUBLE_PLAY_PASS") + " enters=" + enters + " exits=" + exits + " verified=" + verified +
                    " optionsRestored=" + EditorSettings.enterPlayModeOptionsEnabled + ":" + EditorSettings.enterPlayModeOptions + Environment.NewLine);
            }
            finally { Debug.unityLogger.logHandler = handler; armed = errors.Count != 0; }
            if (errors.Count != 0) throw new AggregateException("Acceptance state restoration incomplete; Restore can be retried.", errors);
        }

        private static void Append(string message)
        {
            try { File.AppendAllText(log, message); }
            catch (Exception error) { failed = true; Debug.LogError("RUNTIME55 evidence log failed: " + error); }
        }
    }
}
#endif
