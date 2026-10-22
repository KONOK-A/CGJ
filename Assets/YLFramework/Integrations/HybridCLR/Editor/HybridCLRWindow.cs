using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    /// <summary>所有副作用仅由按钮显式发起；选中、绘制、打开窗口不保存或生成。</summary>
    public sealed class HybridCLRWindow : EditorWindow
    {
        [SerializeField] private HybridCLRBuildProfile profile;
        [SerializeField] private string playerPath;
        [SerializeField] private string packagePath;
        [SerializeField] private bool includeSample;
        private string summary = "Select a saved profile. No operation has run.";
        private Vector2 scroll;
        private string officialStatus;

        private void OnEnable() => ReadOfficialStatus();

        private void ReadOfficialStatus()
        {
            try
            {
                var installer = new global::HybridCLR.Installer.InstallerController();
                officialStatus = "Package=" + installer.PackageVersion + "; installed=" + (installer.InstalledLibil2cppVersion ?? "Not installed");
            }
            catch (Exception error) { officialStatus = "Official status unavailable: " + error; }
        }

        [MenuItem("YLFramework/Integrations/HybridCLR")]
        private static void Open() => GetWindow<HybridCLRWindow>("HybridCLR");

        private void OnGUI()
        {
            profile = (HybridCLRBuildProfile)EditorGUILayout.ObjectField("Build Profile", profile, typeof(HybridCLRBuildProfile), false);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Resource Recipe", profile ? profile.Recipe : null, typeof(YLFramework.Editor.Resource.ResourceBuildRecipe), false);
            EditorGUILayout.HelpBox("Official 9.0.3, Windows64 IL2CPP, .NET Framework. Configure and save assets explicitly. Patch never regenerates the native base. Signing/decryption keys are supplied by the application to the headless API, not stored here.", MessageType.Info);
            EditorGUILayout.LabelField(officialStatus ?? "Official status not read.", EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Official Settings")) SettingsService.OpenProjectSettings("Project/HybridCLR Settings");
                if (GUILayout.Button("Open Official Installer")) Run(() =>
                {
                    if (!EditorApplication.ExecuteMenuItem("HybridCLR/Installer...")) throw new InvalidOperationException("The official Installer menu is unavailable.");
                });
            }
            playerPath = EditorGUILayout.TextField("New Player .exe", playerPath);
            using (new EditorGUI.DisabledScope(!profile || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer))
            {
                if (GUILayout.Button("Validate Only")) Run(() => { summary = HybridCLRBuildPipeline.Validate(profile).Summary; ReadOfficialStatus(); });
                if (GUILayout.Button("Generate, Publish And Build New Base")) Run(() => summary = HybridCLRBuildPipeline.BuildBasePlayer(profile, new BuildPlayerOptions
                {
                    target = profile.Recipe.Target, targetGroup = BuildTargetGroup.Standalone, subtarget = (int)StandaloneBuildSubtarget.Player,
                    locationPathName = playerPath, scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    options = profile.Development ? BuildOptions.Development : BuildOptions.None
                }).Summary);
                if (GUILayout.Button("Publish Patch Against Frozen Base")) Run(() => summary = HybridCLRBuildPipeline.PublishPatch(profile).Summary);
                if (GUILayout.Button("Deploy Current Published Release Explicitly")) Run(() =>
                {
                    var validation = HybridCLRBuildPipeline.Validate(profile);
                    if (!validation.Succeeded) { summary = validation.Summary; return; }
                    var release = ResourcePublisher.ReadCurrent(profile.Recipe.Project, profile.Recipe.Target, profile.Recipe.AppContentCompatibilityId);
                    if (release == null) throw new InvalidOperationException("Publish a release through the selected saved recipe first.");
                    var deployed = ResourceBuiltInDeployer.Deploy(ResourceBuiltInDeployer.Preflight(release));
                    summary = "Deploy: " + deployed.DirectoryPath + "\nCatalogDigest=" + release.Catalog.CatalogDigest;
                });
            }
            packagePath = EditorGUILayout.TextField("Optional .unitypackage", packagePath);
            includeSample = EditorGUILayout.Toggle("Include Isolated Sample", includeSample);
            if (GUILayout.Button("Export Optional Integration Only")) Run(() => { HybridCLRIntegrationExporter.Export(packagePath, includeSample); summary = "Optional export: " + packagePath; });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.SelectableLabel(summary, EditorStyles.textArea, GUILayout.ExpandHeight(true), GUILayout.MinHeight(180));
            EditorGUILayout.EndScrollView();
        }

        private void Run(Action operation)
        {
            try { operation(); }
            catch (Exception error) { summary = error.ToString(); }
            Repaint();
        }
    }
}
