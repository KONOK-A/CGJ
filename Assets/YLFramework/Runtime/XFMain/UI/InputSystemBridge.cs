using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using YLFramework.Core.Singleton;

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
        private static readonly FieldInfo ActiveSystems = typeof(EventSystem).GetField("m_EventSystems", BindingFlags.NonPublic | BindingFlags.Static);

        // UGUI 没有公开 active 系统枚举 API。仅只读现有小集合，避免自建系统服务时每帧全场景扫描。
        internal static EventSystem FindExternalSystem(EventSystem owned)
        {
            if (ActiveSystems == null)
                throw new MissingFieldException(typeof(EventSystem).FullName, "m_EventSystems");
            var systems = (List<EventSystem>)ActiveSystems.GetValue(null);
            foreach (var candidate in systems)
                if (candidate && candidate != owned && candidate.isActiveAndEnabled) return candidate;
            return null;
        }

        internal static void DisableOwnedSystem(GameObject owned)
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var module in owned.GetComponents<BaseInputModule>())
            {
                var type = module.GetType();
                if (type.FullName != "UnityEngine.InputSystem.UI.InputSystemUIInputModule" || !module.isActiveAndEnabled) continue;
                var asset = type.GetProperty("actionsAsset").GetValue(module) as UnityEngine.Object;
                var systems = (List<EventSystem>)ActiveSystems.GetValue(null);
                bool shared = false;
                foreach (var system in systems)
                    if (system && system.gameObject != owned)
                        foreach (var other in system.GetComponents<BaseInputModule>())
                            if (other.isActiveAndEnabled && other.GetType() == type &&
                                (UnityEngine.Object)type.GetProperty("actionsAsset").GetValue(other) == asset) shared = true;
                if (!shared) continue;
                // 1.14 的 UnassignActions 无条件 Dispose static defaultActions，即使另一 active 模块仍引用它。
                // 先完成自身 PointerExit 回调，再临时摘下默认资源所有权；仅模块 OnDisable 期间保护共享资产。
                var defaultsField = type.GetField("defaultActions", BindingFlags.Static | BindingFlags.NonPublic);
                var resetPointers = type.GetMethod("ResetPointers", BindingFlags.Instance | BindingFlags.NonPublic);
                if (defaultsField == null || resetPointers == null)
                    throw new MissingMemberException(type.FullName, "defaultActions/ResetPointers");
                resetPointers.Invoke(module, null);
                var defaults = defaultsField.GetValue(null);
                if (defaults == null) continue;
                defaultsField.SetValue(null, null);
                try { module.enabled = false; }
                finally
                {
                    if (defaultsField.GetValue(null) == null) defaultsField.SetValue(null, defaults);
                    else Debug.LogError("[InputSystemBridge] Default UI actions changed during owned module shutdown; retained the new owner's defaults.");
                }
            }
#endif
            owned.SetActive(false);
        }

        internal static object GetPointerButton(PointerEventData data)
        {
#if ENABLE_INPUT_SYSTEM
            var device = data.GetType().GetProperty("device")?.GetValue(data);
            if (device == null) return null;
            var buttonName = data.button == PointerEventData.InputButton.Right ? "rightButton" :
                data.button == PointerEventData.InputButton.Middle ? "middleButton" : "leftButton";
            var button = device.GetType().GetProperty(buttonName)?.GetValue(device);
            if (button != null) return button;
            button = device.GetType().GetProperty("tip")?.GetValue(device);
            if (button != null) return button;
            var touches = device.GetType().GetProperty("touches")?.GetValue(device) as IEnumerable;
            var touchId = data.GetType().GetProperty("touchId")?.GetValue(data);
            if (touches != null && touchId != null)
                foreach (var touch in touches)
                {
                    var id = touch.GetType().GetProperty("touchId").GetValue(touch);
                    if (Equals(id.GetType().GetMethod("ReadValue").Invoke(id, null), touchId))
                        return touch.GetType().GetProperty("press").GetValue(touch);
                }
#endif
            return null;
        }

        internal static bool PointerStillPressed(BaseInputModule module, object buttonControl, PointerEventData data, PointerEventData.InputButton button)
        {
            if (buttonControl != null)
            {
                var device = buttonControl.GetType().GetProperty("device").GetValue(buttonControl);
                if (!(bool)device.GetType().GetProperty("added").GetValue(device)) return false;
                return (bool)buttonControl.GetType().GetProperty("isPressed").GetValue(buttonControl);
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (module is StandaloneInputModule)
            {
                if (data.pointerId < 0) return module.input.GetMouseButton((int)button);
                for (int i = 0; i < module.input.touchCount; i++)
                {
                    var touch = module.input.GetTouch(i);
                    if (touch.fingerId == data.pointerId) return touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                }
                return false;
            }
#endif
            // 自定义输入模块没有公开捕获状态时，等待其真实 Up/Drag 取消，不臆造释放或清全局输入。
            return true;
        }

        static InputSystemBridge() => SingletonSessionState.RegisterSessionReset(() =>
        {
            _inputModuleFallbackWarned = _mouseReflectionWarned = false;
#if ENABLE_INPUT_SYSTEM
            _mouseReflectionInitialized = false;
            _mouseCurrentProperty = _mousePositionProperty = null;
            _readValueMethod = null;
#endif
        });

#if ENABLE_INPUT_SYSTEM
        private static bool _mouseReflectionInitialized;
        private static PropertyInfo _mouseCurrentProperty;
        private static PropertyInfo _mousePositionProperty;
        private static MethodInfo _readValueMethod;
#endif

        /// <summary>
        /// 幂等：确保场景中的 EventSystem 拥有输入模块。
        /// 仅供自建系统配置；已有模块不替换。Both 优先 Input System，只有 Legacy 可用时才允许回退。
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
#if ENABLE_LEGACY_INPUT_MANAGER
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
#else
            throw new InvalidOperationException("Input System-only UI requires an available InputSystemUIInputModule; no Legacy fallback is permitted.");
#endif
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
            Debug.LogWarning($"[InputSystemBridge] InputSystem 输入模块不可用（{reason}）。仅启用 Legacy 时允许回退 StandaloneInputModule。");
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

                // 新模块 OnEnable 已配置默认 actions；只在 inactive 对象未初始化时补齐，避免重复分配引用。
                var asset = moduleType.GetProperty("actionsAsset", BindingFlags.Public | BindingFlags.Instance);
                if (asset == null) throw new MissingMemberException(moduleType.FullName, "actionsAsset");
                if (asset.GetValue(module) == null) assignDefaultActions.Invoke(module, null);
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
