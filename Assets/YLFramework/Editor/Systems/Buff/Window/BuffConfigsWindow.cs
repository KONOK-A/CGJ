// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


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

        private const string SELECT_GUID_KEY = "SELECT_GUID_BUFF_KEY";


        private GUIContent currentBuffConfigContent;

        private string searchStr;

        [SerializeField]
        private TreeViewStateGroup treeViewStateGroup = new TreeViewStateGroup();


#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> buffListState => treeViewStateGroup.Get(currentBuffConfig.GetHashCode());
#else
        private TreeViewState buffListState => treeViewStateGroup.Get(currentBuffConfig.GetHashCode());
#endif


        internal BuffListTree buffListTree;

        internal BuffsConfig currentBuffConfig;

        private float timer = 0;

        internal int currentShowBuffId = 0;

        internal static int DefaultSelectBuffId = -1;

        private float lastRepaintCount = 0;

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
            titleContent = new GUIContent("Buff配置窗口", "Buff配置窗口");

        }

        private void Update()
        {
            lastRepaintCount++;
            if (lastRepaintCount > 15) {
                Repaint();
                lastRepaintCount = 0;
            }

        }


        protected override void OnGUI()
        {

            if (Time.realtimeSinceStartup - timer > 1 && currentBuffConfig == null)
            {
                currentBuffConfig = GetCurrentConfig();
                timer = Time.realtimeSinceStartup;
            }

            if (currentBuffConfig == null)
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

        internal BuffsConfig GetCurrentConfig()
        {
            if (!string.IsNullOrEmpty(SelectGUID))
            {
                string assets_path = AssetDatabase.GUIDToAssetPath(SelectGUID);
                BuffsConfig config = AssetDatabase.LoadAssetAtPath<BuffsConfig>(assets_path);

                if (config != null)
                    return config;
            }

            List<BuffsConfig> allConfigs = BuffsConfigController.GetAllConfigs();

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

        private void DrawLeft(Rect rect)
        {
            if (currentBuffConfigContent == null)
                currentBuffConfigContent = new GUIContent();

            if (currentBuffConfig == null)
                return;

            currentBuffConfigContent.text = currentBuffConfig.name;
            currentBuffConfigContent.tooltip = AssetDatabase.GetAssetPath(currentBuffConfig);

            GUILayout.BeginHorizontal();
            //

            EditorGUI.BeginChangeCheck();

            searchStr = EditorGUILayout.TextField(string.Empty,searchStr, "SearchTextField",GUILayout.MinWidth(0));

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(searchStr) && buffListTree.state.selectedIDs.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        buffListTree.FrameItem(buffListTree.state.selectedIDs[0]);
                    };
                }
            }

            if (EditorGUILayout.DropdownButton(currentBuffConfigContent, FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(this.position.width * 0.35f * 0.3f)))
            {
                GenericMenu menu = new GenericMenu();

                List<BuffsConfig> allConfigs = BuffsConfigController.GetAllConfigs();

                foreach (var item in allConfigs)
                {
                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                    bool isOn = SelectGUID == guid;

                    menu.AddItem(new GUIContent(item.name), isOn, () =>
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

                menu.AddItem(new GUIContent("新建Buff配表"), false, () =>
                {
                    CreateNew();
                });

                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();

            if (buffListTree == null)
            {
                buffListTree = new BuffListTree(buffListState, currentBuffConfig);
                buffListTree.Reload();
                //SyncDefaultId();
            }

            buffListTree.searchString = searchStr;

            rect.Set(rect.x, rect.y + 20, rect.width , rect.height - 20);
            buffListTree.OnGUI(rect);

            if (buffListTree != null && buffListTree.BuffsConfig != currentBuffConfig)
            {
                buffListTree.Dispose();
                buffListTree = null;
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

            if (buffListState.selectedIDs.Count == 1)
            {
                DrawData();
            }
            else if (buffListState.selectedIDs.Count == 0)
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
            int id = buffListState.selectedIDs[0];

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


            BuffConfigData data = currentBuffConfig.GetBuffData(id);
            BuffConfigDataInspector.Instance.OnInspectorGUI(data, this, currentBuffConfig);
        }

        internal bool IsEmpty()
        {
            if (currentBuffConfig == null)
                return true;

            for (int i = 0; i < currentBuffConfig.buffs.Count; i++)
            {
                if (currentBuffConfig.buffs[i] != null)
                    return false;
            }


            return true;
        }

        public void SetSelection(int id)
        {
            if (buffListTree != null)
            {
                buffListTree.SetSelection(new int[] { id });
            }
            else
            {
                EditorApplication.delayCall += () =>
                {
                    if (buffListTree == null) return;
                    buffListTree.SetSelection(new int[] { id });
                };
            }


        }

        public void SetCurrentConfigs(BuffsConfig item) {
            buffListTree?.Dispose();
            buffListTree = null;
            currentID = -1;
            currentBuffConfig = item;
            GUI.FocusControl(null);
            if (buffListTree != null)
            {
                buffListTree.Reload();
            }
            SelectGUID = AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath(item));
        }

        private void OnDisable()
        {
            buffListTree?.Dispose();
            buffListTree = null;
            BuffConfigDataInspector.Instance.ReleaseSelection();
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
                BuffsConfig buffConfig = CreateInstance<BuffsConfig>();
                buffConfig.start_id = InitIDStart();
                //CreateAsset(buffConfig, path);
                BuffsConfigController.CreateAsset(buffConfig, path);
                SelectGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(buffConfig));
                if (buffListTree != null)
                    buffListTree.Reload();
            }
            else
            {
                this.ShowNotification(new GUIContent("请把资源保存到Assets目录下!"));
            }
        }

        private int InitIDStart()
        {
            int max_id_start = 0;

            foreach (var item in BuffsConfigController.GetAllConfigs())
            {
                if (item.start_id > max_id_start)
                    max_id_start = item.start_id;
            }

            return max_id_start + 1000;
        }

        #endregion


    }

}
