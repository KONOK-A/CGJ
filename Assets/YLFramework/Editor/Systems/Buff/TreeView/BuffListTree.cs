// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace YLFramework.BuffSystem
{


    public class BuffListTree : ReorderableTreeView<BuffConfigData>
    {

        private BuffsConfig buffsConfig;

        private bool isContextClickItem;

        private Rect icon_rect;
        private Rect name_rect;
        private Rect id_rect;


        private Dictionary<int,GUIContent> name_contents = new Dictionary<int, GUIContent>();


        private GUIStyle style_name;


        public BuffsConfig BuffsConfig => buffsConfig;
        protected override int GetItemIndex(int id) => buffsConfig.buffs.FindIndex(data => data && data.buff_id == id);


#if UNITY_6000_2_OR_NEWER
        public BuffListTree(TreeViewState<int> state, BuffsConfig buffsConfig) : base(buffsConfig, () => buffsConfig.buffs, state)
#else
        public BuffListTree(TreeViewState state, BuffsConfig buffsConfig) : base(buffsConfig, () => buffsConfig.buffs, state)
#endif

        {
            this.buffsConfig = buffsConfig;
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
                if (info == null) continue;

#if UNITY_6000_2_OR_NEWER
                TreeViewItem<int> child = new TreeViewItem<int>(info.buff_id, 0, string.Format("{0}-{1}", info.buff_name, info.buff_id));
#else
                TreeViewItem child = new TreeViewItem(info.buff_id, 0, string.Format("{0}-{1}", info.buff_name, info.buff_id));
#endif

                root.AddChild(child);
            }

        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (buffsConfig == null) return;

            BuffConfigData info = buffsConfig.GetBuffData(args.item.id);

            if (info == null) return;

            icon_rect.Set(0, args.rowRect.y, args.rowRect.height, args.rowRect.height);
            name_rect.Set(args.rowRect.height, args.rowRect.y, args.rowRect.width - args.rowRect.height - 70, args.rowRect.height);

            id_rect.Set(args.rowRect.width - 70, args.rowRect.y, 70, args.rowRect.height);

            GUIContent icon_content = EditorAssetTool.GetTextureGUIContent(info.icon.projectName,info.icon.iconName);

            EditorGUI.LabelField(icon_rect, icon_content);

            if (!name_contents.ContainsKey(info.buff_id))
            {
                name_contents.Add(info.buff_id, new GUIContent());
            }

            if (string.IsNullOrEmpty(info.description))
            {
                name_contents[info.buff_id].text = info.buff_name;
                name_contents[info.buff_id].tooltip = string.Empty;
            }
            else
            {
                string des = info.description.Replace('\n', '\0') ;
                name_contents[info.buff_id].text = string.Format("{0}(<color=grey>{1}</color>)", info.buff_name, des);
                name_contents[info.buff_id].tooltip = info.description;
            }

            if (style_name == null) {

                style_name = GUI.skin.label;
                style_name.richText = true;
            }

            EditorGUI.LabelField(name_rect, name_contents[info.buff_id], style_name);

            GUI.Label(id_rect, string.Format("id:{0}", info.buff_id), "AnimationTimelineTick");

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
            CreateBuff(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            isContextClickItem = true;

            if (buffsConfig == null) return;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                foreach (var item in state.selectedIDs)
                {
                    DeleteBuff(item);
                }

                // 保存
                EditorUtility.SetDirty(buffsConfig);
                AssetDatabase.SaveAssets();
                Reload();
            });

            menu.AddItem(new GUIContent("复制"), false, () =>
            {
                List<int> temp = new List<int>();

                foreach (var item in state.selectedIDs)
                {
                    BuffConfigData data = buffsConfig.GetBuffData(item);
                    int copy_id = CopyBuff(data);
                    if(copy_id != -1)
                        temp.Add(copy_id);
                }
                SetSelection(temp);
                Reload();
                if (temp.Count > 0)
                    FrameItem(temp[temp.Count - 1]); // 定位
            });

            menu.AddSeparator(string.Empty);
            CreateBuff(menu);
        }

        private void CreateBuff(GenericMenu menu)
        {
            //  查找项目中所有继承自
            List<Type> buffs = GetAllBuffTypes(buffsConfig.GetDataType());

            foreach (var item in buffs)
            {
                string name = string.Format("创建Buff/{0}", item.Name);
                if (!string.IsNullOrEmpty(item.Namespace))
                    name = string.Format("{0}-{1}", name, item.Namespace);

                if (item == typeof(BuffConfigData))
                    name = "创建Buff/基础配置";

                GUIContent content = new GUIContent(name);

                menu.AddItem(content, false, () =>
                {
                    int id = BuffsConfigController.CreateBuffConfigData(item, buffsConfig);
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
            if (buffsConfig == null)
                return;

            BuffConfigData data = buffsConfig.GetBuffData(id);
            if (data == null) return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(data));
            buffsConfig.buffs.Remove(data);
            buffsConfig.ClearCache();

        }

        private int CopyBuff(BuffConfigData buff)
        {
            if (buff == null)
                return -1;

            BuffConfigData data = GameObject.Instantiate(buff);

            BuffsConfigController.CreateBuffConfigData(data, buffsConfig);

            data.buff_name = GetBuffName(buff.buff_name);

            EditorUtility.SetDirty(data);

            return data.buff_id;
        }

        private string GetBuffName(string buff_name)
        {

            int count = 1;

            if (Regex.IsMatch(buff_name, "\\([0-9]+\\)$"))
            {
                for (int i = buff_name.Length - 1; i >= 0; i--)
                {
                    if (buff_name[i] == '(')
                    {
                        string c = buff_name.Substring(i + 1, buff_name.Length - i - 2);
                        count = int.Parse(c);

                        buff_name = buff_name.Substring(0, i);
                        break;
                    }
                }
            }

            buff_name = buff_name.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", buff_name,count);
                count++;
            } while (IsContainsBuffName(name));

            return name;
        }

        private bool IsContainsBuffName(string buff_name)
        {

            foreach (var item in buffsConfig.buffs)
            {
                if (item.buff_name == buff_name)
                    return true;
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

                if (script == null) continue;

                Type type = script.GetClass();

                if (type == null) continue;

                if (type.IsSubclassOf(parent))
                    states.Add(type);
            }

            return states;
        }

        protected override void RefreshDatas()
        {
            base.RefreshDatas();

            if(buffsConfig)
                buffsConfig.ClearCache();
        }

    }
}
