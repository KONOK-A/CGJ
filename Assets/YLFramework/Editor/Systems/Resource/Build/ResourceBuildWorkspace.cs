using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using YLFramework.Services.Resource;

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
        internal class Ownership
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

        [Serializable]
        internal sealed class DeliveryRecord
        {
            public string RelativePath;
            public string Name;
            public long RawLength;
            public string RawSha256;
            public long Length;
            public string Sha256;
            public bool Complete;
            internal FileDigest Digest() => new() { Name = Name, Length = Length, Sha256 = Sha256 };
        }

        // 独立派生类型使 None 继续写原 v1 字段，而不是给旧格式追加空 delivery 字段。
        [Serializable]
        internal sealed class EncryptedOwnership : Ownership
        {
            public string KeyId;
            public string Platform;
            public string UnityCompatibility;
            public int ContentProtocolVersion = 1;
            public string[] KnownDeliveryFiles = Array.Empty<string>();
            public DeliveryRecord[] Deliveries = Array.Empty<DeliveryRecord>();
            public string DeliveryDirectory;
            public FileDigest[] DeliveryFiles = Array.Empty<FileDigest>();
        }

        private readonly ResourceBuildMap _map;
        private readonly string _settings;
        private readonly string _platform;
        private readonly string _keyId;
        private readonly bool _isEncrypted;
        internal string Root
        {
            get;
        }
        internal string Output
        {
            get;
        }
        internal string Leaf
        {
            get;
        }

        internal ResourceBuildWorkspace(ResourceBuildMap map, string settings, string platform,
            ResourceContentEncryption encryption = ResourceContentEncryption.None, string keyId = null)
        {
            this._map = map;
            this._settings = settings;
            this._platform = platform;
            this._keyId = keyId;
            _isEncrypted = encryption == ResourceContentEncryption.Aes256GcmHkdfSegmentedV1;
            if (!_isEncrypted && encryption != ResourceContentEncryption.None)
            {
                throw new ArgumentOutOfRangeException(nameof(encryption));
            }

            Leaf = "ylr_" + map.ProjectToken + "_" + platform;
            // Unity 禁止向 Library 构建。只管理 AssetBundles 下新 GUID 的独立根，不认领/清理共享父目录或历史产物。
            // 路径使用 128-bit token 缩短 Windows 路径；marker 仍比较完整配置 SHA-256，碰撞只能拒绝，不能复用。
            Root = ResourceBuildMap.Absolute("AssetBundles/YLR_" + map.ProjectGuid + "_" + settings.Substring(0, 32));
            Output = Path.Combine(Root, Leaf);
            // Native管线可写出Mono随后无法读回的长路径，必须在认领Workspace或调用构建前拒绝。
            if (Path.DirectorySeparatorChar == '\\' && ExpectedFiles().Any(name => Path.Combine(Output, name).Length >= 260))
            {
                throw new PathTooLongException("Workspace paths exceed this Unity Mono Windows file API limit; use a shorter Unity project path before building.");
            }
        }

        internal Ownership Read()
        {
            ResourceBuildOperation.RejectLinks(Root);
            if (File.Exists(Root) || File.Exists(Root + ".meta"))
            {
                throw new InvalidOperationException("Workspace root is occupied by a file or orphan meta: " + Root);
            }

            if (!Directory.Exists(Root))
            {
                return null;
            }

            var marker = Path.Combine(Root, Marker);
            if (!File.Exists(marker) || new FileInfo(marker).Length > 16 * 1024 * 1024)
            {
                throw new InvalidOperationException("Unknown or invalid workspace ownership; refusing adoption: " + Root);
            }

            Ownership record;
            try
            {
                record = _isEncrypted ? JsonUtility.FromJson<EncryptedOwnership>(File.ReadAllText(marker)) : JsonUtility.FromJson<Ownership>(File.ReadAllText(marker));
            }
            catch (ArgumentException error)
            {
                throw new InvalidOperationException("Invalid workspace marker: " + marker, error);
            }
            if (record == null || record.Version != (_isEncrypted ? 2 : 1) || record.ProjectRoot != ResourceBuildMap.Absolute(".") ||
                record.ProjectGuid != _map.ProjectGuid || record.ProjectId != _map.ProjectId || record.Settings != _settings ||
                record.Leaf != Leaf || record.KnownFiles == null || record.Files == null || record.KnownFiles.Length > MaximumFiles ||
                record.Files.Length > MaximumFiles)
            {
                throw new InvalidOperationException("Workspace owner/settings conflict: " + Root);
            }

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in record.KnownFiles)
            {
                if (!ValidFile(file) || !known.Add(file))
                {
                    throw new InvalidOperationException("Invalid workspace file registration: " + file);
                }
            }

            var digests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in record.Files)
            {
                if (file == null || !known.Contains(file.Name) || !digests.Add(file.Name) || file.Length < 0 ||
                    file.Sha256 == null || file.Sha256.Length != 64 || file.Sha256.Any(x => !Uri.IsHexDigit(x)))
                {
                    throw new InvalidOperationException("Invalid workspace digest record.");
                }
            }

            if (record is EncryptedOwnership delivery)
            {
                if (delivery.KeyId != _keyId || delivery.Platform != _platform || delivery.UnityCompatibility != Application.unityVersion ||
                    delivery.ContentProtocolVersion != 1 || delivery.KnownDeliveryFiles == null || delivery.Deliveries == null || delivery.DeliveryFiles == null ||
                    delivery.KnownDeliveryFiles.Length > MaximumFiles || delivery.Deliveries.Length > MaximumFiles || delivery.DeliveryFiles.Length > MaximumFiles)
                {
                    throw new InvalidOperationException("Encrypted workspace policy/registration conflict.");
                }

                var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in delivery.KnownDeliveryFiles)
                {
                    if (!ValidDeliveryPath(path) || !registered.Add(path))
                    {
                        throw new InvalidOperationException("Invalid delivery file registration.");
                    }
                }

                var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in delivery.Deliveries)
                {
                    if (file == null || !registered.Contains(file.RelativePath) || !candidates.Add(file.RelativePath) ||
                        file.Name != file.RelativePath.Split('/')[1] || file.RawLength < 0 || !ValidDigest(file.RawSha256) ||
                        file.Complete && (file.Length < 0 || !ValidDigest(file.Sha256)))
                    {
                        throw new InvalidOperationException("Invalid delivery candidate identity.");
                    }
                }

                if (!string.IsNullOrEmpty(delivery.DeliveryDirectory) && !ValidDeliveryDirectory(delivery.DeliveryDirectory))
                {
                    throw new InvalidOperationException("Invalid current delivery directory.");
                }

                var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in delivery.DeliveryFiles)
                {
                    if (file == null || !ValidFile(file.Name) || !current.Add(file.Name) || file.Length < 0 || !ValidDigest(file.Sha256) ||
                        !registered.Contains(delivery.DeliveryDirectory + "/" + file.Name))
                    {
                        throw new InvalidOperationException("Invalid current delivery snapshot.");
                    }
                }
            }
            ValidateContents(record);
            return record;
        }

        private static bool ValidDigest(string value) => value != null && value.Length == 64 && value.All(x => x >= '0' && x <= '9' || x >= 'a' && x <= 'f');
        private static bool ValidDeliveryDirectory(string value) => value != null && value.Length == 34 && value.StartsWith("d_", StringComparison.Ordinal) &&
            value.Substring(2).All(x => x >= '0' && x <= '9' || x >= 'a' && x <= 'f');
        private bool ValidDeliveryPath(string value)
        {
            var parts = value?.Split('/');
            return parts?.Length == 2 && ValidDeliveryDirectory(parts[0]) && ValidFile(parts[1]);
        }

        private bool ValidFile(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                return false;
            }

            var native = name.EndsWith(".manifest", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9) : name;
            return native.StartsWith("ylr_" + _map.ProjectToken + "_", StringComparison.Ordinal) &&
                native.All(x => x >= 'a' && x <= 'z' || x >= 'A' && x <= 'Z' || x >= '0' && x <= '9' || x == '_');
        }

        internal void ValidateContents(Ownership record)
        {
            ResourceBuildOperation.RejectLinks(Root);
            var known = new HashSet<string>(record.KnownFiles, StringComparer.Ordinal);
            var delivery = record as EncryptedOwnership;
            var deliveryFiles = new HashSet<string>(delivery?.KnownDeliveryFiles ?? Array.Empty<string>(), StringComparer.Ordinal);
            var deliveryDirectories = new HashSet<string>(deliveryFiles.Select(x => x.Split('/')[0]), StringComparer.Ordinal);
            foreach (var entry in Directory.GetFileSystemEntries(Root))
            {
                var name = Path.GetFileName(entry);
                if (name == Leaf && Directory.Exists(entry))
                {
                    continue;
                }

                if (deliveryDirectories.Contains(name) && Directory.Exists(entry))
                {
                    foreach (var file in Directory.GetFileSystemEntries(entry))
                    {
                        if (!File.Exists(file) || !deliveryFiles.Contains(name + "/" + Path.GetFileName(file)))
                        {
                            throw new InvalidOperationException("Workspace contains unregistered delivery content: " + file);
                        }
                    }

                    continue;
                }
                if ((name == Marker || name == Next || name == Lock) && File.Exists(entry))
                {
                    continue;
                }

                throw new InvalidOperationException("Workspace contains unknown content: " + entry);
            }
            if (!Directory.Exists(Output))
            {
                return;
            }

            foreach (var entry in Directory.GetFileSystemEntries(Output))
            {
                if (!File.Exists(entry) || !known.Contains(Path.GetFileName(entry)))
                {
                    throw new InvalidOperationException("Workspace contains an unregistered file/directory: " + entry);
                }
            }
        }

        internal FileStream AcquireLock(ref Ownership record)
        {
            var creating = record == null;
            if (creating)
            {
                Directory.CreateDirectory(Root);
            }

            var handle = File.Open(Path.Combine(Root, Lock), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                if (creating && !File.Exists(Path.Combine(Root, Marker)))
                {
                    if (Directory.GetFileSystemEntries(Root).Any(x => Path.GetFileName(x) != Lock))
                    {
                        throw new InvalidOperationException("Concurrent unknown workspace content; refusing adoption.");
                    }

                    record = _isEncrypted ? new EncryptedOwnership { Version = 2, KeyId = _keyId, Platform = _platform, UnityCompatibility = Application.unityVersion } : new Ownership();
                    record.ProjectRoot = ResourceBuildMap.Absolute(".");
                    record.ProjectGuid = _map.ProjectGuid;
                    record.ProjectId = _map.ProjectId;
                    record.Settings = _settings;
                    record.Leaf = Leaf;
                    Write(record);
                }
                else
                {
                    record = Read();
                }

                return handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        internal string[] ExpectedFiles() => _map.Bundles.Select(x => x.Name).Append(Leaf)
            .SelectMany(x => new[] { x, x + ".manifest" }).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        internal void BeginAttempt(Ownership record)
        {
            var expected = ExpectedFiles();
            record.KnownFiles = record.KnownFiles.Concat(expected).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (record.KnownFiles.Length > MaximumFiles)
            {
                throw new InvalidOperationException("Workspace registration limit exceeded; use a new explicit configuration.");
            }

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
            if (File.Exists(marker))
            {
                File.Replace(next, marker, null);
            }
            else
            {
                File.Move(next, marker);
            }
        }

        internal string RegisterDelivery(EncryptedOwnership record, FileDigest[] raw, out DeliveryRecord[] pending)
        {
            var directory = "d_" + Guid.NewGuid().ToString("N");
            var path = Path.Combine(Root, directory);
            if (Directory.Exists(path) || File.Exists(path))
            {
                throw new IOException("New delivery identity is already occupied.");
            }

            pending = raw.Select(file => new DeliveryRecord
            {
                RelativePath = directory + "/" + file.Name,
                Name = file.Name,
                RawLength = file.Length,
                RawSha256 = file.Sha256
            }).ToArray();
            if (record.KnownDeliveryFiles.Length + pending.Length > MaximumFiles)
            {
                throw new InvalidOperationException("Delivery registration limit exceeded; use a new explicit configuration.");
            }

            if (Path.DirectorySeparatorChar == '\\' && pending.Any(x => Path.Combine(Root, x.RelativePath).Length >= 260))
            {
                throw new InvalidOperationException("Delivery paths exceed this Unity Mono Windows file API limit.");
            }

            record.KnownDeliveryFiles = record.KnownDeliveryFiles.Concat(pending.Select(x => x.RelativePath)).ToArray();
            record.Deliveries = record.Deliveries.Concat(pending).ToArray();
            record.Complete = false;
            Write(record);
            Directory.CreateDirectory(path);
            return directory;
        }

        internal static FileStream OpenVerified(string path, long length, string digest, CancellationToken token = default)
        {
            ResourceBuildOperation.RejectLinks(path);
            var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                using var sha = SHA256.Create();
                var buffer = new byte[32768];
                if (input.Length != length)
                {
                    throw new InvalidDataException("Artifact length mismatch: " + Path.GetFileName(path));
                }

                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, count, buffer, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                token.ThrowIfCancellationRequested();
                if (input.Length != length || ResourceBundleEnvelope.Digest(sha.Hash) != digest)
                {
                    throw new InvalidDataException("Artifact SHA-256 mismatch: " + Path.GetFileName(path));
                }

                input.Position = 0;
                return input;
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }
    }
}
