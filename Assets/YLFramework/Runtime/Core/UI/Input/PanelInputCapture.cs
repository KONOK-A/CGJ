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
            internal object Session, Epoch;
            internal PointerEventData Data;
            internal BaseInputModule Module;
            internal object ButtonControl, Device;
            internal int PointerId, TouchId;
            internal PointerEventData.InputButton Button;
            internal RaycastResult PressRaycast;
            internal Vector2 Position, PressPosition;
            internal bool Invalid, Cleaned;
        }
        private readonly Dictionary<long, Capture> captures = new Dictionary<long, Capture>();
        private Capture primaryCapture;
        private object epoch = new object();
        private Panel framePanel;
        private object frameSession, frameEpoch;
        private bool frameBlocked;
        private int frame = -1;

        internal void Invalidate()
        {
            epoch = new object();
            foreach (var capture in new List<Capture>(captures.Values))
            {
                capture.Invalid = true;
                Cancel(capture);
            }
        }

        internal void Record(PointerEventData data)
        {
            var panel = GetComponentInParent<Panel>(true);
            var capture = new Capture
            {
                Panel = framePanel, Session = frameSession, Epoch = frameEpoch,
                Data = data, Module = data.currentInputModule, Button = data.button,
                PointerId = data.pointerId, ButtonControl = InputSystemBridge.GetPointerButton(data),
                Device = InputSystemBridge.GetPointerDevice(data), TouchId = InputSystemBridge.GetTouchId(data), PressRaycast = data.pointerCurrentRaycast,
                Position = data.position, PressPosition = data.pressPosition
            };
            capture.Invalid = frame != Time.frameCount || frameBlocked || !panel || panel != framePanel ||
                !ReferenceEquals(panel.Session, frameSession) || !ReferenceEquals(epoch, frameEpoch) || panel.IsInputBlocked;
            captures[Key(capture.PointerId, capture.Button)] = capture;
            if (capture.Button == PointerEventData.InputButton.Left) primaryCapture = capture;
            if (capture.Invalid) Cancel(capture, true);
        }

        private static long Key(int pointer, PointerEventData.InputButton button) => ((long)(uint)pointer << 2) | (uint)button;
        private bool Valid(Capture capture) => !capture.Invalid && capture.Panel && capture.Panel.IsLoaded &&
            !capture.Panel.IsInputBlocked && ReferenceEquals(capture.Panel.Session, capture.Session) &&
            ReferenceEquals(capture.Epoch, epoch);

        private void Cancel(Capture capture, bool acceptingDown = false)
        {
            capture.Invalid = true;
            var data = capture.Data;
            // 先撤销原生捕获，再调用标准控件取消。回调内 Close/复用不得对新 Session 重放旧收尾。
            if (!InputSystemBridge.CancelPointer(capture.Module, data, capture.PointerId, capture.Button, capture.Device, capture.TouchId, gameObject, acceptingDown)) return;
            FinishControls(capture);
        }

        private void FinishControls(Capture capture)
        {
            if (capture.Cleaned) return;
            capture.Cleaned = true;
            // 同一标准控件已有后来的主手势时，旧设备退出不能收尾新手势。
            if (capture.Button == PointerEventData.InputButton.Left && !ReferenceEquals(primaryCapture, capture)) return;
            // Input System 的三个鼠标按钮共享 eventData；收尾不能借用最后处理按钮的可变 button。
            var cancelData = new PointerEventData(capture.Module ? capture.Module.GetComponent<EventSystem>() : null)
            { pointerId = capture.PointerId, button = capture.Button, position = capture.Position, pressPosition = capture.PressPosition,
                pointerPressRaycast = capture.PressRaycast };
            bool Current() => capture.Panel && ReferenceEquals(capture.Panel.Session, capture.Session);
            foreach (var control in GetComponents<UIBehaviour>())
            {
                if (!Current()) break;
                if (control is ScrollRect scroll)
                {
                    scroll.OnEndDrag(cancelData);
                    if (Current()) scroll.StopMovement();
                }
                else if (control is Scrollbar scrollbar) scrollbar.OnPointerUp(cancelData);
                else if (control is InputField input) input.OnEndDrag(cancelData);
                else if (control is Selectable selectable) selectable.OnPointerUp(cancelData);
            }
        }

        public void OnPointerDown(PointerEventData data) => Record(data);
        internal void Drag(PointerEventData data)
        {
            if (captures.TryGetValue(Key(data.pointerId, data.button), out var capture) && !Valid(capture)) Cancel(capture);
        }
        public void OnPointerUp(PointerEventData data)
        {
            long key = Key(data.pointerId, data.button);
            if (!captures.TryGetValue(key, out var capture)) return;
            if (!Valid(capture)) Cancel(capture);
            Forget(key, capture);
        }
        internal void EndDrag(PointerEventData data)
        {
            long key = Key(data.pointerId, data.button);
            if (captures.TryGetValue(key, out var capture) && !capture.Invalid) Forget(key, capture);
        }
        private void OnDisable() { Invalidate(); captures.Clear(); primaryCapture = null; }
        private void Forget(long key, Capture capture)
        {
            if (!captures.TryGetValue(key, out var current) || !ReferenceEquals(current, capture)) return;
            captures.Remove(key);
            if (ReferenceEquals(primaryCapture, capture)) primaryCapture = null;
        }
        private void Update()
        {
            foreach (var pair in new List<KeyValuePair<long, Capture>>(captures))
            {
                var capture = pair.Value;
                // 移除设备时模块可能已清空/回收 eventData，只收尾原控件，不再写它的可变原生镜像。
                if (InputSystemBridge.IsPointerDeviceRemoved(capture.Device) || !capture.Module || !capture.Module.isActiveAndEnabled)
                {
                    capture.Invalid = true;
                    FinishControls(capture);
                    Forget(pair.Key, capture);
                    continue;
                }
                // eventData 可由池复用，不能根据它的新身份清理外部手势。
                if (capture.Data.pointerId != capture.PointerId || !ReferenceEquals(InputSystemBridge.GetPointerDevice(capture.Data), capture.Device) ||
                    InputSystemBridge.GetTouchId(capture.Data) != capture.TouchId)
                { Forget(pair.Key, capture); continue; }
                if (!Valid(capture)) Cancel(capture);
                if (!InputSystemBridge.PointerStillPressed(capture.Module, capture.ButtonControl, capture.PointerId, capture.Button))
                    Forget(pair.Key, capture);
            }
            framePanel = GetComponentInParent<Panel>(true);
            frameSession = framePanel ? framePanel.Session : null;
            frameEpoch = epoch;
            frameBlocked = !framePanel || framePanel.IsInputBlocked;
            frame = Time.frameCount;
        }
    }
}
