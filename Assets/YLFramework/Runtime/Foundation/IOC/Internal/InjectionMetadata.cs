using System;
using System.Linq;
using System.Reflection;

namespace YLFramework.Core.IOC
{
    /// <summary>
    /// 单个带 <see cref="InjectAttribute"/> 成员的描述（字段或属性）。
    /// </summary>
    internal sealed class InjectableMember
    {
        internal MemberInfo Member;

        internal Type MemberType;

        internal string InjectName;

        internal bool IsPublic;

        internal bool CanInject;

        internal string InvalidReason;

        internal string KindName => Member is FieldInfo ? "field" : "property";
    }

    /// <summary>
    /// 单个类型的可注入成员清单（按声明层级 base→derived、字段→属性排列，顺序确定但非公开契约）。
    /// 按注入模式惰性缓存过滤结果，避免每次 Inject 重新扫描反射。
    /// </summary>
    internal sealed class InjectionMetadata
    {
        private readonly InjectableMember[] _orderedMembers;
        private InjectableMember[] _onlyPublic;
        private InjectableMember[] _onlyNonPublic;

        internal InjectionMetadata(InjectableMember[] orderedMembers)
        {
            _orderedMembers = orderedMembers;
        }

        /// <summary>
        /// 按搜索模式返回本次要处理的成员：OnlyPublic 只含公开成员，OnlyNonPublic 只含非公开成员，All 为全部。
        /// </summary>
        internal InjectableMember[] GetMembers(IOCContainer.MemberSearchModes mode)
        {
            switch (mode)
            {
                case IOCContainer.MemberSearchModes.OnlyPublic:
                    return _onlyPublic ?? (_onlyPublic = Filter(true));
                case IOCContainer.MemberSearchModes.OnlyNonPublic:
                    return _onlyNonPublic ?? (_onlyNonPublic = Filter(false));
                default:
                    return _orderedMembers;
            }
        }

        private InjectableMember[] Filter(bool isPublic) =>
            _orderedMembers.Where(member => member.IsPublic == isPublic).ToArray();
    }
}
