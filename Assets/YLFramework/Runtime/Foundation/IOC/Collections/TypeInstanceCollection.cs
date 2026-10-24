using System;
using System.Collections.Generic;

namespace YLFramework.DependencyInjection
{
    /// <summary>
    /// 多键字典：(服务类型, 名称) → 已注册实例。
    /// 名称的 null 与空字符串等价；同一键重复写入会覆盖旧值；值允许为 null（显式注册的空实例）。
    /// 属于容器的公开状态，直接操作时不会经过注册校验。
    /// </summary>
    public class TypeInstanceCollection : Dictionary<Tuple<Type, string>, object>
    {
        /// <summary>
        /// 按服务类型与名称读写实例；键不存在时返回 null（与显式注册 null 实例无法通过本索引器区分，可用 <see cref="Dictionary{TKey,TValue}.TryGetValue"/> 判断）。
        /// </summary>
        /// <param name="from">服务类型（注册源类型）。</param>
        /// <param name="name">注册名称；null 与空字符串等价。</param>
        public object this[Type from, string name = null]
        {
            get
            {
                var key = new Tuple<Type, string>(from, NameKey.Normalize(name));
                return TryGetValue(key, out var instance) ? instance : null;
            }
            set
            {
                var key = new Tuple<Type, string>(from, NameKey.Normalize(name));
                this[key] = value;
            }
        }
    }
}
