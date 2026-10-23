using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace YLFramework.BuffSystem
{
#if UNITY_6000_2_OR_NEWER
    public class SelectEffectDataTreeViewItem : TreeViewItem<int>
#else
    public class SelectEffectDataTreeViewItem : TreeViewItem
#endif

    {


        public EffectData EffectData;


        public SelectEffectDataTreeViewItem(EffectData effectData) : base(effectData.id, 0, effectData.effectName)
        {
            this.EffectData = effectData;
        }


        public bool DoesItemMatchSearch(string search)
        {

            if (EffectData.id.ToString().Contains(search))
            {
                return true;
            }

            if (EffectData.effectName.Contains(search))
            {
                return true;
            }

            if (EffectData.description.Contains(search))
            {
                return true;
            }

            return false;

        }

    }

#if UNITY_6000_2_OR_NEWER
    public class SelectEffectDataListTree : TreeView<int>
#else
    public class SelectEffectDataListTree : TreeView
#endif

    {

        // Fix编码
        #region 字段

        private SelectEffectPopWindow _editorWindow;

        private SerializedProperty _property;

        private Dictionary<int, GUIContent> _contents = new Dictionary<int, GUIContent>();

        private List<int> _selections = new List<int>();

        #endregion


        #region 重写方法


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
                new MultiColumnHeaderState.Column()
            };
            retVal[0].headerContent = new GUIContent("ID", "ID");
            retVal[0].minWidth = 50;
            retVal[0].width = 60;
            retVal[0].maxWidth = 300;
            retVal[0].headerTextAlignment = TextAlignment.Left;
            retVal[0].canSort = true;
            retVal[0].autoResize = true;


            retVal[1].headerContent = new GUIContent("名称", "名称");
            retVal[1].minWidth = 30;
            retVal[1].width = 100;
            retVal[1].maxWidth = 300;
            retVal[1].headerTextAlignment = TextAlignment.Left;
            retVal[1].canSort = true;
            retVal[1].autoResize = true;


            retVal[2].headerContent = new GUIContent("类型", "类型,可根据类型查询效果!");
            retVal[2].minWidth = 30;
            retVal[2].width = 100;
            retVal[2].maxWidth = 300;
            retVal[2].headerTextAlignment = TextAlignment.Left;
            retVal[2].canSort = true;
            retVal[2].autoResize = true;


            retVal[3].headerContent = new GUIContent("效果分类", "效果分类!");
            retVal[3].minWidth = 30;
            retVal[3].width = 150;
            retVal[3].maxWidth = 1000;
            retVal[3].headerTextAlignment = TextAlignment.Left;
            retVal[3].canSort = false;
            retVal[3].autoResize = true;

            retVal[4].headerContent = new GUIContent("描述", "描述!");
            retVal[4].minWidth = 30;
            retVal[4].width = 100;
            retVal[4].maxWidth = 1000;
            retVal[4].headerTextAlignment = TextAlignment.Left;
            retVal[4].canSort = false;
            retVal[4].autoResize = true;



            return retVal;
        }

#if UNITY_6000_2_OR_NEWER
        protected override bool DoesItemMatchSearch(TreeViewItem<int> item, string search)
#else
        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
#endif

        {
            SelectEffectDataTreeViewItem effect = item as SelectEffectDataTreeViewItem;
            if (effect == null)
            {
                return false;
            }

            return effect.DoesItemMatchSearch(search);
        }

#if UNITY_6000_2_OR_NEWER
        protected override TreeViewItem<int> BuildRoot()
#else
        protected override TreeViewItem BuildRoot()
#endif

        {

#if UNITY_6000_2_OR_NEWER
            TreeViewItem<int> root = new TreeViewItem<int>(-1, -1);
#else
            TreeViewItem root = new TreeViewItem(-1, -1);
#endif

            List<EffectData> effects = BuffSystemSettings.Instance.AllEffectDatas;

            foreach (var item in effects)
            {
                if (item == null)
                {
                    continue;
                }

                root.AddChild(new SelectEffectDataTreeViewItem(item));
            }

            return root;
        }

#if UNITY_6000_2_OR_NEWER
        protected override IList<TreeViewItem<int>> BuildRows(TreeViewItem<int> root)
#else
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
#endif

        {
            return base.BuildRows(root);
        }

        protected override void SingleClickedItem(int id)
        {
            base.SingleClickedItem(id);

            SelectEffectDataTreeViewItem item = FindItem(id, rootItem) as SelectEffectDataTreeViewItem;

            _property.objectReferenceValue = item.EffectData;
            _property.serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            _editorWindow.Close();
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            //base.RowGUI(args);

            SelectEffectDataTreeViewItem item = args.item as SelectEffectDataTreeViewItem;
            if (item == null)
            {
                return;
            }

            if (args.rowRect.Contains(Event.current.mousePosition))
            {
                _selections.Clear();
                _selections.Add(item.id);
                this.SetSelection(_selections);
            }

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGui(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        private void CellGui(Rect cellRect, SelectEffectDataTreeViewItem item, int column, ref RowGUIArgs args)
        {
            if (!_contents.ContainsKey(item.id))
            {
                _contents.Add(item.id, new GUIContent());
            }

            GUIContent content = _contents[item.id];


            switch (column)
            {
                case 0:

                    content.text = item.EffectData.id.ToString();
                    content.tooltip = content.text;

                    GUI.Label(cellRect, content);
                    break;

                case 1:

                    content.text = item.EffectData.effectName;
                    content.tooltip = content.text;

                    GUI.Label(cellRect, content);
                    break;
                case 2:

                    content.text = BuffSystemSettings.Instance.GetType(item.EffectData.type);
                    content.tooltip = content.text;

                    GUI.Label(cellRect, content);

                    break;

                case 3:

                    content.text = GetEffectData(item.EffectData);
                    content.tooltip = content.text;

                    GUI.Label(cellRect, content);

                    break;

                case 4:

                    content.text = item.EffectData.description.Replace('\n', '\0');
                    content.tooltip = content.text;

                    GUI.Label(cellRect, content);

                    break;

            }

        }


        private string GetEffectData(EffectData effect)
        {

            if (effect.GetType().GetCustomAttribute<EffectName>() != null)
            {
                return effect.GetType().GetCustomAttribute<EffectName>().Name;
            }

            return effect.GetType().FullName;
        }


        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
        }
#if UNITY_6000_2_OR_NEWER
        protected override bool CanMultiSelect(TreeViewItem<int> item)
#else
        protected override bool CanMultiSelect(TreeViewItem item)
#endif

        {
            return false;
        }

        #endregion

        #region 方法

#if UNITY_6000_2_OR_NEWER
        public SelectEffectDataListTree(TreeViewState<int> state, SerializedProperty property, SelectEffectPopWindow editorWindow, MultiColumnHeaderState multiColumnHeaderState) : base(state, new MultiColumnHeader(multiColumnHeaderState))
#else
        public SelectEffectDataListTree(TreeViewState state, SerializedProperty property, SelectEffectPopWindow editorWindow, MultiColumnHeaderState multiColumnHeaderState) : base(state, new MultiColumnHeader(multiColumnHeaderState))
#endif

        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;

            this._property = property;
            this._editorWindow = editorWindow;
        }


        #endregion
    }


    public class SelectEffectPopWindow : PopupWindowContent
    {


        private SerializedProperty _property;

        private SelectEffectDataListTree _listTree;
#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> _state;
#else
        private TreeViewState _state;
#endif

        private MultiColumnHeaderState _effectListMCHState;

        private string _searchStr;

        public SelectEffectPopWindow(SerializedProperty property)
        {
            this._property = property;
        }

        public override void OnGUI(Rect rect)
        {
            if (_state == null)
            {
#if UNITY_6000_2_OR_NEWER
                _state = new TreeViewState<int>();
#else
                _state = new TreeViewState();
#endif

            }


            if (_listTree == null)
            {

                var headerState = SelectEffectDataListTree.CreateDefaultMultiColumnHeaderState();// multiColumnTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(_effectListMCHState, headerState))
                {
                    MultiColumnHeaderState.OverwriteSerializedFields(_effectListMCHState, headerState);
                }

                _effectListMCHState = headerState;

                _listTree = new SelectEffectDataListTree(_state, _property, this, _effectListMCHState);
                _listTree.Reload();
            }

            float h = rect.height;

            rect.height = 20;
            rect.y += 3;
            rect.x += 3;
            rect.width -= 6;
            _searchStr = EditorGUI.TextField(rect, string.Empty, _searchStr, "SearchTextField");

            rect.y += rect.height - 2;
            rect.height = h - 23;

            _listTree.OnGUI(rect);
            _listTree.searchString = _searchStr;
        }
        public void Close()
        {

            EditorApplication.update -= Update;

            if (editorWindow == null)
            {
                return;
            }

            editorWindow.Close();
        }


        public override void OnOpen()
        {
            base.OnOpen();
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        public override void OnClose()
        {
            base.OnClose();
            EditorApplication.update -= Update;
        }

        public void Update()
        {

            editorWindow.Repaint();

            if (EditorWindow.focusedWindow != editorWindow)
            {
                EditorApplication.update -= Update;
            }
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(500, 200);
        }


    }

}

#endif
