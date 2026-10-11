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
        private static EventSystem system;
        private static PointerEventData pointer;
        private static readonly List<RaycastResult> results = new List<RaycastResult>();
        private static bool querying;

        static UIPointer()
        {
            SingletonSessionState.RegisterSessionReset(() => { system = null; pointer = null; results.Clear(); });
        }

        public static bool CheckPositionOnUI(Vector2 position)
        {
            if (querying) throw new InvalidOperationException("UI pointer query cannot reenter a raycast.");
            var current = EventSystem.current;
            if (!current) { system = null; pointer = null; return false; }
            if (system != current) { system = current; pointer = new PointerEventData(current); }
            querying = true;
            try
            {
                pointer.position = position;
                current.RaycastAll(pointer, results);
                foreach (var result in results)
                    if (result.module is GraphicRaycaster) return true;
                return false;
            }
            finally { results.Clear(); querying = false; }
        }

        public static bool CheckMouseOnUI()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return CheckPositionOnUI(Input.mousePosition);
#elif ENABLE_INPUT_SYSTEM
            return InputSystemBridge.TryGetMousePosition(out var position) && CheckPositionOnUI(position);
#else
            return false;
#endif
        }
    }
}
