using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFABManager;
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
            string projectPath = root + "/YLCoreSmokeModule.asset";
            foreach (string guid in AssetDatabase.FindAssets("t:XFABProject"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<XFABProject>(path);
                Require(candidate.name != YLCoreSmokeModule.Project || path == projectPath,
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
                Require(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "/" + name + ".json"), "Missing startup JSON fixture: " + name);
            CreateOrValidateScene(YLCoreSmokeModule.ScenePath, true);
            CreateOrValidateScene(YLCoreSmokeModule.HarnessPath, false);

            var project = AssetDatabase.LoadAssetAtPath<XFABProject>(projectPath);
            if (!project)
            {
                Require(!File.Exists(projectPath) && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(projectPath)),
                    "Existing project asset has an unexpected type.");
                project = ScriptableObject.CreateInstance<XFABProject>();
                project.name = YLCoreSmokeModule.Project;
                project.suffix = ".unity3d";
                project.version = "1.0.0";
                AssetDatabase.CreateAsset(project, projectPath);
                AddBundle(project, "views", viewPath);
                AddBundle(project, "panels", panelPath);
                AddBundle(project, "text", new[] { tablePath }.Concat(failureTables.Select(name => root + "/" + name + ".json")).ToArray());
                AddBundle(project, "scenes", YLCoreSmokeModule.ScenePath, YLCoreSmokeModule.HarnessPath);
                AssetDatabase.SaveAssetIfDirty(project);
            }

            Require(project.name == YLCoreSmokeModule.Project && project.suffix == ".unity3d" && project.version == "1.0.0",
                "Existing project identity does not match; it was not overwritten.");
            Require(project.assetBundles != null && project.assetBundles.Count == 4, "Unexpected fixture bundles.");
            ValidateBundle(project, "views", viewPath);
            ValidateBundle(project, "panels", panelPath);
            ValidateBundle(project, "text", new[] { tablePath }.Concat(failureTables.Select(name => root + "/" + name + ".json")).ToArray());
            ValidateBundle(project, "scenes", YLCoreSmokeModule.ScenePath, YLCoreSmokeModule.HarnessPath);
            // No global SaveAssets/RefreshProjects/AB build or Profile mutation.
        }

        private static void AddBundle(XFABProject project, string name, params string[] paths)
        {
            var bundle = new XFABAssetBundle(name, project.name);
            foreach (string path in paths) bundle.AddFile(path);
            Require(project.AddAssetBundle(bundle), "Could not register fixture bundle " + name);
        }

        private static void ValidateBundle(XFABProject project, string name, params string[] paths)
        {
            var bundles = project.assetBundles.Where(bundle => bundle.bundle_name == name).ToArray();
            Require(bundles.Length == 1 && bundles[0].projectName == project.name, "Unexpected bundle " + name);
            Require(bundles[0].Files.Count == paths.Length &&
                bundles[0].Files.All(file => file.type == XFBundleFileType.File) &&
                bundles[0].GetAllAssetPaths().OrderBy(path => path).SequenceEqual(paths.OrderBy(path => path)),
                "Existing bundle paths do not match; they were not overwritten: " + name);
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
