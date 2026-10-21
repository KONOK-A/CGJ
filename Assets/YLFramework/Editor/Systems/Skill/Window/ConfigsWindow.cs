using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace YLFramework.SkillSystem
{
    public class ConfigsWindow : SplitWindow
    {

        [MenuItem("Window/YLFramework/SkillSystem", false, 10)]
        static void Open()
        {
            GetWindow<ConfigsWindow>().Show();
        }

        #region 字段

        private const string SELECT_GUID_KEY = "YLFramework.Skill.SelectedConfig";

        //private Rect left;

        private GUIContent currentSkillConfigContent;

        private string searchStr;

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> skillListState => treeViewStateGroup.Get(currentSkillConfig.GetHashCode());

#else
        private TreeViewState skillListState => treeViewStateGroup.Get(currentSkillConfig.GetHashCode());
#endif


        [SerializeField]
        private TreeViewStateGroup treeViewStateGroup = new TreeViewStateGroup();

        internal SkillListTree skillListTree;

        internal SkillsConfig currentSkillConfig;

        private float timer = 0;

        internal int currentShowSkillId = 0;

        internal static int DefaultSelectSkillId = -1;

        private Vector2 rightScrollPosition;

        private float lastRepaintCount;


        private int currentID;
        public int idChangeCount;

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
            titleContent = new GUIContent("技能配置窗口", "技能配置窗口");
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
            if (Time.realtimeSinceStartup - timer > 1 && currentSkillConfig == null)
            {
                currentSkillConfig = GetCurrentConfig();
                timer = Time.realtimeSinceStartup;
            }

            if (currentSkillConfig == null)
            {
                EmptyGUI();
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
            if (!string.IsNullOrEmpty(SelectGUID))
            {
                string assets_path = AssetDatabase.GUIDToAssetPath(SelectGUID);
                SkillsConfig config = AssetDatabase.LoadAssetAtPath<SkillsConfig>(assets_path);

                if (config != null)
                    return config;
            }

            List<SkillsConfig> configs = SkillsConfigController.GetAllConfigs();

            if (configs == null || configs.Count == 0)
                return null;

            SelectGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(configs[0]));
            return configs[0];
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


        private void EmptyGUI()
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
            if (currentSkillConfigContent == null)
                currentSkillConfigContent = new GUIContent();

            if (currentSkillConfig == null)
                return;

            currentSkillConfigContent.text = currentSkillConfig.name;
            currentSkillConfigContent.tooltip = AssetDatabase.GetAssetPath(currentSkillConfig);

            GUILayout.BeginHorizontal();
            //

            EditorGUI.BeginChangeCheck();

            searchStr = EditorGUILayout.TextField(string.Empty, searchStr, "SearchTextField", GUILayout.MinWidth(0));

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(searchStr) && skillListTree.state.selectedIDs.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        skillListTree.FrameItem(skillListTree.state.selectedIDs[0]);
                    };
                }
            }

            if (EditorGUILayout.DropdownButton(currentSkillConfigContent, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(this.position.width * 0.35f * 0.3f)))
            {
                GenericMenu menu = new GenericMenu();
                List<SkillsConfig> configs = SkillsConfigController.GetAllConfigs();
                foreach (var item in configs)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                    bool isOn = SelectGUID == guid;

                    menu.AddItem(new GUIContent(item.name), isOn, () =>
                    {
                        SelectGUID = guid;

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




            if (skillListTree == null)
            {
                skillListTree = new SkillListTree(skillListState, currentSkillConfig);
                skillListTree.Reload();
            }

            skillListTree.searchString = searchStr;

            rect.Set(rect.x, rect.y + 20, rect.width, rect.height - 20);


            skillListTree.OnGUI(rect);


            if (skillListTree != null && skillListTree.SkillsConfig != currentSkillConfig)
            {
                skillListTree.Dispose();
                skillListTree = null;
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

            if (skillListState.selectedIDs.Count == 1)
            {
                DrawData();
            }
            else if (skillListState.selectedIDs.Count == 0)
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
            int id = skillListState.selectedIDs[0];

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


            rightScrollPosition = GUILayout.BeginScrollView(rightScrollPosition);
            SkillConfigData data = currentSkillConfig.GetSkillConfigData(id);
            SkillConfigDataInspector.Instance.OnInspectorGUI(data, this, currentSkillConfig);

            GUILayout.EndScrollView();
        }

        internal bool IsEmpty()
        {
            if (currentSkillConfig == null)
                return true;


            for (int i = 0; i < currentSkillConfig.skills.Count; i++)
            {
                if (currentSkillConfig.skills[i] != null)
                    return false;
            }


            return true;
        }


        public void SetSelection(int id)
        {
            if (skillListTree != null)
                skillListTree.SetSelection(new int[] { id });
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (skillListTree != null)
                        skillListTree.SetSelection(new int[] { id });
                };
            }
        }

        public void SetCurrentConfigs(SkillsConfig item)
        {
            skillListTree?.Dispose();
            skillListTree = null;
            currentID = -1; // 重置当前选中的id
            currentSkillConfig = item;
            GUI.FocusControl(null);
            if (skillListTree != null)
            {
                skillListTree.Reload();
            }
            SelectGUID = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath(item));
        }

        private void OnDisable()
        {
            skillListTree?.Dispose();
            skillListTree = null;
            SkillConfigDataInspector.Instance.ReleaseSelection();
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
                //Debug.LogFormat("path:{0}", path);
                SkillsConfig skillsConfig = CreateInstance<SkillsConfig>();
                skillsConfig.start_id = InitIDStart();

                SkillsConfigController.CreateAsset(skillsConfig, path);

                SelectGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skillsConfig));

                if (skillListTree != null)
                    skillListTree.Reload();
            }
            else
            {
                this.ShowNotification(new GUIContent("请把资源保存到Assets目录下!"));
            }
        }

        private int InitIDStart()
        {
            int max_id_start = 0;

            foreach (var item in SkillsConfigController.GetAllConfigs())
            {
                if (item.start_id > max_id_start)
                    max_id_start = item.start_id;
            }

            return max_id_start + 1000;
        }

        #endregion


    }

}
