#if UNITY_EDITOR

using System;
using UnityEditor;

namespace YLFramework.Binding
{
    /// <summary>
    /// EditorPrefs 持久化属性基类：构造时读取，Value 变化时立即写回（仅在编辑器环境下编译）。
    /// 不会感知外部对 EditorPrefs 的直接修改，需要同步时调用 Reload。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    public abstract class EditorPrefsProperty<T> : BindableProperty<T>
    {
        private bool _skipNextSave;

        /// <summary>
        /// 构造时立即从 EditorPrefs 读取（键不存在时使用 <paramref name="defaultValue"/>）。
        /// </summary>
        /// <param name="key">EditorPrefs 存储键，不能为 null 或空字符串。</param>
        /// <param name="defaultValue">键不存在时的默认值。</param>
        /// <exception cref="ArgumentException">key 为 null 或空字符串。</exception>
        protected EditorPrefsProperty(string key, T defaultValue)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("key 不能为空", nameof(key));
            }

            Key = key;
            DefaultValue = defaultValue;
            _value = Load(defaultValue);
        }

        /// <summary>
        /// EditorPrefs 存储键，构造后不可变。
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 键不存在或调用 <see cref="Delete"/> 后使用的默认值。
        /// </summary>
        public T DefaultValue { get; }

        /// <summary>
        /// EditorPrefs 中当前是否存在该键。
        /// </summary>
        public bool HasKey => EditorPrefs.HasKey(Key);

        /// <summary>
        /// 删除持久化键并把内存值重置为 DefaultValue（会触发一次正常的值变化回调，但不会写回）
        /// </summary>
        public void Delete()
        {
            EditorPrefs.DeleteKey(Key);
            SetValueFromStorage(DefaultValue);
        }

        /// <summary>
        /// 重新从 EditorPrefs 读取（外部直接改过 EditorPrefs 时使用），变化时正常触发回调
        /// </summary>
        public void Reload() => SetValueFromStorage(Load(DefaultValue));

        /// <summary>
        /// 值变化时先写回 EditorPrefs（Delete/Reload 触发的这一次会跳过），再执行基类通知。
        /// </summary>
        protected override void OnValueChange()
        {
            // 只跳过本次由 Delete/Reload 触发的写回；回调里再次改值仍会正常持久化
            if (_skipNextSave)
            {
                _skipNextSave = false;
            }
            else
            {
                Save(_value);
            }
            base.OnValueChange();
        }

        /// <summary>
        /// 从 EditorPrefs 读取值；仅由构造与 <see cref="Reload"/>/<see cref="Delete"/> 调用，不触发通知。
        /// </summary>
        /// <param name="defaultValue">键不存在时的默认值。</param>
        protected abstract T Load(T defaultValue);

        /// <summary>
        /// 把值写入 EditorPrefs；在值变化通知之前调用，写入异常会中断后续通知并向上传播。
        /// </summary>
        protected abstract void Save(T value);

        private void SetValueFromStorage(T value)
        {
            _skipNextSave = true;
            try
            {
                Value = value;
            }
            finally
            {
                // 兜底复位：回调抛异常时也不会把跳过标记残留到下一次正常写入
                _skipNextSave = false;
            }
        }
    }
}

#endif
