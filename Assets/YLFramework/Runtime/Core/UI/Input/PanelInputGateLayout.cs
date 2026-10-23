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
        private static readonly PropertyInfo _drivenProperties = typeof(RectTransform).GetProperty("drivenProperties",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private readonly List<Component> _ignorers = new List<Component>();
        private readonly List<Component> _elements = new List<Component>();
        private RectTransform _target;
        private RectTransform _slot;
        private RectTransform _frame;
        private PanelInputGateLayout _coordinates;
        private PanelInputGate _binding;
        private Transform _initialParent;
        private Transform _synchronizationParent;
        private ulong _synchronizationBinding;
        private bool _isInitialized;
        private bool _isPassThrough;
        private bool _wasAttached;
        private bool _isSynchronizing;
        private bool _isRetiring;
        private bool _previousIgnore = true;

        internal void Initialize(RectTransform target, RectTransform frame)
        {
            if (_drivenProperties == null || _drivenProperties.PropertyType != typeof(DrivenTransformProperties) ||
                _drivenProperties.GetGetMethod(true) == null)
            {
                throw new InvalidOperationException("无法只读访问 RectTransform.drivenProperties；请核对 Unity 版本及 IL2CPP accessor 保留。");
            }

            _target = target;
            _binding = target.GetComponent<PanelInputGate>();
            _frame = frame;
            _slot = (RectTransform)transform;
            _coordinates = frame.GetComponent<PanelInputGateLayout>();
            if (!_coordinates || !_coordinates._isPassThrough || _coordinates._target != target)
            {
                throw new InvalidOperationException("Panel 输入门禁需要先初始化 frame 的布局穿透组件。");
            }

            _initialParent = target.parent;
            _isPassThrough = false;
            _wasAttached = false;
            _isInitialized = true;
            Synchronize();
        }

        internal void InitializePassThrough(RectTransform target)
        {
            this._target = target;
            _binding = target.GetComponent<PanelInputGate>();
            _frame = (RectTransform)transform;
            _slot = _frame.parent as RectTransform;
            _initialParent = target.parent;
            _isPassThrough = true;
            _wasAttached = false;
            _isInitialized = true;
            Synchronize();
        }

        private bool IsValid()
        {
            if (!_isInitialized || !_target || !_slot || !_frame || _frame.parent != _slot)
            {
                return false;
            }

            if (_isSynchronizing && (!_binding || _binding.BindingVersion != _synchronizationBinding || _slot.parent != _synchronizationParent))
            {
                return false;
            }

            if (_target.parent == _frame)
            {
                _wasAttached = true;
                return true;
            }
            // 首次挂接前只准备自有坐标；挂接后绝不追随外部重挂的业务对象。
            return !_wasAttached && _target.parent == _initialParent;
        }

        private bool IsMeasurable => !_isPassThrough && IsValid() && _target.parent == _frame && _target.gameObject.activeInHierarchy;
        public float minWidth => IsMeasurable ? LayoutUtility.GetMinWidth(_target) : 0;
        public float preferredWidth => IsMeasurable ? LayoutUtility.GetPreferredWidth(_target) : 0;
        public float flexibleWidth => IsMeasurable ? LayoutUtility.GetFlexibleWidth(_target) : 0;
        public float minHeight => IsMeasurable ? LayoutUtility.GetMinHeight(_target) : 0;
        public float preferredHeight => IsMeasurable ? LayoutUtility.GetPreferredHeight(_target) : 0;
        public float flexibleHeight => IsMeasurable ? LayoutUtility.GetFlexibleHeight(_target) : 0;

        public int layoutPriority
        {
            get
            {
                if (!IsMeasurable)
                {
                    return 0;
                }

                _target.GetComponents(typeof(ILayoutElement), _elements);
                int priority = int.MinValue;
                bool found = false;
                foreach (var element in _elements)
                {
                    if (element is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    {
                        continue;
                    }

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
                if (!IsMeasurable)
                {
                    return true;
                }

                _target.GetComponents(typeof(ILayoutIgnorer), _ignorers);
                foreach (var ignorer in _ignorers)
                {
                    if (!((ILayoutIgnorer)ignorer).ignoreLayout)
                    {
                        return false;
                    }
                }

                return _ignorers.Count != 0;
            }
        }

        internal void Synchronize() => Synchronize(false);
        internal void RetireMount() => _isRetiring = true;

        private void Synchronize(bool prepareHorizontal)
        {
            if (_isRetiring || _isSynchronizing || !IsValid())
            {
                return;
            }

            _synchronizationBinding = _binding ? _binding.BindingVersion : 0;
            _synchronizationParent = _slot.parent;
            _isSynchronizing = true;
            bool changed = false;
            try
            {
                if (_isPassThrough)
                {
                    var parent = _slot.parent as RectTransform;
                    var size = parent ? parent.rect.size : Vector2.zero;
                    var pivot = parent ? parent.pivot : new Vector2(.5f, .5f);
                    var scale = _slot.localScale;
                    var inverse = new Vector3(Inverse(scale.x), Inverse(scale.y), Inverse(scale.z));
                    if (!_frame.anchorMin.Equals(Vector2.zero))
                    {
                        _frame.anchorMin = Vector2.zero;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_frame.anchorMax.Equals(Vector2.zero))
                    {
                        _frame.anchorMax = Vector2.zero;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_frame.pivot.Equals(pivot))
                    {
                        _frame.pivot = pivot;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_frame.sizeDelta.Equals(size))
                    {
                        _frame.sizeDelta = size;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_frame.localRotation.Equals(Quaternion.identity))
                    {
                        _frame.localRotation = Quaternion.identity;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_frame.localScale.Equals(inverse))
                    {
                        _frame.localScale = inverse;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    var position = -Vector3.Scale(_slot.localPosition, inverse);
                    if (!_frame.localPosition.Equals(position))
                    {
                        _frame.localPosition = position;
                        changed = true;
                    }
                    return;
                }

                bool ignored = ignoreLayout;
                if (_previousIgnore != ignored)
                {
                    _previousIgnore = ignored;
                    changed = true;
                }
                var layout = ParentLayout();
                if (layout && !ignored)
                {
                    // 横算在原父清除上轮 tracker 之前执行，不能沿用旧驱动 flags。
                    var preserve = prepareHorizontal ? DrivenTransformProperties.None : ReadDriven(layout);
                    changed |= CopyRect(_target, _slot, RectProperties & ~preserve);
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_slot.pivot.Equals(_target.pivot))
                    {
                        _slot.pivot = _target.pivot;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_slot.localScale.Equals(_target.localScale))
                    {
                        _slot.localScale = _target.localScale;
                        changed = true;
                    }
                }
                else
                {
                    var parent = _slot.parent as RectTransform;
                    var pivot = parent ? parent.pivot : new Vector2(.5f, .5f);
                    if (!_slot.anchorMin.Equals(Vector2.zero))
                    {
                        _slot.anchorMin = Vector2.zero;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_slot.anchorMax.Equals(Vector2.one))
                    {
                        _slot.anchorMax = Vector2.one;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_slot.pivot.Equals(pivot))
                    {
                        _slot.pivot = pivot;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_slot.sizeDelta.Equals(Vector2.zero))
                    {
                        _slot.sizeDelta = Vector2.zero;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_slot.anchoredPosition3D.Equals(Vector3.zero))
                    {
                        _slot.anchoredPosition3D = Vector3.zero;
                        changed = true;
                    }
                    if (!IsValid())
                    {
                        return;
                    }

                    if (!_slot.localScale.Equals(Vector3.one))
                    {
                        _slot.localScale = Vector3.one;
                        changed = true;
                    }
                }
                if (!IsValid())
                {
                    return;
                }

                if (!_slot.localRotation.Equals(Quaternion.identity))
                {
                    _slot.localRotation = Quaternion.identity;
                    changed = true;
                }
                if (IsValid())
                {
                    _coordinates.Synchronize();
                }
            }
            finally
            {
                _isSynchronizing = false;
                if (changed)
                {
                    SetDirty();
                }
            }
        }

        private static float Inverse(float value)
        {
            if (value == 0)
            {
                return 0;
            }

            float inverse = 1f / value;
            return float.IsInfinity(inverse) || float.IsNaN(inverse) ? 0 : inverse;
        }

        private LayoutGroup ParentLayout()
        {
            var layout = _slot.parent ? _slot.parent.GetComponent<LayoutGroup>() : null;
            return layout && layout.isActiveAndEnabled ? layout : null;
        }

        private DrivenTransformProperties ReadDriven(LayoutGroup layout) => _slot.drivenByObject == layout
            ? (DrivenTransformProperties)_drivenProperties.GetValue(_slot) : DrivenTransformProperties.None;

        public void CalculateLayoutInputHorizontal() => Synchronize(true);
        public void CalculateLayoutInputVertical() => Synchronize(false);
        public void SetLayoutHorizontal() => ApplyLayout(false);
        public void SetLayoutVertical() => ApplyLayout(true);

        private void ApplyLayout(bool vertical)
        {
            if (_isPassThrough)
            {
                Synchronize();
                return;
            }
            if (_isSynchronizing || !IsValid() || _target.parent != _frame)
            {
                return;
            }

            var layout = ParentLayout();
            if (!layout || ignoreLayout)
            {
                Synchronize();
                return;
            }
            var driven = ReadDriven(layout);
            if (layout is GridLayoutGroup)
            {
                driven &= DrivenTransformProperties.Anchors | DrivenTransformProperties.SizeDelta |
                    (vertical ? DrivenTransformProperties.AnchoredPosition : DrivenTransformProperties.None);
            }
            else if (layout is HorizontalOrVerticalLayoutGroup)
            {
                driven &= DrivenTransformProperties.Anchors | (vertical
                    ? DrivenTransformProperties.AnchoredPositionY | DrivenTransformProperties.SizeDeltaY
                    : DrivenTransformProperties.AnchoredPositionX | DrivenTransformProperties.SizeDeltaX);
            }
            // Grid 纵控确实重写两轴；线性布局纵控不能重放横向值覆盖已经执行的 CSF。
            _synchronizationBinding = _binding ? _binding.BindingVersion : 0;
            _synchronizationParent = _slot.parent;
            _isSynchronizing = true;
            try
            {
                _coordinates.Synchronize();
                if (!IsValid())
                {
                    return;
                }

                CopyRect(_slot, _target, driven);
                if (IsValid())
                {
                    _coordinates.Synchronize();
                }
            }
            finally
            {
                _isSynchronizing = false;
            }
        }

        private bool CopyRect(RectTransform source, RectTransform destination, DrivenTransformProperties properties)
        {
            bool changed = false;
            var value = Select(destination.anchorMin, source.anchorMin, properties,
                DrivenTransformProperties.AnchorMinX, DrivenTransformProperties.AnchorMinY);
            if (!destination.anchorMin.Equals(value))
            {
                destination.anchorMin = value;
                changed = true;
            }
            if (!IsValid())
            {
                return changed;
            }

            value = Select(destination.anchorMax, source.anchorMax, properties,
                DrivenTransformProperties.AnchorMaxX, DrivenTransformProperties.AnchorMaxY);
            if (!destination.anchorMax.Equals(value))
            {
                destination.anchorMax = value;
                changed = true;
            }
            if (!IsValid())
            {
                return changed;
            }

            value = Select(destination.sizeDelta, source.sizeDelta, properties,
                DrivenTransformProperties.SizeDeltaX, DrivenTransformProperties.SizeDeltaY);
            if (!destination.sizeDelta.Equals(value))
            {
                destination.sizeDelta = value;
                changed = true;
            }
            if (!IsValid())
            {
                return changed;
            }

            value = Select(destination.anchoredPosition, source.anchoredPosition, properties,
                DrivenTransformProperties.AnchoredPositionX, DrivenTransformProperties.AnchoredPositionY);
            if (!destination.anchoredPosition.Equals(value))
            {
                destination.anchoredPosition = value;
                changed = true;
            }
            if (!IsValid())
            {
                return changed;
            }

            if ((properties & DrivenTransformProperties.AnchoredPositionZ) != 0)
            {
                var position = destination.localPosition;
                float z = source.localPosition.z;
                if (!position.z.Equals(z))
                {
                    position.z = z;
                    destination.localPosition = position;
                    changed = true;
                }
            }
            return changed;
        }

        private static Vector2 Select(Vector2 current, Vector2 input, DrivenTransformProperties properties,
            DrivenTransformProperties x, DrivenTransformProperties y)
        {
            if ((properties & x) != 0)
            {
                current.x = input.x;
            }

            if ((properties & y) != 0)
            {
                current.y = input.y;
            }

            return current;
        }

        private void SetDirty()
        {
            var rect = _isPassThrough ? _frame : _slot;
            if (rect && !CanvasUpdateRegistry.IsRebuildingLayout())
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_isRetiring)
            {
                Retire();
            }
            else
            {
                Synchronize();
                SetDirty();
            }
        }
        protected override void OnDisable()
        {
            SetDirty();
            base.OnDisable();
        }
        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            Synchronize();
            SetDirty();
        }
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (_isSynchronizing)
            {
                return;
            }

            Synchronize();
            SetDirty();
        }
        private void OnTransformChildrenChanged()
        {
            Synchronize();
            SetDirty();
        }
        private void LateUpdate()
        {
            if (_isRetiring)
            {
                Retire();
            }
            else
            {
                Synchronize();
            }
        }

        private void Retire()
        {
            if (_isPassThrough || !_slot)
            {
                return;
            }

            var children = new List<Transform>();
            if (_frame)
            {
                for (int i = 0; i < _frame.childCount; i++)
                {
                    children.Add(_frame.GetChild(i));
                }
            }

            foreach (var child in children)
            {
                if (_slot && child && child.parent == _frame)
                {
                    child.SetParent(_slot.parent, false);
                }
            }

            if (!_slot)
            {
                return;
            }

            children.Clear();
            for (int i = 0; i < _slot.childCount; i++)
            {
                if (_slot.GetChild(i) != _frame)
                {
                    children.Add(_slot.GetChild(i));
                }
            }

            foreach (var child in children)
            {
                if (_slot && child && child.parent == _slot)
                {
                    child.SetParent(_slot.parent, true);
                }
            }

            if (!_slot)
            {
                return;
            }
            // 父级回调可向旧壳再次放入业务节点；本轮只处理快照，下帧重试，不能连带销毁。
            if ((_frame && _frame.childCount != 0) || _slot.childCount > (_frame && _frame.parent == _slot ? 1 : 0))
            {
                return;
            }

            Destroy(_slot.gameObject);
            _slot = null;
        }
    }
}
