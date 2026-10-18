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
        internal string ReferenceDirectory = "Assets/YLFrameworkData";
        private ResourceProject project;
        private string projectGuid;
        private ResourceCatalog catalog;
        private ResourceEditorSimulation simulation;
        private ResourceBuildMap map;
        private string status;
        private Exception statusError;
        private string statusBrief;
        private MessageType statusType = MessageType.Info;
        private string statusGuid;
        private string statusProject;
        private int statusTab;
        private DateTime statusAt;
        private bool statusExpired;
        private bool executing;
        private int observedDirty;
        private EditorApplication.CallbackFunction pending;
        private readonly ResourceDefinitionView definition = new();
        private readonly ResourceBuildView build = new();
        private readonly ResourceDiagnosticsView diagnostics = new();
        private int tab;
        private Vector2 outputScroll;
        private ResourceBuildStage lastStage;
        private readonly ResourceWorkbenchLayout fallbackLayout = new();
        private ResourceWorkbenchLayout Layout => (EditorWindow as ResServiceEditorWindow)?.Layout ?? fallbackLayout;
        private static readonly string[] Tabs = { "资源配置", "分包分析", "构建发布", "版本交付", "诊断" };
        internal bool IsExecuting => executing;
        internal bool IsBusy => pending != null || executing || EditorBlocked;
        private static bool EditorBlocked => EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;
        internal bool CanEdit => project && !IsBusy && AssetDatabase.IsOpenForEdit(project);
        internal string VisibleStatus => statusGuid == projectGuid && statusTab == tab &&
            (statusType != MessageType.Info || DateTime.UtcNow - statusAt < TimeSpan.FromSeconds(6)) ? status : null;

        public void SetProject(ResourceProject value)
        {
            var guid = value ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : null;
            if (executing && guid != projectGuid) throw new InvalidOperationException("操作已开始，暂时不能切换资源项目。");
            if (value) ResourceEditorOperations.RequireEditableProject(value);
            if (guid == projectGuid)
            {
                project = value;
                var count = value ? EditorUtility.GetDirtyCount(value) : 0;
                if (count != observedDirty) { observedDirty = count; Invalidate(); }
                return;
            }
            if (definition.HasPendingRename) definition.CommitRename();
            definition.StoreUIState(project, Layout);
            CancelPending();
            if (guid != projectGuid) { definition.Reset(); build.Reset(); diagnostics.SetContext(guid); }
            projectGuid = guid; project = value;
            observedDirty = value ? EditorUtility.GetDirtyCount(value) : 0;
            tab = Mathf.Clamp(Layout.Task, 0, Tabs.Length - 1);
            Invalidate(); status = null;
        }

        public void Invalidate()
        {
            catalog = null; simulation = null; map = null;
            definition.Invalidate(); build.InvalidatePreview();
        }

        internal void SetRecipe(ResourceBuildRecipe value, string guid) => build.SetRecipe(value, guid);

        public void RefreshPreview()
        {
            if (!project) throw new InvalidOperationException("请先选择资源项目。");
            ResourceAtlasPlan atlasPlan = null;
            try
            {
                if (ResourceAtlasGenerator.IsConfigured(project)) atlasPlan = ResourceAtlasGenerator.Preview(project);
                if (atlasPlan?.NeedsGeneration == true) throw new InvalidOperationException("图集尚未准备完成。请先生成或更新图集，再重新计算分包。");
                map = ResourceBuildMap.Create(project);
                simulation = new ResourceEditorSimulation(new[] { project });
                catalog = simulation.GetCatalog(project.ProjectId);
                definition.SetPreview(map, catalog, simulation);
                build.SetPreview(map, project);
            }
            catch (Exception error) { build.SetPreviewError(error, atlasPlan?.NeedsGeneration == true ? atlasPlan : null); throw; }
        }

        public void OnGUI(ref string app, ref bool generate, ref bool force, float availableWidth = 0)
        {
            HandleKeyboard();
            if (project && observedDirty != EditorUtility.GetDirtyCount(project))
            { observedDirty = EditorUtility.GetDirtyCount(project); Invalidate(); }
            var width = availableWidth > 0 ? availableWidth : EditorWindow.position.width - 16;
            GUILayout.Space(4);
            var next = width < 720 ? EditorGUILayout.Popup("任务", tab, Tabs, ResourceWorkbenchGUI.Popup, GUILayout.Height(ResourceWorkbenchGUI.FieldHeight)) : ResourceWorkbenchGUI.Navigation(tab, Tabs, true);
            if (next != tab)
            {
                NavigateTask(next);
            }
            diagnostics.SetVisible(tab == 4 && !Layout.Settings && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                (EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode));
            if (EditorBlocked && tab != 4) GUILayout.Label("当前不能编辑或构建：" + (EditorApplication.isPlaying ? "Unity 正在运行" : "Unity 正在编译、导入或切换运行状态"), ResourceWorkbenchGUI.Secondary);
            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            if (Layout.Settings && project && EditorWindow is ResServiceEditorWindow host) host.DrawSettings(width);
            else if (tab == 4) diagnostics.Draw(width, build.CurrentReport, () => NavigateTask(2), NavigateTask);
            else DrawTask(ref app, ref generate, ref force, width);
            EditorGUILayout.EndVertical();
            DrawOutput();
        }

        internal void NavigateTask(int next)
        {
            if (next < 0 || next >= Tabs.Length) return;
            if (CanEdit && definition.HasPendingRename) RunOperation(() => { definition.CommitRename(); Invalidate(); return "分组名称已应用，尚未保存。"; });
            tab = next; Layout.Task = next; Layout.Settings = false; GUI.FocusControl(null);
            if (next != 4) diagnostics.SetVisible(false);
        }

        internal void ShowGuide() { NavigateTask(4); diagnostics.ShowGuide(); }

        private void DrawTask(ref string app, ref bool generate, ref bool force, float width)
        {
            if (!project)
            {
                GUILayout.Space(24);
                GUILayout.Label("选择一个资源项目", ResourceWorkbenchGUI.Title);
                GUILayout.Label("从左上角选择已有资源项目，或新建 ResourceProject 配置。资源项目不是 Unity 工程；一个 Unity 工程可以包含多个资源项目。", ResourceWorkbenchGUI.Secondary);
                GUILayout.Space(12);
                GUILayout.Label("日常流程", EditorStyles.boldLabel);
                GUILayout.Label("配置资源 → 保存项目 → 构建并本地发布 → 查看发布版本", EditorStyles.wordWrappedLabel);
                GUILayout.Label("第一次先使用 None（不加密），不需要先设置配方、图集、签发或部署。", ResourceWorkbenchGUI.Secondary);
                if (GUILayout.Button("查看开始使用与任务入口", ResourceWorkbenchGUI.Button, GUILayout.Width(220))) ShowGuide();
                GUILayout.FlexibleSpace();
                return;
            }
            try { ResourceEditorOperations.RequireEditableProject(project); }
            catch (Exception error)
            {
                CancelPending();
                EditorGUILayout.HelpBox("当前资源项目不可编辑，请检查项目配置。错误详情：\n" + ResourceEditorText.FailureDetails(error), MessageType.Error);
                return;
            }
            var originGuid = projectGuid;
            void ScopedRun(Func<string> operation)
            {
                if (originGuid == projectGuid) RunOperation(operation);
                else RecordStatus("项目已切换，旧菜单中的操作未执行；请在当前项目重新打开菜单。", MessageType.Warning, projectGuid, project ? project.DisplayName : "", tab);
            }
            void ScopedQueue(Func<string> operation)
            {
                if (originGuid == projectGuid) QueueOperation(operation);
            }
            using (new EditorGUI.DisabledScope(!CanEdit))
            {
                if (tab == 0)
                {
                    definition.Draw(project, EditorWindow, width, Invalidate, ScopedRun, ScopedQueue,
                        () => ScopedQueue(() => { RefreshPreview(); return "资源解析预览已更新，尚未构建。"; }));
                }
                else if (tab == 1) build.DrawPreview(project, width, () => ScopedQueue(() => { RefreshPreview(); return "已更新当前定义分包预估。"; }), source => ScopedRun(() =>
                {
                    definition.LocateSource(project, source); tab = 0; Layout.Task = tab; Layout.Settings = false;
                    return "已核对来源并定位对应配置；未修改或保存资产。";
                }));
                else if (tab == 2) build.Draw(project, ref app, ref generate, ref force, ref ReferenceDirectory, width, ScopedQueue, ScopedRun, RebindProject,
                    value =>
                    {
                        if (!CanEdit || originGuid != projectGuid) return;
                        try
                        {
                            if (EditorWindow is ResServiceEditorWindow host) host.SetRecipe(value); else build.SetRecipe(value, value ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : null);
                        }
                        catch (Exception error) { RecordStatus("无法选择构建配方，请展开输出查看错误详情。", MessageType.Error, projectGuid, project.DisplayName, tab, error: error); }
                    }, value =>
                    {
                        if (!CanEdit || originGuid != projectGuid) return;
                        if (EditorWindow is ResServiceEditorWindow host) host.SetProject(value); else SetProject(value);
                    }, () => { tab = 1; Layout.Task = tab; Layout.Settings = false; });
                else build.DrawDelivery(project, ref app, width, ScopedQueue, ScopedRun);
            }
        }

        private void HandleKeyboard()
        {
            var current = Event.current;
            if (current.rawType != EventType.KeyDown) return;
            var save = (current.control || current.command) && current.keyCode == KeyCode.S;
            var add = current.alt && current.keyCode == KeyCode.G;
            if ((save || add) && (!GUI.enabled || !CanEdit)) { current.Use(); return; }
            if (!GUI.enabled) return;
            if (current.keyCode == KeyCode.Escape)
            {
                CancelPending();
                if (EditorGUIUtility.editingTextField && GUI.GetNameOfFocusedControl() != "Resource.EntrySearch") return;
                definition.Escape(); GUI.FocusControl(null); current.Use(); return;
            }
            if ((current.control || current.command) && current.keyCode == KeyCode.F)
            {
                tab = 0; Layout.Task = tab; Layout.Settings = false; definition.FocusSearch(); current.Use(); return;
            }
            if (!CanEdit) return;
            if ((current.control || current.command) && current.keyCode == KeyCode.S) { Save(true); current.Use(); }
            else if (current.alt && current.keyCode == KeyCode.G)
            {
                tab = 0; Layout.Task = tab; Layout.Settings = false; GUI.FocusControl(null);
                RunOperation(() => { definition.AddGroup(project); Invalidate(); return "已新增空分组；添加资源后请保存项目。"; });
                current.Use();
            }
        }

        private void Save(bool context = false)
        {
            GUI.FocusControl(null);
            RunOperation(() =>
            {
                if (context && !Layout.Settings && tab == 0 && definition.IsAtlasMode)
                {
                    ResourceAtlasFields.Save(project);
                    return "已保存当前图集规则资产；未保存项目配置、其他资产或场景。";
                }
                if (context && !Layout.Settings && tab == 2 && build.HasRecipe)
                {
                    build.SaveRecipe();
                    return "已保存所选构建配方；未保存项目配置、其他资产或场景。";
                }
                if (definition.CommitRename()) Invalidate();
                ResourceEditorOperations.SaveProject(project);
                (EditorWindow as ResServiceEditorWindow)?.ClearSelectionError();
                return "项目配置已保存；未保存或修改场景及其他项目。";
            });
        }
        internal void SaveProject() { if (CanEdit) Save(); }

        private void RebindProject(ResourceBuildResult result)
        {
            var guid = result?.Map.ProjectGuid ?? projectGuid;
            if (string.IsNullOrEmpty(guid)) return;
            var current = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
            if (EditorWindow is ResServiceEditorWindow window) window.SetProject(current); else SetProject(current);
        }

        private void RunOperation(Func<string> operation)
        {
            if (IsBusy || !GUI.enabled) return;
            PerformOperation(operation, projectGuid, project ? project.DisplayName ?? project.ProjectId : "未选资源项目", tab);
        }

        private void PerformOperation(Func<string> operation, string originGuid, string originName, int originTab)
        {
            executing = true; lastStage = null;
            ResourceBuildProgress.StageReached += ObserveStage;
            var previousReport = build.CurrentReport;
            try
            {
                var text = operation();
                var report = build.CurrentReport;
                var fresh = report != previousReport && report != null;
                var type = fresh && report.BuildError != null ? MessageType.Error : fresh &&
                    (report.PublicationError != null || report.GenerationError != null) ? MessageType.Warning : MessageType.Info;
                var failure = report?.BuildError ?? report?.PublicationError ?? report?.GenerationError;
                var brief = fresh ? ResourceBuildView.Outcome(report) + (failure == null ? "" : " 请展开输出或技术详情查看错误原因。") : null;
                RecordStatus(text, type, originGuid, originName, originTab, brief, fresh ? failure : null);
            }
            catch (Exception error) { RecordStatus("操作未完成，请展开输出查看错误详情。", MessageType.Error, originGuid, originName, originTab, error: error); }
            finally { ResourceBuildProgress.StageReached -= ObserveStage; executing = false; }
            if (EditorWindow) EditorWindow.Repaint();
        }

        private void ObserveStage(ResourceBuildStage stage)
        {
            if (!executing || stage.ProjectGuid != projectGuid) return;
            lastStage = stage;
            if (EditorWindow) EditorWindow.Repaint();
        }

        private void RecordStatus(string text, MessageType type, string guid, string name, int page, string brief = null, Exception error = null)
        {
            statusError = error;
            status = text + (statusError == null ? "" : "\n\n" + ResourceEditorText.FailureDetails(statusError));
            statusType = type; statusGuid = guid; statusProject = name; statusTab = page; statusAt = DateTime.UtcNow; statusExpired = false;
            var summary = brief ?? text;
            var end = (summary ?? "").IndexOf('\n');
            statusBrief = end < 0 ? summary : summary.Substring(0, end);
            if (statusBrief?.Length > 140) statusBrief = statusBrief.Substring(0, 140) + "...";
        }

        private void QueueOperation(Func<string> operation)
        {
            if (IsBusy) return;
            if (definition.HasPendingRename)
            {
                try { definition.CommitRename(); Invalidate(); }
                catch (Exception error) { RecordStatus("分组名称未应用，操作尚未排队。请展开输出查看错误详情。", MessageType.Error, projectGuid, project ? project.DisplayName : "", tab, error: error); return; }
            }
            var originGuid = projectGuid;
            var originName = project ? project.DisplayName ?? project.ProjectId : "未选资源项目";
            var originTab = tab;
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                EditorApplication.update -= callback;
                if (!ReferenceEquals(pending, callback)) return;
                pending = null;
                if (EditorBlocked || projectGuid != originGuid)
                {
                    RecordStatus("Unity 状态或所选资源项目已变化，排队操作未执行。", MessageType.Warning, originGuid, originName, originTab);
                    if (EditorWindow) EditorWindow.Repaint();
                    return;
                }
                PerformOperation(operation, originGuid, originName, originTab);
            };
            pending = callback;
            EditorApplication.update += callback;
            if (EditorWindow) EditorWindow.Repaint();
        }

        private void CancelPending() { if (pending != null) EditorApplication.update -= pending; pending = null; }
        private void DrawOutput()
        {
            var height = EditorWindow ? EditorWindow.position.height : 720;
            var displayed = ResourceWorkbenchGUI.GetOutputHeight(Layout.OutputHeight, height, !string.IsNullOrEmpty(status));
            if (Layout.Output) ResourceWorkbenchGUI.HeightSplitter(GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true)), ref Layout.OutputHeight, displayed, height);
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            Layout.Output = ResourceWorkbenchGUI.Disclosure(Layout.Output, pending != null ? "输出 · 操作已排队" : "输出");
            if (pending != null && GUILayout.Button("取消排队", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(78)))
            { CancelPending(); RecordStatus("排队已取消，操作未开始。", MessageType.Info, projectGuid, project ? project.DisplayName : "", tab); }
            GUILayout.FlexibleSpace();
            if (project) GUILayout.Label(EditorUserBuildSettings.activeBuildTarget.ToString(), GUILayout.Height(ResourceWorkbenchGUI.BarHeight));
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(VisibleStatus)) EditorGUILayout.HelpBox(statusBrief, statusType);
            if (executing) GUILayout.Label("操作已开始，暂时不能取消。最近到达的步骤：" + (lastStage == null ? "正在开始" : ResourceBuildProgress.PhaseName(lastStage.Phase)), EditorStyles.wordWrappedLabel);
            if (Layout.Output)
            {
                outputScroll = EditorGUILayout.BeginScrollView(outputScroll, GUILayout.Height(displayed));
                if (statusGuid == projectGuid && !string.IsNullOrEmpty(status))
                {
                    GUILayout.Label(statusProject + " / " + Tabs[statusTab] + " / " + statusAt.ToLocalTime().ToString("HH:mm:ss"), EditorStyles.miniBoldLabel);
                    EditorGUILayout.SelectableLabel(status, EditorStyles.wordWrappedLabel, GUILayout.MinWidth(0), GUILayout.Height(Mathf.Max(42, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(status), Mathf.Max(200, (EditorWindow ? EditorWindow.position.width : 720) - 48)))));
                    if (GUILayout.Button("复制完整输出", EditorStyles.miniButton, GUILayout.Width(100))) EditorGUIUtility.systemCopyBuffer = status;
                }
                else GUILayout.Label("尚无操作输出。执行后的详细记录会显示在这里。", ResourceWorkbenchGUI.Secondary);
                using (new EditorGUI.DisabledScope(IsBusy))
                    if (project && build.DrawAttempts(project)) { tab = 2; Layout.Task = tab; Layout.Settings = false; }
                EditorGUILayout.EndScrollView();
            }
        }
        internal void TickDiagnostics()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) diagnostics.SetVisible(false);
            else if (tab == 4 && diagnostics.Tick() && EditorWindow) EditorWindow.Repaint();
            if (!statusExpired && status != null && DateTime.UtcNow - statusAt >= TimeSpan.FromSeconds(6))
            { statusExpired = true; if (EditorWindow) EditorWindow.Repaint(); }
        }
        internal void PauseDiagnostics() => diagnostics.SetVisible(false);
        internal void OnEditorTransition() { CancelPending(); PauseDiagnostics(); }
        public void OnDispose()
        {
            CancelPending();
            ResourceBuildProgress.StageReached -= ObserveStage;
            try { if (!EditorBlocked && !executing) definition.CommitRename(); }
            catch (Exception error) { Debug.LogWarning("分组名称未应用，仍保留原配置。\n" + error); }
            definition.StoreUIState(project, Layout);
            definition.Dispose(); diagnostics.Dispose(); Invalidate();
        }
    }
}
