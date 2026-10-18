using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceAnalysisView
    {
        private ResourceBundleTable table;
        private ResourceBundleDependencyTree tree;
        private ResourceBuildMap displayed;
        private ResourceBuildResult displayedBuild;
        private ResourceBundleTable.Row selected;
        private Dictionary<string, ResourceBuildAsset> mainAssets;
        private Dictionary<string, ResourceBuiltFile> raw;
        private Dictionary<string, ResourceBuiltFile> delivery;
        private Dictionary<string, ResourceBundleChange> changes;
        private int source;
        private int detail;
        private int placement;
        private bool compactDetail;
        private bool shared;
        private bool identity;
        private bool errorDetails;
        private string search = "";
        private string selectedPath;
        private bool displayedStale;
        private Vector2 detailScroll;
        private Vector2 fileScroll;
        internal Exception Error;
        internal ResourceAtlasPlan AtlasPending;
        internal void UseExact() { source = 1; displayed = null; }
        internal void Reset()
        {
            displayed = null; displayedBuild = null; table = null; tree = null; selected = null;
            source = detail = placement = 0; search = ""; Error = null; AtlasPending = null;
        }

        internal void Draw(ResourceProject project, ResourceBuildMap preview, ResourceEditorBuildReport report, bool reportStale, bool previewStale,
            float width, Action refresh, Action<ResourceBuildSource> locate)
        {
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            var next = ResourceWorkbenchGUI.Navigation(source, new[] { "当前配置预估", "这次构建结果" }, false, GUILayout.Width(230));
            if (next != source) { source = next; displayed = null; }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("计算当前预估", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(124))) { source = 0; refresh(); }
            EditorGUILayout.EndHorizontal();
            var build = source == 1 ? report?.Build : null;
            var stale = source == 1 ? reportStale : previewStale;
            displayedStale = stale;
            var map = source == 1 ? build?.Map : preview;
            if (source == 0 && AtlasPending != null)
                EditorGUILayout.HelpBox("图集需要生成或更新，当前还不能计算最终分包。请到“资源配置 > 图集”完成准备；旧结果不代表新规则。", MessageType.Warning);
            if (Error != null && source == 0)
            {
                EditorGUILayout.HelpBox("分包预估未完成。请查看错误详情并修正问题后重新计算；下方仍保留上次结果。", MessageType.Error);
                errorDetails = ResourceWorkbenchGUI.Disclosure(errorDetails, "分包计算错误详情");
                if (errorDetails) GUILayout.Label(ResourceEditorText.FailureDetails(Error), ResourceWorkbenchGUI.Secondary);
            }
            if (map == null)
            {
                GUILayout.Space(16); GUILayout.Label(source == 0 ? "尚无分包预估" : "这条执行记录没有构建结果", ResourceWorkbenchGUI.Title);
                GUILayout.Label(source == 0 ? "点击“计算当前预估”，查看资源如何分包以及包之间的依赖；这一步不会构建或发布。" : "先在构建页执行，或从底部“输出”的执行记录中选择已有构建。临时构建报告不会跨重载恢复。", ResourceWorkbenchGUI.Secondary);
                GUILayout.FlexibleSpace(); return;
            }
            if (table == null || !ReferenceEquals(displayed, map) || !ReferenceEquals(displayedBuild, build)) Bind(map, build);
            GUILayout.Label((source == 0 ? "预估 / " + (map.ContainsUnsavedDefinitions ? "含未保存配置" : "计算时配置已保存") : "构建结果 / " + report.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")) +
                " / " + map.ProjectId + " / " + (build?.Platform ?? map.Platform), EditorStyles.boldLabel);
            if (stale) EditorGUILayout.HelpBox("这是较早的结果，配置或资源可能已变化。来源仍保留，但不代表当前输入；请重新计算或构建。", MessageType.Warning);
            GUILayout.Label("资源包（Bundle） " + map.Bundles.Count + " / 主资源 " + map.Assets.Count + " / 分组 " + map.Groups.Count +
                " / 循环归并 " + map.MergedBundleCount + " / 估计减少的重复源文件量 " + ResourceWorkbenchGUI.Bytes(map.AvoidedDuplicateSourceBytes), ResourceWorkbenchGUI.Secondary);
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            GUI.SetNextControlName("Resource.BundleSearch");
            var text = ResourceWorkbenchGUI.SearchField(search, "筛选分包：名称、源路径、分包方式", GUILayout.MinWidth(90));
            if (text != search) { search = text; table.searchString = search; }
            var names = new[] { "全部分包方式", "按组", "按文件", "共享依赖", "场景依赖", "循环归并" };
            var filter = EditorGUILayout.Popup(placement, names, ResourceWorkbenchGUI.Popup, GUILayout.Width(width < 800 ? 120 : 160), GUILayout.Height(ResourceWorkbenchGUI.FieldHeight));
            var onlyShared = ResourceWorkbenchGUI.ToolbarToggle(shared, "仅共享", 74);
            if (filter != placement || shared != onlyShared)
            {
                placement = filter; shared = onlyShared; table.Placement = placement == 0 ? null : (ResourceBundlePlacement?)(placement - 1); table.SharedOnly = shared; table.Reload();
            }
            if (width < 900 && GUILayout.Button(compactDetail ? "返回列表" : "所选详情", EditorStyles.toolbarButton, GUILayout.Width(74))) compactDetail = !compactDetail;
            EditorGUILayout.EndHorizontal();
            var wide = width >= 900;
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            if (wide || !compactDetail)
                table.OnGUI(GUILayoutUtility.GetRect(0, 100000, 100, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
            if (!ReferenceEquals(selected, table.Selected))
            {
                selected = table.Selected; detailScroll = fileScroll = Vector2.zero; selectedPath = selected?.Paths.FirstOrDefault(); tree = null;
            }
            if (wide || compactDetail)
            {
                var detailWidth = wide ? width * .44f : width;
                EditorGUILayout.BeginVertical(GUILayout.Width(detailWidth), GUILayout.ExpandHeight(true));
                DrawDetails(project, detailWidth, locate); EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void Bind(ResourceBuildMap map, ResourceBuildResult build)
        {
            table ??= new ResourceBundleTable(); table.SetData(map, build);
            table.searchString = search; table.Placement = placement == 0 ? null : (ResourceBundlePlacement?)(placement - 1); table.SharedOnly = shared; table.Reload();
            displayed = map; displayedBuild = build;
            mainAssets = map.Assets.ToDictionary(x => x.SourcePath, StringComparer.Ordinal);
            raw = build?.Files.ToDictionary(x => x.Name, StringComparer.Ordinal);
            delivery = build?.DeliveryFiles.ToDictionary(x => x.Name, StringComparer.Ordinal);
            changes = build?.BundleChanges.ToDictionary(x => x.Name, StringComparer.Ordinal);
            selected = null; tree = null;
        }

        private void DrawDetails(ResourceProject project, float width, Action<ResourceBuildSource> locate)
        {
            if (selected == null) { GUILayout.Label("选择一个资源包，查看来源与依赖。", ResourceWorkbenchGUI.Secondary); return; }
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            GUILayout.Label(ResourceBundleTable.PlacementName(selected.Bundle.Placement) + (selected.Bundle.IsShared ? " · 共享" : selected.Bundle.IsScene ? " · 场景" : " · 主资源"), EditorStyles.boldLabel);
            GUILayout.Label("来源分组：" + selected.Groups, EditorStyles.wordWrappedLabel);
            var next = ResourceWorkbenchGUI.Navigation(detail, new[] { "源文件", "依赖", "被依赖", "构建值" });
            if (next != detail) { detail = next; tree = null; }
            if (detail == 0) DrawFiles(width, locate);
            else if (detail == 1 || detail == 2)
            {
                GUILayout.Label("这里展示包之间的依赖，不代表某个文件的精确引入链。双击节点可定位相应包。", ResourceWorkbenchGUI.Secondary);
                tree ??= new ResourceBundleDependencyTree(table, selected.Bundle.Name, detail == 2, name =>
                {
                    table.SelectBundle(name); search = ""; placement = 0; shared = false;
                });
                var rect = GUILayoutUtility.GetRect(50, 260, GUILayout.ExpandWidth(true)); tree.OnGUI(rect);
            }
            else DrawBuildValues(width);
            identity = ResourceWorkbenchGUI.Disclosure(identity, "技术详情：完整包名与来源");
            if (identity)
            {
                ResourceWorkbenchGUI.CopyLine("完整包名（BundleName）", selected.Bundle.Name, width - 20);
                ResourceWorkbenchGUI.Metric("分包方式原值", selected.Bundle.Placement.ToString());
                if (selectedPath != null && mainAssets.TryGetValue(selectedPath, out var main))
                {
                    ResourceWorkbenchGUI.CopyLine("源文件 GUID", main.Guid, width - 20);
                    if (main.Source != null) ResourceWorkbenchGUI.CopyLine("原登记路径", main.Source.SelectionPath, width - 20);
                }
                else if (selectedPath != null) GUILayout.Label("原结果没有记录此依赖文件的独立 GUID，不能仅凭当前路径确认它仍是同一文件。", ResourceWorkbenchGUI.Secondary);
                if (changes != null && changes.TryGetValue(selected.Bundle.Name, out var change))
                    GUILayout.Label("内容对比原因（原始记录）：\n" + change.Reason, ResourceWorkbenchGUI.Secondary);
                GUILayout.Label("源文件字节、构建文件、交付文件和发布复制量各有不同含义，不能据此推算原生内存。", ResourceWorkbenchGUI.Secondary);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawFiles(float width, Action<ResourceBuildSource> locate)
        {
            GUILayout.Label("主资源有直接加载地址；依赖文件随包参与构建，没有独立加载地址。", ResourceWorkbenchGUI.Secondary);
            var rowHeight = ResourceWorkbenchGUI.RowHeight;
            var rect = GUILayoutUtility.GetRect(80, Mathf.Clamp(selected.Paths.Length * rowHeight, rowHeight, rowHeight * 4), GUILayout.ExpandWidth(true));
            var scrollable = selected.Paths.Length > 4;
            if (scrollable) fileScroll = GUI.BeginScrollView(rect, fileScroll, new Rect(0, 0, Math.Max(20, rect.width - 18), selected.Paths.Length * rowHeight));
            var first = scrollable ? Math.Max(0, (int)(fileScroll.y / rowHeight)) : 0;
            for (var i = first; i < Math.Min(selected.Paths.Length, first + 5); i++)
            {
                var path = selected.Paths[i];
                var main = mainAssets.TryGetValue(path, out var asset);
                var label = (main ? "主资源  " : "依赖文件  ") + System.IO.Path.GetFileName(path);
                if (GUI.Toggle(new Rect((scrollable ? 0 : rect.x) + 2, (scrollable ? 0 : rect.y) + i * rowHeight, Math.Max(20, rect.width - 24), rowHeight), selectedPath == path,
                    new GUIContent(label, path + (main ? "\n" + asset.Address : "\n无直接加载地址")), EditorStyles.miniButton)) selectedPath = path;
            }
            if (scrollable) GUI.EndScrollView();
            if (selectedPath == null) return;
            ResourceWorkbenchGUI.CopyLine("源文件路径", selectedPath, width - 20);
            var isMain = mainAssets.TryGetValue(selectedPath, out var currentAsset);
            var currentIdentity = isMain ? AssetDatabase.GUIDToAssetPath(currentAsset.Guid) == selectedPath : !displayedStale;
            using (new EditorGUI.DisabledScope(!currentIdentity))
                if (GUILayout.Button("定位源资产", ResourceWorkbenchGUI.Button)) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(selectedPath));
            if (!currentIdentity) GUILayout.Label("源文件或当前配置已变化，不能再按旧路径定位。请重新计算或构建。", ResourceWorkbenchGUI.Secondary);
            if (!mainAssets.TryGetValue(selectedPath, out var selectedAsset)) { GUILayout.Label("这是随包构建的依赖文件；报告未记录它由哪个具体文件引入。", ResourceWorkbenchGUI.Secondary); return; }
            ResourceWorkbenchGUI.CopyLine("可加载主地址", selectedAsset.Address.ToString(), width - 20);
            var origin = selectedAsset.Source;
            if (origin == null) { GUILayout.Label("此报告没有保留来源配置，无法定位；不会用当前项目配置替代原记录。", ResourceWorkbenchGUI.Secondary); return; }
            GUILayout.Label("计算时来源：" + origin.GroupId + " / " + (origin.Packing == ResourcePacking.Together ? "按组合包" : "按文件分包") + (origin.AtlasRuleId == null ? " / 资源选择项" : " / 自动图集规则 " + origin.AtlasRuleId), EditorStyles.boldLabel);
            ResourceWorkbenchGUI.Metric("登记来源", System.IO.Path.GetFileName(origin.SelectionPath), origin.SelectionPath);
            if (origin.Aliases.Count > 0) GUILayout.Label("地址别名：" + string.Join(", ", origin.Aliases), EditorStyles.wordWrappedLabel);
            if (origin.Extensions.Count > 0) GUILayout.Label("扩展名筛选：" + string.Join(", ", origin.Extensions), EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("定位资源配置中的来源", ResourceWorkbenchGUI.Button)) locate(origin);
        }

        private void DrawBuildValues(float width)
        {
            if (displayedBuild == null) { GUILayout.Label("预估只提供源文件量。构建文件量、交付文件量和 SHA256 需实际构建后查看；原生内存未测量。", ResourceWorkbenchGUI.Secondary); return; }
            var name = selected.Bundle.Name;
            raw.TryGetValue(name, out var file); delivery.TryGetValue(name, out var encoded); changes.TryGetValue(name, out var change);
            ResourceWorkbenchGUI.Metric("源文件量", ResourceWorkbenchGUI.Bytes(selected.Bundle.SourceBytes), selected.Bundle.SourceBytes + " B");
            ResourceWorkbenchGUI.Metric("构建文件量（raw）", ResourceWorkbenchGUI.Bytes(file?.Length), file?.Length + " B");
            ResourceWorkbenchGUI.Metric("交付文件量", ResourceWorkbenchGUI.Bytes(encoded?.Length), encoded?.Length + " B");
            ResourceWorkbenchGUI.Metric("内容是否变化", change?.ContentChanged.HasValue == true ? change.ContentChanged.Value ? "已变化" : "未变化" : "未知（无可比基线）");
            ResourceWorkbenchGUI.Metric("原生内存", "未测量");
            ResourceWorkbenchGUI.CopyLine("raw SHA256", file?.Sha256 ?? "不可用", width - 20);
            ResourceWorkbenchGUI.CopyLine("delivery SHA256", encoded?.Sha256 ?? "不可用", width - 20);
            ResourceWorkbenchGUI.CopyLine("InputFingerprint", displayedBuild.InputFingerprint, width - 20);
            GUILayout.Label("原始构建文件总字节（含 Manifest 配套文件）=" + displayedBuild.Files.Sum(x => x.Length) + " / Bundle 原始文件字节=" + displayedBuild.BundleFileBytes +
                " / 交付文件总字节=" + displayedBuild.DeliveryBytes + "。发布复制量由对应的发布报告提供，不能将 Builder.CopiedBytes 视为实测值。", ResourceWorkbenchGUI.Secondary);
        }
    }

    internal sealed class ResourceBundleDependencyTree : TreeView
    {
        private readonly ResourceBundleTable table;
        private readonly string bundle;
        private readonly bool reverse;
        private readonly Action<string> locate;
        private readonly Dictionary<string, int> ids = new(StringComparer.Ordinal);
        private readonly Dictionary<int, string> names = new();
        internal ResourceBundleDependencyTree(ResourceBundleTable table, string bundle, bool reverse, Action<string> locate) : base(new TreeViewState())
        {
            this.table = table; this.bundle = bundle; this.reverse = reverse; this.locate = locate;
            rowHeight = 24; showBorder = true; Reload(); SetExpanded(1, true);
        }
        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(0, -1, "包依赖"); names.Clear();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<(TreeViewItem Parent, string Name, string Key)>();
            pending.Push((root, bundle, bundle));
            var count = 0;
            while (pending.Count > 0 && count++ < 16384)
            {
                var item = pending.Pop();
                if (!ids.TryGetValue(item.Key, out var id)) ids.Add(item.Key, id = ids.Count + 1);
                var row = table.Find(item.Name); if (row == null) continue;
                names[id] = item.Name;
                var repeated = !visited.Add(item.Name);
                var node = new TreeViewItem(id, item.Parent.depth + 1, row.displayName + (repeated ? "（重复引用）" : "")); item.Parent.AddChild(node);
                var edges = reverse ? row.Dependents : row.Dependencies;
                if (edges.Length == 0) continue;
                if (!IsExpanded(id)) { node.children = CreateChildListForCollapsedParent(); continue; }
                for (var i = edges.Length - 1; i >= 0; i--) pending.Push((node, edges[i], item.Key + "/" + edges[i]));
            }
            if (pending.Count > 0) root.AddChild(new TreeViewItem(int.MaxValue, 0, "本次展开已达16384个节点上限，可双击跳转后继续查看。"));
            root.children ??= new List<TreeViewItem>(); return root;
        }
        protected override void ExpandedStateChanged() => Reload();
        protected override void DoubleClickedItem(int id) { if (names.TryGetValue(id, out var name)) locate(name); }
    }
}
