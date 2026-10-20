using YLFramework.Core.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YLFramework.StateMachine
{

    [CustomEditor(typeof(FSMTransitionInspectorHelper))]
    public class FSMTransitionInspector : UnityEditor.Editor
    {

        private ReorderableList reorderableList;

        private Rect condition_left_rect;
        private Rect condition_right_rect;
        private Rect popRect;

        private GUIContent add_a_sets_of_conditions = null;

        private GUIContent auto_switch = null;

        private static Dictionary<ParameterType, FSMConditionInspector> conditionInspector = new Dictionary<ParameterType, FSMConditionInspector>();

        private Dictionary<GroupCondition, ReorderableList> reorderables = new Dictionary<GroupCondition, ReorderableList>();
        private SerializedObject controllerObject;
        private FSMTransitionData boundTransition;

        private bool autoSwitch;

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= RebindAfterUndo;
            Undo.undoRedoPerformed += RebindAfterUndo;
            BindLists();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RebindAfterUndo;
            controllerObject?.Dispose();
            controllerObject = null;
        }

        private void RebindAfterUndo()
        {
            var helper = target as FSMTransitionInspectorHelper;
            if (helper && helper.controller && helper.transition != null)
            {
                helper.controller.ClearCache();
                helper.transition = helper.controller.GetTransitionData(helper.transition.fromStateName, helper.transition.toStateName);
            }
            BindLists();
            Repaint();
        }

        private SerializedProperty TransitionProperty(FSMTransitionInspectorHelper helper)
        {
            int index = helper.controller.transitions.IndexOf(helper.transition);
            return index < 0 ? null : controllerObject.FindProperty("transitions").GetArrayElementAtIndex(index);
        }

        private void BindLists()
        {
            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            reorderableList = null;
            reorderables.Clear();
            boundTransition = helper ? helper.transition : null;
            if (helper == null || !helper.controller || helper.transition == null) return;
            if (controllerObject == null || controllerObject.targetObject != helper.controller)
            {
                controllerObject?.Dispose();
                controllerObject = new SerializedObject(helper.controller);
            }
            controllerObject.Update();
            var transitionProperty = TransitionProperty(helper);
            if (transitionProperty == null) return;

            reorderableList = new ReorderableList(controllerObject, transitionProperty.FindPropertyRelative("conditions"), true, true, true, true);
            reorderableList.drawHeaderCallback += OnDrawHeaderCallback;
            reorderableList.onAddCallback += this.OnAddCallback;
            reorderableList.onRemoveCallback += this.OnRemoveCallback;
            reorderableList.drawElementCallback += this.DrawItem;
            reorderableList.onReorderCallback += ApplyReorder;

            autoSwitch = helper.transition.AutoSwtich;
        }

        private void ApplyReorder(ReorderableList list)
        {
            controllerObject.ApplyModifiedProperties();
            var helper = (FSMTransitionInspectorHelper)target;
            helper.controller.Save();
            RebindAfterUndo();
        }

        public override void OnInspectorGUI()
        {
            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null || !helper.controller || helper.transition == null) { BindLists(); return; }
            if (controllerObject == null || controllerObject.targetObject != helper.controller || boundTransition != helper.transition) BindLists();
            controllerObject.Update();
            var transitionProperty = TransitionProperty(helper);
            if (transitionProperty == null)
            {
                reorderableList = null;
                reorderables.Clear();
                EditorGUILayout.HelpBox("This transition has been removed.", MessageType.Info);
                return;
            }
            if (reorderableList == null) BindLists();
            // 数组删除/重排后重新取得句柄，保留列表本身的选中项和拖拽状态。
            reorderableList.serializedProperty = transitionProperty.FindPropertyRelative("conditions");

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            if (auto_switch == null)
                auto_switch = new GUIContent("AutoSwitch", "当条件为空时,是否自动切换?当前过渡的条件为空时可用!");




            EditorGUI.BeginDisabledGroup(!helper.transition.Empty);
            Rect rect = GUILayoutUtility.GetRect(0f, 20, GUILayout.ExpandWidth(expand: true));
            GUI.Label(rect, auto_switch);
            rect.Set(rect.width - 20, rect.y, 20, 20);
            if (helper.transition.Empty)
            {
                FSMTransitionFactory.SetAutoSwitch(helper.controller, helper.transition, GUI.Toggle(rect, helper.transition.AutoSwtich, string.Empty));
            }
            else {
                GUI.Toggle(rect,false, string.Empty);
            }

            if (autoSwitch != helper.transition.AutoSwtich)
            {
                helper.controller.Save();
                autoSwitch = helper.transition.AutoSwtich;
            }
            GUILayout.Space(10);
            EditorGUI.EndDisabledGroup();

            reorderableList.DoLayoutList();

            GUILayout.Space(10);

            for (int i = 0; i < helper.transition.group_conditions.Count; i++)
            {
                GroupCondition condition = helper.transition.group_conditions[i];
                if (condition == null) continue;
                ReorderableList list = GetReorderableList(condition);
                if (list != null)
                {
                    list.DoLayoutList();
                    GUILayout.Space(10);
                }
            }




            if (add_a_sets_of_conditions == null)
                add_a_sets_of_conditions = new GUIContent("Add a set of conditions","添加一组条件");

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (helper.transition.group_conditions.Count > 0)
            {
                GUILayout.Label("注:当有多组条件时,其中一组满足,状态就会切换!", "CN StatusWarn");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(add_a_sets_of_conditions, GUILayout.Width(260), GUILayout.Height(25)))
            {
                FSMConditionFactory.AddGroup(helper.controller, helper.transition);
                controllerObject.Update();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();
            controllerObject.ApplyModifiedProperties();

        }

        protected override void OnHeaderGUI()
        {
            //base.OnHeaderGUI();
            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null || helper.transition == null) return;

            GUILayout.BeginHorizontal();

            GUILayout.Label(EditorGUIUtility.IconContent("icons/processed/unityeditor/animations/animatorstatetransition icon.asset"),GUILayout.Width(30),GUILayout.Height(30));
            GUILayout.Label(string.Format("{0} -> {1}", helper.transition.fromStateName, helper.transition.toStateName));
            GUILayout.EndHorizontal();

            // 画一条 分隔的线
            var rect = EditorGUILayout.BeginHorizontal();

            Handles.color = Color.black;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector3(rect.x + rect.width, rect.y));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

        }


        private static void InitConditionInspectors() {

            if (conditionInspector.Count == 0) {
                conditionInspector.Add(ParameterType.Bool, new FSMBoolConditionInspector());
                conditionInspector.Add(ParameterType.Float, new FSMFloatConditionInspector());
                conditionInspector.Add(ParameterType.Int, new FSMIntConditionInspector());
                conditionInspector.Add(ParameterType.Trigger, new FSMConditionInspector()); // 不需要做任何绘制
            }

        }

        private void OnAddCallback(ReorderableList list) {

            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null) { return; }
            //Debug.Log("添加条件");
            FSMConditionFactory.CreateCondition(helper.controller, helper.transition);
            controllerObject.Update();
            list.serializedProperty = TransitionProperty(helper).FindPropertyRelative("conditions");
        }

        private void OnRemoveCallback(ReorderableList list)
        {

            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null) return;

            FSMConditionFactory.DeleteCondition(helper.controller, helper.transition,list.index);
            controllerObject.Update();
            list.serializedProperty = TransitionProperty(helper).FindPropertyRelative("conditions");
        }

        private void DrawItem(Rect rect, int index, bool isActive, bool isFocused) {

            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null) { return; }

            var conditon = helper.transition.conditions[index];

            DrawItemExcute(rect, conditon);
        }

        private void DrawItemExcute(Rect rect, FSMConditionData condition)
        {
            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null) return;

            condition_left_rect.Set(rect.x, rect.y, rect.width / 2, rect.height);
            condition_right_rect.Set(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);

            if (helper.controller.parameters.Count > 0)
            {
                //tempContent.text = conditon.parameterName;
                if (EditorGUI.DropdownButton(condition_left_rect, new GUIContent(condition.parameterName), FocusType.Keyboard))
                {
                    // 弹出选择参数的弹框 TODO
                    PopupWindow.Show(rect, new FSMSelectParamWindow(rect.width / 2, condition, helper.controller));


                }
            }

            InitConditionInspectors();

            FSMParameterData parameter = helper.controller.GetParameterData(condition.parameterName);

            if (parameter == null)
            {
                EditorGUI.LabelField(condition_right_rect, "缺少参数!");
            }
            else
            {

                // 根据不同的参数类型绘制不同的内容
                if (conditionInspector.ContainsKey(parameter.parameterType))
                    // 进行绘制
                    if (conditionInspector[parameter.parameterType] != null)
                        conditionInspector[parameter.parameterType].OnGUI(condition_right_rect, condition, helper.controller);
                    else
                        Debug.LogErrorFormat("未查询到对应的绘制方式:{0}", parameter.parameterType);
            }

        }

        private void OnDrawHeaderCallback(Rect rect)
        {
            GUI.Label(rect, "Conditions");
        }

        public ReorderableList GetReorderableList(GroupCondition condition)
        {
            if (condition == null)
                return null;

            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null || !helper.controller || helper.transition == null)
                return null;

            int groupIndex = helper.transition.group_conditions.IndexOf(condition);
            var transitionProperty = TransitionProperty(helper);
            if (groupIndex < 0 || transitionProperty == null) return null;
            var property = transitionProperty.FindPropertyRelative("group_conditions").GetArrayElementAtIndex(groupIndex).FindPropertyRelative("conditions");
            if (!reorderables.ContainsKey(condition))
            {
                ReorderableList list = new ReorderableList(controllerObject, property, true, true, true, true);
                list.drawHeaderCallback += (rect)=> {

                    GUI.Label(rect, "Conditions");

                    rect.Set(rect.width - 10, rect.y + 1, 25, 25);

                    if (GUI.Button(rect,EditorGUIUtility.IconContent("d__Menu"), "IconButton"))
                    {
                        ShowConditionMenu(condition);
                    }


                };
                list.onAddCallback += (a) => {
                    FSMConditionFactory.AddGroupCondition(helper.controller, condition);
                    controllerObject.Update();
                    GetReorderableList(condition);
                };

                list.onRemoveCallback += (a) =>
                {
                    FSMConditionFactory.RemoveGroupCondition(helper.controller, condition, a.index);
                    controllerObject.Update();
                    GetReorderableList(condition);
                };

                list.drawElementCallback += (rect,index,c,d) =>
                {
                    DrawItemExcute(rect, condition.conditions[index]);
                };
                list.onReorderCallback += ApplyReorder;
                reorderables.Add(condition, list);
            }

            reorderables[condition].serializedProperty = property;
            return reorderables[condition];
        }


        private void ShowConditionMenu(GroupCondition condition)
        {
            FSMTransitionInspectorHelper helper = target as FSMTransitionInspectorHelper;
            if (helper == null) { return; }

            var genricMenu = new GenericMenu();

            genricMenu.AddItem(new GUIContent("Remove"), false, () =>
            {
                // 移除一组条件
                FSMConditionFactory.RemoveGroup(helper.controller, helper.transition, condition);
                reorderables.Clear();
                controllerObject.Update();
            });

            genricMenu.ShowAsContext();
        }

    }

}
