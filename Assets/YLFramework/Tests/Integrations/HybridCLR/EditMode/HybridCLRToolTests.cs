using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Editor.Resource;
using YLFramework.Integrations.HybridCLR.Editor;
using YLFramework.Samples.HybridCLR.Editor;

namespace YLFramework.Tests.Integrations.HybridCLR
{
    public class HybridCLRToolTests
    {
        [Test]
        public void IsolatedSampleAssetsAreExplicitAndDoNotConfigureTheOfficialSettingsOrPlayer()
        {
            var root = "Assets/YLFramework/Tests/Fixtures/HybridCLRTool-" + Guid.NewGuid().ToString("N");
            var settings = File.Exists("ProjectSettings/HybridCLRSettings.asset") ? File.ReadAllBytes("ProjectSettings/HybridCLRSettings.asset") : null;
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            var development = EditorUserBuildSettings.development;
            try
            {
                var profile = HybridCLRExampleAssets.Create(root, Path.GetFullPath("Build/HybridCLR/ToolTest-" + Guid.NewGuid().ToString("N")));
                Assert.That(profile.Recipe.Action, Is.EqualTo(ResourceBuildAction.BuildAndPublish));
                Assert.That(profile.Recipe.Project.Groups.Count, Is.EqualTo(3));
                Assert.That(profile.Recipe.Project.SourceDirectories.Count, Is.EqualTo(2));
                Assert.That(profile.Recipe.Project.Groups.Single(g => g.Id == "configuration").Selections.Single().Address, Is.EqualTo("sample/message.json"));
                Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(profile.Recipe.Project), "The empty code group must not be buildable until generation succeeds.");
                Assert.That(EditorUtility.IsDirty(profile) || EditorUtility.IsDirty(profile.Recipe) || EditorUtility.IsDirty(profile.Recipe.Project), Is.False);
                Assert.Throws<ArgumentException>(() => HybridCLRExampleAssets.Create(root, profile.BaselineDirectory));
                Assert.That(File.Exists("ProjectSettings/HybridCLRSettings.asset"), Is.EqualTo(settings != null));
                if (settings != null) CollectionAssert.AreEqual(settings, File.ReadAllBytes("ProjectSettings/HybridCLRSettings.asset"));
                Assert.That(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone), Is.EqualTo(backend));
                Assert.That(EditorUserBuildSettings.development, Is.EqualTo(development));
            }
            finally { if (AssetDatabase.IsValidFolder(root)) AssetDatabase.DeleteAsset(root); }
        }

        [UnityTest]
        public IEnumerator TheSampleSupportExercisesAnActualAsyncContinuation()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "YLFramework.HybridCLR.Sample.Support");
            var method = assembly.GetType("YLFramework.Samples.HybridCLR.HotUpdate.VersionInfo", true).GetMethod("DescribeAsync");
            Assert.That(method, Is.Not.Null);
            var task = (System.Threading.Tasks.Task<string>)method.Invoke(null, null);
            for (var i = 0; i < 240 && !task.IsCompleted; i++) yield return null;
            Assert.That(task.IsCompleted, Is.True); Assert.That(task.IsFaulted, Is.False);
            Assert.That(task.Result, Is.EqualTo("v1-async"));
        }

        [Test]
        public void SamplePrefabHasARealHotScriptWithoutDirtyingTheOpenScene()
        {
            var root = "Assets/YLFramework/Tests/Fixtures/HybridCLRPrefab-" + Guid.NewGuid().ToString("N");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); var wasDirty = scene.isDirty;
            try
            {
                var profile = HybridCLRExampleAssets.Create(root, Path.GetFullPath("Build/HybridCLR/PrefabTest-" + Guid.NewGuid().ToString("N")));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Data/HotView.prefab");
                Assert.That(prefab, Is.Not.Null);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero);
                Assert.That(prefab.GetComponents<Component>().Any(c => c && c.GetType().FullName == "YLFramework.Samples.HybridCLR.HotUpdate.ExampleBehaviour"), Is.True);
                Assert.That(profile.Recipe.Project.Groups.Single(g => g.Id == "prefab").Selections.Single().Address, Is.EqualTo("sample/hot-view"));
                Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
            }
            finally { if (AssetDatabase.IsValidFolder(root)) AssetDatabase.DeleteAsset(root); }
        }

        [UnityTest]
        public IEnumerator OpeningTheWindowDoesNotGenerateInstallOrSaveAnyAssets()
        {
            var settings = File.Exists("ProjectSettings/HybridCLRSettings.asset") ? File.ReadAllBytes("ProjectSettings/HybridCLRSettings.asset") : null;
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            var window = ScriptableObject.CreateInstance<HybridCLRWindow>();
            try
            {
                window.Show(); yield return null;
                Assert.That(EditorUtility.scriptCompilationFailed, Is.False);
                Assert.That(File.Exists("ProjectSettings/HybridCLRSettings.asset"), Is.EqualTo(settings != null));
                if (settings != null) CollectionAssert.AreEqual(settings, File.ReadAllBytes("ProjectSettings/HybridCLRSettings.asset"));
                Assert.That(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone), Is.EqualTo(backend));
            }
            finally { window.Close(); }
        }
    }
}
