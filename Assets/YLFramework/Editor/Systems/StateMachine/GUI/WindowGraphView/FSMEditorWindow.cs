using YLFramework.Core.Editor;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YLFramework.StateMachine
{

    public class FSMEditorWindow : UnityEditor.EditorWindow
    {

        #region 字段

        private static FSMEditorWindow _instance;

        private float percent_of_param = 0.3f; // 参数所占用的比例

        private VisualElement left = null;
        private VisualElement middle = null;
        private VisualElement right = null;

        private Rect paramResizeArea;

        const float ResizeWidth = 5;

        private bool isResizingParamArea = false; // 是否正在调整参数区域

        private Rect stateRect;

        private FSMStateGraphView graphGUI = null;

        private ParamLayer paramLayer = null;

        private IMGUIContainer state_top_container;
        private IMGUIContainer state_bottom_container;

        private Vector3 splitLineStart;
        private Vector3 splitLineEnd;

        private string parentLayer;

        private int lastRepaintCount = 0;

        #endregion

        #region 属性

        public float PercentOfParam => percent_of_param;

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
            var controller = Context.Instance.RuntimeFSMController;
            if (controller) controller.ClearCache();
            graphGUI?.RefreshNodes();
            Repaint();
        }


        private void InitGUI()
        {
            rootVisualElement.Clear();

            ClearInspetor();

            rootVisualElement.style.flexDirection = FlexDirection.Row;

            left = new VisualElement();

            IMGUIContainer param_container = new IMGUIContainer();
            param_container.onGUIHandler += () =>
            {
                if (paramLayer == null)
                {
                    paramLayer = new ParamLayer(this);
                }

                paramLayer.OnGUI(param_container.layout);
                paramLayer.ProcessEvents();
            };
            param_container.StretchToParentSize();
            left.Add(param_container);

            middle = new VisualElement();
            right = new VisualElement();


            state_top_container = new IMGUIContainer();
            state_top_container.onGUIHandler += () =>
            {
                DoGraphToolbar(state_top_container.layout);
            };
            right.Add(state_top_container);

            graphGUI = new FSMStateGraphView();
            graphGUI.Window = this;
            //graphGUI.StretchToParentSize();
            right.Add(graphGUI);

            state_bottom_container = new IMGUIContainer();
            state_bottom_container.onGUIHandler += () =>
            {
                DrawButtom(state_bottom_container.layout);
            };
            right.Add(state_bottom_container);

            rootVisualElement.Add(left);
            rootVisualElement.Add(middle);
            rootVisualElement.Add(right);


            middle.RegisterCallback<MouseDownEvent>((e) =>
            {
                if (e != null && e.button == 0)
                {
                    isResizingParamArea = true;
                }
            });

            rootVisualElement.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);

        }

        private void ClearInspetor()
        {
            // 清空当前的Inspector
            if (Selection.activeObject == null)
                return;

            if (Selection.activeObject.GetType() == typeof(FSMStateInspectorHelper) ||
                    Selection.activeObject.GetType() == typeof(FSMTransitionInspectorHelper))
            {
                Selection.activeObject = null;
            }

        }

        private void Update()
        {
            lastRepaintCount++;
            if (lastRepaintCount >= 10)
            {
                Repaint();
                lastRepaintCount = 0;
            }

            if (graphGUI != null)
                graphGUI.Update();

        }

        private void OnGUI()
        {
            wantsMouseEnterLeaveWindow = true;
            wantsMouseMove = true;

            stateRect.Set(this.position.width * percent_of_param + 2, 20, this.position.width * (1 - percent_of_param), position.height - 15);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(stateRect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.7f, 0.7f, 0.7f));

            if (left == null)
            {
                InitGUI();
            }

            ParamAreaResize();

            left.style.width = this.position.width * percent_of_param - ResizeWidth / 2;
            left.style.height = this.position.height;

            middle.style.width = ResizeWidth;
            middle.style.height = this.position.height;

            right.style.width = this.position.width * (1 - percent_of_param) - ResizeWidth / 2;
            right.style.height = this.position.height;


            state_top_container.style.width = right.style.width;
            state_top_container.style.height = 20;

            graphGUI.style.width = right.style.width;
            graphGUI.style.height = this.position.height - 35;


            state_bottom_container.style.width = right.style.width;
            state_bottom_container.style.height = 15;

            splitLineStart.Set(this.position.width * percent_of_param + 2, 0, 0);
            splitLineEnd.Set(this.position.width * percent_of_param + 2, this.position.height, 0);
            Handles.color = Color.black;
            Handles.DrawAAPolyLine(2, splitLineStart, splitLineEnd);
            var controller = Context.Instance.RuntimeFSMController;
            if (controller && !controller.ValidateConfiguration(out string error))
                EditorGUI.HelpBox(new Rect(position.width * percent_of_param + 10, 25, Mathf.Max(100, stateRect.width - 20), 42), $"Asset '{controller.name}': {error}", MessageType.Error);




            // 取消拖拽
            if (Event.current.type == EventType.MouseLeaveWindow && Event.current.button == 0)
            {
                isResizingParamArea = false;
            }


            _instance = this;
        }


        private void ParamAreaResize()
        {
            paramResizeArea.Set(this.position.width * percent_of_param - ResizeWidth / 2, 0, ResizeWidth, position.height);

            EditorGUIUtility.AddCursorRect(paramResizeArea, MouseCursor.ResizeHorizontal);

            if (paramResizeArea.Contains(Event.current.mousePosition))
            {
                Vector3 start = paramResizeArea.center - new Vector2(-1, paramResizeArea.height / 2);
                Vector3 end = paramResizeArea.center + new Vector2(1, paramResizeArea.height / 2);
                Handles.color = isResizingParamArea ? Color.green : Color.white;
                Handles.DrawLine(start, end);
            }

            if (isResizingParamArea)
            {
                percent_of_param = Mathf.Clamp(Event.current.mousePosition.x / position.width, 0.1f, 0.5f);
                if (this.position.width * percent_of_param < 150)
                    percent_of_param = 150 / this.position.width;
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

            if (parentLayer != Context.Instance.LayerParent && graphGUI != null)
            {
                graphGUI.RefreshNodes();
                parentLayer = Context.Instance.LayerParent;
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
            GUIStyle gUIStyle = StateStyles.Named(index == 0 ? "GUIEditor.BreadcrumbLeftBackground" : "GUIEditor.BreadcrumbMidBackground", EditorStyles.helpBox);
            GUIContent content = new GUIContent(name);
            Rect breadcrumbLayoutRect = GetBreadcrumbLayoutRect(content, style);
            if (Event.current.type == EventType.Repaint)
            {
                gUIStyle.Draw(breadcrumbLayoutRect, GUIContent.none, 0);
            }

            GUI.Toggle(breadcrumbLayoutRect, index == maxCount - 1, content, style);
        }


        private void DrawButtom(Rect rect)
        {
            rect.Set(0, 0, rect.width, rect.height);
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            if (Context.Instance.FSMController != null)
            {
                if (GUILayout.Button(Context.Instance.FSMController.name, "ShurikenLabel"))
                    EditorGUIUtility.PingObject(Context.Instance.FSMController.gameObject);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(AssetDatabase.GetAssetPath(Context.Instance.RuntimeFSMController), "ShurikenLabel"))
                EditorGUIUtility.PingObject(Context.Instance.RuntimeFSMController);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }


        private void OnMouseUpEvent(MouseUpEvent e)
        {
            if (e != null && e.button == 0)
            {
                isResizingParamArea = false;
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
                return;
            _instance.ShowNotification(new GUIContent(message));
        }

    }
}
