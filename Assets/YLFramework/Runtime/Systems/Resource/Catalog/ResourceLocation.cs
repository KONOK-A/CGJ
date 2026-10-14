using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>来源身份，不使用注册顺序或字符串前缀猜测加载后端。</summary>
    public readonly struct ResourceSourceId : IEquatable<ResourceSourceId>
    {
        public static ResourceSourceId Resources { get; } = new ResourceSourceId("resources");
        public string Value { get; }
        public bool IsValid => Value != null;
        public ResourceSourceId(string value)
        {
            ResourceAddress.ValidateId(value, nameof(value));
            Value = value;
        }
        public bool Equals(ResourceSourceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ResourceSourceId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value ?? "<invalid-source>";
    }

    /// <summary>最小内存加载描述；locator 原样保真，来源自行验证其语法。不定义持久化格式。</summary>
    public sealed class ResourceLocation : IEquatable<ResourceLocation>
    {
        public ResourceSourceId Source { get; }
        public string Locator { get; }
        public Type AssetType { get; }

        public ResourceLocation(ResourceSourceId source, string locator, Type assetType)
        {
            if (!source.IsValid) throw new ArgumentException("A valid source identity is required.", nameof(source));
            if (string.IsNullOrWhiteSpace(locator)) throw new ArgumentException("An exact source locator is required.", nameof(locator));
            if (assetType == null || !typeof(UnityEngine.Object).IsAssignableFrom(assetType) || assetType.ContainsGenericParameters)
                throw new ArgumentException("Asset types must be compiled UnityEngine.Object types.", nameof(assetType));
            Source = source;
            Locator = locator;
            AssetType = assetType;
        }

        public bool Equals(ResourceLocation other) => other != null && Source.Equals(other.Source) && Locator == other.Locator && AssetType == other.AssetType;
        public override bool Equals(object obj) => obj is ResourceLocation other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return (Source.GetHashCode() * 397 ^ Locator.GetHashCode()) * 397 ^ AssetType.GetHashCode(); }
        }
        public override string ToString() => $"{Source}:{Locator} ({AssetType.FullName})";
    }
}
