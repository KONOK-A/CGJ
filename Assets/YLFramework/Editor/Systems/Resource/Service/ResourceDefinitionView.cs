using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceDefinitionView : IDisposable
    {
        private ResourceBuildMap map;
        private ResourceCatalog catalog;
        private ResourceEditorSimulation simulation;
        private int group;
        private int mode;
        private string search = "";
        private bool focusSearch;
        private bool groupSettings;
        private bool help;
        private bool dirty = true;
        private bool previewStale;
        private int renderedMode = -1;
        private int observedDirty = -1;
        private string selectGuid;
        private DateTime previewAt;
        private ResourceEntryTable table;
        private string dropHint;
        private float tableWidth;
        private ResourceProject renameProject;
        private ResourceGroup renameGroup;
        private string renameText;
        private string renameOriginal;
        private readonly ResourceAtlasView atlas = new();
        internal bool IsAtlasMode => mode == 2;
        private readonly ResourceWorkbenchLayout fallbackLayout = new();
        private Vector2 groupScroll;
        private Vector2 detailScroll;
        private bool restoredUI;
        private string restoredNotice;

        internal void Reset()
        {
            group = 0; mode = 0; search = ""; selectGuid = null;
            map = null; catalog = null; simulation = null; table = null;
            renderedMode = -1; observedDirty = -1; dirty = true; previewStale = false;
            renameProject = null; renameGroup = null; renameText = null; renameOriginal = null;
            atlas.Reset();
            restoredUI = false; restoredNotice = null;
        }
        internal void Invalidate()
        {
            dirty = true;
            if (catalog != null) previewStale = true;
            atlas.Invalidate();
        }
        internal void ClearSearch() { search = ""; if (table != null) table.searchString = ""; }
        internal void ClearSelection() => table?.SetSelection(Array.Empty<int>());
        internal void Escape() { if (!string.IsNullOrEmpty(search)) ClearSearch(); else ClearSelection(); }
        internal void FocusSearch() { if (mode == 2) { mode = 0; dirty = true; } focusSearch = true; }
        internal bool HasPendingRename => renameProject && renameGroup != null && renameText != null && renameText != renameOriginal;
        internal bool CommitRename()
        {
            if (!HasPendingRename) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Unity状态转换中，尚未提交Group名称；请稳定后重试。");
            var index = renameProject.Groups.IndexOf(renameGroup);
            if (index < 0) throw new InvalidOperationException("重命名来源已变化，请重新选择Group。");
            if (renameGroup.Id != renameOriginal) throw new InvalidOperationException("Group名称已被外部编辑或Undo修改，请重新选择后编辑。");
            ResourceEditorOperations.RenameGroup(renameProject, index, renameText);
            renameGroup = renameProject.Groups[index]; renameText = renameOriginal = renameGroup.Id; dirty = true;
            return true;
        }
        public void Dispose() => Reset();

        internal void SetPreview(ResourceBuildMap value, ResourceCatalog index, ResourceEditorSimulation resolver)
        {
            map = value; catalog = index; simulation = resolver; mode = 1;
            previewStale = false; previewAt = DateTime.UtcNow; dirty = true;
            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(value.ProjectGuid));
            if (project) RefreshRows(project, fallbackLayout);
        }

        internal void AddGroup(ResourceProject project)
        {
            var number = project.Groups.Count + 1;
            while (project.Groups.Any(x => string.Equals(x.Id, "group-" + number, StringComparison.OrdinalIgnoreCase))) number++;
            group = ResourceEditorOperations.AddGroup(project, "group-" + number);
            ClearSelection(); mode = 0; dirty = true;
        }

        internal void Draw(ResourceProject project, EditorWindow window, float availableWidth, Action changed, Action<Func<string>> run, Action<Func<string>> queue = null, Action refresh = null)
        {
            var layout = (window as ResServiceEditorWindow)?.Layout ?? fallbackLayout;
            if (group >= project.Groups.Count) { group = Math.Max(-1, project.Groups.Count - 1); ClearSelection(); dirty = true; }
            if (observedDirty != EditorUtility.GetDirtyCount(project)) { observedDirty = EditorUtility.GetDirtyCount(project); dirty = true; }
            RefreshRows(project, layout);
            DrawToolbar(project, layout, availableWidth, changed, run, refresh);
            if (restoredNotice != null) GUILayout.Label(restoredNotice, ResourceWorkbenchGUI.Secondary);
            if (mode == 0 && project.Groups.Count == 0)
            {
                GUILayout.Space(18);
                GUILayout.Label("先创建一个 Group，再添加主资源", ResourceWorkbenchGUI.Title);
                GUILayout.Label("Group用于组织资源并设置分包方式，不是磁盘文件夹。同一个Group可以包含不同资源目录中的文件；登记目录本身不会把资源加入构建。", ResourceWorkbenchGUI.Secondary);
                GUILayout.Space(8);
                if (ResourceWorkbenchGUI.PrimaryAction("创建第一个 Group", GUILayout.Width(190))) run(() => { AddGroup(project); changed(); return "已创建 Group，尚未保存。"; });
                if (GUILayout.Button("设置资源目录", ResourceWorkbenchGUI.Button, GUILayout.Width(190))) layout.Settings = true;
                if (GUILayout.Button("查看完整入门流程", ResourceWorkbenchGUI.Button, GUILayout.Width(190))) (window as ResServiceEditorWindow)?.ShowGuide();
                GUILayout.FlexibleSpace();
                return;
            }
            if (groupSettings && group >= 0 && mode == 0) DrawGroup(project, changed, run);
            if (mode == 2) { atlas.Draw(project, availableWidth, changed, run, queue ?? run); return; }
            if (mode == 1)
                GUILayout.Label(catalog == null ? "尚未计算，请点击‘计算预览’。" : (previewStale ? "配置已变化，当前显示上次结果。请重新计算预览。" : "最近计算于 " + previewAt.ToLocalTime().ToString("HH:mm:ss")) + "  此处只解析配置，不会构建资源。", ResourceWorkbenchGUI.Secondary);
            if (help)
                EditorGUILayout.HelpBox("在 Unity Project 窗口选择文件或目录后，使用“添加资源...”菜单中的所选项入口，也可以直接拖入列表。目录会在解析或构建时展开；选中行只是编辑选择，不决定哪些资源参与构建。\nCtrl/Shift 多选；从定义移除不会删除源文件。子资产按主文件登记，不单独选择 Sprite。", MessageType.None);
            DrawBody(availableWidth, project, window, layout, changed, run);
            DrawBatch(project, changed, run, window);
            if (HasPendingRename && (!groupSettings || mode != 0 || group != project.Groups.IndexOf(renameGroup) || GUI.GetNameOfFocusedControl() != "Resource.GroupId"))
                run(() => { CommitRename(); changed(); return "Group名称已应用，可Undo；尚未保存。"; });
            if (focusSearch && Event.current.type == EventType.Repaint && GUI.enabled)
            {
                EditorGUI.FocusTextInControl("Resource.EntrySearch");
                focusSearch = GUI.GetNameOfFocusedControl() != "Resource.EntrySearch";
            }
        }

        private void DrawToolbar(ResourceProject project, ResourceWorkbenchLayout layout, float width, Action changed, Action<Func<string>> run, Action refresh)
        {
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            var nextMode = ResourceWorkbenchGUI.Navigation(mode, new[] { "主资源", "解析预览", "图集" }, false, GUILayout.Width(248));
            if (nextMode != mode) { mode = nextMode; ClearSelection(); dirty = true; detailScroll = Vector2.zero; }
            if (width < 1000 || !layout.ShowGroups)
            {
                if (GUILayout.Button(group < 0 ? "全部Group（只读）" : project.Groups[group].Id, EditorStyles.toolbarDropDown, GUILayout.MinWidth(90))) GroupMenu(project);
            }
            GUILayout.FlexibleSpace();
            if (width >= 1000) layout.ShowGroups = ResourceWorkbenchGUI.ToolbarToggle(layout.ShowGroups, "显示Group", 90, "只控制左侧分组列表的显示，不改变 Group 或分包方式。");
            if (GUILayout.Button(width >= 1000 ? (layout.ShowDetails ? "收起详情" : "展开详情") : (layout.CompactDetails ? "返回列表" : "查看详情"), ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(70)))
            {
                if (width >= 1000) layout.ShowDetails = !layout.ShowDetails; else layout.CompactDetails = !layout.CompactDetails;
            }
            help = ResourceWorkbenchGUI.ToolbarToggle(help, "帮助", 56);
            EditorGUILayout.EndHorizontal();
            if (mode == 2) return;
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            GUI.SetNextControlName("Resource.EntrySearch");
            var next = ResourceWorkbenchGUI.SearchField(search, mode == 0 ? "筛选主资源：名称、路径、GUID、地址" : "筛选解析结果：路径、地址、别名、GUID", GUILayout.MinWidth(100));
            if (next != search) { search = next; table.searchString = search; }
            if (GUILayout.Button("x", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(26))) { ClearSearch(); GUI.FocusControl(null); }
            using (new EditorGUI.DisabledScope(mode != 0 || group < 0))
            {
                if (GUILayout.Button(new GUIContent("添加资源...", "打开添加菜单；实际来源为 Unity Project 窗口当前选中的文件或目录，也可拖入列表"), ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(92)))
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("添加 Unity Project 窗口所选文件或目录"), false, () => Add(project, Selection.objects, changed, run));
                    menu.AddDisabledItem(new GUIContent("也可拖入列表；子资产按主文件登记"));
                    menu.ShowAsContext();
                }
                groupSettings = ResourceWorkbenchGUI.ToolbarToggle(groupSettings, "组设置", 76);
            }
            if (GUILayout.Button("+ Group", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(68))) run(() => { AddGroup(project); changed(); return "已新增空Group，尚未保存。"; });
            if (mode == 1 && refresh != null && GUILayout.Button("计算预览", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(74))) refresh();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroup(ResourceProject project, Action changed, Action<Func<string>> run)
        {
            if (group < 0 || group >= project.Groups.Count) return;
            var current = project.Groups[group];
            if (renameProject != project || renameGroup != current)
            {
                if (HasPendingRename) run(() => { CommitRename(); changed(); return "前一Group名称已应用，尚未保存。"; });
                if (HasPendingRename) { EditorGUILayout.HelpBox("前一Group的名称尚未应用，请返回修正后再编辑。", MessageType.Warning); return; }
                renameProject = project; renameGroup = current; renameText = renameOriginal = current.Id;
            }
            // 已提交文本不是新输入；外部Undo/Inspector改名应成为新的显示基线，不能被旧文本再次提交。
            else if (!HasPendingRename && current.Id != renameOriginal) renameText = renameOriginal = current.Id;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.SetNextControlName("Resource.GroupId");
            var enter = Event.current.rawType == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            renameText = ResourceWorkbenchGUI.TextField("Group 名称（ID）", renameText, false, "Enter、离开字段或Ctrl+S时应用；只修改定义，可Undo。Group是选择和分包意图，不是磁盘目录。");
            if (enter && HasPendingRename) run(() => { CommitRename(); changed(); return "Group名称已应用，尚未保存。"; });
            if (GUILayout.Button("删除组", ResourceWorkbenchGUI.Button, GUILayout.Width(64)) && EditorUtility.DisplayDialog("删除 Group 定义？",
                "Group: " + current.Id + "\n显式选择：" + current.Selections.Count + "\n仅删除定义及下次目录成员，不删除源资产、已发布 Bundle 或历史版本。可 Undo。", "删除定义", "取消"))
                run(() => { ResourceEditorOperations.RemoveGroup(project, group); renameProject = null; renameGroup = null; renameText = null; renameOriginal = null; groupSettings = false; ClearSelection(); dirty = true; changed(); return "Group已移除，源资产未删除。"; });
            EditorGUILayout.EndHorizontal();
            if (group >= project.Groups.Count) return;
            using var serialized = new SerializedObject(project);
            ResourceWorkbenchGUI.Property(serialized.FindProperty("Groups").GetArrayElementAtIndex(group).FindPropertyRelative("Packing"),
                new GUIContent("分包方式", "Together：组内主资产合包；PerAsset：按文件 GUID 分包。共享依赖和 Scene 会单独处理。"));
            if (serialized.ApplyModifiedProperties()) changed();
        }

        private void GroupMenu(ResourceProject project)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("全部Group（只读）"), group < 0, () => ChooseGroup(-1));
            for (var index = 0; index < project.Groups.Count; index++)
            { var captured = index; menu.AddItem(new GUIContent(project.Groups[index].Id), group == index, () => ChooseGroup(captured)); }
            menu.ShowAsContext();
        }

        private void ChooseGroup(int index)
        {
            group = index; ClearSelection(); dirty = true; groupScroll = Vector2.zero; detailScroll = Vector2.zero;
        }

        private void RefreshRows(ResourceProject project, ResourceWorkbenchLayout layout)
        {
            var restore = !restoredUI && layout.SelectionProjectGuid == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project));
            if (restore)
            {
                search = layout.EntrySearch ?? "";
                if (layout.SelectionGroup == null) group = -1;
                else
                {
                    var groups = project.Groups.Select((value, index) => (value.Id, index)).Where(x => x.Id == layout.SelectionGroup).ToArray();
                    if (groups.Length == 1) group = groups[0].index;
                    else restoredNotice = "上次查看的 Group 已变化，请重新选择；不会选择替代条目。";
                }
            }
            restoredUI = true;
            table ??= new ResourceEntryTable(layout);
            if (!dirty && renderedMode == mode) return;
            var oldMode = renderedMode;
            renderedMode = mode; dirty = false;
            if (mode == 0) table.SetDefinitions(project, group);
            else if (mode == 1)
            {
                IEnumerable<ResourceCatalogEntry> entries = catalog?.Entries ?? Array.Empty<ResourceCatalogEntry>();
                if (group >= 0 && group < project.Groups.Count && map != null)
                {
                    var members = new HashSet<string>(map.Groups.TryGetValue(project.Groups[group].Id, out var ids) ? ids : Array.Empty<string>());
                    var guids = new HashSet<string>(map.Assets.Where(x => members.Contains(x.AssetId)).Select(x => x.Guid));
                    entries = entries.Where(x => guids.Contains(x.AssetId.Substring(0, 32)));
                }
                table.SetResolved(project, entries, group < 0 ? "全部Group" : project.Groups[group].Id);
            }
            table.searchString = search;
            if (restore)
            {
                if (table.RestoreSelection(project, layout.SelectedRows) > 0) restoredNotice = "部分上次选择已删除、重复或发生变化，已清除；请重新选择。";
            }
            else if (selectGuid != null)
            {
                var row = table.GetRows().Cast<ResourceEntryTable.Row>().FirstOrDefault(x => x.Guid == selectGuid);
                if (row != null) table.SetSelection(new[] { row.id });
                selectGuid = null;
            }
            else if (oldMode != mode && table.GetRows().Count > 0) table.SetSelection(new[] { table.GetRows()[0].id });
        }

        private void DrawBody(float width, ResourceProject project, EditorWindow window, ResourceWorkbenchLayout layout, Action changed, Action<Func<string>> run)
        {
            var wide = width >= 1000;
            if (!wide && layout.CompactDetails)
            { DrawInspector(width, project, changed, run); return; }
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            if (wide && layout.ShowGroups)
            {
                layout.GroupWidth = Mathf.Clamp(layout.GroupWidth, 130, 260);
                DrawGroups(GUILayoutUtility.GetRect(layout.GroupWidth, layout.GroupWidth, 0, 100000, GUILayout.ExpandHeight(true)), project);
                var split = GUILayoutUtility.GetRect(6, 6, 0, 100000, GUILayout.ExpandHeight(true));
                ResourceWorkbenchGUI.Splitter(split, ref layout.GroupWidth, 130, 260);
            }
            var rect = GUILayoutUtility.GetRect(0, 100000, 80, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            table.Locate = row => run(() => Locate(row));
            table.Context = row => ContextMenu(row, project, changed, run);
            table.SelectionUpdated = () => { detailScroll = Vector2.zero; StoreUIState(project, layout); if (window) window.Repaint(); };
            // TreeView默认拖放会消费事件；外部资产必须先走定义的统一归属/编辑门禁。
            HandleDrop(rect, project, changed, run);
            table.OnGUI(rect);
            if (Event.current.type == EventType.Repaint && Mathf.Abs(tableWidth - rect.width) > 1)
            { tableWidth = rect.width; table.multiColumnHeader.ResizeToFit(); if (window) window.Repaint(); }
            if (!string.IsNullOrEmpty(dropHint)) GUI.Box(new Rect(rect.x + 6, rect.yMax - 48, rect.width - 12, 42), dropHint, EditorStyles.helpBox);
            if (table.VisibleCount == 0) GUI.Label(new Rect(rect.x + 12, rect.y + 42, rect.width - 24, 60),
                table.Count == 0 ? mode == 1 ? "尚无可显示的解析结果。请显式刷新预览。" : project.Groups.Count == 0 ? "尚无Group。点击 + Group 新建，再添加资源；专用根在项目设置中指定。" : group < 0 ? "全部Group只读聚合；请进入具体Group添加资源。" : "Group为空。添加或拖入文件/目录；不删除真实资产。" : "没有匹配记录；已选的隐藏条目仍保留。", ResourceWorkbenchGUI.Secondary);
            if (wide && layout.ShowDetails)
            {
                var max = Mathf.Min(440, width - (layout.ShowGroups ? layout.GroupWidth : 0) - 350);
                layout.DetailWidth = Mathf.Clamp(layout.DetailWidth, 280, max);
                var split = GUILayoutUtility.GetRect(6, 6, 0, 100000, GUILayout.ExpandHeight(true));
                ResourceWorkbenchGUI.Splitter(split, ref layout.DetailWidth, 280, max, -1);
                DrawInspector(layout.DetailWidth, project, changed, run);
            }
            EditorGUILayout.EndHorizontal();
        }

        internal void StoreUIState(ResourceProject project, ResourceWorkbenchLayout layout)
        {
            if (!project || table == null || mode != 0) return;
            layout.SelectionProjectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project));
            layout.SelectionGroup = group >= 0 && group < project.Groups.Count ? project.Groups[group].Id : null;
            layout.EntrySearch = search;
            layout.SelectedRows = table.CaptureSelection(project);
        }

        private void DrawGroups(Rect rect, ResourceProject project)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, 22), "资源分组", EditorStyles.boldLabel);
            rect.yMin += 32;
            groupScroll = GUI.BeginScrollView(rect, groupScroll, new Rect(0, 0, rect.width - 18, (project.Groups.Count + 1) * 27));
            var first = Mathf.Max(0, (int)(groupScroll.y / 27));
            var end = Math.Min(project.Groups.Count + 1, first + Mathf.CeilToInt(rect.height / 27) + 1);
            for (var index = first; index < end; index++)
            {
                var target = index - 1;
                var name = target < 0 ? "全部Group（只读）" : project.Groups[target].Id + "  (" + project.Groups[target].Selections.Count + ")";
                if (ResourceWorkbenchGUI.SelectableRow(new Rect(4, index * 27, rect.width - 26, 26), target == group, new GUIContent(name, target < 0 ? "聚合浏览，不执行跨Group编辑" : project.Groups[target].Packing.ToString())) && target != group) ChooseGroup(target);
            }
            GUI.EndScrollView();
        }

        private void DrawInspector(float width, ResourceProject project, Action changed, Action<Func<string>> run)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true));
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUILayout.ExpandHeight(true));
            GUILayout.Space(6);
            GUILayout.Label(table.GetSelection().Count > 1 ? "批量选择" : "条目详情", ResourceWorkbenchGUI.Title);
            var row = table.SingleSelection;
            if (row == null)
            {
                GUILayout.Label(table.GetSelection().Count == 0 ? "选择资源行查看详情。Ctrl/Shift可多选。" : "共选 " + table.GetSelection().Count + " 项，当前可见 " + table.VisibleSelectionCount + " 项。\n批量操作位于列表底部；不会静默批改地址。", ResourceWorkbenchGUI.Secondary);
            }
            else
            {
                GUILayout.Label(new GUIContent(row.displayName, row.Path), EditorStyles.boldLabel, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                GUILayout.Label(row.Cells[1] + " / " + row.Group + (group < 0 || mode == 1 ? " / 只读" : ""), ResourceWorkbenchGUI.Secondary);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("定位", ResourceWorkbenchGUI.Button, GUILayout.Width(50))) run(() => Locate(row));
                EditorGUILayout.EndHorizontal();
                ResourceWorkbenchGUI.CopyLine("源路径", row.Path, width - 22);
                if (mode == 0)
                {
                    var owner = project.Groups.FindIndex(x => x.Id == row.Group);
                    var indices = owner < 0 ? Array.Empty<int>() : project.Groups[owner].Selections.Select((value, index) => (value, index)).Where(x => x.value.Guid == row.Guid).Select(x => x.index).ToArray();
                    if (indices.Length != 1) EditorGUILayout.HelpBox("定义已变化或GUID重复，请刷新/重新选择；未编辑其他条目。", MessageType.Warning);
                    else
                    {
                    using var disabled = new EditorGUI.DisabledScope(group < 0);
                    using var serialized = new SerializedObject(project);
                    serialized.Update();
                    var item = serialized.FindProperty("Groups").GetArrayElementAtIndex(owner).FindPropertyRelative("Selections").GetArrayElementAtIndex(indices[0]);
                    var labelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = Mathf.Min(110, width * .35f);
                    if (row.Folder)
                    {
                        GUILayout.Label("解析或构建时递归展开目录。扩展名留空表示全部可打包类型；示例：.png。", ResourceWorkbenchGUI.Secondary);
                        ResourceWorkbenchGUI.Property(item.FindPropertyRelative("Extensions"), new GUIContent("扩展名筛选"));
                    }
                    else
                    {
                        ResourceWorkbenchGUI.Property(item.FindPropertyRelative("Address"), new GUIContent("资源地址", "留空使用所属资源目录的地址前缀和相对路径。地址与别名在资源项目内不得重复或发生大小写冲突。"));
                        ResourceWorkbenchGUI.Property(item.FindPropertyRelative("Aliases"), new GUIContent("地址别名", "Alias 是同一主资产的额外地址，不会复制资源。"));
                    }
                    if (serialized.ApplyModifiedProperties()) changed();
                    EditorGUIUtility.labelWidth = labelWidth;
                    }
                }
                else
                {
                    ResourceWorkbenchGUI.CopyLine("解析地址", row.Address, width - 22);
                    ResourceWorkbenchGUI.CopyLine("AssetId / localId", row.Entry.AssetId, width - 22);
                    ResourceWorkbenchGUI.CopyLine("地址别名", string.Join("\n", row.Entry.Aliases), width - 22);
                    if (row.Entry.Address.SubAsset != null) GUILayout.Label("子资产由主文件导入派生，继承主文件地址/alias；不独立编辑。", ResourceWorkbenchGUI.Secondary);
                }
                GUILayout.Space(8);
                ResourceWorkbenchGUI.CopyLine("源GUID", row.Guid, width - 22);
                if (map != null) GUILayout.Label("分包归属与依赖见“分包分析”；预估不代表已构建结果。", ResourceWorkbenchGUI.Secondary);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private string Locate(ResourceEntryTable.Row row)
        {
            Selection.activeObject = row.Entry == null ? AssetDatabase.LoadMainAssetAtPath(row.Path) : simulation.Load(row.Entry.Address, row.Entry.Location.AssetType);
            EditorGUIUtility.PingObject(Selection.activeObject);
            return "已在Unity Project窗口定位源资产；未执行运行时加载。";
        }

        private void HandleDrop(Rect rect, ResourceProject project, Action changed, Action<Func<string>> run)
        {
            var current = Event.current;
            if (current.rawType == EventType.DragExited) dropHint = null;
            if (!GUI.enabled || group < 0 || group >= project.Groups.Count || mode != 0 || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (rect.Contains(current.mousePosition) && (current.rawType == EventType.DragUpdated || current.rawType == EventType.DragPerform))
                { dropHint = "当前只读或编辑受阻；请进入具体Group的选择定义。"; DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; current.Use(); }
                return;
            }
            if (!rect.Contains(current.mousePosition) || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)) return;
            var paths = DragAndDrop.objectReferences.Select(AssetDatabase.GetAssetPath).ToArray();
            var valid = false;
            try
            {
                var directories = ResourceWorkbenchGUI.DisplayPaths(project);
                valid = paths.Length > 0 && paths.All(path => directories.ContainsSource(path) && !ResourceProjectPaths.Contains(ResourceProjectPaths.GeneratedDirectory(project), path));
            }
            catch (InvalidOperationException) { /* 拖放只显示准入提示；添加和构建仍由统一入口校验。 */ }
            dropHint = valid ? "添加主文件或文件夹，子资产由主文件派生。添加时会继续检查资源类型和重复条目。" :
                "无法添加：资源不在有效的资源目录中，或属于生成目录。请先在‘项目设置’中检查目录登记；不会自动扩大范围。";
            DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (current.type == EventType.DragPerform)
            { if (valid) { DragAndDrop.AcceptDrag(); Add(project, DragAndDrop.objectReferences, changed, run); } dropHint = null; }
            current.Use();
        }

        private void Add(ResourceProject project, Object[] assets, Action changed, Action<Func<string>> run) => run(() =>
        { ResourceEditorOperations.AddSelections(project, group, assets); ClearSearch(); selectGuid = project.Groups[group].Selections.Last().Guid; ClearSelection(); mode = 0; dirty = true; changed(); return "已加入主文件/目录选择定义，尚未保存；子资产由主文件派生，未修改Importer。"; });

        private void DrawBatch(ResourceProject project, Action changed, Action<Func<string>> run, EditorWindow window)
        {
            EditorGUILayout.BeginHorizontal(ResourceWorkbenchGUI.Toolbar);
            GUILayout.Label("选中 " + table.GetSelection().Count + " / 可见选中 " + table.VisibleSelectionCount + " / 匹配 " + table.VisibleCount, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("全选匹配", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(65))) table.SelectAllRows();
            if (GUILayout.Button("清选择", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(52))) ClearSelection();
            using (new EditorGUI.DisabledScope(mode != 0 || group < 0 || table.GetSelection().Count == 0))
            {
                if (GUILayout.Button("移动", ResourceWorkbenchGUI.Menu, GUILayout.Width(50))) MoveMenu(project, changed, run);
                if (GUILayout.Button("从定义移除", ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(82))) Remove(project, changed, run);
            }
            if (mode == 0 && GUILayout.Button(new GUIContent("前往构建发布", "只切换页面；请先保存资源项目，不会自动构建。"), ResourceWorkbenchGUI.ToolbarButton, GUILayout.Width(108)))
                (window as ResServiceEditorWindow)?.NavigateTask(2);
            EditorGUILayout.EndHorizontal();
        }

        private void MoveMenu(ResourceProject project, Action changed, Action<Func<string>> run)
        {
            var source = group;
            var sourceId = project.Groups[source].Id;
            var selection = table.GetSelection().ToArray();
            var menu = new GenericMenu();
            for (var index = 0; index < project.Groups.Count; index++)
            {
                if (index == source) continue;
                var targetId = project.Groups[index].Id;
                menu.AddItem(new GUIContent(project.Groups[index].Id), false, () => run(() =>
                {
                    table.SetSelection(selection);
                    var sourceIndex = project.Groups.FindIndex(x => x.Id == sourceId);
                    var targetIndex = project.Groups.FindIndex(x => x.Id == targetId);
                    if (sourceIndex < 0 || targetIndex < 0) throw new InvalidOperationException("Group已变化，请重新打开移动菜单。");
                    ResourceEditorOperations.MoveSelections(project, sourceIndex, table.GetSelectedIndices(project, sourceIndex), targetIndex);
                    ClearSelection(); dirty = true; changed();
                    return "已移动完整选中集合（包含筛选隐藏项），地址/alias保留，可Undo。";
                }));
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("需要另一个Group"));
            menu.ShowAsContext();
        }

        private void Remove(ResourceProject project, Action changed, Action<Func<string>> run) => run(() =>
        {
            ResourceEditorOperations.RemoveSelections(project, group, table.GetSelectedIndices(project, group));
            ClearSelection(); dirty = true; changed();
            return "已移除完整选择定义集合（含隐藏项），未删除源文件，可Undo。";
        });

        private void ContextMenu(ResourceEntryTable.Row row, ResourceProject project, Action changed, Action<Func<string>> run)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("定位源资产"), false, () => run(() => Locate(row)));
            menu.AddItem(new GUIContent("复制完整路径"), false, () => EditorGUIUtility.systemCopyBuffer = row.Path);
            menu.AddItem(new GUIContent("复制完整地址"), false, () => EditorGUIUtility.systemCopyBuffer = row.Address);
            if (group < 0 && mode == 0) menu.AddItem(new GUIContent("进入所属Group编辑"), false, () => { ChooseGroup(project.Groups.FindIndex(x => x.Id == row.Group)); selectGuid = row.Guid; });
            if (group >= 0 && mode == 0)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("移动完整选中集合..."), false, () => MoveMenu(project, changed, run));
                menu.AddItem(new GUIContent("从定义移除完整选中集合（可Undo）"), false, () => Remove(project, changed, run));
            }
            menu.ShowAsContext();
        }

        internal void LocateSource(ResourceProject project, ResourceBuildSource source)
        {
            var location = ResourceBuildSourceLocator.Resolve(project, source);
            if (location.Rule >= 0) { mode = 2; atlas.SelectRule(location.Rule); }
            else { mode = 0; group = location.Group; selectGuid = source.SelectionGuid; dirty = true; }
        }
    }
}
