// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace YLFramework.MissionSystem
{

    //internal class IconGUIContent
    //{
    //    public string projectName;
    //    public string iconName;
    //    public GUIContent content;
    //}

    public class MissionListTree : ReorderableTreeView<MissionConfigData>
    {
        private const string mission_copy_flag = "YLFramework.MissionSystem.CopyMission";

        private MissionsConfig missionsConfig;

        private bool isContextClickItem;

        private Rect icon_rect;
        private Rect name_rect;
        private Rect id_rect;

        //private Dictionary<int, IconGUIContent> contents = new Dictionary<int, IconGUIContent>();

        private Dictionary<int,GUIContent> name_contents = new Dictionary<int, GUIContent>();

        private GUIStyle name_style;

        public MissionsConfig MissionsConfig => missionsConfig;
        protected override int GetItemIndex(int id) => missionsConfig.missions.FindIndex(data => data && data.id == id);

#if UNITY_6000_2_OR_NEWER
        public MissionListTree(TreeViewState<int> state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.missions, state)
#else
        public MissionListTree(TreeViewState state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.missions, state)
#endif

        {
            this.missionsConfig = missionsConfig;
            showBorder = true;
            rowHeight = 25;
        }

#if UNITY_6000_2_OR_NEWER
        protected override void OnBuildRoot(TreeViewItem<int> root, IList<MissionConfigData> datas)
#else
        protected override void OnBuildRoot(TreeViewItem root, IList<MissionConfigData> datas)
#endif

        {
            for (int i = 0; i < datas.Count; i++)
            {
                MissionConfigData info = datas[i];
                if (info == null) continue;
#if UNITY_6000_2_OR_NEWER
                TreeViewItem<int> child = new TreeViewItem<int>(info.id, 0, string.Format("{0}{1}{2}", info.mission_name, info.id, info.description));
#else
                TreeViewItem child = new TreeViewItem(info.id, 0, string.Format("{0}{1}{2}", info.mission_name, info.id, info.description));
#endif

                root.AddChild(child);
            }
        }

        protected override void RefreshDatas()
        {
            base.RefreshDatas();

            if (missionsConfig != null)
                missionsConfig.ClearCache();
        }


        protected override void RowGUI(RowGUIArgs args)
        {
            MissionConfigData info = missionsConfig.GetMissionConfigData(args.item.id);

            if (info == null) return;

            icon_rect.Set(0, args.rowRect.y, args.rowRect.height, args.rowRect.height);
            name_rect.Set(args.rowRect.height, args.rowRect.y, args.rowRect.width - args.rowRect.height - 70, args.rowRect.height);

            id_rect.Set(args.rowRect.width - 70, args.rowRect.y, 70, args.rowRect.height);

            GUIContent icon_content = EditorAssetTool.GetTextureGUIContent(info.icon.projectName,info.icon.iconName);

            EditorGUI.LabelField(icon_rect, icon_content);

            if (name_style == null)
            {
                name_style = new GUIStyle(GUI.skin.label);
                name_style.richText = true;
            }

            EditorGUI.LabelField(name_rect, GetMissionName( info), name_style);

            GUI.Label(id_rect, string.Format("id:{0}", info.id), "AnimationTimelineTick");

            GUI.Box(args.rowRect, string.Empty, "CN Box");
        }


        private GUIContent GetMissionName(MissionConfigData info)
        {
            if (!name_contents.ContainsKey(info.id))
                name_contents.Add(info.id, new GUIContent());

            if (string.IsNullOrEmpty(info.description))
                name_contents[info.id].text = info.mission_name;
            else
                name_contents[info.id].text = string.Format("{0}<color=grey>({1})</color>", info.mission_name, info.description.Replace("\n",string.Empty));

            name_contents[info.id].tooltip = info.description;

            return name_contents[info.id];
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
            CreateMission(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            isContextClickItem = true;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                foreach (var item in GetSelection())
                {
                    DeleteMission(item);
                }
            });

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                IList<int> ids = GetSelection();

                //SetSelection(Array.Empty<int>());

                List<int> new_ids = new List<int>();

                foreach (var item in ids)
                {
                    MissionConfigData data = missionsConfig.GetMissionConfigData(item);
                    int i = CopyMission(data);
                    if (i == -1) continue;
                    new_ids.Add(i);
                }

                SetSelection(new_ids);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            });

            menu.AddItem(new GUIContent("Copy"), false, () =>
            {
                // --

                List<string> guids = new List<string>();

                foreach (var item in GetSelection())
                {
                    MissionConfigData data = missionsConfig.GetMissionConfigData(item);
                    string asset_path = AssetDatabase.GetAssetPath(data);
                    string guid = AssetDatabase.AssetPathToGUID(asset_path);
                    if (string.IsNullOrEmpty(guid)) continue;
                    guids.Add(guid);
                }

                // 写入数据
                GUIUtility.systemCopyBuffer = string.Format("{0}{1}", mission_copy_flag, JsonConvert.SerializeObject(guids));
            });



            CreateMission(menu);
        }

#if UNITY_6000_2_OR_NEWER
        protected override IList<TreeViewItem<int>> BuildRows(TreeViewItem<int> root)
#else
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
#endif

        {
            return base.BuildRows(root);
        }

        private void CreateMission(GenericMenu menu)
        {

            if (GUIUtility.systemCopyBuffer.StartsWith(mission_copy_flag))
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    //SetSelection(Array.Empty<int>());
                    // 复制
                    string content = GUIUtility.systemCopyBuffer.Substring(mission_copy_flag.Length, GUIUtility.systemCopyBuffer.Length - mission_copy_flag.Length);

                    List<string> p = JsonConvert.DeserializeObject<List<string>>(content);

                    List<int> new_ids = new List<int>();

                    foreach (var guid in p)
                    {
                        string asset_path = AssetDatabase.GUIDToAssetPath(guid);
                        MissionConfigData data = AssetDatabase.LoadAssetAtPath<MissionConfigData>(asset_path);
                        int id = CopyMission(data);
                        if (id == -1) continue;
                        new_ids.Add(id);
                    }

                    SetSelection(new_ids);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }


            menu.AddSeparator(string.Empty);

            //  查找项目中所有继承自
            List<Type> buffs = GetAllMissionTypes(missionsConfig.GetDataType());

            foreach (var item in buffs)
            {
                string name = string.Format("创建任务/{0}", item.Name);
                if (!string.IsNullOrEmpty(item.Namespace))
                    name = string.Format("{0}-{1}", name, item.Namespace);

                if (item == typeof(MissionConfigData))
                    name = "创建任务/基础配置";

                GUIContent content = new GUIContent(name);

                menu.AddItem(content, false, () =>
                {
                    int id = MissionsConfigController.CreateMissionConfigData(item,missionsConfig);

                    SetSelection(new int[] { id });

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                });
            }

            menu.ShowAsContext();
        }

        private void DeleteMission(int id)
        {
            if (missionsConfig == null)
                return;

            MissionConfigData data = missionsConfig.GetMissionConfigData(id);
            if (data == null) return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(data));
            missionsConfig.missions.Remove(data);
            EditorUtility.SetDirty(missionsConfig);
            AssetDatabase.SaveAssets();
            Reload();
        }

        private int CopyMission(MissionConfigData buff)
        {
            MissionConfigData data = GameObject.Instantiate(buff);
            int id = MissionsConfigController.CreateMissionConfigData(data,missionsConfig);
            data.mission_name = GetMissionName(buff.mission_name);

            EditorUtility.SetDirty(data);

            return id;
        }

        private string GetMissionName(string mission_name)
        {

            if (!IsContainsMissionName(mission_name))
                return mission_name;

            int count = 1;

            if (Regex.IsMatch(mission_name, "\\([0-9]+\\)$"))
            {
                for (int i = mission_name.Length - 1; i >= 0; i--)
                {
                    if (mission_name[i] == '(')
                    {
                        string c = mission_name.Substring(i + 1, mission_name.Length - i - 2);
                        count = int.Parse(c);

                        mission_name = mission_name.Substring(0, i);
                        break;
                    }
                }
            }

            mission_name = mission_name.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", mission_name, count);
                count++;
            } while (IsContainsMissionName(name));

            return name;
        }

        private bool IsContainsMissionName(string mission_name)
        {
            MissionsConfig config = missionsConfig;
            foreach (var item in config.missions)
            {
                if (item.mission_name == mission_name)
                    return true;
            }

            return false;
        }


        public static List<Type> GetAllMissionTypes(Type parent)
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

    }
}
