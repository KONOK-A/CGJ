using UnityEngine;

namespace YLFramework.Core
{
    // 每个最近 Panel 所属的 Canvas 都安装门禁，包括 overrideSorting 的嵌套 Canvas。
    internal sealed class PanelRaycastGate : MonoBehaviour, ICanvasRaycastFilter
    {
        private Panel _owner;
        private object _session;

        internal void Bind(Panel panel, object currentSession)
        {
            _owner = panel;
            _session = currentSession;
        }
        internal void Release(object endedSession)
        {
            if (!ReferenceEquals(_session, endedSession))
            {
                return;
            }

            _owner = null;
            _session = null;
        }
        public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera) =>
            !_owner || !ReferenceEquals(_owner.Session, _session) || !_owner.IsInputBlocked;
    }
}
