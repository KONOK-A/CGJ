using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;
using YLFramework.Core.Tests;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Editor.Tests
{
    public static class CoreSmokeAssetFixture
    {
        public static void CreateOrValidate()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Create fixtures in Edit Mode only.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Refusing to touch fixtures while any open scene is dirty.");

            string root = YLCoreSmokeModule.FixtureRoot;
            string projectPath = root + "/ResourceProject.asset";
            foreach (string guid in AssetDatabase.FindAssets("t:ResourceProject"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<ResourceProject>(path);
                Require(candidate.ProjectId != YLCoreSmokeModule.Project || path == projectPath,
                    "Fixture project name is already owned by " + path);
            }

            EnsureFolder(root);
            string viewPath = root + "/" + nameof(YLCoreSmokeView) + ".prefab";
            string panelPath = root + "/" + nameof(YLCoreSmokePanel) + ".prefab";
            string tablePath = root + "/" + YLCoreSmokeModule.Table + ".json";
            CreateOrValidatePrefab<YLCoreSmokeView>(viewPath, false);
            CreateOrValidatePrefab<YLCoreSmokePanel>(panelPath, true);
            if (!File.Exists(tablePath))
            {
                Require(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(tablePath)), "Unresolved existing table asset.");
                File.WriteAllText(tablePath, YLCoreSmokeModule.Json);
                AssetDatabase.ImportAsset(tablePath, ImportAssetOptions.ForceSynchronousImport);
            }
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(tablePath);
            Require(text && text.text == YLCoreSmokeModule.Json, "Existing fixture JSON does not match; it was not overwritten.");
            string[] failureTables = { YLCoreSmokeModule.EmptyTable, YLCoreSmokeModule.BlankTable, YLCoreSmokeModule.InvalidTable };
            foreach (string name in failureTables)
            {
                string path = root + "/" + name + ".json";
                if (!File.Exists(path))
                {
                    Require(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)), "Unresolved existing table asset.");
                    File.WriteAllText(path, name == YLCoreSmokeModule.EmptyTable ? "[]" : name == YLCoreSmokeModule.BlankTable ? " " : "invalid JSON");
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
                Require(AssetDatabase.LoadAssetAtPath<TextAsset>(path), "Missing startup JSON fixture: " + name);
            }
            CreateOrValidateScene(YLCoreSmokeModule.ScenePath, true);
            CreateOrValidateScene(YLCoreSmokeModule.HarnessPath, false);

            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(projectPath);
            if (!project)
            {
                Require(!File.Exists(projectPath) && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(projectPath)),
                    "Existing project asset has an unexpected type.");
                project = ScriptableObject.CreateInstance<ResourceProject>();
                project.ProjectId = YLCoreSmokeModule.Project;
                project.DisplayName = "Core Resource Fixture";
                project.SourceDirectories.Add(new ResourceSourceDirectory { Guid = AssetDatabase.AssetPathToGUID(root) });
                AssetDatabase.CreateAsset(project, projectPath);
                AddBundle(project, "views", viewPath);
                AddBundle(project, "panels", panelPath);
                AddBundle(project, "text", new[] { tablePath }.Concat(failureTables.Select(name => root + "/" + name + ".json")).ToArray());
                AddBundle(project, "scenes", YLCoreSmokeModule.ScenePath, YLCoreSmokeModule.HarnessPath);
                EditorUtility.SetDirty(project);
                AssetDatabase.SaveAssetIfDirty(project);
            }

            Require(project.ProjectId == YLCoreSmokeModule.Project && project.SourceDirectories != null && project.SourceDirectories.Count == 1 &&
                project.SourceDirectories.Any(x => x != null && x.Guid == AssetDatabase.AssetPathToGUID(root) && x.AddressPrefix == ""),
                "Existing project identity does not match; it was not overwritten.");
            Require(project.Groups != null && project.Groups.Count == 4, "Unexpected fixture groups.");
            ValidateBundle(project, "views", viewPath);
            ValidateBundle(project, "panels", panelPath);
            ValidateBundle(project, "text", new[] { tablePath }.Concat(failureTables.Select(name => root + "/" + name + ".json")).ToArray());
            ValidateBundle(project, "scenes", YLCoreSmokeModule.ScenePath, YLCoreSmokeModule.HarnessPath);
            ResourceBuildMap.Create(project);
        }

        public static ResourceConfiguration CreateConfiguration()
        {
            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(YLCoreSmokeModule.FixtureRoot + "/ResourceProject.asset");
            Require(project, "Create the independent YL fixture in Edit Mode first.");
            return new ResourceEditorSimulation(new[] { project }).CreateConfiguration();
        }

        private static void AddBundle(ResourceProject project, string name, params string[] paths)
        {
            project.Groups.Add(new ResourceGroup { Id = name, Selections = paths.Select(path => new ResourceSelection
                { Guid = AssetDatabase.AssetPathToGUID(path), Address = Path.GetFileNameWithoutExtension(path) }).ToList() });
        }

        private static void ValidateBundle(ResourceProject project, string name, params string[] paths)
        {
            var groups = project.Groups.Where(group => group.Id == name).ToArray();
            Require(groups.Length == 1 && groups[0].Selections.Select(x => AssetDatabase.GUIDToAssetPath(x.Guid))
                .OrderBy(x => x).SequenceEqual(paths.OrderBy(x => x)), "Unexpected fixture group " + name);
        }

        private static void CreateOrValidatePrefab<T>(string path, bool panel) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                Require(!File.Exists(path) && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)),
                    "Existing prefab asset has an unexpected type: " + path);
                Scene previous = SceneManager.GetActiveScene();
                Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(temporary);
                    var obj = panel ? new GameObject(typeof(T).Name, typeof(RectTransform)) : new GameObject(typeof(T).Name);
                    obj.AddComponent<T>();
                    prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
                    Require(prefab, "Could not save fixture prefab " + path);
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(temporary, true);
                }
            }
            Require(prefab.name == typeof(T).Name && prefab.activeSelf && prefab.GetComponent<T>() && prefab.transform.childCount == 0,
                "Existing fixture prefab root does not match: " + path);
            Require(!prefab.GetComponent<Canvas>(), "Fixture prefab must not contain a Canvas: " + path);
            if (panel) Require(prefab.GetComponent<RectTransform>(), "Panel root must be a RectTransform.");
        }

        private static void CreateOrValidateScene(string path, bool entry)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene temporary = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(SceneManager.GetSceneAt(i).path != path, "Close the fixture scene before validating it: " + path);
            try
            {
                if (File.Exists(path))
                {
                    Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), "Existing scene has an unexpected type.");
                    temporary = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                else
                {
                    Require(string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)), "Unresolved existing scene asset.");
                    temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(temporary);
                    if (entry) new GameObject(nameof(YLCoreSmokeSceneEntry)).AddComponent<YLCoreSmokeSceneEntry>();
                    Require(EditorSceneManager.SaveScene(temporary, path), "Could not save fixture scene.");
                }
                var roots = temporary.GetRootGameObjects();
                Require(entry ? roots.Length == 1 && roots[0].GetComponent<YLCoreSmokeSceneEntry>() &&
                    roots[0].activeSelf && roots[0].transform.childCount == 0 && roots[0].GetComponents<Component>().Length == 2 : roots.Length == 0,
                    "Existing fixture scene does not match; it was not overwritten: " + path);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Require(path.StartsWith(YLCoreSmokeModule.FixtureRoot, StringComparison.Ordinal) ||
                YLCoreSmokeModule.FixtureRoot.StartsWith(path + "/", StringComparison.Ordinal), "Folder outside fixture ancestry.");
            EnsureFolder(parent);
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))), "Could not create " + path);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
