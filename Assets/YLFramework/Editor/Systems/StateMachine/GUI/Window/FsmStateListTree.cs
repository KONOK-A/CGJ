using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;

#if UNITY_6000_2_OR_NEWER
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#endif

namespace YLFramework.StateMachine
{
    public class FsmStateTreeViewItem : TreeViewItem
    {

        public MonoScript MonoScript
        {
            get; private set;
        }


        private Type _type = null;

        public Type Type
        {
            get
            {

                if (_type == null)
                {
                    _type = MonoScript.GetClass();
                }

                return _type;
            }
        }

        public FsmStateTreeViewItem(int id, int depth, string displayName, MonoScript script) : base(id, depth, displayName)
        {
            MonoScript = script;
        }




    }


    public class FsmStateListTree : TreeView
    {

        // Fix编码
        #region 字段

        private RuntimeFsmController _controller;
        //private FSMConditionData condition;
        private GUIStyle _style = new GUIStyle("label");

        private FsmStateNodeData _nodeData = null;

        private FsmSelectStateWindow _editorWindow = null;

        private List<int> _selections = new List<int>();

        #endregion




        #region 重写方法
        protected override TreeViewItem BuildRoot()
        {



            TreeViewItem root = new TreeViewItem(-1, -1);

            List<MonoScript> scripts = StateMachineEditorTools.GetAllStatesType();

            for (int i = 0; i < scripts.Count; i++)
            {

                Type type = scripts[i].GetClass();

                if (type == null)
                {
                    continue;
                }

                string displayName = type.Name;
                FsmStateTreeViewItem item = new FsmStateTreeViewItem(i, 0, displayName, scripts[i]);
                root.AddChild(item);
            }

            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            return base.BuildRows(root);
        }

        protected override void SingleClickedItem(int id)
        {
            base.SingleClickedItem(id);

            FsmStateTreeViewItem item = FindItem(id, rootItem) as FsmStateTreeViewItem;

            if (item != null)
            {
                try
                {
                    Undo.RecordObject(_controller, "Add FSM State Script");
                    _nodeData.AddStateScript(item.MonoScript);
                }
                catch (Exception e)
                {
                    _editorWindow.editorWindow.ShowNotification(new GUIContent(e.Message));
                    return;
                }
            }
            // 保存
            _controller.Save();
            _editorWindow.Close();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            //base.RowGUI(args);

            FsmStateTreeViewItem item = args.item as FsmStateTreeViewItem;
            if (item == null)
            {
                return;
            }

            Type type = item.Type;
            if (type == null)
            {
                return;
            }

            if (args.rowRect.Contains(Event.current.mousePosition))
            {
                _selections.Clear();
                _selections.Add(item.id);
                this.SetSelection(_selections);
            }


            Rect rect = new Rect();
            rect.Set(args.rowRect.x + 20, args.rowRect.y, args.rowRect.width - 20, args.rowRect.height);

            GUI.Label(new Rect(0, args.rowRect.y - 2, 20, 20), EditorGUIUtility.IconContent("d_cs Script Icon"));

            if (string.IsNullOrEmpty(type.Namespace))
            {
                GUI.Label(rect, type.Name);
            }
            else
            {
                _style.richText = true;
                GUI.Label(rect, string.Format("{0}<color=#A4A4A4>({1})</color>", type.Name, type.Namespace), _style);
            }

            //EditorGUIUtility.IconContent("d_cs Script Icon");
        }

        //protected override void ContextClicked()
        //{
        //    base.ContextClicked();
        //}

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        #endregion

        #region 方法
        public FsmStateListTree(TreeViewState state, RuntimeFsmController controller, FsmStateNodeData nodeData, FsmSelectStateWindow editorWindow) : base(state)
        {
            this._controller = controller;

            showBorder = true;
            showAlternatingRowBackgrounds = true;
            this._nodeData = nodeData;
            this._editorWindow = editorWindow;
        }


        #endregion
    }
}
