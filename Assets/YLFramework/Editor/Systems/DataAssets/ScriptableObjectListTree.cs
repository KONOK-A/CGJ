using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.DataAssets.Editor
{
    public sealed class ScriptableObjectListTree : ReorderableTreeView<ScriptableObjectData>
    {
        private readonly ScriptableObjectConfigs _owner;
        private ScriptableObjectData[] _rows = Array.Empty<ScriptableObjectData>();
        private readonly GUIContent _content = new GUIContent();
        public Action<ScriptableObjectData> Selected;
        public Action Changed;

        public ScriptableObjectListTree(ScriptableObjectConfigs owner, TreeViewState state)
            : base(owner, () => owner.configs, state, new MultiColumnHeader(CreateHeader()))
        {
            this._owner = owner;
            rowHeight = 25;
            showBorder = true;
            multiColumnHeader.sortingChanged += SortChanged;
            Reload();
        }

        private static MultiColumnHeaderState CreateHeader() => new MultiColumnHeaderState(new[]
        {
            new MultiColumnHeaderState.Column { headerContent = new GUIContent("Icon"), width = 42, minWidth = 42, maxWidth = 42, canSort = false, autoResize = false },
            new MultiColumnHeaderState.Column { headerContent = new GUIContent("ID"), width = 65, minWidth = 45, canSort = true, autoResize = false },
            new MultiColumnHeaderState.Column { headerContent = new GUIContent("Name"), width = 220, minWidth = 80, canSort = true, autoResize = true }
        });

        protected override void OnBuildRoot(TreeViewItem root, IList<ScriptableObjectData> data)
        {
            _rows = data.ToArray();
            // TreeView ID是槽位，不是业务ID；坏配置/重复ID仍可选中并修复。
            for (var i = 0; i < _rows.Length; i++)
            {
                var item = _rows[i];
                root.AddChild(new TreeViewItem(i + 1, 0, item ? item.id + " " + item.configName : "(Missing reference)"));
            }
        }

        public ScriptableObjectData GetData(int rowId) => rowId > 0 && rowId <= _rows.Length ? _rows[rowId - 1] : null;

        public void RefreshKeepingSelection()
        {
            var selected = GetSelection().Select(GetData).Where(item => item).ToArray();
            Reload();
            RestoreSelection(selected);
        }

        private void RestoreSelection(IEnumerable<ScriptableObjectData> selected)
        {
            var remaining = new List<ScriptableObjectData>(selected);
            var ids = new List<int>();
            for (var i = 0; i < _rows.Length; i++)
            {
                var index = remaining.FindIndex(item => ReferenceEquals(item, _rows[i]));
                if (index < 0)
                {
                    continue;
                }

                ids.Add(i + 1);
                remaining.RemoveAt(index);
            }
            SetSelection(ids);
            Selected?.Invoke(ids.Count == 1 ? GetData(ids[0]) : null);
        }

        protected override void RefreshDatas() => RefreshKeepingSelection();

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            Selected?.Invoke(selectedIds.Count == 1 ? GetData(selectedIds[0]) : null);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var data = GetData(args.item.id);
            for (var i = 0; i < args.GetNumVisibleColumns(); i++)
            {
                var rect = args.GetCellRect(i);
                switch (args.GetColumn(i))
                {
                    case 0:
                        if (data && data.icon != null)
                        {
                            EditorGUI.LabelField(rect, EditorAssetTool.GetTextureGuiContent(data.icon.projectName, data.icon.iconName));
                        }

                        break;
                    case 1:
                        EditorGUI.LabelField(rect, data ? data.id.ToString() : "-");
                        break;
                    case 2:
                        _content.text = data ? data.configName : "(Missing reference)";
                        _content.tooltip = data ? data.description : "Use Unlink to remove this missing slot.";
                        EditorGUI.LabelField(rect, _content);
                        break;
                }
            }
        }

        protected override void ContextClicked() => ShowCreateMenu();

        protected override void ContextClickedItem(int id)
        {
            if (!GetSelection().Contains(id))
            {
                SetSelection(new[] { id });
            }

            var selected = GetSelection().OrderBy(value => value).Select(value => (row: value - 1, data: GetData(value))).ToArray();
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Unlink References (Undo)"), false, () => Run(() =>
            {
                foreach (var item in selected.Reverse())
                {
                    ScriptableObjectConfigsController.RemoveReference(_owner, item.row, item.data);
                }
            }));
            if (selected.All(item => item.data))
            {
                menu.AddItem(new GUIContent("Copy"), false, () =>
                {
                    var copies = new List<ScriptableObjectData>();
                    Run(() =>
                    {
                        foreach (var item in selected)
                        {
                            copies.Add(ScriptableObjectConfigsController.CopyData(_owner, item.data));
                        }
                    }, () => copies);
                });
                menu.AddItem(new GUIContent("Delete Owned Assets..."), false, () => Run(() =>
                {
                    if (selected.Any(item => !ScriptableObjectConfigsController.IsOwned(_owner, item.data)))
                    {
                        throw new InvalidOperationException("The selection contains external assets. Unlink those references instead.");
                    }

                    if (!EditorUtility.DisplayDialog("Delete DataAssets", "Delete selected owned assets? Standalone asset files cannot be restored by Undo. External references are never deleted.", "Delete", "Cancel"))
                    {
                        return;
                    }

                    foreach (var item in selected)
                    {
                        if (item.data)
                        {
                            ScriptableObjectConfigsController.DeleteData(_owner, item.data, true);
                        }
                    }
                }));
            }
            menu.AddSeparator(string.Empty);
            AddCreateItems(menu);
            menu.ShowAsContext();
        }

        public void ShowCreateMenu()
        {
            var menu = new GenericMenu();
            AddCreateItems(menu);
            menu.ShowAsContext();
        }

        private void AddCreateItems(GenericMenu menu)
        {
            try
            {
                foreach (var type in ScriptableObjectConfigsController.GetDataTypes(_owner))
                {
                    var selectedType = type;
                    menu.AddItem(new GUIContent("Create/" + type.FullName.Replace('.', '/')), false,
                        () =>
                        {
                            ScriptableObjectData created = null;
                            Run(() => created = ScriptableObjectConfigsController.CreateData(selectedType, _owner), () => new[] { created });
                        });
                }
            }
            catch (InvalidOperationException error)
            {
                menu.AddDisabledItem(new GUIContent(error.Message));
            }
        }

        private void SortChanged(MultiColumnHeader header) => Run(() => ScriptableObjectConfigsController.Sort(_owner,
            header.sortedColumnIndex == 1 ? DataAssetSort.Id : DataAssetSort.Name, header.IsSortedAscending(header.sortedColumnIndex)));

        private void Run(Action operation, Func<IEnumerable<ScriptableObjectData>> selectAfter = null)
        {
            var selected = GetSelection().Select(GetData).Where(item => item).ToArray();
            try
            {
                operation();
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is System.IO.IOException)
            {
                EditorUtility.DisplayDialog("DataAssets", error.Message, "Close");
            }
            Reload();
            RestoreSelection(selectAfter == null ? selected : selectAfter().Where(item => item));
            Changed?.Invoke();
        }

        protected override void OnDragEnd()
        {
            _owner.ClearCache();
            Changed?.Invoke();
        }
    }
}
