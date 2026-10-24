using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{

    public class FsmNodeView : Node
    {

        public FsmStateNodeData Data
        {
            get; private set;
        }


        private FsmStateGraphView _graphView = null;

        private GUIStyle _labelStyle = null;

        private float _runingTimer;


        public FsmNodeView(FsmStateNodeData data, FsmStateGraphView graphView)
        {
            this.Data = data;
            this._graphView = graphView;
            Clear();
            focusable = true;

            VisualElement image = new VisualElement();
            image.pickingMode = PickingMode.Ignore;
            image.style.width = 200;
            image.style.height = 40;
            IMGUIContainer container = new IMGUIContainer();
            container.pickingMode = PickingMode.Ignore;
            container.focusable = false;
            container.onGUIHandler += () =>
            {

                if (Context.Instance.RuntimeFsmController == null)
                {
                    return;
                }

                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle(GUI.skin.label);
                    _labelStyle.alignment = TextAnchor.MiddleCenter;
                    _labelStyle.fontSize = 15;
                    _labelStyle.padding.top = -5;
                }

                GUI.Label(container.layout, string.Empty, GetStateStyle(selected));
                GUI.Label(container.layout, Data.DisplayName, _labelStyle);

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
            RegisterCallback<DetachFromPanelEvent>(evt => EditorApplication.delayCall -= DelayCallSelection);
        }

        public override void SetPosition(Rect newPos)
        {

            // 实际位置
            newPos.Set((int)(newPos.x / 20) * 20, (int)(newPos.y / 20) * 20, FsmConst.StateNodeWith, FsmConst.StateNodeHeight);

            // 显示位置
            base.SetPosition(new Rect(newPos.x - 2, newPos.y - 2, newPos.width, newPos.height));

            if (Data.rect == newPos)
            {
                return;
            }

            if (Context.Instance.RuntimeFsmController)
            {
                Undo.RecordObject(Context.Instance.RuntimeFsmController, "Move FSM State");
            }

            Data.rect = newPos;
            if (Context.Instance.RuntimeFsmController != null)
            {
                Context.Instance.RuntimeFsmController.Save();
            }
        }


        public void ShowMenu(ContextualMenuPopulateEvent evt)
        {

            bool isAny = Data.IsAnyState;
            bool isEntry = Data.IsEntryState;

            if (!isEntry && !Data.IsUpState)
            {
                evt.menu.AppendAction("Make Transition", (a) =>
                {
                    _graphView.StartMakeTransition(this);
                });
            }
            else
            {
                evt.menu.AppendAction("Make Transition", null, DropdownMenuAction.Status.Disabled);
            }

            if (!isEntry && !isAny && !Data.IsUpState)
            {

                if (Data.isDefaultState)
                {
                    evt.menu.AppendAction("Set as Layer Default State", null, DropdownMenuAction.Status.Disabled);
                }
                else
                {
                    evt.menu.AppendAction("Set as Layer Default State", (a) =>
                    {
                        //SetDefaultState(node);
                        SetDefaultState();
                        _graphView.RefreshNodes();
                    });
                }

                evt.menu.AppendAction("Delete", (a) =>
                {
                    DeleteStates();
                });
            }


        }


        private void DeleteStates()
        {

            _graphView.DeleteSelectedElements();
        }

        private void SetDefaultState()
        {
            Undo.RecordObject(Context.Instance.RuntimeFsmController, "Set FSM Default State");
            List<FsmStateNodeData> states = Context.Instance.GetCurrentShowStateNodeData();
            if (states != null)
            {
                foreach (var item in states)
                {
                    item.isDefaultState = false;
                }
            }
            this.Data.isDefaultState = true;
            Context.Instance.RuntimeFsmController.Save();
        }


        private GUIStyle GetStateStyle(bool selected)
        {
            if (Data.isDefaultState)
            {

                if (Data.isSubStateMachine)
                {
                    return StateStyles.Get(selected ? Style.OrangeOnHEX : Style.OrangeHEX);
                }

                return StateStyles.Get(selected ? Style.OrangeOn : Style.Orange);
            }
            else if (Data.IsEntryState)
            {
                if (Data.isSubStateMachine)
                {
                    return StateStyles.Get(selected ? Style.GreenOnHEX : Style.GreenHEX);
                }

                return StateStyles.Get(selected ? Style.GreenOn : Style.Green);
            }
            else if (Data.IsAnyState)
            {
                if (Data.isSubStateMachine)
                {
                    return StateStyles.Get(selected ? Style.MintOnHEX : Style.MintHEX);
                }

                return StateStyles.Get(selected ? Style.MintOn : Style.Mint);
            }
            else if (Data.IsUpState)
            {
                return StateStyles.Get(selected ? Style.RedOnHEX : Style.RedHEX);
            }

            else
            {
                if (Data.isSubStateMachine)
                {
                    return StateStyles.Get(selected ? Style.NormalOnHEX : Style.NormalHEX);
                }

                return StateStyles.Get(selected ? Style.NormalOn : Style.Normal);
            }

        }

        public override void OnSelected()
        {
            base.OnSelected();
            EditorApplication.delayCall -= DelayCallSelection;
            EditorApplication.delayCall += DelayCallSelection;
        }

        private void DelayCallSelection()
        {
            if (panel == null || !Context.Instance.RuntimeFsmController || !Context.Instance.RuntimeFsmController.states.Contains(Data))
            {
                return;
            }

            FsmStateInspectorHelper.Instance.Inspect(Context.Instance.RuntimeFsmController, Data, _graphView);
        }

        public override void OnUnselected()
        {
            EditorApplication.delayCall -= DelayCallSelection;
            base.OnUnselected();
            FsmStateInspectorHelper.Instance.Inspect(Context.Instance.RuntimeFsmController, null, _graphView);
        }


        private void DrawRuning(Rect rect)
        {

            FsmStateNode currentState = GetCurrentState();

            if (currentState == null)
            {
                return;
            }

            if (Event.current.type != EventType.Repaint || (!(currentState.Data.name == Data.name)))
            {
                return;
            }

            rect.Set(15, 28, rect.width - 30, 20);

            _runingTimer = Time.realtimeSinceStartup % 1;

            GUIStyle guiStyle = "MeLivePlayBackground";
            GUIStyle guiStyle2 = "MeLivePlayBar";
            rect = guiStyle.margin.Remove(rect);
            Rect rect2 = guiStyle.padding.Remove(rect);

            rect2.width *= _runingTimer;

            guiStyle2.Draw(rect2, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
            guiStyle.Draw(rect, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
        }

        private FsmStateNode GetCurrentState()
        {
            if (!Application.isPlaying)
            {
                return null;
            }

            if (Context.Instance.FsmController == null)
            {
                return null;
            }

            if (Context.Instance.FsmController.RuntimeFsmController == null)
            {
                return null;
            }

            if (Context.Instance.RuntimeFsmController == null)
            {
                return null;
            }

            // 如果在运行时 正在运行的状态是orange
            int index = Context.Instance.FsmController.RuntimeFsmController.IndexOf(Context.Instance.RuntimeFsmController);
            FsmStateNode currentStateNode = Context.Instance.FsmController.GetCurrentStateInfo(index, Context.Instance.LayerParent);

            return currentStateNode;

        }


        public void OnMouseDownEvent(MouseDownEvent evt)
        {
            if (evt != null && evt.pressedButtons == 1 && evt.clickCount == 1)
            {

                if (_graphView.IsMakingTransition)
                {
                    _graphView.StopMakeTransition(this);
                }

                //graphView.selector.DragStateNodeStart();
                DelayCallSelection();
            }

            if (evt != null && evt.pressedButtons == 1 && evt.clickCount == 2)
            {
                if (Data.isSubStateMachine)
                {
                    Context.Instance.AddLayer(Data.name);
                    _graphView.RefreshNodes();
                }

                if (Data.IsUpState)
                {
                    Context.Instance.RemoveLast(Data.Parent);
                    _graphView.RefreshNodes();
                }

            }

        }

    }

}
