using YLFramework.Core.Editor;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YLFramework.StateMachine
{
    public class FSMStateGraphView : GraphView
    {

        public Dictionary<string, FSMNodeView> StateNodes = new Dictionary<string, FSMNodeView>();

        internal FSMTransitionBackground transition = null;


        public bool isMakeTransition = false;

        public FSMNodeView MakeTransitionFrom = null;

        public FSMNodeView HoverNode = null;

        private RuntimeFSMController controller = null;


        public FSMEditorWindow Window { get; set; }

        //private FSMNodeView selectNode = null;

        public FSMStateGraphView()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/YLFramework/Editor/Systems/StateMachine/Styles/FSMGraph.uss"));
            var background = new GridBackground();
            background.AddToClassList("yl-fsm-grid");
            background.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");
            background.StretchToParentSize();
            Insert(0, background);

            transition = new FSMTransitionBackground();
            transition.pickingMode = PickingMode.Ignore;
            transition.StretchToParentSize();
            Insert(1, transition);

            deleteSelection = (operationName, askUser) => DeleteSelectedElements();
            RegisterCallback<KeyDownEvent>(KeyDownControl, TrickleDown.TrickleDown);
            RegisterCallback<MouseDownEvent>(MouseDownControl);
            RegisterCallback<MouseMoveEvent>(UpdateHoverState);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            ContentZoomer zoomer = new ContentZoomer();
            zoomer.minScale = 0.1f;
            zoomer.maxScale = 3;

            this.AddManipulator(zoomer);

            RefreshView();
        }

        private void RefreshView()
        {
            if (Context.Instance.RuntimeFSMController != null)
            {
                //Debug.LogFormat("刷新视图:position:{0} scale:{1}", Context.Instance.RuntimeFSMController.viewPosition, Context.Instance.RuntimeFSMController.viewScale);

#if UNITY_6000_2_OR_NEWER
                // 默认位置和缩放
                var pos = Context.Instance.RuntimeFSMController.viewPosition;
                contentViewContainer.style.translate = new Translate(pos.x, pos.y, pos.z);
                contentViewContainer.style.scale = new Scale((Vector2)Context.Instance.RuntimeFSMController.viewScale);
#else
                // 默认位置和缩放
                viewTransform.position = Context.Instance.RuntimeFSMController.viewPosition;
                viewTransform.scale = Context.Instance.RuntimeFSMController.viewScale;
#endif
            }
            controller = Context.Instance.RuntimeFSMController;
            RefreshNodes();
        }

        private void SaveScaleAndPosition() {

            if (viewTransform == null || Context.Instance.RuntimeFSMController == null)
                return;

#if UNITY_6000_2_OR_NEWER
            Vector3 scale = contentViewContainer.resolvedStyle.scale.value;
#else
            Vector3 scale = viewTransform.scale;
#endif

            if (Context.Instance.RuntimeFSMController.viewScale != scale)
            {
                Context.Instance.RuntimeFSMController.viewScale = scale;
                Context.Instance.RuntimeFSMController.Save();
            }

#if UNITY_6000_2_OR_NEWER
            Vector3 position = contentViewContainer.resolvedStyle.translate;
#else
            Vector3 position = viewTransform.position;
#endif


            if (Context.Instance.RuntimeFSMController.viewPosition != position)
            {
                Context.Instance.RuntimeFSMController.viewPosition = position;
                Context.Instance.RuntimeFSMController.Save();
            }

        }

        public void Update()
        {
            if (controller != Context.Instance.RuntimeFSMController)
            {
                RefreshView();
                controller = Context.Instance.RuntimeFSMController;
            }

            SaveScaleAndPosition();
        }

        public void RefreshNodes()
        {

            foreach (var item in StateNodes.Values)
            {
                RemoveElement(item);
            }

            StateNodes.Clear();

            List<FSMStateNodeData> datas = Context.Instance.GetCurrentShowStateNodeData();

            if (datas == null || datas.Count == 0)
                return;

            //selectNode = null;

            foreach (var item in datas)
            {
                if (item == null || string.IsNullOrEmpty(item.name) || StateNodes.ContainsKey(item.name)) continue;
                FSMNodeView node = new FSMNodeView(item,this);
                node.title = item.DisplayName;
                node.SetPosition(item.rect);
                this.AddElement(node);
                StateNodes.Add(item.name, node);

                if (GetPrefsSelection(item.name))
                {
                    AddToSelection(node);
                }
            }


        }

        public void RenameState(string oldName,string newName)
        {
            //Debug.LogFormat("重命名:old:{0} new:{1} cn:{2} co:{3}",oldName,newName, StateNodes.ContainsKey(newName), StateNodes.ContainsKey(oldName));
            if (StateNodes.ContainsKey(newName))
                return;
            if (!StateNodes.ContainsKey(oldName))
                return;
            StateNodes.Add(newName, StateNodes[oldName]);
            StateNodes.Remove(oldName);
        }


        public FSMNodeView GetNode(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (StateNodes.ContainsKey(name))
                return StateNodes[name];

            return null;
        }


        private void KeyDownControl(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete || evt.target is TextElement ||
                (evt.target as VisualElement)?.GetFirstAncestorOfType<TextField>() != null || EditorGUIUtility.editingTextField) return;
            DeleteSelectedElements();
            evt.StopImmediatePropagation();
        }

        internal void DeleteSelectedElements()
        {
            var controller = Context.Instance.RuntimeFSMController;
            if (!controller || EditorApplication.isPlaying) return;
            var states = StateNodes.Values.Where(node => node.selected && !node.Data.IsAnyState && !node.Data.IsEntryState && !node.Data.IsUpState)
                .Select(node => node.Data).Where(controller.states.Contains).ToList();
            var edge = transition.Selection;
            bool deleteEdge = edge != null && controller.transitions.Contains(edge);
            if (states.Count == 0 && !deleteEdge) return;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete FSM Selection");
            ClearSelection();
            transition.OnTransitionSelection(null);
            if (isMakeTransition) StopMakeTransition(null);
            if (deleteEdge) FSMTransitionFactory.DeleteTransition(controller, edge);
            foreach (var state in states)
                if (controller.states.Contains(state)) FSMStateNodeFactory.DeleteState(controller, state);
            Undo.CollapseUndoOperations(undoGroup);
            RefreshNodes();
        }

        private void MouseDownControl(MouseDownEvent evt)
        {
            //BehaviourTreeView.TreeWindow.WindowRoot.InspectorView.UpdateInspector();
            if (evt.button == 0 && evt.clickCount == 1)
            {
                // 左键单击
                transition.OnTransitionSelection(transition.CloseTransition);
                StopMakeTransition(null);
            }
        }


        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {

            FSMNodeView node = evt.target as FSMNodeView;

            if (node != null)
                transition.OnTransitionSelection(null);


            int count = evt.menu.MenuItems().Count;
            for (int i = 0; i < count; i++)
            {
                evt.menu.RemoveItemAt(0);
            }

            RuntimeFSMController controller = Context.Instance.RuntimeFSMController;

            bool disable = controller == null || EditorApplication.isPlaying;

            DropdownMenuAction.Status status = disable ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal;

            if (node == null)
            {

                //Debug.LogFormat("鼠标位置:{0}",Event.current.mousePosition);

                evt.menu.AppendAction("Create State", CreateState, (d) => status, evt.mousePosition);
                evt.menu.AppendAction("Create Sub-State Machine", CreateSubStateMachine, (d) => status, evt.mousePosition);
            }
            else {
                node.ShowMenu(evt);
            }

            //base.BuildContextualMenu

        }


        public void CreateState(DropdownMenuAction action)
        {
            Vector2 mousePosition = (Vector2)(action.userData);
            Vector2 position = contentViewContainer.WorldToLocal(mousePosition);
            CreateState(false, position);
        }

        public void CreateSubStateMachine(DropdownMenuAction action) {
            Vector2 mousePosition = (Vector2)(action.userData);
            Vector2 position = contentViewContainer.WorldToLocal(mousePosition);
            CreateState(true, position);
        }


        private void CreateState(bool isSubStateMachine, Vector3 mousePosition)
        {

            Rect rect = new Rect(0, 0, FSMConst.StateNodeWith, FSMConst.StateNodeHeight);

            rect.center = mousePosition;

            List<FSMStateNodeData> states = Context.Instance.GetCurrentShowStateNodeData();

            bool isDefaultState = states == null || states.Count == 2;

            if (states != null)
            {
                bool isAllBuildIn = true;
                foreach (var item in states)
                {
                    if (!item.isBuildInState)
                    {
                        isAllBuildIn = false;
                        break;
                    }
                }
                if (isAllBuildIn) isDefaultState = true;
            }


            RuntimeFSMController controller = Context.Instance.RuntimeFSMController;

            List<string> parents = new List<string>();
            parents.AddRange(Context.Instance.Layers);

            FSMStateNodeData state = FSMStateNodeFactory.CreateStateNode(controller, rect, isDefaultState, isSubStateMachine, parents);

            if (isSubStateMachine)
            {
                // 给当前子状态机创建 Entry 状态
                Rect r = new Rect(0, 300, FSMConst.StateNodeWith, FSMConst.StateNodeHeight);
                CreateSubState(controller, FSMConst.entryState, state, parents, r, FSMConst.entryState);
                // 给当前子状态机创建 Any 状态
                r = new Rect(0, 100, FSMConst.StateNodeWith, FSMConst.StateNodeHeight);
                CreateSubState(controller, FSMConst.anyState, state, parents, r, FSMConst.anyState);
                // 给当前子状态机创建 Entry 状态
                r = new Rect(600, 300, FSMConst.StateNodeWith, FSMConst.StateNodeHeight);
                CreateSubState(controller, FSMConst.up, state, parents, r, FSMConst.up);
            }


            RefreshNodes();
        }

        private void CreateSubState(RuntimeFSMController controller, string name, FSMStateNodeData state, List<string> parents, Rect rect, string buildInName)
        {
            // 创建三个内置状态
            List<string> parents_temp = new List<string>();
            parents_temp.AddRange(parents);
            parents_temp.Add(state.name);

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < parents_temp.Count; i++)
            {
                stringBuilder.Append(parents_temp[i]);
                stringBuilder.Append("/");
            }
            stringBuilder.Append(name);
            FSMStateNodeFactory.CreateStateNode(controller, stringBuilder.ToString(), rect, false, false, parents_temp, true, buildInName);
        }


        public void StartMakeTransition(FSMNodeView node)
        {
            isMakeTransition = true;
            MakeTransitionFrom = node;
        }

        public void StopMakeTransition(FSMNodeView node)
        {
            if (node == MakeTransitionFrom)
                return;

            isMakeTransition = false;
            HoverNode = null;
            var from = MakeTransitionFrom;
            MakeTransitionFrom = null;
            if (node == null || from == null)
                return;

            RuntimeFSMController controller = Context.Instance.RuntimeFSMController;
            if (controller == null)
                return;
            FSMTransitionFactory.CreateTransition(controller, from.Data.name, node.Data.name);
        }

        private void UpdateHoverState(MouseMoveEvent evt)
        {
            Vector2 point = contentViewContainer.WorldToLocal(evt.mousePosition);
            HoverNode = null;
            foreach (var node in StateNodes.Values)
            {
                if (!node.GetPosition().Contains(point)) continue;
                HoverNode = node;
                transition.CloseTransition = null;
                break;
            }
        }


        internal static bool GetPrefsSelection(string name) {
            string key = PrefsSelectionKey(name);
            if (string.IsNullOrEmpty(key))
                return false;
            bool v = EditorPrefs.GetBool(key, false);
            if (v)
                EditorPrefs.DeleteKey(key);
            return v;
        }

        internal static void SavePrefsSelection(string name)
        {
            string key = PrefsSelectionKey(name);
            if (string.IsNullOrEmpty(key))
                return;
            EditorPrefs.SetBool(key, true);
        }

        internal static string PrefsSelectionKey(string name)
        {
            RuntimeFSMController controller = Context.Instance.RuntimeFSMController;
            if (controller == null)
                return string.Empty;

            return string.Format("YL.AssetFSM:{0}:{1}", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(controller)), name);
        }

    }
}
