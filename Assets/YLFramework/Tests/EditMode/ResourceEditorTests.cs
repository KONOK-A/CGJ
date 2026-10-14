using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;
using YLFramework.Tests.ResourceBuild;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceEditorTests
    {
        private string folder;
        [SetUp]
        public void SetUp()
        {
            folder = "Assets/YLFramework/Tests/EditMode/Resource82c-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode", Path.GetFileName(folder));
        }
        [TearDown] public void TearDown() => AssetDatabase.DeleteAsset(folder);

        [Test]
        public void ResourceEditorPathsFollowBuildServiceCodeGenerationAndDataResponsibilities()
        {
            const string root = "Assets/YLFramework/Editor/Systems/Resource/";
            foreach (var file in new[] { "Build/ResourceBuildOperation.cs", "Build/ResourceBuildMap.cs", "Build/ResourceBuildWorkspace.cs",
                "Build/ResourceBundleBuilder.cs", "Build/ResourceCatalogBuilder.cs", "Data/ResourceProject.cs", "Publishing/ResourcePublisher.cs",
                "Service/ResourceEditorOperations.cs", "Service/ResourceEditorSimulation.cs", "Service/ResServiceAssetsMenu.cs", "Service/ResServiceEditorWindow.cs",
                "CodeGeneration/ResourceCodeGenerator.cs", "CodeGeneration/ResourceReferenceOutput.cs" })
            {
                Assert.That(File.Exists(root + file) && File.Exists(root + file + ".meta"), Is.True, file);
                Assert.That(Path.GetFileNameWithoutExtension(UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(root + file)), Is.EqualTo("YLFramework.Editor"));
            }
            var assembly = typeof(ResourceProject).Assembly;
            Assert.That(assembly.GetType("YLFramework.Editor.Resource.BuildScript"), Is.Null);
            Assert.That(assembly.GetType("YLFramework.Editor.Resource.AssetBundleExporter"), Is.Null);
            Assert.That(assembly.GetType("YLFramework.Editor.Resource.ResourceBuildOperation").GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(x => x.Name == "Execute" || x.Name == "CreatePlan"), Is.False, "There must not be a competing legacy builder.");
        }

        [Test]
        public void FormalSelectionEditingSupportsUndoAndSavedGuidWithoutImporterWrites()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            var path = fixture.Folder + "/Content/Shared.asset";
            var before = File.ReadAllBytes(path + ".meta");
            var count = project.Groups[0].Selections.Count;
            ResourceEditorOperations.AddSelections(project, 0, new[] { AssetDatabase.LoadMainAssetAtPath(path) });
            Assert.That(project.Groups[0].Selections.Count, Is.EqualTo(count + 1));
            Undo.PerformUndo(); Assert.That(project.Groups[0].Selections.Count, Is.EqualTo(count));
            Undo.PerformRedo(); Assert.That(project.Groups[0].Selections[count].Guid, Is.EqualTo(AssetDatabase.AssetPathToGUID(path)));
            AssetDatabase.SaveAssetIfDirty(project);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(project), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(fixture.Project.Groups[0].Selections.Count, Is.EqualTo(count + 1));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path + ".meta"));
            Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.AddSelections(fixture.Project, 0, new[] { AssetDatabase.LoadMainAssetAtPath(folder) }));
            Undo.ClearUndo(fixture.Project);
        }

        [Test]
        public void WorkbenchCreatesAndSavesOnlyNewAssetsIdentityWithoutOverwritingExistingData()
        {
            var path = folder + "/Created.asset";
            var project = ResourceEditorOperations.CreateProject(path);
            Assert.That(project, Is.SameAs(AssetDatabase.LoadAssetAtPath<ResourceProject>(path)));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Has.Length.EqualTo(32));
            project.DisplayName = "saved definition"; EditorUtility.SetDirty(project);
            ResourceEditorOperations.SaveProject(project);
            var bytes = File.ReadAllBytes(path); var meta = File.ReadAllBytes(path + ".meta");
            Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.CreateProject(path));
            Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.CreateProject("Packages/Protected82c.asset"));
            Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.CreateProject(folder + "/../Escaped.asset"));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path)); CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.LoadAssetAtPath<ResourceProject>(path).DisplayName, Is.EqualTo("saved definition"));
        }

        [Test]
        public void WorkbenchRefusesJunctionDefinitionBeforeEditingOrSavingExternalBytes()
        {
            using var fixture = new ResourceReleaseFixture();
            var source = fixture.Folder + "/Project.asset";
            var outside = Path.GetFullPath("Temp/Definition82c-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outside);
            File.Copy(source, Path.Combine(outside, "Project.asset")); File.Copy(source + ".meta", Path.Combine(outside, "Project.asset.meta"));
            AssetDatabase.DeleteAsset(source);
            var link = Path.GetFullPath(folder + "/Linked");
            try
            {
                ResourceOutputSafetyTests.CreateJunction(link, outside);
                AssetDatabase.ImportAsset(folder + "/Linked/Project.asset", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var linked = AssetDatabase.LoadAssetAtPath<ResourceProject>(folder + "/Linked/Project.asset");
                Assert.That(linked, Is.Not.Null);
                var bytes = File.ReadAllBytes(Path.Combine(outside, "Project.asset"));
                var meta = File.ReadAllBytes(Path.Combine(outside, "Project.asset.meta"));
                var view = new ResServiceView();
                try { Assert.Throws<InvalidOperationException>(() => view.SetProject(linked)); }
                finally { view.OnDispose(); }
                Assert.Throws<InvalidOperationException>(() => Invoke(typeof(ResourceEditorOperations), "SaveProject", linked));
                Assert.Throws<InvalidOperationException>(() => Invoke(typeof(ResourceEditorOperations), "CreateProject", folder + "/Linked/New.asset"));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(outside, "Project.asset")));
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(Path.Combine(outside, "Project.asset.meta")));
                Assert.That(File.Exists(Path.Combine(outside, "New.asset")), Is.False);
            }
            finally
            {
                if (Directory.Exists(link)) Directory.Delete(link);
                if (File.Exists(folder + "/Linked.meta")) File.Delete(folder + "/Linked.meta");
                Directory.Delete(outside, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void DirtyCandidateCannotDisappearFromPublishedGenerationCollection()
        {
            using var first = new ResourceReleaseFixture();
            using var second = new ResourceReleaseFixture();
            var output = folder + "/Generated";
            Directory.CreateDirectory(output);
            File.WriteAllText(output + "/YLResourceReferences.cs", "collection sentinel");
            var path = output + "/YLResourceReferences.cs";
            var bytes = File.ReadAllBytes(path); var time = File.GetLastWriteTimeUtc(path);
            first.Project.ProjectId = "unpersisted-id"; EditorUtility.SetDirty(first.Project);
            var error = Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.PublishedCatalogs("fixture-v1"));
            StringAssert.Contains("未保存", error.Message, "A dirty definition must fail as dirty, not be interpreted as an unpublished project.");
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(time));
        }

        [Test]
        public void DirtyDefinitionsAndCanceledOperationsDoNotCreateOutputs()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            project.DisplayName = "pending definition edit"; EditorUtility.SetDirty(project);
            Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.Preflight(project, "fixture-v1"));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var report = ResourceEditorOperations.BuildAndPublish(project, "fixture-v1", cancellation: cancel.Token);
            Assert.That(report.BuildError, Is.InstanceOf<OperationCanceledException>());
            Assert.That(report.Build, Is.Null); Assert.That(report.Release, Is.Null);
            Assert.That(Directory.Exists("AssetBundles/YLFramework/" + project.ProjectId), Is.False);
        }

        // 旧 Runtime 尚在消费 SubProjectData；只保留其纯分区契约，不执行旧构建或标记菜单。
        private SubProjectData Owner(string path, string name)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SubProject>(), path + "/" + name + ".asset");
            return SubProjectData.SearchAllInProject().Single(x => x.Path == path + "/" + name + ".asset");
        }
        private void MarkedText(string path, string bundle)
        {
            File.WriteAllText(path, "isolated legacy Runtime metadata"); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path); importer.assetBundleName = bundle; importer.SaveAndReimport();
        }
        [Test]
        public void DirectoryBoundaryDoesNotClaimSiblingWithSamePrefix()
        {
            AssetDatabase.CreateFolder(folder, "Owner"); AssetDatabase.CreateFolder(folder, "OwnerExtra");
            var owner = Owner(folder + "/Owner", "Project"); var bundle = "resource82c-" + Guid.NewGuid().ToString("N");
            MarkedText(folder + "/OwnerExtra/Text.bytes", bundle);
            var defaults = new SubProjectData(); SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(defaults, new List<SubProjectData> { owner });
            Assert.That(owner.Builds, Is.Empty); Assert.That(defaults.Builds.Any(x => x.assetBundleName == bundle), Is.True);
        }
        [Test]
        public void NestedAndCrossOwnerLegacyMetadataRemainRejected()
        {
            AssetDatabase.CreateFolder(folder, "Child");
            var parent = Owner(folder, "Parent"); var child = Owner(folder + "/Child", "Child");
            var bundle = "resource82c-" + Guid.NewGuid().ToString("N");
            MarkedText(folder + "/Child/Text.bytes", bundle);
            Assert.Throws<InvalidOperationException>(() => SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(new SubProjectData(), new List<SubProjectData> { parent, child }));
            Assert.That(parent.Builds.Concat(child.Builds), Is.Empty);
            AssetDatabase.DeleteAsset(folder + "/Parent.asset");
            MarkedText(folder + "/Default.bytes", bundle);
            Assert.Throws<InvalidOperationException>(() => SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(new SubProjectData(), new List<SubProjectData> { child }));
        }
        internal static object Invoke(Type type, string method, params object[] args)
        {
            var member = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).SingleOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
            Assert.That(member, Is.Not.Null, "Missing operation: " + method);
            try { return member.Invoke(null, args); } catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
