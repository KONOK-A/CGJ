using YLFramework.Core.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class FSMStateNodeFactory
    {
        public static FSMStateNodeData CreateStateNode(RuntimeFSMController controller, string name, Rect rect, bool defaultState,bool isSubStateMachine = false,List<string> parent = null,bool isBuildInState = false,string buildInStateName = "")
        {

            // 判断名称是否重复
            if (controller.GetStateNodeData(name) != null)
            {
                string message = string.Format("创建状态节点失败,名称:{0}重复!", name);
                Debug.LogError(message);
                FSMEditorWindow.ShowNotification(message);
                return null;
            }

            FSMStateNodeData node = new FSMStateNodeData();
            Undo.RecordObject(controller, "Create FSM State");
            node.name = name;
            node.rect = rect;
            node.isSubStateMachine = isSubStateMachine;
            node.parents = parent;
            node.isBuildInState = isBuildInState;
            node.buildInStateName = buildInStateName;
            if (defaultState)
            {
                foreach (var item in controller.states)
                {
                    // 把相同层级的默认状态设置成false
                    if (!item.CompareParent(node)) continue;
                    item.defaultState = false;
                }
            }

            node.defaultState = defaultState;
            controller.AddState(node);
            AssetDatabase.SaveAssetIfDirty(controller);
            return node;
        }

        public static FSMStateNodeData CreateStateNode(RuntimeFSMController controller, Rect rect, bool defaultState, bool isSubStateMachine = false, List<string> parent = null,bool isBuildInState = false, string buildInStateName = "") {

            return CreateStateNode(controller,GetStateNodeName(controller),rect,defaultState,isSubStateMachine,parent, isBuildInState, buildInStateName);
        }

        private static string GetStateNodeName(RuntimeFSMController controller) {

            string name = null;
            int i = 1;
            do
            {
                name = string.Format("New State{0}", i);
                i++;
            } while (controller.GetStateNodeData(name) != null);



            return name;
        }

        public static void DeleteState(RuntimeFSMController controller,FSMStateNodeData state) {
            Undo.RecordObject(controller, "Delete FSM State");

            // 判断 删除的是不是 entry  或者 any
            if (state.IsAnyState || state.IsEntryState || state.IsUpState)
            {
                if (state.BaseLayer())
                {
                    string message = string.Format("状态:{0}不能删除!", state.DisplayName);
                    Debug.LogWarning(message);
                    FSMEditorWindow.ShowNotification(message);
                    return;
                }
            }

            // 删除相关的过渡
            for (int i = 0; i < controller.transitions.Count; i++)
            {
                FSMTransitionData transitionData = controller.transitions[i];
                if (transitionData.fromStateName.Equals(state.name) || transitionData.toStateName.Equals(state.name))
                {
                    controller.RemoveTransition(transitionData);
                    i--;
                }
            }

            controller.RemoveState(state);
            // 判断是不是默认状态
            if (state.defaultState) {
                foreach (var item in controller.states)
                {
                    if (item.IsAnyState || item.IsEntryState || item.IsUpState)
                        continue;

                    if (!item.CompareParent(state)) continue;

                    item.defaultState = true;
                    break;
                }
            }

            // 判断是不是子状态机
            if (state.isSubStateMachine) {

                List<FSMStateNodeData> subStates = new List<FSMStateNodeData>();
                foreach (var item in controller.states)
                {
                    if (item.BelongToParent(state.name))
                        subStates.Add(item);
                }

                foreach (var item in subStates)
                {
                    // 删除子状态
                    DeleteState(controller, item);
                }

            }

        }

        public static bool Rename(RuntimeFSMController controller,FSMStateNodeData node,string newName) {

            if ( node.name.Equals(FSMConst.entryState) || node.name.Equals(FSMConst.anyState) ) {
                return false;
            }

            if (string.IsNullOrEmpty(newName))
            {
                FSMEditorWindow.ShowNotification("名称不能为空!");
                return false;
            }

            if ( controller.GetStateNodeData(newName) !=null )
            {
                string message = string.Format("状态重命名失败,名称:{0}已经存在,请使用其他的名称!", newName);
                FSMEditorWindow.ShowNotification(message);
                return false;
            }

            Undo.RecordObject(controller, "Rename FSM State");
            // 找到 相关的过渡 进行修改
            foreach (var item in controller.transitions)
            {
                if (item.fromStateName.Equals(node.name)) {
                    item.fromStateName = newName;
                }

                if (item.toStateName.Equals(node.name)) {
                    item.toStateName = newName;
                }
            }

            string oldName = node.name;
            node.name = newName;

            // 修改子状态层级
            if (node.isSubStateMachine) {
                // 查到所有包含该状态的状态
                List<FSMStateNodeData> subStates = new List<FSMStateNodeData>();
                foreach (var item in controller.states)
                {
                    if (item.ContainsParent(oldName)) subStates.Add(item);
                }

                foreach (var item in subStates)
                {
                    // 重命名
                    item.RenameParent(oldName, newName);

                    if (item.isBuildInState)
                    {
                        string itemNewName = string.Format("{0}/{1}",item.ParentPath,item.buildInStateName);
                        Rename(controller,item, itemNewName);
                    }

                }
            }

            controller.ClearCache();
            EditorUtility.SetDirty(controller);

            return true;
        }

    }

}
