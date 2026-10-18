using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceEntryTable : TreeView
    {
        internal sealed class Row : TreeViewItem
        {
            internal string Guid;
            internal string Group;
            internal string Path;
            internal string Address;
            internal bool Folder;
            internal ResourceCatalogEntry Entry;
            internal string Search;
            internal string[] Cells;
            internal GUIContent[] Content;
        }

        private readonly Dictionary<string, int> ids = new(StringComparer.Ordinal);
        private readonly Dictionary<int, Row> byId = new();
        private readonly HashSet<int> visibleIds = new();
        private Row[] rows = Array.Empty<Row>();
        private string projectGuid;
        private bool resolved;
        internal Action<Row> Locate;
        internal Action<Row> Context;
        internal Action SelectionUpdated;
        internal int Count => rows.Length;
        internal int VisibleCount => visibleIds.Count;
        internal Row SingleSelection => GetSelection().Count == 1 && byId.TryGetValue(GetSelection()[0], out var row) ? row : null;
        internal int VisibleSelectionCount
        {
            get { var count = 0; foreach (var id in GetSelection()) if (visibleIds.Contains(id)) count++; return count; }
        }

        internal ResourceEntryTable() : this(new ResourceWorkbenchLayout()) { }
        internal ResourceEntryTable(ResourceWorkbenchLayout layout) : base(new TreeViewState(), Header(layout))
        {
            rowHeight = ResourceWorkbenchGUI.RowHeight;
            showBorder = true;
            showAlternatingRowBackgrounds = false;
            enableItemHovering = true;
            multiColumnHeader.sortingChanged += _ => Reload();
            multiColumnHeader.height = ResourceWorkbenchGUI.BarHeight;
            Reload();
        }

        private static MultiColumnHeader Header(ResourceWorkbenchLayout layout)
        {
            var names = new[] { "名称", "类型 / 目录", "地址", "定义状态", "Group", "完整路径" };
            var widths = new[] { 210f, 88, 210, 98, 110, 300 };
            var state = new MultiColumnHeaderState(names.Select((name, index) => new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(name), width = widths[index], minWidth = index == 0 || index == 2 ? 100 : 64,
                autoResize = index == 0 || index == 2, canSort = true, allowToggleVisibility = index != 0
            }).ToArray()) { visibleColumns = new[] { 0, 1, 2, 3 } };
            if (layout.Columns != null && MultiColumnHeaderState.CanOverwriteSerializedFields(layout.Columns, state))
                MultiColumnHeaderState.OverwriteSerializedFields(layout.Columns, state);
            layout.Columns = state;
            return new MultiColumnHeader(state);
        }

        internal void SetDefinitions(ResourceProject project, int group)
        {
            SetProject(project);
            resolved = false;
            var next = new List<Row>();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            ResourceProjectPaths directories = null;
            try { directories = ResourceWorkbenchGUI.DisplayPaths(project); }
            catch (InvalidOperationException) { /* 无效草稿仍显示原条目；正式解析会给出具体阻塞原因。 */ }
            for (var index = 0; index < project.Groups.Count; index++)
            {
                if (group >= 0 && index != group) continue;
                var owner = project.Groups[index];
                foreach (var selection in owner.Selections)
                {
                    var path = AssetDatabase.GUIDToAssetPath(selection.Guid);
                    var folder = AssetDatabase.IsValidFolder(path);
                    var legal = directories != null && directories.ContainsSource(path);
                    var relative = legal ? directories.DisplayAddress(path) : path;
                    var address = folder ? relative + "/*" : string.IsNullOrEmpty(selection.Address) ? relative : selection.Address;
                    var key = "d/" + owner.Id + "/" + selection.Guid;
                    occurrences.TryGetValue(key, out var occurrence);
                    occurrences[key] = occurrence + 1;
                    var row = MakeRow(key + "/" + occurrence, selection.Guid, owner.Id, path, address,
                        folder ? "目录" : AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "资源缺失", !legal ? "目录范围无效" : folder ? "目录定义" : "文件定义");
                    row.Folder = folder;
                    row.Search += "\n" + string.Join("\n", selection.Aliases ?? Array.Empty<string>());
                    next.Add(row);
                }
            }
            UpdateRows(next.ToArray());
        }

        internal void SetResolved(ResourceProject project, IEnumerable<ResourceCatalogEntry> entries, string group)
        {
            SetProject(project);
            resolved = true;
            UpdateRows(entries.Select(entry =>
            {
                var row = MakeRow("r/" + entry.AssetId, entry.AssetId.Substring(0, 32), group, entry.Location.Locator,
                    entry.Address.ToString(), entry.Location.AssetType.Name, entry.Address.SubAsset == null ? "解析主资产" : "派生子资产");
                row.Entry = entry;
                row.Search += "\n" + string.Join("\n", entry.Aliases);
                return row;
            }).ToArray());
        }

        private void SetProject(ResourceProject project)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project));
            if (guid == projectGuid) return;
            projectGuid = guid;
            ids.Clear();
            SetSelection(Array.Empty<int>());
        }

        private Row MakeRow(string key, string guid, string group, string path, string address, string type, string status)
        {
            if (!ids.TryGetValue(key, out var id)) ids.Add(key, id = ids.Count + 1);
            var name = string.IsNullOrEmpty(path) ? "[资源GUID失效]" : Path.GetFileName(path);
            var cells = new[] { name, type, address, status, group, path };
            return new Row
            {
                id = id, depth = 0, displayName = name, Guid = guid, Group = group, Path = path, Address = address,
                Search = string.Join("\n", cells), Cells = cells,
                Content = cells.Select((cell, index) => new GUIContent(cell, index == 0 ? AssetDatabase.GetCachedIcon(path) : null, path + "\n" + address)).ToArray()
            };
        }

        private void UpdateRows(Row[] values)
        {
            rows = values;
            byId.Clear();
            foreach (var row in rows) byId.Add(row.id, row);
            var selection = GetSelection().Where(byId.ContainsKey).ToArray();
            Reload();
            SetSelection(selection);
        }

        internal int[] GetSelectedIndices(ResourceProject project, int group)
        {
            if (resolved || group < 0 || group >= project.Groups.Count) return Array.Empty<int>();
            if (AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) != projectGuid) throw new InvalidOperationException("Project已切换，请重新选择。");
            var owner = project.Groups[group];
            var indices = owner.Selections.Select((value, index) => (value.Guid, index)).ToLookup(x => x.Guid, StringComparer.Ordinal);
            var result = new List<int>();
            foreach (var id in GetSelection())
            {
                if (!byId.TryGetValue(id, out var row) || row.Group != owner.Id || indices[row.Guid].Count() != 1)
                    throw new InvalidOperationException("定义身份已变化或存在重复GUID，请刷新后重新选择；未执行部分操作。");
                result.Add(indices[row.Guid].Single().index);
            }
            return result.OrderBy(x => x).ToArray();
        }

        internal ResourceWorkbenchRowState[] CaptureSelection(ResourceProject project)
        {
            if (resolved) return Array.Empty<ResourceWorkbenchRowState>();
            var saved = new List<ResourceWorkbenchRowState>();
            foreach (var id in GetSelection())
            {
                if (!byId.TryGetValue(id, out var row)) continue;
                var groups = project.Groups.Where(x => x.Id == row.Group).ToArray();
                var definitions = groups.SelectMany(x => x.Selections).Where(x => x.Guid == row.Guid).ToArray();
                if (groups.Length != 1 || definitions.Length != 1) continue;
                saved.Add(new ResourceWorkbenchRowState { ProjectGuid = projectGuid, Group = row.Group, Guid = row.Guid,
                    Definition = groups[0].Packing + "/" + JsonUtility.ToJson(definitions[0]), Address = row.Address });
            }
            return saved.ToArray();
        }

        internal int RestoreSelection(ResourceProject project, ResourceWorkbenchRowState[] saved)
        {
            var selection = new List<int>();
            foreach (var item in saved ?? Array.Empty<ResourceWorkbenchRowState>())
            {
                if (resolved || item.ProjectGuid != projectGuid) continue;
                var groups = project.Groups.Where(x => x.Id == item.Group).ToArray();
                var definitions = groups.SelectMany(x => x.Selections).Where(x => x.Guid == item.Guid).ToArray();
                var matches = rows.Where(x => x.Group == item.Group && x.Guid == item.Guid && x.Address == item.Address).ToArray();
                if (groups.Length == 1 && definitions.Length == 1 && matches.Length == 1 &&
                    item.Definition == groups[0].Packing + "/" + JsonUtility.ToJson(definitions[0])) selection.Add(matches[0].id);
            }
            SetSelection(selection);
            return (saved?.Length ?? 0) - selection.Count;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(0, -1, "Root");
            foreach (var row in rows) root.AddChild(row);
            root.children ??= new List<TreeViewItem>();
            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var filtered = rows.Where(x => string.IsNullOrEmpty(searchString) || x.Search.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0);
            var column = multiColumnHeader.sortedColumnIndex;
            if (column >= 0) filtered = multiColumnHeader.IsSortedAscending(column)
                ? filtered.OrderBy(x => x.Cells[column], StringComparer.OrdinalIgnoreCase).ThenBy(x => x.id)
                : filtered.OrderByDescending(x => x.Cells[column], StringComparer.OrdinalIgnoreCase).ThenBy(x => x.id);
            var result = filtered.Cast<TreeViewItem>().ToList();
            visibleIds.Clear();
            foreach (var row in result) visibleIds.Add(row.id);
            return result;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var row = (Row)args.item;
            for (var index = 0; index < args.GetNumVisibleColumns(); index++)
            {
                var rect = args.GetCellRect(index);
                CenterRectUsingSingleLineHeight(ref rect);
                rect.xMin += 6;
                EditorGUI.LabelField(rect, row.Content[args.GetColumn(index)]);
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds) => SelectionUpdated?.Invoke();
        protected override void DoubleClickedItem(int id) { if (byId.TryGetValue(id, out var row)) Locate?.Invoke(row); }
        protected override void ContextClickedItem(int id)
        {
            if (!IsSelected(id)) SetSelection(new[] { id });
            if (byId.TryGetValue(id, out var row)) Context?.Invoke(row);
        }
    }
}
