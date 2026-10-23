using System;
using System.Collections.Generic;

namespace YLFramework.Core
{
    internal class ControllerManager
    {
        internal static void AddController(Module module, Controller controller)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            module.EnsureCanAddObjects();
            Type type = controller.GetType();
            if (controller.Module != null)
            {
                if (!ReferenceEquals(controller.Module, module))
                {
                    throw new InvalidOperationException("Controller is already owned by another Module.");
                }

                if (controller.IsInitializing || controller.IsExiting)
                {
                    throw new InvalidOperationException("Cannot register a Controller during its lifecycle callback.");
                }

                if (ReferenceEquals(GetController(module, type), controller))
                {
                    return;
                }

                throw new InvalidOperationException("Controller owner and registration do not match.");
            }
            if (module.Controllers.ContainsKey(type) || module.CreatingControllers.Contains(type))
            {
                throw new InvalidOperationException($"Controller {type.FullName} is already registered or being constructed.");
            }

            module.Controllers.Add(type, controller);
            controller.Module = module;
            controller.IsInitializing = true;
            try
            {
                controller.OnInit();
                controller.IsInitializing = false;
            }
            catch (Exception initializationError)
            {
                controller.IsInitializing = false;
                try
                {
                    ExitController(module, controller);
                }
                catch (Exception cleanupError)
                {
                    StartUpModuleRequest.AttachCleanupError(initializationError, cleanupError);
                }
                throw;
            }
        }

        internal static void RemoveController(Module module, Type controllerType)
        {
            RemoveController(module, GetController(module, controllerType));
        }

        internal static void RemoveController(Module module, Controller controller)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (controller == null || !ReferenceEquals(controller.Module, module))
            {
                return;
            }

            if (!ReferenceEquals(GetController(module, controller.GetType()), controller))
            {
                return;
            }

            if (controller.IsExiting)
            {
                return;
            }

            if (controller.IsInitializing)
            {
                throw new InvalidOperationException("Cannot remove a Controller during initialization.");
            }

            ExitController(module, controller);
        }

        private static void ExitController(Module module, Controller controller)
        {
            controller.IsExiting = true;
            try
            {
                controller.OnExit();
            }
            finally
            {
                Type type = controller.GetType();
                if (module.Controllers.TryGetValue(type, out var registered) && ReferenceEquals(registered, controller))
                {
                    module.Controllers.Remove(type);
                }

                controller.Module = null;
                controller.IsExiting = false;
            }
        }

        internal static Controller GetController(Module module, Type controllerType)
        {
            ValidateControllerType(controllerType);
            Controller controller;
            if (module.Controllers.TryGetValue(controllerType, out controller))
            {
                return controller;
            }

            return null;
        }

        internal static Controller LoadController(Module module, Type controllerType)
        {

            Controller controller = GetController(module, controllerType);
            if (controller != null)
            {
                if (controller.IsExiting)
                {
                    throw new InvalidOperationException("Cannot load a Controller during exit.");
                }

                return controller;
            }

            return CreateController(module, controllerType);
        }

        internal static T LoadController<T>(Module module) where T : Controller
        {
            return LoadController(module, typeof(T)) as T;
        }


        internal static void ClearController(Module module)
        {
            List<Controller> removes = new List<Controller>(module.Controllers.Values);

            foreach (var item in removes)
            {
                module.RemoveController(item);
            }
        }


        internal static Controller CreateController(Module module, Type controllerType)
        {
            ValidateControllerType(controllerType);
            module.EnsureCanAddObjects();
            if (module.Controllers.ContainsKey(controllerType))
            {
                throw new InvalidOperationException($"Controller {controllerType.FullName} is already registered.");
            }

            if (controllerType.IsAbstract || controllerType.ContainsGenericParameters || controllerType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new ArgumentException("Controller type must be concrete and have a public parameterless constructor.", nameof(controllerType));
            }

            if (!module.CreatingControllers.Add(controllerType))
            {
                throw new InvalidOperationException("Recursive Controller construction is not allowed.");
            }

            Controller controller;
            try
            {
                controller = (Controller)Activator.CreateInstance(controllerType);
            }
            finally
            {
                module.CreatingControllers.Remove(controllerType);
            }
            AddController(module, controller);

            return controller;
        }


        internal static T CreateController<T>(Module module) where T : Controller
        {
            return CreateController(module, typeof(T)) as T;
        }

        private static void ValidateControllerType(Type controllerType)
        {
            if (controllerType == null)
            {
                throw new ArgumentNullException(nameof(controllerType));
            }

            if (!typeof(Controller).IsAssignableFrom(controllerType))
            {
                throw new ArgumentException("Type must inherit from Controller.", nameof(controllerType));
            }
        }

    }

}
