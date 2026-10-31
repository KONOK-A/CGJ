using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using YLFramework.Integrations.HybridCLR;
using YLFramework.Integrations.HybridCLR.Editor;
using YLFramework.Utilities.ZipTool.GZip;
using YLFramework.Utilities.ZipTool.Tar;

namespace YLFramework.Tests.Integrations.HybridCLR
{
    public class HybridCLRIntegrationExportTests
    {
        private const string Root = "Assets/YLFramework/Integrations/HybridCLR";

        [Test]
        public void OptionalSelectionIsExplicitAndDoesNotIncludeSampleOrGeneratedInputsByDefault()
        {
            var files = HybridCLRIntegrationExporter.GetExportPaths();
            Assert.That(files, Does.Contain(Root + "/Runtime/HybridCLRLoader.cs"));
            Assert.That(files, Does.Contain(Root + "/Editor/HybridCLRBuildPipeline.cs"));
            Assert.That(files, Does.Contain(Root + "/Dependencies.json"));
            Assert.That(files, Does.Contain(Root + "/README.md"));
            Assert.That(files.Any(p => p.StartsWith(Root + "/Samples/", StringComparison.Ordinal)), Is.False);
            Assert.That(files.Any(p => p.Contains("Tests/") || p.StartsWith("Packages/", StringComparison.Ordinal) || p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(HybridCLRIntegrationExporter.GetExportPaths(true).Any(p => p.StartsWith(Root + "/Samples/", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void ActualOptionalArchiveContainsOnlyTheSelectedSourcesAndPreservesGuid()
        {
            var parent = Path.Combine(Path.GetTempPath(), "kilo", "YLHybridCLRExport-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(parent);
            try
            {
                var path = parent + "/integration.unitypackage";
                HybridCLRIntegrationExporter.Export(path, true);
                var names = new HashSet<string>(StringComparer.Ordinal);
                using (var stream = File.OpenRead(path))
                using (var gzip = new GZipInputStream(stream))
                using (var tar = new TarInputStream(gzip))
                {
                    TarEntry entry;
                    while ((entry = tar.GetNextEntry()) != null)
                    {
                        if (!entry.Name.EndsWith("/pathname", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        using var output = new MemoryStream();
                        tar.CopyEntryContents(output);
                        var asset = Encoding.UTF8.GetString(output.ToArray()).TrimEnd('\0', '\r', '\n');
                        Assert.That(asset == "Assets/YLFramework/LICENSE.md" || asset == Root || asset.StartsWith(Root + "/", StringComparison.Ordinal) ||
                            AssetDatabase.IsValidFolder(asset) && Root.StartsWith(asset + "/", StringComparison.Ordinal), Is.True, asset);
                        Assert.That(asset.Contains("Tests/") || asset.StartsWith("Packages/", StringComparison.Ordinal) || asset.EndsWith(".dll", StringComparison.OrdinalIgnoreCase), Is.False, asset);
                        Assert.That(AssetDatabase.AssetPathToGUID(asset), Is.EqualTo(entry.Name.Split('/')[0]), asset);
                        names.Add(asset);
                    }
                }
                foreach (var required in HybridCLRIntegrationExporter.GetExportPaths(true))
                {
                    Assert.That(names, Does.Contain(required));
                }

                Assert.Throws<IOException>(() => HybridCLRIntegrationExporter.Export(path));
            }
            finally
            {
                Directory.Delete(parent, true);
            }
        }

        [TestCase("//127.0.0.1/share/integration.unitypackage")]
        [TestCase("\\\\127.0.0.1\\share\\integration.unitypackage")]
        public void NetworkExportDestinationsAreRejectedBeforeAccess(string path) =>
            Assert.Throws<ArgumentException>(() => HybridCLRIntegrationExporter.Export(path));
    }
}
