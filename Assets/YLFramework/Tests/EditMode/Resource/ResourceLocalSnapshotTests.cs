using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public class ResourceLocalSnapshotTests
    {
        internal static object Open(ResourceRelease release)
        {
            var type = typeof(ResourceSystem).Assembly.GetType("YLFramework.Services.Resource.ResourceLocalSnapshot");
            Assert.That(type, Is.Not.Null, "Runtime must mount an explicit verified Catalog v1 publication, without XFAB.");
            try
            {
                return type.GetMethod("Open").Invoke(null, new object[] { release.DirectoryPath, release.Catalog.ProjectId,
                    release.Catalog.CatalogDigest, release.Catalog.Platform, "fixture-v1" });
            }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        [Test]
        public void ExplicitPublicationMountValidatesEveryFileAndCompiledTypesWithoutNativeLoads()
        {
            using var fixture = new ResourceReleaseFixture();
            var build = fixture.Build();
            var release = ResourcePublisher.Publish(build, "fixture-v1");
            fixture.ReleaseRoot = release.RootPath;
            var root = Path.GetPathRoot(release.DirectoryPath);
            Assert.Throws<ArgumentException>(() => ResourceLocalSnapshot.Open(root.Substring(0, 2) + release.DirectoryPath.Substring(root.Length),
                release.Catalog.ProjectId, release.Catalog.CatalogDigest, release.Catalog.Platform, "fixture-v1"));
            var nativeBefore = AssetBundle.GetAllLoadedAssetBundles().Select(x => x.GetInstanceID()).ToArray();
            var snapshot = Open(release);
            var catalog = (ResourceCatalog)snapshot.GetType().GetMethod("CreateCatalog").Invoke(snapshot, new object[] { null });
            Assert.That(catalog.Resolve(new ResourceAddress(release.Catalog.ProjectId, "entry.txt")).Location.AssetType, Is.EqualTo(typeof(TextAsset)));
            foreach (var sprite in release.Catalog.Assets.Where(x => x.Kind == "SubAsset"))
                Assert.That(catalog.Resolve(new ResourceAddress(release.Catalog.ProjectId, sprite.Address, sprite.SubAssetKey)).AssetId, Is.EqualTo(sprite.AssetId));
            CollectionAssert.AreEquivalent(nativeBefore, AssetBundle.GetAllLoadedAssetBundles().Select(x => x.GetInstanceID()));
            var file = Path.Combine(release.DirectoryPath, release.Catalog.Bundles[0].RelativeFile);
            var bytes = File.ReadAllBytes(file);
            try
            {
                File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
                Assert.Throws<InvalidDataException>(() => Open(release));
                File.Delete(file);
                Assert.That(Assert.Throws<InvalidDataException>(() => Open(release)).Message, Is.EqualTo("Publication contains missing or unregistered content."));
            }
            finally { File.WriteAllBytes(file, bytes); }
            fixture.AssertProtected();
        }
    }
}
