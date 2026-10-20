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



    public class FSMSelectStateWindow : PopupWindowContent
    {


        // Fix编码
        #region 字段

        private RuntimeFSMController controller;

        // 搜索框
        private SearchField searchField;
        private Rect searchRect;
        const float searchHeight = 25f;

        // 标签
        private Rect labelRect;
        const float labelHeight = 30f;

        // 参数列表
        private FSMStateListTree stateTree;
        private TreeViewState stateState;
        private Rect stateRect;

        private Rect rect;

        private FSMStateNodeData nodeData;

        private bool showCreateScriptGUI = false;
        private string scriptName = string.Empty;
        private string scriptName2;
        #endregion


        public FSMSelectStateWindow(Rect rect,RuntimeFSMController controller,FSMStateNodeData nodeData)
        {
            //this.width = width;
            this.controller = controller;
            this.rect = rect;
            this.nodeData = nodeData;
            this.showCreateScriptGUI = false;
        }


        public override Vector2 GetWindowSize()
        {
            return new Vector2(this.rect.width, this.rect.height);
        }

        public override void OnGUI(Rect rect)
        {
            if (!showCreateScriptGUI)
            {
                OnGUISearchScripts(rect);
            }
            else {
                OnGUICreateScripts(rect);
            }

        }

        private void OnGUISearchScripts(Rect rect)
        {
            if (stateTree == null)
            {
                if (stateState == null)
                {
                    stateState = new TreeViewState();
                }

                stateTree = new FSMStateListTree(stateState, controller, this.nodeData,this);
                stateTree.Reload();
            }

            // 搜索框
            if (searchField == null)
            {
                searchField = new SearchField();
            }
            searchRect.Set(rect.x + 5, rect.y + 5, rect.width - 5, searchHeight);
            stateTree.searchString = searchField.OnGUI(searchRect, stateTree.searchString);

            // 标签
            labelRect.Set(rect.x, rect.y + searchHeight, rect.width, labelHeight);
            EditorGUI.LabelField(labelRect, "FSMStates", GUI.skin.GetStyle("AC BoldHeader"));

            // 参数列表

            stateRect.Set(rect.x, rect.y + searchHeight + labelHeight - 5, rect.width, rect.height - searchHeight - labelHeight - 20);
            stateTree.OnGUI(stateRect);

            stateRect.Set(rect.x, stateRect.y + stateRect.height, rect.width, 23);
            if (GUI.Button(stateRect, "New Scripts", "AppToolbarButtonMid")) {
                showCreateScriptGUI = true;
                scriptName = string.Empty;
            }
        }

        private void OnGUICreateScripts(Rect rect) {

            EditorGUI.BeginDisabledGroup(true);

            // 搜索框
            if (searchField == null)
            {
                searchField = new SearchField();
            }
            searchRect.Set(rect.x + 5, rect.y + 5, rect.width - 5, searchHeight);
            stateTree.searchString = searchField.OnGUI(searchRect, stateTree.searchString);

            EditorGUI.EndDisabledGroup();

            // 标签
            labelRect.Set(rect.x, rect.y + searchHeight, rect.width, labelHeight);

            if (GUI.Button(labelRect, "New Script", "AC BoldHeader"))
            {
                showCreateScriptGUI = false;
            }
            labelRect.y -= 3;
            GUI.Label(labelRect, EditorGUIUtility.IconContent("ArrowNavigationLeft"));

            // 参数列表

            stateRect.Set(rect.x, rect.y + searchHeight + labelHeight - 5, rect.width, rect.height - searchHeight - labelHeight - 20);

            GUILayout.BeginArea(stateRect);
            GUILayout.Space(10);
            GUILayout.Label("Name");
            EditorGUI.BeginChangeCheck();

            scriptName2 = EditorGUILayout.DelayedTextField(scriptName);

            if (EditorGUI.EndChangeCheck())
            {
                scriptName = scriptName2;
                EditorApplication.delayCall += CreateAndAddScript;
            }
            //GUILayout.Label(string.Format("scriptName:{0} scriptName2:{1}", scriptName, scriptName2));
            GUILayout.EndArea();

            stateRect.Set(rect.x,rect.height - 23, rect.width, 23);
            if (GUI.Button(stateRect, "Create And Add", "AppToolbarButtonMid"))
            {
                OnButtonClick();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return )
            {
                EditorApplication.delayCall += CreateAndAddScript;
            }

        }

        private void Update() {

            if(!showCreateScriptGUI && editorWindow != null)
                editorWindow.Repaint();

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
                return;

            editorWindow.Close();
            GUIUtility.ExitGUI();
        }


        private void CreateAndAddScript() {

            if (EditorWindow.focusedWindow != editorWindow)
                return;

            if (string.IsNullOrEmpty(scriptName2))
                return;

            FSMStateGraphView.SavePrefsSelection(this.nodeData.name);

            string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(controller));
            string path = string.Format("{0}/{1}.cs", dir, scriptName2);
            bool isSuccess = FSMStateCreator.CreateFSMState(path, false, controller, nodeData);

            if (!isSuccess)
            {
                GUIContent content = new GUIContent(string.Format("名称:{0}不可用,请修改后重试!", scriptName));
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
