using System;
using System.Collections.Generic;
using System.Linq;

namespace YLFramework.Utilities.Extensions
{
    /// <summary>
    /// 集合扩展：遍历、字典合并与空集合判断。
    /// 不复制 LINQ 语义，仅提供少量便捷方法与 Unity 友好的空判断。
    /// </summary>
    public static class SystemCollectionsExtension
    {

        /// <summary>
        /// 遍历 IEnumerable。为保证遍历中修改原集合安全，会先物化为数组（数组直接复用），并返回该数组。
        /// </summary>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> self, Action<T> action)
        {
            var forEach = self as T[] ?? self.ToArray();
            foreach (var item in forEach)
            {
                action(item);
            }

            return forEach;
        }
        /// <summary>
        /// 从末尾向前遍历 List，返回原 List。
        /// </summary>
        public static List<T> ForEachReverse<T>(this List<T> selfList, Action<T> action)
        {
            for (var i = selfList.Count - 1; i >= 0; --i)
            {
                action(selfList[i]);
            }
            return selfList;
        }


        /// <summary>
        /// 带索引正序遍历 List，回调参数为 (index, item)。
        /// </summary>
        public static void ForEach<T>(this List<T> list, Action<int, T> action)
        {
            for (var i = 0; i < list.Count; i++)
            {
                action(i, list[i]);
            }
        }



        /// <summary>
        /// 遍历字典，回调参数为 (key, value)。
        /// </summary>
        public static void ForEach<K, V>(this Dictionary<K, V> dict, Action<K, V> action)
        {
            var dictE = dict.GetEnumerator();

            while (dictE.MoveNext())
            {
                var current = dictE.Current;
                action(current.Key, current.Value);
            }

            dictE.Dispose();
        }


        /// <summary>
        /// 合并字典并返回合并结果。不支持重复 Key：键相同但值不同的键值对会抛 <see cref="ArgumentException"/>；
        /// 键值完全相同的键值对会被 Union 去重。未传入任何待合并字典时直接返回原字典实例，不复制。
        /// </summary>
        public static Dictionary<TKey, TValue> Merge<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            params Dictionary<TKey, TValue>[] dictionaries)
        {
            return dictionaries.Aggregate(dictionary,
                (current, dict) => dict.Union(current).ToDictionary(kv => kv.Key, kv => kv.Value));
        }
        /// <summary>
        /// 将另一个字典的内容加入当前字典，支持重复 Key。
        /// </summary>
        /// <param name="isOverride">重复 Key 时是否用参数字典的值覆盖当前值。</param>
        public static void AddRange<K, V>(this Dictionary<K, V> dict, Dictionary<K, V> addInDict,
            bool isOverride = false)
        {
            // 自合并时覆盖写入会让同一字典的枚举器失效，先物化一份（仅退化路径产生分配）
            if (ReferenceEquals(dict, addInDict))
            {
                addInDict = new Dictionary<K, V>(addInDict);
            }

            var enumerator = addInDict.GetEnumerator();

            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (dict.ContainsKey(current.Key))
                {
                    if (isOverride)
                        dict[current.Key] = current.Value;
                    continue;
                }

                dict.Add(current.Key, current.Value);
            }

            enumerator.Dispose();
        }


        /// <summary>数组是否为 null 或长度为 0。</summary>
        public static bool IsNullOrEmpty<T>(this T[] collection) => collection == null || collection.Length == 0;
        /// <summary>List 等 IList 是否为 null 或元素数为 0。</summary>
        public static bool IsNullOrEmpty<T>(this IList<T> collection) => collection == null || collection.Count == 0;
        /// <summary>序列是否为 null 或不含任何元素（会枚举一次）。</summary>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection) => collection == null || !collection.Any();//Any表示有任意一个
        /// <summary>数组是否非 null 且长度不为 0。</summary>
        public static bool IsNotNullAndEmpty<T>(this T[] collection) => !IsNullOrEmpty(collection);
        /// <summary>List 等 IList 是否非 null 且元素数不为 0。</summary>
        public static bool IsNotNullAndEmpty<T>(this IList<T> collection) => !IsNullOrEmpty(collection);
        /// <summary>序列是否非 null 且含至少一个元素（会枚举一次）。</summary>
        public static bool IsNotNullAndEmpty<T>(this IEnumerable<T> collection) => !IsNullOrEmpty(collection);
    }
}
