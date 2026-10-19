using System;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class PanelInputGate : MonoBehaviour
    {
        private Selectable target;
        private PanelInputState owner;
        private CanvasGroup group;
        private RectTransform slot;
        private RectTransform frame;
        private PanelInputGateLayout layout;
        private bool installingSelf;
        private bool creatingMount;
        internal ulong BindingVersion { get; private set; }

        internal bool Bind(PanelInputState state, Selectable selectable)
        {
            target = selectable;
            if (!ReferenceEquals(owner, state)) { owner = state; BindingVersion++; }
            if (creatingMount) return false;
            if (slot && target.transform.parent != frame) ReleaseMount();
            if (!group)
            {
                if (installingSelf) group = GetComponent<CanvasGroup>();
                else if (GetComponent<CanvasGroup>()) CreateMount();
                else
                {
                    // AddComponent 的原生回调可关闭并重开同一实例；新会话识别在建的自有组，不再 Add。
                    installingSelf = true;
                    try { group = gameObject.AddComponent<CanvasGroup>(); }
                    finally { installingSelf = false; }
                }
            }
            if (!Bound(state) || creatingMount) return false;
            if (!group && !installingSelf) throw new InvalidOperationException("无法创建框架自有 CanvasGroup 门禁。");
            layout?.Synchronize();
            return Bound(state);
        }

        private bool Bound(PanelInputState state) => ReferenceEquals(owner, state) && state.IsCurrent;

        internal bool Apply(PanelInputState state, bool blocked)
        {
            if (!Bound(state) || creatingMount) return false;
            layout?.Synchronize();
            if (!Bound(state) || state.Blocked != blocked) return false;
            if (group) group.interactable = !blocked;
            return Bound(state);
        }

        internal void Release(PanelInputState state)
        {
            if (!ReferenceEquals(owner, state)) return;
            owner = null;
            BindingVersion++;
            if (group) group.interactable = true;
            // 资源随物理实例保留；会话结束不延迟 Destroy 后再于同帧争用唯一组件槽。
        }

        private void CreateMount()
        {
            var initial = owner;
            creatingMount = true;
            try
            {
                var rect = (RectTransform)target.transform;
                var host = new GameObject("YL.PanelInputGate", typeof(RectTransform));
                host.SetActive(false);
                host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                slot = (RectTransform)host.transform;
                group = host.AddComponent<CanvasGroup>();
                var coordinates = new GameObject("Coordinates", typeof(RectTransform));
                coordinates.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                frame = (RectTransform)coordinates.transform;
                frame.SetParent(slot, false);
                coordinates.AddComponent<PanelInputGateLayout>().InitializePassThrough(rect);
                layout = host.AddComponent<PanelInputGateLayout>();
                layout.Initialize(rect, frame);
                // 先备齐自有资源再接入业务层级；原生父级回调重开时只接管绑定，不重复安装。
                slot.SetParent(rect.parent, false);
                if (!target || owner == null || !owner.IsCurrent) return;
                if (slot.parent != rect.parent) slot.SetParent(rect.parent, false);
                slot.SetSiblingIndex(rect.GetSiblingIndex());
                layout.Synchronize();
                host.SetActive(true);
                if (!target || owner == null || !owner.IsCurrent) return;
                rect.SetParent(frame, false);
                if (target && target.transform.parent == frame) layout.Synchronize();
            }
            finally
            {
                creatingMount = false;
                if (owner != null && owner.IsCurrent && group) group.interactable = !owner.Blocked;
                if (owner != null && owner.IsCurrent && !ReferenceEquals(owner, initial)) PanelManager.LateUpdate();
            }
        }

        internal static void Reparent(Panel panel, Transform parent)
        {
            var gate = panel.GetComponent<PanelInputGate>();
            if (gate) gate.BindingVersion++;
            if (!gate || !gate.slot || panel.transform.parent != gate.frame)
            { panel.transform.SetParent(parent, false); return; }
            gate.slot.SetParent(parent, false);
            if (gate && gate.layout && panel && panel.transform.parent == gate.frame) gate.layout.Synchronize();
        }

        private void OnTransformParentChanged()
        {
            if (owner == null || (frame && transform.parent == frame)) return;
            PanelManager.LateUpdate();
        }

        private void ReleaseMount()
        {
            var previous = layout;
            var previousGroup = group;
            slot = null;
            frame = null;
            layout = null;
            group = null;
            if (previousGroup) previousGroup.interactable = true;
            if (previous) previous.RetireMount();
        }

        private void OnDestroy()
        {
            owner = null;
            BindingVersion++;
            if (group) group.interactable = true;
            // OnDestroy 中业务 Transform 仍可能 truthy 但已禁止重挂；让存活的自有壳在下一帧清理。
            if (layout) layout.RetireMount();
        }
    }
}
