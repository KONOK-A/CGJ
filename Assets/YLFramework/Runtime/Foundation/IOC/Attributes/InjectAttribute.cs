using System;

namespace YLFramework.Core.IOC
{
    /// <summary>
    /// 标记需要由 IOC 容器注入的实例字段或属性，可指定注册名称。
    /// 被标记的成员视为必需依赖：无法解析时会抛出 <see cref="IOCInjectionException"/>；
    /// readonly 字段、无 setter 属性与 indexer 不支持注入。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectAttribute : Attribute
    {
        /// <summary>
        /// 创建无名称（默认注册）的注入标记。
        /// </summary>
        public InjectAttribute()
        {
        }

        /// <summary>
        /// 创建指定注册名称的注入标记。
        /// </summary>
        /// <param name="name">注册名称；null 与空字符串等价于无名注册。</param>
        public InjectAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 注册名称；null 或空字符串表示使用无名注册。
        /// </summary>
        public string Name { get; set; }
    }
}
