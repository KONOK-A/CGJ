using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YLFramework.Core
{
    // 只覆盖当前 Panel 会话的输入属性，不停用业务脚本或接管其他根的输入。
    internal sealed class PanelInputState
    {
        private readonly Panel panel;
        private readonly object session;
        private bool applying;
        private readonly Dictionary<Selectable, bool> selectables = new Dictionary<Selectable, bool>();
        private readonly Dictionary<GraphicRaycaster, bool> raycasters = new Dictionary<GraphicRaycaster, bool>();
        private readonly List<Selectable> selectableBuffer = new List<Selectable>();
        private readonly List<GraphicRaycaster> raycasterBuffer = new List<GraphicRaycaster>();
        private readonly Dictionary<UnityEngine.EventSystems.UIBehaviour, bool> dragHandlers = new Dictionary<UnityEngine.EventSystems.UIBehaviour, bool>();
        private readonly List<UnityEngine.EventSystems.UIBehaviour> dragBuffer = new List<UnityEngine.EventSystems.UIBehaviour>();
        private readonly List<PanelInputCapture> captureBuffer = new List<PanelInputCapture>();
        private readonly List<UnityEngine.EventSystems.UIBehaviour> captureTargets = new List<UnityEngine.EventSystems.UIBehaviour>();
        internal bool Blocked;
        internal GameObject Selection;
        internal EventSystem SelectionSystem;

        internal PanelInputState(Panel panel) { this.panel = panel; session = panel.Session; }
        private bool Current => panel && ReferenceEquals(panel.Session, session) && !panel.IsRemoving &&
            panel.Module != null && panel.Module.State != ModuleState.Closing;

        internal void Apply(bool blocked)
        {
            bool changed = blocked != Blocked;
            Blocked = blocked;
            if (!applying && Current) EnsureCaptures();
            if (changed && blocked)
            {
                panel.GetComponentsInChildren(true, captureBuffer);
                foreach (var capture in captureBuffer)
                    if (capture.GetComponentInParent<Panel>(true) == panel) capture.Invalidate();
            }
            if (!blocked) { Restore(); return; }
            if (!Current || applying) return;
            applying = true;
            try
            {
                panel.GetComponentsInChildren(true, selectableBuffer);
                foreach (var selectable in selectableBuffer)
                {
                    if (!selectable || selectable.GetComponentInParent<Panel>(true) != panel) continue;
                    if (!selectables.ContainsKey(selectable)) selectables.Add(selectable, selectable.interactable);
                    var system = EventSystem.current;
                    // 在 native OnSelect 内关闭/复用时，false setter 会试图嵌套 SetSelected 并报错；本轮用策略阻挡，下一轮补齐属性。
                    if (system && system.alreadySelecting && system.currentSelectedGameObject == selectable.gameObject) continue;
                    selectable.interactable = false;
                    // Selectable setter 会同步 Deselect；回调可能结束或替换本会话，也可能取消阻挡。
                    if (!Current || !Blocked) return;
                }
                panel.GetComponentsInChildren(true, raycasterBuffer);
                foreach (var raycaster in raycasterBuffer)
                {
                    if (!raycaster || raycaster.GetComponentInParent<Panel>(true) != panel) continue;
                    if (!raycasters.ContainsKey(raycaster)) raycasters.Add(raycaster, raycaster.enabled);
                    raycaster.enabled = false;
                    if (!Current || !Blocked) return;
                }
                // 标准 UGUI 拖动已有 pointerDrag 捕获，不依赖后续 Raycast；停用输入控件使其取消拖动态。
                // 不停用任意业务 MonoBehaviour；自定义业务拖动仍应遵守 Panel.IsInputBlocked。
                panel.GetComponentsInChildren(true, dragBuffer);
                foreach (var handler in dragBuffer)
                {
                    if (!(handler is IDragHandler) || handler.GetComponentInParent<Panel>(true) != panel) continue;
                    if (!dragHandlers.ContainsKey(handler)) dragHandlers.Add(handler, handler.enabled);
                    handler.enabled = false;
                    if (!Current || !Blocked) return;
                }
            }
            finally { applying = false; }
        }

        private void EnsureCaptures()
        {
            if (applying) return;
            applying = true;
            try
            {
                panel.GetComponentsInChildren(true, captureTargets);
                foreach (var handler in captureTargets)
                {
                    if (!handler || (!(handler is IDragHandler) && !(handler is Selectable)) || handler.GetComponentInParent<Panel>(true) != panel) continue;
                    var capture = handler.GetComponent<PanelInputCapture>();
                    if (!capture) capture = handler.gameObject.AddComponent<PanelInputCapture>();
                    if (handler is IDragHandler && !handler.GetComponent<PanelDragCapture>()) handler.gameObject.AddComponent<PanelDragCapture>();
                    if (handler is IDragHandler && capture.HasInvalidCapture)
                    {
                        if (!dragHandlers.ContainsKey(handler)) dragHandlers.Add(handler, handler.enabled);
                        handler.enabled = false;
                        if (!Current) return;
                    }
                }
            }
            finally { applying = false; }
        }

        internal void Restore(bool endSession = false)
        {
            if (selectables.Count == 0 && raycasters.Count == 0 && dragHandlers.Count == 0) return;
            var selectableValues = new List<KeyValuePair<Selectable, bool>>(selectables);
            var raycasterValues = new List<KeyValuePair<GraphicRaycaster, bool>>(raycasters);
            var dragValues = new List<KeyValuePair<UnityEngine.EventSystems.UIBehaviour, bool>>(dragHandlers);
            selectables.Clear();
            raycasters.Clear();
            dragHandlers.Clear();
            foreach (var entry in selectableValues) if (entry.Key) entry.Key.interactable = entry.Value;
            foreach (var entry in raycasterValues) if (entry.Key) entry.Key.enabled = entry.Value;
            foreach (var entry in dragValues)
            {
                if (!endSession && entry.Key && entry.Key.TryGetComponent<PanelInputCapture>(out var capture) && capture.HasInvalidCapture)
                {
                    dragHandlers[entry.Key] = entry.Value;
                    continue;
                }
                if (entry.Key) entry.Key.enabled = entry.Value;
            }
        }

        internal void RestoreDeparted()
        {
            if (applying) return;
            selectableBuffer.Clear();
            foreach (var entry in selectables)
                if (!entry.Key || entry.Key.GetComponentInParent<Panel>(true) != panel) selectableBuffer.Add(entry.Key);
            foreach (var selectable in selectableBuffer)
            {
                bool original = selectables[selectable];
                selectables.Remove(selectable);
                if (selectable) selectable.interactable = original;
            }
            raycasterBuffer.Clear();
            foreach (var entry in raycasters)
                if (!entry.Key || entry.Key.GetComponentInParent<Panel>(true) != panel) raycasterBuffer.Add(entry.Key);
            foreach (var raycaster in raycasterBuffer)
            {
                bool original = raycasters[raycaster];
                raycasters.Remove(raycaster);
                if (raycaster) raycaster.enabled = original;
            }
            dragBuffer.Clear();
            foreach (var entry in dragHandlers)
                if (!entry.Key || entry.Key.GetComponentInParent<Panel>(true) != panel) dragBuffer.Add(entry.Key);
            foreach (var handler in dragBuffer)
            {
                bool original = dragHandlers[handler];
                dragHandlers.Remove(handler);
                if (handler) handler.enabled = original;
            }
        }

        internal bool Owns(GameObject target)
        {
            if (!panel || !target) return false;
            for (var parent = target.transform; parent; parent = parent.parent)
            {
                var owner = parent.GetComponent<Panel>();
                if (owner == panel) return true;
                if (owner && owner.IsMyselfAddCanvas && owner.Session != null) return false;
            }
            return false;
        }

        internal GameObject FirstSelection()
        {
            if (!panel || Blocked) return null;
            panel.GetComponentsInChildren(false, selectableBuffer);
            foreach (var selectable in selectableBuffer)
                if (selectable.isActiveAndEnabled && selectable.IsInteractable()) return selectable.gameObject;
            return null;
        }
    }
}
