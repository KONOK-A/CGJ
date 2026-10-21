using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace YLFramework.SkillSystem
{

    internal class IconGUIContent
    {
        public string projectName;
        public string iconName;
        public GUIContent content;
    }

    public class SkillListTree : ReorderableTreeView<SkillConfigData>
    {

        private SkillsConfig skillsConfig;

        private bool isContextClickItem;

        private Rect icon_rect;
        private Rect name_rect;
        private Rect id_rect;

        private Dictionary<int, IconGUIContent> contents = new Dictionary<int, IconGUIContent>();

        private GUIStyle label_style;

        private Dictionary<int, GUIContent> displayNameContents = new Dictionary<int, GUIContent>();


        public SkillsConfig SkillsConfig => skillsConfig;

#if UNITY_6000_2_OR_NEWER
        public SkillListTree(TreeViewState<int> state, SkillsConfig skillsConfig) : base(skillsConfig, () => skillsConfig.skills, state)
#else
        public SkillListTree(TreeViewState state, SkillsConfig skillsConfig) : base(skillsConfig, () => skillsConfig.skills, state)
#endif

        {
            this.skillsConfig = skillsConfig;
            showBorder = true;
            rowHeight = 25;
        }

#if UNITY_6000_2_OR_NEWER
        protected override void OnBuildRoot(TreeViewItem<int> root, IList<SkillConfigData> datas)
#else
        protected override void OnBuildRoot(TreeViewItem root, IList<SkillConfigData> datas)
#endif

        {

            foreach (var item in datas)
            {
                if (item == null) continue;
#if UNITY_6000_2_OR_NEWER
                TreeViewItem<int> child = new TreeViewItem<int>(item.id, 0, string.Format("{0}-{1}", item.skill_name, item.id));
#else
                TreeViewItem child = new TreeViewItem(item.id, 0, string.Format("{0}-{1}", item.skill_name, item.id));
#endif
                root.AddChild(child);
            }
        }


        protected override void RowGUI(RowGUIArgs args)
        {
            if (skillsConfig == null) return;

            SkillConfigData info = skillsConfig.GetSkillConfigData(args.item.id);

            if (info == null) return;

            icon_rect.Set(0, args.rowRect.y, args.rowRect.height, args.rowRect.height);
            name_rect.Set(args.rowRect.height, args.rowRect.y, args.rowRect.width - args.rowRect.height - 70, args.rowRect.height);

            id_rect.Set(args.rowRect.width - 70, args.rowRect.y, 70, args.rowRect.height);

            EditorGUI.LabelField(icon_rect, EditorAssetTool.GetTextureGUIContent(info.icon.projectName,info.icon.iconName));

            if (!displayNameContents.ContainsKey(info.id))
                displayNameContents.Add(info.id, new GUIContent());

            displayNameContents[info.id].text = GetDisplayName(info);
            displayNameContents[info.id].tooltip = info.description;

            if (label_style == null)
            {
                label_style = new GUIStyle(EditorStyles.label);
                label_style.richText = true;
            }

            EditorGUI.LabelField(name_rect, displayNameContents[info.id], label_style);

            GUI.Label(id_rect, string.Format("id:{0}", info.id), "AnimationTimelineTick");

            GUI.Box(args.rowRect, string.Empty, "CN Box");
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
            CreateSkill(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            isContextClickItem = true;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                IList<int> selects = GetSelection();

                foreach (var item in selects)
                {
                    DeleteSkill(item);
                }

                AssetDatabase.SaveAssets();
                Reload();
            });

            menu.AddItem(new GUIContent("复制"), false, () =>
            {
                IList<int> selects = GetSelection();

                List<int> ids = new List<int>();

                foreach (var item in selects)
                {
                    int id = CopySkill(item);
                    if (id == -1) continue;
                    ids.Add(id);
                }

                AssetDatabase.SaveAssets();
                Reload();

                SetSelection(ids);
            });

            menu.AddSeparator(string.Empty);
            CreateSkill(menu);
        }

        private void CreateSkill(GenericMenu menu)
        {
            Type parent = skillsConfig.GetDataType();

            //Debug.Log(parent.FullName);

            //  查找项目中所有继承自
            List<Type> skills = ScriptBindController.GetAllSkillConfigDataTypes(parent);

            foreach (var item in skills)
            {
                string name = string.Format("创建技能/{0}", item.Name);
                if (!string.IsNullOrEmpty(item.Namespace))
                    name = string.Format("{0}-{1}", name, item.Namespace);

                if (item == typeof(SkillConfigData))
                    name = "创建技能/基础配置";

                GUIContent content = new GUIContent(name);

                menu.AddItem(content, false, () =>
                {
                    int id = SkillsConfigController.CreateSkillConfigData(item, skillsConfig);
                    SetSelection(new int[] { id });
                });
            }


            menu.ShowAsContext();
        }

        private void DeleteSkill(int id)
        {
            if (skillsConfig == null) return;

            SkillConfigData data = skillsConfig.GetSkillConfigData(id);
            if (data == null) return;

            YLFramework.Editor.Domain.DomainAssetOwnership.DeleteEntry(skillsConfig, skillsConfig.skills, data, SkillsConfigController.GetSkillConfigDataPath(skillsConfig));
            skillsConfig.ClearCache();
            Reload();
        }

        private int CopySkill(int id)
        {
            if (skillsConfig == null) return -1;

            SkillConfigData data = skillsConfig.GetSkillConfigData(id);
            if (data == null) return -1;

            SkillConfigData skillConfigData = GameObject.Instantiate(data);
            int new_id = SkillsConfigController.CreateSkillConfigData(skillConfigData, skillsConfig);
            skillConfigData.skill_name = SkillsConfigController.GetCopyName(data.skill_name,skillsConfig);
            return new_id;
        }


        protected override void RefreshDatas()
        {
            base.RefreshDatas();
            if(skillsConfig != null)
                skillsConfig.ClearCache();
        }

        protected override int GetItemIndex(int id) => skillsConfig.skills.FindIndex(data => data && data.id == id);

        private string GetDisplayName(SkillConfigData skillConfigData)
        {
            if (string.IsNullOrEmpty(skillConfigData.description))
                return skillConfigData.skill_name;

            return string.Format("{0}(<color=grey>{1}</color>)",skillConfigData.skill_name,skillConfigData.description.Replace("\n",string.Empty));
        }

    }
}
