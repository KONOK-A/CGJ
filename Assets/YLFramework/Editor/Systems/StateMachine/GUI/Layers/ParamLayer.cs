using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class ParamLayer : GraphLayer
    {

        #region 字段
        private ReorderableList reorderableList;
        private ReorderableList reorderableListStates;
        private Vector2 scrollView;

        // 参数区域
        //private Rect paramLabelRect;
        //private Rect paramValueRect;
        //const float param_value_width = 50f;
        //const float param_value_padding = 2f;

        private bool isRenaming = false;
        private string newName;
        private List<FSMParameterData> EmptyList = new List<FSMParameterData>();

        private Rect headerRect = new Rect();

        private string[] toolbars = new string[] { "Controllers", "Paramters" };
        private int select = 1;

        private int index = 0;

        //private Vector3[] lines = new Vector3[2];
        #endregion

        #region 重写方法

        public override void OnGUI(Rect rect)
        {
            rect.Set(rect.x + 2,rect.y,rect.width,rect.height);

            base.OnGUI(rect);

            headerRect.Set(rect.x, rect.y, rect.width, 20);

            //EditorGUI.DrawRect(headerRect, ColorConst.ParaBackground);

            headerRect.Set(headerRect.x + 5, headerRect.y, 150, headerRect.height);
            select = GUI.Toolbar(headerRect, select, toolbars, "toolbarbuttonLeft");

            //Handles.color = Color.black;
            //Handles.DrawLine(new Vector2(rect.x, rect.y + headerRect.height), new Vector2(rect.x + rect.width, rect.y + headerRect.height));
            //Handles.DrawLine(new Vector2(rect.x + rect.width, rect.y), new Vector2(rect.x + rect.width, rect.y + rect.height));
            // 偏移20, 用来绘制 States 和 Parmaters
            rect.Set(rect.x, rect.y + 20, rect.width, rect.height);

            //EditorGUI.DrawRect(rect, ColorConst.ParaBackground);
            //GUI.Box(rect, string.Empty, GUI.skin.GetStyle("CN Box"));

            if (select == 1)
            {
                DrawParamters(rect);
            }
            else {
                DrawStates(rect);
            }


        }

        private void DrawParamters(Rect rect)
        {
            if (reorderableList == null)
            {
                if (Context.Instance.RuntimeFSMController != null)
                {
                    reorderableList = new ReorderableList(Context.Instance.RuntimeFSMController.parameters, typeof(FSMParameterData), true, true, true, true);
                }
                else
                {
                    reorderableList = new ReorderableList(EmptyList, typeof(FSMParameterData), true, true, true, true);
                }

                reorderableList.drawHeaderCallback += HeaderCallbackDelegate;
                reorderableList.onAddDropdownCallback += OnAddDropdownCallback;
                reorderableList.onRemoveCallback += OnRemoveCallback;
                reorderableList.drawElementCallback += DrawElementCallback;

                reorderableList.onCanRemoveCallback += onCanRemoveCallback;
                reorderableList.onCanAddCallback += onCanRemoveCallback;
            }

            if (Context.Instance.RuntimeFSMController != null)
                reorderableList.list = Context.Instance.RuntimeFSMController.parameters;
            else
                reorderableList.list = EmptyList;

            EditorGUI.BeginDisabledGroup(Context.Instance.RuntimeFSMController == null);

            GUILayout.BeginArea(rect);
            scrollView = GUILayout.BeginScrollView(scrollView);
            reorderableList.DoLayoutList();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            EditorGUI.EndDisabledGroup();
        }

        private void DrawStates(Rect rect)
        {
            if (reorderableListStates == null)
            {

                reorderableListStates = new ReorderableList(Context.Instance.RuntimeFSMControllers, typeof(RuntimeFSMController), false, true, false, false);

                reorderableListStates.headerHeight = 0;
                reorderableListStates.drawElementCallback += DrawStateElementCallback;
                reorderableListStates.onSelectCallback += OnStateChanged;
            }

            for (int i = 0; i < Context.Instance.RuntimeFSMControllers.Count; i++)
            {
                if (Context.Instance.RuntimeFSMControllers[i] == null) {
                    Context.Instance.RuntimeFSMControllers.RemoveAt(i);
                    i--;
                }
            }

            reorderableListStates.list = Context.Instance.RuntimeFSMControllers;

            GUILayout.BeginArea(rect);
            scrollView = GUILayout.BeginScrollView(scrollView);
            reorderableListStates.DoLayoutList();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public override void ProcessEvents()
        {
            base.ProcessEvents();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                position.Contains(Event.current.mousePosition)
                )
            {
                Context.Instance.ClearSelections();
            }
        }

        public override void OnLostFocus()
        {
            base.OnLostFocus();
            isRenaming = false;
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

                    FSMParamterFactory.CreateParamter(Context.Instance.RuntimeFSMController, v);
                });
            }

            menu.ShowAsContext();
        }

        // 移除
        private void OnRemoveCallback(ReorderableList list)
        {
            FSMParamterFactory.RemoveParamter(Context.Instance.RuntimeFSMController, list.index);
        }

        // 绘制每一条数据
        private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (Context.Instance.RuntimeFSMController == null) return;
            if (index < 0 || index >= Context.Instance.RuntimeFSMController.parameters.Count) return;

            FSMParameterData parameter = Context.Instance.RuntimeFSMController.parameters[index];

            if (parameter == null) return;

            rect.width *= 0.6f;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0  && rect.Contains(Event.current.mousePosition) && isFocused )
            {
                isRenaming = true;
                this.index = index;
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                !rect.Contains(Event.current.mousePosition) && index == reorderableList.index)
            {
                EditorApplication.delayCall += () =>
                {
                    isRenaming = false;
                };

                GUI.FocusControl(null);
            }

            // 按下回车键的时候 也需要取消重命名
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                EditorApplication.delayCall += () =>
                {
                    isRenaming = false;
                };

            }

            if (isRenaming && index == reorderableList.index)
            {
                // 绘制输入框
                EditorGUI.BeginChangeCheck();
                newName = EditorGUI.DelayedTextField(rect, parameter.name);
                if (EditorGUI.EndChangeCheck() && this.index == index)
                {
                    isRenaming = false;
                    if (parameter.name.Equals(newName)) return;
                    FSMParamterFactory.RenameParamter(Context.Instance.RuntimeFSMController, parameter, newName);
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
                if (!Application.isPlaying) Undo.RecordObject(Context.Instance.RuntimeFSMController, "Change FSM Parameter");
                parameter.Value = value;
                Context.Instance.RuntimeFSMController.Save();
            }

        }

        private string GetParameterType(FSMParameterData parameter)
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
            if (index < 0 || index >= Context.Instance.RuntimeFSMControllers.Count) return;

            RuntimeFSMController controller = Context.Instance.RuntimeFSMControllers[index];

            //if (controller == null) return;

            try
            {
                GUIContent content = EditorGUIUtility.IconContent("AnimatorController Icon");

                if (controller == Context.Instance.RuntimeFSMController)
                    GUI.Label(new Rect(rect.x, rect.y, rect.height, rect.height), "✓");

                GUI.Label(new Rect(rect.x + rect.height, rect.y, rect.height, rect.height), content);
                rect.Set(rect.x + rect.height * 2,rect.y,rect.width - rect.height,rect.height);
                GUI.Label(rect,controller == null ? "None" : controller.name);
            }
            catch (Exception)
            {
            }


        }

        private bool onCanRemoveCallback(ReorderableList list)
        {
            return Application.isPlaying == false;
        }


        private void OnStateChanged(ReorderableList list)
        {
            if (Context.Instance.RuntimeFSMControllers == null || Context.Instance.RuntimeFSMControllers.Count == 0) return;

            if (Context.Instance.RuntimeFSMController == Context.Instance.RuntimeFSMControllers[list.index]) return;

            Context.Instance.FSMControllerIndex = list.index;
        }


        private void HeaderCallbackDelegate(Rect rect)
        {
            rect.width *= 0.6f;
            rect.x += 15;
            GUI.Label(rect,"名称");
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
