using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YLFramework.Events;
using YLFramework.Core.Singleton;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using UpdateManager = YLFramework.Foundation.UpdateManager;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core
{
    public partial class PanelManager
    {
        private static void RefreshEventSystem()
        {
            _instance?.RefreshInput(true);
        }

        private void ReleaseOwnedInput()
        {
            var owned = _eventSystem;
            _eventSystem = null;
            if (!owned) return;
            InputSystemBridge.DisableOwnedSystem(owned);
            if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
            else UnityEngine.Object.DestroyImmediate(owned);
        }

        private void RefreshInput(bool discover = false)
        {
            if (released || !ApplicationTool.isPlaying) return;
            inputRoots.RemoveWhere(root => !root);
            bool needed = false;
            foreach (var root in inputRoots) if (root.isActiveAndEnabled) { needed = true; break; }
            if (!needed) { ReleaseOwnedInput(); SyncLateSubscription(); return; }
            var current = EventSystem.current;
            if (current && !current.isActiveAndEnabled) current = null;
            if (_eventSystem && current && current.gameObject == _eventSystem)
            {
                var external = InputSystemBridge.FindExternalSystem(current);
                if (external) current = external;
            }
            if (discover || !current)
            {
                var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int active = 0;
                bool inactive = false;
                foreach (var system in systems)
                {
                    if (!system.isActiveAndEnabled) { inactive = true; continue; }
                    active++;
                    if ((!current || current.gameObject == _eventSystem) && system.gameObject != _eventSystem) current = system;
                }
                if (!inputConfigurationWarned && (active > 1 || (!current && inactive)))
                {
                    inputConfigurationWarned = true;
                    Debug.LogWarning("[PanelManager] 多个 active 或仅 inactive EventSystem。保留外部对象；优先借用 active 系统，inactive 系统不会被激活。");
                }
            }
            if (current && current.gameObject != _eventSystem)
            {
                ReleaseOwnedInput();
                // 借用系统不补齐、不切换其模块，也不更改其配置与对象寿命。
                if (!inputConfigurationWarned)
                {
                    bool usable = false;
                    foreach (var module in current.GetComponents<BaseInputModule>())
                    {
#if !ENABLE_LEGACY_INPUT_MANAGER
                        if (module is StandaloneInputModule) continue;
#endif
                        if (module.isActiveAndEnabled && module.IsModuleSupported()) { usable = true; break; }
                    }
                    if (!usable)
                    {
                        inputConfigurationWarned = true;
                        Debug.LogWarning("[PanelManager] 借用的 EventSystem 没有可用输入模块，请由其 owner 配置；框架不会替换外部模块。");
                    }
                }
                return;
            }
            if (_eventSystem) return;
            var prefab = Resources.Load<GameObject>("YLFramework/Core/EventSystem");
            if (!prefab) throw new InvalidOperationException("Core EventSystem prefab is unavailable.");
            var created = UnityEngine.Object.Instantiate(prefab);
            try
            {
                created.name = "EventSystem(Created By YLFramework.Core)";
                InputSystemBridge.EnsureEventSystemInputModule(created.GetComponent<EventSystem>());
                UnityEngine.Object.DontDestroyOnLoad(created);
                _eventSystem = created;
                SyncLateSubscription();
            }
            catch
            {
                created.SetActive(false);
                UnityEngine.Object.Destroy(created);
                throw;
            }
        }

        private void RefreshPanelInput(List<PanelState> snapshot)
        {
            var system = EventSystem.current;
            if (pendingSelectionRelease != null && system && !system.alreadySelecting)
            {
                var ended = pendingSelectionRelease;
                var target = releasedSelection;
                var targetSystem = releasedSelectionSystem;
                pendingSelectionRelease = null;
                releasedSelection = null;
                releasedSelectionSystem = null;
                if (system == targetSystem && system.currentSelectedGameObject == target &&
                    (!ended.Panel || ended.Panel.Session == null || ReferenceEquals(ended.Panel.Session, ended.Session)))
                    system.SetSelectedGameObject(null);
            }
            // 移出原窗口的组件先交还输入属性，再由新窗口决定覆盖；不能由枚举顺序决定新快照。
            foreach (var state in snapshot) if (Current(state)) state.Input.RestoreDeparted();
            foreach (var state in snapshot)
            {
                if (!Current(state)) continue;
                var input = state.Input;
                if (state.Blocked && !input.Blocked && system && input.Owns(system.currentSelectedGameObject))
                {
                    input.Selection = system.currentSelectedGameObject;
                    input.SelectionSystem = system;
                }
                input.Apply(state.Blocked);
            }
            if (!system || !system.isActiveAndEnabled || system.alreadySelecting) return;
            bool cleared = false;
            foreach (var state in snapshot)
                if (Current(state) && state.Blocked && state.Input.Owns(system.currentSelectedGameObject))
                {
                    system.SetSelectedGameObject(null);
                    cleared = true;
                    break;
                }
            // Select/Deselect 可关闭、销毁或重开窗口。只允许仍匹配的会话恢复，且不覆盖外部或新选择。
            if (released || EventSystem.current != system || system.currentSelectedGameObject) return;
            foreach (var state in snapshot)
            {
                if (!Active(state) || state.Blocked || GetFocusedPanel(state.Root) != state.Panel) continue;
                var input = state.Input;
                var selection = input.Selection;
                if (selection && input.SelectionSystem == system && input.Owns(selection) && selection.activeInHierarchy &&
                    selection.TryGetComponent<Selectable>(out var selectable) && selectable.isActiveAndEnabled && selectable.IsInteractable())
                {
                    input.Selection = null;
                    system.SetSelectedGameObject(selection);
                    return;
                }
                if ((cleared || state.Modal) && state.Modal)
                {
                    selection = input.FirstSelection();
                    if (selection) { system.SetSelectedGameObject(selection); return; }
                }
            }
        }

        internal static void ReleaseView(View view)
        {
            var manager = _instance;
            if (manager == null || !(view is Panel panel)) return;
            if (manager.states.TryGetValue(panel, out var state))
            {
                state.Input.Restore(true);
                manager.states.Remove(panel);
                var system = EventSystem.current;
                if (system && state.Input.Owns(system.currentSelectedGameObject))
                {
                    if (system.alreadySelecting)
                    {
                        manager.pendingSelectionRelease = state;
                        manager.releasedSelection = system.currentSelectedGameObject;
                        manager.releasedSelectionSystem = system;
                    }
                    else system.SetSelectedGameObject(null);
                }
            }
            manager.Reconcile();
        }

        internal static bool IsVisible(Panel panel) => _instance != null && panel &&
            _instance.states.TryGetValue(panel, out var state) && _instance.Active(state) && state.Visible;
        internal static bool IsInputBlocked(Panel panel) => !IsVisible(panel) || _instance.states[panel].Blocked;

        private static Panel Query(Canvas root, bool modal, bool focus, UIType? type = null)
        {
            var manager = _instance;
            if (manager == null || (!root && !type.HasValue)) return null;
            PanelState top = null;
            foreach (var state in manager.states.Values)
            {
                if (!manager.Active(state) || !state.Panel.IsMyselfAddCanvas || state.Panel.PolicyType == PanelType.Overlay ||
                    !state.Visible || (root && state.Root != root) || (type.HasValue && state.Panel.UIType != type.Value) ||
                    (modal && !state.Modal) || (focus && state.Blocked)) continue;
                if (top == null || Compare(state, top) > 0) top = state;
            }
            return top?.Panel;
        }

        /// <summary>指定实际根的普通窗口 Top，不创建 Manager 或宿主。</summary>
        public static Panel GetTopPanel(Canvas root) => Query(root, false, false);
        /// <summary>兼容寿命分类查询；多根业务应使用明确根重载。</summary>
        public static Panel GetTopPanel(UIType type) => Query(null, false, false, type);
        public static Panel GetModalPanel(Canvas root) => Query(root, true, false);
        public static Panel GetFocusedPanel(Canvas root) => Query(root, false, true);

        internal static Panel GetLayerModal(Canvas root, int layer)
        {
            if (_instance == null) return null;
            PanelState top = null;
            foreach (var state in _instance.states.Values)
                if (_instance.Active(state) && state.Visible && state.Root == root && state.Modal &&
                    state.Panel.WindowLayer == layer && (top == null || Compare(state, top) > 0)) top = state;
            return top?.Panel;
        }

        public static void GetAllPanels(UIType type, List<Panel> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            var manager = _instance;
            if (manager == null) return;
            foreach (var state in manager.states.Values)
                if (manager.Current(state) && state.Panel.UIType == type && state.Panel.IsMyselfAddCanvas) results.Add(state.Panel);
        }

    }
}
