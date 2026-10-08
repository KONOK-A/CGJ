using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YLFramework.Core.IOC
{
    /// <summary>
    /// 按引用比较对象的内部比较器，用于 InjectAll 对同一实例去重。
    /// </summary>
    internal sealed class ObjectReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ObjectReferenceComparer Instance = new ObjectReferenceComparer();

        private ObjectReferenceComparer()
        {
        }

        bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
