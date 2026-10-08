using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace YLFramework.Services.Save
{
    /// <summary>
    /// <see cref="SaveFileIO.ReadJson{T}"/> 的读取结果状态。
    /// </summary>
    internal enum SaveReadStatus
    {
        /// <summary>主文件读取成功。</summary>
        Success,

        /// <summary>主文件缺失或损坏，已从 <c>.bak</c> 备份读取成功，并已尝试把备份提升为主文件。</summary>
        RecoveredFromBackup,

        /// <summary>主文件与备份均不存在（正常的“尚无数据”状态，不是错误）。</summary>
        Missing,

        /// <summary>主文件存在但读取失败（损坏且备份不可用 / 备份同样失败 / 权限、IO 错误），<c>error</c> 包含原因。</summary>
        Failed
    }

    /// <summary>
    /// Save 模块内部文件读写辅助：JSON 序列化、原子写入（临时文件 + 替换 + 备份）、
    /// 读取失败时从备份恢复，以及损坏文件的保留。
    /// </summary>
    /// <remarks>
    /// 写入流程：先写 <c>{path}.tmp</c> 并 flush，再替换主文件并把旧内容留为 <c>{path}.bak</c>；
    /// 不支持 <see cref="File.Replace(string,string,string)"/> 的平台退化为“备份拷贝 + 删除 + 移动”。
    /// 读取流程：主文件失败时尝试 <c>.bak</c>，成功后将损坏主文件重命名为 <c>{path}.corrupt-时间戳</c>，
    /// 并把备份提升为主文件。
    /// </remarks>
    internal static class SaveFileIO
    {
        /// <summary>备份文件后缀；写入替换时旧内容保留于此。</summary>
        public const string BackupSuffix = ".bak";

        /// <summary>原子写入使用的临时文件后缀；正常写入完成后不存在。</summary>
        public const string TempSuffix = ".tmp";
        private const string CorruptSuffix = ".corrupt-";
        private const int MaxFileNameLength = 200;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

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
            if (fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0) return false;

            // Windows 会忽略文件名末尾的点与空白，拒绝以避免与合法名称互相覆盖
            var last = fileName[fileName.Length - 1];
            if (last == '.' || char.IsWhiteSpace(last)) return false;

            // Windows 保留设备名（"CON"、"CON.txt" 均不可用）
            var baseName = fileName.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            return !ReservedDeviceNames.Contains(baseName);
        }

        /// <summary>
        /// 将对象序列化为 JSON 并原子写入 <paramref name="path"/>；文件已存在时旧内容保留为 <c>.bak</c>。
        /// </summary>
        /// <param name="value">要序列化的对象。</param>
        /// <param name="path">目标文件路径；所在目录不存在时自动创建。</param>
        /// <param name="settings">JSON 序列化设置。</param>
        /// <param name="formatting">输出格式（缩进 / 紧凑）。</param>
        /// <exception cref="IOException">写入或替换失败时原样抛出；临时文件会被清理。</exception>
        /// <exception cref="UnauthorizedAccessException">目标路径无权限时原样抛出。</exception>
        public static void WriteJson(object value, string path, JsonSerializerSettings settings, Formatting formatting)
        {
            var json = JsonConvert.SerializeObject(value, formatting, settings);
            WriteTextAtomic(path, json);
        }

        /// <summary>
        /// 读取 JSON：优先主文件；主文件缺失或读取失败时尝试 <c>.bak</c> 备份，备份读取成功会将其提升为主文件。
        /// </summary>
        /// <typeparam name="T">反序列化目标类型（引用类型）。</typeparam>
        /// <param name="path">主文件路径。</param>
        /// <param name="settings">JSON 序列化设置。</param>
        /// <param name="value">读取成功时的结果；失败为 null。</param>
        /// <param name="error">失败原因；成功为 null。主文件与备份都失败时包含两段错误。</param>
        /// <returns>读取状态，见 <see cref="SaveReadStatus"/>。</returns>
        /// <remarks>
        /// 主文件损坏但备份可用时，损坏主文件会被保留为 <c>{path}.corrupt-时间戳</c>（<see cref="PreserveCorruptFile"/>），
        /// 然后备份被移动为主文件；提升失败则保留备份，下次读取会再次从备份恢复。
        /// </remarks>
        public static SaveReadStatus ReadJson<T>(string path, JsonSerializerSettings settings, out T value, out string error)
            where T : class
        {
            value = null;
            error = null;
            var primaryExists = File.Exists(path);
            if (primaryExists && TryReadJsonFile(path, settings, out value, out error))
            {
                return SaveReadStatus.Success;
            }

            var backupPath = path + BackupSuffix;
            if (File.Exists(backupPath))
            {
                if (TryReadJsonFile(backupPath, settings, out value, out var backupError))
                {
                    if (primaryExists)
                    {
                        PreserveCorruptFile(path);
                    }
                    TryPromoteBackup(backupPath, path);
                    return SaveReadStatus.RecoveredFromBackup;
                }
                error = string.IsNullOrEmpty(error) ? backupError : error + " | backup: " + backupError;
            }

            return primaryExists ? SaveReadStatus.Failed : SaveReadStatus.Missing;
        }

        /// <summary>
        /// 保留损坏文件：重命名为 <c>{path}.corrupt-时间戳</c> 并返回新路径。
        /// </summary>
        /// <param name="path">损坏文件路径；文件不存在时返回 null。</param>
        /// <returns>重命名后的路径；目标已存在时追加随机后缀；移动失败（IO / 权限）返回 null。</returns>
        public static string PreserveCorruptFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var target = path + CorruptSuffix + DateTime.Now.ToString("yyyyMMddHHmmss");
                if (File.Exists(target))
                {
                    target += "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
                }
                File.Move(path, target);
                return target;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
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

        // 单文件读取：捕获 JSON / IO / 权限异常并写入 error；反序列化结果为 null 同样视为失败。
        private static bool TryReadJsonFile<T>(string path, JsonSerializerSettings settings, out T value, out string error)
            where T : class
        {
            value = null;
            error = null;
            try
            {
                var json = File.ReadAllText(path);
                value = JsonConvert.DeserializeObject<T>(json, settings);
                if (value != null) return true;
                error = "反序列化结果为空";
                return false;
            }
            catch (JsonException e)
            {
                error = e.Message;
                return false;
            }
            catch (IOException e)
            {
                error = e.Message;
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                error = e.Message;
                return false;
            }
        }

        // 把备份提升为主文件；移动失败时静默保留备份（下次读取会再次从备份恢复，状态仍为 RecoveredFromBackup）。
        private static void TryPromoteBackup(string backupPath, string path)
        {
            try
            {
                File.Move(backupPath, path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // 原子写入：先完整写入 .tmp 并 flush 到磁盘，再替换主文件（旧内容转 .bak）；
        // 主文件此前不存在时直接移动。任一步骤失败都会清理临时文件并抛出。
        private static void WriteTextAtomic(string path, string content)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var tempPath = path + TempSuffix;
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Utf8NoBom))
                {
                    writer.Write(content);
                    writer.Flush();
                    // 参数 true 表示 flush 到磁盘，降低掉电 / 崩溃导致临时文件内容不完整的概率
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    var backupPath = path + BackupSuffix;
                    try
                    {
                        File.Replace(tempPath, path, backupPath);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        // 平台不支持 File.Replace 时走兼容路径（非原子）
                        FallbackReplace(tempPath, path, backupPath);
                    }
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

        // File.Replace 不可用时的兼容路径：先把旧文件拷贝为备份（拷贝失败也继续），再删除旧文件并移动临时文件。
        // 非原子：删除与移动之间存在主文件短暂缺失的窗口。
        private static void FallbackReplace(string tempPath, string path, string backupPath)
        {
            try
            {
                File.Copy(path, backupPath, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            File.Delete(path);
            File.Move(tempPath, path);
        }
    }
}
