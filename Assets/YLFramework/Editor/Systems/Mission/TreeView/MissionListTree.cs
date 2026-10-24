using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Attributes;
using YLFramework.Editor.Utilities;
using YLFramework.Mission;

namespace YLFramework.Editor.Mission
{

    public class MissionListTree : ReorderableTreeView<MissionConfigData>
    {
        private const string MissionCopyFlag = "YLFramework.Mission.Objects.v1:";

        private MissionsConfig _missionsConfig;

        private bool _isContextClickItem;

        private Rect _iconRect;
        private Rect _nameRect;
        private Rect _idRect;

        private Dictionary<int, GUIContent> _nameContents = new Dictionary<int, GUIContent>();

        private GUIStyle _nameStyle;

        public MissionsConfig MissionsConfig => _missionsConfig;
        protected override int GetItemIndex(int id) => _missionsConfig.missions.FindIndex(data => data && data.id == id);

#if UNITY_6000_2_OR_NEWER
        public MissionListTree(TreeViewState<int> state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.missions, state)
#else
        public MissionListTree(TreeViewState state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.missions, state)
#endif

        {
            this._missionsConfig = missionsConfig;
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
                if (info == null)
                {
                    continue;
                }
#if UNITY_6000_2_OR_NEWER
                TreeViewItem<int> child = new TreeViewItem<int>(info.id, 0, string.Format("{0}{1}{2}", info.missionName, info.id, info.description));
#else
                TreeViewItem child = new TreeViewItem(info.id, 0, string.Format("{0}{1}{2}", info.missionName, info.id, info.description));
#endif

                root.AddChild(child);
            }
        }

        protected override void RefreshDatas()
        {
            base.RefreshDatas();

            if (_missionsConfig != null)
            {
                _missionsConfig.ClearCache();
            }
        }


        protected override void RowGUI(RowGUIArgs args)
        {
            MissionConfigData info = _missionsConfig.GetMissionConfigData(args.item.id);

            if (info == null)
            {
                return;
            }

            _iconRect.Set(0, args.rowRect.y, args.rowRect.height, args.rowRect.height);
            _nameRect.Set(args.rowRect.height, args.rowRect.y, args.rowRect.width - args.rowRect.height - 70, args.rowRect.height);

            _idRect.Set(args.rowRect.width - 70, args.rowRect.y, 70, args.rowRect.height);

            GUIContent iconContent = EditorAssetTool.GetTextureGuiContent(info.icon.projectName, info.icon.iconName);

            EditorGUI.LabelField(_iconRect, iconContent);

            if (_nameStyle == null)
            {
                _nameStyle = new GUIStyle(GUI.skin.label);
                _nameStyle.richText = true;
            }

            EditorGUI.LabelField(_nameRect, GetMissionName(info), _nameStyle);

            GUI.Label(_idRect, string.Format("id:{0}", info.id), "AnimationTimelineTick");

            GUI.Box(args.rowRect, string.Empty, "CN Box");
        }


        private GUIContent GetMissionName(MissionConfigData info)
        {
            if (!_nameContents.ContainsKey(info.id))
            {
                _nameContents.Add(info.id, new GUIContent());
            }

            if (string.IsNullOrEmpty(info.description))
            {
                _nameContents[info.id].text = info.missionName;
            }
            else
            {
                _nameContents[info.id].text = string.Format("{0}<color=grey>({1})</color>", info.missionName, info.description.Replace("\n", string.Empty));
            }

            _nameContents[info.id].tooltip = info.description;

            return _nameContents[info.id];
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
            CreateMission(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            _isContextClickItem = true;

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

                List<int> newIds = new List<int>();

                foreach (var item in ids)
                {
                    MissionConfigData data = _missionsConfig.GetMissionConfigData(item);
                    int i = CopyMission(data);
                    if (i == -1)
                    {
                        continue;
                    }

                    newIds.Add(i);
                }

                SetSelection(newIds);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            });

            menu.AddItem(new GUIContent("Copy"), false, CopySelection);



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

            if (GUIUtility.systemCopyBuffer.StartsWith(MissionCopyFlag))
            {
                menu.AddItem(new GUIContent("Paste"), false, PasteSelection);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }


            menu.AddSeparator(string.Empty);

            //  查找项目中所有继承自
            List<Type> buffs = GetAllMissionTypes(_missionsConfig.GetDataType());

            foreach (var item in buffs)
            {
                string name = string.Format("创建任务/{0}", item.Name);
                if (!string.IsNullOrEmpty(item.Namespace))
                {
                    name = string.Format("{0}-{1}", name, item.Namespace);
                }

                if (item == typeof(MissionConfigData))
                {
                    name = "创建任务/基础配置";
                }

                GUIContent content = new GUIContent(name);

                menu.AddItem(content, false, () =>
                {
                    int id = MissionsConfigController.CreateMissionConfigData(item, _missionsConfig);

                    SetSelection(new int[] { id });

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                });
            }

            menu.ShowAsContext();
        }

        private void CopySelection()
        {
            var objects = new List<string>();
            foreach (var id in GetSelection())
            {
                var data = _missionsConfig.GetMissionConfigData(id);
                if (data && AssetDatabase.Contains(data))
                {
                    objects.Add(GlobalObjectId.GetGlobalObjectIdSlow(data).ToString());
                }
            }
            GUIUtility.systemCopyBuffer = MissionCopyFlag + JsonConvert.SerializeObject(objects);
        }

        private void PasteSelection()
        {
            var text = GUIUtility.systemCopyBuffer;
            if (!text.StartsWith(MissionCopyFlag, StringComparison.Ordinal))
            {
                return;
            }

            var identities = JsonConvert.DeserializeObject<List<string>>(text.Substring(MissionCopyFlag.Length), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None })
                ?? throw new InvalidOperationException("任务剪贴板没有对象身份列表。");
            var sources = new List<MissionConfigData>();
            foreach (var identity in identities)
            {
                if (!GlobalObjectId.TryParse(identity, out var id) || !(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is MissionConfigData data))
                {
                    throw new InvalidOperationException("任务剪贴板对象已失效: " + identity);
                }

                sources.Add(data);
            }
            var selected = new List<int>();
            foreach (var source in sources)
            {
                selected.Add(CopyMission(source));
            }

            _missionsConfig.ClearCache();
            AssetDatabase.SaveAssetIfDirty(_missionsConfig);
            SetSelection(selected);
            Reload();
        }

        private void DeleteMission(int id)
        {
            if (_missionsConfig == null)
            {
                return;
            }

            MissionConfigData data = _missionsConfig.GetMissionConfigData(id);
            if (data == null)
            {
                return;
            }

            YLFramework.Editor.Utilities.DomainAssetOwnership.DeleteEntry(_missionsConfig, _missionsConfig.missions, data, MissionAuthoring.GeneratedDirectory(_missionsConfig));
            _missionsConfig.ClearCache();
            Reload();
        }

        private int CopyMission(MissionConfigData buff)
        {
            MissionConfigData data = GameObject.Instantiate(buff);
            int id = MissionsConfigController.CreateMissionConfigData(data, _missionsConfig);
            data.missionName = GetMissionName(buff.missionName);

            EditorUtility.SetDirty(data);

            return id;
        }

        private string GetMissionName(string missionName)
        {

            if (!IsContainsMissionName(missionName))
            {
                return missionName;
            }

            int count = 1;

            if (Regex.IsMatch(missionName, "\\([0-9]+\\)$"))
            {
                for (int i = missionName.Length - 1; i >= 0; i--)
                {
                    if (missionName[i] == '(')
                    {
                        string c = missionName.Substring(i + 1, missionName.Length - i - 2);
                        count = int.Parse(c);

                        missionName = missionName.Substring(0, i);
                        break;
                    }
                }
            }

            missionName = missionName.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", missionName, count);
                count++;
            } while (IsContainsMissionName(name));

            return name;
        }

        private bool IsContainsMissionName(string missionName)
        {
            MissionsConfig config = _missionsConfig;
            foreach (var item in config.missions)
            {
                if (item.missionName == missionName)
                {
                    return true;
                }
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

    }
}
