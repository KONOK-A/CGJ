using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceDiagnosticsView : IDisposable
    {
        private ResourceDiagnosticsReport report;
        private ResourceSystem sampledSystem;
        private bool automatic;
        private double nextSample;
        private int section;
        private int selected;
        private string filter = "";
        private string error;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private (string Title, string Detail)[] rows = Array.Empty<(string, string)>();
        private bool rowsDirty;
        private static readonly string[] Sections = { "Projects", "Assets", "Bundles", "Instances", "Scenes", "Updates", "History" };

        internal void Draw()
        {
            var system = ResourceSystem.Current;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(system == null || !system.IsAvailable))
            {
                if (GUILayout.Button("采样 / 刷新", EditorStyles.toolbarButton, GUILayout.Width(88))) Capture();
                automatic = GUILayout.Toggle(automatic, "自动采样 (5s)", EditorStyles.toolbarButton, GUILayout.Width(110));
            }
            GUILayout.FlexibleSpace();
            if (system != null && system.IsAvailable)
            {
                var recording = report != null && ReferenceEquals(system, sampledSystem) && report.HistoryCapacity > 0;
                if (GUILayout.Button(recording ? "关闭请求历史" : "记录最近 128 条请求", EditorStyles.toolbarButton))
                {
                    system.ConfigureDiagnosticsHistory(recording ? 0 : 128);
                    Capture();
                }
            }
            EditorGUILayout.EndHorizontal();
            if (system == null || !system.IsAvailable)
                EditorGUILayout.HelpBox("ResourceSystem 未初始化或当前不可用。本面板不会 Initialize、加载资源、激活版本或扫描全局 Unity 对象。" +
                    (report == null ? "" : " 下方保留最后一次采样，不代表当前状态。"), MessageType.Info);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (report == null)
            {
                EditorGUILayout.HelpBox("应用显式初始化正式 ResourceSystem 后，点击“采样 / 刷新”。只读快照不持有资源；关闭面板不会取消业务请求。请求历史默认关闭，启用后有界记录。", MessageType.Info);
                return;
            }
            var summary = report.Summary;
            GUILayout.Label("UTC " + report.CapturedAtUtc.ToString("u", CultureInfo.InvariantCulture) + "  |  " + report.State +
                (ReferenceEquals(system, sampledSystem) ? "" : "  [历史会话采样]"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assets " + summary.RecordCount + " · Producers " + summary.ProducerCount + " · Waiters " + summary.WaiterCount +
                " · Leases " + summary.LeaseCount + "\nBundles " + summary.BundleRecordCount + " · Root pins " + summary.BundleRootPinCount +
                " · Dependency pins " + summary.BundleDependencyPinCount + " · Running/Queued " + summary.RunningBundleCount + "/" + summary.QueuedBundleCount +
                " · Native loads/unloads " + summary.NativeBundleLoadCount + "/" + summary.NativeBundleUnloadCount, MessageType.None);
            var next = GUILayout.Toolbar(section, Sections);
            if (next != section) { section = next; selected = 0; listScroll = Vector2.zero; rowsDirty = true; }
            EditorGUI.BeginChangeCheck();
            filter = EditorGUILayout.TextField("筛选身份 / 路径 / 原因", filter);
            if (EditorGUI.EndChangeCheck()) { rowsDirty = true; selected = 0; listScroll = Vector2.zero; }
            if (rowsDirty) BuildRows();
            GUILayout.Label(rows.Length + " 条匹配记录；只读采样，不执行卸载或清缓存。", EditorStyles.miniLabel);
            var rect = GUILayoutUtility.GetRect(200, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(340));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            listScroll = GUI.BeginScrollView(rect, listScroll, new Rect(0, 0, Mathf.Max(0, rect.width - 20), Mathf.Max(rect.height - 2, rows.Length * 25 + 4)));
            var first = Mathf.Max(0, (int)(listScroll.y / 25));
            for (var i = first; i < Math.Min(rows.Length, first + Mathf.CeilToInt(rect.height / 25) + 2); i++)
                if (GUI.Toggle(new Rect(4, i * 25, rect.width - 28, 24), selected == i, rows[i].Title, EditorStyles.miniButton))
                { selected = i; detailScroll = Vector2.zero; }
            GUI.EndScrollView();
            if (rows.Length == 0)
                EditorGUILayout.HelpBox(section == 6 && report.HistoryCapacity == 0 ? "请求历史未启用。没有伪造的历史或零耗时测量。" : "本次快照没有匹配记录。", MessageType.Info);
            else
            {
                selected = Mathf.Clamp(selected, 0, rows.Length - 1);
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.MinHeight(100), GUILayout.MaxHeight(260));
                EditorGUILayout.SelectableLabel(rows[selected].Detail, EditorStyles.wordWrappedLabel,
                    GUILayout.MinHeight(Mathf.Max(100, EditorStyles.wordWrappedLabel.CalcHeight(new GUIContent(rows[selected].Detail), Math.Max(300, EditorGUIUtility.currentViewWidth - 260)))));
                EditorGUILayout.EndScrollView();
            }
            GUILayout.Label("FileBytes 是 Catalog 压缩文件字节，ImageBytes 是缓存估计；均不是 Unity native memory 实测。未标注 owner 明示为 Unknown，不猜业务归属。", EditorStyles.wordWrappedMiniLabel);
        }

        private void Capture()
        {
            var system = ResourceSystem.Current;
            if (system == null || !system.IsAvailable) { error = "服务当前不可用；未执行初始化或扫描。"; return; }
            try { report = system.GetDiagnosticsReport(); sampledSystem = system; error = null; rowsDirty = true; nextSample = EditorApplication.timeSinceStartup + 5; }
            catch (Exception failure) { error = "采样失败：" + failure.Message; }
        }

        internal bool Tick()
        {
            if (!automatic || EditorApplication.timeSinceStartup < nextSample) return false;
            nextSample = EditorApplication.timeSinceStartup + 5;
            if (ResourceSystem.Current == null || !ResourceSystem.Current.IsAvailable) return false;
            Capture(); return true;
        }

        private void BuildRows()
        {
            var result = new List<(string, string)>();
            void Add(string title, string detail)
            {
                if (filter.Length == 0 || title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || detail.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    result.Add((title, detail));
            }
            switch (section)
            {
                case 0:
                    foreach (var value in report.Projects)
                        Add(value.ProjectId + "  [" + (value.ActivationBlocked ? "Activation blocked" : "Mounted") + "]",
                            "Project: " + value.ProjectId + "\nSnapshot: " + value.SnapshotId + "\nCatalogDigest: " + (value.CatalogDigest ?? "In-memory catalog") +
                            "\nBuildId: " + (value.BuildId ?? "N/A") + "\nSchema: " + (value.SchemaVersion?.ToString() ?? "N/A") +
                            "\nPlatform: " + (value.Platform ?? "N/A") + "\nUnity: " + (value.UnityCompatibility ?? "N/A") + "\nApp: " + (value.AppContentCompatibilityId ?? "N/A") +
                            "\nSources: " + string.Join(", ", value.Sources));
                    break;
                case 1:
                    foreach (var value in report.Assets)
                        Add(value.Address + "  [" + (value.IsLoading ? "Loading" : value.IsImageIdle ? "Image idle" : "Ready") + "] leases=" + value.LeaseCount + " waiters=" + value.WaiterCount,
                            "Canonical: " + value.Address + "\nAssetId: " + value.AssetId + "\nSnapshot: " + value.SnapshotId + "\nCatalogDigest: " + value.CatalogDigest +
                            "\nSource: " + value.Source + "\nLocator: " + value.Locator + "\nType: " + value.DeclaredType?.FullName +
                            "\nProducer: " + value.IsLoading + " / progress=" + value.Progress.ToString("P0") + " / cancellation=" + value.CancellationRequested +
                            "\nLeases / instance pins / waiters: " + value.LeaseCount + " / " + value.InstancePins + " / " + value.WaiterCount +
                            "\nObject alive: " + value.ObjectAlive + " / instanceID=" + value.ObjectInstanceId +
                            "\nImage idle: " + value.IsImageIdle + " / estimate bytes=" + value.ImageBytes + " / idleSince=" + value.IdleSince +
                            "\nOwners:\n" + string.Join("\n", value.Owners.Select(x => "  " + (x.IsLabeled ? x.OwnerLabel : "Unknown (unlabeled lease)") + " | " + x.Address + (x.IsInstancePin ? " | Instance pin" : ""))));
                    break;
                case 2:
                    foreach (var value in report.Bundles)
                        Add(value.BundleId + "  [" + value.State + "] root=" + value.RootPins + " dep=" + value.DependencyPins + " waiters=" + value.WaiterCount,
                            "Project: " + value.ProjectId + "\nCatalogDigest: " + value.CatalogDigest + "\nBundle: " + value.BundleId + "\nNative: " + value.NativeName +
                            "\nState: " + value.State + " / native pending=" + value.NativePending + " / object alive=" + value.ObjectAlive +
                            "\nRootPins / DependencyPins / Waiters: " + value.RootPins + " / " + value.DependencyPins + " / " + value.WaiterCount +
                            "\nFileBytes (compressed): " + value.FileBytes + "\n为何不能卸载:\n" + string.Join("\n", value.RetentionReasons.Select(x =>
                                "  " + x.Kind + " ×" + x.Count + " | " + (x.OwnerLabel ?? "Unknown (unlabeled)") + " | " + x.Address + " | parent=" + x.ParentBundleId + "\n  " + x.Detail)) +
                            (value.Cause == null ? "" : "\nCause: " + value.Cause));
                    break;
                case 3:
                    foreach (var value in report.InstanceCaches)
                        Add(value.Address + "  active=" + value.ActiveCount + " idle=" + value.IdleCount + (value.IsRetired ? " [Retired]" : ""),
                            "Address: " + value.Address + "\nSnapshot: " + value.SnapshotId + "\nCatalogDigest: " + value.CatalogDigest + "\nPolicy: " + value.Policy +
                            "\nScene handle: " + value.SceneHandle + " / retired=" + value.IsRetired + "\nActive / Idle / Destroy pending: " + value.ActiveCount + " / " + value.IdleCount + " / " + value.DestroyPendingCount +
                            "\nDead awaiting prune: " + value.DeadAwaitingPruneCount + "\nTransitions / Operations: " + value.TransitionCount + " / " + value.OperationCount);
                    break;
                case 4:
                    foreach (var value in report.Scenes)
                        Add(value.Address + "  [" + value.State + "] handle=" + (value.Handle?.ToString() ?? "N/A"),
                            "Address: " + value.Address + "\nPath: " + value.Path + "\nSnapshot: " + value.SnapshotId + "\nCatalogDigest: " + value.CatalogDigest +
                            "\nHandle: " + (value.Handle?.ToString() ?? "N/A") + " / sequence=" + value.LoadSequence + "\nState: " + value.State +
                            "\nNative pending: " + value.NativePending + " / loaded=" + value.IsLoaded + " / ambiguous=" + value.IsAmbiguous +
                            "\nWaiting activation: " + value.WaitingForActivation + " / request=" + (value.RequestStatus?.ToString() ?? "N/A"));
                    break;
                case 5:
                    foreach (var value in report.Updates)
                        Add(value.Phase + "  " + value.Progress.ToString("P0") + " waiters=" + value.WaiterCount,
                            "Phase: " + value.Phase + "\nWaiters: " + value.WaiterCount + "\nProgress: " + value.Progress.ToString("P1") +
                            "\nTransferred / expected bytes: " + (value.TransferredBytes?.ToString() ?? "未测 / 不适用") + " / " + (value.ExpectedBytes?.ToString() ?? "未知") +
                            "\nRetries: " + (value.RetryCount?.ToString() ?? "不适用") + " / waiting=" + value.WaitingToRetry +
                            "\nDuration ms: " + (value.DurationMilliseconds?.ToString("F2") ?? "未测 / 不适用") + "\nActivation projects: " + string.Join(", ", value.ActivationProjects));
                    foreach (var note in report.RecoveryNotes) Add("Recovery", note);
                    break;
                case 6:
                    foreach (var value in report.History.Reverse())
                        Add(value.AtUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + value.Kind + " " + value.Status + " " + value.Address,
                            "UTC: " + value.AtUtc.ToString("u") + "\nKind: " + value.Kind + "\nStatus: " + value.Status + "\nAddress: " + value.Address +
                            "\nDuration ms: " + (value.HasDuration ? value.DurationMilliseconds.ToString("F2") : "未测 / 不适用") + "\nCause: " + (value.Cause ?? "None"));
                    break;
            }
            rows = result.ToArray(); rowsDirty = false;
        }

        public void Dispose() { automatic = false; report = null; sampledSystem = null; rows = Array.Empty<(string, string)>(); }
    }
}
