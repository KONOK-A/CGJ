using YLFramework.Core.Editor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class FSMTransitionFactory
    {
        public static void SetAutoSwitch(RuntimeFSMController controller, FSMTransitionData transition, bool value)
        {
            if (transition.AutoSwtich == value) return;
            UnityEditor.Undo.RecordObject(controller, "Change FSM AutoSwitch");
            transition.AutoSwtich = value;
            controller.Save();
        }

        public static void CreateTransition(RuntimeFSMController controller,string fromStateName,string toStateName) {

            FSMStateNodeData toNode = controller.GetStateNodeData(toStateName);
            if (toNode == null || controller.GetStateNodeData(fromStateName) == null) return;

            if (toNode.IsAnyState || toNode.IsEntryState || toNode.IsUpState )
            {
                string message = string.Format("状态:{0}不能添加过渡!", toNode.DisplayName);
                Debug.LogWarning(message);
                FSMEditorWindow.ShowNotification(message);
                return;
            }

            if (fromStateName.Equals(toStateName)) {
                return;
            }

            foreach (var item in controller.transitions)
            {
                if (item.fromStateName.Equals(fromStateName) && item.toStateName.Equals(toStateName) )
                {
                    string message = string.Format("过渡 {0} -> {1} 已经存在,请勿重复添加!", fromStateName, toStateName);

                    Debug.LogError(message);
                    FSMEditorWindow.ShowNotification(message);
                    return;
                }
            }

            FSMTransitionData transition = new FSMTransitionData();
            UnityEditor.Undo.RecordObject(controller, "Create FSM Transition");
            transition.fromStateName = fromStateName;
            transition.toStateName = toStateName;
            controller.AddTransition(transition);
            UnityEditor.EditorUtility.SetDirty(controller);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(controller);

        }

        public static void DeleteTransition(RuntimeFSMController controller,FSMTransitionData transition) {
            UnityEditor.Undo.RecordObject(controller, "Delete FSM Transition");
            controller.RemoveTransition(transition);
            UnityEditor.EditorUtility.SetDirty(controller);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(controller);
        }

    }

}
