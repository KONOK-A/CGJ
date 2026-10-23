using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace YLFramework.Services.Resource
{
    /// <summary>只定位一个完整 Catalog 快照，不复制资产表，不表示运行时激活或生产签名授权。</summary>
    public sealed class ResourceReleaseIndex
    {
        public int SchemaVersion => 1;
        public string ProjectId
        {
            get;
        }
        public string Platform
        {
            get;
        }
        public string UnityCompatibility
        {
            get;
        }
        public string AppContentCompatibilityId
        {
            get;
        }
        public string BuildId
        {
            get;
        }
        public string CatalogDigest
        {
            get;
        }
        public string CatalogFile => CatalogDigest + "/resource-catalog.json";

        private ResourceReleaseIndex(string project, string platform, string unity, string app, string build, string digest)
        {
            ProjectId = project;
            Platform = platform;
            UnityCompatibility = unity;
            AppContentCompatibilityId = app;
            BuildId = build;
            CatalogDigest = digest;
        }

        public static ResourceReleaseIndex Create(ResourceCatalogDocument catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return new ResourceReleaseIndex(catalog.ProjectId, catalog.Platform, catalog.UnityCompatibility,
                catalog.AppContentCompatibilityId, catalog.BuildId, catalog.CatalogDigest);
        }

        public byte[] Write() => ResourceCatalogCodec.CanonicalBytes(new JObject
        {
            ["SchemaVersion"] = 1,
            ["ProjectId"] = ProjectId,
            ["Platform"] = Platform,
            ["UnityCompatibility"] = UnityCompatibility,
            ["AppContentCompatibilityId"] = AppContentCompatibilityId,
            ["BuildId"] = BuildId,
            ["CatalogDigest"] = CatalogDigest,
            ["CatalogFile"] = CatalogFile
        });

        public static ResourceReleaseIndex Read(byte[] bytes, string projectId, string platform, string unityCompatibility, string appContentCompatibilityId)
        {
            if (bytes == null || bytes.Length > 8192)
            {
                throw new InvalidDataException("Release Index byte limit exceeded.");
            }

            var data = ResourceCatalogCodec.ParseObject(bytes);
            ResourceCatalogCodec.Fields(data, "SchemaVersion,ProjectId,Platform,UnityCompatibility,AppContentCompatibilityId,BuildId,CatalogDigest,CatalogFile");
            if (ResourceCatalogCodec.Integer(data, "SchemaVersion") != 1)
            {
                throw new InvalidDataException("Unsupported Release Index schema.");
            }

            var project = ResourceCatalogCodec.Text(data, "ProjectId");
            ResourceCatalogCodec.Id(project);
            var actualPlatform = ResourceCatalogCodec.Text(data, "Platform");
            var unity = ResourceCatalogCodec.Text(data, "UnityCompatibility");
            var app = ResourceCatalogCodec.Text(data, "AppContentCompatibilityId");
            ResourceCatalogCodec.Id(app);
            var build = ResourceCatalogCodec.Text(data, "BuildId");
            ResourceCatalogCodec.Hex(build, 64);
            var digest = ResourceCatalogCodec.Text(data, "CatalogDigest");
            ResourceCatalogCodec.Hex(digest, 64);
            if (project != projectId || actualPlatform != platform || unity != unityCompatibility || app != appContentCompatibilityId)
            {
                throw new InvalidDataException("Release Index project/platform/Unity/app compatibility mismatch.");
            }

            var index = new ResourceReleaseIndex(project, actualPlatform, unity, app, build, digest);
            if (ResourceCatalogCodec.Text(data, "CatalogFile") != index.CatalogFile || !bytes.SequenceEqual(index.Write()))
            {
                throw new InvalidDataException("Invalid or noncanonical Catalog locator in Release Index.");
            }

            return index;
        }
    }
}
