using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using YLFramework.Editor.Resource;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceOutputSafetyTests
    {
        private string _root;
        private static Type Output => typeof(ResourceCodeGenerator).Assembly.GetType("YLFramework.Editor.Resource.ResourceReferenceOutput");
        [SetUp] public void SetUp() => _root = "Assets/Safety82c-" + Guid.NewGuid().ToString("N");
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }

            if (File.Exists(_root + ".meta"))
            {
                File.Delete(_root + ".meta");
            }
        }
        private static bool Publish(string path) => (bool)ResourceEditorTests.Invoke(Output, "Publish", path, "namespace Safe82c { public static class References {} }\n");

        [Test]
        public void GenerationRefusesAncestorAssemblyFileOccupancyAndOutOfAssetsPaths()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(_root + "/User.asmdef", "{\"name\":\"UserAssembly\"}");
            Assert.Throws<InvalidOperationException>(() => Publish(_root + "/Generated"));
            Assert.That(Directory.Exists(_root + "/Generated"), Is.False);
            Assert.That(File.ReadAllText(_root + "/User.asmdef"), Is.EqualTo("{\"name\":\"UserAssembly\"}"));
            File.Delete(_root + "/User.asmdef");
            File.WriteAllText(_root + "/Generated", "occupied by user file");
            Assert.Throws<InvalidOperationException>(() => Publish(_root + "/Generated"));
            Assert.That(File.ReadAllText(_root + "/Generated"), Is.EqualTo("occupied by user file"));
            Assert.Throws<InvalidOperationException>(() => Publish("Packages/Generated82c"));
        }

        [Test]
        public void JunctionAncestorsAndNestedLinksAreRejectedWithoutFollowingTheirTargets()
        {
            Directory.CreateDirectory(_root);
            var outside = Path.GetFullPath("Temp/Safety82c-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "foreign"), "keep");
            var link = Path.GetFullPath(_root + "/Linked");
            try
            {
                CreateJunction(link, outside);
                Assert.Throws<InvalidOperationException>(() => Publish(_root + "/Linked/Generated"));
                Assert.Throws<InvalidOperationException>(() => Publish(_root));
                Assert.That(File.ReadAllText(Path.Combine(outside, "foreign")), Is.EqualTo("keep"));
                Assert.That(Directory.Exists(Path.Combine(outside, "Generated")), Is.False);
            }
            finally
            {
                if (Directory.Exists(link))
                {
                    Directory.Delete(link);
                }
                Directory.Delete(outside, true);
            }
        }

        [Test]
        public void LiveDuplicateAssemblyIsRejectedEvenForByteIdenticalGeneration()
        {
            Assert.That(Publish(_root), Is.True);
            var source = File.ReadAllBytes(_root + "/YLResourceReferences.cs");
            var foreign = "Assets/ForeignAssembly82c-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(foreign));
            try
            {
                var path = foreign + "/Foreign.asmdef";
                File.WriteAllText(path, "{\"name\":\"YLFramework.Generated.ResourceNames\",\"references\":[],\"noEngineReferences\":true}");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Assert.Throws<InvalidOperationException>(() => Publish(_root));
                CollectionAssert.AreEqual(source, File.ReadAllBytes(_root + "/YLResourceReferences.cs"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(foreign);
            }
        }

        internal static void CreateJunction(string link, string target)
        {
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(link)) && Directory.Exists(target));
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = System.Diagnostics.Process.Start(start);
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.EqualTo(0), output);
            Assert.That((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0, Is.True);
        }
    }
}
