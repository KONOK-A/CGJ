using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Attributes;
using YLFramework.Buff;
using YLFramework.Editor.Utilities;


namespace YLFramework.Editor.Buff
{


    public class BuffListTree : ReorderableTreeView<BuffConfigData>
    {

        private BuffsConfig _buffsConfig;

        private bool _isContextClickItem;

        private Rect _iconRect;
        private Rect _nameRect;
        private Rect _idRect;


        private Dictionary<int, GUIContent> _nameContents = new Dictionary<int, GUIContent>();


        private GUIStyle _styleName;


        public BuffsConfig BuffsConfig => _buffsConfig;
        protected override int GetItemIndex(int id) => _buffsConfig.buffs.FindIndex(data => data && data.id == id);


#if UNITY_6000_2_OR_NEWER
        public BuffListTree(TreeViewState<int> state, BuffsConfig buffsConfig) : base(buffsConfig, () => buffsConfig.buffs, state)
#else
        public BuffListTree(TreeViewState state, BuffsConfig buffsConfig) : base(buffsConfig, () => buffsConfig.buffs, state)
#endif

        {
            this._buffsConfig = buffsConfig;
            showBorder = true;
            rowHeight = 25;
        }

#if UNITY_6000_2_OR_NEWER
        protected override void OnBuildRoot(TreeViewItem<int> root, IList<BuffConfigData> datas)
#else
        protected override void OnBuildRoot(TreeViewItem root, IList<BuffConfigData> datas)
#endif

        {

            for (int i = 0; i < datas.Count; i++)
            {
                BuffConfigData info = datas[i];
                if (info == null)
                {
                    continue;
                }

#if UNITY_6000_2_OR_NEWER
                TreeViewItem<int> child = new TreeViewItem<int>(info.id, 0, string.Format("{0}-{1}", info.buffName, info.id));
#else
                TreeViewItem child = new TreeViewItem(info.id, 0, string.Format("{0}-{1}", info.buffName, info.id));
#endif

                root.AddChild(child);
            }

        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (_buffsConfig == null)
            {
                return;
            }

            BuffConfigData info = _buffsConfig.GetBuffData(args.item.id);

            if (info == null)
            {
                return;
            }

            _iconRect.Set(0, args.rowRect.y, args.rowRect.height, args.rowRect.height);
            _nameRect.Set(args.rowRect.height, args.rowRect.y, args.rowRect.width - args.rowRect.height - 70, args.rowRect.height);

            _idRect.Set(args.rowRect.width - 70, args.rowRect.y, 70, args.rowRect.height);

            GUIContent iconContent = EditorAssetTool.GetTextureGuiContent(info.icon.projectName, info.icon.iconName);

            EditorGUI.LabelField(_iconRect, iconContent);

            if (!_nameContents.ContainsKey(info.id))
            {
                _nameContents.Add(info.id, new GUIContent());
            }

            if (string.IsNullOrEmpty(info.description))
            {
                _nameContents[info.id].text = info.buffName;
                _nameContents[info.id].tooltip = string.Empty;
            }
            else
            {
                string des = info.description.Replace('\n', '\0');
                _nameContents[info.id].text = string.Format("{0}(<color=grey>{1}</color>)", info.buffName, des);
                _nameContents[info.id].tooltip = info.description;
            }

            if (_styleName == null)
            {

                _styleName = GUI.skin.label;
                _styleName.richText = true;
            }

            EditorGUI.LabelField(_nameRect, _nameContents[info.id], _styleName);

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
            CreateBuff(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            _isContextClickItem = true;

            if (_buffsConfig == null)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                foreach (var item in state.selectedIDs)
                {
                    DeleteBuff(item);
                }

                // 保存
                EditorUtility.SetDirty(_buffsConfig);
                AssetDatabase.SaveAssets();
                Reload();
            });

            menu.AddItem(new GUIContent("复制"), false, () =>
            {
                List<int> temp = new List<int>();

                foreach (var item in state.selectedIDs)
                {
                    BuffConfigData data = _buffsConfig.GetBuffData(item);
                    int copyId = CopyBuff(data);
                    if (copyId != -1)
                    {
                        temp.Add(copyId);
                    }
                }
                SetSelection(temp);
                Reload();
                if (temp.Count > 0)
                {
                    FrameItem(temp[temp.Count - 1]); // 定位
                }
            });

            menu.AddSeparator(string.Empty);
            CreateBuff(menu);
        }

        private void CreateBuff(GenericMenu menu)
        {
            //  查找项目中所有继承自
            List<Type> buffs = GetAllBuffTypes(_buffsConfig.GetDataType());

            foreach (var item in buffs)
            {
                string name = string.Format("创建Buff/{0}", item.Name);
                if (!string.IsNullOrEmpty(item.Namespace))
                {
                    name = string.Format("{0}-{1}", name, item.Namespace);
                }

                if (item == typeof(BuffConfigData))
                {
                    name = "创建Buff/基础配置";
                }

                GUIContent content = new GUIContent(name);

                menu.AddItem(content, false, () =>
                {
                    int id = BuffsConfigController.CreateBuffConfigData(item, _buffsConfig);
                    Reload();
                    if (id != -1)
                    {
                        SetSelection(new int[] { id });
                        FrameItem(id);
                    }
                });
            }


            menu.ShowAsContext();
        }

        private void DeleteBuff(int id)
        {
            if (_buffsConfig == null)
            {
                return;
            }

            BuffConfigData data = _buffsConfig.GetBuffData(id);
            if (data == null)
            {
                return;
            }

            YLFramework.Editor.Utilities.DomainAssetOwnership.DeleteEntry(_buffsConfig, _buffsConfig.buffs, data, BuffsConfigController.GetBuffConfigDataPath(_buffsConfig));
            _buffsConfig.ClearCache();
            Reload();

        }

        private int CopyBuff(BuffConfigData buff)
        {
            if (buff == null)
            {
                return -1;
            }

            BuffConfigData data = GameObject.Instantiate(buff);

            BuffsConfigController.CreateBuffConfigData(data, _buffsConfig);

            data.buffName = GetBuffName(buff.buffName);

            EditorUtility.SetDirty(data);

            return data.id;
        }

        private string GetBuffName(string buffName)
        {

            int count = 1;

            if (Regex.IsMatch(buffName, "\\([0-9]+\\)$"))
            {
                for (int i = buffName.Length - 1; i >= 0; i--)
                {
                    if (buffName[i] == '(')
                    {
                        string c = buffName.Substring(i + 1, buffName.Length - i - 2);
                        count = int.Parse(c);

                        buffName = buffName.Substring(0, i);
                        break;
                    }
                }
            }

            buffName = buffName.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", buffName, count);
                count++;
            } while (IsContainsBuffName(name));

            return name;
        }

        private bool IsContainsBuffName(string buffName)
        {

            foreach (var item in _buffsConfig.buffs)
            {
                if (item.buffName == buffName)
                {
                    return true;
                }
            }

            return false;
        }

        public static List<Type> GetAllBuffTypes(Type parent)
        {
            List<Type> states = new List<Type>();

            states.Add(parent);

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var item in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(item);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null)
                {
                    continue;
                }

                Type type = script.GetClass();

                if (type == null)
                {
                    continue;
                }

                if (type.IsSubclassOf(parent))
                {
                    states.Add(type);
                }
            }

            return states;
        }

        protected override void RefreshDatas()
        {
            base.RefreshDatas();

            if (_buffsConfig)
            {
                _buffsConfig.ClearCache();
            }
        }

    }
}
