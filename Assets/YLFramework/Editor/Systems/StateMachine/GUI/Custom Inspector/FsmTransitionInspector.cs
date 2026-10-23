using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.StateMachine
{

    [CustomEditor(typeof(FsmTransitionInspectorHelper))]
    public class FsmTransitionInspector : UnityEditor.Editor
    {

        private ReorderableList _reorderableList;

        private Rect _conditionLeftRect;
        private Rect _conditionRightRect;
        private Rect _popRect;

        private GUIContent _addConditionGroupLabel = null;

        private GUIContent _autoSwitchLabel = null;

        private static Dictionary<ParameterType, FsmConditionInspector> _conditionInspector = new Dictionary<ParameterType, FsmConditionInspector>();

        private Dictionary<GroupCondition, ReorderableList> _reorderables = new Dictionary<GroupCondition, ReorderableList>();
        private SerializedObject _controllerObject;
        private FsmTransitionData _boundTransition;

        private bool _autoSwitch;

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= RebindAfterUndo;
            Undo.undoRedoPerformed += RebindAfterUndo;
            BindLists();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RebindAfterUndo;
            _controllerObject?.Dispose();
            _controllerObject = null;
        }

        private void RebindAfterUndo()
        {
            var helper = target as FsmTransitionInspectorHelper;
            if (helper && helper.controller && helper.transition != null)
            {
                helper.controller.ClearCache();
                helper.transition = helper.controller.GetTransitionData(helper.transition.fromStateName, helper.transition.toStateName);
            }
            BindLists();
            Repaint();
        }

        private SerializedProperty TransitionProperty(FsmTransitionInspectorHelper helper)
        {
            int index = helper.controller.transitions.IndexOf(helper.transition);
            return index < 0 ? null : _controllerObject.FindProperty("transitions").GetArrayElementAtIndex(index);
        }

        private void BindLists()
        {
            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            _reorderableList = null;
            _reorderables.Clear();
            _boundTransition = helper ? helper.transition : null;
            if (helper == null || !helper.controller || helper.transition == null)
            {
                return;
            }

            if (_controllerObject == null || _controllerObject.targetObject != helper.controller)
            {
                _controllerObject?.Dispose();
                _controllerObject = new SerializedObject(helper.controller);
            }
            _controllerObject.Update();
            var transitionProperty = TransitionProperty(helper);
            if (transitionProperty == null)
            {
                return;
            }

            _reorderableList = new ReorderableList(_controllerObject, transitionProperty.FindPropertyRelative("conditions"), true, true, true, true);
            _reorderableList.drawHeaderCallback += OnDrawHeaderCallback;
            _reorderableList.onAddCallback += this.OnAddCallback;
            _reorderableList.onRemoveCallback += this.OnRemoveCallback;
            _reorderableList.drawElementCallback += this.DrawItem;
            _reorderableList.onReorderCallback += ApplyReorder;

            _autoSwitch = helper.transition.autoSwitch;
        }

        private void ApplyReorder(ReorderableList list)
        {
            _controllerObject.ApplyModifiedProperties();
            var helper = (FsmTransitionInspectorHelper)target;
            helper.controller.Save();
            RebindAfterUndo();
        }

        public override void OnInspectorGUI()
        {
            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null || !helper.controller || helper.transition == null)
            {
                BindLists();
                return;
            }
            if (_controllerObject == null || _controllerObject.targetObject != helper.controller || _boundTransition != helper.transition)
            {
                BindLists();
            }

            _controllerObject.Update();
            var transitionProperty = TransitionProperty(helper);
            if (transitionProperty == null)
            {
                _reorderableList = null;
                _reorderables.Clear();
                EditorGUILayout.HelpBox("This transition has been removed.", MessageType.Info);
                return;
            }
            if (_reorderableList == null)
            {
                BindLists();
            }
            // 数组删除/重排后重新取得句柄，保留列表本身的选中项和拖拽状态。
            _reorderableList.serializedProperty = transitionProperty.FindPropertyRelative("conditions");

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            if (_autoSwitchLabel == null)
            {
                _autoSwitchLabel = new GUIContent("AutoSwitch", "当条件为空时,是否自动切换?当前过渡的条件为空时可用!");
            }

            EditorGUI.BeginDisabledGroup(!helper.transition.IsEmpty);
            Rect rect = GUILayoutUtility.GetRect(0f, 20, GUILayout.ExpandWidth(expand: true));
            GUI.Label(rect, _autoSwitchLabel);
            rect.Set(rect.width - 20, rect.y, 20, 20);
            if (helper.transition.IsEmpty)
            {
                FsmTransitionFactory.SetAutoSwitch(helper.controller, helper.transition, GUI.Toggle(rect, helper.transition.autoSwitch, string.Empty));
            }
            else
            {
                GUI.Toggle(rect, false, string.Empty);
            }

            if (_autoSwitch != helper.transition.autoSwitch)
            {
                helper.controller.Save();
                _autoSwitch = helper.transition.autoSwitch;
            }
            GUILayout.Space(10);
            EditorGUI.EndDisabledGroup();

            _reorderableList.DoLayoutList();

            GUILayout.Space(10);

            for (int i = 0; i < helper.transition.groupConditions.Count; i++)
            {
                GroupCondition condition = helper.transition.groupConditions[i];
                if (condition == null)
                {
                    continue;
                }

                ReorderableList list = GetReorderableList(condition);
                if (list != null)
                {
                    list.DoLayoutList();
                    GUILayout.Space(10);
                }
            }




            if (_addConditionGroupLabel == null)
            {
                _addConditionGroupLabel = new GUIContent("Add a set of conditions", "添加一组条件");
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (helper.transition.groupConditions.Count > 0)
            {
                GUILayout.Label("注:当有多组条件时,其中一组满足,状态就会切换!", "CN StatusWarn");
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(_addConditionGroupLabel, GUILayout.Width(260), GUILayout.Height(25)))
            {
                FsmConditionFactory.AddGroup(helper.controller, helper.transition);
                _controllerObject.Update();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();
            _controllerObject.ApplyModifiedProperties();

        }

        protected override void OnHeaderGUI()
        {
            //base.OnHeaderGUI();
            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null || helper.transition == null)
            {
                return;
            }

            GUILayout.BeginHorizontal();

            GUILayout.Label(EditorGUIUtility.IconContent("icons/processed/unityeditor/animations/animatorstatetransition icon.asset"), GUILayout.Width(30), GUILayout.Height(30));
            GUILayout.Label(string.Format("{0} -> {1}", helper.transition.fromStateName, helper.transition.toStateName));
            GUILayout.EndHorizontal();

            // 画一条 分隔的线
            var rect = EditorGUILayout.BeginHorizontal();

            Handles.color = Color.black;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector3(rect.x + rect.width, rect.y));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

        }


        private static void InitConditionInspectors()
        {

            if (_conditionInspector.Count == 0)
            {
                _conditionInspector.Add(ParameterType.Bool, new FsmBoolConditionInspector());
                _conditionInspector.Add(ParameterType.Float, new FsmFloatConditionInspector());
                _conditionInspector.Add(ParameterType.Int, new FsmIntConditionInspector());
                _conditionInspector.Add(ParameterType.Trigger, new FsmConditionInspector()); // 不需要做任何绘制
            }

        }

        private void OnAddCallback(ReorderableList list)
        {

            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null)
            {
                return;
            }
            //Debug.Log("添加条件");
            FsmConditionFactory.CreateCondition(helper.controller, helper.transition);
            _controllerObject.Update();
            list.serializedProperty = TransitionProperty(helper).FindPropertyRelative("conditions");
        }

        private void OnRemoveCallback(ReorderableList list)
        {

            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null)
            {
                return;
            }

            FsmConditionFactory.DeleteCondition(helper.controller, helper.transition, list.index);
            _controllerObject.Update();
            list.serializedProperty = TransitionProperty(helper).FindPropertyRelative("conditions");
        }

        private void DrawItem(Rect rect, int index, bool isActive, bool isFocused)
        {

            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null)
            {
                return;
            }

            var conditon = helper.transition.conditions[index];

            DrawItemExcute(rect, conditon);
        }

        private void DrawItemExcute(Rect rect, FsmConditionData condition)
        {
            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null)
            {
                return;
            }

            _conditionLeftRect.Set(rect.x, rect.y, rect.width / 2, rect.height);
            _conditionRightRect.Set(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);

            if (helper.controller.parameters.Count > 0)
            {
                //tempContent.text = conditon.parameterName;
                if (EditorGUI.DropdownButton(_conditionLeftRect, new GUIContent(condition.parameterName), FocusType.Keyboard))
                {
                    // 弹出选择参数的弹框 TODO
                    PopupWindow.Show(rect, new FsmSelectParamWindow(rect.width / 2, condition, helper.controller));


                }
            }

            InitConditionInspectors();

            FsmParameterData parameter = helper.controller.GetParameterData(condition.parameterName);

            if (parameter == null)
            {
                EditorGUI.LabelField(_conditionRightRect, "缺少参数!");
            }
            else
            {

                // 根据不同的参数类型绘制不同的内容
                if (_conditionInspector.ContainsKey(parameter.parameterType))
                {
                    // 进行绘制
                    if (_conditionInspector[parameter.parameterType] != null)
                    {
                        _conditionInspector[parameter.parameterType].OnGUI(_conditionRightRect, condition, helper.controller);
                    }
                    else
                    {
                        Debug.LogErrorFormat("未查询到对应的绘制方式:{0}", parameter.parameterType);
                    }
                }
            }

        }

        private void OnDrawHeaderCallback(Rect rect)
        {
            GUI.Label(rect, "Conditions");
        }

        public ReorderableList GetReorderableList(GroupCondition condition)
        {
            if (condition == null)
            {
                return null;
            }

            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null || !helper.controller || helper.transition == null)
            {
                return null;
            }

            int groupIndex = helper.transition.groupConditions.IndexOf(condition);
            var transitionProperty = TransitionProperty(helper);
            if (groupIndex < 0 || transitionProperty == null)
            {
                return null;
            }

            var property = transitionProperty.FindPropertyRelative(nameof(FsmTransitionData.groupConditions)).GetArrayElementAtIndex(groupIndex).FindPropertyRelative("conditions");
            if (!_reorderables.ContainsKey(condition))
            {
                ReorderableList list = new ReorderableList(_controllerObject, property, true, true, true, true);
                list.drawHeaderCallback += (rect) =>
                {

                    GUI.Label(rect, "Conditions");

                    rect.Set(rect.width - 10, rect.y + 1, 25, 25);

                    if (GUI.Button(rect, EditorGUIUtility.IconContent("d__Menu"), "IconButton"))
                    {
                        ShowConditionMenu(condition);
                    }


                };
                list.onAddCallback += (a) =>
                {
                    FsmConditionFactory.AddGroupCondition(helper.controller, condition);
                    _controllerObject.Update();
                    GetReorderableList(condition);
                };

                list.onRemoveCallback += (a) =>
                {
                    FsmConditionFactory.RemoveGroupCondition(helper.controller, condition, a.index);
                    _controllerObject.Update();
                    GetReorderableList(condition);
                };

                list.drawElementCallback += (rect, index, c, d) =>
                {
                    DrawItemExcute(rect, condition.conditions[index]);
                };
                list.onReorderCallback += ApplyReorder;
                _reorderables.Add(condition, list);
            }

            _reorderables[condition].serializedProperty = property;
            return _reorderables[condition];
        }


        private void ShowConditionMenu(GroupCondition condition)
        {
            FsmTransitionInspectorHelper helper = target as FsmTransitionInspectorHelper;
            if (helper == null)
            {
                return;
            }

            var genricMenu = new GenericMenu();

            genricMenu.AddItem(new GUIContent("Remove"), false, () =>
            {
                // 移除一组条件
                FsmConditionFactory.RemoveGroup(helper.controller, helper.transition, condition);
                _reorderables.Clear();
                _controllerObject.Update();
            });

            genricMenu.ShowAsContext();
        }

    }

}
