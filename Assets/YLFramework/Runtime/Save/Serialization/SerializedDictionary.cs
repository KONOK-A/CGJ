using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace YLFramework.Services.Save
{
    /// <summary>
    /// 可序列化的字典：用两个平行列表 <c>keyList</c> / <c>valueList</c> 承载数据，
    /// 以支持 Unity 序列化（Inspector / JsonUtility / 二进制）等无法直接序列化 <see cref="Dictionary{K,V}"/> 的场合；
    /// 运行时通过 <see cref="Dictionary"/> 访问真正的字典。
    /// </summary>
    /// <typeparam name="K">键类型；需支持相等比较，使用 Unity 序列化时自身需可被序列化。</typeparam>
    /// <typeparam name="V">值类型；使用 Unity 序列化时自身需可被序列化。</typeparam>
    /// <remarks>
    /// <para>
    /// 序列化前 <see cref="OnBeforeSerialize"/> 把字典复制到两个列表，反序列化后 <see cref="OnAfterDeserialize"/> 用列表重建字典；
    /// 列表由本类维护，正常流程下两者等长。若序列化数据中出现重复键，重建时 <see cref="Dictionary{K,V}.Add"/> 会抛出异常。
    /// </para>
    /// <para>
    /// 类型名与 SRP Core 中的 <c>UnityEngine.Rendering.SerializedDictionary&lt;K,V&gt;</c> 相同，注意区分，避免 using 冲突。
    /// </para>
    /// </remarks>
    [Serializable]
    public class SerializedDictionary<K, V> : ISerializationCallbackReceiver
    {
        [SerializeField,HideInInspector] private List<K> keyList;
        [SerializeField,HideInInspector] private List<V> valueList;

        [NonSerialized] // 不序列化 避免报错
        private Dictionary<K, V> dictionary;

        /// <summary>
        /// 底层字典（构造函数与反序列化流程都会初始化，正常情况下非 null）。
        /// 直接增删改即可，下次序列化时会自动同步到 <c>keyList</c> / <c>valueList</c>。
        /// </summary>
        public Dictionary<K, V> Dictionary => dictionary;

        /// <summary>
        /// 创建空字典。
        /// </summary>
        public SerializedDictionary()
        {
            dictionary = new Dictionary<K, V>();
        }

        /// <summary>
        /// 以现有字典创建包装（直接引用，不复制）。
        /// </summary>
        /// <param name="dictionary">初始字典；不能为 null，否则后续序列化读取 <c>Keys / Values</c> 时会抛出 <see cref="NullReferenceException"/>。</param>
        public SerializedDictionary(Dictionary<K, V> dictionary)
        {
            this.dictionary = dictionary;
        }

        // 序列化的时候把字典里面的内容放进list
        [OnSerializing]
        private void OnSerializing(StreamingContext context)
        {
            OnBeforeSerialize();
        }

        // 反序列化时候自动完成字典的初始化
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            OnAfterDeserialize();
        }

        /// <summary>
        /// 序列化前由 Unity（<see cref="ISerializationCallbackReceiver"/>）或 <c>[OnSerializing]</c> 调用：
        /// 用字典当前内容整体重建 <c>keyList</c> / <c>valueList</c>（键与值按同一枚举顺序一一对应）。
        /// </summary>
        public void OnBeforeSerialize()
        {
            keyList = new List<K>(dictionary.Keys);
            valueList = new List<V>(dictionary.Values);
        }

        /// <summary>
        /// 反序列化后由 Unity（<see cref="ISerializationCallbackReceiver"/>）或 <c>[OnDeserialized]</c> 调用：
        /// 先丢弃旧字典，再用 <c>keyList</c> / <c>valueList</c> 重建，最后清空两个列表。
        /// 若列表中存在重复键将抛出异常。
        /// </summary>
        public void OnAfterDeserialize()
        {
            dictionary = new Dictionary<K, V>();
            for (var i = 0; i < keyList.Count; i++)
            {
                dictionary.Add(keyList[i], valueList[i]);
            }
            keyList.Clear();
            valueList.Clear();
        }
    }
}