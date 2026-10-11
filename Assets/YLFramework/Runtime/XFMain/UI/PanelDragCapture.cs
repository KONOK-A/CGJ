using UnityEngine;
using UnityEngine.EventSystems;

namespace YLFramework.Core
{
    // 只附加到本来就可拖动的控件，不能让普通 Button 变成拖动目标。
    internal sealed class PanelDragCapture : MonoBehaviour, IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
    {
        private PanelInputCapture capture;
        private PanelInputCapture Capture => capture ? capture : (capture = GetComponent<PanelInputCapture>());
        public void OnInitializePotentialDrag(PointerEventData data) => Capture.Record(data);
        public void OnDrag(PointerEventData data) => Capture.Drag(data);
        public void OnEndDrag(PointerEventData data) => Capture.EndDrag(data);
    }
}
