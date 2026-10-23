using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public class ResourcePublishingTests
    {
        private Type Publisher => typeof(ResourceProject).Assembly.GetType("YLFramework.Editor.Resource.ResourcePublisher");

        [Test]
        public void RealReleaseRepeatFaultCancellationAndRecoveryPreserveCompleteVersions()
        {
            using var fixture = new ResourceReleaseFixture();
            var firstBuild = fixture.Build();
            Assert.That(Publisher, Is.Not.Null, "The safe local publication transaction is missing.");
            fixture.ReleaseRoot = (string)Publisher.GetMethod("GetReleaseRoot").Invoke(null, new object[] { firstBuild });
            var root = fixture.ReleaseRoot;
            Assert.That(Directory.Exists(root), Is.False);
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Rejected<OperationCanceledException>(() => Publish(firstBuild, cancel.Token));
                Assert.That(Directory.Exists(root), Is.False);
            }
            Directory.CreateDirectory(root);
            Rejected<InvalidOperationException>(() => Publish(firstBuild));
            Assert.That(Directory.GetFileSystemEntries(root), Is.Empty, "An empty unknown output is not owned.");
            Directory.Delete(root);
            var first = Publish(firstBuild);
            var firstPath = (string)Value(first, "DirectoryPath");
            var firstCatalog = (ResourceCatalogDocument)Value(first, "Catalog");
            var immutableBefore = ResourceReleaseFixture.Snapshot(firstPath);
            var indexPath = Path.Combine(root, "release-index.json");
            var originalIndex = File.ReadAllBytes(indexPath);
            var repeat = Publish(firstBuild);
            Assert.That(Value(repeat, "IsRepeat"), Is.True);
            Assert.That(Value(repeat, "CopiedBytes"), Is.EqualTo(0L));
            CollectionAssert.AreEqual(originalIndex, File.ReadAllBytes(indexPath));
            CollectionAssert.AreEqual(immutableBefore, ResourceReleaseFixture.Snapshot(firstPath));

            // 真实第二代 Bundle，不用伪造文件代替发布闭环；下面故障都复用这一代的已验证产物。
            File.WriteAllText(fixture.TextPath, "second real release content");
            AssetDatabase.ImportAsset(fixture.TextPath, ImportAssetOptions.ForceSynchronousImport);
            var secondBuild = fixture.Build();
            var secondCatalog = ResourceCatalogBuildTests.Create(secondBuild);
            Assert.That(secondCatalog.CatalogDigest, Is.Not.EqualTo(firstCatalog.CatalogDigest));
            Rejected<InvalidOperationException>(() => Publish(firstBuild));
            Rejected<IOException>(() => Execute(secondBuild, default, step =>
            {
                if (step != "StagingCreated")
                {
                    return;
                }

                var stage = Directory.GetDirectories(Path.Combine(root, ".staging")).Single();
                var registered = secondCatalog.Bundles[0].RelativeFile;
                File.WriteAllText(Path.Combine(stage, registered), "copy destination already exists");
            }));
            CollectionAssert.AreEqual(originalIndex, File.ReadAllBytes(indexPath));
            Recover(fixture);
            using (var cancel = new CancellationTokenSource())
            {
                Rejected<OperationCanceledException>(() => Execute(secondBuild, cancel.Token, step =>
                {
                    if (step == "CatalogWritten")
                    {
                        cancel.Cancel();
                    }
                }));
                CollectionAssert.AreEqual(originalIndex, File.ReadAllBytes(indexPath));
                Assert.That(((ResourceCatalogDocument)Value(Recover(fixture), "Catalog")).CatalogDigest, Is.EqualTo(firstCatalog.CatalogDigest));
            }
            foreach (var fault in new[] { "StagingCreated", "FileCopied", "CatalogWritten", "StageVerified", "VersionInstalled", "BeforeIndex" })
            {
                Rejected<IOException>(() => Execute(secondBuild, default, step =>
                {
                    if (step == fault)
                    {
                        throw new IOException("Injected " + fault);
                    }
                }));
                CollectionAssert.AreEqual(originalIndex, File.ReadAllBytes(indexPath), fault);
                CollectionAssert.AreEqual(immutableBefore, ResourceReleaseFixture.Snapshot(firstPath), fault);
                var recovered = Recover(fixture);
                Assert.That(((ResourceCatalogDocument)Value(recovered, "Catalog")).CatalogDigest, Is.EqualTo(firstCatalog.CatalogDigest), fault);
                Assert.That(File.Exists(Path.Combine(root, ".ylr-publish-journal.json")), Is.False);
            }
            FileStream blockedIndex = null;
            try
            {
                Rejected<IOException>(() => Execute(secondBuild, default, step =>
                {
                    if (step == "BeforeIndex")
                    {
                        blockedIndex = File.Open(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                }));
            }
            finally
            {
                blockedIndex?.Dispose();
            }
            CollectionAssert.AreEqual(originalIndex, File.ReadAllBytes(indexPath), "A real File.Replace failure must retain the old Index.");
            Recover(fixture);
            Rejected<IOException>(() => Execute(secondBuild, default, step =>
            {
                if (step == "IndexCommitted")
                {
                    throw new IOException("Simulated interruption after the commit point");
                }
            }));
            var second = Recover(fixture);
            Assert.That(((ResourceCatalogDocument)Value(second, "Catalog")).CatalogDigest, Is.EqualTo(secondCatalog.CatalogDigest));
            CollectionAssert.AreEqual(immutableBefore, ResourceReleaseFixture.Snapshot(firstPath));
            var secondPath = (string)Value(second, "DirectoryPath");
            AssertNativeText(firstPath, firstCatalog, "first release content");
            AssertNativeText(secondPath, secondCatalog, "second real release content");
            var index = JObject.Parse(File.ReadAllText(indexPath));
            Assert.That((string)index["CatalogDigest"], Is.EqualTo(secondCatalog.CatalogDigest));
            Assert.That((string)index["BuildId"], Is.EqualTo(secondCatalog.BuildId));
            Assert.That((string)index["CatalogFile"], Is.EqualTo(secondCatalog.CatalogDigest + "/resource-catalog.json"));
            Assert.That(index["Assets"], Is.Null, "Release Index is not another resource table.");
            var stableBuild = fixture.Build();
            var stableCatalog = ResourceCatalogBuildTests.Create(stableBuild);
            Assert.That(stableCatalog.CatalogDigest, Is.EqualTo(secondCatalog.CatalogDigest));
            var stable = Publish(stableBuild);
            Assert.That(Value(stable, "IsRepeat"), Is.True);
            Assert.That(stableBuild.CacheReuseBlockedBy.Count, Is.EqualTo(6), "Keep the established untracked-callback correctness gate.");
            Assert.That(stableBuild.WasUnityBuildInvoked && stableBuild.ForceRebuild && !stableBuild.IsCacheHit, Is.True);
            fixture.AssertProtected();
            fixture.RetainRelease = true;
            Debug.Log("REAL_82B root=" + root + "; v1=" + firstCatalog.CatalogDigest + "; v2=" + secondCatalog.CatalogDigest +
                "; bundleCount=" + secondCatalog.Bundles.Count + "; assets=" + secondCatalog.Assets.Count +
                "; groups=" + secondCatalog.Groups.Count + "; repeatCopiedBytes=" + Value(stable, "CopiedBytes"));
        }

        [Test]
        public void UnsafeReleaseSegmentsAndWindowsPathLengthsAreRejectedWithoutWrites()
        {
            Assert.That(Publisher, Is.Not.Null);
            var method = Publisher.GetMethod("Root", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var id in new[] { "CON", "NUL.txt", "trailing.", new string('x', 128) })
            {
                Rejected<InvalidOperationException>(() => method.Invoke(null, new object[] { id, "StandaloneWindows64" }));
            }
        }

        [Test]
        public void CommitRevalidatesLiveOwnerAndJournalBeforeChangingIndex()
        {
            using var fixture = new ResourceReleaseFixture();
            var build = fixture.Build();
            var first = Publish(build);
            fixture.ReleaseRoot = (string)Value(first, "RootPath");
            var ownerPath = Path.Combine(fixture.ReleaseRoot, ".ylr-release-owner.json");
            var journalPath = Path.Combine(fixture.ReleaseRoot, ".ylr-publish-journal.json");
            var indexPath = Path.Combine(fixture.ReleaseRoot, "release-index.json");
            var originalIndex = File.ReadAllBytes(indexPath);
            foreach (var mutation in new[] { "missing-owner", "conflicting-owner", "missing-journal", "different-attempt" })
            {
                byte[] owner = null, journal = null;
                try
                {
                    Rejected<InvalidOperationException>(() => Publisher.GetMethod("Execute", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { build, "fixture-next", default(CancellationToken), (Action<string>)(step =>
                        {
                            if (step != "BeforeIndex")
{
return;
}
owner = File.ReadAllBytes(ownerPath);
journal = File.ReadAllBytes(journalPath);
                            if (mutation == "missing-owner")
{
File.Delete(ownerPath);
} else if (mutation == "missing-journal")
{
File.Delete(journalPath);
} else
                            {
                                var file = mutation == "conflicting-owner" ? ownerPath : journalPath;
                                var data = JObject.Parse(File.ReadAllText(file));
                                data[mutation == "conflicting-owner" ? "ProjectGuid" : "Attempt"] = new string('0', 32);
                                File.WriteAllText(file, data.ToString(Formatting.None));
                            }
                        }) }));
                    CollectionAssert.AreEqual(originalIndex, File.ReadAllBytes(indexPath), mutation);
                }
                finally
                {
                    if (owner != null)
                    {
                        File.WriteAllBytes(ownerPath, owner);
                    }

                    if (journal != null)
                    {
                        File.WriteAllBytes(journalPath, journal);
                    }
                }
                Recover(fixture);
            }
            fixture.AssertProtected();
        }

        [Test]
        public void UnknownOwnershipLinksCorruptionAndUnregisteredStageContentCauseNoTakeover()
        {
            using var fixture = new ResourceReleaseFixture();
            var build = fixture.Build();
            Assert.That(Publisher, Is.Not.Null);
            fixture.ReleaseRoot = (string)Publisher.GetMethod("GetReleaseRoot").Invoke(null, new object[] { build });
            var release = Publish(build);
            var root = fixture.ReleaseRoot;
            var path = (string)Value(release, "DirectoryPath");
            var unknown = Path.Combine(root, "foreign.txt");
            File.WriteAllText(unknown, "do not adopt");
            var before = ResourceReleaseFixture.Snapshot(root);
            Rejected<InvalidOperationException>(() => Publish(build));
            CollectionAssert.AreEqual(before, ResourceReleaseFixture.Snapshot(root));
            File.Delete(unknown);
            var ownerPath = Path.Combine(root, ".ylr-release-owner.json");
            var ownerBytes = File.ReadAllBytes(ownerPath);
            var owner = JObject.Parse(File.ReadAllText(ownerPath));
            owner["ProjectGuid"] = new string('0', 32);
            File.WriteAllText(ownerPath, owner.ToString(Formatting.None));
            before = ResourceReleaseFixture.Snapshot(root);
            Rejected<InvalidOperationException>(() => Publish(build));
            CollectionAssert.AreEqual(before, ResourceReleaseFixture.Snapshot(root));
            File.WriteAllBytes(ownerPath, ownerBytes);
            var junction = Path.Combine(root, "owned-link");
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + junction + "\" \"" + Path.GetFullPath(fixture.Folder) + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero, process.StandardError.ReadToEnd());
            }
            Rejected<InvalidOperationException>(() => Publish(build));
            Directory.Delete(junction);
            using (File.Open(Path.Combine(root, ".ylr-release.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Rejected<IOException>(() => Publish(build));
            }

            var bundlePath = Path.Combine(path, ((ResourceCatalogDocument)Value(release, "Catalog")).Bundles[0].RelativeFile);
            var saved = File.ReadAllBytes(bundlePath);
            File.AppendAllText(bundlePath, "corrupt immutable output");
            before = ResourceReleaseFixture.Snapshot(root);
            Rejected<InvalidDataException>(() => Publish(build));
            CollectionAssert.AreEqual(before, ResourceReleaseFixture.Snapshot(root));
            File.WriteAllBytes(bundlePath, saved);

            File.WriteAllText(fixture.TextPath, "new generation for partial-stage test");
            AssetDatabase.ImportAsset(fixture.TextPath, ImportAssetOptions.ForceSynchronousImport);
            build = fixture.Build();
            Rejected<IOException>(() => Execute(build, default, step =>
            {
                if (step == "FileCopied")
                {
                    throw new IOException("partial copy");
                }
            }));
            var stage = Directory.GetDirectories(Path.Combine(root, ".staging")).Single();
            var foreign = Path.Combine(stage, "foreign.txt");
            File.WriteAllText(foreign, "unregistered content");
            before = ResourceReleaseFixture.Snapshot(root);
            Rejected<InvalidOperationException>(() => Recover(fixture));
            CollectionAssert.AreEqual(before, ResourceReleaseFixture.Snapshot(root));
            File.Delete(foreign);
            Recover(fixture);
            Assert.That(Directory.GetDirectories(Path.Combine(root, ".staging")), Is.Empty);
            var workspaceArtifact = Path.Combine(build.WorkspacePath, build.Bundles[0].Name);
            File.AppendAllText(workspaceArtifact, "corrupt cached build");
            before = ResourceReleaseFixture.Snapshot(root);
            Rejected<InvalidDataException>(() => Publish(build));
            CollectionAssert.AreEqual(before, ResourceReleaseFixture.Snapshot(root));
            fixture.AssertProtected();
        }

        private object Publish(ResourceBuildResult build, CancellationToken cancellation = default) =>
            Publisher.GetMethod("Publish").Invoke(null, new object[] { build, "fixture-v1", cancellation });
        private object Execute(ResourceBuildResult build, CancellationToken cancellation, Action<string> checkpoint) =>
            Publisher.GetMethod("Execute", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { build, "fixture-v1", cancellation, checkpoint });
        private object Recover(ResourceReleaseFixture fixture) => Publisher.GetMethod("Recover").Invoke(null,
            new object[] { fixture.Project, EditorUserBuildSettings.activeBuildTarget, "fixture-v1" });
        private static object Value(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
        private static void Rejected<T>(Action action) where T : Exception
        {
            var error = Assert.Throws<TargetInvocationException>(() => action());
            Assert.That(error.InnerException, Is.InstanceOf<T>(), error.InnerException?.ToString());
        }
        private static void AssertNativeText(string path, ResourceCatalogDocument document, string text)
        {
            var asset = document.Assets.Single(x => x.AssetType == typeof(TextAsset).FullName);
            var bundle = AssetBundle.LoadFromFile(Path.Combine(path, document.Bundles.Single(x => x.LogicalId == asset.BundleId).RelativeFile));
            try
            {
                Assert.That(bundle.LoadAsset<TextAsset>(asset.LoadName).text, Is.EqualTo(text));
            }
            finally
            {
                if (bundle)
                {
                    bundle.Unload(true);
                }
            }
        }
    }
}
