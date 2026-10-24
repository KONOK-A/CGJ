using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    internal class ParamLayer : GraphLayer
    {

        #region 字段
        private ReorderableList _reorderableList;
        private ReorderableList _reorderableListStates;
        private Vector2 _scrollView;

        // 参数区域
        //private Rect paramLabelRect;
        //private Rect paramValueRect;
        //const float param_value_width = 50f;
        //const float param_value_padding = 2f;

        private bool _isRenaming = false;
        private string _newName;
        private List<FsmParameterData> _emptyList = new List<FsmParameterData>();

        private Rect _headerRect = new Rect();

        private string[] _toolbars = new string[] { "Controllers", "Paramters" };
        private int _select = 1;

        private int _index = 0;

        //private Vector3[] lines = new Vector3[2];
        #endregion

        #region 重写方法

        public override void OnGUI(Rect rect)
        {
            rect.Set(rect.x + 2, rect.y, rect.width, rect.height);

            base.OnGUI(rect);

            _headerRect.Set(rect.x, rect.y, rect.width, 20);

            //EditorGUI.DrawRect(headerRect, ColorConst.ParaBackground);

            _headerRect.Set(_headerRect.x + 5, _headerRect.y, 150, _headerRect.height);
            _select = GUI.Toolbar(_headerRect, _select, _toolbars, "toolbarbuttonLeft");

            //Handles.color = Color.black;
            //Handles.DrawLine(new Vector2(rect.x, rect.y + headerRect.height), new Vector2(rect.x + rect.width, rect.y + headerRect.height));
            //Handles.DrawLine(new Vector2(rect.x + rect.width, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height));
            // 偏移20, 用来绘制 States 和 Parmaters
            rect.Set(rect.x, rect.y + 20, rect.width, rect.height);

            //EditorGUI.DrawRect(rect, ColorConst.ParaBackground);
            //GUI.Box(rect, string.Empty, GUI.skin.GetStyle("CN Box"));

            if (_select == 1)
            {
                DrawParamters(rect);
            }
            else
            {
                DrawStates(rect);
            }


        }

        private void DrawParamters(Rect rect)
        {
            if (_reorderableList == null)
            {
                if (Context.Instance.RuntimeFsmController != null)
                {
                    _reorderableList = new ReorderableList(Context.Instance.RuntimeFsmController.parameters, typeof(FsmParameterData), true, true, true, true);
                }
                else
                {
                    _reorderableList = new ReorderableList(_emptyList, typeof(FsmParameterData), true, true, true, true);
                }

                _reorderableList.drawHeaderCallback += HeaderCallbackDelegate;
                _reorderableList.onAddDropdownCallback += OnAddDropdownCallback;
                _reorderableList.onRemoveCallback += OnRemoveCallback;
                _reorderableList.drawElementCallback += DrawElementCallback;

                _reorderableList.onCanRemoveCallback += OnCanRemoveCallback;
                _reorderableList.onCanAddCallback += OnCanRemoveCallback;
            }

            if (Context.Instance.RuntimeFsmController != null)
            {
                _reorderableList.list = Context.Instance.RuntimeFsmController.parameters;
            }
            else
            {
                _reorderableList.list = _emptyList;
            }

            EditorGUI.BeginDisabledGroup(Context.Instance.RuntimeFsmController == null);

            GUILayout.BeginArea(rect);
            _scrollView = GUILayout.BeginScrollView(_scrollView);
            _reorderableList.DoLayoutList();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            EditorGUI.EndDisabledGroup();
        }

        private void DrawStates(Rect rect)
        {
            if (_reorderableListStates == null)
            {

                _reorderableListStates = new ReorderableList(Context.Instance.RuntimeFsmControllers, typeof(RuntimeFsmController), false, true, false, false);

                _reorderableListStates.headerHeight = 0;
                _reorderableListStates.drawElementCallback += DrawStateElementCallback;
                _reorderableListStates.onSelectCallback += OnStateChanged;
            }

            for (int i = 0; i < Context.Instance.RuntimeFsmControllers.Count; i++)
            {
                if (Context.Instance.RuntimeFsmControllers[i] == null)
                {
                    Context.Instance.RuntimeFsmControllers.RemoveAt(i);
                    i--;
                }
            }

            _reorderableListStates.list = Context.Instance.RuntimeFsmControllers;

            GUILayout.BeginArea(rect);
            _scrollView = GUILayout.BeginScrollView(_scrollView);
            _reorderableListStates.DoLayoutList();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public override void ProcessEvents()
        {
            base.ProcessEvents();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                Position.Contains(Event.current.mousePosition)
                )
            {
                Context.Instance.ClearSelections();
            }
        }

        public override void OnLostFocus()
        {
            base.OnLostFocus();
            _isRenaming = false;
        }

        #endregion

        #region 方法
        public ParamLayer(EditorWindow editorWindow) : base(editorWindow)
        {

        }

        // 添加
        private void OnAddDropdownCallback(Rect buttonRect, ReorderableList list)
        {
            GenericMenu menu = new GenericMenu();

            for (int i = 0; i < Enum.GetValues(typeof(ParameterType)).Length; i++)
            {
                ParameterType v = (ParameterType)Enum.GetValues(typeof(ParameterType)).GetValue(i);
                menu.AddItem(new GUIContent(v.ToString()), false, () =>
                {

                    FsmParameterFactory.CreateParamter(Context.Instance.RuntimeFsmController, v);
                });
            }

            menu.ShowAsContext();
        }

        // 移除
        private void OnRemoveCallback(ReorderableList list)
        {
            FsmParameterFactory.RemoveParamter(Context.Instance.RuntimeFsmController, list.index);
        }

        // 绘制每一条数据
        private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (Context.Instance.RuntimeFsmController == null)
            {
                return;
            }

            if (index < 0 || index >= Context.Instance.RuntimeFsmController.parameters.Count)
            {
                return;
            }

            FsmParameterData parameter = Context.Instance.RuntimeFsmController.parameters[index];

            if (parameter == null)
            {
                return;
            }

            rect.width *= 0.6f;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition) && isFocused)
            {
                _isRenaming = true;
                this._index = index;
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                !rect.Contains(Event.current.mousePosition) && index == _reorderableList.index)
            {
                EditorApplication.delayCall += () =>
                {
                    _isRenaming = false;
                };

                GUI.FocusControl(null);
            }

            // 按下回车键的时候 也需要取消重命名
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                EditorApplication.delayCall += () =>
                {
                    _isRenaming = false;
                };

            }

            if (_isRenaming && index == _reorderableList.index)
            {
                // 绘制输入框
                EditorGUI.BeginChangeCheck();
                _newName = EditorGUI.DelayedTextField(rect, parameter.name);
                if (EditorGUI.EndChangeCheck() && this._index == index)
                {
                    _isRenaming = false;
                    if (parameter.name.Equals(_newName))
                    {
                        return;
                    }

                    FsmParameterFactory.RenameParamter(Context.Instance.RuntimeFsmController, parameter, _newName);
                }
            }
            else
            {
                GUI.Label(rect, parameter.name);
            }

            rect.x += rect.width;
            rect.width /= 3;
            EditorGUI.BeginDisabledGroup(true);
            GUI.Label(rect, GetParameterType(parameter));
            EditorGUI.EndDisabledGroup();
            rect.x += rect.width;

            float value = parameter.Value;
            switch (parameter.parameterType)
            {
                case ParameterType.Float:
                    value = EditorGUI.FloatField(rect, value);
                    break;
                case ParameterType.Int:
                    value = EditorGUI.IntField(rect, (int)value);
                    break;
                case ParameterType.Bool:
                    value = EditorGUI.Toggle(rect, value == 1) ? 1 : 0;
                    break;
                case ParameterType.Trigger:
                    value = EditorGUI.Toggle(rect, value == 1, StateStyles.Named("Radio", EditorStyles.radioButton)) ? 1 : 0;
                    break;
            }
            if (value != parameter.Value)
            {
                if (!Application.isPlaying)
                {
                    Undo.RecordObject(Context.Instance.RuntimeFsmController, "Change FSM Parameter");
                }

                parameter.Value = value;
                Context.Instance.RuntimeFsmController.Save();
            }

        }

        private string GetParameterType(FsmParameterData parameter)
        {
            switch (parameter.parameterType)
            {
                case ParameterType.Float:
                    return "Float";
                case ParameterType.Int:
                    return "Int";
                case ParameterType.Bool:
                    return "Bool";
                case ParameterType.Trigger:
                    return "Trigger";
            }

            return string.Empty;
        }

        private void DrawStateElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= Context.Instance.RuntimeFsmControllers.Count)
            {
                return;
            }

            RuntimeFsmController controller = Context.Instance.RuntimeFsmControllers[index];

            //if (controller == null) return;

            try
            {
                GUIContent content = EditorGUIUtility.IconContent("AnimatorController Icon");

                if (controller == Context.Instance.RuntimeFsmController)
                {
                    GUI.Label(new Rect(rect.x, rect.y, rect.height, rect.height), "✓");
                }

                GUI.Label(new Rect(rect.x + rect.height, rect.y, rect.height, rect.height), content);
                rect.Set(rect.x + rect.height * 2, rect.y, rect.width - rect.height, rect.height);
                GUI.Label(rect, controller == null ? "None" : controller.name);
            }
            catch (Exception)
            {
            }


        }

        private bool OnCanRemoveCallback(ReorderableList list)
        {
            return Application.isPlaying == false;
        }


        private void OnStateChanged(ReorderableList list)
        {
            if (Context.Instance.RuntimeFsmControllers == null || Context.Instance.RuntimeFsmControllers.Count == 0)
            {
                return;
            }

            if (Context.Instance.RuntimeFsmController == Context.Instance.RuntimeFsmControllers[list.index])
            {
                return;
            }

            Context.Instance.FsmControllerIndex = list.index;
        }


        private void HeaderCallbackDelegate(Rect rect)
        {
            rect.width *= 0.6f;
            rect.x += 15;
            GUI.Label(rect, "名称");
            rect.x += rect.width;
            rect.width /= 3;
            rect.x -= 10;
            GUI.Label(rect, "类型");
            rect.x += rect.width;
            rect.x -= 5;
            GUI.Label(rect, "值");
        }

        #endregion




    }

}
