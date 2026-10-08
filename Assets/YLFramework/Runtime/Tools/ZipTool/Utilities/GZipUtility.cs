using System;
using System.IO;
using YLFramework.Utilities.ZipTool.GZip;

namespace YLFramework.Utilities.ZipTool
{
    /// <summary>
    /// GZip 压缩/解压工具（无 Unity 依赖），基于模块内置的 SharpZipLib GZip 流实现。
    /// 支持 0 字节数据（空输入压缩为合法 gzip，解压回 0 字节）；永不 Dispose 调用方传入的流；
    /// 截断或损坏的输入会抛出异常（GZipException / EndOfStreamException），不做静默容错；
    /// 例外：完整数据块之后的合法多块（concatenated gzip）会继续解压，无法解析的尾随数据被静默忽略。
    /// </summary>
    public static class GZipUtility
    {
        private const int BufferSize = 81920;

        /// <summary>
        /// 压缩字节数组；data 为 null 抛 <see cref="ArgumentNullException"/>。
        /// </summary>
        public static byte[] Compress(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            {
                Compress(input, output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// 解压 GZip 字节数组；data 为 null 抛 <see cref="ArgumentNullException"/>，
        /// 0 字节输入返回 0 字节，截断 / 损坏输入抛 GZipException / EndOfStreamException
        /// （完整块之后无法解析的尾随数据会被忽略）。
        /// </summary>
        public static byte[] Decompress(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            using (var input = new MemoryStream(data))
            using (var output = new MemoryStream())
            {
                Decompress(input, output);
                return output.ToArray();
            }
        }

        /// <summary>
        /// 从 input 读取数据压缩后写入 output；不关闭也不接管调用方传入的流（调用完成后仍可继续使用）。
        /// </summary>
        public static void Compress(Stream input, Stream output)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            using (var gzipStream = new GZipOutputStream(output) { IsStreamOwner = false })
            {
                CopyStream(input, gzipStream);
            }
        }

        /// <summary>
        /// 从 input 读取 GZip 数据解压后写入 output；不关闭也不接管调用方传入的流。
        /// 异常时 output 可能已写入部分数据，由调用方决定如何清理。
        /// </summary>
        public static void Decompress(Stream input, Stream output)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            using (var gzipStream = new GZipInputStream(input) { IsStreamOwner = false })
            {
                CopyStream(gzipStream, output);
            }
        }

        private static void CopyStream(Stream input, Stream output)
        {
            var buffer = new byte[BufferSize];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }
    }
}
