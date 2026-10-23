using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using YLFramework.Foundation;
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
                    EditorApplication.playModeStateChanged -= EditorApplicationPlayModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplicationPlayModeStateChanged;
#endif
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


#if UNITY_EDITOR

        private static void EditorApplicationPlayModeStateChanged(PlayModeStateChange obj)
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
            _allModules = new ReadOnlyDictionary<string, Module>(_modules);
            _allBaseModules = new ReadOnlyDictionary<string, Module>(_baseModules);
        }

        #endregion

        #region 字段

        private readonly Dictionary<string, Module> _modules = new Dictionary<string, Module>();

        private readonly Dictionary<string, Module> _baseModules = new Dictionary<string, Module>();
        private readonly IReadOnlyDictionary<string, Module> _allModules;
        private readonly IReadOnlyDictionary<string, Module> _allBaseModules;
        private readonly Dictionary<Type, StartUpModuleRequest> _startingModules = new Dictionary<Type, StartUpModuleRequest>();
        private readonly HashSet<Type> _creatingModules = new HashSet<Type>();
        private readonly List<Module> _registrationOrder = new List<Module>();
        private bool _isShuttingDown;

        private Module _currentModule;
        private Module _startupContext;

        #endregion




        #region 属性

        /// <summary>
        /// 所有已注册模块的只读实时视图；Starting可见不代表已经Running。
        /// </summary>
        public static IReadOnlyDictionary<string, Module> AllModules => Instance._allModules;

        /// <summary>
        /// 已注册基础模块的只读实时视图，不允许外部修改权威索引。
        /// </summary>
        public static IReadOnlyDictionary<string, Module> AllBaseModules => Instance._allBaseModules;


        private static Module CurrentModule
        {
            get
            {
                return Instance._currentModule;
            }
            set
            {
                Instance._currentModule = value;
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

            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (Instance._isShuttingDown)
            {
                throw new InvalidOperationException("Cannot register a Module during Shutdown.");
            }

            if (Instance._modules.TryGetValue(module.ModuleName, out var registered))
            {
                if (!ReferenceEquals(registered, module))
                {
                    throw new InvalidOperationException($"Module already registered: {module.ModuleName}");
                }

                return;
            }
            if (module.State == ModuleState.Closed || module.State == ModuleState.Closing || module.State == ModuleState.CloseFailed)
            {
                throw new InvalidOperationException($"Cannot register Module in state {module.State}: {module.ModuleName}");
            }

            Instance._modules.Add(module.ModuleName, module);
            module.ViewLifecycle = new object();
            Instance._registrationOrder.Add(module);
            if (module.IsBaseModule)
            {
                Instance._baseModules.Add(module.ModuleName, module);
            }
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
        /// 按具体类型查询已注册实例；Starting可见，启动失败回滚后不可见。
        /// </summary>
        public static Module GetModule(Type moduleType)
        {
            if (moduleType == null)
            {
                throw new ArgumentNullException(nameof(moduleType));
            }

            var module = GetModule(moduleType.FullName);
            return module != null && module.GetType() == moduleType ? module : null;
        }

        /// <summary>
        /// 唯一关闭管线；基础模块及Starting拒绝，清理错误在Closed与注销后汇总报告。
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
            CloseModule(module, false);
        }

        private static void ValidateClose(Module module)
        {
            if (module.State == ModuleState.Starting)
            {
                throw new InvalidOperationException($"Cannot close a starting Module: {module.ModuleName}");
            }

            ValidateObjectTransactions(module);
        }

        private static void ValidateObjectTransactions(Module module)
        {
            if (module.ViewTransactions != 0)
            {
                throw new InvalidOperationException("Cannot start or close a Module during View initialization or exit.");
            }
            // 不在B的对象级初始化/退出事务中结束模块，避免恢复到已关闭的owner或重复退出。
            if (module.CreatingModels.Count != 0 || module.CreatingControllers.Count != 0)
            {
                throw new InvalidOperationException("Cannot start or close a Module during object construction.");
            }

            foreach (var controller in module.Controllers.Values)
            {
                if (controller.IsInitializing || controller.IsExiting)
                {
                    throw new InvalidOperationException("Cannot start or close a Module during Controller initialization or exit.");
                }
            }

            foreach (var bucket in module.Models.Values)
            {
                foreach (var model in bucket.Values)
                {
                    if (model.IsInitializing || model.IsExiting)
                    {
                        throw new InvalidOperationException("Cannot start or close a Module during Model initialization or exit.");
                    }
                }
            }
        }

        private static void CloseModule(Module module, bool allowBaseModule, List<IPendingViewRequest> deferredNotifications = null)
        {
            if (!ReferenceEquals(GetModule(module.ModuleName), module))
            {
                return;
            }

            if (module.State == ModuleState.Closing)
            {
                return;
            }

            if (!allowBaseModule && Instance._isShuttingDown)
            {
                throw new InvalidOperationException("Cannot close another Module during Shutdown.");
            }

            if (!allowBaseModule && module.IsBaseModule)
            {
                throw new InvalidOperationException("基础模块不能关闭! 请使用ModuleManager.Shutdown。");
            }

            ValidateClose(module);
            module.State = ModuleState.Closing;
            var errors = new List<Exception>();
            module.ViewLifecycle = null;
            var pendingViews = new List<IPendingViewRequest>(module.PendingViews);
            foreach (var pending in pendingViews)
            {
                try
                {
                    pending.Invalidate();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            // 保存会话身份，钩子内关闭并复用的实例不能被旧快照再次退出。
            var views = ViewManager.Snapshot(module);
            try
            {
                module.Exit();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }

            foreach (var controller in new List<Controller>(module.Controllers.Values))
            {
                try
                {
                    module.RemoveController(controller);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            foreach (var session in views)
            {
                try
                {
                    ViewManager.RemoveView(module, session.Key, session.Value, !session.Key);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            foreach (var model in module.GetModels<Model>())
            {
                try
                {
                    module.RemoveModel(model);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            Instance._modules.Remove(module.ModuleName);
            Instance._baseModules.Remove(module.ModuleName);
            Instance._registrationOrder.RemoveAll(item => ReferenceEquals(item, module));
            if (ReferenceEquals(module, CurrentModule))
            {
                CurrentModule = null;
            }

            if (ReferenceEquals(module, Instance._startupContext))
            {
                Instance._startupContext = null;
            }

            module.Controllers.Clear();
            module.Models.Clear();
            module.Views.Clear();
            module.TypeViews.Clear();
            module.OwnedViews.Clear();
            module.State = ModuleState.Closed;
            if (deferredNotifications != null)
            {
                deferredNotifications.AddRange(pendingViews);
            }
            else
            {
                foreach (var pending in pendingViews)
                {
                    try
                    {
                        pending.PublishInvalidation();
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }

            if (errors.Count != 0)
            {
                throw new AggregateException($"Module close cleanup failed: {module.ModuleName}", errors).Flatten();
            }
        }

        /// <summary>
        /// 显式结束全部已注册模块，普通组先于基础组，各组逆注册顺序。
        /// Starting或对象生命周期事务中拒绝执行，不提供启动取消；清理继续并在结束后汇总错误。
        /// </summary>
        public static void Shutdown()
        {
            if (Instance._isShuttingDown)
            {
                return;
            }

            var order = new List<Module>();
            for (int group = 0; group < 2; group++)
            {
                for (int i = Instance._registrationOrder.Count - 1; i >= 0; i--)
                {
                    var module = Instance._registrationOrder[i];
                    if (!ReferenceEquals(GetModule(module.ModuleName), module))
                    {
                        continue;
                    }

                    if (Instance._baseModules.ContainsKey(module.ModuleName) != (group == 1))
                    {
                        continue;
                    }

                    if (module.State == ModuleState.Closing)
                    {
                        throw new InvalidOperationException("Cannot begin Shutdown during Module close.");
                    }

                    ValidateClose(module);
                    order.Add(module);
                }
            }

            var errors = new List<Exception>();
            Instance._isShuttingDown = true;
            try
            {
                foreach (var module in order)
                {
                    try
                    {
                        CloseModule(module, true);
                    }
                    catch (Exception error)
                    {
                        errors.Add(error);
                    }
                }
            }
            finally
            {
                Instance._isShuttingDown = false;
            }
            if (errors.Count != 0)
            {
                throw new AggregateException("Module Shutdown cleanup failed.", errors).Flatten();
            }
        }

        internal static List<string> ViewResourceProjects(Module owner)
        {
            var projects = new List<string> { owner.ProjectName };
            foreach (var module in Instance._registrationOrder)
            {
                if (Instance._baseModules.TryGetValue(module.ModuleName, out var registered) && ReferenceEquals(registered, module) &&
                    (module.State == ModuleState.Starting || module.State == ModuleState.Running) &&
                    !projects.Contains(module.ProjectName))
                {
                    projects.Add(module.ProjectName);
                }
            }

            return projects;
        }

        private static bool HasRegisteredObjects(Module module)
        {
            if (module.OwnedViews.Count != 0)
            {
                return true;
            }

            if (module.Controllers.Count != 0)
            {
                return true;
            }

            foreach (var bucket in module.Models.Values)
            {
                if (bucket.Count != 0)
                {
                    return true;
                }
            }

            foreach (var bucket in module.Views.Values)
            {
                if (bucket.Count != 0)
                {
                    return true;
                }
            }

            foreach (var bucket in module.TypeViews.Values)
            {
                if (bucket.Count != 0)
                {
                    return true;
                }
            }

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
        /// 请求不池化；启动失败先回滚逻辑所有权再通知，正确清理后可显式重启。
        /// 基础模块同样回滚；身份冲突在建立本次请求前抛出异常，不清理实际拥有者。
        /// </summary>
        /// <param name="module">模块对象</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static StartUpModuleRequest StartUpModule(Module module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (Instance._isShuttingDown)
            {
                throw new InvalidOperationException("Cannot start a Module during Shutdown.");
            }

            var type = module.GetType();
            if (Instance._creatingModules.Contains(type))
            {
                throw new InvalidOperationException($"Module construction is in progress: {module.ModuleName}");
            }

            var registered = GetModule(module.ModuleName);
            if (registered != null && !ReferenceEquals(registered, module))
            {
                throw new InvalidOperationException($"Module already registered: {module.ModuleName}");
            }

            if ((module.State == ModuleState.Starting || module.State == ModuleState.Running) && !ReferenceEquals(registered, module))
            {
                throw new InvalidOperationException($"Module lifecycle has no matching registration: {module.ModuleName}");
            }

            if (module.State == ModuleState.Starting)
            {
                return Instance._startingModules[type];
            }

            if (module.State == ModuleState.Running)
            {
                var completed = new StartUpModuleRequest(module);
                completed.Finish();
                return completed;
            }
            if (module.State != ModuleState.Created && module.State != ModuleState.Closed)
            {
                throw new InvalidOperationException($"Module must be cleaned before restarting: {module.ModuleName} ({module.State})");
            }

            if (module.State == ModuleState.Closed && HasRegisteredObjects(module))
            {
                throw new InvalidOperationException($"Closed Module still owns registered objects: {module.ModuleName}");
            }
            // 不能让启动回滚与尚未结束的B对象事务交叉；拒绝发生在本次注册和请求之前。
            ValidateObjectTransactions(module);
            var request = new StartUpModuleRequest(module, CurrentModule);
            // 发布记录后才执行注册、协程和任何业务回调，支持无yield的同步完成与重入。
            module.State = ModuleState.Starting;
            Instance._startingModules.Add(type, request);
            try
            {
                AddModule(module);
                CurrentModule = module;
                var coroutine = CoroutineStarter.Start(request.StartUp());
                if (coroutine == null && !request.IsDone)
                {
                    request.Finish(new InvalidOperationException("Module startup requires an active coroutine host."));
                }
            }
            catch (Exception error)
            {
                if (!request.IsDone)
                {
                    request.Finish(error);
                }
                else
                {
                    Debug.LogException(error);
                }
            }
            return request;
        }

        internal static void CompleteStartup(StartUpModuleRequest request, Exception error)
        {
            var module = request.Module;
            if (!Instance._startingModules.TryGetValue(module.GetType(), out var pending) || !ReferenceEquals(pending, request))
            {
                return;
            }

            bool restoreCurrent = ReferenceEquals(CurrentModule, module);
            var deferredNotifications = new List<IPendingViewRequest>();
            try
            {
                if (error == null)
                {
                    module.State = ModuleState.Running;
                    CurrentModule = module;
                }
                else if (ReferenceEquals(GetModule(module.ModuleName), module))
                {
                    // 启动事务已展开完毕；只绕过Starting和基础模块禁令，复用D的唯一管线。
                    module.State = ModuleState.StartFailed;
                    try
                    {
                        CloseModule(module, true, deferredNotifications);
                    }
                    catch (Exception cleanupError)
                    {
                        StartUpModuleRequest.AttachCleanupError(error, cleanupError);
                    }
                }
                else
                {
                    module.State = request.InitialState;
                }
            }
            finally
            {
                Instance._startingModules.Remove(module.GetType());
                if (error != null && restoreCurrent && (CurrentModule == null || ReferenceEquals(CurrentModule, module)))
                {
                    CurrentModule = IsCurrentCandidate(request.PreviousCurrentModule) ? request.PreviousCurrentModule : null;
                }

                request.PreviousCurrentModule = null;
                // View通知可重启同一实例；旧启动记录与上下文必须先完成收尾。
                foreach (var pendingView in deferredNotifications)
                {
                    try
                    {
                        pendingView.PublishInvalidation();
                    }
                    catch (Exception notificationError)
                    {
                        Debug.LogException(notificationError);
                    }
                }
            }
        }

        internal static bool MoveStartup(Module module, System.Collections.IEnumerator routine)
        {
            var previous = Instance._startupContext;
            Instance._startupContext = module;
            try
            {
                return routine.MoveNext();
            }
            finally
            {
                Instance._startupContext = IsCurrentCandidate(previous) ? previous : null;
            }
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
            if (Instance._isShuttingDown)
            {
                throw new InvalidOperationException("Cannot start a Module during Shutdown.");
            }

            if (moduleType == null)
            {
                throw new ArgumentNullException(nameof(moduleType));
            }

            if (!typeof(Module).IsAssignableFrom(moduleType) || moduleType.IsAbstract || moduleType.ContainsGenericParameters)
            {
                throw new ArgumentException("A concrete Module type is required.", nameof(moduleType));
            }

            var registered = GetModule(moduleType.FullName);
            if (registered != null)
            {
                if (registered.GetType() != moduleType)
                {
                    throw new InvalidOperationException($"Module name already registered: {moduleType.FullName}");
                }

                return StartUpModule(registered);
            }
            if (!Instance._creatingModules.Add(moduleType))
            {
                throw new InvalidOperationException($"Module construction is in progress: {moduleType.FullName}");
            }

            Module module;
            try
            {
                module = (Module)Activator.CreateInstance(moduleType);
            }
            finally
            {
                Instance._creatingModules.Remove(moduleType);
            }
            return StartUpModule(module);
        }

        /// <summary>
        /// 启动模块(泛型方式)
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns></returns>
        public static StartUpModuleRequest StartUpModule<T>() where T : Module, new()
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
            Type type = AssemblyTool.GetType(moduleTypeFullName);
            return StartUpModule(type);
        }



        /// <summary>
        /// 获取当前使用的模块
        /// </summary>
        /// <returns></returns>
        internal static Module GetCurrentModule()
        {
            if (IsCurrentCandidate(Instance._startupContext))
            {
                return Instance._startupContext;
            }

            if (IsCurrentCandidate(CurrentModule))
            {
                return CurrentModule;
            }

            foreach (var item in AllModules.Values)
            {
                if (IsCurrentCandidate(item) && !AllBaseModules.ContainsKey(item.ModuleName))
                {
                    return item;
                }
            }

            foreach (var item in AllBaseModules.Values)
            {
                if (IsCurrentCandidate(item))
                {
                    return item;
                }
            }

            return null;
        }

        #endregion
    }

}
