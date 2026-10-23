using YLFramework.Core.Editor;

namespace YLFramework.StateMachine
{

    public class FsmConditionFactory
    {
        public static void SetTargetValue(RuntimeFsmController controller, FsmConditionData condition, float value)
        {
            if (condition.targetValue == value)
            {
                return;
            }

            UnityEditor.Undo.RecordObject(controller, "Change FSM Condition Value");
            condition.targetValue = value;
            controller.Save();
        }

        public static void SetComparison(RuntimeFsmController controller, FsmConditionData condition, CompareType value)
        {
            if (condition.compareType == value)
            {
                return;
            }

            UnityEditor.Undo.RecordObject(controller, "Change FSM Condition Comparison");
            condition.compareType = value;
            controller.Save();
        }

        public static GroupCondition AddGroup(RuntimeFsmController controller, FsmTransitionData transition)
        {
            UnityEditor.Undo.RecordObject(controller, "Add FSM Condition Group");
            var group = new GroupCondition();
            transition.groupConditions.Add(group);
            controller.Save();
            return group;
        }

        public static void RemoveGroup(RuntimeFsmController controller, FsmTransitionData transition, GroupCondition group)
        {
            UnityEditor.Undo.RecordObject(controller, "Remove FSM Condition Group");
            transition.groupConditions.Remove(group);
            controller.Save();
        }

        public static void AddGroupCondition(RuntimeFsmController controller, GroupCondition group)
        {
            UnityEditor.Undo.RecordObject(controller, "Add FSM Group Condition");
            group.conditions.Add(CreateCondition(controller));
            controller.Save();
        }

        public static void RemoveGroupCondition(RuntimeFsmController controller, GroupCondition group, int index)
        {
            if (index < 0 || index >= group.conditions.Count)
            {
                return;
            }

            UnityEditor.Undo.RecordObject(controller, "Remove FSM Group Condition");
            group.conditions.RemoveAt(index);
            controller.Save();
        }

        public static FsmConditionData CreateCondition(RuntimeFsmController controller)
        {
            FsmConditionData condition = new FsmConditionData();

            string parameterName = string.Empty;

            FsmParameterData parameter = null;

            if (controller.parameters.Count > 0)
            {
                parameter = controller.parameters[0];
                parameterName = parameter.name;
            }

            if (parameter != null)
            {
                switch (parameter.parameterType)
                {
                    case ParameterType.Float:
                        condition.compareType = CompareType.Greater;
                        break;
                    case ParameterType.Int:
                        condition.compareType = CompareType.Greater;
                        break;
                    case ParameterType.Bool:
                        condition.compareType = CompareType.Equal;
                        break;

                }
            }
            else
            {
                condition.compareType = CompareType.Greater;
            }


            condition.targetValue = 0;
            condition.parameterName = parameterName;
            return condition;
        }

        // 创建条件
        public static void CreateCondition(RuntimeFsmController controller, FsmTransitionData transition)
        {
            UnityEditor.Undo.RecordObject(controller, "Create FSM Condition");
            FsmConditionData condition = CreateCondition(controller);
            transition.conditions.Add(condition);
            UnityEditor.EditorUtility.SetDirty(controller);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(controller);
        }

        public static void DeleteCondition(RuntimeFsmController controller, FsmTransitionData transition, int index)
        {
            if (index < 0 || index >= transition.conditions.Count)
            {
                return;
            }

            UnityEditor.Undo.RecordObject(controller, "Delete FSM Condition");
            transition.conditions.RemoveAt(index);


            UnityEditor.EditorUtility.SetDirty(controller);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(controller);
        }

    }

}
