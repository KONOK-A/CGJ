using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using YLFramework.Editor.Resource;
using YLFramework.Tests.ResourceBuild;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceBuildIntegrationTests
    {
        private static Type Operations
        {
            get
            {
                var type = typeof(ResourceProject).Assembly.GetType("YLFramework.Editor.Resource.ResourceEditorOperations");
                Assert.That(type, Is.Not.Null, "The workbench must execute the formal build/publication authority.");
                return type;
            }
        }
        private static object Run(ResourceProject project, bool generate, string output) => ResourceEditorTests.Invoke(Operations,
            "BuildAndPublish", project, "fixture-v1", generate, output, false, CancellationToken.None);
        private static T Value<T>(object result, string name) => (T)result.GetType().GetProperty(name).GetValue(result);

        [Test]
        public void RealWorkbenchBuildPublishAndIndependentGenerationFailurePreserveVersions()
        {
            using var fixture = new ResourceReleaseFixture();
            var output = "Assets/Workbench82c-" + Guid.NewGuid().ToString("N");
            var root = Path.GetFullPath("AssetBundles/YLFramework/" + fixture.Project.ProjectId + "/" + EditorUserBuildSettings.activeBuildTarget);
            Assert.That(Directory.Exists(root), Is.False);
            var workspace = ResourceBundleBuilder.GetWorkspacePath(fixture.Project, EditorUserBuildSettings.activeBuildTarget);
            Assert.That(Directory.Exists(Path.GetDirectoryName(workspace)), Is.False);
            var preview = (string)ResourceEditorTests.Invoke(Operations, "Preflight", fixture.Project, "fixture-v1");
            StringAssert.Contains(root, preview);
            Assert.That(Directory.Exists(root) || Directory.Exists(workspace), Is.False, "Preflight is read-only.");
            try
            {
                var first = Run(fixture.Project, false, output);
                Assert.That(Value<Exception>(first, "BuildError"), Is.Null);
                Assert.That(Value<Exception>(first, "PublicationError"), Is.Null);
                var release = Value<ResourceRelease>(first, "Release");
                Assert.That(release, Is.Not.Null);
                fixture.ReleaseRoot = root;
                var old = ResourceReleaseFixture.Snapshot(release.DirectoryPath);
                using var other = new ResourceReleaseFixture();
                var otherBuild = other.Build();
                other.ReleaseRoot = ResourcePublisher.GetReleaseRoot(otherBuild);
                var otherRelease = ResourcePublisher.Publish(otherBuild, "fixture-v1");
                var beforeRead = ResourceReleaseFixture.Snapshot(root).Concat(ResourceReleaseFixture.Snapshot(other.ReleaseRoot)).ToArray();
                var catalogs = ResourceEditorOperations.PublishedCatalogs("fixture-v1");
                CollectionAssert.AreEquivalent(new[] { fixture.Project.ProjectId, other.Project.ProjectId }, catalogs.Select(x => x.ProjectId));
                CollectionAssert.AreEqual(beforeRead, ResourceReleaseFixture.Snapshot(root).Concat(ResourceReleaseFixture.Snapshot(other.ReleaseRoot)).ToArray());
                Assert.Throws<InvalidOperationException>(() => ResourcePublisher.ReadCurrent(other.Project, EditorUserBuildSettings.activeBuildTarget, "other-app"));
                var savedId = fixture.Project.ProjectId;
                fixture.Project.ProjectId = "unsaved-" + Guid.NewGuid().ToString("N"); EditorUtility.SetDirty(fixture.Project);
                try { Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.PublishedCatalogs("fixture-v1"), "A/B remain published; dirty A cannot silently disappear from the collection generated for B."); }
                finally { fixture.Project.ProjectId = savedId; AssetDatabase.SaveAssetIfDirty(fixture.Project); }
                File.WriteAllText(fixture.TextPath, "second workbench release");
                AssetDatabase.ImportAsset(fixture.TextPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                // Catalog 地址合法，但常量标识符和 entry.txt 碰撞；这只能使生成阶段失败。
                var project = fixture.Project;
                project.Groups[1].Selections[0].Address = "entry_txt";
                EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
                var second = Run(project, true, output);
                Assert.That(Value<Exception>(second, "BuildError"), Is.Null);
                Assert.That(Value<Exception>(second, "PublicationError"), Is.Null);
                Assert.That(Value<Exception>(second, "GenerationError"), Is.InstanceOf<InvalidOperationException>());
                Assert.That(Value<System.Collections.Generic.IReadOnlyList<string>>(second, "GeneratedProjects").Count, Is.EqualTo(2));
                var updated = Value<ResourceRelease>(second, "Release");
                Assert.That(updated.Catalog.CatalogDigest, Is.Not.EqualTo(release.Catalog.CatalogDigest));
                Assert.That(Directory.Exists(output), Is.False);
                CollectionAssert.AreEqual(old, ResourceReleaseFixture.Snapshot(release.DirectoryPath));
                Assert.That(ResourcePublisher.Recover(fixture.Project, EditorUserBuildSettings.activeBuildTarget, "fixture-v1").Catalog.CatalogDigest,
                    Is.EqualTo(updated.Catalog.CatalogDigest));
                var simulation = new ResourceEditorSimulation(new[] { fixture.Project }, new[] { updated.Catalog });
                Assert.That(simulation.Load<UnityEngine.TextAsset>(new YLFramework.Services.Resource.ResourceAddress(updated.Catalog.ProjectId, "entry.txt")).text,
                    Is.EqualTo("second workbench release"));
                fixture.AssertProtected();
            }
            finally
            {
                // 工作台创建的 workspace 不在 fixture.Build() 的登记内，只清理预检时确认不存在的本项目结果。
                if (Directory.Exists(Path.GetDirectoryName(workspace))) Directory.Delete(Path.GetDirectoryName(workspace), true);
            }
        }
    }
}
