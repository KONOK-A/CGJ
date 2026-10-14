using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    // 复用旧 YL 的归属预检/链接拒绝契约；仅管理未发布的增量缓存，不执行发布、回滚或目录清理。
    internal sealed class ResourceBuildWorkspace
    {
        internal const string Marker = ".ylr-workspace.json";
        private const string Next = ".ylr-workspace.next";
        private const string Lock = ".ylr-workspace.lock";
        private const int MaximumFiles = 65540;

        [Serializable]
        internal sealed class FileDigest
        {
            public string Name;
            public long Length;
            public string Sha256;
        }

        [Serializable]
        internal sealed class Ownership
        {
            public int Version = 1;
            public string ProjectRoot;
            public string ProjectGuid;
            public string ProjectId;
            public string Settings;
            public string Leaf;
            public string[] KnownFiles = Array.Empty<string>();
            public bool Complete;
            public string Fingerprint;
            public FileDigest[] Files = Array.Empty<FileDigest>();
        }

        private readonly ResourceBuildMap map;
        private readonly string settings;
        internal string Root { get; }
        internal string Output { get; }
        internal string Leaf { get; }

        internal ResourceBuildWorkspace(ResourceBuildMap map, string settings, string platform)
        {
            this.map = map;
            this.settings = settings;
            Leaf = "ylr_" + map.ProjectToken + "_" + platform;
            // Unity 禁止向 Library 构建。只管理 AssetBundles 下新 GUID 的独立根，不认领/清理共享父目录或历史产物。
            // 路径使用 128-bit token 缩短 Windows 路径；marker 仍比较完整配置 SHA-256，碰撞只能拒绝，不能复用。
            Root = ResourceBuildMap.Absolute("AssetBundles/YLR_" + map.ProjectGuid + "_" + settings.Substring(0, 32));
            Output = Path.Combine(Root, Leaf);
        }

        internal Ownership Read()
        {
            ResourceBuildOperation.RejectLinks(Root);
            if (File.Exists(Root) || File.Exists(Root + ".meta")) throw new InvalidOperationException("Workspace root is occupied by a file or orphan meta: " + Root);
            if (!Directory.Exists(Root)) return null;
            var marker = Path.Combine(Root, Marker);
            if (!File.Exists(marker) || new FileInfo(marker).Length > 16 * 1024 * 1024)
                throw new InvalidOperationException("Unknown or invalid workspace ownership; refusing adoption: " + Root);
            Ownership record;
            try { record = JsonUtility.FromJson<Ownership>(File.ReadAllText(marker)); }
            catch (ArgumentException error) { throw new InvalidOperationException("Invalid workspace marker: " + marker, error); }
            if (record == null || record.Version != 1 || record.ProjectRoot != ResourceBuildMap.Absolute(".") ||
                record.ProjectGuid != map.ProjectGuid || record.ProjectId != map.ProjectId || record.Settings != settings ||
                record.Leaf != Leaf || record.KnownFiles == null || record.Files == null || record.KnownFiles.Length > MaximumFiles ||
                record.Files.Length > MaximumFiles)
                throw new InvalidOperationException("Workspace owner/settings conflict: " + Root);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in record.KnownFiles)
                if (!ValidFile(file) || !known.Add(file)) throw new InvalidOperationException("Invalid workspace file registration: " + file);
            var digests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in record.Files)
                if (file == null || !known.Contains(file.Name) || !digests.Add(file.Name) || file.Length < 0 ||
                    file.Sha256 == null || file.Sha256.Length != 64 || file.Sha256.Any(x => !Uri.IsHexDigit(x)))
                    throw new InvalidOperationException("Invalid workspace digest record.");
            ValidateContents(record);
            return record;
        }

        private bool ValidFile(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0) return false;
            var native = name.EndsWith(".manifest", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9) : name;
            return native.StartsWith("ylr_" + map.ProjectToken + "_", StringComparison.Ordinal) &&
                native.All(x => x >= 'a' && x <= 'z' || x >= 'A' && x <= 'Z' || x >= '0' && x <= '9' || x == '_');
        }

        internal void ValidateContents(Ownership record)
        {
            ResourceBuildOperation.RejectLinks(Root);
            var known = new HashSet<string>(record.KnownFiles, StringComparer.Ordinal);
            foreach (var entry in Directory.GetFileSystemEntries(Root))
            {
                var name = Path.GetFileName(entry);
                if (name == Leaf && Directory.Exists(entry)) continue;
                if ((name == Marker || name == Next || name == Lock) && File.Exists(entry)) continue;
                throw new InvalidOperationException("Workspace contains unknown content: " + entry);
            }
            if (!Directory.Exists(Output)) return;
            foreach (var entry in Directory.GetFileSystemEntries(Output))
                if (!File.Exists(entry) || !known.Contains(Path.GetFileName(entry)))
                    throw new InvalidOperationException("Workspace contains an unregistered file/directory: " + entry);
        }

        internal FileStream AcquireLock(ref Ownership record)
        {
            var creating = record == null;
            if (creating) Directory.CreateDirectory(Root);
            var handle = File.Open(Path.Combine(Root, Lock), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (creating && !File.Exists(Path.Combine(Root, Marker)))
                {
                    if (Directory.GetFileSystemEntries(Root).Any(x => Path.GetFileName(x) != Lock))
                        throw new InvalidOperationException("Concurrent unknown workspace content; refusing adoption.");
                    record = new Ownership
                    {
                        ProjectRoot = ResourceBuildMap.Absolute("."), ProjectGuid = map.ProjectGuid,
                        ProjectId = map.ProjectId, Settings = settings, Leaf = Leaf
                    };
                    Write(record);
                }
                else record = Read();
                return handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        internal string[] ExpectedFiles() => map.Bundles.Select(x => x.Name).Append(Leaf)
            .SelectMany(x => new[] { x, x + ".manifest" }).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        internal void BeginAttempt(Ownership record)
        {
            var expected = ExpectedFiles();
            record.KnownFiles = record.KnownFiles.Concat(expected).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (record.KnownFiles.Length > MaximumFiles) throw new InvalidOperationException("Workspace registration limit exceeded; use a new explicit configuration.");
            record.Complete = false;
            record.Fingerprint = null;
            record.Files = Array.Empty<FileDigest>();
            // 在调用 native 构建前登记所有可能产物；失败/取消仅使缓存无效，不删除或猜测部分产物。
            Write(record);
            Directory.CreateDirectory(Output);
        }

        internal void Write(Ownership record)
        {
            ValidateContents(record);
            var next = Path.Combine(Root, Next);
            File.WriteAllText(next, JsonUtility.ToJson(record, true), new UTF8Encoding(false));
            var marker = Path.Combine(Root, Marker);
            if (File.Exists(marker)) File.Replace(next, marker, null);
            else File.Move(next, marker);
        }
    }
}
