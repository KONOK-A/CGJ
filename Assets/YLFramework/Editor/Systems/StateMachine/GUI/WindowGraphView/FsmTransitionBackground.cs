using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YLFramework.Core.Editor;

namespace YLFramework.StateMachine
{
    public class FsmTransitionBackground : ImmediateModeElement
    {
        private float EdgeDistanceMultiplier => Mathf.Clamp(_graphView.scale, 0.1f, 1);

        private float EdgeSizeMultiplier => Mathf.Clamp(_graphView.scale, 0.1f, 1);

        private float LineWidth => Mathf.Max(1f, 2f * EdgeSizeMultiplier);


        private FsmStateGraphView _graphView;

        private static Vector3 EdgeToSelfOffsetVector => new Vector3(0f, 30f, 0f);

        private static Color SelectedEdgeColor => new Color(0.42f, 0.7f, 1f, 1f);

        public FsmTransitionData CloseTransition
        {
            get; internal set;
        }

        public FsmTransitionData Selection
        {
            get; set;
        }

        private FsmTransitionData _entryToDefault = new FsmTransitionData();

        private Vector3[] _vectors = new Vector3[2];

        private Vector3[] _points = new Vector3[2];

        private static Color DefaultTransitionColor => new Color(0.6f, 0.4f, 0f, 1f);

        private static FsmTransitionData _currentChangeTransition;
        private float _currentChangeTransitionTimer = 0;

        private float _lastRealTime;
        private float _deltaTime;

        #region 退出当前子状态
        private FsmStateNode _currentState;
        private bool _isStateExiting = false;
        private string _exitStateParent;
        private string _exitStateName;
        private string _exitStateToName;
        private float _exitStateTimer = 0;
        #endregion

        // Fix编码
        protected override void ImmediateRepaint()
        {
            List<FsmTransitionData> transitions = Context.Instance.GetCurrentShowTransitionData();
            //if (transitions == null || transitions.Count == 0)
            //    return;

            _deltaTime = Time.realtimeSinceStartup - _lastRealTime;
            _lastRealTime = Time.realtimeSinceStartup;

            if (_deltaTime > 0.2)
            {
                _deltaTime = 0;
            }

            if (Event.current.type == EventType.Repaint)
            {
                VisualElement visualElement = base.parent;
                _graphView = visualElement as FsmStateGraphView;
                if (_graphView == null)
                {
                    throw new InvalidOperationException("FSMTransitionBackground can only be added to a GraphView");
                }

                if (transitions != null)
                {
                    foreach (var transition in transitions)
                    {
                        DrawTransiton(transition, Color.white);
                        DrawCurrentTransition(transition);
                    }
                }

                List<FsmStateNodeData> nodes = Context.Instance.GetCurrentShowStateNodeData();
                if (nodes != null)
                {
                    _entryToDefault.fromStateName = string.Empty;
                    _entryToDefault.toStateName = string.Empty;

                    foreach (var item in nodes)
                    {
                        if (item.IsEntryState)
                        {
                            _entryToDefault.fromStateName = item.name;
                        }

                        if (item.isDefaultState)
                        {
                            _entryToDefault.toStateName = item.name;
                        }
                    }

                    DrawTransiton(_entryToDefault, DefaultTransitionColor);
                }

                DrawMakeTransition();

                DrawStateExitTransition();
            }

            if (_graphView.HoverNode == null)
            {
                CloseTransition = FindClosestTransition();
            }
        }

        private void DrawTransiton(FsmTransitionData data, Color color)
        {

            FsmNodeView fromNode = _graphView.GetNode(data.fromStateName);
            FsmNodeView toNode = _graphView.GetNode(data.toStateName);

            if (fromNode == null || toNode == null)
            {
                return;
            }

            if (data == Selection)
            {
                color = SelectedEdgeColor;
            }

            Vector3[] edgePoints = GetTransitionPoints(data);

            DrawTransiton(edgePoints, color, data == CloseTransition, true);
        }

        private void DrawTransiton(Vector3[] edgePoints, Color color, bool bold, bool isDrawArrow)
        {
            float arrowSize = 5f * EdgeSizeMultiplier;
            float outlineWidth = 2f * EdgeSizeMultiplier;
            float arrowLength = 13f * EdgeSizeMultiplier;
            Handles.color = color;
            float width = LineWidth;

            if (bold)
            {
                arrowSize *= 1.5f;
                width *= 1.5f;
            }

            Handles.DrawAAPolyLine(width, edgePoints[0], edgePoints[1]);
            if (isDrawArrow)
            {
                Vector3 cross = Vector3.Cross((edgePoints[0] - edgePoints[1]).normalized, Vector3.forward);
                DrawArrows(color, cross, edgePoints, isSelf: false, arrowSize, outlineWidth, arrowLength);
            }
        }

        private Vector3[] GetTransitionPoints(FsmTransitionData data)
        {
            return GetTransitionPoints(data.fromStateName, data.toStateName);
        }

        private Vector3[] GetTransitionPoints(string fromStateName, string toStateName)
        {
            FsmNodeView fromNode = _graphView.GetNode(fromStateName);
            FsmNodeView toNode = _graphView.GetNode(toStateName);

            if (fromNode == null || toNode == null)
            {
                return null;
            }

            float num = 5f * EdgeDistanceMultiplier;

            _points[0] = GetNodeViewPosition(fromNode);
            _points[1] = GetNodeViewPosition(toNode);

            Vector3 cross = Vector3.Cross((_points[0] - _points[1]).normalized, Vector3.forward);
            _points[0] += cross * num;
            _points[1] += cross * num;
            return _points;
        }

        private Vector3 GetNodeViewPosition(FsmNodeView node)
        {
            return _graphView.contentViewContainer.Matrix().MultiplyPoint(node.GetPosition().center);
        }

        private static void DrawArrows(Color color, Vector3 cross, Vector3[] edgePoints, bool isSelf, float arrowSize, float outlineWidth, float arrowLength)
        {
            Vector3 vector = edgePoints[1] - edgePoints[0];
            Vector3 normalized = vector.normalized;
            Vector3 vector2 = vector * 0.5f + edgePoints[0];
            vector2 -= cross * 0.5f;
            float num = Mathf.Min(arrowLength * 1.5f, (vector2 - edgePoints[0]).magnitude) * 0.66f;
            int num2 = 1;

            for (int i = 0; i < num2; i++)
            {
                Color color2 = color;

                Vector3 center = vector2 + (float)((num2 == 1) ? i : (i - 1)) * num * (isSelf ? cross : normalized);
                DrawArrow(color2, cross, normalized, center, arrowSize, outlineWidth);
            }
        }

        private static void DrawArrow(Color color, Vector3 cross, Vector3 direction, Vector3 center, float arrowSize, float outlineWidth)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Vector3[] array = new Vector3[4];
                array[0] = center + direction * arrowSize;
                array[1] = center - direction * arrowSize + cross * arrowSize;
                array[2] = center - direction * arrowSize - cross * arrowSize;
                array[3] = array[0];
                Handles.color = color;
                Handles.DrawAAPolyLine(outlineWidth, array);
                Handles.DrawAAConvexPolygon(array);

            }
        }

        public FsmTransitionData FindClosestTransition()
        {
            FsmTransitionData result = null;
            float num = float.PositiveInfinity;
            Vector3 vector = Event.current.mousePosition;

#if UNITY_6000_2_OR_NEWER
            vector.x -= _graphView.Window.position.width * _graphView.Window.PercentOfParam + 2;
            vector.y -= _graphView.Window.docked ? 44 : 46; // 20
#endif

            List<FsmTransitionData> transitions = Context.Instance.GetCurrentShowTransitionData();
            if (transitions == null || transitions.Count == 0)
            {
                return null;
            }

            foreach (var transition in transitions)
            {
                Vector3[] edgePoints = GetTransitionPoints(transition);

                if (edgePoints == null)
                {
                    continue;
                }

                float num2 = float.PositiveInfinity;
                num2 = ((!(edgePoints[0] == edgePoints[1])) ? HandleUtility.DistancePointLine(vector, edgePoints[0], edgePoints[1]) : Vector3.Distance(EdgeToSelfOffsetVector + edgePoints[0], vector));
                if (num2 < num && num2 < 10f)
                {
                    num = num2;
                    result = transition;
                }
            }

            return result;
        }

        public void OnTransitionSelection(FsmTransitionData transition)
        {
            EditorApplication.delayCall -= DelayCallSelection;
            if (this.Selection != null && transition == null)
            {
                FsmTransitionInspectorHelper.Instance.Inspect(Context.Instance.RuntimeFsmController, null);
            }

            this.Selection = transition;
            //Debug.Log("OnTransitionSelection");


            if (Selection != null)
            {
                EditorApplication.delayCall += DelayCallSelection;
            }

        }

        private void DrawMakeTransition()
        {
            if (!_graphView.IsMakingTransition)
            {
                return;
            }

            _vectors[0] = GetNodeViewPosition(_graphView.MakeTransitionFrom);

            if (_graphView.HoverNode != null && !_graphView.HoverNode.Data.isBuiltInState)
            {
                _vectors[1] = GetNodeViewPosition(_graphView.HoverNode);
            }
            else
            {
                _vectors[1] = Event.current.mousePosition;
#if UNITY_6000_2_OR_NEWER
                _vectors[1].x -= _graphView.Window.position.width * _graphView.Window.PercentOfParam + 2;
                _vectors[1].y -= _graphView.Window.docked ? 44 : 46; // 20
#endif
            }

            DrawTransiton(_vectors, Color.white, false, true);
        }


        /// <summary>
        /// 绘制当前状态切换的过渡
        /// </summary>
        private void DrawCurrentTransition(FsmTransitionData transition)
        {
            if (Context.Instance.FsmController == null)
            {
                return;
            }

            if (Context.Instance.FsmController.RuntimeFsmController == null)
            {
                return;
            }

            if (Context.Instance.RuntimeFsmController == null)
            {
                return;
            }

            float width = LineWidth;

            if (CloseTransition == transition)
            {
                width *= 1.5f;
            }

            Vector3[] edgePoints = GetTransitionPoints(transition);

            int index = Context.Instance.FsmController.RuntimeFsmController.IndexOf(Context.Instance.RuntimeFsmController);

            // 找到当前的过渡

            FsmTransition currentTransition = Context.Instance.FsmController.GetCurrentTransition(index);
            if (currentTransition == null)
            {
                //lastRealTime = Time.realtimeSinceStartup;
                return;
            }
            if (!transition.Key.Equals(currentTransition.Data.Key))
            {
                return;
            }

            if (_currentChangeTransition != currentTransition.Data)
            {
                _currentChangeTransition = currentTransition.Data;
                _currentChangeTransitionTimer = 0;
            }

            if (_currentChangeTransition.Key.Equals(currentTransition.Data.Key) && _currentChangeTransitionTimer >= 1)
            {
                //lastRealTime = Time.realtimeSinceStartup;
                return;
            }

            FsmStateNodeData from = Context.Instance.RuntimeFsmController.GetStateNodeData(_currentChangeTransition.fromStateName);

            if (from == null || from.Parent != Context.Instance.LayerParent)
            {
                _currentChangeTransitionTimer = 1;
                //lastRealTime = Time.realtimeSinceStartup;
                return;
            }


            _currentChangeTransitionTimer += _deltaTime * 3;

            if (edgePoints != null && edgePoints.Length >= 2)
            {
                Handles.color = SelectedEdgeColor;
                Handles.DrawAAPolyLine(width, edgePoints[0], Vector2.Lerp(edgePoints[0], edgePoints[1], _currentChangeTransitionTimer));
            }

        }

        private void DrawStateExitTransition()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (Context.Instance.FsmController == null)
            {
                return;
            }

            if (Context.Instance.FsmController.RuntimeFsmController == null)
            {
                return;
            }

            if (Context.Instance.RuntimeFsmController == null)
            {
                return;
            }

            float width = LineWidth;

            int index = Context.Instance.FsmController.RuntimeFsmController.IndexOf(Context.Instance.RuntimeFsmController);
            FsmStateNode stateNode = Context.Instance.FsmController.GetCurrentStateInfo(index, Context.Instance.LayerParent);

            if (_currentState != null && stateNode == null)
            {
                _isStateExiting = true;
                _exitStateParent = _currentState.Data.Parent;
                _exitStateName = _currentState.Data.name;
                _exitStateTimer = 0;

                List<FsmStateNodeData> nodes = Context.Instance.GetCurrentShowStateNodeData();

                if (nodes != null)
                {
                    foreach (var item in nodes)
                    {
                        if (item.isBuiltInState && item.builtInStateName.Equals(FsmConst.Up))
                        {
                            _exitStateToName = item.name;
                            break;
                        }
                    }
                }

            }

            if (_isStateExiting)
            {
                _exitStateTimer += Time.unscaledDeltaTime * 3;

                Handles.color = SelectedEdgeColor;
                Vector3[] edgePoints = GetTransitionPoints(_exitStateName, _exitStateToName);

                if (edgePoints != null)
                {
                    Handles.DrawAAPolyLine(width, edgePoints[0], Vector2.Lerp(edgePoints[0], edgePoints[1], _exitStateTimer));
                }

                if (_exitStateTimer >= 1 || !_exitStateParent.Equals(Context.Instance.LayerParent))
                {
                    _isStateExiting = false;
                    _exitStateParent = null;
                    _exitStateName = null;
                    _exitStateToName = null;
                    _exitStateTimer = 0;
                }
            }
            _currentState = stateNode;

        }

        private void DelayCallSelection()
        {
            FsmTransitionInspectorHelper.Instance.Inspect(Context.Instance.RuntimeFsmController, Selection);
        }

    }

}
