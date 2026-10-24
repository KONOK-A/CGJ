using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Editor.Utilities;
using YLFramework.Equipment;
#if UNITY_6000_2_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#endif

namespace YLFramework.Editor.Equipment
{
    public sealed class EquipmentListTree : ReorderableTreeView<EquipmentConfigData>
    {
        public EquipmentsConfig EquipmentsConfig
        {
            get;
        }
        private readonly Dictionary<int, int> _indices = new();
        private readonly Dictionary<int, EquipmentConfigData> _targets = new();
        private int[] _rowIds = Array.Empty<int>();
        private int[] _businessIds = Array.Empty<int>();
        private string[] _names = Array.Empty<string>();
        private bool[] _alive = Array.Empty<bool>();
        public EquipmentListTree(TreeViewState state, EquipmentsConfig root) : base(root, () => root.Equipments, state)
        {
            EquipmentsConfig = root;
            rowHeight = 28;
            showBorder = true;
            Reload();
        }

        protected override void OnBuildRoot(TreeViewItem root, IList<EquipmentConfigData> datas)
        {
            _indices.Clear();
            _targets.Clear();
            _rowIds = new int[datas.Count];
            _businessIds = new int[datas.Count];
            _names = new string[datas.Count];
            _alive = new bool[datas.Count];
            var reserved = new HashSet<int>(datas.Where(data => data).Select(data => data.GetInstanceID()));
            var fallback = int.MinValue;
            for (var i = 0; i < datas.Count; i++)
            {
                var data = datas[i];
                var id = data ? data.GetInstanceID() : 0;
                if (id == 0 || _indices.ContainsKey(id))
                {
                    while (reserved.Contains(fallback) || _indices.ContainsKey(fallback))
                    {
                        fallback++;
                    }

                    id = fallback++;
                }
                _indices[id] = i;
                _targets[id] = data;
                _rowIds[i] = id;
                _alive[i] = data;
                _businessIds[i] = data ? data.id : 0;
                _names[i] = data ? data.equipmentName : null;
                root.AddChild(new TreeViewItem(id, 0, data ? data.id + "  " + data.equipmentName : "(Missing)"));
            }
        }
        public override void OnGUI(Rect rect)
        {
            var datas = EquipmentsConfig.Equipments;
            var changed = datas.Count != _rowIds.Length;
            for (var i = 0; !changed && i < datas.Count; i++)
            {
                changed = _alive[i] != (bool)datas[i] || _businessIds[i] != (datas[i] ? datas[i].id : 0) || _names[i] != (datas[i] ? datas[i].equipmentName : null);
            }

            if (changed)
            {
                Reload();
            }

            base.OnGUI(rect);
        }
        protected override int GetItemIndex(int id) => _indices.TryGetValue(id, out var index) ? index : -1;
        protected override void RowGUI(RowGUIArgs args)
        {
            var data = EquipmentsConfig.Equipments[GetItemIndex(args.item.id)];
            if (!data)
            {
                base.RowGUI(args);
                return;
            }
            var icon = data.icon == null ? GUIContent.none : EditorAssetTool.GetTextureGuiContent(data.icon.projectName, data.icon.iconName);
            GUI.Label(new Rect(args.rowRect.x, args.rowRect.y, 26, 26), icon);
            GUI.Label(new Rect(args.rowRect.x + 30, args.rowRect.y, args.rowRect.width - 30, args.rowRect.height), new GUIContent(args.item.displayName, data.description));
        }
        protected override void RefreshDatas()
        {
            EquipmentsConfig.ClearCache();
            base.RefreshDatas();
        }
        protected override void OnDragEnd() => EquipmentsConfig.ClearCache();

        protected override void ContextClicked() => ShowMenu(false);
        protected override void ContextClickedItem(int id) => ShowMenu(true);
        private void ShowMenu(bool selected)
        {
            var menu = new GenericMenu();
            if (selected)
            {
                var rows = GetSelection().Where(_targets.ContainsKey).Select(id => _targets[id]).Where(d => d).ToArray();
                menu.AddItem(new GUIContent("复制"), false, () =>
                {
                    foreach (var row in rows)
                    {
                        EquipmentsConfigController.CopyEquipmentConfigData(row, EquipmentsConfig);
                    }
                    Reload();
                });
                menu.AddItem(new GUIContent("删除"), false, () =>
                {
                    foreach (var row in rows)
                    {
                        EquipmentsConfigController.DeleteEquipmentConfigData(row, EquipmentsConfig);
                    }
                    Reload();
                });
                menu.AddSeparator("");
            }
            foreach (var type in GetAllEquipmentConfigTypes(EquipmentsConfig.GetDataType()))
            {
                var selectedType = type;
                menu.AddItem(new GUIContent("创建装备/" + selectedType.FullName), false, () =>
                {
                    var id = EquipmentsConfigController.CreateEquipmentConfigData(selectedType, EquipmentsConfig);
                    Reload();
                    SelectEquipment(id);
                });
            }
            menu.ShowAsContext();
        }
        public static List<Type> GetAllEquipmentConfigTypes(Type parent) => TypeCache.GetTypesDerivedFrom<EquipmentConfigData>().Append(parent)
            .Where(t => parent.IsAssignableFrom(t) && !t.IsAbstract && !t.ContainsGenericParameters).Distinct().OrderBy(t => t.FullName).ToList();
        public void SelectEquipment(int id)
        {
            var index = EquipmentsConfig.Equipments.FindIndex(d => d && d.id == id);
            SetSelection(index < 0 ? Array.Empty<int>() : new[] { _rowIds[index] });
        }
        public EquipmentConfigData Selected => GetSelection().Count != 0 && _targets.TryGetValue(GetSelection()[0], out var data) && data && EquipmentsConfig.Equipments.Any(item => ReferenceEquals(item, data)) ? data : null;
    }
}
