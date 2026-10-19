#if UNITY_EDITOR
using System;
using UnityEngine;

namespace YLFramework.StateMachine
{
    // 仅传递作者操作；UnityEditor 类型和实现留在 Editor 程序集。
    public static class FSMEditorHooks
    {
        public static Action<RuntimeFSMController> Save;
        public static Action<FSMStateNodeData, RuntimeFSMController> RefreshNode;
        public static Action<FSMStateNodeData, UnityEngine.Object> AddScript;
        public static Action<FSMStateNodeData, UnityEngine.Object> RemoveScript;
        public static Action<RuntimeFSMController, RuntimeFSMController> CloneCreated;
    }
}
#endif
