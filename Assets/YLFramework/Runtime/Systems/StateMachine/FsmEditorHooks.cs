#if UNITY_EDITOR
using System;
using UnityEngine;

namespace YLFramework.StateMachine
{
    // 仅传递作者操作；UnityEditor 类型和实现留在 Editor 程序集。
    public static class FsmEditorHooks
    {
        public static Action<RuntimeFsmController> Save;
        public static Action<FsmStateNodeData, RuntimeFsmController> RefreshNode;
        public static Action<FsmStateNodeData, UnityEngine.Object> AddScript;
        public static Action<FsmStateNodeData, UnityEngine.Object> RemoveScript;
        public static Action<RuntimeFsmController, RuntimeFsmController> CloneCreated;
    }
}
#endif
