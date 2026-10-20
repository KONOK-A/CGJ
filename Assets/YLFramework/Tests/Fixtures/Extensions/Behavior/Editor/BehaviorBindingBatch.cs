using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Behavior.Acceptance
{
    [InitializeOnLoad]
    public static class BehaviorBindingBatch
    {
        private const string Key = "YLFramework.Behavior.BindingBatch";
        [Serializable] private sealed class State
        {
            public string output, assetPath, scriptPath, typeName, assetGuid, scriptGuid, error;
            public long deadlineTicks;
            public bool complete, passed;
        }
        private static State state;

        static BehaviorBindingBatch()
        {
            string json = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            state = JsonUtility.FromJson<State>(json);
            if (!state.complete) Hook();
        }

        private static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        private static void Save() => SessionState.SetString(Key, JsonUtility.ToJson(state));

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated EditMode BatchMode.");
            string scene = BehaviorAcceptanceBuilder.Generate();
            string folder = Path.GetDirectoryName(scene).Replace('\\', '/');
            string assetPath = folder + "/BehaviorAcceptanceFSM.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(assetPath);
            var node = new FSMStateNodeData { name = "AutoBound" };
            asset.AddState(node);
            AssetDatabase.SaveAssetIfDirty(asset);
            state = new State { output = BehaviorUnityBatch.Output(), assetPath = assetPath, assetGuid = AssetDatabase.AssetPathToGUID(assetPath), scriptPath = folder + "/T3ABoundState" + Guid.NewGuid().ToString("N") + ".cs", deadlineTicks = DateTime.UtcNow.AddMinutes(2).Ticks };
            Save();
            Hook();
            // 使用产品Create And Add入口及其待绑定队列，不在测试中手动附加脚本。
            var creator = Type.GetType("YLFramework.StateMachine.FSMStateCreator, YLFramework.Editor", true);
            var create = creator.GetMethod("CreateFSMState", BindingFlags.Static | BindingFlags.NonPublic);
            if (!(bool)create.Invoke(null, new object[] { state.scriptPath, false, asset, node })) throw new InvalidOperationException("Create And Add refused the owned new path.");
        }

        private static void Tick()
        {
            if (state == null || state.complete || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (DateTime.UtcNow.Ticks > state.deadlineTicks) throw new TimeoutException("Compiled state was not safely attached after import/reload.");
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(state.scriptPath);
                var type = script ? script.GetClass() : null;
                if (type == null) return;
                var asset = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(state.assetPath);
                var node = asset ? asset.GetStateNodeData("AutoBound") : null;
                string guid = AssetDatabase.AssetPathToGUID(state.scriptPath);
                if (node == null || !node.StateScripts.Any(info => info.guid == guid && info.className == type.FullName)) return;
                AssetDatabase.SaveAssetIfDirty(asset);
                AssetDatabase.ImportAsset(state.assetPath, ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(state.assetPath);
                reloaded.ClearCache();
                if (!reloaded.GetStateNodeData("AutoBound").StateScripts.Any(info => info.guid == guid && info.className == type.FullName)) throw new InvalidOperationException("Bound script did not survive asset reimport.");
                if (!reloaded.ValidateConfiguration(out string error)) throw new InvalidOperationException(error);
                state.typeName = type.FullName;
                state.scriptGuid = guid;
                state.passed = true;
                Finish(0);
            }
            catch (Exception error) { state.error = error.ToString(); Debug.LogException(error); Finish(1); }
        }

        private static void Finish(int code)
        {
            state.complete = true;
            EditorApplication.update -= Tick;
            File.WriteAllText(Path.Combine(state.output, "binding-results.json"), JsonUtility.ToJson(state, true), new UTF8Encoding(false));
            SessionState.EraseString(Key);
            Debug.Log($"T3-A Create And Add actual compile/import/reload/reimport: {(state.passed ? "PASS" : "FAIL")}");
            EditorApplication.Exit(code);
        }
    }
}
