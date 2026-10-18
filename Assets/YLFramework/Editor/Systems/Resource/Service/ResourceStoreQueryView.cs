using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceStoreQueryView : IDisposable
    {
        private sealed class ProjectInput { internal string ProjectId = ""; internal string Platform = ""; internal string App = ""; }
        private readonly List<ProjectInput> projects = new() { new ProjectInput() };
        private string root = "";
        private int maxEntries = 10000;
        private IEnumerator query;
        private CancellationTokenSource cancellation;
        private object runningIdentity;
        private ArtifactStoreReport report;
        private string reportIdentity;
        private string queryIdentity;
        private string reportSummary;
        private string reportNotes;
        private Exception error;
        private Exception cleanupError;
        private bool errorDetails;
        private string note;
        private bool oldReport;
        private bool showResults;
        private bool compactDetail;
        private ResourceArtifactTable table;
        private Vector2 scroll;
        private Vector2 resultPageScroll;
        private bool showInputHelp;
        private bool showTechnical;
        private string lifecycleDetail;
        internal bool IsQuerying => query != null;

        internal void Begin(string path, ResourceProjectMount[] closure, int steps)
        {
            if (query != null) throw new InvalidOperationException("一次只能运行一个磁盘缓存查询，请先停止当前查询。");
            if (closure == null || closure.Any(x => x == null || x.BuiltIn != null || x.Remote != null))
                throw new ArgumentException("编辑器查询只接收完整的项目参数列表，不能包含内置版本快照或远程配置对象。");
            oldReport = report != null;
            var store = new ArtifactStore(path, closure);
            cancellation = new CancellationTokenSource(); runningIdentity = new object();
            var identity = runningIdentity;
            queryIdentity = store.RootPath + "\n" + string.Join("\n", closure.Select(x => x.ProjectId + " / " + x.Platform + " / App=" + x.AppContentCompatibilityId)) + "\n扫描步骤上限=" + steps;
            error = cleanupError = null; note = report == null ? "正在查询磁盘缓存。" : "正在查询，当前显示上次结果。"; showResults = true;
            lifecycleDetail = "QueryAsync 在 Editor 主线程分段执行，每次 MoveNext 最多推进32步；同一时间只运行一个查询。";
            try
            {
                query = store.QueryAsync(value =>
                {
                    if (!ReferenceEquals(identity, runningIdentity)) return;
                    report = value; reportIdentity = queryIdentity; oldReport = false;
                    table ??= new ResourceArtifactTable(); table.SetData(value);
                    reportSummary = "Status=" + value.Status + " / SizeComplete=" + value.IsSizeComplete + " / 已观测逻辑字节=" + value.ObservedBytes +
                        " / 候选字节=" + (value.CleanupCandidateBytes?.ToString() ?? "不可用（不是0）") + " / Entries=" + value.Entries.Count;
                    reportNotes = "完整项目列表（App 由操作者填写，项目登记记录不验证 App）：\n" + string.Join("\n", value.Projects.Select(x => x.ProjectId + " / " + x.Platform + " / App=" + x.AppContentCompatibilityId)) +
                        "\n\n原始问题记录：\n" + string.Join("\n", value.Issues) + "\n\n受保护版本：\n" + string.Join("\n", value.ProtectedVersions.Select(x => x.DirectoryPath + " / " + x.Reasons)) +
                        "\n\n正在进行的操作：\n" + string.Join("\n", value.InFlightOperations) + "\n\n已准备更新的记录（只读）：\n" + string.Join("\n", value.PreparedUpdates.Select(x => x.RecordPath + "\n" +
                            string.Join("\n", x.Projects.Select(p => p.ProjectId + " / " + p.Platform + " / " + p.FromDigest + " → " + p.ToDigest))));
                }, steps, cancellation.Token);
            }
            catch { Stop("查询未能启动，未生成新报告"); throw; }
        }

        internal bool Tick()
        {
            if (query == null) return false;
            try
            {
                if (!query.MoveNext()) { StopCore(); note = report != null && !oldReport ? "查询已结束。" : "查询已结束，未收到新结果。"; lifecycleDetail = "查询枚举器已结束并释放，结果状态来自实际查询回调。"; }
            }
            catch (Exception failure) { error = failure; StopCore(); note = report == null ? "本次查询未生成结果。" : "本次查询未完成，当前显示上次结果。"; lifecycleDetail = "发生异常后已停止查询并释放枚举器；未收到新回调时，不将旧报告改为完成或取消。"; }
            return true;
        }
        internal void Stop(string reason)
        {
            if (query != null)
            {
                note = report == null ? "查询已停止，尚无查询结果。" : "查询已停止，当前显示上次结果。";
                lifecycleDetail = "停止原因：" + reason + "。已取消并释放查询枚举器；未收到新回调时，不将旧报告改为取消或完成，仍保留原查询参数。";
            }
            if (report != null) oldReport = true;
            StopCore();
        }
        private void StopCore()
        {
            var original = query; query = null; runningIdentity = null;
            var source = cancellation; cancellation = null;
            try { source?.Cancel(); (original as IDisposable)?.Dispose(); }
            catch (Exception failure) { if (error == null) error = failure; else cleanupError = failure; }
            finally { source?.Dispose(); }
        }

        internal void Draw(float width)
        {
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            showResults = ResourceWorkbenchGUI.Navigation(showResults ? 1 : 0, new[] { "查询参数", "查询结果" }, false, GUILayout.Width(168)) == 1;
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(IsQuerying))
                if (GUILayout.Button("开始只读查询", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(112)))
                    try { Begin(root, projects.Select(x => new ResourceProjectMount(x.ProjectId, x.Platform, x.App)).ToArray(), maxEntries); }
                    catch (Exception failure) { error = failure; }
            using (new EditorGUI.DisabledScope(!IsQuerying))
                if (GUILayout.Button("停止查询", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(76))) Stop("用户停止");
            EditorGUILayout.EndHorizontal();
            if (!showResults)
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(0), GUILayout.ExpandHeight(true));
                DrawErrorDetails();
                GUILayout.Label("仅查询 YL ArtifactStore，不是全盘缓存。请提供应用的真实缓存根与完整项目列表。", ResourceWorkbenchGUI.Secondary);
                EditorGUI.BeginChangeCheck();
                root = ResourceWorkbenchGUI.TextField("缓存根目录", root, true, "填写应用传给 ResourceConfiguration.Create(mode, mounts, cacheRoot) 的 cacheRoot 绝对路径；不会从 persistentDataPath 或工作台来源目录推算。");
                maxEntries = EditorGUI.IntField(EditorGUILayout.GetControlRect(true, ResourceWorkbenchGUI.FieldHeight, ResourceWorkbenchGUI.Field),
                    new GUIContent("扫描步数上限", "maxEntries，1～100000；限制扫描步骤，不是返回条目行数。"), maxEntries, ResourceWorkbenchGUI.Field);
                var remove = -1;
                for (var i = 0; i < projects.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("项目 " + (i + 1), EditorStyles.boldLabel, GUILayout.Height(ResourceWorkbenchGUI.BarHeight));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("移除", ResourceWorkbenchGUI.Button, GUILayout.Width(60))) remove = i;
                    EditorGUILayout.EndHorizontal();
                    projects[i].ProjectId = ResourceWorkbenchGUI.TextField("项目标识", projects[i].ProjectId, help: "ResourceProjectMount.ProjectId：填写应用实际挂载的项目标识，不自动使用当前工作台项目。");
                    projects[i].Platform = ResourceWorkbenchGUI.TextField("平台", projects[i].Platform, help: "ResourceProjectMount.Platform，应用明确配置的平台值，不按当前Editor平台猜测。");
                    projects[i].App = ResourceWorkbenchGUI.TextField("应用兼容标识", projects[i].App, help: "ResourceProjectMount.AppContentCompatibilityId：由操作者填写。项目登记记录只核对 ProjectId 和 Platform，不验证 App。");
                }
                if (EditorGUI.EndChangeCheck()) Stop("表单输入变化");
                if (remove >= 0) { projects.RemoveAt(remove); Stop("项目列表变化"); }
                if (GUILayout.Button("添加项目", ResourceWorkbenchGUI.Button, GUILayout.Width(132))) { projects.Add(new ProjectInput()); Stop("项目列表变化"); }
                showInputHelp = ResourceWorkbenchGUI.Disclosure(showInputHelp, "参数来源与查询范围");
                if (showInputHelp)
                {
                    GUILayout.Label("请使用应用传给 ResourceConfiguration.Create(mode, mounts, cacheRoot) 的 cacheRoot 和完整 ResourceProjectMount 列表。本工具不会自动读取游戏配置、反射应用对象或扫描其他目录寻找参数。", ResourceWorkbenchGUI.Secondary);
                    GUILayout.Label("App 由操作者填写，项目登记记录不验证 App。扫描步数上限 maxEntries 允许1～100000，不是结果行数。参数只在本次窗口会话保留，不写入资源项目或配方。", ResourceWorkbenchGUI.Secondary);
                    GUILayout.Label("查询不初始化或挂载运行时，也不恢复、清理、删除、激活、创建缓存根或锁。", ResourceWorkbenchGUI.Secondary);
                }
                EditorGUILayout.EndScrollView();
            }
            else DrawReport(width);
            if (error != null || note != null && (report == null || oldReport || IsQuerying))
                EditorGUI.LabelField(EditorGUILayout.GetControlRect(false, ResourceWorkbenchGUI.RowHeight), new GUIContent(error == null ? note : "查询未完成，请展开错误详情。", "查询状态：" + note + "\n" + lifecycleDetail), ResourceWorkbenchGUI.Secondary);
        }

        private void DrawReport(float width)
        {
            if (report == null)
            {
                resultPageScroll = EditorGUILayout.BeginScrollView(resultPageScroll, GUILayout.MinHeight(0), GUILayout.ExpandHeight(true));
                DrawErrorDetails();
                GUILayout.Label(IsQuerying ? "正在查询，尚无结果。" : "尚无查询结果。填写明确的缓存根与项目列表后，开始只读查询。", ResourceWorkbenchGUI.Secondary);
                EditorGUILayout.EndScrollView();
                return;
            }
            EditorGUILayout.BeginHorizontal();
            ResourceWorkbenchGUI.Metric("结果状态", (oldReport ? "上次 / " : "") + StatusLabel(report.Status), "Status: " + report.Status + "；Ready 只表示查询完成，不保证 App 兼容、解密、签名或运行时加载检查通过。", 80);
            ResourceWorkbenchGUI.Metric("空间观测", report.IsSizeComplete ? "完整" : "不完整 / 总量未知", "IsSizeComplete；未知目录与链接目标不纳入完整观测，不完整字节不是0。", 80);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            ResourceWorkbenchGUI.Metric("已观测字节", ResourceWorkbenchGUI.Bytes(report.ObservedBytes), "ObservedBytes: " + report.ObservedBytes + "；普通文件逻辑长度之和，不是实际磁盘占用或原生内存。观测不完整时不代表总量。", 80);
            ResourceWorkbenchGUI.Metric("候选字节", ResourceWorkbenchGUI.Bytes(report.CleanupCandidateBytes), "CleanupCandidateBytes；仅Ready提供，未知不是0；候选逻辑字节不保证可释放同等磁盘空间，也不是删除授权。", 80);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUI.LabelField(EditorGUILayout.GetControlRect(false, ResourceWorkbenchGUI.RowHeight), new GUIContent("YL缓存范围", "仅查询 YL ArtifactStore，不是全盘缓存。RootPath：" + report.RootPath), new GUIContent(report.RootPath, report.RootPath));
            GUILayout.Label(new GUIContent(report.Entries.Count + " 条目 / " + report.ProtectedVersions.Count + " 保护版本", "Entries / ProtectedVersions，来自同次原报告。"), GUILayout.Width(168), GUILayout.Height(ResourceWorkbenchGUI.RowHeight));
            EditorGUILayout.EndHorizontal();
            ResourceWorkbenchGUI.Metric("观测时间（UTC）", report.StartedUtc.ToString("MM-dd HH:mm:ss") + " 至 " + report.CompletedUtc.ToString("MM-dd HH:mm:ss"),
                "StartedUtc: " + report.StartedUtc.ToString("u") + "\nCompletedUtc: " + report.CompletedUtc.ToString("u") + "\n一次有界观测，不是磁盘快照。", 116);
            var wide = width >= 950;
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            GUILayout.Label("筛选", GUILayout.Width(32), GUILayout.Height(ResourceWorkbenchGUI.FieldHeight));
            table.searchString = ResourceWorkbenchGUI.SearchField(table.searchString, "筛选本次查询：路径、类型、状态、保护信息");
            table.AlternatingRows = ResourceWorkbenchGUI.ToolbarToggle(table.AlternatingRows, "底纹", 64);
            if (!wide) compactDetail = GUILayout.Toggle(compactDetail, "详情", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            if (wide || !compactDetail)
                table.OnGUI(GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
            if (wide || compactDetail)
            {
                EditorGUILayout.BeginVertical(wide ? new[] { GUILayout.Width(width * .44f), GUILayout.MinHeight(0), GUILayout.ExpandHeight(true) } : new[] { GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.MinHeight(0), GUILayout.ExpandHeight(true) });
                resultPageScroll = EditorGUILayout.BeginScrollView(resultPageScroll, GUILayout.MinHeight(0), GUILayout.ExpandHeight(true));
                DrawErrorDetails();
                if (oldReport) GUILayout.Label("当前显示上次结果，保留原查询范围与时间，不代表当前表单或磁盘状态。", ResourceWorkbenchGUI.Secondary);
                if (report.Issues.Count > 0) GUILayout.Label("本次查询记录了 " + report.Issues.Count + " 项问题，请在技术详情中查看原始记录。", ResourceWorkbenchGUI.Secondary);
                var entry = table.Selected;
                if (entry != null)
                {
                    GUILayout.Label("所选条目", EditorStyles.boldLabel);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.SelectableLabel(entry.Path, ResourceWorkbenchGUI.Field, GUILayout.Height(ResourceWorkbenchGUI.FieldHeight), GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                    if (GUILayout.Button("复制路径", ResourceWorkbenchGUI.Button, GUILayout.Width(76))) EditorGUIUtility.systemCopyBuffer = entry.Path;
                    EditorGUILayout.EndHorizontal();
                    ResourceWorkbenchGUI.Metric("条目状态", ResourceArtifactTable.StateLabel(entry.State), "State: " + entry.State + "；CompleteRecord 只表示登记记录和文件集合完整，不表示 Bundle SHA256 或签名验证通过。");
                    ResourceWorkbenchGUI.Metric("保护原因", ResourceArtifactTable.ProtectionLabel(entry.Protection), "Protection: " + entry.Protection + "；来自同次报告，多个保护原因可叠加。");
                    ResourceWorkbenchGUI.Metric("逻辑字节", ResourceWorkbenchGUI.Bytes(entry.ObservedBytes), "ObservedBytes: " + entry.ObservedBytes + "；不是磁盘分配量。");
                    ResourceWorkbenchGUI.Metric("空间观测", entry.IsSizeComplete ? "完整" : "不完整 / 总量未知", "IsSizeComplete；已观测0不等于确认总量为0。");
                    ResourceWorkbenchGUI.Metric("清理候选", report.Status != ArtifactQueryStatus.Ready || !entry.IsSizeComplete ? "未确认" : entry.IsCleanupCandidate ? "是" : "否", "IsCleanupCandidate: " + entry.IsCleanupCandidate + "；候选不是删除授权，不保证可释放空间。");
                    ResourceWorkbenchGUI.Metric("所属项目", entry.ProjectId ?? "未知", "ProjectId：记录未提供项目时显示未知。");
                    ResourceWorkbenchGUI.Metric("平台", entry.Platform ?? "未知", "Platform：来自同次查询的记录。");
                    if (entry.Issues.Count > 0) GUILayout.Label("此条目有 " + entry.Issues.Count + " 项问题，请展开技术详情。", ResourceWorkbenchGUI.Secondary);
                }
                else GUILayout.Label("在条目列表选择一行查看状态、字节与保护原因。", ResourceWorkbenchGUI.Secondary);
                GUILayout.Label("候选为逻辑字节，不保证可释放同等磁盘空间；本页仅查询，不执行清理。", ResourceWorkbenchGUI.Secondary);
                showTechnical = ResourceWorkbenchGUI.Disclosure(showTechnical, "完整查询身份与技术详情");
                if (showTechnical)
                {
                    var text = reportIdentity + "\n\n" + reportSummary + "\n\n" + reportNotes + "\n\n" + lifecycleDetail;
                    if (entry != null) text += "\n\n所选条目技术字段：\nPath=" + entry.Path + "\nProjectId=" + entry.ProjectId + "\nPlatform=" + entry.Platform + "\nCatalogDigest=" + entry.CatalogDigest +
                        "\nKind=" + entry.Kind + "\nState=" + entry.State + "\nObservedBytes=" + entry.ObservedBytes + "\nIsSizeComplete=" + entry.IsSizeComplete + "\nIsCleanupCandidate=" + entry.IsCleanupCandidate + "\nProtection=" + entry.Protection +
                        "\n原始问题记录：\n" + string.Join("\n", entry.Issues);
                    EditorGUILayout.SelectableLabel(text, EditorStyles.wordWrappedLabel, GUILayout.Height(Mathf.Max(ResourceWorkbenchGUI.RowHeight,
                        EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(text), Mathf.Max(240, wide ? width * .44f - 44 : width - 44)))));
                }
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string StatusLabel(ArtifactQueryStatus value) => value switch
        {
            ArtifactQueryStatus.Ready => "查询完成",
            ArtifactQueryStatus.NotConfigured => "未配置",
            ArtifactQueryStatus.Missing => "根目录不存在",
            ArtifactQueryStatus.Busy => "缓存使用中",
            ArtifactQueryStatus.RecoveryRequired => "需要恢复",
            ArtifactQueryStatus.Invalid => "记录无效",
            ArtifactQueryStatus.Unreadable => "不可读取",
            ArtifactQueryStatus.Incomplete => "扫描未完成",
            ArtifactQueryStatus.Changed => "观测期间已变化",
            ArtifactQueryStatus.Canceled => "已取消",
            _ => "未知状态（" + value + "）"
        };
        private void DrawErrorDetails()
        {
            if (error == null) return;
            EditorGUILayout.HelpBox("本次磁盘缓存查询未完成，请查看错误详情；下方可能仍显示上次查询结果。", MessageType.Error);
            errorDetails = ResourceWorkbenchGUI.Disclosure(errorDetails, "查询错误详情");
            if (!errorDetails) return;
            GUILayout.Label(ResourceEditorText.FailureDetails(error), ResourceWorkbenchGUI.Secondary);
            if (cleanupError != null) GUILayout.Label("结束查询时另有异常：\n" + ResourceEditorText.FailureDetails(cleanupError), ResourceWorkbenchGUI.Secondary);
        }
        public void Dispose() => Stop("关闭或重载");
    }
}
