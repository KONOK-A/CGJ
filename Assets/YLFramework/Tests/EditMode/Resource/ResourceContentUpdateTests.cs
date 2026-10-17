using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public sealed partial class ResourceContentUpdateTests
    {
        [TestCase("Bundle")]
        [TestCase("Manifest")]
        public void DirectPreparationRejectsUnknownContentBeforeRecordingPrepared(string role)
        {
            using var fixture = new UpdateFixture();
            var target = fixture.Create("candidate", role);
            var plan = fixture.Store.Check(fixture.Store.Mount(), new[] { target });
            var error = Assert.Throws<ResourceException>(() => fixture.Store.Prepare(plan, CancellationToken.None),
                "Matching Catalog delivery hashes do not authenticate an unknown content encoding.");
            Assert.That(error.Kind.ToString(), Is.EqualTo("ContentFormatUnsupported"));
            Assert.That(fixture.Store.GetPreparedUpdates(), Is.Empty);
            Assert.That(File.Exists(fixture.Control("active.json")), Is.False);
            Assert.That(File.Exists(fixture.Control("last-known-good.json")), Is.False);
            Assert.That(fixture.Store.Mount().Single().Document.CatalogDigest, Is.EqualTo(fixture.BuiltIn.Document.CatalogDigest));
        }

        [Test]
        public void UnchangedProjectStillRequiresContentValidationBeforePrepared()
        {
            using var fixture = new UpdateFixture("Manifest");
            var plan = fixture.Store.Check(fixture.Store.Mount(), new[] { fixture.BuiltIn });
            Assert.That(plan.Projects.Single().IsChanged, Is.False);
            var error = Assert.Throws<ResourceException>(() => fixture.Store.Prepare(plan, CancellationToken.None));
            Assert.That(error.Kind.ToString(), Is.EqualTo("ContentFormatUnsupported"));
            Assert.That(fixture.Store.GetPreparedUpdates(), Is.Empty);
            Assert.That(File.Exists(fixture.Control("commit-journal.json")), Is.False);
        }

        [Test]
        public void CheckAndMountOnlyVerifyDeliveryBytesAndDoNotCreateAStore()
        {
            using var fixture = new UpdateFixture("Manifest");
            var current = fixture.Store.Mount();
            var plan = fixture.Store.Check(current, new[] { fixture.BuiltIn });
            Assert.That(plan.Projects.Single().ToDigest, Is.EqualTo(fixture.BuiltIn.Document.CatalogDigest));
            Assert.That(current.Single().Document.CatalogDigest, Is.EqualTo(fixture.BuiltIn.Document.CatalogDigest));
            Assert.That(Directory.Exists(fixture.Store.RootPath), Is.False,
                "Content availability cannot turn a read-only Check or Mount into key acquisition or repair.");
        }

        // 仅测试 Store 的交付与认证边界；UnityFS 前缀夹具不声称可由 native 加载。
        private sealed class UpdateFixture : IDisposable
        {
            private readonly string root = Path.Combine(Path.GetTempPath(), "kilo", "cu-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            internal ResourceLocalSnapshot BuiltIn { get; }
            internal ArtifactStore Store { get; }

            internal UpdateFixture(string invalidRole = null, ResourceRemoteSource remote = null)
            {
                BuiltIn = Create("built-in", invalidRole);
                Store = new ArtifactStore(Path.Combine(root, "store"), new[] {
                    new ResourceProjectMount("catalog", "StandaloneWindows64", "app-v1", BuiltIn, remote: remote) });
            }

            internal string Control(string name) => Path.Combine(Store.RootPath, "catalog", "StandaloneWindows64", name);
            internal string Owned(ResourceLocalSnapshot snapshot) => Path.Combine(Control("versions"), snapshot.Document.CatalogDigest);

            internal ResourceLocalSnapshot Create(string version, string invalidRole = null, bool encrypted = false, bool corruptMiddle = false)
            {
                var payload = ResourceCatalogCodecTests.Payload();
                payload["UnityCompatibility"] = Application.unityVersion;
                var files = new Dictionary<string, byte[]>();
                foreach (JObject record in ((JArray)payload["Bundles"]).Concat((JArray)payload["SupportFiles"]))
                {
                    var name = (string)record["RelativeFile"];
                    var role = record["LogicalId"] != null ? "Bundle" : name == "manifest" ? "Manifest" : null;
                    var bytes = Encoding.UTF8.GetBytes((role == null ? "support:" : role == invalidRole ? "invalid:" : "UnityFS\0") + version + ":" + name);
                    if (encrypted && role != null)
                    {
                        Array.Resize(ref bytes, 200000);
                        using var raw = new MemoryStream(bytes, false);
                        using var output = new MemoryStream();
                        ResourceBundleEnvelope.Encrypt(raw, output, new ResourceBundleContext("catalog", "StandaloneWindows64", Application.unityVersion,
                            role, role == "Bundle" ? (string)record["LogicalId"] : "manifest"), "test-content", TestMaterial);
                        bytes = output.ToArray();
                        if (corruptMiddle && role == "Bundle") bytes[12 + BitConverter.ToInt32(bytes, 8) + 65536 + 50] ^= 1;
                    }
                    record["Length"] = bytes.Length;
                    record["Sha256"] = ResourceCatalogCodec.Digest(bytes);
                    files.Add(name, bytes);
                }
                var catalogBytes = ResourceCatalogCodec.Seal(ResourceCatalogCodec.CanonicalBytes(payload));
                var document = ResourceCatalogCodec.Read(catalogBytes, "catalog", "StandaloneWindows64", Application.unityVersion, "app-v1");
                var path = Path.Combine(root, "source", document.CatalogDigest);
                Directory.CreateDirectory(path);
                foreach (var file in files) File.WriteAllBytes(Path.Combine(path, file.Key), file.Value);
                File.WriteAllBytes(Path.Combine(path, "resource-catalog.json"), catalogBytes);
                File.WriteAllBytes(Path.Combine(path, ".ylr-complete.json"), ResourceCatalogCodec.CanonicalBytes(new JObject {
                    ["Version"] = 1, ["ProjectGuid"] = new string('1', 32), ["ProjectId"] = document.ProjectId,
                    ["Platform"] = document.Platform, ["BuildId"] = document.BuildId, ["CatalogDigest"] = document.CatalogDigest }));
                return ResourceLocalSnapshot.Open(path, "catalog", document.CatalogDigest, document.Platform, "app-v1");
            }

            public void Dispose()
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
