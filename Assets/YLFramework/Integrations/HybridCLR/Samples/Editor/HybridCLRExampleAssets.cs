using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Integrations.HybridCLR.Editor;

namespace YLFramework.Samples.HybridCLR.Editor
{
    public static class HybridCLRExampleAssets
    {
        /// <summary>只创建一个全新自有样例目录；不配置官方热更名单、切换后端或保存场景。</summary>
        public static HybridCLRBuildProfile Create(string assetDirectory, string persistentBaselineDirectory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Create sample assets in a settled Editor.");
            if (string.IsNullOrEmpty(assetDirectory) || !assetDirectory.StartsWith("Assets/", StringComparison.Ordinal) ||
                assetDirectory.Contains('\\') || assetDirectory.Contains("..") || Directory.Exists(assetDirectory) || File.Exists(assetDirectory))
                throw new ArgumentException("Choose a new explicit Assets directory.", nameof(assetDirectory));
            var parent = Path.GetDirectoryName(assetDirectory).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) throw new DirectoryNotFoundException(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetDirectory));
            AssetDatabase.CreateFolder(assetDirectory, "Generated"); AssetDatabase.CreateFolder(assetDirectory + "/Generated", "hybridclr");
            AssetDatabase.CreateFolder(assetDirectory, "Data");
            var textPath = assetDirectory + "/Data/message.json";
            File.WriteAllText(textPath, "[{\"Id\":1,\"Text\":\"YL Resource configuration\"}]");
            AssetDatabase.ImportAsset(textPath, ImportAssetOptions.ForceSynchronousImport);
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject source = null;
            var prefabPath = assetDirectory + "/Data/HotView.prefab";
            try
            {
                source = new GameObject("HotView") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source, preview);
                source.hideFlags = HideFlags.None;
                var entry = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "YLFramework.HybridCLR.Sample.Entry");
                source.AddComponent(entry.GetType("YLFramework.Samples.HybridCLR.HotUpdate.ExampleBehaviour", true));
                if (!PrefabUtility.SaveAsPrefabAsset(source, prefabPath)) throw new IOException("Could not save the owned hot-script prefab.");
            }
            finally
            {
                if (source) UnityEngine.Object.DestroyImmediate(source);
                EditorSceneManager.ClosePreviewScene(preview);
            }
            var project = UnityEngine.ScriptableObject.CreateInstance<ResourceProject>();
            project.ProjectId = "hybridclr-example-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            project.SourceDirectories.Add(new ResourceSourceDirectory { Guid = AssetDatabase.AssetPathToGUID(assetDirectory + "/Generated") });
            project.SourceDirectories.Add(new ResourceSourceDirectory { Guid = AssetDatabase.AssetPathToGUID(assetDirectory + "/Data") });
            project.Groups.Add(new ResourceGroup { Id = "code", Selections = new List<ResourceSelection>
                { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(assetDirectory + "/Generated/hybridclr"), Extensions = new[] { ".bytes", ".json" } } } });
            project.Groups.Add(new ResourceGroup { Id = "configuration", Selections = new List<ResourceSelection>
                { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(textPath), Address = "sample/message.json" } } });
            project.Groups.Add(new ResourceGroup { Id = "prefab", Selections = new List<ResourceSelection>
                { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(prefabPath), Address = "sample/hot-view" } } });
            AssetDatabase.CreateAsset(project, assetDirectory + "/Project.asset");
            var recipe = UnityEngine.ScriptableObject.CreateInstance<ResourceBuildRecipe>();
            recipe.Project = project; recipe.Target = BuildTarget.StandaloneWindows64; recipe.Action = ResourceBuildAction.BuildAndPublish;
            recipe.AppContentCompatibilityId = "hybridclr-base-v1";
            AssetDatabase.CreateAsset(recipe, assetDirectory + "/Recipe.asset");
            var profile = UnityEngine.ScriptableObject.CreateInstance<HybridCLRBuildProfile>();
            profile.Recipe = recipe; profile.Development = EditorUserBuildSettings.development;
            profile.GeneratedAssetDirectory = assetDirectory + "/Generated"; profile.BaselineDirectory = persistentBaselineDirectory;
            AssetDatabase.CreateAsset(profile, assetDirectory + "/Profile.asset");
            AssetDatabase.SaveAssetIfDirty(project); AssetDatabase.SaveAssetIfDirty(recipe); AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }
    }
}
