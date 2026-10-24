using System;

namespace YLFramework.Singletons
{
    /// <summary>
    /// 指定 MonoSingleton 在 Hierarchy 中的查找/创建路径：
    /// 创建实例时会先按该路径逐级查找（不存在则创建）承载 GameObject。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MonoSingletonPathAttribute : Attribute
    {
        /// <summary>
        /// 层级路径，例如 "YLFramework/Pool/GameObjectPool"（'/' 分隔）。
        /// </summary>
        public string HierarchyPath
        {
            get; private set;
        }

        /// <summary>
        /// 路径根节点是否 DontDestroyOnLoad，默认 true。
        /// </summary>
        public bool IsDontDestroy
        {
            get; private set;
        }

        /// <summary>
        /// 创建路径特性。
        /// </summary>
        /// <param name="hierarchyPath">层级路径，'/' 分隔；为空时查找失败并回退为默认创建方式。</param>
        /// <param name="isDontDestroy">路径根节点是否 DontDestroyOnLoad。</param>
        public MonoSingletonPathAttribute(string hierarchyPath, bool isDontDestroy = true)
        {
            HierarchyPath = hierarchyPath;
            IsDontDestroy = isDontDestroy;
        }
    }
}
