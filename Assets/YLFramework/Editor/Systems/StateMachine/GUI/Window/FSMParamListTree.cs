using YLFramework.Core.Editor;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_6000_2_OR_NEWER
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#endif


namespace YLFramework.StateMachine
{
    public class FSMParamListTree : TreeView
    {

        #region 字段

        private RuntimeFSMController controller;
        private FSMConditionData condition;

        #endregion




        #region 重写方法
        protected override TreeViewItem BuildRoot()
        {

            TreeViewItem root = new TreeViewItem(-1, -1);

            if (controller != null)
            {
                for (int i = 0; i < controller.parameters.Count; i++)
                {
                    root.AddChild(new TreeViewItem(i, 0, controller.parameters[i].name));
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

            FSMParameterData p = controller.GetParameterData(paramterName);

            if (p != null)
            {
                UnityEditor.Undo.RecordObject(controller, "Change FSM Condition Parameter");
                condition.parameterName = paramterName;

                switch (p.parameterType)
                {
                    case ParameterType.Float:
                    case ParameterType.Int:
                        condition.compareType = CompareType.Greater;
                        break;
                    case ParameterType.Bool:
                        condition.compareType = CompareType.Equal;
                        break;
                    case ParameterType.Trigger:
                        condition.compareType = CompareType.Equal;
                        condition.targetValue = 1;
                        break;
                }
            }
            else {
                Debug.LogErrorFormat("参数查询失败:{0}", paramterName);
            }

            // 保存
            controller.Save();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);

            if ( args.label.Equals(condition.parameterName) ) {
                GUI.Label(args.rowRect, "√");
            }

        }

        #endregion

        #region 方法
        public FSMParamListTree(TreeViewState state, RuntimeFSMController controller, FSMConditionData condition) : base(state)
        {
            this.controller = controller;
            this.condition = condition;

            showBorder = true;
            showAlternatingRowBackgrounds = true;
        }


        #endregion


    }

}
