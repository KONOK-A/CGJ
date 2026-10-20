// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;



namespace YLFramework.MissionSystem
{

#if UNITY_6000_2_OR_NEWER
    public class ParamTreeViewItem : TreeViewItem<int>
#else
    public class ParamTreeViewItem : TreeViewItem
#endif

    {

        public MissionParameter missionParameter;

        public ParamTreeViewItem(MissionParameter missionParameter) :
            base(MissionManager.StringToHash(missionParameter.name),0, string.Format("{0}{1}", missionParameter.name, missionParameter.des))
        {
            this.missionParameter = missionParameter;
        }
    }

    public class ParamListTree : ReorderableTreeView<MissionParameter>
    {

        private const string paramter_copy_flag = "YLFramework.MissionSystem.CopyParamter";


        private MissionsConfig missionsConfig;

        private bool isContextClickItem;

        private Rect name_rect;

        private GUIStyle name_style;
        private GUIStyle type_style;
        private GUIStyle value_style;

        private Dictionary<string,GUIContent> name_contents = new Dictionary<string,GUIContent>();

        public MissionsConfig MissionsConfig => missionsConfig;
        protected override int GetItemIndex(int id) => missionsConfig.param.FindIndex(data => data != null && MissionManager.StringToHash(data.name) == id);

#if UNITY_6000_2_OR_NEWER
        public ParamListTree(TreeViewState<int> state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.param, state)
#else
        public ParamListTree(TreeViewState state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.param, state)
#endif

        {
            this.missionsConfig = missionsConfig;
            showBorder = true;
            rowHeight = 25;
        }

#if UNITY_6000_2_OR_NEWER
        protected override void OnBuildRoot(TreeViewItem<int> root, IList<MissionParameter> datas)
#else
        protected override void OnBuildRoot(TreeViewItem root, IList<MissionParameter> datas)
#endif

        {

            for (int i = 0; i < datas.Count; i++)
            {
                MissionParameter param = datas[i];
                if (param == null) continue;

                ParamTreeViewItem child = new ParamTreeViewItem(param);
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
            GUI.Box(args.rowRect, string.Empty, "CN Box");


            MissionParameter param = GetMissionParameter(args.item.id);

            if (param == null) return;

            if (args.isRenaming) return;


            name_rect.Set(args.rowRect.x + 10, args.rowRect.y, args.rowRect.width - 80, args.rowRect.height);

            if (!name_contents.ContainsKey(param.name))
                name_contents.Add(param.name,new GUIContent());


            if (string.IsNullOrEmpty(param.des))
                name_contents[param.name].text = param.name;
            else
                name_contents[param.name].text =string.Format("{0}<color=grey>({1})</color>",param.name,param.des.Replace("\n",string.Empty));

            name_contents[param.name].tooltip = param.des;

            if (name_style == null)
            {
                name_style = new GUIStyle(GUI.skin.label);
                name_style.richText = true;
            }

            EditorGUI.LabelField(name_rect, name_contents[param.name], name_style);

            name_rect.x += name_rect.width;
            name_rect.width = 45;

            name_rect.y += 1;
            name_rect.height -= 2;
            name_rect.width -= 11;

            if (type_style == null)
            {
                type_style = new GUIStyle( GUI.skin.label);
                type_style.normal.textColor = Color.yellow;
            }

            EditorGUI.LabelField(name_rect, param.type.ToString(), type_style);

            name_rect.x += name_rect.width;
            if (Application.isPlaying)
            {
                if (value_style == null)
                {
                    value_style = new GUIStyle(GUI.skin.label);
                    value_style.normal.textColor = Color.green;
                }

                switch (param.type)
                {
                    case MissionParamType.Float:
                    case MissionParamType.Int:
                        EditorGUI.LabelField(name_rect, param.Value.ToString(), value_style);
                        break;
                    case MissionParamType.Bool:
                        EditorGUI.LabelField(name_rect, (Mathf.RoundToInt(param.Value) == 1).ToString(), value_style);
                        break;
                }

            }

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
            CreateParam(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            isContextClickItem = true;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                List<MissionParameter> p = new List<MissionParameter>();

                foreach (var item in GetSelection())
                {
                    MissionParameter parameter = GetMissionParameter(item);
                    if(parameter == null) continue;
                    p.Add(parameter);
                }

                foreach (var item in p)
                {
                    DeleteParam(item);
                }

                AssetDatabase.SaveAssets();
            });

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                IList<int> ids = GetSelection();

                SetSelection(Array.Empty<int>());

                // 写入
                foreach (var item in ids)
                {
                    MissionParameter parameter = GetMissionParameter(item);
                    if(parameter == null) continue;
                    CopyMissionParam(parameter);
                }

                AssetDatabase.SaveAssets();
            });

            menu.AddItem(new GUIContent("Copy"), false, () =>
            {
                List<MissionParameter> p = new List<MissionParameter>();

                // 写入
                foreach (var item in GetSelection())
                {
                    MissionParameter parameter = GetMissionParameter(item);
                    if (parameter == null) continue;
                    p.Add(parameter);
                }

                GUIUtility.systemCopyBuffer = string.Format("{0}{1}", paramter_copy_flag, JsonConvert.SerializeObject(p));

            });

            CreateParam(menu);
        }

        private string GenerateParamName(MissionParamType type)
        {
            string name = string.Format("New {0}", type);

            int count = 1;

            while (IsContainParamName(string.Format("{0} {1}",name, count)))
            {
                count++;
            }

            return string.Format("{0} {1}", name, count);
        }

        private bool IsContainParamName(string paramName)
        {
            if (missionsConfig.GetMissionParam(paramName) != null)
                return true;

            return false;
        }

        private string GetCopyParamName(string param_name)
        {
            if (!IsContainParamName(param_name))
                return param_name;

            int count = 1;

            if (Regex.IsMatch(param_name, "\\([0-9]+\\)$"))
            {
                for (int i = param_name.Length - 1; i >= 0; i--)
                {
                    if (param_name[i] == '(')
                    {
                        string c = param_name.Substring(i + 1, param_name.Length - i - 2);
                        count = int.Parse(c);

                        param_name = param_name.Substring(0, i);
                        break;
                    }
                }
            }

            param_name = param_name.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", param_name, count);
                count++;
            } while (IsContainParamName(name));

            return name;
        }

        private void CreateParam(GenericMenu menu)
        {

            if (GUIUtility.systemCopyBuffer.StartsWith(paramter_copy_flag))
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    SetSelection(Array.Empty<int>());

                    // 复制
                    string content = GUIUtility.systemCopyBuffer.Substring(paramter_copy_flag.Length, GUIUtility.systemCopyBuffer.Length - paramter_copy_flag.Length);

                    List<MissionParameter> p = JsonConvert.DeserializeObject<List<MissionParameter>>(content);

                    foreach (var item in p)
                    {
                        CopyMissionParam(item);
                    }

                    AssetDatabase.SaveAssets();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Int"), false, () => { CreateMissionParam( MissionParamType.Int); });
            menu.AddItem(new GUIContent("Float"), false, () => { CreateMissionParam(MissionParamType.Float); });
            menu.AddItem(new GUIContent("Bool"), false, () => { CreateMissionParam(MissionParamType.Bool); });

            menu.ShowAsContext();
        }

        private void DeleteParam(MissionParameter param )
        {
            if (param == null)
                return;

            if (missionsConfig == null)
                return;

            List<MissionConfigData> missions = GetRefMissions(param);

            if (missions.Count > 0)
            {
                StringBuilder str = new StringBuilder();

                str.Append("任务:");

                foreach (var item in missions)
                {
                    str.Append("<").Append(item.mission_name).Append(">");
                }

                str.Append("引用了该参数,是否确定删除?");


                if (!EditorUtility.DisplayDialog(string.Format("是否删除参数:{0}?", param.name),str.ToString(), "删除", "取消" ))
                {
                    return;
                }
            }

            missionsConfig.param.Remove(param);
            EditorUtility.SetDirty(missionsConfig);
            missionsConfig.ClearCache();
            Reload();
        }

        /// <summary>
        /// 查询引用该参数的任务
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private List<MissionConfigData> GetRefMissions(MissionParameter p) {
            List<MissionConfigData> mission = new List<MissionConfigData>();

            foreach (var item in missionsConfig.missions)
            {
                foreach (var condition in item.conditions)
                {
                    if (condition.param_name == p.name)
                    {
                        mission.Add(item);
                        break;
                    }
                }
            }

            return mission;
        }


        private void CreateMissionParam(MissionParamType type)
        {
            MissionParameter data = new MissionParameter();
            data.name = GenerateParamName(type);
            data.type = type;
            data.Value = 0;
            missionsConfig.param.Add(data);
            EditorUtility.SetDirty(missionsConfig);
            AssetDatabase.SaveAssets();

            missionsConfig.ClearCache();

            Reload();
            // 选中
            SetSelection(new int[] { MissionManager.StringToHash(data.name) });
        }

        private void CopyMissionParam(MissionParameter source )
        {
            MissionParameter data = source.CloneConfiguration();
            data.name = GetCopyParamName( source.name);
            missionsConfig.param.Add(data);
            EditorUtility.SetDirty(missionsConfig);

            missionsConfig.ClearCache();

            Reload();

            List<int> ints = new List<int>();
            ints.AddRange(GetSelection());
            ints.Add(MissionManager.StringToHash(data.name));

            SetSelection(ints);

        }


        public void RenameParam(string originalName, string newName) {
            if (IsContainParamName( newName))
            {
                EditorWindow.focusedWindow.ShowNotification(EditorGUIUtility.TrTempContent(string.Format("参数名称:{0}已经存在,请使用其他的名称!", newName)));
                return;
            }

            MissionParameter param = missionsConfig.GetMissionParam( originalName);
            if (param == null || string.IsNullOrWhiteSpace(newName)) return;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rename mission parameter");
            Undo.RecordObject(missionsConfig, "Rename mission parameter");

            for (int i = 0; i < missionsConfig.missions.Count; i++)
            {
                MissionConfigData mission = missionsConfig.missions[i];
                if (mission == null) continue;

                for (int j = 0; j < mission.conditions.Count; j++)
                {
                    if (mission.conditions[j].param_name == originalName)
                    {
                        Undo.RecordObject(mission, "Rename mission parameter");
                        mission.conditions[j].param_name = newName;
                        EditorUtility.SetDirty(mission);
                    }
                }
            }

            param.name = newName;
            EditorUtility.SetDirty(missionsConfig);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssetIfDirty(missionsConfig);
            foreach (var mission in missionsConfig.missions) if (mission) AssetDatabase.SaveAssetIfDirty(mission);
            missionsConfig.ClearCache();

            SetSelection(new int[] { MissionManager.StringToHash(newName) });
        }

        public MissionParameter GetMissionParameter(int item_id)
        {
            ParamTreeViewItem paramTreeViewItem = FindItem(item_id, rootItem) as ParamTreeViewItem;
            if (paramTreeViewItem == null) return null;
            return paramTreeViewItem.missionParameter;
        }

    }

}
