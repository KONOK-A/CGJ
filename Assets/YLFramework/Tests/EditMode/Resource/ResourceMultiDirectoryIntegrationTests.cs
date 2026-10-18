using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public sealed class ResourceMultiDirectoryIntegrationTests
    {
        private string root;
        private ResourceProject project;
        private bool createdModern;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(ResourceMultiDirectoryEvidence.FixtureRoot), Is.True, "仅使用本批已登记夹具父目录。 ");
            if (!AssetDatabase.IsValidFolder(ResourceMultiDirectoryEvidence.Root))
            {
                Assert.That(Directory.Exists(ResourceMultiDirectoryEvidence.Root) || File.Exists(ResourceMultiDirectoryEvidence.Root + ".meta"), Is.False);
                Assert.That(AssetDatabase.CreateFolder(ResourceMultiDirectoryEvidence.FixtureRoot, "Modern"), Is.Not.Empty);
                createdModern = true;
            }
            root = ResourceMultiDirectoryEvidence.Root + "/Integration-" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder(ResourceMultiDirectoryEvidence.Root, Path.GetFileName(root)), Is.Not.Empty);
            foreach (var name in new[] { "Textures", "Materials", "Text" }) Assert.That(AssetDatabase.CreateFolder(root, name), Is.Not.Empty);
            project = ResourceEditorOperations.CreateProject(root + "/Project.asset");
            project.ProjectId = "multi-integration-" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (project) Undo.ClearUndo(project);
            if (!string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root)) Assert.That(AssetDatabase.DeleteAsset(root), Is.True);
            if (createdModern && AssetDatabase.IsValidFolder(ResourceMultiDirectoryEvidence.Root) &&
                !Directory.EnumerateFileSystemEntries(ResourceMultiDirectoryEvidence.Root).Any())
                Assert.That(AssetDatabase.DeleteAsset(ResourceMultiDirectoryEvidence.Root), Is.True);
            createdModern = false;
        }

        [Test]
        public void CrossDirectoryDependencyIsAssignedOnceAcrossGroupsWithoutCollectingUnselectedText()
        {
            SharedGraph();
            var map = ResourceBuildMap.Create(project);
            CollectionAssert.AreEquivalent(new[] { "materials/A.mat", "materials/B.mat", "text/selected.txt" }, map.Assets.Select(x => x.Address.Path));
            Assert.That(map.Groups["main"].Count, Is.EqualTo(2), "同 Group 的材质与文本来自不同源目录。 ");
            var shared = map.Bundles.Single(x => x.IsShared);
            CollectionAssert.AreEqual(new[] { root + "/Textures/Shared.asset" }, shared.AssetPaths);
            var paths = map.Bundles.SelectMany(x => x.AssetPaths).ToArray();
            Assert.That(paths.Count(x => x == root + "/Textures/Shared.asset"), Is.EqualTo(1));
            Assert.That(paths, Does.Not.Contain(root + "/Text/unselected.txt"));
            var a = map.Assets.Single(x => x.Address.Path == "materials/A.mat");
            var b = map.Assets.Single(x => x.Address.Path == "materials/B.mat");
            Assert.That(map.Bundles.Single(x => x.Name == a.BundleName).Dependencies, Does.Contain(shared.Name));
            Assert.That(map.Bundles.Single(x => x.Name == b.BundleName).Dependencies, Does.Contain(shared.Name));
        }

        [Test]
        public void RemovingADependencyOnlyDirectoryRejectsTheGraphWithoutDeletingSelectionsOrSourceBytes()
        {
            SharedGraph();
            var before = ResourceReleaseFixture.Snapshot(root);
            ResourceMultiDirectoryEvidence.SetSources(project, (root + "/Materials", "materials"), (root + "/Text", "text"));
            var error = Assert.Throws<InvalidOperationException>(() => ResourceBuildMap.Create(project));
            Assert.That(error.Message, Does.Contain(root + "/Textures/Shared.asset"));
            Assert.That(project.Groups.Single(x => x.Id == "main").Selections.Count, Is.EqualTo(2));
            Assert.That(project.Groups.Single(x => x.Id == "other").Selections.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(before, ResourceReleaseFixture.Snapshot(root), "A dependency-range failure must not save a definition or rewrite source files/meta.");
        }

        [Test]
        public void SimulationResolvesTheSameCanonicalIdentityAndAliasAfterDirectoryReordering()
        {
            SharedGraph();
            var map = ResourceBuildMap.Create(project);
            var simulation = new ResourceEditorSimulation(new[] { project });
            var expected = map.Assets.Single(x => x.Address.Path == "text/selected.txt");
            var canonical = new ResourceAddress(project.ProjectId, "text/selected.txt");
            var alias = new ResourceAddress(project.ProjectId, "message");
            Assert.That(simulation.Resolve(canonical).AssetId, Is.EqualTo(expected.AssetId));
            Assert.That(simulation.Resolve(alias).AssetId, Is.EqualTo(expected.AssetId));
            Assert.That(simulation.Load<TextAsset>(alias), Is.SameAs(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "/Text/selected.txt")));
            Assert.That(simulation.Load<Material>(new ResourceAddress(project.ProjectId, "materials/A.mat")),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/A.mat")));
            ResourceMultiDirectoryEvidence.SetSources(project, (root + "/Text", "text"), (root + "/Materials", "materials"), (root + "/Textures", "textures"));
            Assert.That(simulation.Resolve(alias).AssetId, Is.EqualTo(expected.AssetId));
            CollectionAssert.AreEquivalent(map.Groups["main"], simulation.GetGroup(project.ProjectId, "main").Select(x => x.AssetId));
            Assert.Throws<ArgumentException>(() => simulation.Resolve(new ResourceAddress(project.ProjectId, "text/unselected.txt")));
            Assert.That(ResourceSystem.Current, Is.Null, "Simulation 不应初始化 Runtime。 ");
        }

        [Test]
        public void SetSourceDirectoriesSupportsUndoAndOnlyExplicitSavePersistsTheNewPrefix()
        {
            var path = Text("selected.txt");
            project.Groups.Add(new ResourceGroup { Id = "main", Selections = new List<ResourceSelection> { Select(path) } });
            ResourceMultiDirectoryEvidence.SetSources(project, (root + "/Text", "text"));
            EditorUtility.SetDirty(project); ResourceEditorOperations.SaveProject(project);
            var projectPath = AssetDatabase.GetAssetPath(project); var before = File.ReadAllBytes(projectPath);
            ResourceMultiDirectoryEvidence.SetSources(project, (root + "/Text", "renamed"));
            Undo.FlushUndoRecordObjects();
            Assert.That(EditorUtility.IsDirty(project), Is.True);
            Assert.That(ResourceBuildMap.Create(project).Assets.Single().Address.Path, Is.EqualTo("renamed/selected.txt"));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(projectPath), "编辑不能悄悄保存。 ");
            Undo.PerformUndo();
            Assert.That(ResourceBuildMap.Create(project).Assets.Single().Address.Path, Is.EqualTo("text/selected.txt"));
            Undo.PerformRedo();
            Assert.That(ResourceBuildMap.Create(project).Assets.Single().Address.Path, Is.EqualTo("renamed/selected.txt"));
            ResourceEditorOperations.SaveProject(project);
            Undo.ClearUndo(project); Resources.UnloadAsset(project);
            project = AssetDatabase.LoadAssetAtPath<ResourceProject>(projectPath);
            Assert.That(ResourceBuildMap.Create(project).Assets.Single().Address.Path, Is.EqualTo("renamed/selected.txt"));
            Assert.That(EditorUtility.IsDirty(project), Is.False);
        }

        [Test]
        public void CurrentDirectoryModelSaveAndReloadKeepCanonicalAddressAliasAndSourceIdentity()
        {
            var path = Text("selected.txt");
            project.Groups.Add(new ResourceGroup { Id = "main", Selections = new List<ResourceSelection> { Select(path, "message") } });
            ResourceMultiDirectoryEvidence.SetSources(project, (root + "/Materials", "materials"), (root + "/Text", "messages"));
            EditorUtility.SetDirty(project); ResourceEditorOperations.SaveProject(project);
            var projectPath = AssetDatabase.GetAssetPath(project);
            var before = File.ReadAllBytes(projectPath);
            var projectMeta = File.ReadAllBytes(projectPath + ".meta");
            var source = File.ReadAllBytes(path); var sourceMeta = File.ReadAllBytes(path + ".meta");
            var first = ResourceBuildMap.Create(project).Assets.Single();
            Assert.That(first.Address.Path, Is.EqualTo("messages/selected.txt"));
            Assert.That(first.Aliases.Single().Path, Is.EqualTo("message"));
            Assert.That(EditorUtility.IsDirty(project), Is.False);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(projectPath), "只读解析不能改写当前目录列表。 ");
            project.DisplayName = "当前目录模型的显式保存";
            EditorUtility.SetDirty(project); ResourceEditorOperations.SaveProject(project);
            Undo.ClearUndo(project); Resources.UnloadAsset(project); project = AssetDatabase.LoadAssetAtPath<ResourceProject>(projectPath);
            var saved = ResourceBuildMap.Create(project).Assets.Single();
            Assert.That(saved.Address.Path, Is.EqualTo("messages/selected.txt"));
            Assert.That(saved.Aliases.Single().Path, Is.EqualTo("message"));
            Assert.That(saved.AssetId, Is.EqualTo(first.AssetId));
            Assert.That(project.DisplayName, Is.EqualTo("当前目录模型的显式保存"));
            CollectionAssert.AreEqual(new[] { AssetDatabase.AssetPathToGUID(root + "/Materials"), AssetDatabase.AssetPathToGUID(root + "/Text") },
                project.SourceDirectories.Select(x => x.Guid));
            CollectionAssert.AreEqual(new[] { "materials", "messages" }, project.SourceDirectories.Select(x => x.AddressPrefix));
            CollectionAssert.AreEqual(projectMeta, File.ReadAllBytes(projectPath + ".meta"));
            CollectionAssert.AreEqual(source, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(sourceMeta, File.ReadAllBytes(path + ".meta"));
            Assert.That(EditorUtility.IsDirty(project), Is.False);
        }

        private void SharedGraph()
        {
            var texture = new Texture2D(2, 2) { name = "Shared" };
            AssetDatabase.CreateAsset(texture, root + "/Textures/Shared.asset");
            var shader = Shader.Find("Unlit/Texture"); Assert.That(shader, Is.Not.Null);
            foreach (var name in new[] { "A", "B" }) AssetDatabase.CreateAsset(new Material(shader) { name = name, mainTexture = texture }, root + "/Materials/" + name + ".mat");
            var text = Text("selected.txt"); Text("unselected.txt");
            project.Groups.Add(new ResourceGroup { Id = "main", Selections = new List<ResourceSelection> { Select(root + "/Materials/A.mat"), Select(text, "message") } });
            project.Groups.Add(new ResourceGroup { Id = "other", Selections = new List<ResourceSelection> { Select(root + "/Materials/B.mat") } });
            ResourceMultiDirectoryEvidence.SetSources(project, (root + "/Textures", "textures"), (root + "/Materials", "materials"), (root + "/Text", "text"));
            EditorUtility.SetDirty(project); ResourceEditorOperations.SaveProject(project);
        }

        private string Text(string name)
        {
            var path = root + "/Text/" + name;
            File.WriteAllText(path, "自有多目录测试文本"); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); return path;
        }

        private static ResourceSelection Select(string path, string alias = null) => new() { Guid = AssetDatabase.AssetPathToGUID(path),
            Aliases = alias == null ? Array.Empty<string>() : new[] { alias } };
    }
}
