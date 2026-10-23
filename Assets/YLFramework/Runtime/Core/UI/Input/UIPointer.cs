using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YLFramework.Core.Singleton;

namespace YLFramework.Core
{
    /// <summary>主线程 UGUI 命中查询。不创建 EventSystem 或 Manager，模态遮罩作为真实 UI 命中，不按对象名称跳过。</summary>
    public static class UIPointer
    {
        private static EventSystem _system;
        private static PointerEventData _pointer;
        private static readonly List<RaycastResult> _results = new List<RaycastResult>();
        private static bool _isQuerying;

        static UIPointer()
        {
            SingletonSessionState.RegisterSessionReset(() =>
            {
                _system = null;
                _pointer = null;
                _results.Clear();
            });
        }

        public static bool CheckPositionOnUI(Vector2 position)
        {
            if (_isQuerying)
            {
                throw new InvalidOperationException("UI pointer query cannot reenter a raycast.");
            }

            var current = EventSystem.current;
            if (!current || !current.isActiveAndEnabled)
            {
                _system = null;
                _pointer = null;
                return false;
            }
            if (_system != current)
            {
                _system = current;
                _pointer = new PointerEventData(current);
            }
            _isQuerying = true;
            try
            {
                _pointer.Reset();
                _pointer.position = position;
                current.RaycastAll(_pointer, _results);
                foreach (var result in _results)
                {
                    if (result.module is GraphicRaycaster)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _results.Clear();
                _isQuerying = false;
            }
        }

        public static bool CheckMouseOnUI()
        {
#if ENABLE_INPUT_SYSTEM
            var current = EventSystem.current;
            var module = current ? current.currentInputModule : null;
            if (module && module.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule")
            {
                return InputSystemBridge.TryGetMousePosition(out var position) && CheckPositionOnUI(position);
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return CheckPositionOnUI(Input.mousePosition);
#elif ENABLE_INPUT_SYSTEM
            return InputSystemBridge.TryGetMousePosition(out var fallbackPosition) && CheckPositionOnUI(fallbackPosition);
#else
            return false;
#endif
        }
    }
}
