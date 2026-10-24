using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.StateMachine;


#if UNITY_6000_2_OR_NEWER
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#endif

namespace YLFramework.Editor.StateMachine
{
    public class FsmSelectParamWindow : PopupWindowContent
    {

        #region 字段

        private float _width;
        private FsmConditionData _condition;
        private RuntimeFsmController _controller;

        // 搜索框
        private SearchField _searchField;
        private Rect _searchRect;
        const float SearchHeight = 25f;

        // 标签
        private Rect _labelRect;
        const float LabelHeight = 30f;

        // 参数列表
        private FsmParamListTree _paramTree;
        private TreeViewState _paramState;
        private Rect _paramRect;

        #endregion


        public FsmSelectParamWindow(float width, FsmConditionData condition, RuntimeFsmController controller)
        {
            this._width = width;
            this._condition = condition;
            this._controller = controller;
        }


        public override Vector2 GetWindowSize()
        {
            return new Vector2(this._width, 240);
        }

        public override void OnGUI(Rect rect)
        {

            if (_paramTree == null)
            {
                if (_paramState == null)
                {
                    _paramState = new TreeViewState();
                }

                _paramTree = new FsmParamListTree(_paramState, _controller, _condition);
                _paramTree.Reload();
            }

            // 搜索框
            if (_searchField == null)
            {
                _searchField = new SearchField();
            }
            _searchRect.Set(rect.x + 5, rect.y + 5, rect.width - 10, SearchHeight);
            _paramTree.searchString = _searchField.OnGUI(_searchRect, _paramTree.searchString);

            // 标签
            _labelRect.Set(rect.x, rect.y + SearchHeight, rect.width, LabelHeight);
            EditorGUI.LabelField(_labelRect, _condition.parameterName, GUI.skin.GetStyle("AC BoldHeader"));

            // 参数列表


            _paramRect.Set(rect.x, rect.y + SearchHeight + LabelHeight - 5, rect.width, rect.height - SearchHeight - LabelHeight);
            _paramTree.OnGUI(_paramRect);

        }


    }

}
