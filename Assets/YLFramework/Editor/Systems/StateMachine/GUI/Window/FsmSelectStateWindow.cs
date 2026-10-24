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



    public class FsmSelectStateWindow : PopupWindowContent
    {


        // Fix编码
        #region 字段

        private RuntimeFsmController _controller;

        // 搜索框
        private SearchField _searchField;
        private Rect _searchRect;
        const float SearchHeight = 25f;

        // 标签
        private Rect _labelRect;
        const float LabelHeight = 30f;

        // 参数列表
        private FsmStateListTree _stateTree;
        private TreeViewState _stateState;
        private Rect _stateRect;

        private Rect _rect;

        private FsmStateNodeData _nodeData;

        private bool _showCreateScriptGui = false;
        private string _scriptName = string.Empty;
        private string _scriptName2;
        #endregion


        public FsmSelectStateWindow(Rect rect, RuntimeFsmController controller, FsmStateNodeData nodeData)
        {
            //this.width = width;
            this._controller = controller;
            this._rect = rect;
            this._nodeData = nodeData;
            this._showCreateScriptGui = false;
        }


        public override Vector2 GetWindowSize()
        {
            return new Vector2(this._rect.width, this._rect.height);
        }

        public override void OnGUI(Rect rect)
        {
            if (!_showCreateScriptGui)
            {
                OnGuiSearchScripts(rect);
            }
            else
            {
                OnGuiCreateScripts(rect);
            }

        }

        private void OnGuiSearchScripts(Rect rect)
        {
            if (_stateTree == null)
            {
                if (_stateState == null)
                {
                    _stateState = new TreeViewState();
                }

                _stateTree = new FsmStateListTree(_stateState, _controller, this._nodeData, this);
                _stateTree.Reload();
            }

            // 搜索框
            if (_searchField == null)
            {
                _searchField = new SearchField();
            }
            _searchRect.Set(rect.x + 5, rect.y + 5, rect.width - 5, SearchHeight);
            _stateTree.searchString = _searchField.OnGUI(_searchRect, _stateTree.searchString);

            // 标签
            _labelRect.Set(rect.x, rect.y + SearchHeight, rect.width, LabelHeight);
            EditorGUI.LabelField(_labelRect, "FSMStates", GUI.skin.GetStyle("AC BoldHeader"));

            // 参数列表

            _stateRect.Set(rect.x, rect.y + SearchHeight + LabelHeight - 5, rect.width, rect.height - SearchHeight - LabelHeight - 20);
            _stateTree.OnGUI(_stateRect);

            _stateRect.Set(rect.x, _stateRect.y + _stateRect.height, rect.width, 23);
            if (GUI.Button(_stateRect, "New Scripts", "AppToolbarButtonMid"))
            {
                _showCreateScriptGui = true;
                _scriptName = string.Empty;
            }
        }

        private void OnGuiCreateScripts(Rect rect)
        {

            EditorGUI.BeginDisabledGroup(true);

            // 搜索框
            if (_searchField == null)
            {
                _searchField = new SearchField();
            }
            _searchRect.Set(rect.x + 5, rect.y + 5, rect.width - 5, SearchHeight);
            _stateTree.searchString = _searchField.OnGUI(_searchRect, _stateTree.searchString);

            EditorGUI.EndDisabledGroup();

            // 标签
            _labelRect.Set(rect.x, rect.y + SearchHeight, rect.width, LabelHeight);

            if (GUI.Button(_labelRect, "New Script", "AC BoldHeader"))
            {
                _showCreateScriptGui = false;
            }
            _labelRect.y -= 3;
            GUI.Label(_labelRect, EditorGUIUtility.IconContent("ArrowNavigationLeft"));

            // 参数列表

            _stateRect.Set(rect.x, rect.y + SearchHeight + LabelHeight - 5, rect.width, rect.height - SearchHeight - LabelHeight - 20);

            GUILayout.BeginArea(_stateRect);
            GUILayout.Space(10);
            GUILayout.Label("Name");
            EditorGUI.BeginChangeCheck();

            _scriptName2 = EditorGUILayout.DelayedTextField(_scriptName);

            if (EditorGUI.EndChangeCheck())
            {
                _scriptName = _scriptName2;
                EditorApplication.delayCall += CreateAndAddScript;
            }
            //GUILayout.Label(string.Format("scriptName:{0} scriptName2:{1}", scriptName, scriptName2));
            GUILayout.EndArea();

            _stateRect.Set(rect.x, rect.height - 23, rect.width, 23);
            if (GUI.Button(_stateRect, "Create And Add", "AppToolbarButtonMid"))
            {
                OnButtonClick();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                EditorApplication.delayCall += CreateAndAddScript;
            }

        }

        private void Update()
        {

            if (!_showCreateScriptGui && editorWindow != null)
            {
                editorWindow.Repaint();
            }

            if (EditorWindow.focusedWindow != editorWindow)
            {
                EditorApplication.update -= Update;
            }
        }


        public override void OnOpen()
        {
            base.OnOpen();
            EditorApplication.update += Update;
        }

        public override void OnClose()
        {
            base.OnClose();
            EditorApplication.update -= Update;
        }

        public void Close()
        {
            EditorApplication.update -= Update;
            if (editorWindow == null)
            {
                return;
            }

            editorWindow.Close();
            GUIUtility.ExitGUI();
        }


        private void CreateAndAddScript()
        {

            if (EditorWindow.focusedWindow != editorWindow)
            {
                return;
            }

            if (string.IsNullOrEmpty(_scriptName2))
            {
                return;
            }

            FsmStateGraphView.SavePrefsSelection(this._nodeData.name);

            string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(_controller));
            string path = string.Format("{0}/{1}.cs", dir, _scriptName2);
            bool isSuccess = FsmStateCreator.CreateFsmState(path, false, _controller, _nodeData);

            if (!isSuccess)
            {
                GUIContent content = new GUIContent(string.Format("名称:{0}不可用,请修改后重试!", _scriptName));
                this.editorWindow.ShowNotification(content);
                return;
            }

            //Close();
        }


        private void OnButtonClick()
        {
            GUI.FocusControl(null);
        }

    }
}
