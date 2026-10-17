using System;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>只保存 UI 选择及可失效快照，不拥有 Runtime 状态或第二份发布目录。</summary>
    public sealed class ResServiceView
    {
        public EditorWindow EditorWindow { get; set; }
        private ResourceProject project;
        private string projectGuid;
        private ResourceCatalog catalog;
        private ResourceEditorSimulation simulation;
        private ResourceBuildMap map;
        private string status;
        private MessageType statusType = MessageType.Info;
        private EditorApplication.CallbackFunction pending;
        private readonly ResourceDefinitionView definition = new();
        private readonly ResourceBuildView build = new();
        private readonly ResourceDiagnosticsView diagnostics = new();
        private int tab;
        private Vector2 scroll;
        private static readonly string[] Tabs = { "定义与条目", "分包与依赖", "构建与发布", "Diagnostics" };
        internal bool IsBusy => pending != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;

        public void SetProject(ResourceProject value)
        {
            if (value) ResourceEditorOperations.RequireEditableProject(value);
            CancelPending();
            var guid = value ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : null;
            if (guid != projectGuid) { definition.Reset(); build.Reset(); scroll = Vector2.zero; }
            projectGuid = guid; project = value;
            Invalidate(); status = null;
        }

        public void Invalidate()
        {
            catalog = null; simulation = null; map = null;
            definition.Invalidate(); build.InvalidatePreview();
        }

        public void RefreshPreview()
        {
            if (!project) throw new InvalidOperationException("请先选择正式 Project。");
            map = ResourceBuildMap.Create(project);
            simulation = new ResourceEditorSimulation(new[] { project });
            catalog = simulation.GetCatalog(project.ProjectId);
            definition.SetPreview(map, catalog, simulation);
            build.SetPreview(map, project);
        }

        public void OnGUI(ref string app, ref bool generate, ref bool force, float availableWidth = 0)
        {
            HandleKeyboard();
            var next = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(27));
            if (next != tab) { tab = next; GUI.FocusControl(null); scroll = Vector2.zero; }
            if (pending != null)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("操作已排队。切换项目或关闭窗口将取消；native 构建开始后无法中途撤销。", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("取消排队", GUILayout.Width(75))) { CancelPending(); status = "排队操作已取消，未创建输出。"; }
                EditorGUILayout.EndHorizontal();
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                EditorGUILayout.HelpBox(EditorApplication.isCompiling ? "Unity 正在编译，定义与构建写操作暂不可用。" : "AssetDatabase 正在导入，等待稳定后再刷新预览或构建。", MessageType.Info);
            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(status, statusType);
                if (GUILayout.Button("清除", EditorStyles.miniButton, GUILayout.Width(40))) status = null;
                EditorGUILayout.EndHorizontal();
            }
            if (tab == 3) { diagnostics.Draw(); return; }
            if (!project)
            {
                EditorGUILayout.HelpBox("选择或新建 ResourceProject，指定专用资源根，再按 Group → Entries → 分包预览 → 校验 → 构建 → 本地发布 → 常量生成完成工作流。不会接管旧 Importer、XFAB 或未知输出。", MessageType.Info);
                return;
            }
            try { ResourceEditorOperations.RequireEditableProject(project); }
            catch (Exception error) { CancelPending(); EditorGUILayout.HelpBox(error.Message, MessageType.Error); return; }
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(project.DisplayName) ? project.ProjectId : project.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(EditorUserBuildSettings.activeBuildTarget.ToString(), EditorStyles.miniLabel);
            if (EditorUtility.IsDirty(project)) GUILayout.Label("未保存", EditorStyles.miniBoldLabel);
            using (new EditorGUI.DisabledScope(IsBusy))
            {
                if (GUILayout.Button(new GUIContent("保存定义", "Ctrl+S；只保存当前 Project，不保存场景"), GUILayout.Width(78))) Save();
                if (GUILayout.Button(new GUIContent("刷新预览", "只读 AssetDatabase 预估，不构建、不初始化 Runtime"), GUILayout.Width(78))) QueueOperation(() => { RefreshPreview(); return "预估目录已刷新；尚非真实 Bundle 产物。"; });
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(IsBusy || !AssetDatabase.IsOpenForEdit(project)))
            {
                if (tab == 0) definition.Draw(project, EditorWindow, availableWidth > 0 ? availableWidth : EditorWindow.position.width - 12, Invalidate, RunOperation);
                else
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    if (tab == 1) build.DrawPreview(() => QueueOperation(() => { RefreshPreview(); return "已更新预估分包。"; }));
                    else build.Draw(project, ref app, ref generate, ref force, QueueOperation, RebindProject);
                    EditorGUILayout.EndScrollView();
                }
            }
            GUILayout.Label("定义编辑支持 Undo/Redo。Alt+G 新增组 · Ctrl+F 条目搜索 · Ctrl+S 保存 · Esc 取消排队/清除搜索。", EditorStyles.wordWrappedMiniLabel);
        }

        private void HandleKeyboard()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown) return;
            if (current.keyCode == KeyCode.Escape)
            {
                CancelPending(); definition.ClearSearch(); GUI.FocusControl(null); current.Use(); return;
            }
            if ((current.control || current.command) && current.keyCode == KeyCode.F)
            {
                tab = 0; definition.FocusSearch(); current.Use(); return;
            }
            if (!project || IsBusy) return;
            if ((current.control || current.command) && current.keyCode == KeyCode.S) { Save(); current.Use(); }
            else if (current.alt && current.keyCode == KeyCode.G)
            {
                tab = 0; GUI.FocusControl(null);
                RunOperation(() => { definition.AddGroup(project); Invalidate(); return "已新增空 Group；选择条目后保存定义。"; });
                current.Use();
            }
        }

        private void Save() => RunOperation(() => { GUI.FocusControl(null); ResourceEditorOperations.SaveProject(project); return "Project 定义已保存；未保存或修改任何 Scene。"; });

        private void RebindProject(ResourceBuildResult result)
        {
            var guid = result?.Map.ProjectGuid ?? projectGuid;
            if (string.IsNullOrEmpty(guid)) return;
            var current = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
            if (EditorWindow is ResServiceEditorWindow window) window.SetProject(current); else SetProject(current);
        }

        private void RunOperation(Func<string> operation)
        {
            try { status = operation(); statusType = MessageType.Info; }
            catch (Exception error) { status = "操作未完成：" + error.Message; statusType = MessageType.Error; }
            if (EditorWindow) EditorWindow.Repaint();
        }

        private void QueueOperation(Func<string> operation)
        {
            if (pending != null) return;
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                EditorApplication.update -= callback;
                if (!ReferenceEquals(pending, callback)) return;
                pending = null;
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    status = "Unity 状态已变化，排队操作已取消。待 Play、编译或导入结束后重新执行。";
                    statusType = MessageType.Warning;
                    if (EditorWindow) EditorWindow.Repaint();
                    return;
                }
                RunOperation(operation);
            };
            pending = callback;
            EditorApplication.update += callback;
            if (EditorWindow) EditorWindow.Repaint();
        }

        private void CancelPending() { if (pending != null) EditorApplication.update -= pending; pending = null; }
        internal void TickDiagnostics()
        {
            if (tab == 3 && diagnostics.Tick() && EditorWindow) EditorWindow.Repaint();
        }
        public void OnDispose() { CancelPending(); definition.Dispose(); diagnostics.Dispose(); Invalidate(); }
    }
}
