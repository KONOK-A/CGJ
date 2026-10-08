// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ModuleManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using AssetBundleManager = XFABManager.AssetBundleManager;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{ 

    public class ModuleManager
    {

        #region 单例

        private static ModuleManager _instance;

        internal static ModuleManager Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new ModuleManager();
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
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


        private ModuleManager()
        {
        }

        #endregion

        #region 字段

        internal Dictionary<string, Module> modules = new Dictionary<string, Module>();

        internal Dictionary<string, Module> baseModules = new Dictionary<string, Module>();
 
        private Module current_module;

        #endregion




        #region 属性

        /// <summary>
        /// 所有的模块
        /// </summary>
        public static Dictionary<string, Module> AllModules => Instance.modules;

        public static Dictionary<string, Module> AllBaseModules => Instance.baseModules;
         

        private static Module CurrentModule
        {
            get { 
                return Instance.current_module;
            }
            set {
                Instance.current_module = value;
            }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 添加模块
        /// </summary>
        /// <param name="module">模块对象</param>
        internal static void AddModule(Module module)
        {

            if (!AllModules.ContainsKey(module.ModuleName))
                AllModules.Add(module.ModuleName, module);

            if (module.BaseModule && !AllBaseModules.ContainsKey(module.ModuleName))
            {
                AllBaseModules.Add(module.ModuleName, module);
            }

            CurrentModule = module;
        }

        /// <summary>
        /// 查询模块
        /// </summary>
        /// <param name="moduleTypeFullName">模块类型名称(含命名空间)</param>
        /// <returns></returns>
        public static Module GetModule(string moduleTypeFullName)
        {

            Module m;
            if (AllModules.TryGetValue(moduleTypeFullName, out m))
            {
                return m;
            }
            return null;

        }

        /// <summary>
        /// 查询模块(泛型方式)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns></returns>
        public static T GetModule<T>() where T : Module
        {
            return GetModule(typeof(T).FullName) as T;
        }

        /// <summary>
        /// 关闭模块
        /// </summary>
        /// <param name="moduleTypeFullName">模块类型名称(含命名空间)</param> 
        public static void CloseModule(string moduleTypeFullName)
        {
            Module module = GetModule(moduleTypeFullName);

            if (module != null) 
            {
#pragma warning disable CS0618 // 类型或成员已过时
                module.OnExit();
#pragma warning restore CS0618 // 类型或成员已过时
                module.Exit();
            }
            else
                Debug.LogWarningFormat("未查询到模块:{0}", moduleTypeFullName);

            // 清空表引用
            if (AllModules.ContainsKey(moduleTypeFullName))
                AllModules.Remove(moduleTypeFullName);
            if (AllBaseModules.ContainsKey(moduleTypeFullName))
                AllBaseModules.Remove(moduleTypeFullName);

            // 清空当前模块
            if (module == CurrentModule)
                CurrentModule = null;


            // 回收对象
            ClassObjectPoolManager.Unload(module); 
        }

        /// <summary>
        /// 关闭模块(泛型方式)
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        public static void CloseModule<T>() where T : Module
        {
            CloseModule(typeof(T).FullName);
        }

        /// <summary>
        /// 启动模块
        /// </summary>
        /// <param name="module">模块对象</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static StartUpModuleRequest StartUpModule(Module module)
        {
            if (module == null) throw new Exception("module is null!");


            string key = string.Format("YLFramework.Core.StartUpModule:{0}", module.ModuleName);
            return AssetBundleManager.ExecuteOnlyOnceAtATime<StartUpModuleRequest>(key, () =>
            {
                StartUpModuleRequest request = new StartUpModuleRequest();
                CoroutineStarter.Start(request.StartUp(module));
                return request;
            });
        }

        /// <summary>
        /// 启动模块(根据类型启动)
        /// </summary>
        /// <param name="moduleType">模块类型</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static StartUpModuleRequest StartUpModule(Type moduleType)
        {
            if (moduleType == null)
                throw new Exception("moduleType is null!");

            //Module module = Activator.CreateInstance(moduleType) as Module;
            Module module = ClassObjectPoolManager.Load(moduleType) as Module;
            return StartUpModule(module);
        }

        /// <summary>
        /// 启动模块(泛型方式)
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns></returns>
        public static StartUpModuleRequest StartUpModule<T>() where T : Module,new()
        {
            //Module module = Activator.CreateInstance<T>();
            Module module = ClassObjectPoolManager.Load<T>();
            return StartUpModule(module);
        }

        /// <summary>
        /// 启动模块(根据类名)
        /// </summary>
        /// <param name="moduleTypeFullName">模块类型名称(含命名空间)</param>
        /// <returns></returns>
        public static StartUpModuleRequest StartUpModule(string moduleTypeFullName)
        {
            Type type = AssemblyTools.GetType(moduleTypeFullName);
            return StartUpModule(type);
        }



        /// <summary>
        /// 获取当前使用的模块
        /// </summary>
        /// <returns></returns>
        internal static Module GetCurrentModule()
        {
            if (AllModules.Count == 1)
            {
                foreach (var item in AllModules.Values)
                    return item;
            }

            if (CurrentModule != null)
                return CurrentModule;

            // 
            if (AllBaseModules.Count == AllModules.Count)
            {
                // 返回一个基础模块 
                foreach (var item in AllBaseModules.Values)
                    return item;
            }
            else
            {
                // 找到一个不是基础模块的模块
                foreach (var item in AllModules.Values)
                {
                    if (item.BaseModule) continue;
                    return item;
                }
            }

            return null;
        }

        #endregion
    }

}
