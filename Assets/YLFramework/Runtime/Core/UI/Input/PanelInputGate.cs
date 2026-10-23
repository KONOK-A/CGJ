using System;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class PanelInputGate : MonoBehaviour
    {
        private Selectable _target;
        private PanelInputState _owner;
        private CanvasGroup _group;
        private RectTransform _slot;
        private RectTransform _frame;
        private PanelInputGateLayout _layout;
        private bool _isInstallingSelf;
        private bool _isCreatingMount;
        internal ulong BindingVersion
        {
            get; private set;
        }

        internal bool Bind(PanelInputState state, Selectable selectable)
        {
            _target = selectable;
            if (!ReferenceEquals(_owner, state))
            {
                _owner = state;
                BindingVersion++;
            }
            if (_isCreatingMount)
            {
                return false;
            }

            if (_slot && _target.transform.parent != _frame)
            {
                ReleaseMount();
            }

            if (!_group)
            {
                if (_isInstallingSelf)
                {
                    _group = GetComponent<CanvasGroup>();
                }
                else if (GetComponent<CanvasGroup>())
                {
                    CreateMount();
                }
                else
                {
                    // AddComponent 的原生回调可关闭并重开同一实例；新会话识别在建的自有组，不再 Add。
                    _isInstallingSelf = true;
                    try
                    {
                        _group = gameObject.AddComponent<CanvasGroup>();
                    }
                    finally
                    {
                        _isInstallingSelf = false;
                    }
                }
            }
            if (!IsBound(state) || _isCreatingMount)
            {
                return false;
            }

            if (!_group && !_isInstallingSelf)
            {
                throw new InvalidOperationException("无法创建框架自有 CanvasGroup 门禁。");
            }

            _layout?.Synchronize();
            return IsBound(state);
        }

        private bool IsBound(PanelInputState state) => ReferenceEquals(_owner, state) && state.IsCurrent;

        internal bool Apply(PanelInputState state, bool blocked)
        {
            if (!IsBound(state) || _isCreatingMount)
            {
                return false;
            }

            _layout?.Synchronize();
            if (!IsBound(state) || state.IsBlocked != blocked)
            {
                return false;
            }

            if (_group)
            {
                _group.interactable = !blocked;
            }

            return IsBound(state);
        }

        internal void Release(PanelInputState state)
        {
            if (!ReferenceEquals(_owner, state))
            {
                return;
            }

            _owner = null;
            BindingVersion++;
            if (_group)
            {
                _group.interactable = true;
            }
            // 资源随物理实例保留；会话结束不延迟 Destroy 后再于同帧争用唯一组件槽。
        }

        private void CreateMount()
        {
            var initial = _owner;
            _isCreatingMount = true;
            try
            {
                var rect = (RectTransform)_target.transform;
                var host = new GameObject("YL.PanelInputGate", typeof(RectTransform));
                host.SetActive(false);
                host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                _slot = (RectTransform)host.transform;
                _group = host.AddComponent<CanvasGroup>();
                var coordinates = new GameObject("Coordinates", typeof(RectTransform));
                coordinates.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                _frame = (RectTransform)coordinates.transform;
                _frame.SetParent(_slot, false);
                coordinates.AddComponent<PanelInputGateLayout>().InitializePassThrough(rect);
                _layout = host.AddComponent<PanelInputGateLayout>();
                _layout.Initialize(rect, _frame);
                // 先备齐自有资源再接入业务层级；原生父级回调重开时只接管绑定，不重复安装。
                _slot.SetParent(rect.parent, false);
                if (!_target || _owner == null || !_owner.IsCurrent)
                {
                    return;
                }

                if (_slot.parent != rect.parent)
                {
                    _slot.SetParent(rect.parent, false);
                }

                _slot.SetSiblingIndex(rect.GetSiblingIndex());
                _layout.Synchronize();
                host.SetActive(true);
                if (!_target || _owner == null || !_owner.IsCurrent)
                {
                    return;
                }

                rect.SetParent(_frame, false);
                if (_target && _target.transform.parent == _frame)
                {
                    _layout.Synchronize();
                }
            }
            finally
            {
                _isCreatingMount = false;
                if (_owner != null && _owner.IsCurrent && _group)
                {
                    _group.interactable = !_owner.IsBlocked;
                }

                if (_owner != null && _owner.IsCurrent && !ReferenceEquals(_owner, initial))
                {
                    PanelManager.LateUpdate();
                }
            }
        }

        internal static void Reparent(Panel panel, Transform parent)
        {
            var gate = panel.GetComponent<PanelInputGate>();
            if (gate)
            {
                gate.BindingVersion++;
            }

            if (!gate || !gate._slot || panel.transform.parent != gate._frame)
            {
                panel.transform.SetParent(parent, false);
                return;
            }
            gate._slot.SetParent(parent, false);
            if (gate && gate._layout && panel && panel.transform.parent == gate._frame)
            {
                gate._layout.Synchronize();
            }
        }

        private void OnTransformParentChanged()
        {
            if (_owner == null || (_frame && transform.parent == _frame))
            {
                return;
            }

            PanelManager.LateUpdate();
        }

        private void ReleaseMount()
        {
            var previous = _layout;
            var previousGroup = _group;
            _slot = null;
            _frame = null;
            _layout = null;
            _group = null;
            if (previousGroup)
            {
                previousGroup.interactable = true;
            }

            if (previous)
            {
                previous.RetireMount();
            }
        }

        private void OnDestroy()
        {
            _owner = null;
            BindingVersion++;
            if (_group)
            {
                _group.interactable = true;
            }
            // OnDestroy 中业务 Transform 仍可能 truthy 但已禁止重挂；让存活的自有壳在下一帧清理。
            if (_layout)
            {
                _layout.RetireMount();
            }
        }
    }
}
