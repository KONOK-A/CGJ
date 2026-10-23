using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;



namespace YLFramework.MissionSystem
{

#if UNITY_6000_2_OR_NEWER
    public class ParamTreeViewItem : TreeViewItem<int>
#else
    public class ParamTreeViewItem : TreeViewItem
#endif

    {

        public MissionParameter MissionParameter;

        public ParamTreeViewItem(MissionParameter missionParameter) :
            base(MissionManager.StringToHash(missionParameter.name), 0, string.Format("{0}{1}", missionParameter.name, missionParameter.description))
        {
            this.MissionParameter = missionParameter;
        }
    }

    public class ParamListTree : ReorderableTreeView<MissionParameter>
    {

        private const string ParameterCopyFlag = "YLFramework.MissionSystem.CopyParamter";


        private MissionsConfig _missionsConfig;

        private bool _isContextClickItem;

        private Rect _nameRect;

        private GUIStyle _nameStyle;
        private GUIStyle _typeStyle;
        private GUIStyle _valueStyle;

        private Dictionary<string, GUIContent> _nameContents = new Dictionary<string, GUIContent>();

        public MissionsConfig MissionsConfig => _missionsConfig;
        protected override int GetItemIndex(int id) => _missionsConfig.parameters.FindIndex(data => data != null && MissionManager.StringToHash(data.name) == id);

#if UNITY_6000_2_OR_NEWER
        public ParamListTree(TreeViewState<int> state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.parameters, state)
#else
        public ParamListTree(TreeViewState state, MissionsConfig missionsConfig) : base(missionsConfig, () => missionsConfig.parameters, state)
#endif

        {
            this._missionsConfig = missionsConfig;
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
                if (param == null)
                {
                    continue;
                }

                ParamTreeViewItem child = new ParamTreeViewItem(param);
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
            GUI.Box(args.rowRect, string.Empty, "CN Box");


            MissionParameter param = GetMissionParameter(args.item.id);

            if (param == null)
            {
                return;
            }

            if (args.isRenaming)
            {
                return;
            }

            _nameRect.Set(args.rowRect.x + 10, args.rowRect.y, args.rowRect.width - 80, args.rowRect.height);

            if (!_nameContents.ContainsKey(param.name))
            {
                _nameContents.Add(param.name, new GUIContent());
            }

            if (string.IsNullOrEmpty(param.description))
            {
                _nameContents[param.name].text = param.name;
            }
            else
            {
                _nameContents[param.name].text = string.Format("{0}<color=grey>({1})</color>", param.name, param.description.Replace("\n", string.Empty));
            }

            _nameContents[param.name].tooltip = param.description;

            if (_nameStyle == null)
            {
                _nameStyle = new GUIStyle(GUI.skin.label);
                _nameStyle.richText = true;
            }

            EditorGUI.LabelField(_nameRect, _nameContents[param.name], _nameStyle);

            _nameRect.x += _nameRect.width;
            _nameRect.width = 45;

            _nameRect.y += 1;
            _nameRect.height -= 2;
            _nameRect.width -= 11;

            if (_typeStyle == null)
            {
                _typeStyle = new GUIStyle(GUI.skin.label);
                _typeStyle.normal.textColor = Color.yellow;
            }

            EditorGUI.LabelField(_nameRect, param.type.ToString(), _typeStyle);

            _nameRect.x += _nameRect.width;
            if (Application.isPlaying)
            {
                if (_valueStyle == null)
                {
                    _valueStyle = new GUIStyle(GUI.skin.label);
                    _valueStyle.normal.textColor = Color.green;
                }

                switch (param.type)
                {
                    case MissionParamType.Float:
                    case MissionParamType.Int:
                        EditorGUI.LabelField(_nameRect, param.Value.ToString(), _valueStyle);
                        break;
                    case MissionParamType.Bool:
                        EditorGUI.LabelField(_nameRect, (Mathf.RoundToInt(param.Value) == 1).ToString(), _valueStyle);
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

            if (_isContextClickItem)
            {
                _isContextClickItem = false;
                return;
            }

            GenericMenu menu = new GenericMenu();
            CreateParam(menu);
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            _isContextClickItem = true;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                List<MissionParameter> p = new List<MissionParameter>();

                foreach (var item in GetSelection())
                {
                    MissionParameter parameter = GetMissionParameter(item);
                    if (parameter == null)
                    {
                        continue;
                    }

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
                    if (parameter == null)
                    {
                        continue;
                    }

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
                    if (parameter == null)
                    {
                        continue;
                    }

                    p.Add(parameter);
                }

                GUIUtility.systemCopyBuffer = string.Format("{0}{1}", ParameterCopyFlag, JsonConvert.SerializeObject(p));

            });

            CreateParam(menu);
        }

        private string GenerateParamName(MissionParamType type)
        {
            string name = string.Format("New {0}", type);

            int count = 1;

            while (IsContainParamName(string.Format("{0} {1}", name, count)))
            {
                count++;
            }

            return string.Format("{0} {1}", name, count);
        }

        private bool IsContainParamName(string paramName)
        {
            if (_missionsConfig.GetMissionParam(paramName) != null)
            {
                return true;
            }

            return false;
        }

        private string GetCopyParamName(string paramName)
        {
            if (!IsContainParamName(paramName))
            {
                return paramName;
            }

            int count = 1;

            if (Regex.IsMatch(paramName, "\\([0-9]+\\)$"))
            {
                for (int i = paramName.Length - 1; i >= 0; i--)
                {
                    if (paramName[i] == '(')
                    {
                        string c = paramName.Substring(i + 1, paramName.Length - i - 2);
                        count = int.Parse(c);

                        paramName = paramName.Substring(0, i);
                        break;
                    }
                }
            }

            paramName = paramName.TrimEnd();

            string name = string.Empty;

            do
            {
                name = string.Format("{0} ({1})", paramName, count);
                count++;
            } while (IsContainParamName(name));

            return name;
        }

        private void CreateParam(GenericMenu menu)
        {

            if (GUIUtility.systemCopyBuffer.StartsWith(ParameterCopyFlag))
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    SetSelection(Array.Empty<int>());

                    // 复制
                    string content = GUIUtility.systemCopyBuffer.Substring(ParameterCopyFlag.Length, GUIUtility.systemCopyBuffer.Length - ParameterCopyFlag.Length);

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

            menu.AddItem(new GUIContent("Int"), false, () =>
            {
                CreateMissionParam(MissionParamType.Int);
            });
            menu.AddItem(new GUIContent("Float"), false, () =>
            {
                CreateMissionParam(MissionParamType.Float);
            });
            menu.AddItem(new GUIContent("Bool"), false, () =>
            {
                CreateMissionParam(MissionParamType.Bool);
            });

            menu.ShowAsContext();
        }

        private void DeleteParam(MissionParameter param)
        {
            if (param == null)
            {
                return;
            }

            if (_missionsConfig == null)
            {
                return;
            }

            List<MissionConfigData> missions = GetRefMissions(param);

            if (missions.Count > 0)
            {
                StringBuilder str = new StringBuilder();

                str.Append("任务:");

                foreach (var item in missions)
                {
                    str.Append("<").Append(item.missionName).Append(">");
                }

                str.Append("引用了该参数,是否确定删除?");


                if (!EditorUtility.DisplayDialog(string.Format("是否删除参数:{0}?", param.name), str.ToString(), "删除", "取消"))
                {
                    return;
                }
            }

            _missionsConfig.parameters.Remove(param);
            EditorUtility.SetDirty(_missionsConfig);
            _missionsConfig.ClearCache();
            Reload();
        }

        /// <summary>
        /// 查询引用该参数的任务
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private List<MissionConfigData> GetRefMissions(MissionParameter p)
        {
            List<MissionConfigData> mission = new List<MissionConfigData>();

            foreach (var item in _missionsConfig.missions)
            {
                foreach (var condition in item.conditions)
                {
                    if (condition.paramName == p.name)
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
            _missionsConfig.parameters.Add(data);
            EditorUtility.SetDirty(_missionsConfig);
            AssetDatabase.SaveAssets();

            _missionsConfig.ClearCache();

            Reload();
            // 选中
            SetSelection(new int[] { MissionManager.StringToHash(data.name) });
        }

        private void CopyMissionParam(MissionParameter source)
        {
            MissionParameter data = source.CloneConfiguration();
            data.name = GetCopyParamName(source.name);
            _missionsConfig.parameters.Add(data);
            EditorUtility.SetDirty(_missionsConfig);

            _missionsConfig.ClearCache();

            Reload();

            List<int> ints = new List<int>();
            ints.AddRange(GetSelection());
            ints.Add(MissionManager.StringToHash(data.name));

            SetSelection(ints);

        }


        public void RenameParam(string originalName, string newName)
        {
            if (IsContainParamName(newName))
            {
                EditorWindow.focusedWindow.ShowNotification(EditorGUIUtility.TrTempContent(string.Format("参数名称:{0}已经存在,请使用其他的名称!", newName)));
                return;
            }

            MissionParameter param = _missionsConfig.GetMissionParam(originalName);
            if (param == null || string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rename mission parameter");
            Undo.RecordObject(_missionsConfig, "Rename mission parameter");

            for (int i = 0; i < _missionsConfig.missions.Count; i++)
            {
                MissionConfigData mission = _missionsConfig.missions[i];
                if (mission == null)
                {
                    continue;
                }

                for (int j = 0; j < mission.conditions.Count; j++)
                {
                    if (mission.conditions[j].paramName == originalName)
                    {
                        Undo.RecordObject(mission, "Rename mission parameter");
                        mission.conditions[j].paramName = newName;
                        EditorUtility.SetDirty(mission);
                    }
                }
            }

            param.name = newName;
            EditorUtility.SetDirty(_missionsConfig);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssetIfDirty(_missionsConfig);
            foreach (var mission in _missionsConfig.missions)
            {
                if (mission)
                {
                    AssetDatabase.SaveAssetIfDirty(mission);
                }
            }

            _missionsConfig.ClearCache();

            SetSelection(new int[] { MissionManager.StringToHash(newName) });
        }

        public MissionParameter GetMissionParameter(int itemId)
        {
            ParamTreeViewItem paramTreeViewItem = FindItem(itemId, rootItem) as ParamTreeViewItem;
            if (paramTreeViewItem == null)
            {
                return null;
            }

            return paramTreeViewItem.MissionParameter;
        }

    }

}
