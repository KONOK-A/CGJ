using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YLFramework.Editor.StateMachine
{

    public class FsmEditorWindow : UnityEditor.EditorWindow
    {

        #region 字段

        private static FsmEditorWindow _instance;

        private float _percentOfParam = 0.3f; // 参数所占用的比例

        private VisualElement _left = null;
        private VisualElement _middle = null;
        private VisualElement _right = null;

        private Rect _paramResizeArea;

        const float ResizeWidth = 5;

        private bool _isResizingParamArea = false; // 是否正在调整参数区域

        private Rect _stateRect;

        private FsmStateGraphView _graphGui = null;

        private ParamLayer _paramLayer = null;

        private IMGUIContainer _stateTopContainer;
        private IMGUIContainer _stateBottomContainer;

        private Vector3 _splitLineStart;
        private Vector3 _splitLineEnd;

        private string _parentLayer;

        private int _lastRepaintCount = 0;

        #endregion

        #region 属性

        public float PercentOfParam => _percentOfParam;

        #endregion

        // Fix编码

        private void Awake()
        {
            try
            {
                this.titleContent = new GUIContent("YL.AssetFSM", EditorGUIUtility.IconContent("AnimatorController Icon").image);
            }
            catch (System.Exception)
            {
                this.titleContent = new GUIContent("YL.AssetFSM");
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            var controller = Context.Instance.RuntimeFsmController;
            if (controller)
            {
                controller.ClearCache();
            }

            _graphGui?.RefreshNodes();
            Repaint();
        }


        private void InitGui()
        {
            rootVisualElement.Clear();

            ClearInspetor();

            rootVisualElement.style.flexDirection = FlexDirection.Row;

            _left = new VisualElement();

            IMGUIContainer paramContainer = new IMGUIContainer();
            paramContainer.onGUIHandler += () =>
            {
                if (_paramLayer == null)
                {
                    _paramLayer = new ParamLayer(this);
                }

                _paramLayer.OnGUI(paramContainer.layout);
                _paramLayer.ProcessEvents();
            };
            paramContainer.StretchToParentSize();
            _left.Add(paramContainer);

            _middle = new VisualElement();
            _right = new VisualElement();


            _stateTopContainer = new IMGUIContainer();
            _stateTopContainer.onGUIHandler += () =>
            {
                DoGraphToolbar(_stateTopContainer.layout);
            };
            _right.Add(_stateTopContainer);

            _graphGui = new FsmStateGraphView();
            _graphGui.Window = this;
            //graphGUI.StretchToParentSize();
            _right.Add(_graphGui);

            _stateBottomContainer = new IMGUIContainer();
            _stateBottomContainer.onGUIHandler += () =>
            {
                DrawButtom(_stateBottomContainer.layout);
            };
            _right.Add(_stateBottomContainer);

            rootVisualElement.Add(_left);
            rootVisualElement.Add(_middle);
            rootVisualElement.Add(_right);


            _middle.RegisterCallback<MouseDownEvent>((e) =>
            {
                if (e != null && e.button == 0)
                {
                    _isResizingParamArea = true;
                }
            });

            rootVisualElement.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);

        }

        private void ClearInspetor()
        {
            // 清空当前的Inspector
            if (Selection.activeObject == null)
            {
                return;
            }

            if (Selection.activeObject.GetType() == typeof(FsmStateInspectorHelper) ||
                    Selection.activeObject.GetType() == typeof(FsmTransitionInspectorHelper))
            {
                Selection.activeObject = null;
            }

        }

        private void Update()
        {
            _lastRepaintCount++;
            if (_lastRepaintCount >= 10)
            {
                Repaint();
                _lastRepaintCount = 0;
            }

            if (_graphGui != null)
            {
                _graphGui.Update();
            }
        }

        private void OnGUI()
        {
            wantsMouseEnterLeaveWindow = true;
            wantsMouseMove = true;

            _stateRect.Set(this.position.width * _percentOfParam + 2, 20, this.position.width * (1 - _percentOfParam), position.height - 15);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(_stateRect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.7f, 0.7f, 0.7f));
            }

            if (_left == null)
            {
                InitGui();
            }

            ParamAreaResize();

            _left.style.width = this.position.width * _percentOfParam - ResizeWidth / 2;
            _left.style.height = this.position.height;

            _middle.style.width = ResizeWidth;
            _middle.style.height = this.position.height;

            _right.style.width = this.position.width * (1 - _percentOfParam) - ResizeWidth / 2;
            _right.style.height = this.position.height;


            _stateTopContainer.style.width = _right.style.width;
            _stateTopContainer.style.height = 20;

            _graphGui.style.width = _right.style.width;
            _graphGui.style.height = this.position.height - 35;


            _stateBottomContainer.style.width = _right.style.width;
            _stateBottomContainer.style.height = 15;

            _splitLineStart.Set(this.position.width * _percentOfParam + 2, 0, 0);
            _splitLineEnd.Set(this.position.width * _percentOfParam + 2, this.position.height, 0);
            Handles.color = Color.black;
            Handles.DrawAAPolyLine(2, _splitLineStart, _splitLineEnd);
            var controller = Context.Instance.RuntimeFsmController;
            if (controller && !controller.ValidateConfiguration(out string error))
            {
                EditorGUI.HelpBox(new Rect(position.width * _percentOfParam + 10, 25, Mathf.Max(100, _stateRect.width - 20), 42), $"Asset '{controller.name}': {error}", MessageType.Error);
            }




            // 取消拖拽
            if (Event.current.type == EventType.MouseLeaveWindow && Event.current.button == 0)
            {
                _isResizingParamArea = false;
            }


            _instance = this;
        }


        private void ParamAreaResize()
        {
            _paramResizeArea.Set(this.position.width * _percentOfParam - ResizeWidth / 2, 0, ResizeWidth, position.height);

            EditorGUIUtility.AddCursorRect(_paramResizeArea, MouseCursor.ResizeHorizontal);

            if (_paramResizeArea.Contains(Event.current.mousePosition))
            {
                Vector3 start = _paramResizeArea.center - new Vector2(-1, _paramResizeArea.height / 2);
                Vector3 end = _paramResizeArea.center + new Vector2(1, _paramResizeArea.height / 2);
                Handles.color = _isResizingParamArea ? Color.green : Color.white;
                Handles.DrawLine(start, end);
            }

            if (_isResizingParamArea)
            {
                _percentOfParam = Mathf.Clamp(Event.current.mousePosition.x / position.width, 0.1f, 0.5f);
                if (this.position.width * _percentOfParam < 150)
                {
                    _percentOfParam = 150 / this.position.width;
                }
            }

        }

        private void DoGraphToolbar(Rect toolbarRect)
        {
            Handles.color = new Color(0, 0, 0, 0.4f);
            Handles.DrawLine(new Vector3(toolbarRect.x, 0), new Vector3(toolbarRect.x + toolbarRect.width, 0));

            GUILayout.BeginArea(toolbarRect);
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            BreadCrumb(0, Context.Instance.Layers.Count + 1, "Base Layer");
            if (EditorGUI.EndChangeCheck() && Context.Instance.Layers.Count > 0)
            {
                Context.Instance.RemoveLayer(0, Context.Instance.Layers.Count);
            }

            for (int i = 0; i < Context.Instance.Layers.Count; i++)
            {
                EditorGUI.BeginChangeCheck();
                BreadCrumb(i + 1, Context.Instance.Layers.Count + 1, Context.Instance.Layers[i]);
                if (EditorGUI.EndChangeCheck())
                {
                    Context.Instance.RemoveLayer(i + 1, Context.Instance.Layers.Count - 1 - i);
                }
            }

            if (_parentLayer != Context.Instance.LayerParent && _graphGui != null)
            {
                _graphGui.RefreshNodes();
                _parentLayer = Context.Instance.LayerParent;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static Rect GetBreadcrumbLayoutRect(GUIContent content, GUIStyle style)
        {
            Texture image = content.image;
            content.image = null;
            Vector2 vector = style.CalcSize(content);
            content.image = image;
            if (image != null)
            {
                vector.x += vector.y;
            }

            return GUILayoutUtility.GetRect(content, style, GUILayout.MaxWidth(vector.x));
        }

        private void BreadCrumb(int index, int maxCount, string name)
        {
            GUIStyle style = StateStyles.Named(index == 0 ? "GUIEditor.BreadcrumbLeft" : "GUIEditor.BreadcrumbMid", EditorStyles.miniButton);
            GUIStyle guiStyle = StateStyles.Named(index == 0 ? "GUIEditor.BreadcrumbLeftBackground" : "GUIEditor.BreadcrumbMidBackground", EditorStyles.helpBox);
            GUIContent content = new GUIContent(name);
            Rect breadcrumbLayoutRect = GetBreadcrumbLayoutRect(content, style);
            if (Event.current.type == EventType.Repaint)
            {
                guiStyle.Draw(breadcrumbLayoutRect, GUIContent.none, 0);
            }

            GUI.Toggle(breadcrumbLayoutRect, index == maxCount - 1, content, style);
        }


        private void DrawButtom(Rect rect)
        {
            rect.Set(0, 0, rect.width, rect.height);
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            if (Context.Instance.FsmController != null)
            {
                if (GUILayout.Button(Context.Instance.FsmController.name, "ShurikenLabel"))
                {
                    EditorGUIUtility.PingObject(Context.Instance.FsmController.gameObject);
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(AssetDatabase.GetAssetPath(Context.Instance.RuntimeFsmController), "ShurikenLabel"))
            {
                EditorGUIUtility.PingObject(Context.Instance.RuntimeFsmController);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }


        private void OnMouseUpEvent(MouseUpEvent e)
        {
            if (e != null && e.button == 0)
            {
                _isResizingParamArea = false;
            }

        }


        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ClearInspetor();
            _instance = null;
        }

        public static void ShowNotification(string message)
        {
            if (_instance == null)
            {
                return;
            }

            _instance.ShowNotification(new GUIContent(message));
        }

    }
}
