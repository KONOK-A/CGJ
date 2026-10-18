using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public sealed class ResourceMultiDirectoryTests
    {
        private string root;
        private ResourceProject project;

        [SetUp]
        public void SetUp()
        {
            root = "Assets/YLFramework/Tests/Fixtures/MultiDirectory-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Fixtures", Path.GetFileName(root));
            foreach (var name in new[] { "First", "Second", "Unused" }) AssetDatabase.CreateFolder(root, name);
            project = ScriptableObject.CreateInstance<ResourceProject>();
            project.ProjectId = "multi-" + Guid.NewGuid().ToString("N");
            project.SourceDirectories.Add(new ResourceSourceDirectory { Guid = AssetDatabase.AssetPathToGUID(root + "/First") });
            project.Groups.Add(new ResourceGroup { Id = "main" });
            AssetDatabase.CreateAsset(project, root + "/Project.asset");
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(root);

        [Test]
        public void CurrentModelsExposeOnlyTheExplicitDirectoryAndAtlasOwnershipContract()
        {
            const BindingFlags members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in new[] { "AssetRootGuid", "DirectoryConfigurationVersion", "legacyAssetRootGuid", "DirectoryDefinitions" })
                Assert.That(typeof(ResourceProject).GetMember(name, members), Is.Empty, "ResourceProject must not expose a second directory model: " + name);
            Assert.That(typeof(ResourceProject).GetField("SourceDirectories", members)?.FieldType,
                Is.EqualTo(typeof(List<ResourceSourceDirectory>)));
            foreach (var name in new[] { "LayoutVersion", "RootGuid" })
                Assert.That(typeof(ResourceAtlasConfiguration).GetMember(name, members), Is.Empty, "Atlas ownership must not depend on a legacy layout: " + name);
            Assert.That(typeof(ResourceBuildMap).GetMember("AssetRoot", members), Is.Empty, "BuildMap must expose the complete source collection, never a single-root shell.");
            var factories = typeof(ResourceAtlasGenerator).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.Name == "CreateConfiguration").ToArray();
            Assert.That(factories.Length, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { typeof(ResourceProject), typeof(string), typeof(string) },
                factories.Single().GetParameters().Select(x => x.ParameterType));
            Assert.That(factories.Single().GetParameters().Any(x => x.IsOptional), Is.False, "Every caller must explicitly choose the generation parent and folder.");
        }

        [Test]
        public void OneGroupCanSelectTwoRegisteredDirectoriesWithoutCollectingUnselectedFiles()
        {
            var a = Text("First/a.txt"); var b = Text("Second/b.txt"); Text("Second/unselected.txt");
            Configure(("First", ""), ("Second", "audio"));
            ResourceEditorOperations.AddSelections(project, 0, new[] { AssetDatabase.LoadMainAssetAtPath(a), AssetDatabase.LoadMainAssetAtPath(b) });
            var map = ResourceBuildMap.Create(project);
            CollectionAssert.AreEquivalent(new[] { "a.txt", "audio/b.txt" }, map.Assets.Select(x => x.Address.Path));
            CollectionAssert.AreEquivalent(new[] { a, b }, map.Bundles.SelectMany(x => x.AssetPaths));
        }

        [Test]
        public void ReorderingOrAddingDirectoriesDoesNotRenameExistingAddresses()
        {
            var a = Text("First/a.txt"); var b = Text("Second/b.txt");
            project.Groups[0].Selections.Add(Select(a)); project.Groups[0].Selections.Add(Select(b));
            Configure(("First", ""), ("Second", "audio"));
            CollectionAssert.AreEquivalent(new[] { "a.txt", "audio/b.txt" }, ResourceBuildMap.Create(project).Assets.Select(x => x.Address.Path));
            Configure(("Unused", "extra"), ("Second", "audio"), ("First", ""));
            CollectionAssert.AreEquivalent(new[] { "a.txt", "audio/b.txt" }, ResourceBuildMap.Create(project).Assets.Select(x => x.Address.Path));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrEmptySourceCollectionCannotFallBackToTheProjectFolderOrExistingSelections(bool missing)
        {
            var path = Text("First/a.txt"); project.Groups[0].Selections.Add(Select(path));
            var source = File.ReadAllBytes(path); var meta = File.ReadAllBytes(path + ".meta");
            Configure();
            if (missing) project.SourceDirectories = null;
            Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(project));
            Assert.Throws<InvalidOperationException>(() => ResourceWorkbenchGUI.DisplayPaths(project));
            Assert.That(project.Groups[0].Selections.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(source, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta"));
        }

        [Test]
        public void RemovingUsedDirectoryKeepsTheSelectionAndSourceButBlocksTheBuild()
        {
            var a = Text("First/a.txt"); var b = Text("Second/b.txt");
            project.Groups[0].Selections.Add(Select(a)); project.Groups[0].Selections.Add(Select(b));
            Configure(("First", ""), ("Second", "audio"));
            Assert.That(ResourceBuildMap.Create(project).Assets.Count, Is.EqualTo(2));
            Configure(("First", ""));
            var error = Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(project));
            Assert.That(error.Message, Does.Contain(b));
            Assert.That(project.Groups[0].Selections.Count, Is.EqualTo(2));
            Assert.That(File.Exists(b), Is.True);
        }

        [Test]
        public void DuplicateAndNestedDirectoriesAreRejectedRatherThanUsingTheFirstMatch()
        {
            project.Groups[0].Selections.Add(Select(Text("First/a.txt")));
            Configure(("First", ""), ("First", "other"));
            Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(project));
            AssetDatabase.CreateFolder(root + "/First", "Child");
            Configure(("First", ""), ("First/Child", "child"));
            Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(project));
        }

        [Test]
        public void AddressCollisionReportsBothActualSourcePaths()
        {
            var a = Text("First/same.txt"); var b = Text("Second/same.txt");
            Configure(("First", ""), ("Second", ""));
            project.Groups[0].Selections.Add(Select(a)); project.Groups[0].Selections.Add(Select(b));
            var error = Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(project));
            Assert.That(error.Message, Does.Contain(a).And.Contain(b).And.Contain("same.txt"));
        }

        [Test]
        public void BrowsingCurrentDirectoriesPreservesAddressAliasAndDoesNotWriteTheAsset()
        {
            var a = Text("First/a.txt");
            Configure(("Second", "other"), ("First", "audio"));
            var selection = Select(a); selection.Aliases = new[] { "stable" }; project.Groups[0].Selections.Add(selection);
            EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
            var before = File.ReadAllBytes(AssetDatabase.GetAssetPath(project));
            var map = ResourceBuildMap.Create(project);
            Assert.That(ResourceWorkbenchGUI.DisplayPaths(project).DisplayAddress(a), Is.EqualTo("audio/a.txt"));
            Assert.That(map.Assets.Single().Address.Path, Is.EqualTo("audio/a.txt"));
            Assert.That(map.Assets.Single().Aliases.Single().Path, Is.EqualTo("stable"));
            Assert.That(EditorUtility.IsDirty(project), Is.False);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(AssetDatabase.GetAssetPath(project)));
        }

        [Test]
        public void SourceTraceAcceptsDirectoryReorderingButRejectsChangedPrefixOrRemovedSource()
        {
            project.Groups[0].Selections.Add(Select(Text("First/a.txt")));
            Configure(("First", ""), ("Second", "audio"));
            var map = ResourceBuildMap.Create(project);
            var source = map.Assets.Single().Source;
            Configure(("Second", "audio"), ("First", ""));
            Assert.That(ResourceBuildSourceLocator.Resolve(project, source).Group, Is.EqualTo(0));
            Configure(("Second", "audio"), ("First", "changed"));
            Assert.Throws<InvalidOperationException>(() => ResourceBuildSourceLocator.Resolve(project, source));
            Configure(("Second", "audio"));
            Assert.Throws<InvalidOperationException>(() => ResourceBuildSourceLocator.Resolve(project, source));
        }

        [Test]
        public void ExplicitSaveDoesNotRewriteDirectoryOrderPrefixesOrAddresses()
        {
            var selection = Select(Text("First/a.txt")); selection.Aliases = new[] { "stable" };
            project.Groups[0].Selections.Add(selection);
            Configure(("Second", "other"), ("First", "audio"));
            var before = JsonConvert.SerializeObject(project.SourceDirectories);
            EditorUtility.SetDirty(project);
            ResourceEditorOperations.SaveProject(project);
            Assert.That(JsonConvert.SerializeObject(project.SourceDirectories), Is.EqualTo(before));
            var map = ResourceBuildMap.Create(project);
            Assert.That(map.Assets.Single().Address.Path, Is.EqualTo("audio/a.txt"));
            Assert.That(map.Assets.Single().Aliases.Single().Path, Is.EqualTo("stable"));
            Assert.That(EditorUtility.IsDirty(project), Is.False);
        }

        [Test]
        public void DisplayDirectoryValidationReusesTheSameSnapshotButInvalidatesChangedMeaning()
        {
            Configure(("First", ""), ("Second", "audio"));
            var before = ResourceWorkbenchGUI.DisplayPaths(project);
            Assert.That(ResourceWorkbenchGUI.DisplayPaths(project), Is.SameAs(before));
            Assert.That(before.DisplayAddress(root + "/First"), Is.EqualTo(""));
            Assert.That(before.DisplayAddress(root + "/Second/a.txt"), Is.EqualTo("audio/a.txt"), "A display address is computed from the frozen directory relation, not by scanning the file system.");
            Configure(("Second", "audio"), ("First", ""));
            Assert.That(ResourceWorkbenchGUI.DisplayPaths(project), Is.SameAs(before), "A mere list order change must not rescan source trees while drawing.");
            Configure(("First", "changed"), ("Second", "audio"));
            Assert.That(ResourceWorkbenchGUI.DisplayPaths(project), Is.Not.SameAs(before));
            Assert.That(ResourceWorkbenchGUI.DisplayPaths(project).DisplayAddress(root + "/First"), Is.EqualTo("changed"));
            Configure();
            Assert.Throws<InvalidOperationException>(() => ResourceWorkbenchGUI.DisplayPaths(project));
        }

        [Test]
        public void MovingAnOwnedSourceDirectoryUsesItsGuidAndPreservesItsRelativeAddress()
        {
            var file = Text("First/a.txt");
            var selection = Select(file); project.Groups[0].Selections.Add(selection);
            Configure(("First", "audio"), ("Second", "other"));
            var directoryGuid = project.SourceDirectories[0].Guid;
            Assert.That(ResourceBuildMap.Create(project).Assets.Single().Address.Path, Is.EqualTo("audio/a.txt"));
            Assert.That(AssetDatabase.MoveAsset(root + "/First", root + "/MovedFirst"), Is.Empty);
            var map = ResourceBuildMap.Create(project);
            Assert.That(project.SourceDirectories[0].Guid, Is.EqualTo(directoryGuid));
            Assert.That(map.Assets.Single().SourcePath, Is.EqualTo(root + "/MovedFirst/a.txt"));
            Assert.That(map.Assets.Single().Guid, Is.EqualTo(selection.Guid));
            Assert.That(map.Assets.Single().Address.Path, Is.EqualTo("audio/a.txt"));
        }

        [Test]
        public void MissingGuidAssetsRootAndAFileCannotBecomeSourceDirectoriesOrExpandTheRange()
        {
            var file = Text("First/a.txt"); project.Groups[0].Selections.Add(Select(file));
            foreach (var guid in new[] { Guid.NewGuid().ToString("N"), AssetDatabase.AssetPathToGUID("Assets"), AssetDatabase.AssetPathToGUID(file) })
            {
                JsonUtility.FromJsonOverwrite(JsonConvert.SerializeObject(new {
                    SourceDirectories = new[] { new { Guid = guid, AddressPrefix = "" } } }), project);
                var error = Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(project));
                Assert.That(error.Message, Does.Contain("目录"));
                Assert.That(project.Groups[0].Selections.Count, Is.EqualTo(1));
                Assert.That(File.Exists(file), Is.True);
            }
        }

        private string Text(string relative)
        {
            var path = root + "/" + relative;
            File.WriteAllText(path, "owned multi-directory fixture");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        private static ResourceSelection Select(string path) => new() { Guid = AssetDatabase.AssetPathToGUID(path) };

        private void Configure(params (string Directory, string Prefix)[] directories) => JsonUtility.FromJsonOverwrite(JsonConvert.SerializeObject(new
        {
            SourceDirectories = directories.Select(x => new { Guid = AssetDatabase.AssetPathToGUID(root + "/" + x.Directory), AddressPrefix = x.Prefix }).ToArray()
        }), project);
    }
}
