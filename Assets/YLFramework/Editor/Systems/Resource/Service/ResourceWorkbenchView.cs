using System;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>只保存 UI 选择及可失效快照，不拥有 Runtime 状态或第二份发布目录。</summary>
    public sealed class ResServiceView
    {
        public EditorWindow EditorWindow
        {
            get; set;
        }
        internal string ReferenceDirectory = "Assets/YLFrameworkData";
        private ResourceProject _project;
        private string _projectGuid;
        private ResourceCatalog _catalog;
        private ResourceEditorSimulation _simulation;
        private ResourceBuildMap _map;
        private string _status;
        private Exception _statusError;
        private string _statusBrief;
        private MessageType _statusType = MessageType.Info;
        private string _statusGuid;
        private string _statusProject;
        private int _statusTab;
        private DateTime _statusAt;
        private bool _isStatusExpired;
        private bool _isExecuting;
        private int _observedDirty;
        private EditorApplication.CallbackFunction _pending;
        private readonly ResourceDefinitionView _definition = new();
        private readonly ResourceBuildView _build = new();
        private readonly ResourceDiagnosticsView _diagnostics = new();
        private int _tab;
        private Vector2 _outputScroll;
        private ResourceBuildStage _lastStage;
        private readonly ResourceWorkbenchLayout _fallbackLayout = new();
        private ResourceWorkbenchLayout Layout => (EditorWindow as ResServiceEditorWindow)?.Layout ?? _fallbackLayout;
        private static readonly string[] _tabs = { "资源配置", "分包分析", "构建发布", "版本交付", "诊断" };
        internal bool IsExecuting => _isExecuting;
        internal bool IsBusy => _pending != null || _isExecuting || EditorBlocked;
        private static bool EditorBlocked => EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;
        internal bool CanEdit => _project && !IsBusy && AssetDatabase.IsOpenForEdit(_project);
        internal string VisibleStatus => _statusGuid == _projectGuid && _statusTab == _tab &&
            (_statusType != MessageType.Info || DateTime.UtcNow - _statusAt < TimeSpan.FromSeconds(6)) ? _status : null;

        public void SetProject(ResourceProject value)
        {
            var guid = value ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : null;
            if (_isExecuting && guid != _projectGuid)
            {
                throw new InvalidOperationException("操作已开始，暂时不能切换资源项目。");
            }

            if (value)
            {
                ResourceEditorOperations.RequireEditableProject(value);
            }

            if (guid == _projectGuid)
            {
                _project = value;
                var count = value ? EditorUtility.GetDirtyCount(value) : 0;
                if (count != _observedDirty)
                {
                    _observedDirty = count;
                    Invalidate();
                }
                return;
            }
            if (_definition.HasPendingRename)
            {
                _definition.CommitRename();
            }

            _definition.StoreUIState(_project, Layout);
            CancelPending();
            if (guid != _projectGuid)
            {
                _definition.Reset();
                _build.Reset();
                _diagnostics.SetContext(guid);
            }
            _projectGuid = guid;
            _project = value;
            _observedDirty = value ? EditorUtility.GetDirtyCount(value) : 0;
            _tab = Mathf.Clamp(Layout.task, 0, _tabs.Length - 1);
            Invalidate();
            _status = null;
        }

        public void Invalidate()
        {
            _catalog = null;
            _simulation = null;
            _map = null;
            _definition.Invalidate();
            _build.InvalidatePreview();
        }

        internal void SetRecipe(ResourceBuildRecipe value, string guid) => _build.SetRecipe(value, guid);

        public void RefreshPreview()
        {
            if (!_project)
            {
                throw new InvalidOperationException("请先选择资源项目。");
            }

            ResourceAtlasPlan atlasPlan = null;
            try
            {
                if (ResourceAtlasGenerator.IsConfigured(_project))
                {
                    atlasPlan = ResourceAtlasGenerator.Preview(_project);
                }

                if (atlasPlan?.NeedsGeneration == true)
                {
                    throw new InvalidOperationException("图集尚未准备完成。请先生成或更新图集，再重新计算分包。");
                }

                _map = ResourceBuildMap.Create(_project);
                _simulation = new ResourceEditorSimulation(new[] { _project });
                _catalog = _simulation.GetCatalog(_project.projectId);
                _definition.SetPreview(_map, _catalog, _simulation);
                _build.SetPreview(_map, _project);
            }
            catch (Exception error)
            {
                _build.SetPreviewError(error, atlasPlan?.NeedsGeneration == true ? atlasPlan : null);
                throw;
            }
        }

        public void OnGUI(ref string app, ref bool generate, ref bool force, float availableWidth = 0)
        {
            HandleKeyboard();
            if (_project && _observedDirty != EditorUtility.GetDirtyCount(_project))
            {
                _observedDirty = EditorUtility.GetDirtyCount(_project);
                Invalidate();
            }
            var width = availableWidth > 0 ? availableWidth : EditorWindow.position.width - 16;
            GUILayout.Space(4);
            var next = width < 720 ? EditorGUILayout.Popup("任务", _tab, _tabs, ResourceWorkbenchGui.Popup, GUILayout.Height(ResourceWorkbenchGui.FieldHeight)) : ResourceWorkbenchGui.Navigation(_tab, _tabs, true);
            if (next != _tab)
            {
                NavigateTask(next);
            }
            _diagnostics.SetVisible(_tab == 4 && !Layout.settings && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                (EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode));
            if (EditorBlocked && _tab != 4)
            {
                GUILayout.Label("当前不能编辑或构建：" + (EditorApplication.isPlaying ? "Unity 正在运行" : "Unity 正在编译、导入或切换运行状态"), ResourceWorkbenchGui.Secondary);
            }

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            if (Layout.settings && _project && EditorWindow is ResServiceEditorWindow host)
            {
                host.DrawSettings(width);
            }
            else if (_tab == 4)
            {
                _diagnostics.Draw(width, _build.CurrentReport, () => NavigateTask(2), NavigateTask);
            }
            else
            {
                DrawTask(ref app, ref generate, ref force, width);
            }

            EditorGUILayout.EndVertical();
            DrawOutput();
        }

        internal void NavigateTask(int next)
        {
            if (next < 0 || next >= _tabs.Length)
            {
                return;
            }

            if (CanEdit && _definition.HasPendingRename)
            {
                RunOperation(() =>
                {
                    _definition.CommitRename();
                    Invalidate();
                    return "分组名称已应用，尚未保存。";
                });
            }

            _tab = next;
            Layout.task = next;
            Layout.settings = false;
            GUI.FocusControl(null);
            if (next != 4)
            {
                _diagnostics.SetVisible(false);
            }
        }

        internal void ShowGuide()
        {
            NavigateTask(4);
            _diagnostics.ShowGuide();
        }

        private void DrawTask(ref string app, ref bool generate, ref bool force, float width)
        {
            if (!_project)
            {
                GUILayout.Space(24);
                GUILayout.Label("选择一个资源项目", ResourceWorkbenchGui.Title);
                GUILayout.Label("从左上角选择已有资源项目，或新建 ResourceProject 配置。资源项目不是 Unity 工程；一个 Unity 工程可以包含多个资源项目。", ResourceWorkbenchGui.Secondary);
                GUILayout.Space(12);
                GUILayout.Label("日常流程", EditorStyles.boldLabel);
                GUILayout.Label("配置资源 → 保存项目 → 构建并本地发布 → 查看发布版本", EditorStyles.wordWrappedLabel);
                GUILayout.Label("第一次先使用 None（不加密），不需要先设置配方、图集、签发或部署。", ResourceWorkbenchGui.Secondary);
                if (GUILayout.Button("查看开始使用与任务入口", ResourceWorkbenchGui.Button, GUILayout.Width(220)))
                {
                    ShowGuide();
                }

                GUILayout.FlexibleSpace();
                return;
            }
            try
            {
                ResourceEditorOperations.RequireEditableProject(_project);
            }
            catch (Exception error)
            {
                CancelPending();
                EditorGUILayout.HelpBox("当前资源项目不可编辑，请检查项目配置。错误详情：\n" + ResourceEditorText.FailureDetails(error), MessageType.Error);
                return;
            }
            var originGuid = _projectGuid;
            void ScopedRun(Func<string> operation)
            {
                if (originGuid == _projectGuid)
                {
                    RunOperation(operation);
                }
                else
                {
                    RecordStatus("项目已切换，旧菜单中的操作未执行；请在当前项目重新打开菜单。", MessageType.Warning, _projectGuid, _project ? _project.displayName : "", _tab);
                }
            }
            void ScopedQueue(Func<string> operation)
            {
                if (originGuid == _projectGuid)
                {
                    QueueOperation(operation);
                }
            }
            using (new EditorGUI.DisabledScope(!CanEdit))
            {
                if (_tab == 0)
                {
                    _definition.Draw(_project, EditorWindow, width, Invalidate, ScopedRun, ScopedQueue,
                        () => ScopedQueue(() =>
                        {
                            RefreshPreview();
                            return "资源解析预览已更新，尚未构建。";
                        }));
                }
                else if (_tab == 1)
                {
                    _build.DrawPreview(_project, width, () => ScopedQueue(() =>
                    {
                        RefreshPreview();
                        return "已更新当前定义分包预估。";
                    }), source => ScopedRun(() =>
                {
                    _definition.LocateSource(_project, source);
                    _tab = 0;
                    Layout.task = _tab;
                    Layout.settings = false;
                    return "已核对来源并定位对应配置；未修改或保存资产。";
                }));
                }
                else if (_tab == 2)
                {
                    _build.Draw(_project, ref app, ref generate, ref force, ref ReferenceDirectory, width, ScopedQueue, ScopedRun, RebindProject,
                    value =>
                    {
                        if (!CanEdit || originGuid != _projectGuid)
                        {
                            return;
                        }

                        try
                        {
                            if (EditorWindow is ResServiceEditorWindow host)
                            {
                                host.SetRecipe(value);
                            }
                            else
                            {
                                _build.SetRecipe(value, value ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : null);
                            }
                        }
                        catch (Exception error)
                        {
                            RecordStatus("无法选择构建配方，请展开输出查看错误详情。", MessageType.Error, _projectGuid, _project.displayName, _tab, error: error);
                        }
                    }, value =>
                    {
                        if (!CanEdit || originGuid != _projectGuid)
                        {
                            return;
                        }

                        if (EditorWindow is ResServiceEditorWindow host)
                        {
                            host.SetProject(value);
                        }
                        else
                        {
                            SetProject(value);
                        }
                    }, () =>
                    {
                        _tab = 1;
                        Layout.task = _tab;
                        Layout.settings = false;
                    });
                }
                else
                {
                    _build.DrawDelivery(_project, ref app, width, ScopedQueue, ScopedRun);
                }
            }
        }

        private void HandleKeyboard()
        {
            var current = Event.current;
            if (current.rawType != EventType.KeyDown)
            {
                return;
            }

            var save = (current.control || current.command) && current.keyCode == KeyCode.S;
            var add = current.alt && current.keyCode == KeyCode.G;
            if ((save || add) && (!GUI.enabled || !CanEdit))
            {
                current.Use();
                return;
            }
            if (!GUI.enabled)
            {
                return;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                CancelPending();
                if (EditorGUIUtility.editingTextField && GUI.GetNameOfFocusedControl() != "Resource.EntrySearch")
                {
                    return;
                }

                _definition.Escape();
                GUI.FocusControl(null);
                current.Use();
                return;
            }
            if ((current.control || current.command) && current.keyCode == KeyCode.F)
            {
                _tab = 0;
                Layout.task = _tab;
                Layout.settings = false;
                _definition.FocusSearch();
                current.Use();
                return;
            }
            if (!CanEdit)
            {
                return;
            }

            if ((current.control || current.command) && current.keyCode == KeyCode.S)
            {
                Save(true);
                current.Use();
            }
            else if (current.alt && current.keyCode == KeyCode.G)
            {
                _tab = 0;
                Layout.task = _tab;
                Layout.settings = false;
                GUI.FocusControl(null);
                RunOperation(() =>
                {
                    _definition.AddGroup(_project);
                    Invalidate();
                    return "已新增空分组；添加资源后请保存项目。";
                });
                current.Use();
            }
        }

        private void Save(bool context = false)
        {
            GUI.FocusControl(null);
            RunOperation(() =>
            {
                if (context && !Layout.settings && _tab == 0 && _definition.IsAtlasMode)
                {
                    ResourceAtlasFields.Save(_project);
                    return "已保存当前图集规则资产；未保存项目配置、其他资产或场景。";
                }
                if (context && !Layout.settings && _tab == 2 && _build.HasRecipe)
                {
                    _build.SaveRecipe();
                    return "已保存所选构建配方；未保存项目配置、其他资产或场景。";
                }
                if (_definition.CommitRename())
                {
                    Invalidate();
                }

                ResourceEditorOperations.SaveProject(_project);
                (EditorWindow as ResServiceEditorWindow)?.ClearSelectionError();
                return "项目配置已保存；未保存或修改场景及其他项目。";
            });
        }
        internal void SaveProject()
        {
            if (CanEdit)
            {
                Save();
            }
        }

        private void RebindProject(ResourceBuildResult result)
        {
            var guid = result?.Map.ProjectGuid ?? _projectGuid;
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            var current = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
            if (EditorWindow is ResServiceEditorWindow window)
            {
                window.SetProject(current);
            }
            else
            {
                SetProject(current);
            }
        }

        private void RunOperation(Func<string> operation)
        {
            if (IsBusy || !GUI.enabled)
            {
                return;
            }

            PerformOperation(operation, _projectGuid, _project ? _project.displayName ?? _project.projectId : "未选资源项目", _tab);
        }

        private void PerformOperation(Func<string> operation, string originGuid, string originName, int originTab)
        {
            _isExecuting = true;
            _lastStage = null;
            ResourceBuildProgress.StageReached += ObserveStage;
            var previousReport = _build.CurrentReport;
            try
            {
                var text = operation();
                var report = _build.CurrentReport;
                var fresh = report != previousReport && report != null;
                var type = fresh && report.BuildError != null ? MessageType.Error : fresh &&
                    (report.PublicationError != null || report.GenerationError != null) ? MessageType.Warning : MessageType.Info;
                var failure = report?.BuildError ?? report?.PublicationError ?? report?.GenerationError;
                var brief = fresh ? ResourceBuildView.Outcome(report) + (failure == null ? "" : " 请展开输出或技术详情查看错误原因。") : null;
                RecordStatus(text, type, originGuid, originName, originTab, brief, fresh ? failure : null);
            }
            catch (Exception error)
            {
                RecordStatus("操作未完成，请展开输出查看错误详情。", MessageType.Error, originGuid, originName, originTab, error: error);
            }
            finally
            {
                ResourceBuildProgress.StageReached -= ObserveStage;
                _isExecuting = false;
            }
            if (EditorWindow)
            {
                EditorWindow.Repaint();
            }
        }

        private void ObserveStage(ResourceBuildStage stage)
        {
            if (!_isExecuting || stage.ProjectGuid != _projectGuid)
            {
                return;
            }

            _lastStage = stage;
            if (EditorWindow)
            {
                EditorWindow.Repaint();
            }
        }

        private void RecordStatus(string text, MessageType type, string guid, string name, int page, string brief = null, Exception error = null)
        {
            _statusError = error;
            _status = text + (_statusError == null ? "" : "\n\n" + ResourceEditorText.FailureDetails(_statusError));
            _statusType = type;
            _statusGuid = guid;
            _statusProject = name;
            _statusTab = page;
            _statusAt = DateTime.UtcNow;
            _isStatusExpired = false;
            var summary = brief ?? text;
            var end = (summary ?? "").IndexOf('\n');
            _statusBrief = end < 0 ? summary : summary.Substring(0, end);
            if (_statusBrief?.Length > 140)
            {
                _statusBrief = _statusBrief.Substring(0, 140) + "...";
            }
        }

        private void QueueOperation(Func<string> operation)
        {
            if (IsBusy)
            {
                return;
            }

            if (_definition.HasPendingRename)
            {
                try
                {
                    _definition.CommitRename();
                    Invalidate();
                }
                catch (Exception error)
                {
                    RecordStatus("分组名称未应用，操作尚未排队。请展开输出查看错误详情。", MessageType.Error, _projectGuid, _project ? _project.displayName : "", _tab, error: error);
                    return;
                }
            }
            var originGuid = _projectGuid;
            var originName = _project ? _project.displayName ?? _project.projectId : "未选资源项目";
            var originTab = _tab;
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                EditorApplication.update -= callback;
                if (!ReferenceEquals(_pending, callback))
                {
                    return;
                }

                _pending = null;
                if (EditorBlocked || _projectGuid != originGuid)
                {
                    RecordStatus("Unity 状态或所选资源项目已变化，排队操作未执行。", MessageType.Warning, originGuid, originName, originTab);
                    if (EditorWindow)
                    {
                        EditorWindow.Repaint();
                    }

                    return;
                }
                PerformOperation(operation, originGuid, originName, originTab);
            };
            _pending = callback;
            EditorApplication.update += callback;
            if (EditorWindow)
            {
                EditorWindow.Repaint();
            }
        }

        private void CancelPending()
        {
            if (_pending != null)
            {
                EditorApplication.update -= _pending;
            }
            _pending = null;
        }
        private void DrawOutput()
        {
            var height = EditorWindow ? EditorWindow.position.height : 720;
            var displayed = ResourceWorkbenchGui.GetOutputHeight(Layout.outputHeight, height, !string.IsNullOrEmpty(_status));
            if (Layout.output)
            {
                ResourceWorkbenchGui.HeightSplitter(GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true)), ref Layout.outputHeight, displayed, height);
            }

            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGui.Toolbar);
            Layout.output = ResourceWorkbenchGui.Disclosure(Layout.output, _pending != null ? "输出 · 操作已排队" : "输出");
            if (_pending != null && GUILayout.Button("取消排队", ResourceWorkbenchGui.ToolbarButton, GUILayout.Width(78)))
            {
                CancelPending();
                RecordStatus("排队已取消，操作未开始。", MessageType.Info, _projectGuid, _project ? _project.displayName : "", _tab);
            }
            GUILayout.FlexibleSpace();
            if (_project)
            {
                GUILayout.Label(EditorUserBuildSettings.activeBuildTarget.ToString(), GUILayout.Height(ResourceWorkbenchGui.BarHeight));
            }

            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(VisibleStatus))
            {
                EditorGUILayout.HelpBox(_statusBrief, _statusType);
            }

            if (_isExecuting)
            {
                GUILayout.Label("操作已开始，暂时不能取消。最近到达的步骤：" + (_lastStage == null ? "正在开始" : ResourceBuildProgress.PhaseName(_lastStage.Phase)), EditorStyles.wordWrappedLabel);
            }

            if (Layout.output)
            {
                _outputScroll = EditorGUILayout.BeginScrollView(_outputScroll, GUILayout.Height(displayed));
                if (_statusGuid == _projectGuid && !string.IsNullOrEmpty(_status))
                {
                    GUILayout.Label(_statusProject + " / " + _tabs[_statusTab] + " / " + _statusAt.ToLocalTime().ToString("HH:mm:ss"), EditorStyles.miniBoldLabel);
                    EditorGUILayout.SelectableLabel(_status, EditorStyles.wordWrappedLabel, GUILayout.MinWidth(0), GUILayout.Height(Mathf.Max(42, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(_status), Mathf.Max(200, (EditorWindow ? EditorWindow.position.width : 720) - 48)))));
                    if (GUILayout.Button("复制完整输出", EditorStyles.miniButton, GUILayout.Width(100)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _status;
                    }
                }
                else
                {
                    GUILayout.Label("尚无操作输出。执行后的详细记录会显示在这里。", ResourceWorkbenchGui.Secondary);
                }

                using (new EditorGUI.DisabledScope(IsBusy))
                {
                    if (_project && _build.DrawAttempts(_project))
                    {
                        _tab = 2;
                        Layout.task = _tab;
                        Layout.settings = false;
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }
        internal void TickDiagnostics()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _diagnostics.SetVisible(false);
            }
            else if (_tab == 4 && _diagnostics.Tick() && EditorWindow)
            {
                EditorWindow.Repaint();
            }

            if (!_isStatusExpired && _status != null && DateTime.UtcNow - _statusAt >= TimeSpan.FromSeconds(6))
            {
                _isStatusExpired = true;
                if (EditorWindow)
                {
                    EditorWindow.Repaint();
                }
            }
        }
        internal void PauseDiagnostics() => _diagnostics.SetVisible(false);
        internal void OnEditorTransition()
        {
            CancelPending();
            PauseDiagnostics();
        }
        public void OnDispose()
        {
            CancelPending();
            ResourceBuildProgress.StageReached -= ObserveStage;
            try
            {
                if (!EditorBlocked && !_isExecuting)
                {
                    _definition.CommitRename();
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("分组名称未应用，仍保留原配置。\n" + error);
            }
            _definition.StoreUIState(_project, Layout);
            _definition.Dispose();
            _diagnostics.Dispose();
            Invalidate();
        }
    }
}
