using System;

namespace YLFramework.Resource
{
    public enum ResourceError
    {
        InvalidConfiguration,
        ConfigurationConflict,
        InvalidAddress,
        AddressNotFound,
        TypeMismatch,
        SourceUnavailable,
        Unsupported,
        InProgress,
        LoadFailed,
        Canceled,
        SessionUnavailable,
        Busy,
        SceneIdentityAmbiguous,
        SceneUnloaded,
        ActivationTimedOut,
        StaleBase,
        UpdateFailed,
        /// <summary>应用未提供当前明确选择的内容key，不应因此隐式重下载或切换版本。</summary>
        ContentKeyUnavailable,
        /// <summary>当前密文认证失败，拒绝交付未经认证的字节。</summary>
        ContentAuthenticationFailed,
        /// <summary>内容编码或封装不受支持，不回退为明文输入。</summary>
        ContentFormatUnsupported,
        /// <summary>显式数据预算不足，须归还自有责任后显式重试。</summary>
        ContentBufferUnavailable
    }

    /// <summary>资源域失败，保留原始 cause 与调用地址/来源，不使用 null 伪装取得成功。</summary>
    public sealed class ResourceException : Exception
    {
        public ResourceError Kind
        {
            get;
        }
        public ResourceAddress? Address
        {
            get;
        }
        public ResourceSourceId? Source
        {
            get;
        }
        public ResourceException(ResourceError kind, string message, Exception cause = null,
            ResourceAddress? address = null, ResourceSourceId? source = null) : base(message, cause)
        {
            Kind = kind;
            Address = address;
            Source = source;
        }
    }
}
