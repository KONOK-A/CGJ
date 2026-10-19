using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YLFramework.Core
{
    // 门禁只写自有组件，不保存或恢复业务的 interactable/enabled/raycastTarget。
    internal sealed class PanelInputState
    {
        private readonly Panel panel;
        private readonly object session;
        private bool applying;
        private GameObject pendingSelection;
        private EventSystem pendingSelectionSystem;
        private readonly Dictionary<Selectable, PanelInputGate> groups = new Dictionary<Selectable, PanelInputGate>();
        private readonly List<Selectable> selectables = new List<Selectable>();
        private readonly List<Canvas> canvases = new List<Canvas>();
        private readonly List<PanelRaycastGate> gates = new List<PanelRaycastGate>();
        private readonly List<UIBehaviour> targets = new List<UIBehaviour>();
        private readonly List<PanelInputCapture> captures = new List<PanelInputCapture>();
        internal bool Blocked;
        internal GameObject Selection;
        internal EventSystem SelectionSystem;

        internal PanelInputState(Panel panel) { this.panel = panel; session = panel.Session; }
        private bool Current => panel && ReferenceEquals(panel.Session, session) && !panel.IsRemoving &&
            panel.Module != null && panel.Module.State != ModuleState.Closing;
        internal bool IsCurrent => Current;

        internal static void ValidateHierarchy(Panel owner)
        {
            foreach (var selectable in owner.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.GetComponentInParent<Panel>(true) != owner) continue;
                for (var parent = selectable.transform.parent; parent; parent = parent.parent)
                    if (parent.TryGetComponent<Selectable>(out var ancestor) && ancestor.GetComponentInParent<Panel>(true) != owner)
                        throw new InvalidOperationException("独立子 Panel 不能位于另一 Panel 的 Selectable 子树中。请将子 Panel 移到该控件之外的同级容器。");
            }
        }

        internal void Apply(bool blocked)
        {
            bool changed = blocked != Blocked;
            Blocked = blocked;
            if (!Current || applying) return;
            applying = true;
            try
            {
                FinishPendingSelection();
                if (!Current || Blocked != blocked) return;
                EnsureGates();
                if (!Current) return;
                if (changed && blocked)
                {
                    panel.GetComponentsInChildren(true, captures);
                    foreach (var capture in captures)
                    {
                        if (capture.GetComponentInParent<Panel>(true) == panel) capture.Invalidate();
                        if (!Current || !Blocked) return;
                    }
                    // 原生 InputField 的 Deselect/EndEdit 必须在 IsInteractable 仍合法时执行。
                    var system = EventSystem.current;
                    foreach (var pair in groups)
                    {
                        if (!(pair.Key is InputField input) || !input) continue;
                        // OnSelect 的延迟激活不会被普通 DeactivateInputField 清除，不能在门禁后又复活 caret。
                        InputSystemBridge.CancelPendingInputFieldActivation(input);
                        if (input.isFocused && (!system || system.alreadySelecting || system.currentSelectedGameObject != input.gameObject))
                            input.OnDeselect(new BaseEventData(system));
                        if (!Current || !Blocked) return;
                    }
                    if (system && Owns(system.currentSelectedGameObject))
                    {
                        if (system.alreadySelecting)
                        {
                            pendingSelection = system.currentSelectedGameObject;
                            pendingSelectionSystem = system;
                            // 保留 EventSystem 的锁和真实 Deselect 通知，物理门禁仍须在本调用内生效。
                        }
                        else system.SetSelectedGameObject(null);
                    }
                    if (!Current || !Blocked) return;
                    foreach (var pair in groups)
                        if (pair.Key is InputField input && input) InputSystemBridge.CancelPendingInputFieldActivation(input);
                }
                foreach (var pair in groups)
                {
                    bool bound = !pair.Value || pair.Value.Apply(this, blocked);
                    if (!Current || Blocked != blocked) return;
                    if (!bound) continue;
                    if (blocked && pair.Key && pair.Key.isActiveAndEnabled && pair.Key.IsInteractable())
                        throw new InvalidOperationException("实际 UGUI 布局跳过了独立门禁（例如同对象更早的 CanvasGroup.ignoreParentGroups）。请调整控件层级；不会改写业务字段作为回退。");
                }
            }
            finally { applying = false; }
        }

        private void FinishPendingSelection()
        {
            var system = pendingSelectionSystem;
            if (!pendingSelection || !system || EventSystem.current != system || !Owns(pendingSelection))
            { pendingSelection = null; pendingSelectionSystem = null; return; }
            if (system.alreadySelecting) return;
            var selected = pendingSelection;
            pendingSelection = null;
            pendingSelectionSystem = null;
            if (system.currentSelectedGameObject == selected) system.SetSelectedGameObject(null);
        }

        private void EnsureGates()
        {
            ValidateHierarchy(panel);
            panel.GetComponentsInChildren(true, canvases);
            foreach (var canvas in canvases)
            {
                if (canvas.GetComponentInParent<Panel>(true) != panel) continue;
                var gate = canvas.GetComponent<PanelRaycastGate>();
                if (!gate) gate = canvas.gameObject.AddComponent<PanelRaycastGate>();
                if (!Current) return;
                gate.Bind(panel, session);
                if (!gates.Contains(gate)) gates.Add(gate);
            }
            panel.GetComponentsInChildren(true, targets);
            foreach (var target in targets)
            {
                if (!target || (!(target is IDragHandler) && !(target is Selectable)) || target.GetComponentInParent<Panel>(true) != panel) continue;
                if (!target.GetComponent<PanelInputCapture>()) target.gameObject.AddComponent<PanelInputCapture>();
                if (target is IDragHandler && !target.GetComponent<PanelDragCapture>()) target.gameObject.AddComponent<PanelDragCapture>();
                if (!Current) return;
            }
            panel.GetComponentsInChildren(true, selectables);
            foreach (var selectable in selectables)
            {
                if (selectable.GetComponentInParent<Panel>(true) != panel) continue;
                groups.TryGetValue(selectable, out var owned);
                if (!owned)
                {
                    owned = selectable.GetComponent<PanelInputGate>();
                    if (!owned) owned = selectable.gameObject.AddComponent<PanelInputGate>();
                    if (!Current) return;
                    groups[selectable] = owned;
                }
                if (!owned.Bind(this, selectable) || !Current) return;
            }
        }

        internal void Restore(bool endSession = false)
        {
            if (endSession && panel)
            {
                panel.GetComponentsInChildren(true, captures);
                foreach (var capture in captures)
                    if (capture.GetComponentInParent<Panel>(true) == panel) capture.Invalidate();
            }
            foreach (var group in new List<PanelInputGate>(groups.Values)) if (group) group.Release(this);
            if (!endSession) return;
            pendingSelection = null;
            pendingSelectionSystem = null;
            groups.Clear();
            foreach (var gate in gates) if (gate) gate.Release(session);
            gates.Clear();
        }

        internal void RestoreDeparted()
        {
            if (applying) return;
            var departed = new List<Selectable>();
            foreach (var pair in groups)
                if (!pair.Key || pair.Key.GetComponentInParent<Panel>(true) != panel) departed.Add(pair.Key);
            foreach (var selectable in departed)
            {
                var group = groups[selectable];
                groups.Remove(selectable);
                if (group) group.Release(this);
            }
            foreach (var gate in gates)
                if (gate && gate.GetComponentInParent<Panel>(true) != panel) gate.Release(session);
        }

        internal bool Owns(GameObject target) => panel && target && target.GetComponentInParent<Panel>(true) == panel;

        internal GameObject FirstSelection()
        {
            if (!panel || Blocked) return null;
            panel.GetComponentsInChildren(false, selectables);
            foreach (var selectable in selectables)
                if (Owns(selectable.gameObject) && selectable.isActiveAndEnabled && selectable.IsInteractable()) return selectable.gameObject;
            return null;
        }
    }
}
