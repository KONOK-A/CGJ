using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Attributes;
using YLFramework.Editor.Utilities;
using YLFramework.Skill;


namespace YLFramework.Editor.Skill
{

    internal class IconGuiContent
    {
        public string ProjectName;
        public string IconName;
        public GUIContent Content;
    }

    public class SkillListTree : ReorderableTreeView<SkillConfigData>
    {

        private SkillsConfig _skillsConfig;

        private bool _isContextClickItem;

        private Rect _iconRect;
        private Rect _nameRect;
        private Rect _idRect;

        private Dictionary<int, IconGuiContent> _contents = new Dictionary<int, IconGuiContent>();

        private GUIStyle _labelStyle;

        private Dictionary<int, GUIContent> _displayNameContents = new Dictionary<int, GUIContent>();


        public SkillsConfig SkillsConfig => _skillsConfig;

#if UNITY_6000_2_OR_NEWER
        public SkillListTree(TreeViewState<int> state, SkillsConfig skillsConfig) : base(skillsConfig, () => skillsConfig.skills, state)
#else
        public SkillListTree(TreeViewState state, SkillsConfig skillsConfig) : base(skillsConfig, () => skillsConfig.skills, state)
#endif

        {
            this._skillsConfig = skillsConfig;
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
                if (item == null)
                {
                    continue;
                }
#if UNITY_6000_2_OR_NEWER
                TreeViewItem<int> child = new TreeViewItem<int>(item.id, 0, string.Format("{0}-{1}", item.skillName, item.id));
#else
                TreeViewItem child = new TreeViewItem(item.id, 0, string.Format("{0}-{1}", item.skillName, item.id));
#endif
                root.AddChild(child);
            }
        }


        protected override void RowGUI(RowGUIArgs args)
        {
            if (_skillsConfig == null)
            {
                return;
            }

            SkillConfigData info = _skillsConfig.GetSkillConfigData(args.item.id);

            if (info == null)
            {
                return;
            }

            _iconRect.Set(0, args.rowRect.y, args.rowRect.height, args.rowRect.height);
            _nameRect.Set(args.rowRect.height, args.rowRect.y, args.rowRect.width - args.rowRect.height - 70, args.rowRect.height);

            _idRect.Set(args.rowRect.width - 70, args.rowRect.y, 70, args.rowRect.height);

            EditorGUI.LabelField(_iconRect, EditorAssetTool.GetTextureGuiContent(info.icon.projectName, info.icon.iconName));

            if (!_displayNameContents.ContainsKey(info.id))
            {
                _displayNameContents.Add(info.id, new GUIContent());
            }

            _displayNameContents[info.id].text = GetDisplayName(info);
            _displayNameContents[info.id].tooltip = info.description;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label);
                _labelStyle.richText = true;
            }

            EditorGUI.LabelField(_nameRect, _displayNameContents[info.id], _labelStyle);

            GUI.Label(_idRect, string.Format("id:{0}", info.id), "AnimationTimelineTick");

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

            if (_isContextClickItem)
            {
                _isContextClickItem = false;
                return;
            }

            GenericMenu menu = new GenericMenu();
            CreateSkill(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            _isContextClickItem = true;

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
                    if (id == -1)
                    {
                        continue;
                    }

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
            Type parent = _skillsConfig.GetDataType();

            //Debug.Log(parent.FullName);

            //  查找项目中所有继承自
            List<Type> skills = ScriptBindController.GetAllSkillConfigDataTypes(parent);

            foreach (var item in skills)
            {
                string name = string.Format("创建技能/{0}", item.Name);
                if (!string.IsNullOrEmpty(item.Namespace))
                {
                    name = string.Format("{0}-{1}", name, item.Namespace);
                }

                if (item == typeof(SkillConfigData))
                {
                    name = "创建技能/基础配置";
                }

                GUIContent content = new GUIContent(name);

                menu.AddItem(content, false, () =>
                {
                    int id = SkillsConfigController.CreateSkillConfigData(item, _skillsConfig);
                    SetSelection(new int[] { id });
                });
            }


            menu.ShowAsContext();
        }

        private void DeleteSkill(int id)
        {
            if (_skillsConfig == null)
            {
                return;
            }

            SkillConfigData data = _skillsConfig.GetSkillConfigData(id);
            if (data == null)
            {
                return;
            }

            YLFramework.Editor.Utilities.DomainAssetOwnership.DeleteEntry(_skillsConfig, _skillsConfig.skills, data, SkillsConfigController.GetSkillConfigDataPath(_skillsConfig));
            _skillsConfig.ClearCache();
            Reload();
        }

        private int CopySkill(int id)
        {
            if (_skillsConfig == null)
            {
                return -1;
            }

            SkillConfigData data = _skillsConfig.GetSkillConfigData(id);
            if (data == null)
            {
                return -1;
            }

            SkillConfigData skillConfigData = GameObject.Instantiate(data);
            int newId = SkillsConfigController.CreateSkillConfigData(skillConfigData, _skillsConfig);
            skillConfigData.skillName = SkillsConfigController.GetCopyName(data.skillName, _skillsConfig);
            return newId;
        }


        protected override void RefreshDatas()
        {
            base.RefreshDatas();
            if (_skillsConfig != null)
            {
                _skillsConfig.ClearCache();
            }
        }

        protected override int GetItemIndex(int id) => _skillsConfig.skills.FindIndex(data => data && data.id == id);

        private string GetDisplayName(SkillConfigData skillConfigData)
        {
            if (string.IsNullOrEmpty(skillConfigData.description))
            {
                return skillConfigData.skillName;
            }

            return string.Format("{0}(<color=grey>{1}</color>)", skillConfigData.skillName, skillConfigData.description.Replace("\n", string.Empty));
        }

    }
}
