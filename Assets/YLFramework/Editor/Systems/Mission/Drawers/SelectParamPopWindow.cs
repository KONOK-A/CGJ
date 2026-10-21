using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace YLFramework.MissionSystem
{

    public class ParamPopupTreeView : TreeView
    {
        private MissionsConfig config;
        private SerializedProperty condition;
        private MissionConfigData missionConfigData;

        private GUIStyle label_style;

        private Dictionary<int, GUIContent> contents = new Dictionary<int, GUIContent>();

        public ParamPopupTreeView(TreeViewState state, MissionsConfig controller, SerializedProperty condition ) : base(state)
        {
            this.config = controller;
            this.condition = condition;

            showAlternatingRowBackgrounds = true;
        }

        protected override TreeViewItem BuildRoot()
        {

            TreeViewItem root = new TreeViewItem(-1, -1);

            if (config != null)
            {
                for (int i = 0; i < config.param.Count; i++)
                {
                    root.AddChild(new TreeViewItem(i, 0, string.Format("{0}{1}", config.param[i].name, config.param[i].des)));
                }
            }

            return root;
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            return base.BuildRows(root);
        }

        protected override void SingleClickedItem(int id)
        {
            base.SingleClickedItem(id);

            if (id < 0 || id >= config.param.Count)
                return;

            MissionParameter p = config.param[id];

            if (p != null)
            {
                SerializedProperty param_name = condition.FindPropertyRelative("param_name");
                param_name.stringValue = p.name;

                SerializedProperty compareType = condition.FindPropertyRelative("compareType");


                switch (p.type)
                {
                    case MissionParamType.Float:
                    case MissionParamType.Int:
                        //condition.compareType = CompareType.Greater;

                        for (int i = 0; i < compareType.enumNames.Length; i++)
                        {
                            if (compareType.enumNames[i] == CompareType.Greater.ToString())
                            {
                                compareType.enumValueIndex = i;
                                break;
                            }
                        }

                        break;
                    case MissionParamType.Bool:

                        for (int i = 0; i < compareType.enumNames.Length; i++) {
                            if (compareType.enumNames[i] == CompareType.Equals.ToString())
                            {
                                compareType.enumValueIndex = i;
                                break;
                            }
                        }


                        break;
                }

                // 保存
                condition.serializedObject.ApplyModifiedProperties();
            }

        }

        protected override void RowGUI(RowGUIArgs args)
        {


            if (args.item.id < 0 || args.item.id >= config.param.Count)
                return;
            SerializedProperty param_name = condition.FindPropertyRelative("param_name");

            MissionParameter parameter = config.param[args.item.id];

            if (parameter.name.Equals(param_name.stringValue))
                GUI.Label(args.rowRect, "√");

            args.rowRect.x += 15;
            args.rowRect.width -= 15;

            args.rowRect.width -= 35;

            if (label_style == null) {
                label_style = new GUIStyle(GUI.skin.label);
                label_style.richText = true;
            }

            if (!contents.ContainsKey(args.item.id))
                contents.Add(args.item.id, new GUIContent());

            if (string.IsNullOrEmpty(parameter.des))
            {
                contents[args.item.id].text = parameter.name;
            }
            else {
                contents[args.item.id].text = string.Format("{0}<color=grey>({1})</color>", parameter.name, parameter.des.Replace("\n", string.Empty));
            }

            contents[args.item.id].tooltip = parameter.des;

            EditorGUI.LabelField(args.rowRect, contents[args.item.id], label_style);

            args.rowRect.x += args.rowRect.width;

            Color c = GUI.color;

            GUI.color = Color.yellow;
            EditorGUI.LabelField(args.rowRect, parameter.type.ToString() );

            GUI.color = c;
        }

    }

    public class SelectParamPopWindow : PopupWindowContent
    {
        #region 字段

        private float width;

        private MissionsConfig controller;
        private SerializedProperty condition;
        private MissionConfigData missionConfigData;

        // 搜索框
        private SearchField searchField;
        private Rect searchRect;
        const float searchHeight = 25f;

        // 标签
        private Rect labelRect;
        const float labelHeight = 30f;

        // 参数列表
        private ParamPopupTreeView paramTree;
        private TreeViewState paramState;
        private Rect paramRect;

        #endregion


        public SelectParamPopWindow(float width , MissionsConfig controller, SerializedProperty condition)
        {
            this.width = width;
            this.controller = controller;
            this.condition = condition;
        }


        public override Vector2 GetWindowSize()
        {
            return new Vector2(this.width, 240);
        }

        public override void OnGUI(Rect rect)
        {

            if (paramTree == null)
            {
                if (paramState == null)
                {
                    paramState = new TreeViewState();
                }

                paramTree = new ParamPopupTreeView(paramState, controller,condition);
                paramTree.Reload();
            }

            // 搜索框
            if (searchField == null)
            {
                searchField = new SearchField();
            }
            searchRect.Set(rect.x + 5, rect.y + 5, rect.width - 10, searchHeight);
            paramTree.searchString = searchField.OnGUI(searchRect, paramTree.searchString);

            // 标签
            labelRect.Set(rect.x, rect.y + searchHeight, rect.width, labelHeight);

            SerializedProperty param_name = condition.FindPropertyRelative("param_name");

            EditorGUI.LabelField(labelRect, param_name.stringValue, GUI.skin.GetStyle("AC BoldHeader"));

            // 参数列表

            paramRect.Set(rect.x, rect.y + searchHeight + labelHeight - 5, rect.width, rect.height - searchHeight - labelHeight);
            paramTree.OnGUI(paramRect);

        }
    }
}


#endif
