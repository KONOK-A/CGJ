using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
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

        private const string SELECT_GUID_KEY = "YLFramework.MissionSystem.SELECT_GUID_KEY";

        private GUIContent currentMissionConfigContent;

        private string searchStr;

        [SerializeField]
        private TreeViewStateGroup treeViewStateGroup = new TreeViewStateGroup();

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> missionListState => treeViewStateGroup.Get(currentMissionConfig.GetHashCode());
#else
        private TreeViewState missionListState => treeViewStateGroup.Get(currentMissionConfig.GetHashCode());
#endif


        internal MissionListTree missionListTree;

        [SerializeField]
        private TreeViewStateGroup treeViewStateGroupParam = new TreeViewStateGroup();

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> paramListState => treeViewStateGroupParam.Get(currentMissionConfig.GetHashCode());
#else
        private TreeViewState paramListState => treeViewStateGroupParam.Get(currentMissionConfig.GetHashCode());
#endif


        internal ParamListTree paramListTree;


        internal MissionsConfig currentMissionConfig;

        private float timer = 0;

        internal int currentShowMissionId = 0;

        internal int currentShowParamIndex = 0;

        internal static int DefaultSelectMissionId = -1;


        private GUIContent[] LeftToolbarContents = null;

        private int leftToolbarSelected = 0;

        private GUIStyle leftToolbarStyle = null;

        private float lastRepaintCount = 0;

        private int currentID;
        public int idChangeCount;

        private int currentParameterID;
        public int parameterIdChangeCount;

#endregion


        #region 属性

        internal static string SelectGUID
        {
            get
            {
                return EditorPrefs.GetString(SELECT_GUID_KEY);
            }
            set
            {
                EditorPrefs.SetString(SELECT_GUID_KEY, value);
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
            lastRepaintCount++;
            if (lastRepaintCount > 15)
            {
                Repaint();
                lastRepaintCount = 0;
            }

        }

        protected override void OnGUI()
        {
            if (Time.realtimeSinceStartup - timer > 1 && currentMissionConfig == null)
            {
                currentMissionConfig = GetCurrentConfig();
                timer = Time.realtimeSinceStartup;
            }

            if (currentMissionConfig == null)
                EmptyGUI();
            else
                base.OnGUI();

        }


        #endregion


        #region 方法

        internal MissionsConfig GetCurrentConfig()
        {
            if (!string.IsNullOrEmpty(SelectGUID))
            {
                string assets_path = AssetDatabase.GUIDToAssetPath(SelectGUID);
                MissionsConfig config = AssetDatabase.LoadAssetAtPath<MissionsConfig>(assets_path);

                if (config != null)
                    return config;
            }

            List<MissionsConfig> allConfigs = MissionsConfigController.GetAllConfigs();

            if (allConfigs == null || allConfigs.Count == 0)
                return null;

            SelectGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(allConfigs[0]));
            return allConfigs[0];
        }


        private void EmptyGUI()
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

        protected override void DrawGUILeft(Rect rect)
        {
            base.DrawGUILeft(rect);
            GUILayout.BeginArea(rect);
            DrawLeft(rect);
            GUILayout.EndArea();
        }

        protected override void DrawGUIRight(Rect rect)
        {
            base.DrawGUIRight(rect);
            GUILayout.BeginArea(rect);
            DrawRight();
            GUILayout.EndArea();
        }

        private void DrawLeft(Rect left)
        {
            if (currentMissionConfigContent == null)
                currentMissionConfigContent = new GUIContent();

            if (currentMissionConfig == null)
                return;

            currentMissionConfigContent.text = currentMissionConfig.GetDisplayName();

            currentMissionConfigContent.tooltip = AssetDatabase.GetAssetPath(currentMissionConfig);

            GUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();

            searchStr = EditorGUI.TextField(new Rect(left.x, left.y, left.width * 0.5f, 20), string.Empty, searchStr, "SearchTextField");

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(searchStr)  )
                {
                    if (leftToolbarSelected == 0 && missionListTree.state.selectedIDs.Count > 0)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            missionListTree.FrameItem(missionListTree.state.selectedIDs[0]);
                        };
                    }
                    else if(leftToolbarSelected == 1 && paramListTree.state.selectedIDs.Count > 0)
                    {
                        EditorApplication.delayCall += () =>
                        {
                            paramListTree.FrameItem(paramListTree.state.selectedIDs[0]);
                        };
                    }
                }
            }

            GUI.Box(new Rect(left.x + 1, left.y, left.width - 2, left.height), string.Empty, "CN Box");

            if (EditorGUI.DropdownButton(new Rect(left.x + left.width *0.5f, left.y, left.width * 0.5f + 1, 20), currentMissionConfigContent, FocusType.Passive, "ToolbarDropDownLeft"))
            {
                GUI.FocusControl(null);
                //return;

                GenericMenu menu = new GenericMenu();

                List<MissionsConfig> allConfigs = MissionsConfigController.GetAllConfigs();

                foreach (var item in allConfigs)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                    bool isOn = SelectGUID == guid;

                    menu.AddItem(new GUIContent(item.GetDisplayName()), isOn, () =>
                    {
                        SelectGUID = guid;
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


            if (LeftToolbarContents == null || LeftToolbarContents.Length == 0)
                LeftToolbarContents = new GUIContent[] { new GUIContent("任务列表"), new GUIContent("参数列表") };

            if (leftToolbarStyle == null)
            {
                leftToolbarStyle = new GUIStyle("toolbarbuttonLeft");
            }

            leftToolbarSelected = GUI.Toolbar(new Rect(left.x, left.y + 20, left.width, 20), leftToolbarSelected, LeftToolbarContents);

            if (leftToolbarSelected == 0)
            {
                if (missionListTree == null)
                {
                    missionListTree = new MissionListTree(missionListState, currentMissionConfig);
                    missionListTree.Reload();
                }

                missionListTree.searchString = searchStr;

                left.Set(left.x, left.y + 39, left.width + 1, left.height - 39);

                missionListTree.OnGUI(left);

                if (missionListTree != null && missionListTree.MissionsConfig != currentMissionConfig)
                {
                    //missionListTree.SetSelection(Array.Empty<int>());
                    missionListTree.Dispose();
                    missionListTree = null;
                }

            }
            else if (leftToolbarSelected == 1)
            {
                if (paramListTree == null)
                {
                    paramListTree = new ParamListTree(paramListState, currentMissionConfig);
                    paramListTree.Reload();
                }

                paramListTree.searchString = searchStr;

                left.Set(left.x, left.y + 39, left.width, left.height - 39);

                paramListTree.OnGUI(left);

                if (paramListTree != null && paramListTree.MissionsConfig != currentMissionConfig)
                {
                    paramListTree.Dispose();
                    paramListTree = null;
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

            if (leftToolbarSelected == 0)
            {
                if (missionListState.selectedIDs.Count == 1)
                {
                    DrawMissionData();
                }
                else if (missionListState.selectedIDs.Count == 0)
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

                if (paramListState.selectedIDs.Count == 1  )
                {
                    if (paramListTree != null)
                    {
                        DrawParameterData();
                    }

                }
                else if (paramListState.selectedIDs.Count == 0)
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
            int id = missionListState.selectedIDs[0];

            if (currentID != id)
            {
                idChangeCount++;
                if (idChangeCount > 1)
                {
                    // 如果当前帧是Layout ，不能设置，因为当前帧会被return
                    // 如果当前帧被return , 下一帧repaint 会报错，因为没有位置数据
                    if (Event.current.type != EventType.Layout)
                    {
                        currentID = id;
                        idChangeCount = 0;
                    }
                }
                // 这样处理的目的是 为了防止 EditorGUILayout.DelayedIntField 在编辑时 ,
                // 直接切换id,  EditorGUILayout.DelayedIntField  返回的值会直接给新选中的数据， 导致数据错误的情况
                return;
            }

            MissionConfigData data = currentMissionConfig.GetMissionConfigData(id);
            MissionConfigDataInspector.Instance.OnInspectorGUI(data, this, currentMissionConfig);
        }

        private void DrawParameterData()
        {
            int id = paramListState.selectedIDs[0];

            if (currentParameterID != id)
            {
                parameterIdChangeCount++;
                if (parameterIdChangeCount > 1)
                {
                    // 如果当前帧是Layout ，不能设置，因为当前帧会被return
                    // 如果当前帧被return , 下一帧repaint 会报错，因为没有位置数据
                    if (Event.current.type != EventType.Layout)
                    {
                        currentParameterID = id;
                        parameterIdChangeCount = 0;
                    }

                }
                // 这样处理的目的是 为了防止 EditorGUILayout.DelayedIntField 在编辑时 ,
                // 直接切换id,  EditorGUILayout.DelayedIntField  返回的值会直接给新选中的数据， 导致数据错误的情况
                return;
            }

            MissionParameter parameter = paramListTree.GetMissionParameter(id);
            MissionParameterInspector.OnInspectorGUI(currentMissionConfig, parameter, paramListTree);
        }

        internal bool IsEmpty()
        {
            if (currentMissionConfig == null)
                return true;

            for (int i = 0; i < currentMissionConfig.missions.Count; i++)
            {
                if (currentMissionConfig.missions[i] != null)
                    return false;
            }


            return true;
        }

        public void SetSelection(int id)
        {
            if (missionListTree != null)
            {
                missionListTree.SetSelection(new int[] { id });
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (missionListTree != null)
                    {
                        missionListTree.SetSelection(new int[] { id });
                    }
                };
            }

        }

        public void SetCurrentConfigs(MissionsConfig missionsConfig) {
            ReleaseTrees();
            currentID = -1;
            currentParameterID = -1;

            currentMissionConfig = missionsConfig;

            SelectGUID = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath(missionsConfig));

            if (missionListTree != null)
            {
                missionListTree.Reload();
            }
        }

        private void ReleaseTrees()
        {
            missionListTree?.Dispose();
            paramListTree?.Dispose();
            missionListTree = null;
            paramListTree = null;
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

            if (string.IsNullOrEmpty(path)) return;

            if (path.StartsWith(Application.dataPath))
            {
                path = path.Replace(Application.dataPath, string.Empty);
                path = string.Format("Assets{0}", path);
                MissionsConfig skillsConfig = CreateInstance<MissionsConfig>();
                skillsConfig.start_id = InitIDStart();
                MissionsConfigController.CreateAsset(skillsConfig, path);

                SelectGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skillsConfig));

                if (missionListTree != null)
                    missionListTree.Reload();

            }
            else
            {
                this.ShowNotification(new GUIContent("请把资源保存到Assets目录下!"));
            }
        }

        private int InitIDStart()
        {
            int max_id_start = 0;

            List<MissionsConfig> allConfigs = MissionsConfigController.GetAllConfigs();

            foreach (var item in allConfigs)
            {
                if (item.start_id > max_id_start)
                    max_id_start = item.start_id;
            }

            return max_id_start + 1000;
        }

        #endregion


    }

}
