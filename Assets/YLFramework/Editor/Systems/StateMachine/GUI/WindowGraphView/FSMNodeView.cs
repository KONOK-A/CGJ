using YLFramework.Core.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace YLFramework.StateMachine
{

    public class FSMNodeView : Node
    {

        public FSMStateNodeData Data { get;private set; }


        private FSMStateGraphView graphView = null;

        private GUIStyle labelStyle = null;

        private float runingTimer;


        public FSMNodeView(FSMStateNodeData data, FSMStateGraphView graphView)
        {
            this.Data = data;
            this.graphView = graphView;
            Clear();

            VisualElement image = new VisualElement();
            image.style.width = 200;
            image.style.height = 40;
            IMGUIContainer container = new IMGUIContainer();
            container.onGUIHandler += () =>
            {

                if (Context.Instance.RuntimeFSMController == null)
                    return;

                if (labelStyle == null)
                {
                    labelStyle = new GUIStyle(GUI.skin.label);
                    labelStyle.alignment = TextAnchor.MiddleCenter;
                    labelStyle.fontSize = 15;
                    labelStyle.padding.top = -5;
                }

                GUI.Label(container.layout,string.Empty, GetStateStyle(selected));
                GUI.Label(container.layout, Data.DisplayName, labelStyle);

                DrawRuning(container.layout);
            };

            container.StretchToParentSize();

            //image.name = "node-border";
            image.style.borderRightWidth = 0;
            image.style.borderLeftWidth = 0;
            image.style.borderTopWidth = 0;
            image.style.borderBottomWidth = 0;

            image.Add(container);
            Add(image);

            RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
        }

        public override void SetPosition(Rect newPos)
        {

            // 实际位置
            newPos.Set((int)(newPos.x / 20) * 20 , (int)(newPos.y / 20) * 20,FSMConst.StateNodeWith, FSMConst.StateNodeHeight);

            // 显示位置
            base.SetPosition(new Rect(newPos.x - 2,newPos.y - 2 ,newPos.width,newPos.height));

            if (Data.rect == newPos) return;
            if (Context.Instance.RuntimeFSMController) Undo.RecordObject(Context.Instance.RuntimeFSMController, "Move FSM State");
            Data.rect = newPos;
            if (Context.Instance.RuntimeFSMController != null)
                Context.Instance.RuntimeFSMController.Save();

        }


        public void ShowMenu(ContextualMenuPopulateEvent evt)
        {

            bool is_any = Data.IsAnyState;
            bool is_entry = Data.IsEntryState;

            if (!is_entry && !Data.IsUpState)
            {
                evt.menu.AppendAction( "Make Transition",(a) =>
                {
                    graphView.StartMakeTransition(this);
                });
            }
            else
            {
                evt.menu.AppendAction( "Make Transition", null, DropdownMenuAction.Status.Disabled);
            }

            if (!is_entry && !is_any && !Data.IsUpState)
            {

                if (Data.defaultState)
                {
                    evt.menu.AppendAction("Set as Layer Default State",null, DropdownMenuAction.Status.Disabled);
                }
                else
                {
                    evt.menu.AppendAction( "Set as Layer Default State", (a) =>
                    {
                        //SetDefaultState(node);
                        SetDefaultState();
                        graphView.RefreshNodes();
                    });
                }

                evt.menu.AppendAction( "Delete",(a) =>
                {
                    DeleteStates();
                });
            }


        }


        private void DeleteStates()
        {

            RuntimeFSMController controller = Context.Instance.RuntimeFSMController;
            if (controller == null)
                return;
            foreach (var item in graphView.StateNodes.Values)
            {
                if (item.selected)
                    FSMStateNodeFactory.DeleteState(controller, item.Data);
            }

            graphView.RefreshNodes();
        }

        private void SetDefaultState()
        {
            Undo.RecordObject(Context.Instance.RuntimeFSMController, "Set FSM Default State");
            List<FSMStateNodeData> states = Context.Instance.GetCurrentShowStateNodeData();
            if (states != null)
            {
                foreach (var item in states)
                {
                    item.defaultState = false;
                }
            }
            this.Data.defaultState = true;
            Context.Instance.RuntimeFSMController.Save();
        }


        private GUIStyle GetStateStyle(bool selected)
        {
           if(Data.defaultState)
            {

                if (Data.isSubStateMachine)
                    return StateStyles.Get(selected ? Style.OrangeOnHEX : Style.OrangeHEX);

                return StateStyles.Get(selected ? Style.OrangeOn : Style.Orange);
            }
            else if (Data.IsEntryState)
            {
                if (Data.isSubStateMachine)
                    return StateStyles.Get(selected ? Style.GreenOnHEX : Style.GreenHEX);

                return StateStyles.Get(selected ? Style.GreenOn : Style.Green);
            }
            else if (Data.IsAnyState)
            {
                if (Data.isSubStateMachine)
                    return StateStyles.Get(selected ? Style.MintOnHEX : Style.MintHEX);

                return StateStyles.Get(selected ? Style.MintOn : Style.Mint);
            }
            else if (Data.IsUpState)
            {
                return StateStyles.Get(selected ? Style.RedOnHEX : Style.RedHEX);
            }

            else
            {
                if (Data.isSubStateMachine)
                    return StateStyles.Get(selected ? Style.NormalOnHEX : Style.NormalHEX);

                return StateStyles.Get(selected ? Style.NormalOn : Style.Normal);
            }

        }

        public override void OnSelected()
        {
            base.OnSelected();
            EditorApplication.delayCall -= DelayCallSelection;
            EditorApplication.delayCall += DelayCallSelection;
        }

        private void DelayCallSelection() {
            FSMStateInspectorHelper.Instance.Inspect(Context.Instance.RuntimeFSMController, Data, graphView);
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            FSMStateInspectorHelper.Instance.Inspect(Context.Instance.RuntimeFSMController, null, graphView);
        }


        private void DrawRuning(Rect rect)
        {

            FSMStateNode currentState = GetCurrentState();

            if (currentState == null) return;

            if (Event.current.type != EventType.Repaint || (!(currentState.data.name == Data.name)))
                return;

            rect.Set(15, 28, rect.width - 30, 20);

            runingTimer = Time.realtimeSinceStartup % 1;

            GUIStyle gUIStyle = "MeLivePlayBackground";
            GUIStyle gUIStyle2 = "MeLivePlayBar";
            rect = gUIStyle.margin.Remove(rect);
            Rect rect2 = gUIStyle.padding.Remove(rect);

            rect2.width *= runingTimer;

            gUIStyle2.Draw(rect2, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
            gUIStyle.Draw(rect, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
        }

        private FSMStateNode GetCurrentState()
        {
            if (!Application.isPlaying)
                return null;

            if (Context.Instance.FSMController == null) return null;
            if (Context.Instance.FSMController.RuntimeFSMController == null) return null;
            if (Context.Instance.RuntimeFSMController == null) return null;

            // 如果在运行时 正在运行的状态是orange
            int index = Context.Instance.FSMController.RuntimeFSMController.IndexOf(Context.Instance.RuntimeFSMController);
            FSMStateNode current_state_node = Context.Instance.FSMController.GetCurrentStateInfo(index, Context.Instance.LayerParent);

            return current_state_node;

        }


        public void OnMouseDownEvent(MouseDownEvent evt)
        {
            if (evt != null && evt.pressedButtons == 1 && evt.clickCount == 1)
            {

                if (graphView.isMakeTransition)
                    graphView.StopMakeTransition(this);

                //graphView.selector.DragStateNodeStart();
                DelayCallSelection();
            }

            if (evt != null && evt.pressedButtons == 1 && evt.clickCount == 2)
            {
                if (Data.isSubStateMachine)
                {
                    Context.Instance.AddLayer(Data.name);
                    graphView.RefreshNodes();
                }

                if (Data.IsUpState)
                {
                    Context.Instance.RemoveLast(Data.Parent);
                    graphView.RefreshNodes();
                }

            }

        }

    }

}
