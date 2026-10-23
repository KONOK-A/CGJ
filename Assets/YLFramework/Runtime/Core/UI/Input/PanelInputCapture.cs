using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YLFramework.Core
{
    // 在 EventSystem 处理前清理旧捕获；不重置整个模块，也不改业务控件 enabled。
    [DefaultExecutionOrder(-10000)]
    internal sealed class PanelInputCapture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private sealed class Capture
        {
            internal Panel Panel;
            internal object Session;
            internal object Epoch;
            internal PointerEventData Data;
            internal BaseInputModule Module;
            internal object ButtonControl;
            internal object Device;
            internal int PointerId;
            internal int TouchId;
            internal PointerEventData.InputButton Button;
            internal RaycastResult PressRaycast;
            internal Vector2 Position;
            internal Vector2 PressPosition;
            internal bool IsInvalid;
            internal bool IsCleaned;
        }
        private readonly Dictionary<long, Capture> _captures = new Dictionary<long, Capture>();
        private Capture _primaryCapture;
        private object _epoch = new object();
        private Panel _framePanel;
        private object _frameSession;
        private object _frameEpoch;
        private bool _isFrameBlocked;
        private int _frame = -1;

        internal void Invalidate()
        {
            _epoch = new object();
            foreach (var capture in new List<Capture>(_captures.Values))
            {
                capture.IsInvalid = true;
                Cancel(capture);
            }
        }

        internal void Record(PointerEventData data)
        {
            var panel = GetComponentInParent<Panel>(true);
            var capture = new Capture
            {
                Panel = _framePanel,
                Session = _frameSession,
                Epoch = _frameEpoch,
                Data = data,
                Module = data.currentInputModule,
                Button = data.button,
                PointerId = data.pointerId,
                ButtonControl = InputSystemBridge.GetPointerButton(data),
                Device = InputSystemBridge.GetPointerDevice(data),
                TouchId = InputSystemBridge.GetTouchId(data),
                PressRaycast = data.pointerCurrentRaycast,
                Position = data.position,
                PressPosition = data.pressPosition
            };
            capture.IsInvalid = _frame != Time.frameCount || _isFrameBlocked || !panel || panel != _framePanel ||
                !ReferenceEquals(panel.Session, _frameSession) || !ReferenceEquals(_epoch, _frameEpoch) || panel.IsInputBlocked;
            _captures[Key(capture.PointerId, capture.Button)] = capture;
            if (capture.Button == PointerEventData.InputButton.Left)
            {
                _primaryCapture = capture;
            }

            if (capture.IsInvalid)
            {
                Cancel(capture, true);
            }
        }

        private static long Key(int pointer, PointerEventData.InputButton button) => ((long)(uint)pointer << 2) | (uint)button;
        private bool IsValid(Capture capture) => !capture.IsInvalid && capture.Panel && capture.Panel.IsLoaded &&
            !capture.Panel.IsInputBlocked && ReferenceEquals(capture.Panel.Session, capture.Session) &&
            ReferenceEquals(capture.Epoch, _epoch);

        private void Cancel(Capture capture, bool acceptingDown = false)
        {
            capture.IsInvalid = true;
            var data = capture.Data;
            // 先撤销原生捕获，再调用标准控件取消。回调内 Close/复用不得对新 Session 重放旧收尾。
            if (!InputSystemBridge.CancelPointer(capture.Module, data, capture.PointerId, capture.Button, capture.Device, capture.TouchId, gameObject, acceptingDown))
            {
                return;
            }

            FinishControls(capture);
        }

        private void FinishControls(Capture capture)
        {
            if (capture.IsCleaned)
            {
                return;
            }

            capture.IsCleaned = true;
            // 同一标准控件已有后来的主手势时，旧设备退出不能收尾新手势。
            if (capture.Button == PointerEventData.InputButton.Left && !ReferenceEquals(_primaryCapture, capture))
            {
                return;
            }
            // Input System 的三个鼠标按钮共享 eventData；收尾不能借用最后处理按钮的可变 button。
            var cancelData = new PointerEventData(capture.Module ? capture.Module.GetComponent<EventSystem>() : null)
            {
                pointerId = capture.PointerId,
                button = capture.Button,
                position = capture.Position,
                pressPosition = capture.PressPosition,
                pointerPressRaycast = capture.PressRaycast
            };
            bool Current() => capture.Panel && ReferenceEquals(capture.Panel.Session, capture.Session);
            foreach (var control in GetComponents<UIBehaviour>())
            {
                if (!Current())
                {
                    break;
                }

                if (control is ScrollRect scroll)
                {
                    scroll.OnEndDrag(cancelData);
                    if (Current())
                    {
                        scroll.StopMovement();
                    }
                }
                else if (control is Scrollbar scrollbar)
                {
                    scrollbar.OnPointerUp(cancelData);
                }
                else if (control is InputField input)
                {
                    input.OnEndDrag(cancelData);
                }
                else if (control is Selectable selectable)
                {
                    selectable.OnPointerUp(cancelData);
                }
            }
        }

        public void OnPointerDown(PointerEventData data) => Record(data);
        internal void Drag(PointerEventData data)
        {
            if (_captures.TryGetValue(Key(data.pointerId, data.button), out var capture) && !IsValid(capture))
            {
                Cancel(capture);
            }
        }
        public void OnPointerUp(PointerEventData data)
        {
            long key = Key(data.pointerId, data.button);
            if (!_captures.TryGetValue(key, out var capture))
            {
                return;
            }

            if (!IsValid(capture))
            {
                Cancel(capture);
            }

            Forget(key, capture);
        }
        internal void EndDrag(PointerEventData data)
        {
            long key = Key(data.pointerId, data.button);
            if (_captures.TryGetValue(key, out var capture) && !capture.IsInvalid)
            {
                Forget(key, capture);
            }
        }
        private void OnDisable()
        {
            Invalidate();
            _captures.Clear();
            _primaryCapture = null;
        }
        private void Forget(long key, Capture capture)
        {
            if (!_captures.TryGetValue(key, out var current) || !ReferenceEquals(current, capture))
            {
                return;
            }

            _captures.Remove(key);
            if (ReferenceEquals(_primaryCapture, capture))
            {
                _primaryCapture = null;
            }
        }
        private void Update()
        {
            foreach (var pair in new List<KeyValuePair<long, Capture>>(_captures))
            {
                var capture = pair.Value;
                // 移除设备时模块可能已清空/回收 eventData，只收尾原控件，不再写它的可变原生镜像。
                if (InputSystemBridge.IsPointerDeviceRemoved(capture.Device) || !capture.Module || !capture.Module.isActiveAndEnabled)
                {
                    capture.IsInvalid = true;
                    FinishControls(capture);
                    Forget(pair.Key, capture);
                    continue;
                }
                // eventData 可由池复用，不能根据它的新身份清理外部手势。
                if (capture.Data.pointerId != capture.PointerId || !ReferenceEquals(InputSystemBridge.GetPointerDevice(capture.Data), capture.Device) ||
                    InputSystemBridge.GetTouchId(capture.Data) != capture.TouchId)
                {
                    Forget(pair.Key, capture);
                    continue;
                }
                if (!IsValid(capture))
                {
                    Cancel(capture);
                }

                if (!InputSystemBridge.PointerStillPressed(capture.Module, capture.ButtonControl, capture.PointerId, capture.Button))
                {
                    Forget(pair.Key, capture);
                }
            }
            _framePanel = GetComponentInParent<Panel>(true);
            _frameSession = _framePanel ? _framePanel.Session : null;
            _frameEpoch = _epoch;
            _isFrameBlocked = !_framePanel || _framePanel.IsInputBlocked;
            _frame = Time.frameCount;
        }
    }
}
