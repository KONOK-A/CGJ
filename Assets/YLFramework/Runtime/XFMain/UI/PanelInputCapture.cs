using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YLFramework.Core
{
    // 与原生输入控件同物体；只取消该控件的旧捕获，不清空借用 EventSystem 的其他指针。
    [DefaultExecutionOrder(-10000)]
    internal sealed class PanelInputCapture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private struct Capture
        {
            internal object Session;
            internal object Epoch;
            internal PointerEventData Data;
            internal BaseInputModule Module;
            internal object ButtonControl;
            internal PointerEventData.InputButton Button;
        }
        private readonly Dictionary<long, Capture> captures = new Dictionary<long, Capture>();
        private readonly List<long> stale = new List<long>();
        private object epoch = new object();
        private object frameSession;
        private object frameEpoch;
        private int frame = -1;
        internal void Invalidate() => epoch = new object();

        internal bool HasInvalidCapture
        {
            get
            {
                bool invalid = false;
                stale.Clear();
                foreach (var entry in captures)
                {
                    var capture = entry.Value;
                    var system = EventSystem.current;
                    if (!system || !capture.Module || !capture.Module.isActiveAndEnabled || system.currentInputModule != capture.Module ||
                        !InputSystemBridge.PointerStillPressed(capture.Module, capture.ButtonControl, capture.Data, capture.Button)) stale.Add(entry.Key);
                    else if (!Valid(capture.Data, capture.Button)) invalid = true;
                }
                foreach (var id in stale) captures.Remove(id);
                return invalid;
            }
        }

        internal void Record(PointerEventData data)
        {
            // EventSystem 在 native Down 内先执行 Button/OnSelect，再执行本组件；用帧开始的身份抵御派发中复用。
            captures[Key(data.pointerId, data.button)] = new Capture
            { Session = frame == Time.frameCount ? frameSession : null, Epoch = frame == Time.frameCount ? frameEpoch : null,
                Data = data, Module = data.currentInputModule,
                ButtonControl = InputSystemBridge.GetPointerButton(data), Button = data.button };
        }

        private static long Key(int pointer, PointerEventData.InputButton button) => ((long)(uint)pointer << 2) | (uint)button;

        private bool Valid(PointerEventData data, PointerEventData.InputButton? button = null)
        {
            var panel = GetComponentInParent<Panel>();
            return panel && panel.IsLoaded && !panel.IsInputBlocked && captures.TryGetValue(Key(data.pointerId, button ?? data.button), out var capture) &&
                ReferenceEquals(capture.Session, panel.Session) && ReferenceEquals(capture.Epoch, epoch);
        }

        private static void Cancel(PointerEventData data)
        {
            data.eligibleForClick = false;
            data.pointerPress = null;
            data.rawPointerPress = null;
            data.pointerClick = null;
            data.pointerDrag = null;
            data.dragging = false;
        }

        public void OnPointerDown(PointerEventData data) => Record(data);
        internal void Drag(PointerEventData data)
        {
            if (Valid(data)) return;
            Cancel(data);
            captures.Remove(Key(data.pointerId, data.button));
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (!Valid(data)) Cancel(data);
            captures.Remove(Key(data.pointerId, data.button));
        }
        internal void EndDrag(PointerEventData data) => captures.Remove(Key(data.pointerId, data.button));
        private void OnDisable() => Invalidate();
        private void Update()
        {
            var panel = GetComponentInParent<Panel>();
            frameSession = panel ? panel.Session : null;
            frameEpoch = epoch;
            frame = Time.frameCount;
        }
    }
}
