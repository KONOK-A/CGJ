using YLFramework.Core.Editor;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class FSMParamterFactory
    {

        // 创建参数
        public static void CreateParamter(RuntimeFSMController controller,ParameterType type) {
            UnityEditor.Undo.RecordObject(controller, "Create FSM Parameter");

            FSMParameterData parameter = new FSMParameterData();
            parameter.name = GetDefualtName(controller, type);
            parameter.parameterType = type;
            parameter.value = 0;

            controller.AddParameters(parameter);
        }

        private static string GetDefualtName(RuntimeFSMController controller,ParameterType type) {
            string name = string.Format("New {0}", type.ToString());
            string tempName = name;

            int i = 1;
            while (controller.GetParameterData(tempName) != null)
            {
                tempName = string.Format("{0}{1}", name, i);
                i++;
            }

            return tempName;
        }

        // 删除参数
        public static void RemoveParamter(RuntimeFSMController controller,int index) {

            if (Application.isPlaying) return;

            if (controller == null || index < 0 || index >= controller.parameters.Count) return;

            FSMParameterData parameter = controller.parameters[index];

            List<FSMTransitionData> transitions = new List<FSMTransitionData>();

            // 查询引用了这个参数的过渡
            foreach (var item in controller.transitions)
            {
                foreach (var condition in item.conditions)
                {
                    if (condition.parameterName!=null && condition.parameterName.Equals(parameter.name)) {
                        transitions.Add(item);
                        break;
                    }

                }
                foreach (var group in item.group_conditions)
                    foreach (var condition in group.conditions)
                        if (condition.parameterName == parameter.name && !transitions.Contains(item)) transitions.Add(item);
            }

            if (transitions.Count == 0)
            {
                UnityEditor.Undo.RecordObject(controller, "Delete FSM Parameter");
                controller.RemoveParameters(parameter);
            }
            else {

                StringBuilder content = new StringBuilder();
                content.Append("确定删除参数:").Append(parameter.name).Append("吗？").Append("\n");
                content.Append("有以下过渡引用此参数!\n");

                foreach (var item in transitions)
                {
                    content.Append(item.fromStateName).Append(" -> ").Append(item.toStateName);
                }

                if (UnityEditor.EditorUtility.DisplayDialog("删除参数",content.ToString(),"确定","取消")) {
                    UnityEditor.Undo.RecordObject(controller, "Delete FSM Parameter And Conditions");
                    controller.RemoveParameters(parameter);

                    foreach (var item in transitions)
                    {
                        foreach (var group in item.group_conditions)
                            group.conditions.RemoveAll(condition => condition.parameterName == parameter.name);
                        for (int i = item.conditions.Count - 1; i >=0; i--)
                        {
                            FSMConditionData condition = item.conditions[i];
                            if (condition.parameterName != null && condition.parameterName.Equals(parameter.name))
                            {
                                item.conditions.RemoveAt(i);
                            }
                        }

                    }
                    controller.Save();

                }
            }

        }


        // 重命名参数
        public static void RenameParamter(RuntimeFSMController controller,FSMParameterData parameter,string newName) {

            // 判断名称是否为空
            if (string.IsNullOrEmpty(newName))
            {
                FSMEditorWindow.ShowNotification("参数名称不能为空!");
                Debug.LogError("参数名称不能为空!");
                return;
            }
            // 判断新的名称是否已经存在
            if (controller.GetParameterData(newName) != null) {
                FSMEditorWindow.ShowNotification("参数名称已经存在!");
                Debug.LogError("参数名称已经存在!");
                return;
            }

            UnityEditor.Undo.RecordObject(controller, "Rename FSM Parameter");
            // 查找到所有引用此参数的过渡 修改名称
            foreach (var item in controller.transitions)
            {
                // 遍历所有的条件
                foreach (var condition in item.conditions)
                {
                    if (condition.parameterName!=null && condition.parameterName.Equals(parameter.name)) {
                        condition.parameterName = newName;
                    }
                }

                // 遍历其他组的条件
                foreach (var group in item.group_conditions)
                {
                    foreach (var condition in group.conditions)
                    {
                        if (condition.parameterName != null && condition.parameterName.Equals(parameter.name))
                        {
                            condition.parameterName = newName;
                        }
                    }
                }

            }



            // 修改参数名称
            parameter.name = newName;
            controller.ClearCache();
            controller.Save();
        }

    }

}
