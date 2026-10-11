using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YLFramework.Core
{
    /// <summary>
    /// 运行时为 EventSystem 补充输入模块，并以反射方式访问 InputSystem，
    /// 避免框架程序集在未安装 InputSystem 包的工程产生编译依赖。
    /// </summary>
    internal static class InputSystemBridge
    {
        private const string InputSystemAssembly = "Unity.InputSystem";
        private const string InputSystemModuleTypeName = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, " + InputSystemAssembly;
        private const string MouseTypeName = "UnityEngine.InputSystem.Mouse, " + InputSystemAssembly;
        private const string Vector2ControlTypeName = "UnityEngine.InputSystem.Controls.Vector2Control, " + InputSystemAssembly;

        private static bool _inputModuleFallbackWarned;
        private static bool _mouseReflectionWarned;

#if ENABLE_INPUT_SYSTEM
        private static bool _mouseReflectionInitialized;
        private static PropertyInfo _mouseCurrentProperty;
        private static PropertyInfo _mousePositionProperty;
        private static MethodInfo _readValueMethod;
#endif

        /// <summary>
        /// 幂等：确保场景中的 EventSystem 拥有输入模块。
        /// 已存在任意输入模块时不做改动；优先 InputSystem，不可用时回退 Standalone。
        /// </summary>
        public static void EnsureEventSystemInputModule(EventSystem eventSystem)
        {
            if (!eventSystem) throw new ArgumentNullException(nameof(eventSystem));

            if (eventSystem.GetComponent<BaseInputModule>() != null)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            if (TryAddInputSystemModule(eventSystem))
            {
                return;
            }
#endif
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        /// <summary>
        /// 反射读取 InputSystem 的鼠标位置；无设备或读取失败时明确返回 false，不把零坐标当有效输入。
        /// </summary>
        public static bool TryGetMousePosition(out Vector2 position)
        {
            position = default;
#if ENABLE_INPUT_SYSTEM
            if (!EnsureMouseReflection())
            {
                return false;
            }

            try
            {
                var current = _mouseCurrentProperty.GetValue(null);
                if (current == null)
                {
                    return false;
                }

                var positionControl = _mousePositionProperty.GetValue(current);
                if (positionControl == null)
                {
                    return false;
                }

                if (!(_readValueMethod.Invoke(positionControl, null) is Vector2 result)) return false;
                position = result;
                return true;
            }
            catch (Exception e)
            {
                WarnMouseReflectionOnce(e.Message);
                return false;
            }
#else
            return false;
#endif
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(component);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static void WarnInputModuleFallbackOnce(string reason)
        {
            if (_inputModuleFallbackWarned)
            {
                return;
            }

            _inputModuleFallbackWarned = true;
            Debug.LogWarning($"[InputSystemBridge] InputSystem 输入模块不可用（{reason}），回退 StandaloneInputModule。");
        }

        private static void WarnMouseReflectionOnce(string reason)
        {
            if (_mouseReflectionWarned)
            {
                return;
            }

            _mouseReflectionWarned = true;
            Debug.LogWarning($"[InputSystemBridge] 反射读取 InputSystem 鼠标位置失败（{reason}），本次没有有效鼠标输入。");
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryAddInputSystemModule(EventSystem eventSystem)
        {
            var moduleType = Type.GetType(InputSystemModuleTypeName);
            if (moduleType == null)
            {
                WarnInputModuleFallbackOnce("找不到类型 InputSystemUIInputModule");
                return false;
            }

            Component module = null;
            try
            {
                module = eventSystem.gameObject.AddComponent(moduleType);
                var assignDefaultActions = moduleType.GetMethod("AssignDefaultActions", BindingFlags.Public | BindingFlags.Instance);
                if (assignDefaultActions == null)
                {
                    DestroyComponent(module);
                    WarnInputModuleFallbackOnce("找不到方法 AssignDefaultActions");
                    return false;
                }

                assignDefaultActions.Invoke(module, null);
                return true;
            }
            catch (Exception e)
            {
                DestroyComponent(module);
                WarnInputModuleFallbackOnce(e.Message);
                return false;
            }
        }

        private static bool EnsureMouseReflection()
        {
            if (_mouseReflectionInitialized)
            {
                return _mouseCurrentProperty != null && _mousePositionProperty != null && _readValueMethod != null;
            }

            _mouseReflectionInitialized = true;

            var mouseType = Type.GetType(MouseTypeName);
            _mouseCurrentProperty = mouseType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _mousePositionProperty = mouseType?.GetProperty("position", BindingFlags.Public | BindingFlags.Instance);

            var controlType = Type.GetType(Vector2ControlTypeName);
            _readValueMethod = controlType?.GetMethod("ReadValue", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

            if (_mouseCurrentProperty == null || _mousePositionProperty == null || _readValueMethod == null)
            {
                WarnMouseReflectionOnce("反射成员缺失");
                return false;
            }

            return true;
        }
#endif
    }
}
