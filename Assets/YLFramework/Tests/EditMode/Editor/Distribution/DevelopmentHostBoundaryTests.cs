using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YLFramework.Tests
{
    public class DevelopmentHostBoundaryTests
    {
        private const string ScenePath = "Assets/Scenes/YLFrameworkDev.unity";
        private const string FrameworkPrefabPath = "Assets/YLFramework/Prefabs/YLFramework.prefab";
        private const string GameRoot = "Assets/Games/MainModule";

        [Test]
        public void DefaultBuildEntryAndIdentityBelongToFrameworkDevelopmentHost()
        {
            Assert.That(PlayerSettings.companyName, Is.EqualTo("TANGENT LINE"));
            Assert.That(PlayerSettings.productName, Is.EqualTo("YLFrameworkDev"));
            Assert.That(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone),
                Is.EqualTo("com.tangentline.ylframeworkdev"));
            CollectionAssert.AreEqual(new[] { ScenePath },
                EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path));
        }

        [Test]
        public void DevelopmentHostHasNoMainGameDependencies()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Assert.That(scene, Is.Not.Null, "The real development scene must exist before inspecting its closure.");
            var dependencies = AssetDatabase.GetDependencies(ScenePath, true);
            CollectionAssert.Contains(dependencies, ScenePath);
            CollectionAssert.Contains(dependencies, FrameworkPrefabPath);
            Assert.That(dependencies.Any(path => path.StartsWith(GameRoot, StringComparison.Ordinal)), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(GameRoot), Is.False);
            Assert.That(Directory.Exists(GameRoot), Is.False);
            Assert.That(File.Exists(GameRoot + ".meta"), Is.False);
            Assert.That(File.Exists("Assets/Scenes/Main.unity"), Is.False);
            Assert.That(File.Exists("Assets/Scenes/Main.unity.meta"), Is.False);
            Assert.That(Directory.Exists("Assets/StreamingAssets/MainModule"), Is.False);
            Assert.That(Directory.Exists("Assets/Atlas(AutoGenerate)/MainModule"), Is.False);
            Assert.That(Directory.Exists("AssetBundles/MainModule"), Is.False);

            foreach (var profile in new[] { AssembliesType.Editor, AssembliesType.Player })
            {
                Assert.That(CompilationPipeline.GetAssemblies(profile)
                    .Any(assembly => assembly.name.StartsWith("Project.Game.MainModule", StringComparison.Ordinal)),
                    Is.False, profile.ToString());
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                .Where(value => value.GetName().Name.StartsWith("YLFramework", StringComparison.Ordinal)))
            {
                Assert.That(assembly.GetReferencedAssemblies()
                    .Any(reference => reference.Name.StartsWith("Project.Game.", StringComparison.Ordinal)),
                    Is.False, assembly.FullName);
            }
        }

        [Test]
        public void DevelopmentSceneUsesOneBoundFrameworkPrefabAndHasNoMissingScripts()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<Transform>(true)).Select(transform => transform.gameObject).ToArray();
                var roots = objects.SelectMany(value => value.GetComponents<YLFrameworkRoot>()).ToArray();
                Assert.That(roots, Has.Length.EqualTo(1));
                Assert.That(AssetDatabase.GetAssetPath(
                    PrefabUtility.GetCorrespondingObjectFromSource(roots[0].gameObject)),
                    Is.EqualTo(FrameworkPrefabPath));
                using var rootData = new SerializedObject(roots[0]);
                Assert.That(rootData.FindProperty("panelRoot").objectReferenceValue, Is.Not.Null);
                Assert.That(objects.SelectMany(value => value.GetComponents<Camera>())
                    .Any(camera => camera.enabled && camera.gameObject.activeInHierarchy &&
                        camera.CompareTag("MainCamera")), Is.True);
                foreach (var value in objects)
                {
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(value), Is.Zero, value.name);
                }
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded)
                    {
                        SceneManager.SetActiveScene(previous);
                    }
                }
            }
        }

        [Test]
        public void DevelopmentPlayerSettingsHaveNoGameBrandingReferences()
        {
            using var settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var property = settings.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || !property.objectReferenceValue)
                {
                    continue;
                }

                Assert.That(AssetDatabase.GetAssetPath(property.objectReferenceValue)
                    .StartsWith(GameRoot, StringComparison.Ordinal), Is.False, property.propertyPath);
            }
        }
    }
}
