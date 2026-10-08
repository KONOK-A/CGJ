// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ModuleManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using System.Collections.Generic;
using System.Collections.ObjectModel;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
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
            allModules = new ReadOnlyDictionary<string, Module>(modules);
            allBaseModules = new ReadOnlyDictionary<string, Module>(baseModules);
        }

        #endregion

        #region 字段

        private readonly Dictionary<string, Module> modules = new Dictionary<string, Module>();

        private readonly Dictionary<string, Module> baseModules = new Dictionary<string, Module>();
        private readonly IReadOnlyDictionary<string, Module> allModules;
        private readonly IReadOnlyDictionary<string, Module> allBaseModules;
        private readonly Dictionary<Type, StartUpModuleRequest> startingModules = new Dictionary<Type, StartUpModuleRequest>();
        private readonly HashSet<Type> creatingModules = new HashSet<Type>();
 
        private Module current_module;
        private Module startupContext;

        #endregion




        #region 属性

        /// <summary>
        /// 所有已注册模块的只读实时视图；须通过State区分运行与失败状态。
        /// </summary>
        public static IReadOnlyDictionary<string, Module> AllModules => Instance.allModules;

        /// <summary>
        /// 已注册基础模块的只读实时视图，不允许外部修改权威索引。
        /// </summary>
        public static IReadOnlyDictionary<string, Module> AllBaseModules => Instance.allBaseModules;
         

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
        /// 注册唯一实例；相同实例幂等，同名或同类型的其他实例明确拒绝。
        /// </summary>
        /// <param name="module">模块对象</param>
        internal static void AddModule(Module module)
        {

            if (module == null) throw new ArgumentNullException(nameof(module));
            if (Instance.modules.TryGetValue(module.ModuleName, out var registered))
            {
                if (!ReferenceEquals(registered, module))
                    throw new InvalidOperationException($"Module already registered: {module.ModuleName}");
                return;
            }
            if (module.State == ModuleState.Closed || module.State == ModuleState.Closing || module.State == ModuleState.CloseFailed)
                throw new InvalidOperationException($"Cannot register Module in state {module.State}: {module.ModuleName}");
            Instance.modules.Add(module.ModuleName, module);
            if (module.BaseModule) Instance.baseModules.Add(module.ModuleName, module);
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
            return GetModule(typeof(T)) as T;
        }

        /// <summary>
        /// 按具体类型查询已注册实例；Starting可见，失败状态仍需显式清理。
        /// </summary>
        public static Module GetModule(Type moduleType)
        {
            if (moduleType == null) throw new ArgumentNullException(nameof(moduleType));
            var module = GetModule(moduleType.FullName);
            return module != null && module.GetType() == moduleType ? module : null;
        }

        /// <summary>
        /// 关闭模块；Starting拒绝，CloseFailed允许再次清理。基础模块Shutdown尚未支持。
        /// </summary>
        /// <param name="moduleTypeFullName">模块类型名称(含命名空间)</param> 
        public static void CloseModule(string moduleTypeFullName)
        {
            Module module = GetModule(moduleTypeFullName);
            if (module == null)
            {
                Debug.LogWarningFormat("未查询到模块:{0}", moduleTypeFullName);
                return;
            }
            CloseModule(module);
        }

        internal static void CloseModule(Module module)
        {
            if (!ReferenceEquals(GetModule(module.ModuleName), module)) return;
            if (module.State == ModuleState.Starting)
                throw new InvalidOperationException($"Cannot close a starting Module: {module.ModuleName}");
            if (module.State == ModuleState.Closing) return;
            if (module.BaseModule) throw new InvalidOperationException("基础模块不能关闭!");
            module.State = ModuleState.Closing;
            try
            {
#pragma warning disable CS0618 // 类型或成员已过时
                module.OnExit();
#pragma warning restore CS0618 // 类型或成员已过时
                module.Exit();
                if (HasRegisteredObjects(module))
                    throw new InvalidOperationException($"Module still owns registered objects after close: {module.ModuleName}");
            }
            catch
            {
                // 完整异常清理管线留给后续阶段，不能把部分退出的实例标记为Closed。
                module.State = ModuleState.CloseFailed;
                if (ReferenceEquals(module, CurrentModule)) CurrentModule = null;
                throw;
            }
            Instance.modules.Remove(module.ModuleName);
            Instance.baseModules.Remove(module.ModuleName);
            if (ReferenceEquals(module, CurrentModule)) CurrentModule = null;
            // View移除会保留空桶；只清除已确认无对象的索引，为同实例重启建立干净状态。
            module.models.Clear();
            module.views.Clear();
            module.type_views.Clear();
            module.State = ModuleState.Closed;
        }

        private static bool HasRegisteredObjects(Module module)
        {
            if (module.controllers.Count != 0) return true;
            foreach (var bucket in module.models.Values)
                if (bucket.Count != 0) return true;
            foreach (var bucket in module.views.Values)
                if (bucket.Count != 0) return true;
            foreach (var bucket in module.type_views.Values)
                if (bucket.Count != 0) return true;
            return false;
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
        /// 启动唯一实例；Starting返回同一请求，Running返回新的已完成成功结果。
        /// 请求不池化；普通模块失败后必须关闭并清理才能重启，冲突在执行OnStart前抛出异常。
        /// 基础模块失败后的Shutdown与完整回滚由后续生命周期阶段提供。
        /// </summary>
        /// <param name="module">模块对象</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static StartUpModuleRequest StartUpModule(Module module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            var type = module.GetType();
            if (Instance.creatingModules.Contains(type))
                throw new InvalidOperationException($"Module construction is in progress: {module.ModuleName}");
            var registered = GetModule(module.ModuleName);
            if (registered != null && !ReferenceEquals(registered, module))
                throw new InvalidOperationException($"Module already registered: {module.ModuleName}");
            if ((module.State == ModuleState.Starting || module.State == ModuleState.Running) && !ReferenceEquals(registered, module))
                throw new InvalidOperationException($"Module lifecycle has no matching registration: {module.ModuleName}");
            if (module.State == ModuleState.Starting)
                return Instance.startingModules[type];
            if (module.State == ModuleState.Running)
            {
                var completed = new StartUpModuleRequest(module);
                completed.Finish();
                return completed;
            }
            if (module.State != ModuleState.Created && module.State != ModuleState.Closed)
                throw new InvalidOperationException($"Module must be cleaned before restarting: {module.ModuleName} ({module.State})");
            if (module.State == ModuleState.Closed && HasRegisteredObjects(module))
                throw new InvalidOperationException($"Closed Module still owns registered objects: {module.ModuleName}");
            var request = new StartUpModuleRequest(module, CurrentModule);
            // 发布记录后才执行注册、协程和任何业务回调，支持无yield的同步完成与重入。
            module.State = ModuleState.Starting;
            Instance.startingModules.Add(type, request);
            try
            {
                AddModule(module);
                CurrentModule = module;
                var coroutine = CoroutineStarter.Start(request.StartUp());
                if (coroutine == null && !request.isDone)
                    request.Finish("Module startup requires an active coroutine host.");
            }
            catch (Exception error)
            {
                if (!request.isDone) request.Finish(error.ToString());
                else Debug.LogException(error);
            }
            return request;
        }

        internal static void CompleteStartup(StartUpModuleRequest request, string error)
        {
            var module = request.Module;
            if (!Instance.startingModules.TryGetValue(module.GetType(), out var pending) || !ReferenceEquals(pending, request)) return;
            Instance.startingModules.Remove(module.GetType());
            module.State = string.IsNullOrEmpty(error) ? ModuleState.Running : ModuleState.StartFailed;
            if (module.State == ModuleState.Running) CurrentModule = module;
            else if (ReferenceEquals(CurrentModule, module))
                CurrentModule = IsCurrentCandidate(request.PreviousCurrentModule) ? request.PreviousCurrentModule : null;
            request.PreviousCurrentModule = null;
        }

        internal static bool MoveStartup(Module module, System.Collections.IEnumerator routine)
        {
            var previous = Instance.startupContext;
            Instance.startupContext = module;
            try { return routine.MoveNext(); }
            finally { Instance.startupContext = IsCurrentCandidate(previous) ? previous : null; }
        }

        private static bool IsCurrentCandidate(Module module)
        {
            return module != null && ReferenceEquals(GetModule(module.ModuleName), module) &&
                (module.State == ModuleState.Starting || module.State == ModuleState.Running);
        }

        /// <summary>
        /// 启动模块(根据类型启动)
        /// </summary>
        /// <param name="moduleType">模块类型</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static StartUpModuleRequest StartUpModule(Type moduleType)
        {
            if (moduleType == null) throw new ArgumentNullException(nameof(moduleType));
            if (!typeof(Module).IsAssignableFrom(moduleType) || moduleType.IsAbstract || moduleType.ContainsGenericParameters)
                throw new ArgumentException("A concrete Module type is required.", nameof(moduleType));
            var registered = GetModule(moduleType.FullName);
            if (registered != null)
            {
                if (registered.GetType() != moduleType)
                    throw new InvalidOperationException($"Module name already registered: {moduleType.FullName}");
                return StartUpModule(registered);
            }
            if (!Instance.creatingModules.Add(moduleType))
                throw new InvalidOperationException($"Module construction is in progress: {moduleType.FullName}");
            Module module;
            try { module = (Module)Activator.CreateInstance(moduleType); }
            finally { Instance.creatingModules.Remove(moduleType); }
            return StartUpModule(module);
        }

        /// <summary>
        /// 启动模块(泛型方式)
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns></returns>
        public static StartUpModuleRequest StartUpModule<T>() where T : Module,new()
        {
            return StartUpModule(typeof(T));
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
            if (IsCurrentCandidate(Instance.startupContext)) return Instance.startupContext;
            if (IsCurrentCandidate(CurrentModule)) return CurrentModule;
            foreach (var item in AllModules.Values)
                if (IsCurrentCandidate(item) && !AllBaseModules.ContainsKey(item.ModuleName)) return item;
            foreach (var item in AllBaseModules.Values)
                if (IsCurrentCandidate(item)) return item;
            return null;
        }

        #endregion
    }

}
