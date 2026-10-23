using System.Collections.Generic;
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
    public class FsmParamListTree : TreeView
    {

        #region 字段

        private RuntimeFsmController _controller;
        private FsmConditionData _condition;

        #endregion




        #region 重写方法
        protected override TreeViewItem BuildRoot()
        {

            TreeViewItem root = new TreeViewItem(-1, -1);

            if (_controller != null)
            {
                for (int i = 0; i < _controller.parameters.Count; i++)
                {
                    root.AddChild(new TreeViewItem(i, 0, _controller.parameters[i].name));
                }
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

            string paramterName = FindItem(id, rootItem).displayName;

            FsmParameterData p = _controller.GetParameterData(paramterName);

            if (p != null)
            {
                UnityEditor.Undo.RecordObject(_controller, "Change FSM Condition Parameter");
                _condition.parameterName = paramterName;

                switch (p.parameterType)
                {
                    case ParameterType.Float:
                    case ParameterType.Int:
                        _condition.compareType = CompareType.Greater;
                        break;
                    case ParameterType.Bool:
                        _condition.compareType = CompareType.Equal;
                        break;
                    case ParameterType.Trigger:
                        _condition.compareType = CompareType.Equal;
                        _condition.targetValue = 1;
                        break;
                }
            }
            else
            {
                Debug.LogErrorFormat("参数查询失败:{0}", paramterName);
            }

            // 保存
            _controller.Save();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);

            if (args.label.Equals(_condition.parameterName))
            {
                GUI.Label(args.rowRect, "√");
            }

        }

        #endregion

        #region 方法
        public FsmParamListTree(TreeViewState state, RuntimeFsmController controller, FsmConditionData condition) : base(state)
        {
            this._controller = controller;
            this._condition = condition;

            showBorder = true;
            showAlternatingRowBackgrounds = true;
        }


        #endregion


    }

}
