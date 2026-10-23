using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;
using YLFramework.Tests.ResourceBuild;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceCodeGenerationTests
    {
        private string _output;
        [SetUp] public void SetUp() => _output = "Assets/Names82c-" + Guid.NewGuid().ToString("N");
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_output))
            {
                Directory.Delete(_output, true);
            }

            if (File.Exists(_output + ".meta"))
            {
                File.Delete(_output + ".meta");
            }
        }

        internal static ResourceCatalogDocument Catalog(params string[] addresses)
        {
            var data = ResourceCatalogCodecTests.Payload();
            var original = data["Assets"][0];
            data["Assets"] = new JArray(addresses.Select((address, index) =>
            {
                var asset = original.DeepClone();
                var guid = (index + 1).ToString("x32", CultureInfo.InvariantCulture);
                asset["Guid"] = guid;
                asset["AssetId"] = guid + "_11400000";
                asset["Address"] = address;
                asset["LoadName"] = "assets/owned/file" + index + ".txt";
                return asset;
            }));
            data["Groups"][0]["Members"] = new JArray(((JArray)data["Assets"]).Select(x => new JObject { ["AssetId"] = (string)x["AssetId"], ["Required"] = true }));
            return Read(data);
        }
        internal static ResourceCatalogDocument Read(JObject data)
        {
            var bytes = ResourceCatalogCodec.Seal(Encoding.UTF8.GetBytes(data.ToString(Formatting.None)));
            return ResourceCatalogCodec.Read(bytes, (string)data["ProjectId"], (string)data["Platform"], (string)data["UnityCompatibility"], (string)data["AppContentCompatibilityId"]);
        }
        private static string Generate(ResourceCatalogDocument[] documents, string space = "YLFramework.Resources") =>
            (string)ResourceEditorTests.Invoke(typeof(ResourceCodeGenerator), "GenerateCatalogs", documents, space);
        private bool Publish(string source) => (bool)ResourceEditorTests.Invoke(typeof(ResourceCodeGenerator).Assembly.GetType(
            "YLFramework.Editor.Resource.ResourceReferenceOutput"), "Publish", _output, source);

        [Test]
        public void FormalAddressGroupAndProjectKeysKeepOriginalValuesAndNoBundleNames()
        {
            var source = Generate(new[] { Catalog("hero.icon", "ui/9-item", "class") });
            StringAssert.Contains("public static class Catalog", source);
            StringAssert.Contains("public static class Addresses", source);
            StringAssert.Contains("public static class Groups", source);
            StringAssert.Contains("\"hero.icon\"", source);
            StringAssert.Contains("\"ui/9-item\"", source);
            StringAssert.Contains("\"class\"", source);
            StringAssert.Contains("\"main\"", source);
            StringAssert.DoesNotContain("BundleName", source);
            StringAssert.DoesNotContain("ylr_", source);
        }

        [Test]
        public void SubAssetKeysAndAliasesRemainExplicitAndCanonical()
        {
            var data = JObject.Parse(Encoding.UTF8.GetString(ResourceCatalogCodec.Write(Catalog("Sprites.png"))));
            data.Remove("BuildId");
            data["Assets"][0]["AssetType"] = "UnityEngine.Texture2D";
            data["Assets"][0]["Aliases"] = new JArray("sprite-alias");
            var sub = data["Assets"][0].DeepClone();
            sub["LocalId"] = 21300000;
            sub["AssetId"] = (string)sub["Guid"] + "_21300000";
            sub["AssetType"] = "UnityEngine.Sprite";
            sub["Kind"] = "SubAsset";
            sub["SubAssetName"] = "Left";
            sub["SubAssetKey"] = "21300000";
            ((JArray)data["Assets"]).Add(sub);
            var source = Generate(new[] { Read(data) });
            StringAssert.Contains("public static class SubAssets", source);
            StringAssert.Contains("public static class SpritesPngLeft", source);
            StringAssert.Contains("Key = \"21300000\"", source);
            StringAssert.Contains("Address = \"Sprites.png\"", source);
            StringAssert.Contains("SpriteAlias = \"sprite-alias\"", source);
        }

        [Test]
        public void CollisionsAndInvalidNamespaceFailBeforeWriterOrOutputChanges()
        {
            Publish(Generate(new[] { Catalog("valid.txt") }));
            var before = File.ReadAllBytes(_output + "/YLResourceReferences.cs");
            foreach (var documents in new[] { new[] { Catalog("a-b", "a_b") }, new[] { Catalog("addresses") }, Array.Empty<ResourceCatalogDocument>(), new ResourceCatalogDocument[] { null } })
            {
                Assert.Throws<InvalidOperationException>(() => Publish(Generate(documents)));
            }

            Assert.Throws<ArgumentException>(() => Generate(new[] { Catalog("valid") }, "bad-namespace"));
            Assert.Throws<ArgumentNullException>(() => Generate(null));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(_output + "/YLResourceReferences.cs"));
        }

        [Test]
        public void ExistingTypesNamespacesAndDuplicateProjectsCannotBeShadowed()
        {
            Assert.Throws<InvalidOperationException>(() => Generate(new[] { Catalog("key") }, "YLFramework.Tests.ResourceEditor"));
            Assert.Throws<InvalidOperationException>(() => Generate(new[] { Catalog("key"), Catalog("other") }));
        }

        [Test]
        public void VerbatimNamespaceCannotBypassCompiledTypeCollisionOrChangeOwnedOutput()
        {
            Publish(Generate(new[] { Catalog("Key") }));
            var path = _output + "/YLResourceReferences.cs";
            var before = File.ReadAllBytes(path);
            var time = File.GetLastWriteTimeUtc(path);
            Assert.Throws<ArgumentException>(() => Publish(Generate(new[] { Catalog("Key") }, "@YLFramework.Tests.ResourceEditor")));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(time));
        }

        [Test]
        public void SortingAndCultureKeepGeneratedBytesStable()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                var first = Generate(new[] { Catalog("item.icon", "9-item", "class", "quote\"key") });
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                var second = Generate(new[] { Catalog("quote\"key", "class", "9-item", "item.icon") });
                Assert.That(second, Is.EqualTo(first));
                StringAssert.Contains("_9Item", first);
                StringAssert.Contains("QuoteKey", first);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void IdentifiersUseInvariantPascalCaseWhileAddressesAndOwnedLegacyOutputStayIntact()
        {
            var catalog = Catalog("HTTP_URL", "ui/data-file", "part_9_name", "Café_map", "alreadyPascal", "XML_document");
            var source = Generate(new[] { catalog });
            foreach (var expected in new[] { "HttpUrl", "UIDataFile", "Part9Name", "CaféMap", "AlreadyPascal", "XmlDocument" })
            {
                StringAssert.Contains("public const string " + expected + " = ", source);
            }
            foreach (var address in catalog.Assets.Select(asset => asset.Address))
            {
                StringAssert.Contains("\"" + address + "\"", source);
            }
            var modern = Generate(new[] { Catalog("hero.icon") });
            StringAssert.Contains("HeroIcon = \"hero.icon\"", modern);
            var legacy = modern.Replace("HeroIcon =", "Hero_icon =");
            Assert.That(Publish(legacy), Is.True);
            var path = _output + "/YLResourceReferences.cs";
            File.WriteAllText(path + ".meta", "legacy source identity");
            File.WriteAllText(_output + "/YLFramework.Generated.ResourceNames.asmdef.meta", "legacy assembly identity");
            Assert.That(Publish(modern), Is.True, "A valid old generated body remains owned and can be regenerated.");
            Assert.That(Publish(modern), Is.False, "Second generation must be byte-stable.");
            Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo("legacy source identity"));
            Assert.That(File.ReadAllText(_output + "/YLFramework.Generated.ResourceNames.asmdef.meta"), Is.EqualTo("legacy assembly identity"));
            File.AppendAllText(path, "// handwritten change");
            var changed = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => Publish(legacy));
            CollectionAssert.AreEqual(changed, File.ReadAllBytes(path));
        }

        [Test]
        public void AtomicUpdatesPreserveMetaNoOpTimestampAndOldSourceOnRealIoFailure()
        {
            var first = Generate(new[] { Catalog("First") });
            Assert.That(Publish(first), Is.True);
            var path = _output + "/YLResourceReferences.cs";
            var asmdef = _output + "/YLFramework.Generated.ResourceNames.asmdef";
            File.WriteAllText(path + ".meta", "source identity sentinel");
            File.WriteAllText(asmdef + ".meta", "assembly identity sentinel");
            var time = File.GetLastWriteTimeUtc(path);
            var old = File.ReadAllBytes(path);
            Assert.That(Publish(first), Is.False);
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(time));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => Publish(Generate(new[] { Catalog("Second") })));
            }

            CollectionAssert.AreEqual(old, File.ReadAllBytes(path));
            Assert.That(Directory.GetFiles(_output).Count(x => !x.EndsWith(".meta")), Is.EqualTo(2));
            Publish(Generate(new[] { Catalog("Second") }));
            Assert.That(File.ReadAllText(path + ".meta"), Is.EqualTo("source identity sentinel"));
            Assert.That(File.ReadAllText(asmdef + ".meta"), Is.EqualTo("assembly identity sentinel"));
        }

        [Test]
        public void UnknownEditedIncompleteAndLegacyOutputsCannotBeTakenOver()
        {
            Directory.CreateDirectory(_output);
            var user = _output + "/User.cs";
            File.WriteAllText(user, "// user source");
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(new[] { Catalog("Key") })));
            Assert.That(File.ReadAllText(user), Is.EqualTo("// user source"));
            File.Delete(user);
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(new[] { Catalog("Key") })), "Even empty unknown directories must not be adopted.");
            Directory.Delete(_output);
            Publish(Generate(new[] { Catalog("Key") }));
            var path = _output + "/YLResourceReferences.cs";
            File.AppendAllText(path, "// handwritten change");
            var old = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(new[] { Catalog("Other") })));
            CollectionAssert.AreEqual(old, File.ReadAllBytes(path));
        }

        [Test]
        public void OrphanMetaAndUnknownAssemblyAreRejectedWithoutWrites()
        {
            File.WriteAllText(_output + ".meta", "unknown identity");
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(new[] { Catalog("Key") })));
            Assert.That(Directory.Exists(_output), Is.False);
            Assert.That(File.ReadAllText(_output + ".meta"), Is.EqualTo("unknown identity"));
            File.Delete(_output + ".meta");
            Directory.CreateDirectory(_output);
            var asmdef = _output + "/YLFramework.Generated.ResourceNames.asmdef";
            File.WriteAllText(asmdef, "{\"name\":\"UserAssembly\"}");
            Assert.Throws<InvalidOperationException>(() => Publish(Generate(new[] { Catalog("Key") })));
            Assert.That(File.ReadAllText(asmdef), Is.EqualTo("{\"name\":\"UserAssembly\"}"));
            Assert.That(File.Exists(_output + "/YLResourceReferences.cs"), Is.False);
        }
    }
    internal class Catalog
    {
    }
}
