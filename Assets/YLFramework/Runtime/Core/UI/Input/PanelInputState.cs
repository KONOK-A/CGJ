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
        private readonly Panel _panel;
        private readonly object _session;
        private bool _isApplying;
        private GameObject _pendingSelection;
        private EventSystem _pendingSelectionSystem;
        private readonly Dictionary<Selectable, PanelInputGate> _groups = new Dictionary<Selectable, PanelInputGate>();
        private readonly List<Selectable> _selectables = new List<Selectable>();
        private readonly List<Canvas> _canvases = new List<Canvas>();
        private readonly List<PanelRaycastGate> _gates = new List<PanelRaycastGate>();
        private readonly List<UIBehaviour> _targets = new List<UIBehaviour>();
        private readonly List<PanelInputCapture> _captures = new List<PanelInputCapture>();
        internal bool IsBlocked;
        internal GameObject Selection;
        internal EventSystem SelectionSystem;

        internal PanelInputState(Panel panel)
        {
            this._panel = panel;
            _session = panel.Session;
        }
        private bool HasCurrentSession => _panel && ReferenceEquals(_panel.Session, _session) && !_panel.IsRemoving &&
            _panel.Module != null && _panel.Module.State != ModuleState.Closing;
        internal bool IsCurrent => HasCurrentSession;

        internal static void ValidateHierarchy(Panel owner)
        {
            foreach (var selectable in owner.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.GetComponentInParent<Panel>(true) != owner)
                {
                    continue;
                }

                for (var parent = selectable.transform.parent; parent; parent = parent.parent)
                {
                    if (parent.TryGetComponent<Selectable>(out var ancestor) && ancestor.GetComponentInParent<Panel>(true) != owner)
                    {
                        throw new InvalidOperationException("独立子 Panel 不能位于另一 Panel 的 Selectable 子树中。请将子 Panel 移到该控件之外的同级容器。");
                    }
                }
            }
        }

        internal void Apply(bool blocked)
        {
            bool changed = blocked != IsBlocked;
            IsBlocked = blocked;
            if (!HasCurrentSession || _isApplying)
            {
                return;
            }

            _isApplying = true;
            try
            {
                FinishPendingSelection();
                if (!HasCurrentSession || IsBlocked != blocked)
                {
                    return;
                }

                EnsureGates();
                if (!HasCurrentSession)
                {
                    return;
                }

                if (changed && blocked)
                {
                    _panel.GetComponentsInChildren(true, _captures);
                    foreach (var capture in _captures)
                    {
                        if (capture.GetComponentInParent<Panel>(true) == _panel)
                        {
                            capture.Invalidate();
                        }

                        if (!HasCurrentSession || !IsBlocked)
                        {
                            return;
                        }
                    }
                    // 原生 InputField 的 Deselect/EndEdit 必须在 IsInteractable 仍合法时执行。
                    var system = EventSystem.current;
                    foreach (var pair in _groups)
                    {
                        if (!(pair.Key is InputField input) || !input)
                        {
                            continue;
                        }
                        // OnSelect 的延迟激活不会被普通 DeactivateInputField 清除，不能在门禁后又复活 caret。
                        InputSystemBridge.CancelPendingInputFieldActivation(input);
                        if (input.isFocused && (!system || system.alreadySelecting || system.currentSelectedGameObject != input.gameObject))
                        {
                            input.OnDeselect(new BaseEventData(system));
                        }

                        if (!HasCurrentSession || !IsBlocked)
                        {
                            return;
                        }
                    }
                    if (system && Owns(system.currentSelectedGameObject))
                    {
                        if (system.alreadySelecting)
                        {
                            _pendingSelection = system.currentSelectedGameObject;
                            _pendingSelectionSystem = system;
                            // 保留 EventSystem 的锁和真实 Deselect 通知，物理门禁仍须在本调用内生效。
                        }
                        else
                        {
                            system.SetSelectedGameObject(null);
                        }
                    }
                    if (!HasCurrentSession || !IsBlocked)
                    {
                        return;
                    }

                    foreach (var pair in _groups)
                    {
                        if (pair.Key is InputField input && input)
                        {
                            InputSystemBridge.CancelPendingInputFieldActivation(input);
                        }
                    }
                }
                foreach (var pair in _groups)
                {
                    bool bound = !pair.Value || pair.Value.Apply(this, blocked);
                    if (!HasCurrentSession || IsBlocked != blocked)
                    {
                        return;
                    }

                    if (!bound)
                    {
                        continue;
                    }

                    if (blocked && pair.Key && pair.Key.isActiveAndEnabled && pair.Key.IsInteractable())
                    {
                        throw new InvalidOperationException("实际 UGUI 布局跳过了独立门禁（例如同对象更早的 CanvasGroup.ignoreParentGroups）。请调整控件层级；不会改写业务字段作为回退。");
                    }
                }
            }
            finally
            {
                _isApplying = false;
            }
        }

        private void FinishPendingSelection()
        {
            var system = _pendingSelectionSystem;
            if (!_pendingSelection || !system || EventSystem.current != system || !Owns(_pendingSelection))
            {
                _pendingSelection = null;
                _pendingSelectionSystem = null;
                return;
            }
            if (system.alreadySelecting)
            {
                return;
            }

            var selected = _pendingSelection;
            _pendingSelection = null;
            _pendingSelectionSystem = null;
            if (system.currentSelectedGameObject == selected)
            {
                system.SetSelectedGameObject(null);
            }
        }

        private void EnsureGates()
        {
            ValidateHierarchy(_panel);
            _panel.GetComponentsInChildren(true, _canvases);
            foreach (var canvas in _canvases)
            {
                if (canvas.GetComponentInParent<Panel>(true) != _panel)
                {
                    continue;
                }

                var gate = canvas.GetComponent<PanelRaycastGate>();
                if (!gate)
                {
                    gate = canvas.gameObject.AddComponent<PanelRaycastGate>();
                }

                if (!HasCurrentSession)
                {
                    return;
                }

                gate.Bind(_panel, _session);
                if (!_gates.Contains(gate))
                {
                    _gates.Add(gate);
                }
            }
            _panel.GetComponentsInChildren(true, _targets);
            foreach (var target in _targets)
            {
                if (!target || (!(target is IDragHandler) && !(target is Selectable)) || target.GetComponentInParent<Panel>(true) != _panel)
                {
                    continue;
                }

                if (!target.GetComponent<PanelInputCapture>())
                {
                    target.gameObject.AddComponent<PanelInputCapture>();
                }

                if (target is IDragHandler && !target.GetComponent<PanelDragCapture>())
                {
                    target.gameObject.AddComponent<PanelDragCapture>();
                }

                if (!HasCurrentSession)
                {
                    return;
                }
            }
            _panel.GetComponentsInChildren(true, _selectables);
            foreach (var selectable in _selectables)
            {
                if (selectable.GetComponentInParent<Panel>(true) != _panel)
                {
                    continue;
                }

                _groups.TryGetValue(selectable, out var owned);
                if (!owned)
                {
                    owned = selectable.GetComponent<PanelInputGate>();
                    if (!owned)
                    {
                        owned = selectable.gameObject.AddComponent<PanelInputGate>();
                    }

                    if (!HasCurrentSession)
                    {
                        return;
                    }

                    _groups[selectable] = owned;
                }
                if (!owned.Bind(this, selectable) || !HasCurrentSession)
                {
                    return;
                }
            }
        }

        internal void Restore(bool endSession = false)
        {
            if (endSession && _panel)
            {
                _panel.GetComponentsInChildren(true, _captures);
                foreach (var capture in _captures)
                {
                    if (capture.GetComponentInParent<Panel>(true) == _panel)
                    {
                        capture.Invalidate();
                    }
                }
            }
            foreach (var group in new List<PanelInputGate>(_groups.Values))
            {
                if (group)
                {
                    group.Release(this);
                }
            }

            if (!endSession)
            {
                return;
            }

            _pendingSelection = null;
            _pendingSelectionSystem = null;
            _groups.Clear();
            foreach (var gate in _gates)
            {
                if (gate)
                {
                    gate.Release(_session);
                }
            }

            _gates.Clear();
        }

        internal void RestoreDeparted()
        {
            if (_isApplying)
            {
                return;
            }

            var departed = new List<Selectable>();
            foreach (var pair in _groups)
            {
                if (!pair.Key || pair.Key.GetComponentInParent<Panel>(true) != _panel)
                {
                    departed.Add(pair.Key);
                }
            }

            foreach (var selectable in departed)
            {
                var group = _groups[selectable];
                _groups.Remove(selectable);
                if (group)
                {
                    group.Release(this);
                }
            }
            foreach (var gate in _gates)
            {
                if (gate && gate.GetComponentInParent<Panel>(true) != _panel)
                {
                    gate.Release(_session);
                }
            }
        }

        internal bool Owns(GameObject target) => _panel && target && target.GetComponentInParent<Panel>(true) == _panel;

        internal GameObject FirstSelection()
        {
            if (!_panel || IsBlocked)
            {
                return null;
            }

            _panel.GetComponentsInChildren(false, _selectables);
            foreach (var selectable in _selectables)
            {
                if (Owns(selectable.gameObject) && selectable.isActiveAndEnabled && selectable.IsInteractable())
                {
                    return selectable.gameObject;
                }
            }

            return null;
        }
    }
}
