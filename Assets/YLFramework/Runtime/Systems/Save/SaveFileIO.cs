using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YLFramework.Services.Save
{
    /// <summary>
    /// Save 内部文件边界。读取不改盘；原子替换不可用时失败关闭，不删除原件降级写入。
    /// </summary>
    /// <remarks>
    /// 每次写入独占临时文件，flush 后检查取消。File.Replace / File.Move 为提交点；
    /// 提交后不再检查取消，调用方必须按已成功提交处理。修复备份是显式操作。
    /// </remarks>
    internal static class SaveFileIO
    {
        /// <summary>备份文件后缀；写入替换时旧内容保留于此。</summary>
        public const string BackupSuffix = ".bak";

        /// <summary>原子写入使用的临时文件后缀；正常写入完成后不存在。</summary>
        public const string TempSuffix = ".tmp";
        private const string CorruptSuffix = ".corrupt-";
        private const int MaxFileNameLength = 200;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        // Windows 保留设备名（不区分大小写，带扩展名同样保留）
        private static readonly HashSet<string> ReservedDeviceNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// 校验对象 / 设置文件名（不含扩展名）是否合法。
        /// 约束：非空且非全空白；长度不超过 200；不含路径分隔符与非法字符；不为 <c>"." / ".."</c>；
        /// 末尾不是点或空白（Windows 会忽略这类字符，避免不同名称互相覆盖）；不是 Windows 保留设备名
        /// （如 <c>CON</c>、<c>COM1</c>，带扩展名的形式同样拒绝）。
        /// </summary>
        /// <param name="fileName">待校验的文件名。</param>
        /// <returns>合法返回 true，否则返回 false（不抛异常）。</returns>
        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            if (fileName.Length > MaxFileNameLength) return false;
            if (fileName == "." || fileName == "..") return false;
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            if (fileName.IndexOfAny(new[] { '<', '>', ':', '"', '|', '?', '*' }) >= 0) return false;
            foreach (var character in fileName) if (char.IsControl(character)) return false;
            if (fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0) return false;

            // Windows 会忽略文件名末尾的点与空白，拒绝以避免与合法名称互相覆盖
            var last = fileName[fileName.Length - 1];
            if (last == '.' || char.IsWhiteSpace(last)) return false;

            // Windows 保留设备名（"CON"、"CON.txt" 均不可用）
            var baseName = fileName.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            return !ReservedDeviceNames.Contains(baseName);
        }

        internal static void ValidateFileName(string name)
        { if (!IsValidFileName(name)) throw new ArgumentException("Invalid save file name: " + name, nameof(name)); }

        internal static string Serialize(object value, JsonSerializerSettings settings, Formatting formatting)
        {
            using (var text = new StringWriter(CultureInfo.InvariantCulture))
            using (var writer = new JsonTextWriter(text) { Formatting = formatting })
            {
                JsonSerializer.Create(settings ?? new JsonSerializerSettings()).Serialize(writer, value);
                return text.ToString();
            }
        }

        /// <summary>
        /// 读取 JSON：仅 JSON / UTF8 内容损坏时尝试备份；IO 与权限错误不伪装成缺失或损坏。
        /// </summary>
        /// <typeparam name="T">反序列化目标类型（引用类型）。</typeparam>
        /// <param name="path">主文件路径。</param>
        /// <param name="settings">JSON 序列化设置。</param>
        /// <param name="value">读取成功时的结果；失败为 null。</param>
        /// <param name="error">失败原因；成功为 null。主文件与备份都失败时包含两段错误。</param>
        /// <returns>读取状态，见 <see cref="SaveReadStatus"/>。</returns>
        /// <remarks>
        /// 读取备份不修改任何文件，避免读操作消耗唯一良好备份或覆盖损坏证据。
        /// </remarks>
        public static SaveReadStatus ReadJson<T>(string path, JsonSerializerSettings settings, out T value, out string error)
            where T : class
        {
            var primary = ReadJsonFile(path, settings, out value, out error, out _);
            if (primary == SaveReadStatus.Success) return primary;
            var backupPath = path + BackupSuffix;
            var backup = ReadJsonFile(backupPath, settings, out value, out var backupError, out _);
            if (backup == SaveReadStatus.Success) return SaveReadStatus.RecoveredFromBackup;
            error = string.IsNullOrEmpty(error) ? backupError : error + " | backup: " + backupError;
            return primary == SaveReadStatus.Missing && backup == SaveReadStatus.Missing ? SaveReadStatus.Missing : SaveReadStatus.Failed;
        }

        /// <summary>
        /// 尽力删除文件：文件不存在或删除失败（IO / 权限）时静默忽略，不抛异常。
        /// </summary>
        /// <param name="path">要删除的文件路径。</param>
        public static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // 只将真正的 FileNotFound / DirectoryNotFound 视作缺失，不能用 File.Exists 吞掉权限/IO 错误。
        internal static SaveReadStatus ReadJsonFile<T>(string path, JsonSerializerSettings settings, out T value, out string error, out string json)
            where T : class
        {
            value = null;
            error = null;
            json = null;
            try
            {
                json = File.ReadAllText(path, Utf8NoBom);
                var document = JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (document.Type == JTokenType.Null) throw new JsonSerializationException("JSON null is not save data.");
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text))
                    value = JsonSerializer.Create(settings ?? new JsonSerializerSettings()).Deserialize<T>(reader);
                if (value != null) return SaveReadStatus.Success;
                error = "反序列化结果为空";
                return SaveReadStatus.Failed;
            }
            catch (JsonException e)
            {
                error = e.Message;
                return SaveReadStatus.Failed;
            }
            catch (DecoderFallbackException e)
            {
                error = e.Message;
                return SaveReadStatus.Failed;
            }
            catch (FileNotFoundException) { return SaveReadStatus.Missing; }
            catch (DirectoryNotFoundException) { return SaveReadStatus.Missing; }
        }

        internal static void ValidateWritableJson(string path)
        {
            var status = ReadJson<JToken>(path, null, out _, out var error);
            if (status == SaveReadStatus.Failed || status == SaveReadStatus.RecoveredFromBackup)
                throw new SaveRecoveryRequiredException(path, error);
        }

        internal static string RestoreBackup<T>(string path, JsonSerializerSettings settings,
            CancellationToken cancellationToken, Action<T> validate = null) where T : class
        {
            var backup = path + BackupSuffix;
            if (ReadJsonFile<T>(backup, settings, out var value, out var error, out var json) != SaveReadStatus.Success)
                throw new InvalidDataException("Invalid backup: " + backup + " | " + error);
            validate?.Invoke(value);
            cancellationToken.ThrowIfCancellationRequested();
            string preserved = null;
            if (File.Exists(path))
            {
                preserved = path + CorruptSuffix + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
                File.Copy(path, preserved, false);
            }
            WriteTextAtomic(path, json, cancellationToken, true, false);
            return preserved;
        }

        internal static void WriteTextAtomic(string path, string content,
            CancellationToken cancellationToken = default, bool overwrite = true, bool keepBackup = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var tempPath = path + TempSuffix + "-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Utf8NoBom))
                {
                    var buffer = new char[4096];
                    for (var offset = 0; offset < content.Length; offset += 4096)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var count = Math.Min(4096, content.Length - offset);
                        content.CopyTo(offset, buffer, 0, count);
                        writer.Write(buffer, 0, count);
                    }
                    writer.Flush();
                    // 参数 true 表示 flush 到磁盘，降低掉电 / 崩溃导致临时文件内容不完整的概率
                    stream.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (overwrite && File.Exists(path))
                {
                    File.Replace(tempPath, path, keepBackup ? path + BackupSuffix : null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        internal static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("An explicit save root is required.", nameof(root));
            var full = Path.GetFullPath(root);
            EnsureNoLinks(full);
            var normalized = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (normalized.Length <= Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
                throw new ArgumentException("A filesystem root cannot be used as a save directory.", nameof(root));
            return normalized;
        }

        internal static void EnsureNoLinks(string path)
        {
            for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(current); }
                catch (FileNotFoundException) { continue; }
                catch (DirectoryNotFoundException) { continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Save paths cannot traverse symbolic links or junctions: " + current);
            }
        }
    }
}
