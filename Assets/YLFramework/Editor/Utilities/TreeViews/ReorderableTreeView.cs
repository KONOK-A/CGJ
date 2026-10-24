using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#if UNITY_6000_2_OR_NEWER
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#endif

namespace YLFramework.Editor.Utilities
{
    /// <summary>
    /// 承接XF平面列表的TreeView构建/重排流程，按索引移动以保留空项和重复引用。
    /// 窗口停用时须Dispose；getter应返回owner当前的序列化列表，以跟随Undo替换。
    /// </summary>
    public class ReorderableTreeView<T> : TreeView, IDisposable where T : class
    {
        private static readonly string _dragKey = typeof(ReorderableTreeView<T>).FullName;
        private readonly ScriptableObject _owner;
        private readonly Func<IList<T>> _getData;
        private T[] _rowData;
        private bool _isDisposed;

        private sealed class DragData
        {
            public ReorderableTreeView<T> Source;
            public int[] Indices;
            public T[] Snapshot;
        }

        /// <summary>以owner当前的序列化列表建立平面树，创建后调用Reload。</summary>
        public ReorderableTreeView(ScriptableObject owner, Func<IList<T>> getData, TreeViewState state)
            : this(owner, getData, state, null)
        {
        }

        /// <summary>保留原作者工具的多列表头入口；数据与Undo责任仍归owner。</summary>
        public ReorderableTreeView(ScriptableObject owner, Func<IList<T>> getData, TreeViewState state,
            MultiColumnHeader header) : base(state, header)
        {
            if (!owner)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            this._owner = owner;
            this._getData = getData ?? throw new ArgumentNullException(nameof(getData));
            if (getData() == null)
            {
                throw new ArgumentException("列表getter不能返回null。", nameof(getData));
            }

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private IList<T> Data => _getData() ?? throw new InvalidOperationException("列表getter返回了null。");

        public override void OnGUI(Rect rect)
        {
            if (_isDisposed)
            {
                return;
            }

            if (!_owner)
            {
                Dispose();
                return;
            }
            RefreshChangedRows();
            base.OnGUI(rect);
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(0, -1) { children = new List<TreeViewItem>() };
            var datas = Data;
            OnBuildRoot(root, datas);
            _rowData = datas.ToArray();
            return root;
        }

        /// <summary>默认每个槽位一行；派生类可沿用原编辑器的行构建和RowGUI。</summary>
        protected virtual void OnBuildRoot(TreeViewItem root, IList<T> datas)
        {
            for (var i = 0; i < datas.Count; i++)
            {
                var item = datas[i];
                var text = item is UnityEngine.Object obj ? (obj ? obj.name : "(Missing)") : item?.ToString() ?? "(Empty)";
                root.AddChild(new TreeViewItem(i + 1, 0, text));
            }
        }

        protected override bool CanMultiSelect(TreeViewItem item) => item.depth == 0;

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            if (_isDisposed || !_owner || RefreshChangedRows())
            {
                return false;
            }

            return !Data.IsReadOnly && args.draggedItem != null &&
                args.draggedItem.depth == 0 && args.draggedItemIDs != null &&
                ReferenceEquals(FindItem(args.draggedItem.id, rootItem), args.draggedItem) &&
                GetSelection().Contains(args.draggedItem.id) && args.draggedItemIDs.Contains(args.draggedItem.id) &&
                GetIndices(args.draggedItemIDs) != null;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            if (PrepareDrag(args.draggedItemIDs))
            {
                DragAndDrop.StartDrag("Reorder items");
            }
        }

        private bool PrepareDrag(IList<int> ids)
        {
            if (_isDisposed || !_owner || RefreshChangedRows() || Data.IsReadOnly)
            {
                return false;
            }

            var indices = GetIndices(ids);
            if (indices == null)
            {
                return false;
            }

            var data = new DragData { Source = this, Indices = indices, Snapshot = Data.ToArray() };
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(_dragKey, data);
            return true;
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (_isDisposed || !_owner || args.parentItem != rootItem || rootItem.children == null ||
                args.insertAtIndex < 0 || args.insertAtIndex > rootItem.children.Count ||
                DragAndDrop.objectReferences.Length != 0 || (DragAndDrop.paths != null && DragAndDrop.paths.Length != 0))
            {
                return DragAndDropVisualMode.Rejected;
            }

            var drag = DragAndDrop.GetGenericData(_dragKey) as DragData;
            if (drag == null || !ReferenceEquals(drag.Source, this))
            {
                return DragAndDropVisualMode.Rejected;
            }

            var datas = Data;
            if (datas.IsReadOnly || datas.Count != drag.Snapshot.Length ||
                Enumerable.Range(0, datas.Count).Any(i => !ReferenceEquals(datas[i], drag.Snapshot[i])))
            {
                return DragAndDropVisualMode.Rejected;
            }

            var insertion = args.insertAtIndex == rootItem.children.Count ? datas.Count :
                GetItemIndex(rootItem.children[args.insertAtIndex].id);
            if (insertion < 0 || insertion > datas.Count)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (!args.performDrop)
            {
                return DragAndDropVisualMode.Move;
            }

            ClearOwnedDrag();
            var moves = drag.Indices.Select(i => datas[i]).ToArray();
            var reordered = new List<T>(datas);
            for (var i = drag.Indices.Length - 1; i >= 0; i--)
            {
                reordered.RemoveAt(drag.Indices[i]);
            }

            var destination = insertion - drag.Indices.Count(i => i < insertion);
            reordered.InsertRange(destination, moves);
            if (!Enumerable.Range(0, datas.Count).Any(i => !ReferenceEquals(datas[i], reordered[i])))
            {
                return DragAndDropVisualMode.Move;
            }

            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(_owner, "Reorder list");
            datas.Clear();
            foreach (var item in reordered)
            {
                datas.Add(item);
            }

            EditorUtility.SetDirty(_owner);
            Reload();
            var movedIndices = new HashSet<int>(Enumerable.Range(destination, moves.Length));
            SetSelection(rootItem.children.Where(item => movedIndices.Contains(GetItemIndex(item.id))).Select(item => item.id).ToArray());
            OnDragEnd();
            return DragAndDropVisualMode.Move;
        }

        private int[] GetIndices(IList<int> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return null;
            }

            var indices = ids.Select(GetItemIndex).Distinct().OrderBy(i => i).ToArray();
            return indices.Any(i => i < 0 || i >= Data.Count) ? null : indices;
        }

        /// <summary>默认行与列表槽位一一对应；筛选/分组行须覆盖此方法返回真实列表索引。</summary>
        protected virtual int GetItemIndex(int id)
        {
            var item = FindItem(id, rootItem);
            return item == null || rootItem.children == null ? -1 : rootItem.children.IndexOf(item);
        }

        /// <summary>沿用来源的刷新扩展点，但不再隐式删除空项、去重或改脏资产。</summary>
        protected virtual void RefreshDatas() => Reload();

        private bool RefreshChangedRows()
        {
            var datas = Data;
            var changed = _rowData == null || _rowData.Length != datas.Count;
            for (var i = 0; !changed && i < datas.Count; i++)
            {
                changed = !ReferenceEquals(_rowData[i], datas[i]);
            }

            if (!changed)
            {
                return false;
            }

            ClearOwnedDrag();
            RefreshDatas();
            return true;
        }

        /// <summary>一次实际重排完成后通知派生编辑器；悬停与原位放回不通知。</summary>
        protected virtual void OnDragEnd()
        {
        }

        private void OnUndoRedo()
        {
            if (_isDisposed)
            {
                return;
            }

            if (!_owner)
            {
                Dispose();
                return;
            }
            ClearOwnedDrag();
            RefreshDatas();
        }

        private void ClearOwnedDrag()
        {
            if (DragAndDrop.GetGenericData(_dragKey) is DragData drag && ReferenceEquals(drag.Source, this))
            {
                DragAndDrop.SetGenericData(_dragKey, null);
            }
        }

        /// <summary>窗口停用时归还本实例的Undo订阅和拖拽上下文，不清除其他窗口的拖拽。</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ClearOwnedDrag();
            _rowData = null;
        }
    }
}
