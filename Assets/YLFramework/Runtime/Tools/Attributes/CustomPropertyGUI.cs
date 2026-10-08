using System;

namespace YLFramework.Core.Attributes
{
    /// <summary>
    /// 标记需要自定义 Editor 绘制的序列化字段。
    /// </summary>
    /// <remarks>
    /// 抽象特性，仅供派生：编辑器下派生类必须实现 <c>OnPropertyGUI</c>，由外部编辑器代码读取字段特性后调用；
    /// Player 构建中不包含该方法，派生类中的重写代码需放在 <c>#if UNITY_EDITOR</c> 内。
    /// 框架内暂无内置消费者，标记字段的读取与绘制由接入方负责。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public abstract class CustomPropertyGUI : Attribute
    {
#if UNITY_EDITOR
        /// <summary>
        /// 在编辑器中绘制当前字段。
        /// </summary>
        /// <param name="serializedProperty">字段对应的序列化属性。</param>
        public abstract void OnPropertyGUI(UnityEditor.SerializedProperty serializedProperty);
#endif
    }
}
