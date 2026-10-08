// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/SerializedDictionary.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace YLFramework.Core
{

#if UNITY_2020_1_OR_NEWER

    [Serializable]
    public class DictionaryItem<TKey, TValue> 
    {
        public TKey Key;
        public TValue Value;
    }

    [Serializable]
    public class SerializedDictionary<TKey,TValue>
    { 
        #region 字段

        [SerializeField]
        private List<DictionaryItem<TKey,TValue>> datas;

        private Dictionary<TKey,TValue> datas_dic = null;

        private Dictionary<TKey, DictionaryItem<TKey, TValue>> datas_item_dic = null;

        #endregion


        #region 属性

        private List<DictionaryItem<TKey, TValue>> Datas {
            get {
                if (datas == null)
                    datas = new List<DictionaryItem<TKey, TValue>>();

                return datas;
            }
        }

        private Dictionary<TKey, DictionaryItem<TKey, TValue>> DatasItemDic
        {
            get
            {
                if (datas_item_dic == null)
                    datas_item_dic = new Dictionary<TKey, DictionaryItem<TKey, TValue>>(Datas.Count);

                if (datas_item_dic.Count != Datas.Count)
                {
                    datas_item_dic.Clear();
                    foreach (var item in Datas)
                    {
                        if (datas_item_dic.ContainsKey(item.Key)) 
                            continue;
                        datas_item_dic.Add(item.Key, item);
                    }
                }

                return datas_item_dic;
            }
        }

        private Dictionary<TKey, TValue> DatasDic 
        {
            get 
            { 
                if (datas_dic == null)
                    datas_dic = new Dictionary<TKey, TValue>(Datas.Count);
                 
                if (datas_dic.Count != Datas.Count)
                {
                    datas_dic.Clear();
                    foreach (var item in Datas)
                    {
                        if (datas_dic.ContainsKey(item.Key)) 
                        {
                            Debug.LogErrorFormat("SerializedDictionary检测到相同的Key:{0}", item.Key);
                            continue;
                        }
                        datas_dic.Add(item.Key, item.Value);
                    } 
                }

                return datas_dic;
            }
        }

        public Dictionary<TKey, TValue>.KeyCollection Keys => DatasDic.Keys;

        public Dictionary<TKey, TValue>.ValueCollection Values => DatasDic.Values;


        public int Count => DatasDic.Count;

        #endregion


        #region 方法



        public void Add(TKey key, TValue value) 
        {
            Datas.Add(new DictionaryItem<TKey, TValue> { Key = key, Value = value } );
        }
        public void Clear() 
        {
            Datas.Clear();
        }

        public bool ContainsKey(TKey key)
        {
            return DatasDic.ContainsKey(key);
        }

        public bool ContainsValue(TValue value) 
        {
            return DatasDic.ContainsValue(value);
        }

        public bool Remove(TKey key) 
        {
            if (ContainsKey(key))
            {  
                return Datas.Remove(DatasItemDic[key]); 
            }

            return false;
        }

        #endregion

        #region 重写方法

        public TValue this[TKey key] 
        {
            get 
            { 
                if(!DatasDic.ContainsKey(key))
                    return default(TValue);
                return DatasDic[key];
            }
            set {
                DatasDic[key] = value;
            }
        }

        #endregion


    }
#endif
}
