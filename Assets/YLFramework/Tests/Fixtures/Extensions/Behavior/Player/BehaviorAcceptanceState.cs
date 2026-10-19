using UnityEngine.Scripting;
using YLFramework.StateMachine;

namespace YLFramework.Behavior.Acceptance
{
    [Preserve]
    public sealed class BehaviorAcceptanceState : FSMState
    {
        public override void OnCreate() => (userData as BehaviorPlayerProbe)?.RecordState("create");
        public override void OnEnter() => (userData as BehaviorPlayerProbe)?.RecordState("enter");
        public override void OnExit() => (userData as BehaviorPlayerProbe)?.RecordState("exit");
    }
}
