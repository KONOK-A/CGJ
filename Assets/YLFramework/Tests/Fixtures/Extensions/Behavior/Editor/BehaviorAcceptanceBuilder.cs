using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.StateMachine;

namespace YLFramework.Behavior.Acceptance
{
    public static class BehaviorAcceptanceBuilder
    {
        private const string Root = "Assets/YLFramework/Tests/Fixtures/Extensions/Behavior";

        [MenuItem("Tools/YLFramework/Behavior Acceptance/Generate Owned Fixture")]
        public static void GenerateMenu() => Generate();

        public static string Generate(bool development = false)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before generating the owned fixture. No scene was overwritten.");
            string folder = "GeneratedAcceptance_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(Root, folder);
            folder = Root + "/" + folder;
            var asset = ScriptableObject.CreateInstance<RuntimeFSMController>();
            asset.name = "BehaviorAcceptanceFSM";
            foreach (string name in new[] { "Idle", "Active" })
            {
                var state = new FSMStateNodeData { name = name, defaultState = name == "Idle" };
                state.StateScripts.Add(new FSMStateScriptInfo { className = typeof(BehaviorAcceptanceState).FullName });
                asset.states.Add(state);
            }
            asset.parameters.Add(new FSMParameterData { name = "Go", parameterType = ParameterType.Bool });
            asset.parameters.Add(new FSMParameterData { name = "Fire", parameterType = ParameterType.Trigger });
            var transition = new FSMTransitionData { fromStateName = "Idle", toStateName = "Active" };
            transition.conditions.Add(new FSMConditionData { parameterName = "Go", targetValue = 1, compareType = CompareType.Equal });
            asset.transitions.Add(transition);
            string assetPath = folder + "/BehaviorAcceptanceFSM.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssetIfDirty(asset);
            string scenePath = folder + "/BehaviorAcceptance.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var obj = new GameObject("YL.Behavior.Acceptance");
                SceneManager.MoveGameObjectToScene(obj, scene);
                var probe = obj.AddComponent<BehaviorPlayerProbe>();
                probe.source = asset;
                probe.sourceGuid = AssetDatabase.AssetPathToGUID(assetPath);
                probe.expectedDevelopment = development;
                if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Acceptance scene save failed.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            Debug.Log($"Owned fixture: {scenePath}; source GUID {AssetDatabase.AssetPathToGUID(assetPath)}. Generated, NOT run or Player-verified.");
            return scenePath;
        }

        [MenuItem("Tools/YLFramework/Behavior Acceptance/Build Release Mono Windows64")]
        public static void BuildMono() => Build(ScriptingImplementation.Mono2x, false);

        [MenuItem("Tools/YLFramework/Behavior Acceptance/Build Release IL2CPP Windows64")]
        public static void BuildIL2CPP() => Build(ScriptingImplementation.IL2CPP, false);

        [MenuItem("Tools/YLFramework/Behavior Acceptance/Build Development Mono Windows64")]
        public static void BuildDevelopmentMono() => Build(ScriptingImplementation.Mono2x, true);

        [MenuItem("Tools/YLFramework/Behavior Acceptance/Build Development IL2CPP Windows64")]
        public static void BuildDevelopmentIL2CPP() => Build(ScriptingImplementation.IL2CPP, true);

        private static void Build(ScriptingImplementation expected, bool development)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 || PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone) != expected)
                throw new InvalidOperationException($"Select Windows64 and {expected} in the main Unity integration workspace first. This tool does not modify ProjectSettings.");
            string scene = Generate(development);
            string directory = Path.GetFullPath("Temp/Behavior.Acceptance/" + expected + "/" + (development ? "Development/" : "Release/") + Guid.NewGuid().ToString("N"));
            if (!Directory.Exists(Path.GetFullPath("Temp"))) throw new InvalidOperationException("Missing project Temp parent.");
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, target = BuildTarget.StandaloneWindows64, locationPathName = Path.Combine(directory, "BehaviorAcceptance.exe"), options = development ? BuildOptions.Development : BuildOptions.None });
            Debug.Log($"Behavior {expected} build: {report.summary.result}; development={development}; errors={report.summary.totalErrors}; output={directory}. Player runtime is not yet verified. Run with -batchmode -nographics -logFile <owned-log-path>, require exit 0 and YL_BEHAVIOR_ACCEPTANCE result=PASS.");
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Behavior acceptance Player build failed.");
        }
    }
}
