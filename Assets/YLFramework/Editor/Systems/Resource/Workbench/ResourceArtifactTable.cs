using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Resource;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceArtifactTable : TreeView
    {
        private sealed class Row : TreeViewItem
        {
            internal ArtifactEntry Entry;
            internal string[] Cells;
            internal string Search;
        }
        private Row[] _rows = Array.Empty<Row>();
        private readonly Dictionary<int, Row> _ids = new();
        internal bool AlternateRows = true;
        internal ArtifactEntry Selected => GetSelection().Count == 1 && _ids.TryGetValue(GetSelection()[0], out var row) ? row.Entry : null;
        internal ResourceArtifactTable() : base(new TreeViewState(), Header())
        {
            rowHeight = ResourceWorkbenchGui.RowHeight;
            showBorder = true;
            showAlternatingRowBackgrounds = false;
            multiColumnHeader.height = ResourceWorkbenchGui.BarHeight;
            multiColumnHeader.sortingChanged += _ => Reload();
            Reload();
        }
        private static MultiColumnHeader Header()
        {
            var names = new[] { "路径 / 文件", "类型", "条目状态", "观测字节", "空间观测", "清理候选", "保护原因" };
            var help = new[] { "Path：完整路径可在详情中复制。", "Kind：版本、暂存、下载分段或元数据。", "State：登记完整不表示 Bundle SHA256、签名或运行时加载检查通过。", "ObservedBytes：普通文件逻辑长度，不是实际磁盘占用；按原数值排序。", "IsSizeComplete：观测不完整或未知时，不将已观测字节视为总量。", "IsCleanupCandidate：清理候选不保证可释放同等磁盘空间；本页不执行删除。", "Protection：多个保护原因可以叠加。" };
            var widths = new[] { 190f, 80, 96, 104, 80, 80, 150 };
            return new MultiColumnHeader(new MultiColumnHeaderState(names.Select((name, i) => new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(name, help[i]),
                width = widths[i],
                minWidth = i == 0 ? 100 : 60,
                autoResize = i == 0,
                canSort = true,
                allowToggleVisibility = i != 0
            }).ToArray())
            {
                visibleColumns = new[] { 0, 2, 3, 6 }
            });
        }
        internal void SetData(ArtifactStoreReport report)
        {
            _rows = report.Entries.Select((entry, i) =>
            {
                var cells = new[] { System.IO.Path.GetFileName(entry.Path), KindLabel(entry.Kind), StateLabel(entry.State),
                    entry.IsSizeComplete ? ResourceWorkbenchGui.Bytes(entry.ObservedBytes) : "未知（已见 " + ResourceWorkbenchGui.Bytes(entry.ObservedBytes) + "）", entry.IsSizeComplete ? "完整" : "不完整",
                    report.Status != ArtifactQueryStatus.Ready || !entry.IsSizeComplete ? "未确认" : entry.IsCleanupCandidate ? "是" : "否", ProtectionLabel(entry.Protection) };
                return new Row
                {
                    id = i + 1,
                    depth = 0,
                    displayName = cells[0],
                    Entry = entry,
                    Cells = cells,
                    Search = entry.Path + "\n" + entry.ProjectId + "\n" + entry.Platform + "\n" + entry.CatalogDigest + "\n" + entry.State + "\n" + entry.Protection + "\n" + string.Join("\n", cells) + "\n" + string.Join("\n", entry.Issues)
                };
            }).ToArray();
            _ids.Clear();
            foreach (var row in _rows)
            {
                _ids.Add(row.id, row);
            }

            SetSelection(Array.Empty<int>());
            Reload();
        }
        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(0, -1, "缓存条目");
            foreach (var row in _rows)
            {
                root.AddChild(row);
            }

            root.children ??= new List<TreeViewItem>();
            return root;
        }
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var values = _rows.Where(x => string.IsNullOrEmpty(searchString) || x.Search.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0);
            var column = multiColumnHeader.sortedColumnIndex;
            if (column >= 0)
            {
                if (column == 3)
                {
                    values = multiColumnHeader.IsSortedAscending(column) ? values.OrderBy(x => x.Entry.ObservedBytes).ThenBy(x => x.id) : values.OrderByDescending(x => x.Entry.ObservedBytes).ThenBy(x => x.id);
                }
                else
                {
                    values = multiColumnHeader.IsSortedAscending(column) ? values.OrderBy(x => column == 0 ? x.Entry.Path : x.Cells[column], StringComparer.Ordinal).ThenBy(x => x.id) :
                    values.OrderByDescending(x => column == 0 ? x.Entry.Path : x.Cells[column], StringComparer.Ordinal).ThenBy(x => x.id);
                }
            }
            return values.Cast<TreeViewItem>().ToList();
        }
        protected override void RowGUI(RowGUIArgs args)
        {
            var row = (Row)args.item;
            if (AlternateRows && !args.selected && args.row % 2 == 1 && Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(args.rowRect, EditorGUIUtility.isProSkin ? new Color(1, 1, 1, .025f) : new Color(0, 0, 0, .025f));
            }

            for (var i = 0; i < args.GetNumVisibleColumns(); i++)
            {
                var rect = args.GetCellRect(i);
                rect.xMin += 5;
                EditorGUI.LabelField(rect, new GUIContent(row.Cells[args.GetColumn(i)], "路径：" + row.Entry.Path + "\n条目状态：" + StateLabel(row.Entry.State) + "（" + row.Entry.State + "）\n保护原因：" + ProtectionLabel(row.Entry.Protection) +
                    "\n观测字节：" + row.Entry.ObservedBytes + "\n观测完整：" + (row.Entry.IsSizeComplete ? "是" : "否")), EditorStyles.label);
            }
        }

        private static string KindLabel(ArtifactKind value) => value switch
        {
            ArtifactKind.Version => "版本目录",
            ArtifactKind.Staging => "暂存目录",
            ArtifactKind.Partial => "下载分段",
            ArtifactKind.Metadata => "元数据",
            _ => "未知类型（" + value + "）"
        };

        internal static string StateLabel(ArtifactState value) => value switch
        {
            ArtifactState.CompleteRecord => "登记完整",
            ArtifactState.Staging => "暂存",
            ArtifactState.Partial => "分段下载",
            ArtifactState.Incomplete => "登记缺件",
            ArtifactState.Unknown => "未知",
            ArtifactState.Invalid => "无效",
            ArtifactState.Unreadable => "不可读",
            ArtifactState.Linked => "链接未跟随",
            _ => "未知状态（" + value + "）"
        };

        internal static string ProtectionLabel(ArtifactProtection value)
        {
            if (value == ArtifactProtection.None)
            {
                return "无";
            }

            return string.Join("、", new[]
            {
                (ArtifactProtection.Active, "当前版本"), (ArtifactProtection.LastKnownGood, "最近可用"),
                (ArtifactProtection.PreparedFrom, "待提交来源"), (ArtifactProtection.PreparedTo, "待提交目标"),
                (ArtifactProtection.Mounted, "已挂载"), (ArtifactProtection.Held, "仍被持有"), (ArtifactProtection.InFlight, "操作中")
            }.Where(x => (value & x.Item1) != 0).Select(x => x.Item2));
        }
    }
}
