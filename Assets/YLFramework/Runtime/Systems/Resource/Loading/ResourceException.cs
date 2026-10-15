using System;

namespace YLFramework.Services.Resource
{
    public enum ResourceError
    {
        InvalidConfiguration, ConfigurationConflict, InvalidAddress, AddressNotFound, TypeMismatch,
        SourceUnavailable, Unsupported, InProgress, LoadFailed, Canceled, SessionUnavailable, Busy,
        SceneIdentityAmbiguous, SceneUnloaded, ActivationTimedOut, StaleBase, UpdateFailed
    }

    /// <summary>资源域失败，保留原始 cause 与调用地址/来源，不使用 null 伪装取得成功。</summary>
    public sealed class ResourceException : Exception
    {
        public ResourceError Kind { get; }
        public ResourceAddress? Address { get; }
        public ResourceSourceId? Source { get; }
        public ResourceException(ResourceError kind, string message, Exception cause = null,
            ResourceAddress? address = null, ResourceSourceId? source = null) : base(message, cause)
        {
            Kind = kind;
            Address = address;
            Source = source;
        }
    }
}
