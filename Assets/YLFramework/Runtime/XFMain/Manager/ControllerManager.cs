// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ControllerManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core
{
    internal class ControllerManager
    {
        internal static void AddController(Module module, Controller controller)
        {

            Type type = controller.GetType();

            if (!module.controllers.ContainsKey(type))
            {
                controller.Module = module;
                controller.OnInit();
                module.controllers.Add(type, controller);
            }
            else
            {
                Debug.LogWarningFormat("controller name:{0} 已经存在,请勿重复添加!", type);
            }
        }

        internal static void RemoveController(Module module, Type controllerType)
        {
            if (!module.controllers.ContainsKey(controllerType))
                return;

            Controller controller = module.controllers[controllerType];

            //if (controller.Type == ControllerType.CannotRemove)
            //    return;

            controller.OnExit();
            module.controllers.Remove(controllerType); 
        }

        internal static Controller GetController(Module module, Type controllerType, bool search_from_base_modules)
        {

            Controller controller;
            if (module.controllers.TryGetValue(controllerType, out controller))
            {
                return controller;
            }

            if (search_from_base_modules) 
            {
                // 尝试 从基础其他模块中( Controller 只需要有一个 不需要有多个 )
                foreach (var item in ModuleManager.AllModules.Values)
                {

                    if (item == module) continue;

                    if (item.controllers.ContainsKey(controllerType))
                        return item.controllers[controllerType];
                }
            }
            


            return null;
        }

        internal static Controller LoadController(Module module, Type controllerType, bool load_from_base_modules)
        {

            Controller controller = GetController(module, controllerType, load_from_base_modules);
            if (controller != null)
            {
                return controller;
            }
              
            return CreateController(module, controllerType);
        }

        internal static T LoadController<T>(Module module,bool load_from_base_modules) where T : Controller
        {
            T controller = GetController(module,typeof(T),load_from_base_modules) as T;
            if (controller != null) return controller;

            // 创建一个业务逻辑
            controller = Activator.CreateInstance<T>();
            controller.Module = module;
            AddController(module, controller) ;
            return controller;
        }


        internal static void ClearController(Module module)
        {
            List<Type> removes = new List<Type>();

            foreach (var item in module.controllers.Keys)
            {
                removes.Add(item);
            }

            foreach (var item in removes)
            {  
                module.RemoveController(item);
            }

            module.controllers.Clear();
        }


        internal static Controller CreateController( Module module,Type controllerType) 
        { 
            if (controllerType == null)
            {
                Debug.LogErrorFormat("加载Controller:{0}失败!未查询到对应类型", controllerType);
                return null;
            }

            // 创建一个业务逻辑
            Controller controller = Activator.CreateInstance(controllerType) as Controller;

            if (controller == null)
            {
                Debug.LogErrorFormat("创建Controller:{0}失败! 请检查类名是否正确!如果有命名空间请加上!", controllerType);
                return null;
            }
              
            AddController(module, controller);
             
            return controller;
        }


        internal static T CreateController<T>(Module module ) where T : Controller
        { 
            // 创建一个业务逻辑
            T controller = Activator.CreateInstance<T>();

            if (controller == null)
            {
                Debug.LogErrorFormat("创建Controller:{0}失败! 请检查类名是否正确!如果有命名空间请加上!", typeof(T).FullName);
                return null;
            }

            AddController(module, controller);

            return controller;
        }

    }

}