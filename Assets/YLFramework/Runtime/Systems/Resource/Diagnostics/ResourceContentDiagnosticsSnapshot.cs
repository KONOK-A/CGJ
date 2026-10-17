namespace YLFramework.Services.Resource
{
    /// <summary>显式解密数据预算与额外native成本分列；不是密码模块或进程总内存硬上限。</summary>
    public readonly struct ResourceContentDiagnosticsSnapshot
    {
        /// <summary>本主体配置的显式数据缓冲上限。</summary>
        public long BufferLimitBytes { get; }
        /// <summary>当前已交付输入占用的数据缓冲字节，共享输入不重复计费。</summary>
        public long AllocatedBufferBytes { get; }
        /// <summary>尚在准备的输入预留额度，不与已分配额度双计。</summary>
        public long ReservedBufferBytes { get; }
        /// <summary>本主体已分配加预留的观测峰值，不是Profiler分配计数。</summary>
        public long PeakBufferBytes { get; }
        /// <summary>由本主体持有的加密输入数量，释放至真实native卸载后。</summary>
        public int LiveInputs { get; }
        /// <summary>当前异步内容IO/认证工作数量。</summary>
        public int WorkCount { get; }
        /// <summary>存活native加密输入声明的Unity读取buffer字节，非Profiler测量。</summary>
        public long DeclaredNativeReadBufferBytes { get; }
        /// <summary>以下CNG瞬时计数为本进程此正式适配器的全部owner，可能包含其他排空主体。</summary>
        public long ProcessCngProviderCount { get; }
        /// <summary>本进程正式适配器的存活CNG key owner数量，含其他排空主体。</summary>
        public long ProcessCngKeyCount { get; }
        /// <summary>CNG查询的存活key-object逻辑字节，非整个密码模块native内存。</summary>
        public long ProcessCngKeyObjectBytes { get; }
        /// <summary>本进程正式适配器累计释放失败次数；零不证明系统失败路径已测试。</summary>
        public long ProcessCngReleaseFailures { get; }
        /// <summary>关闭/归还失败后仍由本主体保留并负责重试的清理owner数量。</summary>
        public int RetainedCleanupOwners { get; }

        internal ResourceContentDiagnosticsSnapshot(long maximum, long allocated, long reserved, long peak, int inputs, int work,
            long nativeBuffers, int retained)
        {
            BufferLimitBytes = maximum; AllocatedBufferBytes = allocated; ReservedBufferBytes = reserved; PeakBufferBytes = peak;
            LiveInputs = inputs; WorkCount = work; DeclaredNativeReadBufferBytes = nativeBuffers; RetainedCleanupOwners = retained;
            ProcessCngProviderCount = CngSegmentAead.ActiveAlgorithms; ProcessCngKeyCount = CngSegmentAead.ActiveKeys;
            ProcessCngKeyObjectBytes = CngSegmentAead.ActiveKeyObjectBytes; ProcessCngReleaseFailures = CngSegmentAead.ReleaseFailures;
        }
    }
}
