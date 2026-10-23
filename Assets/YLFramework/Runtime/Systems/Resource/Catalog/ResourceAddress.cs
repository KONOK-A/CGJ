using System;

namespace YLFramework.Services.Resource
{
    /// <summary>不可变逻辑地址；按 ordinal 匹配，不修正大小写或猜测原 XFAB 项目名。</summary>
    public readonly struct ResourceAddress : IEquatable<ResourceAddress>
    {
        public string ProjectId
        {
            get;
        }
        public string Path
        {
            get;
        }
        public string SubAsset
        {
            get;
        }
        public bool IsValid => ProjectId != null && Path != null;

        public ResourceAddress(string projectId, string path, string subAsset = null)
        {
            ValidateId(projectId, nameof(projectId));
            ValidatePath(path, nameof(path));
            if (subAsset != null)
            {
                ValidatePath(subAsset, nameof(subAsset));
            }

            ProjectId = projectId;
            Path = path;
            SubAsset = subAsset;
        }

        internal static void ValidateId(string value, string parameter)
        {
            if (string.IsNullOrEmpty(value) || value == "." || value == "..")
            {
                throw new ArgumentException("A nonempty stable identifier is required.", parameter);
            }

            foreach (var character in value)
            {
                if (!(character >= 'a' && character <= 'z' || character >= 'A' && character <= 'Z' ||
                      character >= '0' && character <= '9' || character == '_' || character == '-' || character == '.'))
                {
                    throw new ArgumentException("Identifiers only accept ASCII letters, digits, '.', '_' and '-'.", parameter);
                }
            }
        }

        internal static void ValidatePath(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.IndexOf('\\') >= 0)
            {
                throw new ArgumentException("An exact nonempty forward-slash address is required.", parameter);
            }

            foreach (var character in value)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException("Control characters are not allowed.", parameter);
                }
            }

            foreach (var segment in value.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException("Empty, '.' and '..' address segments are not allowed.", parameter);
                }
            }
        }

        public bool Equals(ResourceAddress other) => ProjectId == other.ProjectId && Path == other.Path && SubAsset == other.SubAsset;
        public override bool Equals(object obj) => obj is ResourceAddress other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((ProjectId?.GetHashCode() ?? 0) * 397 ^ (Path?.GetHashCode() ?? 0)) * 397 ^ (SubAsset?.GetHashCode() ?? 0);
            }
        }
        public static bool operator ==(ResourceAddress left, ResourceAddress right) => left.Equals(right);
        public static bool operator !=(ResourceAddress left, ResourceAddress right) => !left.Equals(right);
        public override string ToString() => SubAsset == null ? $"{ProjectId}:{Path}" : $"{ProjectId}:{Path}#{SubAsset}";
    }
}
