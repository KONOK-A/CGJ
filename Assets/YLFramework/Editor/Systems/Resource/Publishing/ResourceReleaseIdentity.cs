using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    /// <summary>Editor窗口的纯值选定身份；不是发布索引或许可，使用时必须ReadVersion。</summary>
    [Serializable]
    internal sealed class ResourceReleaseIdentity
    {
        [SerializeField] private string projectGuid;
        [SerializeField] private string projectId;
        [SerializeField] private string platform;
        [SerializeField] private string unity;
        [SerializeField] private string app;
        [SerializeField] private string buildId;
        [SerializeField] private string digest;
        [SerializeField] private string directory;
        internal string ProjectGuid => projectGuid;
        internal string ProjectId => projectId;
        internal string Platform => platform;
        internal string App => app;
        internal string CatalogDigest => digest;
        internal string DirectoryPath => directory;

        internal ResourceReleaseIdentity(ResourceRelease release)
        {
            if (release?.Catalog == null || string.IsNullOrEmpty(release.ProjectGuid))
            {
                throw new ArgumentException("需要确切owned Release身份。");
            }

            projectGuid = release.ProjectGuid;
            projectId = release.Catalog.ProjectId;
            platform = release.Catalog.Platform;
            unity = release.Catalog.UnityCompatibility;
            app = release.Catalog.AppContentCompatibilityId;
            buildId = release.Catalog.BuildId;
            digest = release.Catalog.CatalogDigest;
            directory = release.DirectoryPath;
        }

        internal ResourceRelease Resolve()
        {
            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(projectGuid));
            if (!project || project.projectId != projectId)
            {
                throw new InvalidOperationException("选定版本的Project身份已删除/变化，不能改读另一Project或当前Index。");
            }

            if (!Enum.TryParse(platform, out BuildTarget target) || !Enum.IsDefined(typeof(BuildTarget), target))
            {
                throw new InvalidDataException("选定版本平台不可识别。");
            }

            var release = ResourcePublisher.ReadVersion(project, target, app, digest);
            RequireSame(release);
            return release;
        }

        internal ResourceRelease ResolveForArchive(ResourceRelease captured)
        {
            RequireSame(captured);
            ResourcePublisher.VerifyExportInput(captured);
            return captured;
        }

        private void RequireSame(ResourceRelease release)
        {
            if (release?.Catalog == null || release.ProjectGuid != projectGuid || release.Catalog.ProjectId != projectId || release.Catalog.Platform != platform ||
                release.Catalog.UnityCompatibility != unity || release.Catalog.BuildId != buildId || release.Catalog.CatalogDigest != digest || release.DirectoryPath != directory)
            {
                throw new InvalidDataException("复验后的完整身份/来源与选定副本不一致。");
            }
        }

        public override string ToString() => "ProjectId=" + projectId + " / GUID=" + projectGuid + "\nPlatform=" + platform + " / Unity=" + unity +
            "\nApp=" + app + "\nCatalogDigest=" + digest + "\nBuildId=" + buildId + "\n发布版本目录=" + directory;
    }
}
