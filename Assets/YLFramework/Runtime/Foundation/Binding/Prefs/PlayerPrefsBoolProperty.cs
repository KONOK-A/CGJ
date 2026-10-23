using UnityEngine;

namespace YLFramework.Binding
{
    /// <summary>
    /// 以 int（0/1）持久化到 PlayerPrefs 的 bool 属性。
    /// </summary>
    public class PlayerPrefsBoolProperty : PlayerPrefsProperty<bool>
    {
        /// <summary>
        /// 创建属性并立即从 PlayerPrefs 读取（0 为 false，1 为 true）。
        /// </summary>
        /// <param name="saveKey">PlayerPrefs 存储键，不能为 null 或空字符串。</param>
        /// <param name="defaultValue">键不存在时的默认值。</param>
        public PlayerPrefsBoolProperty(string saveKey, bool defaultValue = false) : base(saveKey, defaultValue)
        {
        }

        /// <inheritdoc />
        protected override bool Load(bool defaultValue) => PlayerPrefs.GetInt(Key, defaultValue ? 1 : 0) == 1;

        /// <inheritdoc />
        protected override void Save(bool value) => PlayerPrefs.SetInt(Key, value ? 1 : 0);
    }
}
