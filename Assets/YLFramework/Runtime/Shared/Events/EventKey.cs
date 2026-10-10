using System;

namespace YLFramework.Events
{
    /// <summary>
    /// 保留完整身份的事件键；string、int、enum 与 Type 分属独立键域。
    /// string 使用 Ordinal 比较，enum 保留枚举类型及完整底层值；default(EventKey) 不是有效键。
    /// </summary>
    public readonly struct EventKey : IEquatable<EventKey>
    {
        private enum KeyKind : byte { Invalid, String, Int, Enum, Type }

        private readonly KeyKind _kind;
        private readonly object _value;
        private readonly int _intValue;

        public EventKey(string value)
        {
            _kind = KeyKind.String;
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _intValue = 0;
        }

        public EventKey(int value)
        {
            _kind = KeyKind.Int;
            _value = null;
            _intValue = value;
        }

        public EventKey(Enum value)
        {
            _kind = KeyKind.Enum;
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _intValue = 0;
        }

        public EventKey(Type value)
        {
            _kind = KeyKind.Type;
            _value = value ?? throw new ArgumentNullException(nameof(value));
            _intValue = 0;
        }

        /// <summary>以泛型声明类型构造 payload 类型键，不读取对象的运行时类型。</summary>
        public static EventKey ForType<T>() => new EventKey(typeof(T));

        public static implicit operator EventKey(string value) => new EventKey(value);
        public static implicit operator EventKey(int value) => new EventKey(value);
        public static implicit operator EventKey(Enum value) => new EventKey(value);
        public static implicit operator EventKey(Type value) => new EventKey(value);

        internal void Validate()
        {
            if (_kind == KeyKind.Invalid)
            {
                throw new ArgumentException("default(EventKey) is not a valid event key.");
            }
        }

        public bool Equals(EventKey other)
        {
            if (_kind != other._kind) return false;
            switch (_kind)
            {
                case KeyKind.String:
                    return StringComparer.Ordinal.Equals((string)_value, (string)other._value);
                case KeyKind.Int:
                    return _intValue == other._intValue;
                default:
                    return Equals(_value, other._value);
            }
        }

        public override bool Equals(object obj) => obj is EventKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _kind == KeyKind.Int ? _intValue
                    : _kind == KeyKind.String ? StringComparer.Ordinal.GetHashCode((string)_value)
                    : _value?.GetHashCode() ?? 0;
                return ((int)_kind * 397) ^ hash;
            }
        }

        public static bool operator ==(EventKey left, EventKey right) => left.Equals(right);
        public static bool operator !=(EventKey left, EventKey right) => !left.Equals(right);

        public override string ToString()
        {
            switch (_kind)
            {
                case KeyKind.String: return $"string:\"{_value}\"";
                case KeyKind.Int: return $"int:{_intValue}";
                case KeyKind.Enum: return $"enum:{_value.GetType().FullName}:{((Enum)_value).ToString("D")}";
                case KeyKind.Type: return $"type:{((Type)_value).FullName}";
                default: return "invalid";
            }
        }
    }
}
