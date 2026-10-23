using System;
using System.Collections.Generic;
using System.Linq;

namespace YLFramework.Services.Resource
{
    /// <summary>应用显式绑定的项目、兼容要求和可选内置快照。不会扫描发布目录推断版本。</summary>
    public sealed class ResourceProjectMount
    {
        public string ProjectId
        {
            get;
        }
        public string Platform
        {
            get;
        }
        public string AppContentCompatibilityId
        {
            get;
        }
        public ResourceLocalSnapshot BuiltIn
        {
            get;
        }
        public ResourceRemoteSource Remote
        {
            get;
        }
        internal IReadOnlyList<Type> AssetTypes
        {
            get;
        }

        public ResourceProjectMount(string projectId, string platform, string appContentCompatibilityId,
            ResourceLocalSnapshot builtIn = null, IEnumerable<Type> assetTypes = null, ResourceRemoteSource remote = null)
        {
            ResourceCatalogCodec.Id(projectId);
            ResourceCatalogCodec.Id(appContentCompatibilityId);
            if (!new[] { "StandaloneWindows64", "StandaloneOSX", "StandaloneLinux64" }.Contains(platform))
            {
                throw new ArgumentException("A supported local desktop platform is required.", nameof(platform));
            }

            ProjectId = projectId;
            Platform = platform;
            AppContentCompatibilityId = appContentCompatibilityId;
            BuiltIn = builtIn;
            Remote = remote;
            AssetTypes = ResourceCatalog.Copy(assetTypes);
            if (builtIn != null && (builtIn.Document.ProjectId != projectId || builtIn.Document.Platform != platform ||
                builtIn.Document.AppContentCompatibilityId != appContentCompatibilityId))
            {
                throw new ArgumentException("The explicit built-in snapshot does not match its mount.", nameof(builtIn));
            }
        }
    }

    public sealed class UpdateFile
    {
        public string RelativeFile
        {
            get;
        }
        public long Length
        {
            get;
        }
        public bool IsReusable
        {
            get;
        }
        internal string Input;
        internal UpdateFile(string file, long length, bool reusable, string input)
        {
            RelativeFile = file;
            Length = length;
            IsReusable = reusable;
            Input = input;
        }
    }

    /// <summary>一次显式闭包选择；未变化的项目也须给出确切摘要，不推断跨 Catalog 依赖。</summary>
    public sealed class UpdateProject
    {
        public string ProjectId
        {
            get;
        }
        public string Platform
        {
            get;
        }
        public string FromDigest
        {
            get;
        }
        public string ToDigest
        {
            get;
        }
        public string SourceDirectory
        {
            get;
        }
        public IReadOnlyList<UpdateFile> Files
        {
            get;
        }
        public bool IsChanged => FromDigest != ToDigest;
        internal ResourceLocalSnapshot From;
        internal ResourceLocalSnapshot Target;
        internal ResourceRemoteRelease Remote;
        internal UpdateProject(ResourceLocalSnapshot from, ResourceLocalSnapshot target, IEnumerable<UpdateFile> files)
        {
            From = from;
            Target = target;
            ProjectId = target.Document.ProjectId;
            Platform = target.Document.Platform;
            FromDigest = from.Document.CatalogDigest;
            ToDigest = target.Document.CatalogDigest;
            SourceDirectory = target.DirectoryPath;
            Files = ResourceCatalog.Copy(files);
        }
        internal UpdateProject(ResourceLocalSnapshot from, ResourceRemoteRelease remote, IEnumerable<UpdateFile> files)
        {
            From = from;
            Remote = remote;
            ProjectId = remote.ProjectId;
            Platform = remote.Platform;
            FromDigest = from.Document.CatalogDigest;
            ToDigest = remote.CatalogDigest;
            Files = ResourceCatalog.Copy(files);
        }
    }

    /// <summary>只读检查结果。Reusable 文件仍复制到独立版本，不以硬链接或 mtime 代替完整性。</summary>
    public sealed class UpdatePlan
    {
        public IReadOnlyList<UpdateProject> Projects
        {
            get;
        }
        /// <summary>待安装文件和事务控制记录的保守增量空间上界，不是磁盘空间预留。</summary>
        public long RequiredBytes
        {
            get;
        }
        public long MissingBytes
        {
            get;
        }
        public long ReusableBytes
        {
            get;
        }
        public long AvailableBytes
        {
            get;
        }
        public bool HasEnoughSpace => AvailableBytes >= RequiredBytes;
        internal string Root;
        internal bool IsRemote => Projects.Any(x => x.Remote != null);
        internal UpdatePlan(string root, IEnumerable<UpdateProject> projects, long available, long required)
        {
            Root = root;
            Projects = ResourceCatalog.Copy(projects);
            AvailableBytes = available;
            RequiredBytes = required;
            MissingBytes = Projects.SelectMany(x => x.Files).Where(x => !x.IsReusable).Sum(x => x.Length);
            ReusableBytes = Projects.SelectMany(x => x.Files).Where(x => x.IsReusable).Sum(x => x.Length);
        }
    }

    /// <summary>由完整 owned 版本重建的准备结果；不是激活权限，仍须检查 from/to 和真实资源屏障。</summary>
    public sealed class PreparedUpdate
    {
        public IReadOnlyList<UpdateProject> Projects
        {
            get;
        }
        internal string Root;
        internal bool IsRollback
        {
            get;
        }
        internal IReadOnlyList<ResourceRemoteRelease> Releases
        {
            get;
        }
        internal PreparedUpdate(string root, IEnumerable<UpdateProject> projects, bool isRollback = false, IEnumerable<ResourceRemoteRelease> releases = null)
        {
            Root = root;
            Projects = ResourceCatalog.Copy(projects);
            IsRollback = isRollback;
            Releases = ResourceCatalog.Copy(releases);
        }
    }

    public sealed class VersionCleanupResult
    {
        public IReadOnlyList<string> DeletedPaths
        {
            get;
        }
        public IReadOnlyList<string> ProtectedVersions
        {
            get;
        }
        public IReadOnlyList<string> Errors
        {
            get;
        }
        public bool HasSucceeded => Errors.Count == 0;
        internal VersionCleanupResult(IEnumerable<string> deleted, IEnumerable<string> protectedVersions, IEnumerable<string> errors)
        {
            DeletedPaths = ResourceCatalog.Copy(deleted);
            ProtectedVersions = ResourceCatalog.Copy(protectedVersions);
            Errors = ResourceCatalog.Copy(errors);
        }
    }
}
