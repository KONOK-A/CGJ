using System;
using System.IO;
using YLFramework.Utilities.ZipTool.Zip;

namespace YLFramework.Utilities.ZipTool
{
    /// <summary>
    /// 会话式 zip 读取器：一次打开归档，多次读取/解压条目，Dispose 时关闭归档并恢复编码。
    /// 非线程安全，单线程使用；nameCodePage 在 reader 生命周期内生效（进程级静态，非线程安全）。
    /// </summary>
    public sealed class ZipArchiveReader : IDisposable
    {
        private const int BufferSize = 81920;

        private static readonly StringComparison PathComparison =
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private ZipFile _zipFile;
        private CodePageScope _codePageScope;

        /// <summary>
        /// 打开指定 zip 文件。reader 拥有内部打开的 FileStream，Dispose 时一并关闭。
        /// nameCodePage 非 null 时在 reader 生命周期内临时切换条目名解码编码。
        /// </summary>
        /// <exception cref="ArgumentNullException">zipFilePath 为 null。</exception>
        /// <exception cref="IOException">文件不存在或其他 IO 错误。</exception>
        /// <exception cref="ZipException">文件不是有效的 zip 归档。</exception>
        public ZipArchiveReader(string zipFilePath, int? nameCodePage = null)
        {
            _codePageScope = new CodePageScope(nameCodePage);
            try
            {
                _zipFile = new ZipFile(zipFilePath);
            }
            catch
            {
                _codePageScope.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 打开可寻址流（不可寻址抛 ArgumentException）；流可从任意 Position 开始，reader 内部自行定位。
        /// leaveOpen=false（默认）时 Dispose 会关闭调用方传入的流；true 则保留，由调用方负责释放
        /// （注意：打开失败时底层 ZipFile 的清理会先关闭该流，与 leaveOpen 无关）。
        /// </summary>
        public ZipArchiveReader(Stream zipStream, bool leaveOpen = false, int? nameCodePage = null)
        {
            if (zipStream == null)
            {
                throw new ArgumentNullException(nameof(zipStream));
            }

            _codePageScope = new CodePageScope(nameCodePage);
            try
            {
                _zipFile = new ZipFile(zipStream);
                _zipFile.IsStreamOwner = !leaveOpen;
            }
            catch
            {
                _codePageScope.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 条目总数（含目录条目）。
        /// </summary>
        public int EntryCount
        {
            get
            {
                ThrowIfDisposed();
                return (int)_zipFile.Count;
            }
        }

        /// <summary>
        /// 按 zip 内顺序返回全部条目名（含目录条目，分隔符保持存储原样）。
        /// </summary>
        public string[] GetEntryNames()
        {
            ThrowIfDisposed();

            var names = new string[EntryCount];
            int index = 0;
            foreach (ZipEntry entry in _zipFile)
            {
                names[index++] = entry.Name;
            }

            return names;
        }

        /// <summary>
        /// 判断是否存在指定条目（比较不区分大小写）。
        /// </summary>
        public bool ContainsEntry(string entryName)
        {
            ThrowIfDisposed();
            return _zipFile.GetEntry(entryName) != null;
        }

        /// <summary>
        /// 获取条目元数据大小；不存在返回 false；目录条目返回 true 且 size=0。
        /// </summary>
        public bool TryGetEntrySize(string entryName, out long size)
        {
            ThrowIfDisposed();

            ZipEntry entry = _zipFile.GetEntry(entryName);
            if (entry == null)
            {
                size = 0;
                return false;
            }

            size = Math.Max(entry.Size, 0);
            return true;
        }

        /// <summary>
        /// 读取条目全部字节；条目不存在抛 FileNotFoundException（消息含条目名）。
        /// </summary>
        public byte[] ReadEntryBytes(string entryName)
        {
            ThrowIfDisposed();

            ZipEntry entry = GetRequiredEntry(entryName);
            using (Stream entryStream = _zipFile.GetInputStream(entry))
            using (var memoryStream = new MemoryStream())
            {
                CopyStream(entryStream, memoryStream, new byte[BufferSize]);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// 尝试读取条目全部字节；条目不存在时返回 false 且 bytes 为 null。
        /// </summary>
        public bool TryReadEntryBytes(string entryName, out byte[] bytes)
        {
            try
            {
                bytes = ReadEntryBytes(entryName);
                return true;
            }
            catch (FileNotFoundException)
            {
                bytes = null;
                return false;
            }
        }

        /// <summary>
        /// 打开条目流式读取句柄；条目不存在抛 FileNotFoundException。
        /// 调用方必须先 Dispose 返回的流再 Dispose reader；reader 释放后该流失效。
        /// </summary>
        public Stream OpenEntryStream(string entryName)
        {
            ThrowIfDisposed();

            ZipEntry entry = GetRequiredEntry(entryName);
            return _zipFile.GetInputStream(entry);
        }

        /// <summary>
        /// 解压单个条目到指定文件，返回 true 表示文件已写出；条目不存在、条目为目录、
        /// 或 overwrite=false 且目标文件已存在时返回 false。父目录不存在则创建。
        /// entryName 为 zip 内部条目路径（查找不区分大小写）；destFilePath 为调用方指定的最终文件路径，
        /// 条目名不参与目标路径拼接，因此不存在 Zip Slip 问题。
        /// 写入采用"临时文件 + 成功后替换"（删除旧文件 + 改名，非原子）：失败时不会留下半成品文件，临时文件会被清理。
        /// </summary>
        public bool ExtractEntry(string entryName, string destFilePath, bool overwrite = true)
        {
            ThrowIfDisposed();

            ZipEntry entry = _zipFile.GetEntry(entryName);
            if (entry == null || entry.IsDirectory)
            {
                return false;
            }

            if (!overwrite && File.Exists(destFilePath))
            {
                return false;
            }

            string parentDirectory = Path.GetDirectoryName(destFilePath);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string tempPath = CreateTempPath(destFilePath);
            try
            {
                using (Stream entryStream = _zipFile.GetInputStream(entry))
                using (FileStream outputStream = File.Create(tempPath))
                {
                    CopyStream(entryStream, outputStream, new byte[BufferSize]);
                }

                ReplaceFile(tempPath, destFilePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            return true;
        }

        /// <summary>
        /// 解压全部条目到指定目录，返回实际写出的文件数（不含目录条目与 overwrite=false 跳过的文件）。
        /// 目录条目自动创建，父目录不存在则创建；overwrite=false 时已存在的文件跳过；
        /// 进度按已处理字节/总字节回调（0..1，正常完成时最后一次必为 1）。
        /// 解压前预扫描全部条目：配额与 Zip Slip 路径校验都发生在任何文件系统写入之前，命中抛
        /// <see cref="InvalidDataException"/>（负数配额抛 <see cref="ArgumentOutOfRangeException"/>），
        /// 此时目标目录不会被创建、不会写出任何文件。
        /// 每个文件先写同目录临时文件、成功后再替换目标文件（删除旧文件 + 改名，非原子），
        /// 中途失败不会留下半成品文件、临时文件会被清理；配额在拷贝期间按实际写出字节复核，元数据不可信时仍会拦截。
        /// </summary>
        public int ExtractAll(string destDirectory, UnzipOptions options = null, bool overwrite = true, Action<float> onProgress = null)
        {
            ThrowIfDisposed();

            if (destDirectory == null)
            {
                throw new ArgumentNullException(nameof(destDirectory));
            }

            ValidateOptions(options);

            string destRoot = Path.GetFullPath(destDirectory);
            string destRootWithSeparator = AppendDirectorySeparator(destRoot);

            long totalBytes = 0;
            long entryCount = 0;
            foreach (ZipEntry entry in _zipFile)
            {
                entryCount++;
                if (options != null && options.MaxEntryCount > 0 && entryCount > options.MaxEntryCount)
                {
                    throw new InvalidDataException("zip 条目数超过限制：" + entryCount + " > " + options.MaxEntryCount + "。");
                }

                GetSafeTargetPath(destRoot, destRootWithSeparator, entry.Name);

                if (!entry.IsFile || entry.Size <= 0)
                {
                    continue;
                }

                if (options != null && options.MaxEntryUncompressedBytes > 0 && entry.Size > options.MaxEntryUncompressedBytes)
                {
                    throw new InvalidDataException("zip 条目解压后大小超过限制：" + entry.Name + " " + entry.Size + " > " + options.MaxEntryUncompressedBytes + "。");
                }

                if (options != null && options.MaxTotalUncompressedBytes > 0 && entry.Size > options.MaxTotalUncompressedBytes - totalBytes)
                {
                    throw new InvalidDataException("zip 解压后总大小超过限制：" + (totalBytes + entry.Size) + " > " + options.MaxTotalUncompressedBytes + "。");
                }

                totalBytes += entry.Size;
            }

            Directory.CreateDirectory(destRoot);

            var buffer = new byte[BufferSize];
            long processedBytes = 0;
            int writtenCount = 0;

            foreach (ZipEntry entry in _zipFile)
            {
                string targetPath = GetSafeTargetPath(destRoot, destRootWithSeparator, entry.Name);

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                if (!entry.IsFile)
                {
                    continue;
                }

                if (!overwrite && File.Exists(targetPath))
                {
                    processedBytes += Math.Max(entry.Size, 0);
                    ReportProgress(onProgress, processedBytes, totalBytes);
                    continue;
                }

                string parentDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                long entryWritten = 0;
                string tempPath = CreateTempPath(targetPath);
                try
                {
                    using (Stream entryStream = _zipFile.GetInputStream(entry))
                    using (FileStream outputStream = File.Create(tempPath))
                    {
                        int read;
                        while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (options != null && options.MaxEntryUncompressedBytes > 0 && read > options.MaxEntryUncompressedBytes - entryWritten)
                            {
                                throw new InvalidDataException("zip 条目实际解压大小超过限制：" + entry.Name + "。");
                            }

                            if (options != null && options.MaxTotalUncompressedBytes > 0 && read > options.MaxTotalUncompressedBytes - processedBytes)
                            {
                                throw new InvalidDataException("zip 实际解压总量超过限制。");
                            }

                            entryWritten += read;
                            processedBytes += read;
                            outputStream.Write(buffer, 0, read);
                            ReportProgress(onProgress, processedBytes, totalBytes);
                        }
                    }

                    ReplaceFile(tempPath, targetPath);
                }
                finally
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }

                writtenCount++;
            }

            if (onProgress != null && (totalBytes == 0 || processedBytes < totalBytes))
            {
                onProgress(1f);
            }

            return writtenCount;
        }

        /// <summary>
        /// 关闭归档并恢复编码。重复调用安全。
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_zipFile != null)
                {
                    _zipFile.Close();
                    _zipFile = null;
                }
            }
            finally
            {
                _codePageScope.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_zipFile == null)
            {
                throw new ObjectDisposedException(nameof(ZipArchiveReader));
            }
        }

        private ZipEntry GetRequiredEntry(string entryName)
        {
            ZipEntry entry = _zipFile.GetEntry(entryName);
            if (entry == null)
            {
                throw new FileNotFoundException("zip 中不存在条目：" + entryName, _zipFile.Name);
            }

            return entry;
        }

        private static void ValidateOptions(UnzipOptions options)
        {
            if (options == null)
            {
                return;
            }

            if (options.MaxTotalUncompressedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxTotalUncompressedBytes, "MaxTotalUncompressedBytes 不能为负数，0 表示不限制。");
            }

            if (options.MaxEntryUncompressedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxEntryUncompressedBytes, "MaxEntryUncompressedBytes 不能为负数，0 表示不限制。");
            }

            if (options.MaxEntryCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxEntryCount, "MaxEntryCount 不能为负数，0 表示不限制。");
            }
        }

        private static string CreateTempPath(string targetPath)
        {
            return targetPath + ".tmp-" + Guid.NewGuid().ToString("N");
        }

        private static void ReplaceFile(string tempPath, string targetPath)
        {
            // 替换实现为"删除旧文件 + 改名"，不是原子操作；若改名失败，旧文件已删除，临时文件由调用方 finally 清理
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);
        }

        private static string GetSafeTargetPath(string destRoot, string destRootWithSeparator, string entryName)
        {
            if (string.IsNullOrEmpty(entryName))
            {
                throw new InvalidDataException("zip 条目名为空。");
            }

            string targetPath;
            try
            {
                targetPath = Path.GetFullPath(Path.Combine(destRoot, entryName));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                throw new InvalidDataException("zip 条目名非法：" + entryName, ex);
            }

            if (!targetPath.StartsWith(destRootWithSeparator, PathComparison))
            {
                throw new InvalidDataException("zip 条目路径越界（Zip Slip）：" + entryName);
            }

            return targetPath;
        }

        private static string AppendDirectorySeparator(string directoryPath)
        {
            if (directoryPath.Length > 0)
            {
                char last = directoryPath[directoryPath.Length - 1];
                if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                {
                    return directoryPath;
                }
            }

            return directoryPath + Path.DirectorySeparatorChar;
        }

        private static void ReportProgress(Action<float> onProgress, long processedBytes, long totalBytes)
        {
            if (onProgress == null || totalBytes <= 0)
            {
                return;
            }

            float value = (float)processedBytes / totalBytes;
            onProgress(value > 1f ? 1f : value);
        }

        private static void CopyStream(Stream input, Stream output, byte[] buffer)
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }
    }
}
