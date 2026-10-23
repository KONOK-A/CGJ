using System;
using System.Collections.Generic;
using System.Linq;

namespace YLFramework.Services.Resource
{
    public enum ArtifactQueryStatus
    {
        Ready,
        NotConfigured,
        Missing,
        Busy,
        RecoveryRequired,
        Invalid,
        Unreadable,
        Incomplete,
        Changed,
        Canceled
    }
    public enum ArtifactKind
    {
        Version,
        Staging,
        Partial,
        Metadata,
        Unknown
    }
    public enum ArtifactState
    {
        CompleteRecord,
        Staging,
        Partial,
        Incomplete,
        Unknown,
        Invalid,
        Unreadable,
        Linked
    }

    [Flags]
    public enum ArtifactProtection
    {
        None = 0,
        Active = 1,
        LastKnownGood = 2,
        PreparedFrom = 4,
        PreparedTo = 8,
        Mounted = 16,
        Held = 32,
        InFlight = 64
    }

    public sealed class ArtifactProjectInfo
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
        internal ArtifactProjectInfo(ResourceProjectMount mount)
        {
            ProjectId = mount.ProjectId;
            Platform = mount.Platform;
            AppContentCompatibilityId = mount.AppContentCompatibilityId;
        }
    }

    public sealed class ArtifactVersionProtection
    {
        public string DirectoryPath
        {
            get;
        }
        public ArtifactProtection Reasons
        {
            get;
        }
        internal ArtifactVersionProtection(string path, ArtifactProtection reasons)
        {
            DirectoryPath = path;
            Reasons = reasons;
        }
    }

    public sealed class ArtifactTransition
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
        internal ArtifactTransition(string project, string platform, string from, string to)
        {
            ProjectId = project;
            Platform = platform;
            FromDigest = from;
            ToDigest = to;
        }
    }

    /// <summary>只读事务身份，不是可激活的 PreparedUpdate，也不持有快照或 Unity 对象。</summary>
    public sealed class ArtifactPreparedInfo
    {
        public string RecordPath
        {
            get;
        }
        public IReadOnlyList<ArtifactTransition> Projects
        {
            get;
        }
        internal ArtifactPreparedInfo(string path, IEnumerable<ArtifactTransition> projects)
        {
            RecordPath = path;
            Projects = Array.AsReadOnly(projects.ToArray());
        }
    }

    /// <summary>文件逻辑字节观测。CompleteRecord 仅指 owned Complete 记录及登记文件集合，不证明内容 SHA、可挂载性或签名新鲜度。</summary>
    public sealed class ArtifactEntry
    {
        public string Path
        {
            get;
        }
        public string ProjectId
        {
            get;
        }
        public string Platform
        {
            get;
        }
        public string CatalogDigest
        {
            get;
        }
        public ArtifactKind Kind
        {
            get;
        }
        public ArtifactState State
        {
            get;
        }
        public ArtifactProtection Protection
        {
            get;
        }
        public long ObservedBytes
        {
            get;
        }
        public bool IsSizeComplete
        {
            get;
        }
        public bool IsCleanupCandidate
        {
            get;
        }
        public IReadOnlyList<string> Issues
        {
            get;
        }
        internal ArtifactEntry(string path, ResourceProjectMount project, string digest, ArtifactKind kind, ArtifactState state,
            ArtifactProtection protection, long bytes, bool sizeComplete, bool candidate, IEnumerable<string> issues)
        {
            Path = path;
            ProjectId = project?.ProjectId;
            Platform = project?.Platform;
            CatalogDigest = digest;
            Kind = kind;
            State = state;
            Protection = protection;
            ObservedBytes = bytes;
            IsSizeComplete = sizeComplete;
            IsCleanupCandidate = candidate;
            Issues = Array.AsReadOnly(issues.ToArray());
        }
    }

    /// <summary>一次有界观测，不是磁盘快照或删除授权。实际清理必须重新调用 CleanupVersionsAsync。</summary>
    public sealed class ArtifactStoreReport
    {
        public string RootPath
        {
            get;
        }
        public DateTimeOffset StartedUtc
        {
            get;
        }
        public DateTimeOffset CompletedUtc
        {
            get;
        }
        public ArtifactQueryStatus Status
        {
            get;
        }
        public IReadOnlyList<ArtifactProjectInfo> Projects
        {
            get;
        }
        public IReadOnlyList<ArtifactEntry> Entries
        {
            get;
        }
        public IReadOnlyList<ArtifactPreparedInfo> PreparedUpdates
        {
            get;
        }
        public IReadOnlyList<ArtifactVersionProtection> ProtectedVersions
        {
            get;
        }
        public IReadOnlyList<string> InFlightOperations
        {
            get;
        }
        public IReadOnlyList<string> Issues
        {
            get;
        }
        /// <summary>已观测的普通文件逻辑长度之和，条目互不重叠；不含未知目录内部或链接目标。</summary>
        public long ObservedBytes
        {
            get;
        }
        public bool IsSizeComplete
        {
            get;
        }
        /// <summary>仅 Ready 时提供已确认候选的逻辑字节；不是保证可释放的磁盘空间，更不是 native memory。</summary>
        public long? CleanupCandidateBytes
        {
            get;
        }
        internal ArtifactStoreReport(ArtifactStore store, DateTimeOffset started, ArtifactQueryStatus status,
            IEnumerable<ArtifactEntry> entries, IEnumerable<ArtifactPreparedInfo> prepared,
            IEnumerable<ArtifactVersionProtection> protections, IEnumerable<string> operations, IEnumerable<string> issues)
        {
            RootPath = store?.RootPath;
            StartedUtc = started;
            CompletedUtc = DateTimeOffset.UtcNow;
            Status = status;
            Projects = Array.AsReadOnly(store == null ? Array.Empty<ArtifactProjectInfo>() : store.Projects.Select(x => new ArtifactProjectInfo(x)).ToArray());
            Entries = Array.AsReadOnly(entries.ToArray());
            PreparedUpdates = Array.AsReadOnly(prepared.ToArray());
            ProtectedVersions = Array.AsReadOnly(protections.ToArray());
            InFlightOperations = Array.AsReadOnly(operations.ToArray());
            Issues = Array.AsReadOnly(issues.ToArray());
            ObservedBytes = Entries.Sum(x => x.ObservedBytes);
            IsSizeComplete = status == ArtifactQueryStatus.Ready && Entries.All(x => x.IsSizeComplete);
            CleanupCandidateBytes = status == ArtifactQueryStatus.Ready ? Entries.Where(x => x.IsCleanupCandidate).Sum(x => x.ObservedBytes) : null;
        }
    }
}
