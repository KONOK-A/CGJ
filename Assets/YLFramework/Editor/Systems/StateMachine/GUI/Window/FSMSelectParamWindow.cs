using YLFramework.Core.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


#if UNITY_6000_2_OR_NEWER
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#endif

namespace YLFramework.StateMachine
{
    public class FSMSelectParamWindow : PopupWindowContent
    {

        #region 字段

        private float width;
        private FSMConditionData condition;
        private RuntimeFSMController controller;

        // 搜索框
        private SearchField searchField;
        private Rect searchRect;
        const float searchHeight = 25f;

        // 标签
        private Rect labelRect;
        const float labelHeight = 30f;

        // 参数列表
        private FSMParamListTree paramTree;
        private TreeViewState paramState;
        private Rect paramRect;

        #endregion


        public FSMSelectParamWindow(float width,FSMConditionData condition,RuntimeFSMController controller)
        {
            this.width = width;
            this.condition = condition;
            this.controller = controller;
        }


        public override Vector2 GetWindowSize()
        {
            return new Vector2(this.width,240);
        }

        public override void OnGUI(Rect rect)
        {

            if (paramTree == null)
            {
                if (paramState == null)
                {
                    paramState = new TreeViewState();
                }

                paramTree = new FSMParamListTree(paramState, controller, condition);
                paramTree.Reload();
            }

            // 搜索框
            if (searchField == null) {
                searchField = new SearchField();
            }
            searchRect.Set(rect.x + 5, rect.y + 5, rect.width - 10, searchHeight);
            paramTree.searchString = searchField.OnGUI(searchRect, paramTree.searchString);

            // 标签
            labelRect.Set(rect.x, rect.y + searchHeight, rect.width, labelHeight);
            EditorGUI.LabelField(labelRect, condition.parameterName, GUI.skin.GetStyle("AC BoldHeader"));

            // 参数列表


            paramRect.Set(rect.x, rect.y + searchHeight + labelHeight - 5, rect.width, rect.height - searchHeight - labelHeight);
            paramTree.OnGUI(paramRect);

        }


    }

}
