using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    public class FsmStateGraphView : GraphView
    {

        public Dictionary<string, FsmNodeView> StateNodes = new Dictionary<string, FsmNodeView>();

        internal FsmTransitionBackground Transition = null;


        public bool IsMakingTransition = false;

        public FsmNodeView MakeTransitionFrom = null;

        public FsmNodeView HoverNode = null;

        private RuntimeFsmController _controller = null;


        public FsmEditorWindow Window
        {
            get; set;
        }

        //private FSMNodeView selectNode = null;

        public FsmStateGraphView()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/YLFramework/Editor/Systems/StateMachine/Styles/FSMGraph.uss"));
            var background = new GridBackground();
            background.AddToClassList("yl-fsm-grid");
            background.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");
            background.StretchToParentSize();
            Insert(0, background);

            Transition = new FsmTransitionBackground();
            Transition.pickingMode = PickingMode.Ignore;
            Transition.StretchToParentSize();
            Insert(1, Transition);

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
            if (Context.Instance.RuntimeFsmController != null)
            {
                //Debug.LogFormat("刷新视图:position:{0} scale:{1}", Context.Instance.RuntimeFSMController.viewPosition, Context.Instance.RuntimeFSMController.viewScale);

#if UNITY_6000_2_OR_NEWER
                // 默认位置和缩放
                var pos = Context.Instance.RuntimeFsmController.viewPosition;
                contentViewContainer.style.translate = new Translate(pos.x, pos.y, pos.z);
                contentViewContainer.style.scale = new Scale((Vector2)Context.Instance.RuntimeFsmController.viewScale);
#else
                // 默认位置和缩放
                viewTransform.position = Context.Instance.RuntimeFsmController.viewPosition;
                viewTransform.scale = Context.Instance.RuntimeFsmController.viewScale;
#endif
            }
            _controller = Context.Instance.RuntimeFsmController;
            RefreshNodes();
        }

        private void SaveScaleAndPosition()
        {

            if (viewTransform == null || Context.Instance.RuntimeFsmController == null)
            {
                return;
            }

#if UNITY_6000_2_OR_NEWER
            Vector3 scale = contentViewContainer.resolvedStyle.scale.value;
#else
            Vector3 scale = viewTransform.scale;
#endif

            if (Context.Instance.RuntimeFsmController.viewScale != scale)
            {
                Context.Instance.RuntimeFsmController.viewScale = scale;
                Context.Instance.RuntimeFsmController.Save();
            }

#if UNITY_6000_2_OR_NEWER
            Vector3 position = contentViewContainer.resolvedStyle.translate;
#else
            Vector3 position = viewTransform.position;
#endif


            if (Context.Instance.RuntimeFsmController.viewPosition != position)
            {
                Context.Instance.RuntimeFsmController.viewPosition = position;
                Context.Instance.RuntimeFsmController.Save();
            }

        }

        public void Update()
        {
            if (_controller != Context.Instance.RuntimeFsmController)
            {
                RefreshView();
                _controller = Context.Instance.RuntimeFsmController;
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

            List<FsmStateNodeData> datas = Context.Instance.GetCurrentShowStateNodeData();

            if (datas == null || datas.Count == 0)
            {
                return;
            }

            //selectNode = null;

            foreach (var item in datas)
            {
                if (item == null || string.IsNullOrEmpty(item.name) || StateNodes.ContainsKey(item.name))
                {
                    continue;
                }

                FsmNodeView node = new FsmNodeView(item, this);
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

        public void RenameState(string oldName, string newName)
        {
            //Debug.LogFormat("重命名:old:{0} new:{1} cn:{2} co:{3}",oldName,newName, StateNodes.ContainsKey(newName), StateNodes.ContainsKey(oldName));
            if (StateNodes.ContainsKey(newName))
            {
                return;
            }

            if (!StateNodes.ContainsKey(oldName))
            {
                return;
            }

            StateNodes.Add(newName, StateNodes[oldName]);
            StateNodes.Remove(oldName);
        }


        public FsmNodeView GetNode(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (StateNodes.ContainsKey(name))
            {
                return StateNodes[name];
            }

            return null;
        }


        private void KeyDownControl(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Delete || evt.target is TextElement ||
                (evt.target as VisualElement)?.GetFirstAncestorOfType<TextField>() != null || EditorGUIUtility.editingTextField)
            {
                return;
            }

            DeleteSelectedElements();
            evt.StopImmediatePropagation();
        }

        internal void DeleteSelectedElements()
        {
            var controller = Context.Instance.RuntimeFsmController;
            if (!controller || EditorApplication.isPlaying)
            {
                return;
            }

            var states = StateNodes.Values.Where(node => node.selected && !node.Data.IsAnyState && !node.Data.IsEntryState && !node.Data.IsUpState)
                .Select(node => node.Data).Where(controller.states.Contains).ToList();
            var edge = Transition.Selection;
            bool deleteEdge = edge != null && controller.transitions.Contains(edge);
            if (states.Count == 0 && !deleteEdge)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete FSM Selection");
            ClearSelection();
            Transition.OnTransitionSelection(null);
            if (IsMakingTransition)
            {
                StopMakeTransition(null);
            }

            if (deleteEdge)
            {
                FsmTransitionFactory.DeleteTransition(controller, edge);
            }

            foreach (var state in states)
            {
                if (controller.states.Contains(state))
                {
                    FsmStateNodeFactory.DeleteState(controller, state);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            RefreshNodes();
        }

        private void MouseDownControl(MouseDownEvent evt)
        {
            //BehaviourTreeView.TreeWindow.WindowRoot.InspectorView.UpdateInspector();
            if (evt.button == 0 && evt.clickCount == 1)
            {
                // 左键单击
                Transition.OnTransitionSelection(Transition.CloseTransition);
                StopMakeTransition(null);
            }
        }


        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {

            FsmNodeView node = evt.target as FsmNodeView;

            if (node != null)
            {
                Transition.OnTransitionSelection(null);
            }

            int count = evt.menu.MenuItems().Count;
            for (int i = 0; i < count; i++)
            {
                evt.menu.RemoveItemAt(0);
            }

            RuntimeFsmController controller = Context.Instance.RuntimeFsmController;

            bool disable = controller == null || EditorApplication.isPlaying;

            DropdownMenuAction.Status status = disable ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal;

            if (node == null)
            {

                //Debug.LogFormat("鼠标位置:{0}",Event.current.mousePosition);

                evt.menu.AppendAction("Create State", CreateState, (d) => status, evt.mousePosition);
                evt.menu.AppendAction("Create Sub-State Machine", CreateSubStateMachine, (d) => status, evt.mousePosition);
            }
            else
            {
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

        public void CreateSubStateMachine(DropdownMenuAction action)
        {
            Vector2 mousePosition = (Vector2)(action.userData);
            Vector2 position = contentViewContainer.WorldToLocal(mousePosition);
            CreateState(true, position);
        }


        private void CreateState(bool isSubStateMachine, Vector3 mousePosition)
        {

            Rect rect = new Rect(0, 0, FsmConst.StateNodeWith, FsmConst.StateNodeHeight);

            rect.center = mousePosition;

            List<FsmStateNodeData> states = Context.Instance.GetCurrentShowStateNodeData();

            bool isDefaultState = states == null || states.Count == 2;

            if (states != null)
            {
                bool isAllBuildIn = true;
                foreach (var item in states)
                {
                    if (!item.isBuiltInState)
                    {
                        isAllBuildIn = false;
                        break;
                    }
                }
                if (isAllBuildIn)
                {
                    isDefaultState = true;
                }
            }


            RuntimeFsmController controller = Context.Instance.RuntimeFsmController;

            List<string> parents = new List<string>();
            parents.AddRange(Context.Instance.Layers);

            FsmStateNodeData state = FsmStateNodeFactory.CreateStateNode(controller, rect, isDefaultState, isSubStateMachine, parents);

            if (isSubStateMachine)
            {
                // 给当前子状态机创建 Entry 状态
                Rect r = new Rect(0, 300, FsmConst.StateNodeWith, FsmConst.StateNodeHeight);
                CreateSubState(controller, FsmConst.EntryState, state, parents, r, FsmConst.EntryState);
                // 给当前子状态机创建 Any 状态
                r = new Rect(0, 100, FsmConst.StateNodeWith, FsmConst.StateNodeHeight);
                CreateSubState(controller, FsmConst.AnyState, state, parents, r, FsmConst.AnyState);
                // 给当前子状态机创建 Entry 状态
                r = new Rect(600, 300, FsmConst.StateNodeWith, FsmConst.StateNodeHeight);
                CreateSubState(controller, FsmConst.Up, state, parents, r, FsmConst.Up);
            }


            RefreshNodes();
        }

        private void CreateSubState(RuntimeFsmController controller, string name, FsmStateNodeData state, List<string> parents, Rect rect, string buildInName)
        {
            // 创建三个内置状态
            List<string> parentsTemp = new List<string>();
            parentsTemp.AddRange(parents);
            parentsTemp.Add(state.name);

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < parentsTemp.Count; i++)
            {
                stringBuilder.Append(parentsTemp[i]);
                stringBuilder.Append("/");
            }
            stringBuilder.Append(name);
            FsmStateNodeFactory.CreateStateNode(controller, stringBuilder.ToString(), rect, false, false, parentsTemp, true, buildInName);
        }


        public void StartMakeTransition(FsmNodeView node)
        {
            IsMakingTransition = true;
            MakeTransitionFrom = node;
        }

        public void StopMakeTransition(FsmNodeView node)
        {
            if (node == MakeTransitionFrom)
            {
                return;
            }

            IsMakingTransition = false;
            HoverNode = null;
            var from = MakeTransitionFrom;
            MakeTransitionFrom = null;
            if (node == null || from == null)
            {
                return;
            }

            RuntimeFsmController controller = Context.Instance.RuntimeFsmController;
            if (controller == null)
            {
                return;
            }

            FsmTransitionFactory.CreateTransition(controller, from.Data.name, node.Data.name);
        }

        private void UpdateHoverState(MouseMoveEvent evt)
        {
            Vector2 point = contentViewContainer.WorldToLocal(evt.mousePosition);
            HoverNode = null;
            foreach (var node in StateNodes.Values)
            {
                if (!node.GetPosition().Contains(point))
                {
                    continue;
                }

                HoverNode = node;
                Transition.CloseTransition = null;
                break;
            }
        }


        internal static bool GetPrefsSelection(string name)
        {
            string key = PrefsSelectionKey(name);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            bool v = EditorPrefs.GetBool(key, false);
            if (v)
            {
                EditorPrefs.DeleteKey(key);
            }

            return v;
        }

        internal static void SavePrefsSelection(string name)
        {
            string key = PrefsSelectionKey(name);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            EditorPrefs.SetBool(key, true);
        }

        internal static string PrefsSelectionKey(string name)
        {
            RuntimeFsmController controller = Context.Instance.RuntimeFsmController;
            if (controller == null)
            {
                return string.Empty;
            }

            return string.Format("YL.AssetFSM:{0}:{1}", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(controller)), name);
        }

    }
}
