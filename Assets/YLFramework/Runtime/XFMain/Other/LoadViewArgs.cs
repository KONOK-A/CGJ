// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Other/LoadViewArgs.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.RoslynAnalyzers;


 
namespace YLFramework.Core
{
    /// <summary>
    /// 加载View的参数,使用 LoadViewArgs.New() 来创建实例
    /// 改类的对象会在OnLoaded方法执行完成后自动回收，请勿引用 
    /// </summary>
    [DisableNew("请使用静态方法 LoadViewArgs.New() 来创建对象!")]
    [MethodLocalOnlyAttribute("该类会在View.OnLoaded方法执行完成后自动回收,请勿引用!")]
    public class LoadViewArgs : IReuseable
    {
        #region 字段

#pragma warning disable IDE0044 // 添加只读修饰符
        private Dictionary<string, string> strings = new Dictionary<string, string>(); 

        private Dictionary<string, int> ints = new Dictionary<string, int>();

        private Dictionary<string, bool> bools = new Dictionary<string, bool>();
         
        private Dictionary<string, float> floats = new Dictionary<string, float>();

        private Dictionary<string, long> longs = new Dictionary<string, long>();

        private Dictionary<string, double> doubles = new Dictionary<string, double>();

        private Dictionary<string, object> objects = new Dictionary<string, object>();

#pragma warning restore IDE0044 // 添加只读修饰符

        #endregion

        #region 查询

        public string GetString(string name)
        {
            if(strings.ContainsKey(name))
                return strings[name];

            return string.Empty;
        }

        public int GetInt(string name)
        {
            if (ints.ContainsKey(name))
                return ints[name];

            return 0;
        }

        public bool GetBool(string name)
        {
            if (bools.ContainsKey(name))
                return bools[name];

            return false;
        }

        public float GetFloat(string name)
        {
            if (floats.ContainsKey(name))
                return floats[name];

            return 0.0f;
        }

        public long GetLong(string name)
        {
            if (longs.ContainsKey(name))
                return longs[name];

            return 0;
        }

        public double GetDouble(string name)
        {
            if (doubles.ContainsKey(name))
                return doubles[name];

            return 0.0;
        }

        public object GetObject(string name)
        {
            if (objects.ContainsKey(name))
                return objects[name];
            return null;
        }

        #endregion

        #region 修改

        public void SetString(string name, string value)
        {
            strings[name] = value;
        }

        public void SetInt(string name, int value)
        {
            ints[name] = value;
        }

        public void SetBool(string name, bool value)
        {
            bools[name] = value;
        }

        public void SetFloat(string name, float value)
        {
            floats[name] = value;
        }

        public void SetLong(string name, long value)
        {
            longs[name] = value;
        }

        public void SetDouble(string name, double value)
        {
            doubles[name] = value; 
        }

        public void SetObject(string name, object value)
        {
            objects[name] = value;
        }

        #endregion

        #region 生命周期

        public void OnLoaded()
        {
            // 清理数据
            strings.Clear();
            ints.Clear();
            bools.Clear();
            floats.Clear();
            longs.Clear();
            doubles.Clear();
            objects.Clear();
        }

        public void OnUnloaded()
        { 
        }

        #endregion

        #region 构造方法

        /// <summary>
        /// 创建 LoadViewArgs 对象
        /// </summary>
        /// <returns></returns>
        public static LoadViewArgs New()
        { 
            return ClassObjectPoolManager.Load<LoadViewArgs>();
        }

        internal static void Recycle(LoadViewArgs args) 
        {
            ClassObjectPoolManager.Unload(args);
        }

        #endregion 

    } 

}