using UnityEngine;
using YLFramework.FSM;
using YLFramework.StateMachine;

namespace YLFramework.Behavior.Acceptance
{
    // 只用于同一Editor进程反复Play的自有场景，不给产品FSM增加驱动层。
    public sealed class BehaviorReloadProbe : MonoBehaviour
    {
        public FSMController asset;
        public RuntimeFSMController source;
        private FSM<int> lightweight;
        public bool LightweightAllocated => lightweight != null;
        public int LightweightState => lightweight == null ? -1 : lightweight.CurrentStateID;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            lightweight = FSM<int>.Allocate().Init(null);
            lightweight.State(1);
            lightweight.State(2);
            lightweight.SetUp(1);
        }

        private void Update() => lightweight?.Update();
        private void FixedUpdate() => lightweight?.FixedUpdate();
        private void LateUpdate() => lightweight?.LateUpdate();
        public bool ChangeLightweight() => lightweight != null && lightweight.ChangeState(2);

        private void OnDisable()
        {
            if (lightweight == null) return;
            lightweight.RecycleToCache();
            lightweight = null;
        }
    }
}
