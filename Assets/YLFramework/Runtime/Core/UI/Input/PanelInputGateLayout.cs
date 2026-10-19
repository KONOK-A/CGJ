using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YLFramework.Core
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class PanelInputGateLayout : UIBehaviour, ILayoutElement, ILayoutGroup, ILayoutIgnorer
    {
        private const DrivenTransformProperties RectProperties = DrivenTransformProperties.Anchors |
            DrivenTransformProperties.AnchoredPosition3D | DrivenTransformProperties.SizeDelta;
        private static readonly PropertyInfo DrivenProperties = typeof(RectTransform).GetProperty("drivenProperties",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly List<Component> ignorers = new List<Component>();
        private readonly List<Component> elements = new List<Component>();
        private RectTransform target, slot, frame;
        private PanelInputGateLayout coordinates;
        private PanelInputGate binding;
        private Transform initialParent;
        private Transform synchronizationParent;
        private ulong synchronizationBinding;
        private bool initialized, passThrough, wasAttached, synchronizing, retiring, previousIgnore = true;

        internal void Initialize(RectTransform target, RectTransform frame)
        {
            if (DrivenProperties == null || DrivenProperties.PropertyType != typeof(DrivenTransformProperties) ||
                DrivenProperties.GetGetMethod(true) == null)
                throw new InvalidOperationException("无法只读访问 RectTransform.drivenProperties；请核对 Unity 版本及 IL2CPP accessor 保留。");
            this.target = target;
            binding = target.GetComponent<PanelInputGate>();
            this.frame = frame;
            slot = (RectTransform)transform;
            coordinates = frame.GetComponent<PanelInputGateLayout>();
            if (!coordinates || !coordinates.passThrough || coordinates.target != target)
                throw new InvalidOperationException("Panel 输入门禁需要先初始化 frame 的布局穿透组件。");
            initialParent = target.parent;
            passThrough = false;
            wasAttached = false;
            initialized = true;
            Synchronize();
        }

        internal void InitializePassThrough(RectTransform target)
        {
            this.target = target;
            binding = target.GetComponent<PanelInputGate>();
            frame = (RectTransform)transform;
            slot = frame.parent as RectTransform;
            initialParent = target.parent;
            passThrough = true;
            wasAttached = false;
            initialized = true;
            Synchronize();
        }

        private bool Valid()
        {
            if (!initialized || !target || !slot || !frame || frame.parent != slot) return false;
            if (synchronizing && (!binding || binding.BindingVersion != synchronizationBinding || slot.parent != synchronizationParent)) return false;
            if (target.parent == frame) { wasAttached = true; return true; }
            // 首次挂接前只准备自有坐标；挂接后绝不追随外部重挂的业务对象。
            return !wasAttached && target.parent == initialParent;
        }

        private bool Measurable => !passThrough && Valid() && target.parent == frame && target.gameObject.activeInHierarchy;
        public float minWidth => Measurable ? LayoutUtility.GetMinWidth(target) : 0;
        public float preferredWidth => Measurable ? LayoutUtility.GetPreferredWidth(target) : 0;
        public float flexibleWidth => Measurable ? LayoutUtility.GetFlexibleWidth(target) : 0;
        public float minHeight => Measurable ? LayoutUtility.GetMinHeight(target) : 0;
        public float preferredHeight => Measurable ? LayoutUtility.GetPreferredHeight(target) : 0;
        public float flexibleHeight => Measurable ? LayoutUtility.GetFlexibleHeight(target) : 0;

        public int layoutPriority
        {
            get
            {
                if (!Measurable) return 0;
                target.GetComponents(typeof(ILayoutElement), elements);
                int priority = int.MinValue;
                bool found = false;
                foreach (var element in elements)
                {
                    if (element is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                    priority = Math.Max(priority, ((ILayoutElement)element).layoutPriority);
                    found = true;
                }
                return found ? priority : 0;
            }
        }

        public bool ignoreLayout
        {
            get
            {
                if (!Measurable) return true;
                target.GetComponents(typeof(ILayoutIgnorer), ignorers);
                foreach (var ignorer in ignorers)
                    if (!((ILayoutIgnorer)ignorer).ignoreLayout) return false;
                return ignorers.Count != 0;
            }
        }

        internal void Synchronize() => Synchronize(false);
        internal void RetireMount() => retiring = true;

        private void Synchronize(bool prepareHorizontal)
        {
            if (retiring || synchronizing || !Valid()) return;
            synchronizationBinding = binding ? binding.BindingVersion : 0;
            synchronizationParent = slot.parent;
            synchronizing = true;
            bool changed = false;
            try
            {
                if (passThrough)
                {
                    var parent = slot.parent as RectTransform;
                    var size = parent ? parent.rect.size : Vector2.zero;
                    var pivot = parent ? parent.pivot : new Vector2(.5f, .5f);
                    var scale = slot.localScale;
                    var inverse = new Vector3(Inverse(scale.x), Inverse(scale.y), Inverse(scale.z));
                    if (!frame.anchorMin.Equals(Vector2.zero)) { frame.anchorMin = Vector2.zero; changed = true; }
                    if (!Valid()) return;
                    if (!frame.anchorMax.Equals(Vector2.zero)) { frame.anchorMax = Vector2.zero; changed = true; }
                    if (!Valid()) return;
                    if (!frame.pivot.Equals(pivot)) { frame.pivot = pivot; changed = true; }
                    if (!Valid()) return;
                    if (!frame.sizeDelta.Equals(size)) { frame.sizeDelta = size; changed = true; }
                    if (!Valid()) return;
                    if (!frame.localRotation.Equals(Quaternion.identity)) { frame.localRotation = Quaternion.identity; changed = true; }
                    if (!Valid()) return;
                    if (!frame.localScale.Equals(inverse)) { frame.localScale = inverse; changed = true; }
                    if (!Valid()) return;
                    var position = -Vector3.Scale(slot.localPosition, inverse);
                    if (!frame.localPosition.Equals(position)) { frame.localPosition = position; changed = true; }
                    return;
                }

                bool ignored = ignoreLayout;
                if (previousIgnore != ignored) { previousIgnore = ignored; changed = true; }
                var layout = ParentLayout();
                if (layout && !ignored)
                {
                    // 横算在原父清除上轮 tracker 之前执行，不能沿用旧驱动 flags。
                    var preserve = prepareHorizontal ? DrivenTransformProperties.None : ReadDriven(layout);
                    changed |= CopyRect(target, slot, RectProperties & ~preserve);
                    if (!Valid()) return;
                    if (!slot.pivot.Equals(target.pivot)) { slot.pivot = target.pivot; changed = true; }
                    if (!Valid()) return;
                    if (!slot.localScale.Equals(target.localScale)) { slot.localScale = target.localScale; changed = true; }
                }
                else
                {
                    var parent = slot.parent as RectTransform;
                    var pivot = parent ? parent.pivot : new Vector2(.5f, .5f);
                    if (!slot.anchorMin.Equals(Vector2.zero)) { slot.anchorMin = Vector2.zero; changed = true; }
                    if (!Valid()) return;
                    if (!slot.anchorMax.Equals(Vector2.one)) { slot.anchorMax = Vector2.one; changed = true; }
                    if (!Valid()) return;
                    if (!slot.pivot.Equals(pivot)) { slot.pivot = pivot; changed = true; }
                    if (!Valid()) return;
                    if (!slot.sizeDelta.Equals(Vector2.zero)) { slot.sizeDelta = Vector2.zero; changed = true; }
                    if (!Valid()) return;
                    if (!slot.anchoredPosition3D.Equals(Vector3.zero)) { slot.anchoredPosition3D = Vector3.zero; changed = true; }
                    if (!Valid()) return;
                    if (!slot.localScale.Equals(Vector3.one)) { slot.localScale = Vector3.one; changed = true; }
                }
                if (!Valid()) return;
                if (!slot.localRotation.Equals(Quaternion.identity)) { slot.localRotation = Quaternion.identity; changed = true; }
                if (Valid()) coordinates.Synchronize();
            }
            finally
            {
                synchronizing = false;
                if (changed) SetDirty();
            }
        }

        private static float Inverse(float value)
        {
            if (value == 0) return 0;
            float inverse = 1f / value;
            return float.IsInfinity(inverse) || float.IsNaN(inverse) ? 0 : inverse;
        }

        private LayoutGroup ParentLayout()
        {
            var layout = slot.parent ? slot.parent.GetComponent<LayoutGroup>() : null;
            return layout && layout.isActiveAndEnabled ? layout : null;
        }

        private DrivenTransformProperties ReadDriven(LayoutGroup layout) => slot.drivenByObject == layout
            ? (DrivenTransformProperties)DrivenProperties.GetValue(slot) : DrivenTransformProperties.None;

        public void CalculateLayoutInputHorizontal() => Synchronize(true);
        public void CalculateLayoutInputVertical() => Synchronize(false);
        public void SetLayoutHorizontal() => ApplyLayout(false);
        public void SetLayoutVertical() => ApplyLayout(true);

        private void ApplyLayout(bool vertical)
        {
            if (passThrough) { Synchronize(); return; }
            if (synchronizing || !Valid() || target.parent != frame) return;
            var layout = ParentLayout();
            if (!layout || ignoreLayout) { Synchronize(); return; }
            var driven = ReadDriven(layout);
            if (layout is GridLayoutGroup)
                driven &= DrivenTransformProperties.Anchors | DrivenTransformProperties.SizeDelta |
                    (vertical ? DrivenTransformProperties.AnchoredPosition : DrivenTransformProperties.None);
            else if (layout is HorizontalOrVerticalLayoutGroup)
                driven &= DrivenTransformProperties.Anchors | (vertical
                    ? DrivenTransformProperties.AnchoredPositionY | DrivenTransformProperties.SizeDeltaY
                    : DrivenTransformProperties.AnchoredPositionX | DrivenTransformProperties.SizeDeltaX);
            // Grid 纵控确实重写两轴；线性布局纵控不能重放横向值覆盖已经执行的 CSF。
            synchronizationBinding = binding ? binding.BindingVersion : 0;
            synchronizationParent = slot.parent;
            synchronizing = true;
            try
            {
                coordinates.Synchronize();
                if (!Valid()) return;
                CopyRect(slot, target, driven);
                if (Valid()) coordinates.Synchronize();
            }
            finally { synchronizing = false; }
        }

        private bool CopyRect(RectTransform source, RectTransform destination, DrivenTransformProperties properties)
        {
            bool changed = false;
            var value = Select(destination.anchorMin, source.anchorMin, properties,
                DrivenTransformProperties.AnchorMinX, DrivenTransformProperties.AnchorMinY);
            if (!destination.anchorMin.Equals(value)) { destination.anchorMin = value; changed = true; }
            if (!Valid()) return changed;
            value = Select(destination.anchorMax, source.anchorMax, properties,
                DrivenTransformProperties.AnchorMaxX, DrivenTransformProperties.AnchorMaxY);
            if (!destination.anchorMax.Equals(value)) { destination.anchorMax = value; changed = true; }
            if (!Valid()) return changed;
            value = Select(destination.sizeDelta, source.sizeDelta, properties,
                DrivenTransformProperties.SizeDeltaX, DrivenTransformProperties.SizeDeltaY);
            if (!destination.sizeDelta.Equals(value)) { destination.sizeDelta = value; changed = true; }
            if (!Valid()) return changed;
            value = Select(destination.anchoredPosition, source.anchoredPosition, properties,
                DrivenTransformProperties.AnchoredPositionX, DrivenTransformProperties.AnchoredPositionY);
            if (!destination.anchoredPosition.Equals(value)) { destination.anchoredPosition = value; changed = true; }
            if (!Valid()) return changed;
            if ((properties & DrivenTransformProperties.AnchoredPositionZ) != 0)
            {
                var position = destination.localPosition;
                float z = source.localPosition.z;
                if (!position.z.Equals(z)) { position.z = z; destination.localPosition = position; changed = true; }
            }
            return changed;
        }

        private static Vector2 Select(Vector2 current, Vector2 input, DrivenTransformProperties properties,
            DrivenTransformProperties x, DrivenTransformProperties y)
        {
            if ((properties & x) != 0) current.x = input.x;
            if ((properties & y) != 0) current.y = input.y;
            return current;
        }

        private void SetDirty()
        {
            var rect = passThrough ? frame : slot;
            if (rect && !CanvasUpdateRegistry.IsRebuildingLayout()) LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        protected override void OnEnable() { base.OnEnable(); if (retiring) Retire(); else { Synchronize(); SetDirty(); } }
        protected override void OnDisable() { SetDirty(); base.OnDisable(); }
        protected override void OnTransformParentChanged() { base.OnTransformParentChanged(); Synchronize(); SetDirty(); }
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (synchronizing) return;
            Synchronize();
            SetDirty();
        }
        private void OnTransformChildrenChanged() { Synchronize(); SetDirty(); }
        private void LateUpdate() { if (retiring) Retire(); else Synchronize(); }

        private void Retire()
        {
            if (passThrough || !slot) return;
            var children = new List<Transform>();
            if (frame)
                for (int i = 0; i < frame.childCount; i++) children.Add(frame.GetChild(i));
            foreach (var child in children)
                if (slot && child && child.parent == frame) child.SetParent(slot.parent, false);
            if (!slot) return;
            children.Clear();
            for (int i = 0; i < slot.childCount; i++)
                if (slot.GetChild(i) != frame) children.Add(slot.GetChild(i));
            foreach (var child in children)
                if (slot && child && child.parent == slot) child.SetParent(slot.parent, true);
            if (!slot) return;
            // 父级回调可向旧壳再次放入业务节点；本轮只处理快照，下帧重试，不能连带销毁。
            if ((frame && frame.childCount != 0) || slot.childCount > (frame && frame.parent == slot ? 1 : 0)) return;
            Destroy(slot.gameObject);
            slot = null;
        }
    }
}
