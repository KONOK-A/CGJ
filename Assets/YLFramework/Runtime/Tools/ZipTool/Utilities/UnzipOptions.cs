namespace YLFramework.Utilities.ZipTool
{
    /// <summary>
    /// 解压配额选项。各限制为 0 表示不限制，负数非法（解压入口抛 <see cref="System.ArgumentOutOfRangeException"/>）；
    /// 恰好等于上限允许，超过即拒绝。仅约束解压写出量，不改变 zip 解析。
    /// 网络来源（热更、下载）建议使用 <see cref="ForNetworkDownloads"/> 预设。
    /// </summary>
    public sealed class UnzipOptions
    {
        /// <summary>
        /// 全部文件解压后总字节数上限；0 = 不限制。
        /// </summary>
        public long MaxTotalUncompressedBytes { get; set; }

        /// <summary>
        /// 单条目解压后字节数上限；0 = 不限制。
        /// </summary>
        public long MaxEntryUncompressedBytes { get; set; }

        /// <summary>
        /// 条目总数上限（含目录条目）；0 = 不限制。
        /// </summary>
        public int MaxEntryCount { get; set; }

        /// <summary>
        /// 网络下载来源的推荐预设：总量 4GB / 单条目 1GB / 10 万条目。
        /// </summary>
        public static UnzipOptions ForNetworkDownloads()
        {
            return new UnzipOptions
            {
                MaxTotalUncompressedBytes = 4L * 1024 * 1024 * 1024,
                MaxEntryUncompressedBytes = 1L * 1024 * 1024 * 1024,
                MaxEntryCount = 100000,
            };
        }
    }
}
