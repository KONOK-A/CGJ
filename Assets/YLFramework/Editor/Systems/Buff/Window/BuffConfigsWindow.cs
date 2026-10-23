using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;


namespace YLFramework.BuffSystem
{
    public class BuffConfigsWindow : SplitWindow
    {

        [MenuItem("Window/YLFramework/BuffSystem/Buff", false, 11)]
        static void Open()
        {
            BuffConfigsWindow window = GetWindow<BuffConfigsWindow>();
            EffectConfigsWindow window2 = GetWindow<EffectConfigsWindow>(typeof(BuffConfigsWindow));
            window.Focus();
        }

        #region 字段

        private const string SelectGuidKey = "YLFramework.Buff.SelectedConfig";


        private GUIContent _currentBuffConfigContent;

        private string _searchStr;

        [SerializeField]
        private TreeViewStateGroup treeViewStateGroup = new TreeViewStateGroup();


#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> BuffListState => treeViewStateGroup.Get(CurrentBuffConfig.GetHashCode());
#else
        private TreeViewState BuffListState => treeViewStateGroup.Get(CurrentBuffConfig.GetHashCode());
#endif


        internal BuffListTree BuffListTree;

        internal BuffsConfig CurrentBuffConfig;

        private float _timer = 0;

        internal int CurrentShowBuffId = 0;

        internal static int DefaultSelectBuffId = -1;

        private float _lastRepaintCount = 0;

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
            titleContent = new GUIContent("Buff配置窗口", "Buff配置窗口");

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

            if (Time.realtimeSinceStartup - _timer > 1 && CurrentBuffConfig == null)
            {
                CurrentBuffConfig = GetCurrentConfig();
                _timer = Time.realtimeSinceStartup;
            }

            if (CurrentBuffConfig == null)
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

        internal BuffsConfig GetCurrentConfig()
        {
            if (!string.IsNullOrEmpty(SelectGuid))
            {
                string assetsPath = AssetDatabase.GUIDToAssetPath(SelectGuid);
                BuffsConfig config = AssetDatabase.LoadAssetAtPath<BuffsConfig>(assetsPath);

                if (config != null)
                {
                    return config;
                }
            }

            List<BuffsConfig> allConfigs = BuffsConfigController.GetAllConfigs();

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
            GUILayout.Label("未查询到Buff配置文件!");
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

        private void DrawLeft(Rect rect)
        {
            if (_currentBuffConfigContent == null)
            {
                _currentBuffConfigContent = new GUIContent();
            }

            if (CurrentBuffConfig == null)
            {
                return;
            }

            _currentBuffConfigContent.text = CurrentBuffConfig.name;
            _currentBuffConfigContent.tooltip = AssetDatabase.GetAssetPath(CurrentBuffConfig);

            GUILayout.BeginHorizontal();
            //

            EditorGUI.BeginChangeCheck();

            _searchStr = EditorGUILayout.TextField(string.Empty, _searchStr, "SearchTextField", GUILayout.MinWidth(0));

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(_searchStr) && BuffListTree.state.selectedIDs.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        BuffListTree.FrameItem(BuffListTree.state.selectedIDs[0]);
                    };
                }
            }

            if (EditorGUILayout.DropdownButton(_currentBuffConfigContent, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(this.position.width * 0.35f * 0.3f)))
            {
                GenericMenu menu = new GenericMenu();

                List<BuffsConfig> allConfigs = BuffsConfigController.GetAllConfigs();

                foreach (var item in allConfigs)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                    bool isOn = SelectGuid == guid;

                    menu.AddItem(new GUIContent(item.name), isOn, () =>
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

                menu.AddItem(new GUIContent("新建Buff配表"), false, () =>
                {
                    CreateNew();
                });

                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();

            if (BuffListTree == null)
            {
                BuffListTree = new BuffListTree(BuffListState, CurrentBuffConfig);
                BuffListTree.Reload();
                //SyncDefaultId();
            }

            BuffListTree.searchString = _searchStr;

            rect.Set(rect.x, rect.y + 20, rect.width, rect.height - 20);
            BuffListTree.OnGUI(rect);

            if (BuffListTree != null && BuffListTree.BuffsConfig != CurrentBuffConfig)
            {
                BuffListTree.Dispose();
                BuffListTree = null;
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

            if (BuffListState.selectedIDs.Count == 1)
            {
                DrawData();
            }
            else if (BuffListState.selectedIDs.Count == 0)
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
            int id = BuffListState.selectedIDs[0];

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


            BuffConfigData data = CurrentBuffConfig.GetBuffData(id);
            BuffConfigDataInspector.Instance.OnInspectorGUI(data, this, CurrentBuffConfig);
        }

        internal bool IsEmpty()
        {
            if (CurrentBuffConfig == null)
            {
                return true;
            }

            for (int i = 0; i < CurrentBuffConfig.buffs.Count; i++)
            {
                if (CurrentBuffConfig.buffs[i] != null)
                {
                    return false;
                }
            }


            return true;
        }

        public void SetSelection(int id)
        {
            if (BuffListTree != null)
            {
                BuffListTree.SetSelection(new int[] { id });
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (BuffListTree == null)
                    {
                        return;
                    }

                    BuffListTree.SetSelection(new int[] { id });
                };
            }


        }

        public void SetCurrentConfigs(BuffsConfig item)
        {
            BuffListTree?.Dispose();
            BuffListTree = null;
            _currentId = -1;
            CurrentBuffConfig = item;
            GUI.FocusControl(null);
            if (BuffListTree != null)
            {
                BuffListTree.Reload();
            }
            SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
        }

        private void OnDisable()
        {
            BuffListTree?.Dispose();
            BuffListTree = null;
            BuffConfigDataInspector.Instance.ReleaseSelection();
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
                BuffsConfig buffConfig = CreateInstance<BuffsConfig>();
                buffConfig.startId = InitIdStart();
                //CreateAsset(buffConfig, path);
                BuffsConfigController.CreateAsset(buffConfig, path);
                SelectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(buffConfig));
                if (BuffListTree != null)
                {
                    BuffListTree.Reload();
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

            foreach (var item in BuffsConfigController.GetAllConfigs())
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
