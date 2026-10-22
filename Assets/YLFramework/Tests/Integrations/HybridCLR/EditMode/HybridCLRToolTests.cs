using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
        public void CreatingAProfileCreatesSavedResourceBindingsWithoutChangingGlobalSettings()
        {
            var root = NewToolRoot();
            var settings = File.Exists("ProjectSettings/HybridCLRSettings.asset") ? File.ReadAllBytes("ProjectSettings/HybridCLRSettings.asset") : null;
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                Assert.That(profile.Recipe, Is.Not.Null);
                Assert.That(profile.Recipe.Project, Is.Not.Null);
                Assert.That(profile.Recipe.Action, Is.EqualTo(ResourceBuildAction.BuildAndPublish));
                Assert.That(profile.Recipe.Target, Is.EqualTo(BuildTarget.StandaloneWindows64));
                Assert.That(profile.Recipe.Project.SourceDirectories.Count, Is.EqualTo(1));
                var selection = profile.Recipe.Project.Groups.Single().Selections.Single();
                Assert.That(AssetDatabase.GUIDToAssetPath(selection.Guid), Is.EqualTo(profile.GeneratedAssetDirectory + "/hybridclr"));
                Assert.That(selection.Extensions, Is.EquivalentTo(new[] { ".bytes", ".json" }));
                Assert.That(EditorUtility.IsDirty(profile) || EditorUtility.IsDirty(profile.Recipe) || EditorUtility.IsDirty(profile.Recipe.Project), Is.False);
                Assert.DoesNotThrow(() => HybridCLRBuildPipeline.ValidateInputPaths(profile));
                Assert.That(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone), Is.EqualTo(backend));
                Assert.That(File.Exists("ProjectSettings/HybridCLRSettings.asset"), Is.EqualTo(settings != null));
                if (settings != null) CollectionAssert.AreEqual(settings, File.ReadAllBytes("ProjectSettings/HybridCLRSettings.asset"));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void CreatingAProfileCannotOverwriteAnExistingAsset()
        {
            var root = NewToolRoot();
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                var path = AssetDatabase.GetAssetPath(profile); var guid = AssetDatabase.AssetPathToGUID(path); var bytes = File.ReadAllBytes(path);
                Assert.Throws<IOException>(() => ProfileTool("Create", path));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void GeneratedResourceMappingIsIdempotentPreservesOtherGroupsAndSupportsUndo()
        {
            var root = NewToolRoot();
            ResourceProject project = null;
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                project = profile.Recipe.Project;
                project.SourceDirectories.Clear(); project.Groups.Clear();
                project.Groups.Add(new ResourceGroup { Id = "user-content" });
                EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
                ProfileTool("MapGeneratedResources", profile);
                Assert.That(project.SourceDirectories.Count, Is.EqualTo(1));
                Assert.That(project.Groups.Select(g => g.Id), Does.Contain("user-content"));
                Assert.That(project.Groups.Sum(g => g.Selections.Count), Is.EqualTo(1));
                ProfileTool("MapGeneratedResources", profile);
                Assert.That(project.SourceDirectories.Count, Is.EqualTo(1));
                Assert.That(project.Groups.Sum(g => g.Selections.Count), Is.EqualTo(1));
                Undo.PerformUndo();
                Assert.That(project.SourceDirectories, Is.Empty);
                Assert.That(project.Groups.Select(g => g.Id), Is.EqualTo(new[] { "user-content" }));
            }
            finally { if (project) Undo.ClearUndo(project); AssetDatabase.DeleteAsset(root); }
        }

        private static string NewToolRoot()
        {
            var name = "HybridCLRUI-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Fixtures", name);
            return "Assets/YLFramework/Tests/Fixtures/" + name;
        }

        [Test]
        public void MappingANewGenerationDirectoryRejectsOldCodeMappingsWithoutChangingTheProject()
        {
            var root = NewToolRoot();
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                var project = profile.Recipe.Project;
                var before = EditorJsonUtility.ToJson(project);
                profile.GeneratedAssetDirectory = root + "/OtherGenerated";
                Assert.Throws<InvalidDataException>(() => ProfileTool("MapGeneratedResources", profile));
                Assert.That(EditorJsonUtility.ToJson(project), Is.EqualTo(before));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void ConfigurationInspectionReportsStaleCodeMappingsEvenWhenTheNewMappingExists()
        {
            var root = NewToolRoot();
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                AssetDatabase.CreateFolder(root, "OtherGenerated"); AssetDatabase.CreateFolder(root + "/OtherGenerated", "hybridclr");
                profile.GeneratedAssetDirectory = root + "/OtherGenerated";
                profile.Recipe.Project.SourceDirectories.Add(new ResourceSourceDirectory { Guid = AssetDatabase.AssetPathToGUID(profile.GeneratedAssetDirectory) });
                profile.Recipe.Project.Groups.Add(new ResourceGroup { Id = "new-code", Selections = new System.Collections.Generic.List<ResourceSelection>
                    { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(profile.GeneratedAssetDirectory + "/hybridclr"), Extensions = new[] { ".bytes", ".json" } } } });
                var result = (HybridCLRProfileInspection)ProfileTool("Inspect", profile);
                Assert.That(result.Errors.Any(e => e.Contains("代码映射") && e.Contains("其他目录")), Is.True,
                    "Preflight must diagnose the old canonical code mapping, not only notice that the new mapping exists.");
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void MappingAvoidsUserGroupIdsCaseInsensitivelyIncludingSuffixes()
        {
            var root = NewToolRoot();
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                var project = profile.Recipe.Project;
                project.SourceDirectories.Clear(); project.Groups.Clear();
                project.Groups.Add(new ResourceGroup { Id = "HybridCLR-Code" });
                project.Groups.Add(new ResourceGroup { Id = "HYBRIDCLR-CODE-2" });
                ProfileTool("MapGeneratedResources", profile);
                Assert.That(project.Groups.Select(g => g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(project.Groups.Count));
                Assert.That(project.Groups.Select(g => g.Id), Does.Contain("HybridCLR-Code").And.Contain("HYBRIDCLR-CODE-2"));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [TestCase("aliases")]
        [TestCase("duplicate")]
        public void InvalidCanonicalCodeSelectionsAreDiagnosedWithoutRewritingUserConfiguration(string invalid)
        {
            var root = NewToolRoot();
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                var project = profile.Recipe.Project;
                var selection = project.Groups.Single().Selections.Single();
                if (invalid == "aliases") selection.Aliases = new[] { "not-valid-for-a-folder" };
                else project.Groups.Add(new ResourceGroup { Id = "duplicate-selection", Selections = new System.Collections.Generic.List<ResourceSelection>
                    { new ResourceSelection { Guid = selection.Guid, Extensions = new[] { ".bytes", ".json" } } } });
                var before = EditorJsonUtility.ToJson(project);
                Assert.Throws<InvalidDataException>(() => ProfileTool("MapGeneratedResources", profile));
                Assert.That(EditorJsonUtility.ToJson(project), Is.EqualTo(before));
                var result = (HybridCLRProfileInspection)ProfileTool("Inspect", profile);
                Assert.That(result.Errors.Any(e => e.Contains("代码目录选择")), Is.True, "Configuration inspection must explain invalid canonical code selections.");
                Assert.That(EditorJsonUtility.ToJson(project), Is.EqualTo(before));
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        [Test]
        public void DeletedCodeFolderIsNotAcceptedThroughItsRecentlyDeletedGuid()
        {
            var root = NewToolRoot();
            try
            {
                var profile = (HybridCLRBuildProfile)ProfileTool("Create", root + "/Game.asset");
                var code = profile.GeneratedAssetDirectory + "/hybridclr";
                Assert.That(AssetDatabase.DeleteAsset(code), Is.True);
                Assert.That(AssetDatabase.IsValidFolder(code), Is.False);
                var result = (HybridCLRProfileInspection)ProfileTool("Inspect", profile);
                Assert.That(result.Errors.Any(e => e.Contains("代码来源目录")), Is.True,
                    "A historical GUID is not evidence that the selected folder still exists.");
            }
            finally { AssetDatabase.DeleteAsset(root); }
        }

        private static object ProfileTool(string method, params object[] arguments)
        {
            var type = typeof(HybridCLRBuildProfile).Assembly.GetType("YLFramework.Integrations.HybridCLR.Editor.HybridCLRProfileTools");
            Assert.That(type, Is.Not.Null, "The window needs an explicit, testable profile-creation and resource-binding action.");
            var action = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(action, Is.Not.Null);
            try { return action.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

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
        public IEnumerator EveryNavigationTabCanBeClickedAndKeepsARealSelectedSkin()
        {
            var window = ScriptableObject.CreateInstance<HybridCLRWindow>();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                window.position = new Rect(180, 100, 900, 600);
                window.ShowUtility(); window.Focus();
                yield return null;
                var style = (GUIStyle)typeof(HybridCLRWindow).GetField("navigationStyle", flags).GetValue(window);
                Assert.That(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(style.name), Is.Not.Null,
                    "Renaming a native Editor style loses its skin-based selected-state painting.");
                foreach (var target in new[] { 1, 2, 3, 0 })
                {
                    window.Repaint(); yield return null;
                    var area = NavigationArea(window);
                    var point = new Vector2(area.x + area.width * (target + .5f) / 4, area.center.y);
                    window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
                    window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
                    yield return null;
                    Assert.That(typeof(HybridCLRWindow).GetField("page", flags).GetValue(window), Is.EqualTo(target), "Each tab must be reachable through actual GUI mouse events, including returning to the first tab.");
                }
            }
            finally { window.Close(); }
        }

        private static Rect NavigationArea(HybridCLRWindow window)
        {
            var field = typeof(HybridCLRWindow).GetField("navigationBounds", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var area = (Rect)field.GetValue(window);
            Assert.That(area.height, Is.EqualTo(30));
            Assert.That(area.width, Is.GreaterThan(window.position.width * .8f));
            return area;
        }

        [UnityTest]
        public IEnumerator NavigationButtonsKeepUniformHeightAndTypography()
        {
            var window = ScriptableObject.CreateInstance<HybridCLRWindow>();
            try
            {
                window.Show(); yield return null;
                var style = (GUIStyle)typeof(HybridCLRWindow).GetField("navigationStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                Assert.That(style.fixedHeight, Is.EqualTo(30));
                Assert.That(style.fixedWidth, Is.Zero);
                Assert.That(style.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(style.fontSize, Is.EqualTo(12));
                Assert.That(style.fontStyle, Is.EqualTo(FontStyle.Bold));
            }
            finally { window.Close(); }
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
