using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using YLFramework.Editor.Resource;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceCodeGenerationTests
    {
        private string _folder;
        private string _bundle;
        private string _output;

        [SetUp]
        public void SetUp()
        {
            Assert.IsEmpty(AssetDatabase.GetAllAssetBundleNames(), "Probe requires no external Bundle marks.");
            Assert.IsEmpty(AssetDatabase.FindAssets("t:SubProject"), "Probe requires no external owners.");
            _folder = "Assets/YLFramework/Tests/EditMode/Names63-" + Guid.NewGuid().ToString("N");
            _bundle = "names63_" + Guid.NewGuid().ToString("N");
            _output = "Assets/Names63-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode", Path.GetFileName(_folder));
        }

        [TearDown]
        public void TearDown()
        {
            if (_folder != null) AssetDatabase.DeleteAsset(_folder);
            if (_output != null && Directory.Exists(_output))
            {
                Directory.Delete(_output, true);
            }
            if (_output != null && File.Exists(_output + ".meta")) File.Delete(_output + ".meta");
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        private void Text(string name, string bundle)
        {
            var path = _folder + "/" + name + ".bytes";
            File.WriteAllText(path, "isolated resource names");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path);
            importer.assetBundleName = bundle;
            importer.SaveAndReimport();
        }

        [Test]
        public void MultipleBundlesEmitEachClassExactlyOnce()
        {
            Text("First", _bundle + "a");
            Text("Second", _bundle + "b");
            using var writer = new StringWriter();
            ResDataCodeGenerator.WriteClass(writer, "YLFramework.Services.Resource");
            Assert.AreEqual(2, Regex.Matches(writer.ToString(), "public class ").Count);
            StringAssert.Contains("First", writer.ToString());
            StringAssert.Contains("Second", writer.ToString());
        }

        [Test]
        public void DottedResourceKeyIsNotStrippedTwice()
        {
            Text("hero.icon", _bundle);
            using var writer = new StringWriter();
            ResDataCodeGenerator.WriteClass(writer, "YLFramework.Services.Resource");
            StringAssert.Contains("\"hero.icon\"", writer.ToString());
        }

        [Test]
        public void EntireMemberNameIsNormalized()
        {
            Text("hero-icon", _bundle);
            using var writer = new StringWriter();
            ResDataCodeGenerator.WriteClass(writer, "YLFramework.Services.Resource");
            StringAssert.DoesNotContain(" Hero-icon ", writer.ToString());
            StringAssert.Contains("\"hero-icon\"", writer.ToString());
        }

        [Test]
        public void MemberCollisionFailsBeforeWritingAnything()
        {
            Text("hero", _bundle);
            // 使用不同扩展名构成同一资源键，不依赖文件系统大小写规则。
            var path = _folder + "/hero.json";
            File.WriteAllText(path, "{}");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path);
            importer.assetBundleName = _bundle;
            importer.SaveAndReimport();
            using var writer = new StringWriter();
            writer.Write("existing valid output");
            Assert.Throws<InvalidOperationException>(() => ResDataCodeGenerator.WriteClass(writer, "YLFramework.Services.Resource"));
            Assert.AreEqual("existing valid output", writer.ToString());
        }

        private static string Generate(params AssetBundleInfo[] inputs) => (string)ResourceEditorTests.Invoke(
            typeof(ResDataCodeGenerator), "Generate", inputs, "YLFramework.Services.Resource");

        private static AssetBundleInfo Bundle(string name, params string[] keys) => new AssetBundleInfo(name) { Assets = keys };

        private void Publish(string source) => ResourceEditorTests.Invoke(typeof(BuildScript).Assembly.GetType(
            "YLFramework.Editor.Resource.ResReferenceOutput"), "Publish", _output, source);

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("---")]
        [TestCase("bad\nkey")]
        public void UnusableResourceNamesAreRejected(string key)
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("probe63", key)));
        }

        [TestCase("")]
        [TestCase("Upper")]
        [TestCase("../bad")]
        public void InvalidBundleInputIsRejected(string name)
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle(name, "key")));
        }

        [Test]
        public void ClassAndReservedMemberCollisionsAreRejected()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("a-b", "key"), Bundle("a_b", "other")));
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("probe63", "a-b", "a_b")));
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("probe63", "bundleName")));
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("probe63", "probe63")));
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("res", "key")), "Runtime type collision must be rejected.");
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("probe63")));
        }

        [Test]
        public void EmptyInputIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Array.Empty<AssetBundleInfo>()));
        }

        [Test]
        public void RuntimeKeyCollisionIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(Bundle("probe63", "hero", "HERO")));
        }

        [Test]
        public void ExistingNamespaceCollisionIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => ResourceEditorTests.Invoke(typeof(ResDataCodeGenerator), "Generate",
                new[] { Bundle("resourcenamesspace63", "key") }, "YLFramework.Tests.ResourceEditor"));
        }

        [Test]
        public void SortingAndInvariantIdentifiersKeepOutputStableAndKeysOriginal()
        {
            var before = CultureInfo.CurrentCulture;
            try
            {
                var first = Generate(Bundle("9-art", "item.icon", "9-item", "class", "quote\"slash\\key"), Bundle("icons", "ICON"));
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                var second = Generate(Bundle("icons", "ICON"), Bundle("9-art", "quote\"slash\\key", "class", "9-item", "item.icon"));
                Assert.AreEqual(first, second);
                StringAssert.Contains("class _9_art", first);
                StringAssert.Contains("_9_item", first);
                StringAssert.Contains("\"item.icon\"", first);
                StringAssert.Contains("\"ICON\"", first);
                StringAssert.Contains("\"9-art\"", first);
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        [Test]
        public void InvalidNamespaceAndNullInputsAreRejectedBeforeWriting()
        {
            Assert.Throws<ArgumentException>(() => ResourceEditorTests.Invoke(typeof(ResDataCodeGenerator), "Generate",
                new[] { Bundle("probe63", "key") }, "bad-namespace"));
            Assert.Throws<ArgumentNullException>(() => Generate(null));
            Assert.Throws<InvalidOperationException>(() => Generate(new AssetBundleInfo[] { null }));
        }

        [Test]
        public void AtomicUpdatePreservesMetaAndNoOpTimestampAndLockedFile()
        {
            var first = Generate(Bundle("probe63", "First"));
            Publish(first);
            var path = _output + "/YLResData.cs";
            var asmdef = _output + "/YLFramework.Generated.ResourceNames.asmdef";
            // 只用自有 metadata 哨兵测试文件写入，不让该隔离源码加入当前编译。
            File.WriteAllText(path + ".meta", "source identity sentinel");
            File.WriteAllText(asmdef + ".meta", "assembly identity sentinel");
            var time = File.GetLastWriteTimeUtc(path);
            var old = File.ReadAllBytes(path);
            Publish(first);
            Assert.AreEqual(time, File.GetLastWriteTimeUtc(path));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => Publish(Generate(Bundle("probe63", "Second"))));
            CollectionAssert.AreEqual(old, File.ReadAllBytes(path));
            Assert.AreEqual(2, Directory.GetFiles(_output).Count(x => !x.EndsWith(".meta")), "No staging files may remain.");
            Publish(Generate(Bundle("probe63", "Second")));
            Assert.AreEqual("source identity sentinel", File.ReadAllText(path + ".meta"));
            Assert.AreEqual("assembly identity sentinel", File.ReadAllText(asmdef + ".meta"));
            StringAssert.Contains("\"Second\"", File.ReadAllText(path));
        }

        [Test]
        public void UnknownSourceAssemblyAndEditedGeneratedFilesAreNotTakenOver()
        {
            Directory.CreateDirectory(_output);
            var user = _output + "/User.cs";
            File.WriteAllText(user, "// user source");
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(Bundle("probe63", "Key"))));
            Assert.AreEqual("// user source", File.ReadAllText(user));
            Assert.IsFalse(File.Exists(_output + "/YLResData.cs"));
            File.Delete(user);
            Publish(Generate(Bundle("probe63", "Key")));
            var path = _output + "/YLResData.cs";
            File.AppendAllText(path, "// handwritten change");
            var old = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(Bundle("probe63", "Other"))));
            CollectionAssert.AreEqual(old, File.ReadAllBytes(path));
        }

        [Test]
        public void InvalidInputsPreservePreviouslyPublishedSource()
        {
            Publish(Generate(Bundle("probe63", "Key")));
            var path = _output + "/YLResData.cs";
            var before = File.ReadAllBytes(path);
            foreach (var invalid in new[]
                     {
                         Array.Empty<AssetBundleInfo>(), new[] { Bundle("probe63", "") },
                         new[] { Bundle("a-b", "Key"), Bundle("a_b", "Other") },
                         new[] { Bundle("probe63", "a-b", "a_b") }, new[] { Bundle("probe63", "hero", "HERO") }
                     })
            {
                Assert.Throws<InvalidOperationException>(() => Publish(Generate(invalid)));
                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            }
        }

        [Test]
        public void UnknownAssemblyAndOrphanMetaAreNotTakenOver()
        {
            File.WriteAllText(_output + ".meta", "unknown folder identity");
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(Bundle("probe63", "Key"))));
            Assert.IsFalse(Directory.Exists(_output));
            Assert.AreEqual("unknown folder identity", File.ReadAllText(_output + ".meta"));
            File.Delete(_output + ".meta");
            Directory.CreateDirectory(_output);
            var asmdef = _output + "/YLFramework.Generated.ResourceNames.asmdef";
            File.WriteAllText(asmdef, "{\"name\":\"UserAssembly\"}");
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(Bundle("probe63", "Key"))));
            Assert.AreEqual("{\"name\":\"UserAssembly\"}", File.ReadAllText(asmdef));
            Assert.IsFalse(File.Exists(_output + "/YLResData.cs"));
        }

        [Test]
        public void CrossOwnerPreflightIsReusedByGeneration()
        {
            AssetDatabase.CreateFolder(_folder, "Owner");
            var owner = UnityEngine.ScriptableObject.CreateInstance<YLFramework.Services.Resource.SubProject>();
            AssetDatabase.CreateAsset(owner, _folder + "/Owner/Owner63.asset");
            Text("First", _bundle);
            var path = _folder + "/Owner/Second.bytes";
            File.WriteAllText(path, "owned text");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path);
            importer.assetBundleName = _bundle;
            importer.SaveAndReimport();
            using var writer = new StringWriter();
            Assert.Throws<InvalidOperationException>(() => ResDataCodeGenerator.WriteClass(writer, "YLFramework.Services.Resource"));
            Assert.IsEmpty(writer.ToString());
        }
    }
}

namespace YLFramework.Tests.ResourceEditor.Resourcenamesspace63
{
    internal class Marker { }
}
