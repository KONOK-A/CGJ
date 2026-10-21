using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace YLFramework.BuffSystem
{
#if UNITY_6000_2_OR_NEWER
    public class EffectTreeViewItem : TreeViewItem<int>
#else
    public class EffectTreeViewItem : TreeViewItem
#endif

    {
        public EffectData effectData;

        public GroupEffectData groupEffectData;

        public EffectTreeViewItem(GroupEffectData groupEffectData, EffectData data, int depth) : base(data.id, depth, data.effect_name)
        {
            this.effectData = data;
            this.groupEffectData = groupEffectData;
        }
    }

#if UNITY_6000_2_OR_NEWER
    public class EffectListTree : TreeView<int>
#else
    public class EffectListTree : TreeView
#endif

    {

        private const string EFFECT_DRAGGED_DATA_KEY = "YLFramework.Buff.EffectList.DraggedItemId";


        private bool isContextClickItem;

        private GUIStyle style_label;

        private GUIStyle style_dropdown;

        private Dictionary<int, GUIContent> des_contents = new Dictionary<int, GUIContent>();

        private Dictionary<int, GUIContent> type_contents = new Dictionary<int, GUIContent>();

        private Dictionary<int, GUIContent> display_contents = new Dictionary<int, GUIContent>();

        private float lastRefreshTime;

#if UNITY_6000_2_OR_NEWER
        public EffectListTree(TreeViewState<int> state , MultiColumnHeaderState multiColumnHeaderState) : base(state, new MultiColumnHeader(multiColumnHeaderState))
#else
        public EffectListTree(TreeViewState state , MultiColumnHeaderState multiColumnHeaderState) : base(state, new MultiColumnHeader(multiColumnHeaderState))
#endif

        {
            showBorder = true;
            rowHeight = 25;
            showAlternatingRowBackgrounds = true;

            multiColumnHeader.sortingChanged += SortChanged;

        }

        private void SortChanged(MultiColumnHeader multiColumnHeader)
        {
            bool asc = false;

            if (multiColumnHeader.sortedColumnIndex >= 0 && multiColumnHeader.sortedColumnIndex < 3)
                asc = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);

            foreach (var groupEffectData in BuffSystemSettings.Instance.AllEffectDataWithType)
            {
                switch (multiColumnHeader.sortedColumnIndex)
                {
                    case 0:
                        // 根据名称排序
                        groupEffectData.effectDatas.Sort((x, y) =>
                        {
                            return !asc ? x.effect_name.CompareTo(y.effect_name) : y.effect_name.CompareTo(x.effect_name);
                        });
                        break;
                    case 1:
                        // 根据id排序
                        groupEffectData.effectDatas.Sort((x, y) =>
                        {
                            return !asc ? x.id.CompareTo(y.id) : y.id.CompareTo(x.id);
                        });
                        break;

                    case 2:
                        // 根据类型排序
                        groupEffectData.effectDatas.Sort((x, y) =>
                        {
                            return !asc ? x.type.CompareTo(y.type) : y.type.CompareTo(x.type);
                        });
                        break;
                }

            }

            BuffSystemSettings.Save();

            Reload();
        }
#if UNITY_6000_2_OR_NEWER
        protected override TreeViewItem<int> BuildRoot()
#else
        protected override TreeViewItem BuildRoot()
#endif

        {
            return CreateView();
        }


        internal static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            MultiColumnHeaderState state = new MultiColumnHeaderState(GetColumns());

            return state;
        }
        private static MultiColumnHeaderState.Column[] GetColumns()
        {
            var retVal = new MultiColumnHeaderState.Column[] {
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                new MultiColumnHeaderState.Column(),
                //new MultiColumnHeaderState.Column()
            };
            retVal[0].headerContent = new GUIContent("名称", "名称");
            retVal[0].minWidth = 50;
            retVal[0].width = 320;
            retVal[0].maxWidth = 1000;
            retVal[0].headerTextAlignment = TextAlignment.Left;
            retVal[0].canSort = true;
            retVal[0].autoResize = true;


            retVal[1].headerContent = new GUIContent("ID", "ID");
            retVal[1].minWidth = 30;
            retVal[1].width = 80;
            retVal[1].maxWidth = 1000;
            retVal[1].headerTextAlignment = TextAlignment.Left;
            retVal[1].canSort = true;
            retVal[1].autoResize = true;


            retVal[2].headerContent = new GUIContent("类型", "类型,可根据类型查询效果!");
            retVal[2].minWidth = 30;
            retVal[2].width = 100;
            retVal[2].maxWidth = 1000;
            retVal[2].headerTextAlignment = TextAlignment.Left;
            retVal[2].canSort = true;
            retVal[2].autoResize = true;

            retVal[3].headerContent = new GUIContent("描述", "描述!");
            retVal[3].minWidth = 30;
            retVal[3].width =  180;
            retVal[3].maxWidth = 1000;
            retVal[3].headerTextAlignment = TextAlignment.Left;
            retVal[3].canSort = false;
            retVal[3].autoResize = true;

            return retVal;
        }

#if UNITY_6000_2_OR_NEWER
        public TreeViewItem<int> CreateView()
#else
        public TreeViewItem CreateView()
#endif

        {

#if UNITY_6000_2_OR_NEWER

            TreeViewItem<int> root = new TreeViewItem<int>(0, -1);
#else
            TreeViewItem root = new TreeViewItem(0, -1);
#endif


            List<int> ids = new List<int>();

            foreach (GroupEffectData groupEffectData in BuffSystemSettings.Instance.AllEffectDataWithType)
            {
                if (groupEffectData == null || groupEffectData.IsEmpty())
                {
                    continue;
                }

                string groupName = groupEffectData.GroupName;
#if UNITY_6000_2_OR_NEWER
                TreeViewItem<int> child = new TreeViewItem<int>(groupName.GetHashCode(), 0, groupName);
#else
                TreeViewItem child = new TreeViewItem(groupName.GetHashCode(), 0, groupName);
#endif

                foreach (EffectData data in groupEffectData.effectDatas)
                {

                    if (data == null) continue;

                    // 过滤掉重复的数据
                    if (ids.Contains(data.id)) continue;
                    ids.Add(data.id);

                    EffectTreeViewItem d = new EffectTreeViewItem(groupEffectData,data, 1);
                    child.AddChild(d);
                }

                root.AddChild(child);
            }

            return root;
        }

        private int GenerateID()
        {
            int id = 0;


            for (int i = BuffSystemSettings.Instance.AllEffectDatas.Count - 1; i >= 0; i--)
            {
                EffectData item = BuffSystemSettings.Instance.AllEffectDatas[i];

                if (!item)
                {
                    BuffSystemSettings.Instance.AllEffectDatas.RemoveAt(i);
                    continue;
                }

                if (item.id > id) id = item.id;
            }


            return ++id;
        }

#if UNITY_6000_2_OR_NEWER
        protected override bool CanMultiSelect(TreeViewItem<int> item)
#else
        protected override bool CanMultiSelect(TreeViewItem item)
#endif

        {
            return true;
        }

        protected override void ContextClicked()
        {
            base.ContextClicked();

            if (isContextClickItem)
            {
                isContextClickItem = false;
                return;
            }

            GenericMenu menu = new GenericMenu();
            ShowCreateEffectMenu(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            isContextClickItem = true;

            EffectTreeViewItem item = FindItem(id, rootItem) as EffectTreeViewItem;

            GenericMenu menu = new GenericMenu();

            if (item != null)
            {
                menu.AddItem(new GUIContent("删除"), false, () =>
                {
                    foreach (var item in GetSelection())
                    {
                        EffectTreeViewItem tree = FindItem(item, rootItem) as EffectTreeViewItem;
                        if(tree == null) continue;
                        DeleteEffect(tree.effectData.id);
                    }

                    AssetDatabase.SaveAssets();
                    Reload();
                });

                menu.AddItem(new GUIContent("复制"), false, () =>
                {
                    List<int> ints = new List<int>();

                    foreach (var item in GetSelection())
                    {
                        EffectTreeViewItem tree = FindItem(item, rootItem) as EffectTreeViewItem;
                        if (tree == null) continue;
                        ints.Add( CopyEffect(tree.effectData));
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Reload();

                    SetSelection(ints);
                });

                menu.AddSeparator(string.Empty);
            }


            ShowCreateEffectMenu(menu);
        }

#if UNITY_6000_2_OR_NEWER
        protected override IList<TreeViewItem<int>> BuildRows(TreeViewItem<int> root)
#else
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
#endif

        {
            return base.BuildRows(root);
        }

        private int CreateEffect(Type type)
        {

            EffectData effect = ScriptableObject.CreateInstance(type) as EffectData;

            return CreateEffect(effect);
        }

        private int CreateEffect(EffectData effect,string pre_name = null )
        {
            string path = "Assets/YLFrameworkData/Buff/Effects";

            EffectsDataController.CreateAsset(effect, path, true, pre_name);

            effect.effect_name = effect.name;
            effect.id = GenerateID();
            EditorUtility.SetDirty(effect);
            return effect.id;
        }

        private int CopyEffect(EffectData effect)
        {
            EffectData data = GameObject.Instantiate(effect);
            return CreateEffect(data ,data.effect_name);
        }

        private void ShowCreateEffectMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("创建Effect/基础效果"), false, () =>
            {
                int id = CreateEffect(typeof(EffectData));

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Reload();
                SetSelection(new int[] { id });
                FrameItem(id);
            });

            //  查找项目中所有继承自
            List<Type> effects = GetAllBuffTypes();

            List<string> temps = new List<string>();

            if (effects.Count > 0)
            {
                menu.AddSeparator("创建Effect/");
            }

            foreach (var item in effects)
            {
                string name = null;

                EffectName effectName = item.GetCustomAttribute<EffectName>();

                if (effectName != null && !temps.Contains(effectName.Name))
                {
                    name = string.Format("创建Effect/{0}", effectName.Name);
                    temps.Add(effectName.Name);
                }
                else
                {
                    name = string.Format("创建Effect/{0}", item.Name);
                    if (!string.IsNullOrEmpty(item.Namespace))
                        name = string.Format("{0}-{1}", name, item.Namespace);
                }


                GUIContent content = new GUIContent(name);

                menu.AddItem(content, false, () =>
                {
                    int id = CreateEffect(item);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Reload();
                    SetSelection(new int[] { id });
                    FrameItem(id);
                });
            }


            menu.ShowAsContext();
        }

        private void DeleteEffect(int id)
        {
            EffectData data = BuffSystemSettings.Instance.GetEffectDataByID(id);
            if (data == null) return;
            // 移除
            BuffSystemSettings.Instance.RemoveEffectData(data);

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(data));

        }

        public static List<Type> GetAllBuffTypes()
        {
            List<Type> states = new List<Type>();

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var item in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(item);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null) continue;

                Type type = script.GetClass();

                if (type == null) continue;

                if (type.IsSubclassOf(typeof(EffectData)))
                    states.Add(type);
            }

            return states;
        }


        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                CellGUI(args.GetCellRect(i), args.item, args.GetColumn(i), ref args);
        }

#if UNITY_6000_2_OR_NEWER

        private void CellGUI(Rect cellRect, TreeViewItem<int> item, int column, ref RowGUIArgs args)
#else
        private void CellGUI(Rect cellRect, TreeViewItem item, int column, ref RowGUIArgs args)
#endif

        {

            if (style_label == null)
            {
                style_label = GUI.skin.label;
                style_label.richText = true;
            }

            EffectTreeViewItem effectItem = item as EffectTreeViewItem;

            if (effectItem != null)
            {
                switch (column)
                {
                    case 0:
                        cellRect.x += 30;
                        cellRect.width -= 30;
                        GUI.Label(cellRect, effectItem.effectData.effect_name, style_label);
                        break;

                    case 1:
                        GUI.Label(cellRect, effectItem.effectData.id.ToString(), style_label);
                        break;

                    case 2:

                        if (!type_contents.ContainsKey(effectItem.effectData.id))
                            type_contents.Add(effectItem.effectData.id, new GUIContent());

                        type_contents[effectItem.effectData.id].text = BuffSystemSettings.Instance.GetType(effectItem.effectData.type);
                        type_contents[effectItem.effectData.id].tooltip = BuffSystemSettings.Instance.GetType(effectItem.effectData.type, false);

                        if (style_dropdown == null)
                        {
                            style_dropdown = new GUIStyle("DropDown");
                            style_dropdown.richText = true;
                        }

                        if (EditorGUI.DropdownButton(cellRect, type_contents[effectItem.effectData.id], FocusType.Passive, style_dropdown))
                        {
                            GenericMenu menu = new GenericMenu();

                            menu.AddItem(new GUIContent("0(空)"), effectItem.effectData.type == 0, () => {
                                effectItem.effectData.type = 0;
                                EditorUtility.SetDirty(effectItem.effectData);
                                AssetDatabase.SaveAssets();

                                Reload();
                            });

                            if (BuffSystemSettings.Instance.AllEffectType.Count > 0)
                                menu.AddSeparator(string.Empty);

                            foreach (var type in BuffSystemSettings.Instance.AllEffectType)
                            {
                                GUIContent content = new GUIContent(BuffSystemSettings.Instance.GetType(type.type, false));

                                menu.AddItem(content, effectItem.effectData.type == type.type, () =>
                                {
                                    effectItem.effectData.type = type.type;
                                    EditorUtility.SetDirty(effectItem.effectData);
                                    AssetDatabase.SaveAssets();

                                    Reload();
                                });
                            }

                            if (BuffSystemSettings.Instance.AllEffectType.Count > 0)
                                menu.AddSeparator(string.Empty);

                            menu.AddItem(new GUIContent("编辑"), false, () =>
                            {
                                OpenProjectSettings("YLFramework/Buff");
                            });

                            menu.ShowAsContext();
                        }

                        break;

                    case 3:

                        if (!des_contents.ContainsKey(effectItem.effectData.id))
                            des_contents.Add(effectItem.effectData.id, new GUIContent());

                        if (string.IsNullOrEmpty(effectItem.effectData.description))
                            return;
                        des_contents[effectItem.effectData.id].text = effectItem.effectData.description.Replace('\n', '\0');
                        des_contents[effectItem.effectData.id].tooltip = effectItem.effectData.description;

                        GUI.Label(cellRect, des_contents[effectItem.effectData.id], style_label);
                        break;
                }
            }
            else
            {
                if (column == 0)
                {
                    cellRect.x += 20;
                    cellRect.width -= 20;

                    if (!display_contents.ContainsKey(args.item.GetHashCode()))
                        display_contents.Add(args.item.GetHashCode(), new GUIContent());

                    display_contents[args.item.GetHashCode()].text = args.item.displayName;
                    display_contents[args.item.GetHashCode()].tooltip = args.item.displayName;

                    GUI.Label(cellRect, display_contents[args.item.GetHashCode()], style_label);
                }
            }



        }

        internal static void OpenProjectSettings(string path)
        {
            SettingsService.OpenProjectSettings(path);
        }

        protected override void SingleClickedItem(int id)
        {
            base.SingleClickedItem(id);

            EffectTreeViewItem treeViewItem = FindItem(id, rootItem) as EffectTreeViewItem;

            if (treeViewItem == null) return;

            currentDragGroup = treeViewItem.groupEffectData;

            dragItemParent = treeViewItem.parent;

            // 先触发开始拖拽 再触发点击 在这里判断一下要拖拽的东西是否都在同一个节点
            // 如果有的节点 跟当前点击的节点不是同一个父节点的情况下 ， 就需要把他移除
            // 因为只能拖拽同一个节点下面的数据
            // 如果没拖拽的情况下这里的逻辑没有任何影响，因为 DragAndDrop.GetGenericData(EFFECT_DRAGGED_DATA_KEY) 这个数据
            // 只会在拖拽中和拖拽结束的时候用到
            IList<int> draggedItemIDs = DragAndDrop.GetGenericData(EFFECT_DRAGGED_DATA_KEY) as IList<int>;

            if (draggedItemIDs != null)
            {
                for (int i = draggedItemIDs.Count - 1; i >= 0; i--)
                {
#if UNITY_6000_2_OR_NEWER
                    TreeViewItem<int> item = FindItem(draggedItemIDs[i], rootItem);
#else
                    TreeViewItem item = FindItem(draggedItemIDs[i], rootItem);
#endif
                    if (item.parent != dragItemParent)
                    {
                        draggedItemIDs.RemoveAt(i);
                    }
                }
            }
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);
            RefreshList();
        }

        private void RefreshList()
        {
            if (Time.realtimeSinceStartup - lastRefreshTime < 1)
                return;
            lastRefreshTime = Time.realtimeSinceStartup;

            bool remove = false;

            for (int i = BuffSystemSettings.Instance.AllEffectDatas.Count - 1; i>=0; i--)
            {
                EffectData effectData = BuffSystemSettings.Instance.AllEffectDatas[i];
                if (effectData != null) continue;
                remove = true;
                BuffSystemSettings.Instance.AllEffectDatas.RemoveAt(i);
            }

            if (remove)
            {
                BuffSystemSettings.Instance.ClearCache();
                BuffSystemSettings.Save();
                Reload();
            }

        }


        public EffectData GetEffectData(int id)
        {
            EffectTreeViewItem item = FindItem(id, rootItem) as EffectTreeViewItem;
            if (item == null) return null;
            return item.effectData;
        }

        #region 拖拽功能实现

        private float startDragTime;

        private int dragCount;
#if UNITY_6000_2_OR_NEWER
        private TreeViewItem<int> dragItemParent; // 拖拽的parent
#else
        private TreeViewItem dragItemParent; // 拖拽的parent
#endif
        private GroupEffectData currentDragGroup;
        private sealed class EffectDrag
        {
            internal EffectListTree Owner;
            internal GroupEffectData Group;
            internal int[] Indices;
            internal EffectData[] Snapshot;
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            if (!(args.draggedItem is EffectTreeViewItem first) || !GetSelection().Contains(first.id)) return false;
            return args.draggedItemIDs != null && args.draggedItemIDs.All(id => FindItem(id, rootItem) is EffectTreeViewItem item && ReferenceEquals(item.groupEffectData, first.groupEffectData));
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            var drag = DragAndDrop.GetGenericData(EFFECT_DRAGGED_DATA_KEY) as EffectDrag;
            if (drag == null || !ReferenceEquals(drag.Owner, this) || args.parentItem != dragItemParent ||
                args.insertAtIndex < 0 || args.insertAtIndex > drag.Snapshot.Length ||
                !drag.Group.effectDatas.SequenceEqual(drag.Snapshot)) return DragAndDropVisualMode.Rejected;
            if (!args.performDrop) return DragAndDropVisualMode.Move;
            var list = new List<EffectData>(drag.Snapshot);
            var moved = drag.Indices.Select(index => list[index]).ToArray();
            for (var i = drag.Indices.Length - 1; i >= 0; i--) list.RemoveAt(drag.Indices[i]);
            list.InsertRange(args.insertAtIndex - drag.Indices.Count(index => index < args.insertAtIndex), moved);
            if (!list.SequenceEqual(drag.Snapshot))
            {
                Undo.RecordObject(BuffSystemSettings.Instance, "Reorder Buff effects");
                var all = BuffSystemSettings.Instance.AllEffectDatas;
                var next = 0;
                for (var i = 0; i < all.Count; i++) if (drag.Snapshot.Contains(all[i])) all[i] = list[next++];
                drag.Group.effectDatas.Clear();
                drag.Group.effectDatas.AddRange(list);
                BuffSystemSettings.Save();
                Reload();
            }
            Dispose();
            return DragAndDropVisualMode.Move;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            var items = args.draggedItemIDs.Select(id => FindItem(id, rootItem) as EffectTreeViewItem).ToArray();
            if (items.Length == 0 || items.Any(item => item == null)) return;
            currentDragGroup = items[0].groupEffectData;
            dragItemParent = items[0].parent;
            if (items.Any(item => !ReferenceEquals(item.groupEffectData, currentDragGroup))) return;
            var indices = items.Select(item => currentDragGroup.effectDatas.IndexOf(item.effectData)).Distinct().OrderBy(index => index).ToArray();
            if (indices.Any(index => index < 0)) return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(EFFECT_DRAGGED_DATA_KEY, new EffectDrag { Owner = this, Group = currentDragGroup, Indices = indices, Snapshot = currentDragGroup.effectDatas.ToArray() });
            DragAndDrop.StartDrag("Reorder Buff effects");
        }

        public void Dispose()
        {
            if (DragAndDrop.GetGenericData(EFFECT_DRAGGED_DATA_KEY) is EffectDrag drag && ReferenceEquals(drag.Owner, this)) DragAndDrop.SetGenericData(EFFECT_DRAGGED_DATA_KEY, null);
        }


        private int GetItemIndex(int id)
        {
#if UNITY_6000_2_OR_NEWER
            TreeViewItem<int> treeViewItem = FindItem(id, dragItemParent);
#else
            TreeViewItem treeViewItem = FindItem(id, dragItemParent);
#endif

            if (treeViewItem == null)
            {
                return -1;
            }

            return dragItemParent.children.IndexOf(treeViewItem);
        }

#endregion

    }

}
