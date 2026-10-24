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
    public class ConfigsWindow : SplitWindow
    {

        [MenuItem("Window/YLFramework/SkillSystem", false, 10)]
        static void Open()
        {
            GetWindow<ConfigsWindow>().Show();
        }

        #region 字段

        private const string SelectGuidKey = "YLFramework.Skill.SelectedConfig";

        //private Rect left;

        private GUIContent _currentSkillConfigContent;

        private string _searchStr;

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> SkillListState => treeViewStateGroup.Get(CurrentSkillConfig.GetHashCode());

#else
        private TreeViewState SkillListState => treeViewStateGroup.Get(CurrentSkillConfig.GetHashCode());
#endif


        [SerializeField]
        private TreeViewStateGroup treeViewStateGroup = new TreeViewStateGroup();

        internal SkillListTree SkillListTree;

        internal SkillsConfig CurrentSkillConfig;

        private float _timer = 0;

        internal int CurrentShowSkillId = 0;

        internal static int DefaultSelectSkillId = -1;

        private Vector2 _rightScrollPosition;

        private float _lastRepaintCount;


        private int _currentId;
        public int idChangeCount;

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
            titleContent = new GUIContent("技能配置窗口", "技能配置窗口");
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
            if (Time.realtimeSinceStartup - _timer > 1 && CurrentSkillConfig == null)
            {
                CurrentSkillConfig = GetCurrentConfig();
                _timer = Time.realtimeSinceStartup;
            }

            if (CurrentSkillConfig == null)
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

        internal SkillsConfig GetCurrentConfig()
        {
            if (!string.IsNullOrEmpty(SelectGuid))
            {
                string assetsPath = AssetDatabase.GUIDToAssetPath(SelectGuid);
                SkillsConfig config = AssetDatabase.LoadAssetAtPath<SkillsConfig>(assetsPath);

                if (config != null)
                {
                    return config;
                }
            }

            List<SkillsConfig> configs = SkillsConfigController.GetAllConfigs();

            if (configs == null || configs.Count == 0)
            {
                return null;
            }

            SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(configs[0]));
            return configs[0];
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


        private void EmptyGui()
        {

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label("未查询到技能配置文件!");
            if (GUILayout.Button("立即创建"))
            {
                CreateNew();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

        }

        private void DrawLeft(Rect rect)
        {
            if (_currentSkillConfigContent == null)
            {
                _currentSkillConfigContent = new GUIContent();
            }

            if (CurrentSkillConfig == null)
            {
                return;
            }

            _currentSkillConfigContent.text = CurrentSkillConfig.name;
            _currentSkillConfigContent.tooltip = AssetDatabase.GetAssetPath(CurrentSkillConfig);

            GUILayout.BeginHorizontal();
            //

            EditorGUI.BeginChangeCheck();

            _searchStr = EditorGUILayout.TextField(string.Empty, _searchStr, "SearchTextField", GUILayout.MinWidth(0));

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(_searchStr) && SkillListTree.state.selectedIDs.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        SkillListTree.FrameItem(SkillListTree.state.selectedIDs[0]);
                    };
                }
            }

            if (EditorGUILayout.DropdownButton(_currentSkillConfigContent, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(this.position.width * 0.35f * 0.3f)))
            {
                GenericMenu menu = new GenericMenu();
                List<SkillsConfig> configs = SkillsConfigController.GetAllConfigs();
                foreach (var item in configs)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                    bool isOn = SelectGuid == guid;

                    menu.AddItem(new GUIContent(item.name), isOn, () =>
                    {
                        SelectGuid = guid;

                        EditorApplication.delayCall += () =>
                        {
                            EditorGUIUtility.PingObject(item);
                        };
                        if (!isOn)
                        {
                            SetCurrentConfigs(item);
                        }
                    });
                }

                menu.AddSeparator(string.Empty);

                menu.AddItem(new GUIContent("新建技能配表"), false, () =>
                {
                    CreateNew();
                });

                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();




            if (SkillListTree == null)
            {
                SkillListTree = new SkillListTree(SkillListState, CurrentSkillConfig);
                SkillListTree.Reload();
            }

            SkillListTree.searchString = _searchStr;

            rect.Set(rect.x, rect.y + 20, rect.width, rect.height - 20);


            SkillListTree.OnGUI(rect);


            if (SkillListTree != null && SkillListTree.SkillsConfig != CurrentSkillConfig)
            {
                SkillListTree.Dispose();
                SkillListTree = null;
            }
        }

        private void DrawRight()
        {

            // 判断是否为空 TODO

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

            if (SkillListState.selectedIDs.Count == 1)
            {
                DrawData();
            }
            else if (SkillListState.selectedIDs.Count == 0)
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

        private void DrawData()
        {
            int id = SkillListState.selectedIDs[0];

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


            _rightScrollPosition = GUILayout.BeginScrollView(_rightScrollPosition);
            SkillConfigData data = CurrentSkillConfig.GetSkillConfigData(id);
            SkillConfigDataInspector.Instance.OnInspectorGUI(data, this, CurrentSkillConfig);

            GUILayout.EndScrollView();
        }

        internal bool IsEmpty()
        {
            if (CurrentSkillConfig == null)
            {
                return true;
            }

            for (int i = 0; i < CurrentSkillConfig.skills.Count; i++)
            {
                if (CurrentSkillConfig.skills[i] != null)
                {
                    return false;
                }
            }


            return true;
        }


        public void SetSelection(int id)
        {
            if (SkillListTree != null)
            {
                SkillListTree.SetSelection(new int[] { id });
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (SkillListTree != null)
                    {
                        SkillListTree.SetSelection(new int[] { id });
                    }
                };
            }
        }

        public void SetCurrentConfigs(SkillsConfig item)
        {
            SkillListTree?.Dispose();
            SkillListTree = null;
            _currentId = -1; // 重置当前选中的id
            CurrentSkillConfig = item;
            GUI.FocusControl(null);
            if (SkillListTree != null)
            {
                SkillListTree.Reload();
            }
            SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
        }

        private void OnDisable()
        {
            SkillListTree?.Dispose();
            SkillListTree = null;
            SkillConfigDataInspector.Instance.ReleaseSelection();
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
                //Debug.LogFormat("path:{0}", path);
                SkillsConfig skillsConfig = CreateInstance<SkillsConfig>();
                skillsConfig.startId = InitIdStart();

                SkillsConfigController.CreateAsset(skillsConfig, path);

                SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skillsConfig));

                if (SkillListTree != null)
                {
                    SkillListTree.Reload();
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

            foreach (var item in SkillsConfigController.GetAllConfigs())
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
