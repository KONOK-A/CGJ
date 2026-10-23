using System;

namespace YLFramework.Foundation.Attributes
{
    /// <summary>
    /// 标记需要自定义 Editor 绘制的序列化字段。
    /// </summary>
    /// <remarks>
    /// 抽象特性，仅供派生：编辑器下派生类必须实现 <c>OnPropertyGui</c>，由外部编辑器代码读取字段特性后调用；
    /// Player 构建中不包含该方法，派生类中的重写代码需放在 <c>#if UNITY_EDITOR</c> 内。
    /// 框架的领域 Inspector 可读取该特性；Player 中只保留元数据，不引入 UnityEditor 引用。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public abstract class CustomPropertyGui : Attribute
    {
        /// <summary>字段值改变时是否请求接入方重新加载 TreeView；在 Editor 与 Player 中均可配置。</summary>
        public bool ReloadTreeViewOnValueChange;

        public CustomPropertyGui() : this(false) { }

        /// <param name="reloadTreeViewOnValueChange">字段值改变时是否重新加载 TreeView。</param>
        public CustomPropertyGui(bool reloadTreeViewOnValueChange)
        {
            this.ReloadTreeViewOnValueChange = reloadTreeViewOnValueChange;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中绘制当前字段。
        /// </summary>
        /// <param name="serializedProperty">字段对应的序列化属性。</param>
        public abstract void OnPropertyGui(UnityEditor.SerializedProperty serializedProperty);
#endif
    }
}
