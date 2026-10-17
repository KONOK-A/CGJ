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
        private int selected = -1;
        private int mode;
        private string entryId;
        private string search = "";
        private bool focusSearch;
        private bool projectExpanded = true;
        private bool identityExpanded;
        private bool dependenciesExpanded;
        private Vector2 scroll;
        private Vector2 groupScroll;
        private Vector2 listScroll;
        private readonly HashSet<int> checkedEntries = new();
        private ResourceCatalogEntry[] visibleEntries = Array.Empty<ResourceCatalogEntry>();
        private string previewFilter;
        private int[] selectionRows = Array.Empty<int>();
        private string selectionFilter;
        private Object selectedSubAsset;
        private string selectedSubAssetId;
        private GUIStyle rowStyle;
        private GUIStyle RowStyle => rowStyle ??= new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };

        internal void Reset()
        {
            group = 0; selected = -1; mode = 0; entryId = null;
            checkedEntries.Clear(); listScroll = Vector2.zero; ClearSearch(); Invalidate();
        }
        internal void Invalidate()
        {
            map = null; catalog = null; simulation = null; visibleEntries = Array.Empty<ResourceCatalogEntry>();
            previewFilter = null; selectionFilter = null; selectedSubAsset = null; selectedSubAssetId = null;
        }
        internal void ClearSearch() { search = ""; previewFilter = null; selectionFilter = null; }
        internal void FocusSearch() => focusSearch = true;
        public void Dispose() => Invalidate();

        internal void SetPreview(ResourceBuildMap value, ResourceCatalog index, ResourceEditorSimulation resolver)
        {
            map = value; catalog = index; simulation = resolver; mode = 1; previewFilter = null; selectionFilter = null; selectedSubAssetId = null;
            checkedEntries.Clear();
            if (entryId == null || !catalog.Entries.Any(x => x.AssetId == entryId)) entryId = catalog.Entries.FirstOrDefault()?.AssetId;
        }

        internal void AddGroup(ResourceProject project)
        {
            var number = project.Groups.Count + 1;
            while (project.Groups.Any(x => string.Equals(x.Id, "group-" + number, StringComparison.OrdinalIgnoreCase))) number++;
            group = ResourceEditorOperations.AddGroup(project, "group-" + number);
            selected = -1; checkedEntries.Clear(); mode = 0;
        }

        internal void Draw(ResourceProject project, EditorWindow window, float availableWidth, Action changed, Action<Func<string>> run)
        {
            var sideInspector = availableWidth >= 870;
            var groupWidth = availableWidth < 700 ? 145 : 160;
            var entryWidth = sideInspector ? Mathf.Clamp(availableWidth * .38f, 290, 440) : Mathf.Max(250, availableWidth - groupWidth - 12);
            if (group >= project.Groups.Count) { group = Math.Max(0, project.Groups.Count - 1); checkedEntries.Clear(); selected = -1; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawProject(project, changed);
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(groupWidth));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("GROUPS", EditorStyles.miniBoldLabel);
            if (GUILayout.Button(new GUIContent("+", "新增 Group (Alt+G)"), EditorStyles.miniButton, GUILayout.Width(25)))
                run(() => { AddGroup(project); changed(); return "新增 Group 草稿，尚未包含条目。"; });
            EditorGUILayout.EndHorizontal();
            groupScroll = EditorGUILayout.BeginScrollView(groupScroll, GUILayout.Height(190));
            for (var i = 0; i < project.Groups.Count; i++)
            {
                var item = project.Groups[i];
                var title = new GUIContent(item.Id + " (" + item.Selections.Count + ")", item.Packing + "；显式文件/文件夹选择数，不是最终 Bundle 数。");
                if (GUILayout.Toggle(group == i, title, EditorStyles.miniButton) && group != i)
                { group = i; selected = -1; entryId = null; checkedEntries.Clear(); listScroll = Vector2.zero; previewFilter = null; }
            }
            EditorGUILayout.EndScrollView();
            if (project.Groups.Count == 0) GUILayout.Label("新增 Group 后加入文件或文件夹。", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("Together 合包\nPerAsset 按 GUID 分包\nScene、共享依赖与环归并可能改变最终分包。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(GUILayout.Width(entryWidth));
            if (project.Groups.Count != 0)
            {
                DrawGroup(project, changed, run);
                DrawEntries(project, window, changed, run);
            }
            else EditorGUILayout.HelpBox("Project 需要至少一个非空 Group 才能通过构建校验。", MessageType.Info);
            EditorGUILayout.EndVertical();
            if (sideInspector && project.Groups.Count != 0)
            {
                DrawInspector(project, changed, run, Mathf.Max(280, availableWidth - groupWidth - entryWidth - 20));
            }
            EditorGUILayout.EndHorizontal();
            if (!sideInspector && project.Groups.Count != 0) DrawInspector(project, changed, run);
            EditorGUILayout.EndScrollView();
            if (focusSearch && Event.current.type == EventType.Repaint && GUI.enabled)
            {
                EditorGUI.FocusTextInControl("Resource.EntrySearch");
                focusSearch = GUI.GetNameOfFocusedControl() != "Resource.EntrySearch";
            }
        }

        private void DrawProject(ResourceProject project, Action changed)
        {
            projectExpanded = EditorGUILayout.Foldout(projectExpanded, "Project 定义", true);
            if (!projectExpanded) return;
            using var serialized = new SerializedObject(project);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("DisplayName"), new GUIContent("显示名称"));
            var root = serialized.FindProperty("AssetRootGuid");
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(AssetDatabase.GUIDToAssetPath(root.stringValue));
            var next = (DefaultAsset)EditorGUILayout.ObjectField(new GUIContent("专用资源根", "构建资产及私有/共享依赖必须位于该目录内；不认领旧 Importer。"), folder, typeof(DefaultAsset), false);
            if (next != folder)
            {
                var path = AssetDatabase.GetAssetPath(next);
                if (!next || AssetDatabase.IsValidFolder(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                    path != "Assets/StreamingAssets" && !path.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal))
                    root.stringValue = next ? AssetDatabase.AssetPathToGUID(path) : "";
                else EditorGUILayout.HelpBox("资源根必须是 Assets 下的专用真实文件夹，不能使用 StreamingAssets。", MessageType.Error);
            }
            identityExpanded = EditorGUILayout.Foldout(identityExpanded, "高级：稳定身份", true);
            if (identityExpanded)
            {
                EditorGUILayout.HelpBox("ProjectId 是运行时地址与输出归属，不是显示名。修改不会迁移旧发布或缓存，必须重新构建，并同步业务常量。", MessageType.Warning);
                EditorGUILayout.PropertyField(serialized.FindProperty("ProjectId"));
                EditorGUILayout.SelectableLabel("配置 GUID：" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)), EditorStyles.miniLabel, GUILayout.Height(18));
            }
            if (serialized.ApplyModifiedProperties()) changed();
        }

        private void DrawGroup(ResourceProject project, Action changed, Action<Func<string>> run)
        {
            var current = project.Groups[group];
            EditorGUILayout.BeginHorizontal();
            var id = EditorGUILayout.DelayedTextField(new GUIContent("Group ID", "重命名影响下次目录与常量，不改源文件或已发布版本。"), current.Id);
            if (id != current.Id) run(() => { ResourceEditorOperations.RenameGroup(project, group, id); changed(); return "Group ID 已更新，下次构建重新生成目录。"; });
            if (GUILayout.Button("删除组", GUILayout.Width(64)) && EditorUtility.DisplayDialog("删除 Group 定义？",
                "Group: " + current.Id + "\n显式选择：" + current.Selections.Count + "\n仅删除定义及下次目录成员，不删除源资产、已发布 Bundle 或历史版本。可 Undo。", "删除定义", "取消"))
                run(() => { ResourceEditorOperations.RemoveGroup(project, group); selected = -1; checkedEntries.Clear(); changed(); return "Group 已移除，源资产未删除。"; });
            EditorGUILayout.EndHorizontal();
            if (group >= project.Groups.Count) return;
            using var serialized = new SerializedObject(project);
            EditorGUILayout.PropertyField(serialized.FindProperty("Groups").GetArrayElementAtIndex(group).FindPropertyRelative("Packing"),
                new GUIContent("Packing", "Together：组内主资产合包；PerAsset：按文件 GUID 分包。共享依赖和 Scene 会单独处理。"));
            if (serialized.ApplyModifiedProperties()) changed();
        }

        private void DrawEntries(ResourceProject project, EditorWindow window, Action changed, Action<Func<string>> run)
        {
            if (group >= project.Groups.Count) return;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName("Resource.EntrySearch");
            var next = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80));
            if (next != search) { search = next; previewFilter = null; listScroll = Vector2.zero; }
            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(24))) { ClearSearch(); GUI.FocusControl(null); }
            EditorGUILayout.EndHorizontal();
            mode = GUILayout.Toolbar(mode, new[] { "选择定义", "解析目录 / 子资产" });
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("加入选择", "加入 Project 窗口所选文件/文件夹；不写 Importer")))
                Add(project, Selection.objects, changed, run);
            var picked = EditorGUILayout.ObjectField(null, typeof(Object), false, GUILayout.Width(105));
            if (picked) Add(project, new[] { picked }, changed, run);
            if (catalog != null && GUILayout.Button(new GUIContent("选择", "定位当前目录条目的真实源资产，不加载 Runtime"), GUILayout.Width(50)))
                run(() => SelectSource(project));
            EditorGUILayout.EndHorizontal();
            var rect = GUILayoutUtility.GetRect(100, Mathf.Clamp(window.position.height * .28f, 125, 280), GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            HandleDrop(rect, project, changed, run);
            if (mode == 0) DrawSelectionRows(rect, project);
            else DrawResolvedRows(rect, project);
            if (mode == 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全选匹配", EditorStyles.miniButton))
                    foreach (var index in MatchingSelections(project)) checkedEntries.Add(index);
                if (GUILayout.Button("取消选择", EditorStyles.miniButton)) checkedEntries.Clear();
                using (new EditorGUI.DisabledScope(checkedEntries.Count == 0))
                {
                    if (GUILayout.Button("移动 " + checkedEntries.Count, EditorStyles.miniButton))
                    {
                        var menu = new GenericMenu();
                        var source = group; var indices = checkedEntries.OrderBy(x => x).ToArray();
                        for (var i = 0; i < project.Groups.Count; i++)
                        {
                            if (i == source) continue;
                            var target = i;
                            menu.AddItem(new GUIContent(project.Groups[i].Id), false, () => run(() =>
                            { ResourceEditorOperations.MoveSelections(project, source, indices, target); checkedEntries.Clear(); selected = -1; changed(); return "条目已移动；GUID、地址和 alias 保留。"; }));
                        }
                        if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("需要另一个 Group"));
                        menu.ShowAsContext();
                    }
                    if (GUILayout.Button("移除 " + checkedEntries.Count, EditorStyles.miniButton) && EditorUtility.DisplayDialog("移除选择定义？",
                        "移除 " + checkedEntries.Count + " 个文件/文件夹选择。文件夹展开成员会从下次目录退出；源资产、Importer 和已发布历史均不修改。可 Undo。", "移除定义", "取消"))
                        run(() => { ResourceEditorOperations.RemoveSelections(project, group, checkedEntries.ToArray()); checkedEntries.Clear(); selected = -1; changed(); return "已移除选择定义，源资产未修改。"; });
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Label("拖入此列表可批量加入文件/文件夹。搜索匹配源路径、地址或 alias；勾选可批量操作。", EditorStyles.wordWrappedMiniLabel);
            }
            else GUILayout.Label("预估目录来自当前定义。子资产键/name/type 由真实导入结果派生，不是可任意重命名的字段。", EditorStyles.wordWrappedMiniLabel);
        }

        private int[] MatchingSelections(ResourceProject project)
        {
            var key = group + "\n" + search;
            if (selectionFilter == key) return selectionRows;
            selectionRows = project.Groups[group].Selections.Select((value, index) => (value, index))
                .Where(x => Matches(AssetDatabase.GUIDToAssetPath(x.value.Guid)) || Matches(x.value.Address) || (x.value.Aliases ?? Array.Empty<string>()).Any(Matches))
                .Select(x => x.index).ToArray();
            selectionFilter = key;
            return selectionRows;
        }
        private bool Matches(string text) => string.IsNullOrEmpty(search) || (text ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void DrawSelectionRows(Rect rect, ResourceProject project)
        {
            var indices = MatchingSelections(project);
            if (indices.Length == 0) { GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 42), "没有匹配的条目。可从 Project 窗口拖入专用资源根内的文件/文件夹。", EditorStyles.wordWrappedMiniLabel); return; }
            DrawRows(rect, indices.Length, index =>
            {
                var item = indices[index];
                var value = project.Groups[group].Selections[item];
                var path = AssetDatabase.GUIDToAssetPath(value.Guid);
                var row = new Rect(4, index * 24, rect.width - 26, 23);
                var check = GUI.Toggle(new Rect(row.x, row.y + 2, 20, 20), checkedEntries.Contains(item), GUIContent.none);
                if (check) checkedEntries.Add(item); else checkedEntries.Remove(item);
                var root = AssetDatabase.GUIDToAssetPath(project.AssetRootGuid);
                var relative = path.StartsWith(root + "/", StringComparison.Ordinal) ? path.Substring(root.Length + 1) : path;
                var parent = Path.GetDirectoryName(relative)?.Replace('\\', '/');
                var label = string.IsNullOrEmpty(path) ? "[GUID missing] " + value.Guid : Path.GetFileName(path) +
                    (string.IsNullOrEmpty(parent) ? "" : "  / " + parent);
                var content = new GUIContent(label, AssetDatabase.GetCachedIcon(path), path + "\nAddress: " +
                    (string.IsNullOrEmpty(value.Address) ? relative : value.Address) + "\nAliases: " + string.Join(", ", value.Aliases ?? Array.Empty<string>()));
                if (GUI.Toggle(new Rect(25, row.y, row.width - 24, row.height), selected == item, content, RowStyle)) selected = item;
            });
        }

        private void DrawResolvedRows(Rect rect, ResourceProject project)
        {
            if (catalog == null || map == null)
            { GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 40), "尚无目录预估。请点击“刷新预览”；定义错误会明确返回，不读取旧发布冒充当前预览。", EditorStyles.wordWrappedMiniLabel); return; }
            var key = group + "\n" + search;
            if (previewFilter != key)
            {
                var members = new HashSet<string>(map.Groups.TryGetValue(project.Groups[group].Id, out var ids) ? ids : Array.Empty<string>());
                var guids = new HashSet<string>(map.Assets.Where(x => members.Contains(x.AssetId)).Select(x => x.Guid));
                visibleEntries = catalog.Entries.Where(x => guids.Contains(x.AssetId.Substring(0, 32)) &&
                    (Matches(x.Address.ToString()) || Matches(x.Location.Locator) || Matches(x.Location.AssetType.Name) || x.Aliases.Any(a => Matches(a.ToString())))).ToArray();
                if (!visibleEntries.Any(x => x.AssetId == entryId)) entryId = visibleEntries.FirstOrDefault()?.AssetId;
                previewFilter = key;
            }
            if (visibleEntries.Length == 0) GUI.Label(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, 30), "当前 Group 没有匹配的目录条目。", EditorStyles.miniLabel);
            DrawRows(rect, visibleEntries.Length, index =>
            {
                var item = visibleEntries[index];
                var text = item.Address.Path + (item.Address.SubAsset == null ? "" : " #" + item.Address.SubAsset) + "  [" + item.Location.AssetType.Name + "]";
                if (GUI.Toggle(new Rect(4, index * 24, rect.width - 30, 23), item.AssetId == entryId, new GUIContent(text, AssetDatabase.GetCachedIcon(item.Location.Locator), item.Address + "\n" + item.Location.Locator), RowStyle)) entryId = item.AssetId;
            });
        }

        private void DrawRows(Rect rect, int count, Action<int> row)
        {
            listScroll = GUI.BeginScrollView(rect, listScroll, new Rect(0, 0, Mathf.Max(0, rect.width - 18), Math.Max(rect.height - 2, count * 24 + 4)));
            var first = Mathf.Max(0, (int)(listScroll.y / 24));
            var end = Math.Min(count, first + Mathf.CeilToInt(rect.height / 24) + 2);
            for (var i = first; i < end; i++) row(i);
            GUI.EndScrollView();
        }

        private void DrawInspector(ResourceProject project, Action changed, Action<Func<string>> run, float width = 0)
        {
            if (group >= project.Groups.Count) return;
            if (width > 0) EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width));
            else EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            GUILayout.Label("ENTRY INSPECTOR", EditorStyles.miniBoldLabel);
            var entry = mode == 1 ? catalog?.Entries.FirstOrDefault(x => x.AssetId == entryId) : null;
            var selections = project.Groups[group].Selections;
            if (mode == 0 && (selected < 0 || selected >= selections.Count))
                GUILayout.Label("选择一行编辑地址与 alias；勾选框用于批量移动或移除。", EditorStyles.wordWrappedMiniLabel);
            else if (entry != null || mode == 0)
            {
                var path = entry?.Location.Locator ?? AssetDatabase.GUIDToAssetPath(selections[selected].Guid);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("源资产", AssetDatabase.LoadMainAssetAtPath(path), typeof(Object), false);
                EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(18));
                var type = entry?.Location.AssetType ?? AssetDatabase.GetMainAssetTypeAtPath(path);
                GUILayout.Label("Type: " + (type?.FullName ?? "Missing") + "  |  Source: AssetDatabase (borrowed)", EditorStyles.wordWrappedMiniLabel);
                if (mode == 0)
                {
                    using var serialized = new SerializedObject(project);
                    var item = serialized.FindProperty("Groups").GetArrayElementAtIndex(group).FindPropertyRelative("Selections").GetArrayElementAtIndex(selected);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        EditorGUILayout.HelpBox("文件夹递归展开，地址使用资源根相对路径。不能对文件夹覆写单个地址或 alias。Extensions 为空表示全部可打包类型。", MessageType.Info);
                        EditorGUILayout.PropertyField(item.FindPropertyRelative("Extensions"), new GUIContent("扩展名筛选"), true);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(item.FindPropertyRelative("Address"), new GUIContent("Address 覆写", "空值使用资源根相对路径；ordinal 匹配，拒绝大小写冲突。"));
                        EditorGUILayout.PropertyField(item.FindPropertyRelative("Aliases"), new GUIContent("Alias 地址"), true);
                    }
                    if (serialized.ApplyModifiedProperties()) changed();
                }
                else
                {
                    EditorGUILayout.SelectableLabel(entry.Address.ToString(), EditorStyles.textField, GUILayout.Height(18));
                    GUILayout.Label("AssetId: " + entry.AssetId + "\nAliases: " + string.Join(", ", entry.Aliases), EditorStyles.wordWrappedMiniLabel);
                    if (entry.Address.SubAsset != null)
                    {
                        if (selectedSubAssetId != entry.AssetId)
                        {
                            selectedSubAssetId = entry.AssetId;
                            selectedSubAsset = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).FirstOrDefault(x => x &&
                                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out string guid, out long id) && guid + "_" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) == entry.AssetId);
                        }
                        GUILayout.Label("Subasset name: " + (selectedSubAsset ? selectedSubAsset.name : "[导入已变化，请刷新]") + "  |  localId/key: " + entry.Address.SubAsset, EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.HelpBox("子资产身份由 Importer 派生；要编辑地址/alias，请在“选择定义”中编辑该主文件。子资产继承路径与 alias。", MessageType.Info);
                    }
                }
                if (map != null)
                {
                    var bundle = map.Bundles.FirstOrDefault(x => x.AssetPaths.Contains(path));
                    GUILayout.Label("最终 Bundle（预估）: " + (bundle?.Name ?? "未分配"), EditorStyles.wordWrappedMiniLabel);
                    dependenciesExpanded = EditorGUILayout.Foldout(dependenciesExpanded, "主要 / 私有 / 共享依赖（预估）", true);
                    if (dependenciesExpanded && bundle != null)
                    {
                        var explicitPaths = new HashSet<string>(map.Assets.Select(x => x.SourcePath));
                        foreach (var source in bundle.AssetPaths.Take(100)) GUILayout.Label((explicitPaths.Contains(source) ? "Main  " : bundle.IsShared ? "Shared  " : "Private  ") + source, EditorStyles.wordWrappedMiniLabel);
                        if (bundle.AssetPaths.Length > 100) GUILayout.Label("更多源文件见分包预览。", EditorStyles.miniLabel);
                        foreach (var edge in bundle.Dependencies) GUILayout.Label("→ " + edge, EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
            else GUILayout.Label("刷新目录并选择一个解析条目。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private string SelectSource(ResourceProject project)
        {
            if (mode == 0 && selected >= 0 && selected < project.Groups[group].Selections.Count)
            {
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(project.Groups[group].Selections[selected].Guid));
                EditorGUIUtility.PingObject(Selection.activeObject);
                return "已定位显式选择；不修改 Importer 或加载 Runtime。";
            }
            var entry = catalog?.Entries.FirstOrDefault(x => x.AssetId == entryId);
            if (entry == null && map != null && group < project.Groups.Count && map.Groups.TryGetValue(project.Groups[group].Id, out var members))
                entry = catalog.Entries.FirstOrDefault(x => members.Contains(x.AssetId));
            if (entry == null) throw new InvalidOperationException("请先刷新目录并选择条目。");
            Selection.activeObject = simulation.Load(entry.Address, entry.Location.AssetType);
            EditorGUIUtility.PingObject(Selection.activeObject);
            return "已定位 " + entry.Address + "；此对象属于 AssetDatabase，不是 Runtime lease。";
        }

        private void HandleDrop(Rect rect, ResourceProject project, Action changed, Action<Func<string>> run)
        {
            var current = Event.current;
            if (!GUI.enabled)
            {
                if (rect.Contains(current.mousePosition) && (current.rawType == EventType.DragUpdated || current.rawType == EventType.DragPerform))
                { DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; current.Use(); }
                return;
            }
            if (!rect.Contains(current.mousePosition) || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)) return;
            DragAndDrop.visualMode = DragAndDrop.objectReferences.Length == 0 ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            { DragAndDrop.AcceptDrag(); Add(project, DragAndDrop.objectReferences, changed, run); }
            current.Use();
        }

        private void Add(ResourceProject project, Object[] assets, Action changed, Action<Func<string>> run) => run(() =>
        { ResourceEditorOperations.AddSelections(project, group, assets); checkedEntries.Clear(); selected = project.Groups[group].Selections.Count - 1; mode = 0; changed(); return "已加入选择定义，尚未保存；未修改 Importer。"; });
    }
}
