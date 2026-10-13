using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceOutputSafetyTests
    {
        private static readonly Type Operation = typeof(BuildScript).Assembly.GetType("YLFramework.Editor.Resource.ResourceBuildOperation");
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath("Temp/Resource62-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        private static void Set(object value, string field, object data) =>
            value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);

        private object Output(string path, string owner = "default", string root = null)
        {
            var output = Activator.CreateInstance(Operation.GetNestedType("Output", BindingFlags.NonPublic), true);
            Set(output, "Root", root ?? Path.GetDirectoryName(path));
            Set(output, "Path", path);
            Set(output, "Owner", owner);
            Set(output, "Platform", "Windows");
            Set(output, "Builds", new[] { new AssetBundleBuild { assetBundleName = "isolated", assetNames = new[] { "isolated.txt" } } });
            return output;
        }

        private object Plan(params object[] outputs)
        {
            var plan = Activator.CreateInstance(Operation.GetNestedType("Plan", BindingFlags.NonPublic), true);
            Set(plan, "Target", BuildTarget.StandaloneWindows64);
            Set(plan, "Platform", "Windows");
            var list = (IList)plan.GetType().GetField("Outputs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plan);
            foreach (var output in outputs) list.Add(output);
            return plan;
        }

        private string Owned(string container, string owner = "default")
        {
            var path = Path.Combine(_root, container, "Windows");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "isolated"), "old valid product");
            File.WriteAllText(Path.Combine(path, ".yl-resource-output.json"), Newtonsoft.Json.JsonConvert.SerializeObject(
                new { Version = 1, Project = Path.GetFullPath(Application.dataPath), Owner = owner, Platform = "Windows", Files = new[] { "isolated" } }));
            return path;
        }

        private static bool Execute(object plan, bool build, Func<string, bool> confirm,
            Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> builder = null) =>
            (bool)ResourceEditorTests.Invoke(Operation, "Execute", plan, build, confirm, builder);

        [Test]
        public void UnknownDirectoryCannotBeCleanedOrReplaced()
        {
            var path = Path.Combine(_root, "unknown", "Windows");
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "foreign"), "keep");
            var plan = Plan(Output(path));
            foreach (var build in new[] { false, true })
                Assert.Throws<InvalidOperationException>(() => Execute(plan, build, _ => { Assert.Fail("Must reject before confirmation"); return true; }));
            Assert.AreEqual("keep", File.ReadAllText(Path.Combine(path, "foreign")));
        }

        [Test]
        public void OwnerConflictAndUnknownAddedFilesAreRejected()
        {
            var path = Owned("owned");
            Assert.Throws<InvalidOperationException>(() => Execute(Plan(Output(path, "different")), false, _ => true));
            File.WriteAllText(Path.Combine(path, "foreign"), "keep");
            Assert.Throws<InvalidOperationException>(() => Execute(Plan(Output(path)), false, _ => true));
            Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(path, "isolated")));
            Assert.AreEqual("keep", File.ReadAllText(Path.Combine(path, "foreign")));
        }

        [Test]
        public void ParentOutOfRootAndOverlappingOutputsAreRejected()
        {
            var root = Path.Combine(_root, "container");
            foreach (var path in new[] { root, Path.Combine(root, "..", "Windows"), Path.Combine(root, "Windows", "child") })
                Assert.Throws<InvalidOperationException>(() => ResourceEditorTests.Invoke(Operation, "ValidateOutput", Output(path, root: root)));
            var owned = Owned("owned");
            Assert.Throws<InvalidOperationException>(() => Execute(Plan(Output(owned), Output(owned)), false, _ => true));
            Assert.IsTrue(Directory.Exists(owned));
        }

        [Test]
        public void CancelBuildAndClearHaveNoFilesystemSideEffects()
        {
            var path = Owned("owned");
            var before = Directory.GetFiles(path).ToDictionary(file => file, file => (File.ReadAllBytes(file), File.GetLastWriteTimeUtc(file)));
            foreach (var build in new[] { false, true })
            {
                Assert.IsFalse(Execute(Plan(Output(path)), build, summary => { StringAssert.Contains(path, summary); return false; }));
                foreach (var file in before)
                {
                    CollectionAssert.AreEqual(file.Value.Item1, File.ReadAllBytes(file.Key));
                    Assert.AreEqual(file.Value.Item2, File.GetLastWriteTimeUtc(file.Key));
                }
            }
        }

        [Test]
        public void ClearPreservesParentSiblingOtherPlatformAndUnrelatedProducts()
        {
            var path = Owned("owned");
            var sibling = Path.Combine(Path.GetDirectoryName(path), "Unknown");
            Directory.CreateDirectory(sibling);
            File.WriteAllText(Path.Combine(sibling, "foreign"), "keep");
            var platform = Path.Combine(Path.GetDirectoryName(path), "Android");
            Directory.CreateDirectory(platform);
            Assert.IsTrue(Execute(Plan(Output(path)), false, _ => true));
            Assert.IsFalse(Directory.Exists(path));
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(path)));
            Assert.IsTrue(Directory.Exists(platform));
            Assert.AreEqual("keep", File.ReadAllText(Path.Combine(sibling, "foreign")));
        }

        [Test]
        public void NullOrThrowingBuildKeepsAllOldOutputs()
        {
            var first = Owned("build");
            var second = Owned("streaming");
            var plan = Plan(Output(first), Output(second));
            Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> nullBuilder = (_, _, _) => null;
            Assert.Throws<InvalidOperationException>(() => Execute(plan, true, _ => true, nullBuilder));
            Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> failedBuilder = (_, _, _) => throw new IOException("isolated failure");
            Assert.Throws<IOException>(() => Execute(plan, true, _ => true, failedBuilder));
            Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(first, "isolated")));
            Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(second, "isolated")));
        }

        [Test]
        public void LaterClearFailureRollsBackEarlierOutput()
        {
            var first = Owned("build");
            var second = Owned("streaming");
            using (var locked = new FileStream(Path.Combine(second, "isolated"), FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => Execute(Plan(Output(first), Output(second)), false, _ => true));
            Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(first, "isolated")));
            Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(second, "isolated")));
        }

        [TestCase(BuildTarget.StandaloneWindows64, "Windows")]
        [TestCase(BuildTarget.Android, "Android")]
        [TestCase(BuildTarget.StandaloneOSX, "OSX")]
        public void RequestedPlatformDeterminesOutputName(BuildTarget target, string expected)
        {
            Assert.AreEqual(expected, ResourceEditorTests.Invoke(Operation, "GetPlatformName", target));
        }

        [Test]
        public void UnsupportedPlatformAndUnsafeOwnerNamesAreRejected()
        {
            Assert.Throws<NotSupportedException>(() => ResourceEditorTests.Invoke(Operation, "GetPlatformName", BuildTarget.NoTarget));
            foreach (var name in new[] { "../MainModule", "", "AssetBundles", "Windows", "CON", "owner/child", "owner.", "C:\\out" })
                Assert.Throws<InvalidOperationException>(() => ResourceEditorTests.Invoke(Operation, "ValidateOwnerName", name));
        }

        [TestCase(".yl-resource-output.json")]
        [TestCase("windows/child")]
        [TestCase("asset_bundle_config.bin/child")]
        public void BundleCannotOverwriteManifestOwnershipOrDataTable(string name)
        {
            Assert.Throws<InvalidOperationException>(() => ResourceEditorTests.Invoke(Operation, "ValidateBundleName", name));
        }

        [Test]
        public void IntegrationIsolationGateRejectsAnExtraOwnerBeforeAnyOperation()
        {
            var owned = Owned("probe");
            var foreign = Owned("foreign", "foreign-owner");
            var method = typeof(ResourceBuildIntegrationTests).GetMethod("AssertIsolatedPlan", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Integration tests need a full-plan isolation gate.");
            var foreignOutput = Output(foreign, "foreign-owner");
            Set(foreignOutput, "Builds", Array.Empty<AssetBundleBuild>());
            Assert.Throws<AssertionException>(() => ResourceEditorTests.Invoke(typeof(ResourceBuildIntegrationTests), "AssertIsolatedPlan",
                Plan(Output(owned), foreignOutput), new[] { owned }, new[] { "isolated.txt" }));
            var wrongSource = Output(owned);
            Set(wrongSource, "Builds", new[] { new AssetBundleBuild { assetBundleName = "foreign", assetNames = new[] { "foreign.bytes" } } });
            Assert.Throws<AssertionException>(() => ResourceEditorTests.Invoke(typeof(ResourceBuildIntegrationTests), "AssertIsolatedPlan",
                Plan(wrongSource), new[] { owned }, new[] { "isolated.txt" }));
            Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(foreign, "isolated")));
        }

        [Test]
        public void LeafMetaMoveFailureRestoresOldDirectoryAndMetadata()
        {
            var path = Owned("owned");
            File.WriteAllText(path + ".meta", "old owned leaf identity");
            using (var locked = new FileStream(path + ".meta", FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => Execute(Plan(Output(path)), false, _ => true));
            Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(path, "isolated")));
            Assert.AreEqual("old owned leaf identity", File.ReadAllText(path + ".meta"));
        }

        [Test]
        public void JunctionAncestorAndNestedJunctionAreRejectedWithoutFollowingTarget()
        {
            var target = Owned("target");
            var container = Path.Combine(_root, "link");
            var path = Owned("owned");
            var nested = Path.Combine(path, "nested");
            try
            {
                CreateJunction(container, Path.GetDirectoryName(target));
                var ancestor = Assert.Throws<InvalidOperationException>(() => Execute(Plan(Output(Path.Combine(container, "Windows"))), false, _ => true));
                StringAssert.Contains("junction", ancestor.Message);
                CreateJunction(nested, Path.GetDirectoryName(target));
                var descendant = Assert.Throws<InvalidOperationException>(() => Execute(Plan(Output(path)), false, _ => true));
                StringAssert.Contains("junction", descendant.Message);
                Assert.AreEqual("old valid product", File.ReadAllText(Path.Combine(target, "isolated")));
            }
            finally
            {
                if (Directory.Exists(nested)) Directory.Delete(nested);
                if (Directory.Exists(container)) Directory.Delete(container);
            }
        }

        internal static void CreateJunction(string link, string target)
        {
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(link)) && Directory.Exists(target));
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.AreEqual(0, process.ExitCode, output);
            }
            Assert.IsTrue((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
        }
    }
}
