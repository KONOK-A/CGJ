using UnityEngine;

namespace YLFramework.Core
{
    // 每个最近 Panel 所属的 Canvas 都安装门禁，包括 overrideSorting 的嵌套 Canvas。
    internal sealed class PanelRaycastGate : MonoBehaviour, ICanvasRaycastFilter
    {
        private Panel owner;
        private object session;

        internal void Bind(Panel panel, object currentSession) { owner = panel; session = currentSession; }
        internal void Release(object endedSession)
        {
            if (!ReferenceEquals(session, endedSession)) return;
            owner = null;
            session = null;
        }
        public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera) =>
            !owner || !ReferenceEquals(owner.Session, session) || !owner.IsInputBlocked;
    }
}
