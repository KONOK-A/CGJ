using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    /// <summary>正式 Resource 工作台；保留窗口脚本身份，所有写操作仍经过正式安全服务。</summary>
    public class ResServiceEditorWindow : EditorWindow
    {
        [SerializeField] private ResourceProject project;
        [SerializeField] private string app = "app-v1";
        [SerializeField] private bool generate;
        [SerializeField] private bool force;
        [SerializeField] private string referenceDirectory = "Assets/YLFrameworkData";
        [SerializeField] private ResourceBuildRecipe selectedRecipe;
        [SerializeField] private string selectedRecipeGuid;
        [SerializeField] private ResourceWorkbenchLayout layout = new();
        private ResServiceView _resServiceView;
        private readonly List<ResourceProject> _projects = new();
        private Vector2 _settingsScroll;
        private string _selectionError;
        private Rect _projectPopupAnchor;
        private Rect _moreMenuAnchor;
        private Rect _morePopupAnchor;
        internal ResourceWorkbenchLayout Layout => layout ??= new ResourceWorkbenchLayout();
        internal void NavigateTask(int task) => _resServiceView?.NavigateTask(task);
        internal void ShowGuide() => _resServiceView?.ShowGuide();

        [MenuItem("YLFramework/Resource/Workbench %#r")]
        public static void OpenResServiceWindow()
        {
            var window = GetWindow<ResServiceEditorWindow>();
            window.titleContent = new GUIContent("YL Resource");
            window.minSize = new Vector2(620, 440);
            window.Show();
            if (Selection.activeObject is ResourceProject selected)
            {
                window.SetProject(selected);
            }
        }

        public void SetProject(ResourceProject value)
        {
            if (value)
            {
                ResourceEditorOperations.RequireEditableProject(value);
            }

            _resServiceView?.SetProject(value);
            project = value;
            _selectionError = null;
            Repaint();
        }

        /// <summary>只选择同一真实Recipe；不改Recipe.Project、不切Project、不执行或保存。</summary>
        public void SetRecipe(ResourceBuildRecipe value)
        {
            if (_resServiceView?.IsBusy == true)
            {
                throw new InvalidOperationException("排队或执行中不能切换Recipe。");
            }

            if (value)
            {
                ResourceEditorOperations.RequireRecipe(value);
            }

            var guid = value ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : null;
            _resServiceView?.SetRecipe(value, guid);
            selectedRecipe = value;
            selectedRecipeGuid = guid;
            Repaint();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("YL Resource");
            minSize = new Vector2(620, 440);
            _resServiceView = new ResServiceView { EditorWindow = this };
            try
            {
                _resServiceView.SetProject(project);
            }
            catch (Exception error)
            {
                project = null;
                _selectionError = error.Message;
            }
            RebindRecipe();
            RefreshProjects();
            EditorApplication.projectChanged += RefreshView;
            Undo.undoRedoPerformed += RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged += RefreshView;
            Selection.selectionChanged += Repaint;
            EditorApplication.update += TickDiagnostics;
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        private void RefreshProjects()
        {
            _projects.Clear();
            _projects.AddRange(AssetDatabase.FindAssets("t:ResourceProject")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(x => x).OrderBy(x => x.displayName ?? x.projectId, StringComparer.Ordinal));
        }

        private void RebindRecipe()
        {
            if (!string.IsNullOrEmpty(selectedRecipeGuid))
            {
                selectedRecipe = AssetDatabase.LoadAssetAtPath<ResourceBuildRecipe>(AssetDatabase.GUIDToAssetPath(selectedRecipeGuid));
            }

            _resServiceView?.SetRecipe(selectedRecipe, selectedRecipeGuid);
        }
        private void RefreshView()
        {
            RefreshProjects();
            RebindRecipe();
            _resServiceView?.Invalidate();
            Repaint();
        }
        private void TickDiagnostics() => _resServiceView?.TickDiagnostics();
        private void OnBecameInvisible() => _resServiceView?.PauseDiagnostics();
        private void OnPlayState(PlayModeStateChange _)
        {
            _resServiceView?.OnEditorTransition();
            _resServiceView?.Invalidate();
            Repaint();
        }

        public void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshView;
            Undo.undoRedoPerformed -= RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged -= RefreshView;
            Selection.selectionChanged -= Repaint;
            EditorApplication.update -= TickDiagnostics;
            EditorApplication.playModeStateChanged -= OnPlayState;
            _resServiceView?.OnDispose();
            _resServiceView = null;
        }

        private void OnGUI()
        {
            if (_resServiceView == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGui.ObjectBar);
            GUILayout.Space(6);
            using (new EditorGUI.DisabledScope(_resServiceView.IsExecuting))
            {
                var name = project ? (string.IsNullOrWhiteSpace(project.displayName) ? project.name : project.displayName) : "选择 ResourceProject";
                var tooltip = project ? name + "\nProjectId: " + project.projectId + "\n" + AssetDatabase.GetAssetPath(project) : "搜索资源项目名称、ProjectId和路径，或创建ResourceProject配置";
                var content = new GUIContent(name, tooltip);
                // 按名称自适应宽度，让ProjectId紧邻主名称，避免在宽窗中悬在中间。
                var buttonWidth = Mathf.Clamp(ResourceWorkbenchGui.Project.CalcSize(content).x + 34, 140, 320);
                if (GUILayout.Button(content, ResourceWorkbenchGui.Project, GUILayout.Width(buttonWidth)))
                {
                    PopupWindow.Show(_projectPopupAnchor.width > 0 ? _projectPopupAnchor : new Rect(Event.current.mousePosition, Vector2.zero), new ResourceProjectPicker(_projects.ToArray(), TrySelect, CreateProject,
                        () => this && _resServiceView != null && !_resServiceView.IsBusy));
                }

                if (Event.current.type == EventType.Repaint)
                {
                    _projectPopupAnchor = GUILayoutUtility.GetLastRect();
                }
            }
            if (project && position.width >= 960)
            {
                GUILayout.Space(8);
                GUILayout.Label(new GUIContent(project.projectId, "ProjectId: " + project.projectId + "\n可在更多菜单复制完整值"), ResourceWorkbenchGui.Identity, GUILayout.Width(160), GUILayout.Height(ResourceWorkbenchGui.BarHeight));
            }
            if (project && EditorUtility.IsDirty(project))
            {
                GUILayout.Space(8);
                GUILayout.Label("未保存 *", EditorStyles.boldLabel, GUILayout.Width(66));
            }
            GUILayout.FlexibleSpace();
            DrawProjectActions();
            GUILayout.Space(6);
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_selectionError))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(_selectionError, MessageType.Error);
                if (GUILayout.Button("清除提示", GUILayout.Width(74)))
                {
                    _selectionError = null;
                }

                EditorGUILayout.EndHorizontal();
            }
            _resServiceView.ReferenceDirectory = referenceDirectory;
            _resServiceView.OnGUI(ref app, ref generate, ref force, position.width - 16);
            referenceDirectory = _resServiceView.ReferenceDirectory;
            // 控件先处理点击；仅剩余的空白左键事件释放文本焦点，不清资源选择。
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && GUIUtility.hotControl == 0)
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = 0;
                EditorGUIUtility.editingTextField = false;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawProjectActions()
        {
            using (new EditorGUI.DisabledScope(!_resServiceView.CanEdit))
            {
                if (GUILayout.Button(new GUIContent("保存", "保存当前资源项目；图集规则和构建配方使用各自的保存按钮"), ResourceWorkbenchGui.ToolbarButton, GUILayout.Width(58)))
                {
                    _resServiceView.SaveProject();
                }
            }
            using (new EditorGUI.DisabledScope(!project || _resServiceView.IsExecuting))
            {
                if (GUILayout.Button(Layout.settings ? "返回工作区" : "项目设置", ResourceWorkbenchGui.ToolbarButton, GUILayout.Width(82)))
                {
                    Layout.settings = !Layout.settings;
                    GUI.FocusControl(null);
                }
            }

            if (GUILayout.Button("更多", ResourceWorkbenchGui.Menu, GUILayout.Width(54), GUILayout.Height(ResourceWorkbenchGui.BarHeight)))
            {
                var button = _moreMenuAnchor.width > 0 ? _moreMenuAnchor : new Rect(position.width - 60, 0, 54, 26);
                PopupWindow.Show(ResourceProjectActionsPopup.GetAnchor(button), new ResourceProjectActionsPopup(project, error =>
                {
                    if (this)
                    {
                        _selectionError = error;
                        Repaint();
                    }
                }));
            }
            if (Event.current.type == EventType.Repaint)
            {
                _moreMenuAnchor = GUILayoutUtility.GetLastRect();
                _morePopupAnchor = ResourceProjectActionsPopup.GetAnchor(_moreMenuAnchor);
            }
        }

        internal void DrawSettings(float width)
        {
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("项目设置", ResourceWorkbenchGui.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("返回工作区", ResourceWorkbenchGui.Button, GUILayout.Width(100)))
            {
                Layout.settings = false;
            }

            EditorGUILayout.EndHorizontal();
            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(640));
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 130;
            using (new EditorGUI.DisabledScope(!_resServiceView.CanEdit))
            using (var serialized = new SerializedObject(project))
            {
                serialized.Update();
                GUILayout.Label("资源项目身份", EditorStyles.boldLabel);
                ResourceWorkbenchGui.Property(serialized.FindProperty("displayName"), new GUIContent("显示名称"));
                ResourceWorkbenchGui.Property(serialized.FindProperty("projectId"), new GUIContent("ProjectId"));
                GUILayout.Label("显示名称用于界面；ProjectId用于运行时地址和输出识别。修改不会迁移旧发布或缓存。", ResourceWorkbenchGui.Secondary);
                GUILayout.Space(12);
                var rootError = ResourceWorkbenchGui.ResourceDirectoriesFields(serialized);
                if (rootError != null)
                {
                    _selectionError = rootError;
                }

                var generated = ResourceProjectPaths.GeneratedDirectory(project);
                GUILayout.Space(12);
                GUILayout.Label("生成目录（可选）", EditorStyles.boldLabel);
                if (string.IsNullOrEmpty(generated))
                {
                    GUILayout.Label("尚未登记。不使用自动图集时，可以暂不设置；需要时请在‘资源配置 > 图集’中新建并登记。", ResourceWorkbenchGui.Secondary);
                }
                else
                {
                    ResourceWorkbenchGui.CopyLine("自动图集保存位置", generated, Mathf.Min(width, 640));
                }

                GUILayout.Space(12);
                GUILayout.Label("构建策略", EditorStyles.boldLabel);
                ResourceWorkbenchGui.Property(serialized.FindProperty("compression"), new GUIContent("Bundle 压缩"));
                ResourceWorkbenchGui.Property(serialized.FindProperty("contentEncryption"), new GUIContent("内容加密"));
                GUILayout.Label("None 表示不加密，不表示不压缩。初次使用不需要配方、图集或签发。", ResourceWorkbenchGui.Secondary);
                GUI.SetNextControlName("Resource.ContentKeyId");
                ResourceWorkbenchGui.Property(serialized.FindProperty("contentKeyId"), new GUIContent("KeyId（非密钥）", "加密时使用的非秘密标识；密钥由外部提供，不保存在工程内。"));
                if (serialized.ApplyModifiedProperties())
                {
                    _selectionError = null;
                    _resServiceView.Invalidate();
                }
            }
            ResourceWorkbenchGui.DirectoryValidation(project);
            EditorGUIUtility.labelWidth = labelWidth;
            GUILayout.Space(12);
            ResourceWorkbenchGui.CopyLine("配置路径", AssetDatabase.GetAssetPath(project), Mathf.Min(width, 640));
            ResourceWorkbenchGui.CopyLine("配置GUID", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)), Mathf.Min(width, 640));
            if (GUILayout.Button("在Inspector中打开资源项目"))
            {
                Selection.activeObject = project;
                EditorGUIUtility.PingObject(project);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void CreateProject()
        {
            if (_resServiceView == null || _resServiceView.IsBusy || !GUI.enabled)
            {
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject("创建资源项目配置", "ResourceProject", "asset", "选择ResourceProject配置资产的保存位置。此操作不会移动源资源，也不会创建生成目录。");
            if (path.Length == 0)
            {
                return;
            }

            try
            {
                SetProject(ResourceEditorOperations.CreateProject(path));
                RefreshProjects();
            }
            catch (Exception error)
            {
                _selectionError = error.Message;
            }
        }

        internal void ClearSelectionError() => _selectionError = null;

        private void TrySelect(ResourceProject value)
        {
            try
            {
                SetProject(value);
            }
            catch (Exception error)
            {
                _selectionError = error.Message;
                Repaint();
            }
        }
    }
}
