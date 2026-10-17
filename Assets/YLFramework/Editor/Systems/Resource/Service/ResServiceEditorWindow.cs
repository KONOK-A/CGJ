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
        [SerializeField] private bool showProjects = true;
        private ResServiceView _resServiceView;
        private readonly List<ResourceProject> projects = new();
        private Vector2 projectScroll;
        private string projectSearch = "";
        private string selectionError;

        [MenuItem("YLFramework/Resource/Workbench %#r")]
        public static void OpenResServiceWindow()
        {
            var window = GetWindow<ResServiceEditorWindow>();
            window.titleContent = new GUIContent("YL Resource");
            window.minSize = new Vector2(620, 440); window.Show();
            if (Selection.activeObject is ResourceProject selected) window.SetProject(selected);
        }

        public void SetProject(ResourceProject value)
        {
            if (value) ResourceEditorOperations.RequireEditableProject(value);
            _resServiceView?.SetProject(value); project = value; selectionError = null;
            RefreshProjects(); Repaint();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("YL Resource");
            minSize = new Vector2(620, 440);
            _resServiceView = new ResServiceView { EditorWindow = this };
            try { _resServiceView.SetProject(project); }
            catch (Exception error) { project = null; selectionError = error.Message; }
            RefreshProjects();
            EditorApplication.projectChanged += RefreshView;
            Undo.undoRedoPerformed += RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged += RefreshView;
            Selection.selectionChanged += Repaint;
            EditorApplication.update += TickDiagnostics;
        }

        private void RefreshProjects()
        {
            projects.Clear();
            projects.AddRange(AssetDatabase.FindAssets("t:ResourceProject")
                .Select(guid => AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(x => x).OrderBy(x => x.DisplayName ?? x.ProjectId, StringComparer.Ordinal));
        }

        private void RefreshView() { RefreshProjects(); _resServiceView?.Invalidate(); Repaint(); }
        private void TickDiagnostics() => _resServiceView?.TickDiagnostics();

        public void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshView; Undo.undoRedoPerformed -= RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged -= RefreshView; Selection.selectionChanged -= Repaint;
            EditorApplication.update -= TickDiagnostics;
            _resServiceView?.OnDispose(); _resServiceView = null;
        }

        private void OnGUI()
        {
            if (_resServiceView == null) return;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            showProjects = GUILayout.Toggle(showProjects, new GUIContent("Projects", "展开项目导航"), EditorStyles.toolbarButton, GUILayout.Width(72));
            var next = (ResourceProject)EditorGUILayout.ObjectField(project, typeof(ResourceProject), false);
            if (next != project) TrySelect(next);
            if (GUILayout.Button("选择项目", EditorStyles.toolbarDropDown, GUILayout.Width(85))) ProjectMenu();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(48))) CreateProject();
            EditorGUILayout.EndHorizontal();
            if (showProjects && position.width < 1000)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUI.SetNextControlName("Resource.ProjectSearch");
                projectSearch = EditorGUILayout.TextField(projectSearch, EditorStyles.toolbarSearchField);
                GUILayout.Label(projects.Count(x => x && MatchesProject(x)) + " Projects", EditorStyles.miniLabel, GUILayout.Width(75));
                if (GUILayout.Button("搜索结果", EditorStyles.toolbarDropDown, GUILayout.Width(85))) ProjectMenu();
                EditorGUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(selectionError)) EditorGUILayout.HelpBox(selectionError, MessageType.Error);
            EditorGUILayout.BeginHorizontal();
            if (showProjects && position.width >= 1000) DrawProjects();
            EditorGUILayout.BeginVertical();
            _resServiceView.OnGUI(ref app, ref generate, ref force, position.width - (showProjects && position.width >= 1000 ? 225 : 0) - 12);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProjects()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(215), GUILayout.ExpandHeight(true));
            GUILayout.Label("RESOURCE PROJECTS", EditorStyles.miniBoldLabel);
            GUI.SetNextControlName("Resource.ProjectSearch");
            projectSearch = EditorGUILayout.TextField(projectSearch, EditorStyles.toolbarSearchField);
            projectScroll = EditorGUILayout.BeginScrollView(projectScroll);
            var visible = projects.Where(x => x && MatchesProject(x)).ToArray();
            if (visible.Length == 0) EditorGUILayout.HelpBox(projects.Count == 0 ? "尚无正式 ResourceProject。新建配置后选择专用资源根。" : "没有匹配的项目。", MessageType.Info);
            foreach (var item in visible)
            {
                var label = string.IsNullOrWhiteSpace(item.DisplayName) ? item.ProjectId : item.DisplayName;
                var content = new GUIContent(label + "\n" + item.ProjectId, label + "\nProject ID: " + item.ProjectId + "\n" + AssetDatabase.GetAssetPath(item));
                var style = new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, wordWrap = true, fixedHeight = 46 };
                if (GUILayout.Toggle(item == project, content, style) && item != project) TrySelect(item);
            }
            EditorGUILayout.EndScrollView();
            GUILayout.Label("仅选择正式 Project；不读取旧 Importer。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private bool MatchesProject(ResourceProject item) => (item.ProjectId ?? "").IndexOf(projectSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (item.DisplayName ?? "").IndexOf(projectSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
            AssetDatabase.GetAssetPath(item).IndexOf(projectSearch, StringComparison.OrdinalIgnoreCase) >= 0;

        private void ProjectMenu()
        {
            var menu = new GenericMenu();
            foreach (var item in projects.Where(x => x && MatchesProject(x)))
            {
                var captured = item;
                menu.AddItem(new GUIContent(item.ProjectId + " / " + AssetDatabase.GetAssetPath(item)), item == project, () => TrySelect(captured));
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("没有匹配的正式 Project"));
            menu.ShowAsContext();
        }

        private void CreateProject()
        {
            var path = EditorUtility.SaveFilePanelInProject("创建自有 ResourceProject", "ResourceProject", "asset", "请选择明确属于新 YL 的配置位置，不使用原游戏或常量专用目录。");
            if (path.Length == 0) return;
            try { SetProject(ResourceEditorOperations.CreateProject(path)); RefreshProjects(); }
            catch (Exception error) { selectionError = error.Message; }
        }

        private void TrySelect(ResourceProject value)
        {
            try { SetProject(value); }
            catch (Exception error) { selectionError = error.Message; Repaint(); }
        }
    }
}
