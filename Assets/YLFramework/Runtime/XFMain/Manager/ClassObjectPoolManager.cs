// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ClassObjectPoolManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{

    /// <summary>
    /// 可重复利用的接口(使用类对象池ClassObjectPoolManager管理对象时,会触发对应方法)
    /// </summary>
    public interface IReuseable
    {
        void OnLoaded();
        void OnUnloaded();
    }

    /// <summary>
    /// 类的对象池
    /// </summary>
    public class ClassObjectPoolManager
    {

        #region 单例

        private static ClassObjectPoolManager _instance;

        internal static ClassObjectPoolManager Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new ClassObjectPoolManager();
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
                    CoroutineStarter.Start(Check());
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


#if UNITY_EDITOR

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            // Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 
            // 该选项如果禁用掉,重新运行时静态数据不会清空,可能会导致异常,所以这里置空,防止异常
            switch (obj)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _instance = null;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _instance = null;
                    break;
            }
        }
#endif
         
        private ClassObjectPoolManager()
        {
        }

        #endregion


        #region 常量

        private const int UNLOAD_OUT_TIME = 60 * 10; // 10 分钟 
        private const int DETECTION_INTERVAL = 60;

        #endregion

        #region 字段

        // Fix编码
        private Dictionary<Type, List<object>> objects = new Dictionary<Type, List<object>>();

        private Dictionary<object, float> unloaded_times = new Dictionary<object, float>();

        private List<Type> temp_keys = new List<Type>();
        #endregion


        #region 属性

        private static Dictionary<Type, List<object>> Objects => Instance.objects;

        private static Dictionary<object, float> UnloadedTimes => Instance.unloaded_times;

        private static List<Type> TempKeys => Instance.temp_keys;

        #endregion

        #region 方法 


        private static object Get(Type type)
        {

            if (Objects.ContainsKey(type) && Objects[type].Count > 0)
            {
                object obj = Objects[type].Last();
                Objects[type].Remove(obj);
                if (UnloadedTimes.ContainsKey(obj))
                    UnloadedTimes.Remove(obj);
                if (Objects[type].Count == 0)
                    Objects.Remove(type);
                 
                return obj;
            }

            return null;
        }

        /// <summary>
        /// 加载对象
        /// 根据类型去对象池查找是否有空闲的对象，如果有则返回该空闲对象，如果没有空闲对象，则创建对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Load<T>() where T : class , new()
        {
            // 判断字典中是否有闲置的 
            Type type = typeof(T);

             
            // 查询对象池中是否有数据 
            T t = null;

            object obj = Get(type);
            if(obj != null) t = obj as T;

            if(t == null) t = new T();
            
            IReuseable reuseable = obj as IReuseable; 
            if(reuseable != null)  reuseable.OnLoaded();

            return t;
        }

        /// <summary>
        /// 加载对象
        /// 根据类型去对象池查找是否有空闲的对象，如果有则返回该空闲对象，如果没有空闲对象，则创建对象
        /// </summary> 
        /// <returns></returns>
        public static object Load(Type type)
        { 
            // 查询对象池中是否有数据 
            object t = Get(type);

            //try
            //{
                if (t == null) 
                {
                    // 没有查询到数据 直接return!
                    t = Activator.CreateInstance(type); 
                }

                IReuseable reuseable = t as IReuseable;
                if(reuseable != null) reuseable.OnLoaded();
            //}
            //catch (MissingMethodException)
            //{
            //    throw new Exception(string.Format("类型:{0}请提供无参构造函数!", type.FullName));
            //}
             
            return t;
        }

        /// <summary>
        /// 回收对象
        /// 把对象放到对象池中,等待下次使用,如果超过10分钟仍未被使用,会被自动清理!
        /// </summary>
        /// <param name="reuseable"></param>
        public static void Unload(object obj)
        {
            // 放回到对象池中 
            if (obj == null)
                return;

            // 已经回收了
            if (UnloadedTimes.ContainsKey(obj))
                return;

            Type type = obj.GetType();
            // 开始计时(如果这个对象在池子里面的时间过长，就会被移除掉)
            UnloadedTimes.Add(obj, Time.time); 

            IReuseable reuseable = obj as IReuseable; 
            if(reuseable != null) reuseable.OnUnloaded();
            
            // 放回池子
            if (Objects.ContainsKey(type))
                Objects[type].Add(obj);
            else
                Objects.Add(type, new List<object>() { obj });
        }

        // 如果保存的对象超过一定的时间 就把它移除
        private static IEnumerator Check()
        {
            while (true)
            {
                // 1分钟检测一次
                yield return new WaitForSeconds(DETECTION_INTERVAL);

                TempKeys.Clear();

                foreach (var key in Objects.Keys)
                {
                    TempKeys.Add(key);
                }

                for (int i = 0; i < TempKeys.Count; i++)
                {
                    var key = TempKeys[i];

                    if (Objects.TryGetValue(key, out var item)) 
                    {
                        for (var j = 0; j < item.Count; j++)
                        {
                            object reuseable = item[j];

                            bool remove;

                            if (UnloadedTimes.ContainsKey(reuseable))
                            {
                                remove = Time.time - UnloadedTimes[reuseable] >= UNLOAD_OUT_TIME;
                            }
                            else
                            {
                                remove = true;
                            }

                            if (remove)
                            {
                                UnloadedTimes.Remove(reuseable);
                                item.RemoveAt(j);
                                j--;
                            }
                        }

                        if (item.Count == 0 )
                        {
                            Objects.Remove(key); 
                        } 
                    }
                     
                    yield return null;
                }
                  
            }
        }
         
        #endregion
    }
}


