using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using static UnityEditor.Progress;

namespace YLFramework.MissionSystem
{
    public class ConfigsWindow : SplitWindow
    {

        [MenuItem("Window/YLFramework/MissionSystem", false, 10)]
        static void Open()
        {
            GetWindow<ConfigsWindow>().Show();
        }

        #region 字段

        private const string SelectGuidKey = "YLFramework.MissionSystem.SELECT_GUID_KEY";

        private GUIContent _currentMissionConfigContent;

        private string _searchStr;

        [SerializeField]
        private TreeViewStateGroup treeViewStateGroup = new TreeViewStateGroup();

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> MissionListState => treeViewStateGroup.Get(CurrentMissionConfig.GetHashCode());
#else
        private TreeViewState MissionListState => treeViewStateGroup.Get(CurrentMissionConfig.GetHashCode());
#endif


        internal MissionListTree MissionListTree;

        [SerializeField]
        private TreeViewStateGroup treeViewStateGroupParam = new TreeViewStateGroup();

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> ParamListState => treeViewStateGroupParam.Get(CurrentMissionConfig.GetHashCode());
#else
        private TreeViewState ParamListState => treeViewStateGroupParam.Get(CurrentMissionConfig.GetHashCode());
#endif


        internal ParamListTree ParamListTree;


        internal MissionsConfig CurrentMissionConfig;

        private float _timer = 0;

        internal int CurrentShowMissionId = 0;

        internal int CurrentShowParamIndex = 0;

        internal static int DefaultSelectMissionId = -1;


        private GUIContent[] _leftToolbarContents = null;

        private int _leftToolbarSelected = 0;

        private GUIStyle _leftToolbarStyle = null;

        private float _lastRepaintCount = 0;

        private int _currentId;
        public int idChangeCount;

        private int _currentParameterId;
        public int parameterIdChangeCount;

        #endregion


        #region 属性

        internal static string SelectGuid
        {
            get
            {
                return EditorPrefs.GetString(SelectGuidKey);
            }
            set
            {
                EditorPrefs.SetString(SelectGuidKey, value);
            }

        }

        #endregion


        #region 生命周期

        private void Awake()
        {
            titleContent = new GUIContent("任务配置窗口", "任务配置窗口");
        }

        private void Update()
        {
            _lastRepaintCount++;
            if (_lastRepaintCount > 15)
            {
                Repaint();
                _lastRepaintCount = 0;
            }

        }

        protected override void OnGUI()
        {
            if (Time.realtimeSinceStartup - _timer > 1 && CurrentMissionConfig == null)
            {
                CurrentMissionConfig = GetCurrentConfig();
                _timer = Time.realtimeSinceStartup;
            }

            if (CurrentMissionConfig == null)
            {
                EmptyGui();
            }
            else
            {
                base.OnGUI();
            }
        }


        #endregion


        #region 方法

        internal MissionsConfig GetCurrentConfig()
        {
            if (!string.IsNullOrEmpty(SelectGuid))
            {
                string assetsPath = AssetDatabase.GUIDToAssetPath(SelectGuid);
                MissionsConfig config = AssetDatabase.LoadAssetAtPath<MissionsConfig>(assetsPath);

                if (config != null)
                {
                    return config;
                }
            }

            List<MissionsConfig> allConfigs = MissionsConfigController.GetAllConfigs();

            if (allConfigs == null || allConfigs.Count == 0)
            {
                return null;
            }

            SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(allConfigs[0]));
            return allConfigs[0];
        }


        private void EmptyGui()
        {

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label("未查询到任务配置文件!");
            if (GUILayout.Button("立即创建"))
            {
                CreateNew();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

        }

        protected override void DrawGuiLeft(Rect rect)
        {
            base.DrawGuiLeft(rect);
            GUILayout.BeginArea(rect);
            DrawLeft(rect);
            GUILayout.EndArea();
        }

        protected override void DrawGuiRight(Rect rect)
        {
            base.DrawGuiRight(rect);
            GUILayout.BeginArea(rect);
            DrawRight();
            GUILayout.EndArea();
        }

        private void DrawLeft(Rect left)
        {
            if (_currentMissionConfigContent == null)
            {
                _currentMissionConfigContent = new GUIContent();
            }

            if (CurrentMissionConfig == null)
            {
                return;
            }

            _currentMissionConfigContent.text = CurrentMissionConfig.GetDisplayName();

            _currentMissionConfigContent.tooltip = AssetDatabase.GetAssetPath(CurrentMissionConfig);

            GUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();

            _searchStr = EditorGUI.TextField(new Rect(left.x, left.y, left.width * 0.5f, 20), string.Empty, _searchStr, "SearchTextField");

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(_searchStr))
                {
                    if (_leftToolbarSelected == 0 && MissionListTree.state.selectedIDs.Count > 0)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            MissionListTree.FrameItem(MissionListTree.state.selectedIDs[0]);
                        };
                    }
                    else if (_leftToolbarSelected == 1 && ParamListTree.state.selectedIDs.Count > 0)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            ParamListTree.FrameItem(ParamListTree.state.selectedIDs[0]);
                        };
                    }
                }
            }

            GUI.Box(new Rect(left.x + 1, left.y, left.width - 2, left.height), string.Empty, "CN Box");

            if (EditorGUI.DropdownButton(new Rect(left.x + left.width * 0.5f, left.y, left.width * 0.5f + 1, 20), _currentMissionConfigContent, FocusType.Passive, "ToolbarDropDownLeft"))
            {
                GUI.FocusControl(null);
                //return;

                GenericMenu menu = new GenericMenu();

                List<MissionsConfig> allConfigs = MissionsConfigController.GetAllConfigs();

                foreach (var item in allConfigs)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                    bool isOn = SelectGuid == guid;

                    menu.AddItem(new GUIContent(item.GetDisplayName()), isOn, () =>
                    {
                        SelectGuid = guid;
                        EditorGUIUtility.PingObject(item);

                        if (!isOn)
                        {
                            SetCurrentConfigs(item);
                        }
                    });
                }

                menu.AddSeparator(string.Empty);

                menu.AddItem(new GUIContent("新建任务配表"), false, () =>
                {
                    CreateNew();
                });

                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();


            if (_leftToolbarContents == null || _leftToolbarContents.Length == 0)
            {
                _leftToolbarContents = new GUIContent[] { new GUIContent("任务列表"), new GUIContent("参数列表") };
            }

            if (_leftToolbarStyle == null)
            {
                _leftToolbarStyle = new GUIStyle("toolbarbuttonLeft");
            }

            _leftToolbarSelected = GUI.Toolbar(new Rect(left.x, left.y + 20, left.width, 20), _leftToolbarSelected, _leftToolbarContents);

            if (_leftToolbarSelected == 0)
            {
                if (MissionListTree == null)
                {
                    MissionListTree = new MissionListTree(MissionListState, CurrentMissionConfig);
                    MissionListTree.Reload();
                }

                MissionListTree.searchString = _searchStr;

                left.Set(left.x, left.y + 39, left.width + 1, left.height - 39);

                MissionListTree.OnGUI(left);

                if (MissionListTree != null && MissionListTree.MissionsConfig != CurrentMissionConfig)
                {
                    //missionListTree.SetSelection(Array.Empty<int>());
                    MissionListTree.Dispose();
                    MissionListTree = null;
                }

            }
            else if (_leftToolbarSelected == 1)
            {
                if (ParamListTree == null)
                {
                    ParamListTree = new ParamListTree(ParamListState, CurrentMissionConfig);
                    ParamListTree.Reload();
                }

                ParamListTree.searchString = _searchStr;

                left.Set(left.x, left.y + 39, left.width, left.height - 39);

                ParamListTree.OnGUI(left);

                if (ParamListTree != null && ParamListTree.MissionsConfig != CurrentMissionConfig)
                {
                    ParamListTree.Dispose();
                    ParamListTree = null;
                }
            }
        }

        private void DrawRight()
        {
            // 判断是否为空
            if (IsEmpty())
            {
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("空!");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
                return;
            }

            if (_leftToolbarSelected == 0)
            {
                if (MissionListState.selectedIDs.Count == 1)
                {
                    DrawMissionData();
                }
                else if (MissionListState.selectedIDs.Count == 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("空!");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    Color origin = GUI.color;
                    GUI.color = Color.yellow;
                    GUILayout.Label("不支持同时编辑多个配置!");
                    GUI.color = origin;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                }

            }
            else
            {

                if (ParamListState.selectedIDs.Count == 1)
                {
                    if (ParamListTree != null)
                    {
                        DrawParameterData();
                    }

                }
                else if (ParamListState.selectedIDs.Count == 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("空!");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    Color origin = GUI.color;
                    GUI.color = Color.yellow;
                    GUILayout.Label("不支持同时编辑多个配置!");
                    GUI.color = origin;
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                }


            }
        }

        private void DrawMissionData()
        {
            int id = MissionListState.selectedIDs[0];

            if (_currentId != id)
            {
                idChangeCount++;
                if (idChangeCount > 1)
                {
                    // 如果当前帧是Layout ，不能设置，因为当前帧会被return
                    // 如果当前帧被return , 下一帧repaint 会报错，因为没有位置数据
                    if (Event.current.type != EventType.Layout)
                    {
                        _currentId = id;
                        idChangeCount = 0;
                    }
                }
                // 这样处理的目的是 为了防止 EditorGUILayout.DelayedIntField 在编辑时 ,
                // 直接切换id,  EditorGUILayout.DelayedIntField  返回的值会直接给新选中的数据， 导致数据错误的情况
                return;
            }

            MissionConfigData data = CurrentMissionConfig.GetMissionConfigData(id);
            MissionConfigDataInspector.Instance.OnInspectorGUI(data, this, CurrentMissionConfig);
        }

        private void DrawParameterData()
        {
            int id = ParamListState.selectedIDs[0];

            if (_currentParameterId != id)
            {
                parameterIdChangeCount++;
                if (parameterIdChangeCount > 1)
                {
                    // 如果当前帧是Layout ，不能设置，因为当前帧会被return
                    // 如果当前帧被return , 下一帧repaint 会报错，因为没有位置数据
                    if (Event.current.type != EventType.Layout)
                    {
                        _currentParameterId = id;
                        parameterIdChangeCount = 0;
                    }

                }
                // 这样处理的目的是 为了防止 EditorGUILayout.DelayedIntField 在编辑时 ,
                // 直接切换id,  EditorGUILayout.DelayedIntField  返回的值会直接给新选中的数据， 导致数据错误的情况
                return;
            }

            MissionParameter parameter = ParamListTree.GetMissionParameter(id);
            MissionParameterInspector.OnInspectorGUI(CurrentMissionConfig, parameter, ParamListTree);
        }

        internal bool IsEmpty()
        {
            if (CurrentMissionConfig == null)
            {
                return true;
            }

            for (int i = 0; i < CurrentMissionConfig.missions.Count; i++)
            {
                if (CurrentMissionConfig.missions[i] != null)
                {
                    return false;
                }
            }


            return true;
        }

        public void SetSelection(int id)
        {
            if (MissionListTree != null)
            {
                MissionListTree.SetSelection(new int[] { id });
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (MissionListTree != null)
                    {
                        MissionListTree.SetSelection(new int[] { id });
                    }
                };
            }

        }

        public void SetCurrentConfigs(MissionsConfig missionsConfig)
        {
            ReleaseTrees();
            _currentId = -1;
            _currentParameterId = -1;

            CurrentMissionConfig = missionsConfig;

            SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(missionsConfig));

            if (MissionListTree != null)
            {
                MissionListTree.Reload();
            }
        }

        private void ReleaseTrees()
        {
            MissionListTree?.Dispose();
            ParamListTree?.Dispose();
            MissionListTree = null;
            ParamListTree = null;
        }
        private void OnDisable()
        {
            ReleaseTrees();
            MissionConfigDataInspector.Instance.ReleaseSelection();
            MissionParameterInspector.ReleaseSelection();
        }

        #endregion


        #region 创建

        private void CreateNew()
        {
            string path = EditorUtility.OpenFolderPanel("请选择文件夹", Application.dataPath, string.Empty);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (path.StartsWith(Application.dataPath))
            {
                path = path.Replace(Application.dataPath, string.Empty);
                path = string.Format("Assets{0}", path);
                MissionsConfig skillsConfig = CreateInstance<MissionsConfig>();
                skillsConfig.startId = InitIdStart();
                MissionsConfigController.CreateAsset(skillsConfig, path);

                SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skillsConfig));

                if (MissionListTree != null)
                {
                    MissionListTree.Reload();
                }
            }
            else
            {
                this.ShowNotification(new GUIContent("请把资源保存到Assets目录下!"));
            }
        }

        private int InitIdStart()
        {
            int maxIdStart = 0;

            List<MissionsConfig> allConfigs = MissionsConfigController.GetAllConfigs();

            foreach (var item in allConfigs)
            {
                if (item.startId > maxIdStart)
                {
                    maxIdStart = item.startId;
                }
            }

            return maxIdStart + 1000;
        }

        #endregion


    }

}
