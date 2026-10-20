// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Excel;

namespace YLFramework.BuffSystem
{

    public class EffectConfigsWindow : SplitWindow
    {

        [MenuItem("Window/YLFramework/BuffSystem/Effect", false, 12)]
        static void Open()
        {
            BuffConfigsWindow window = GetWindow<BuffConfigsWindow>();
            EffectConfigsWindow window2 = GetWindow<EffectConfigsWindow>(typeof(BuffConfigsWindow));
        }

        #region 字段

        //private Rect left;

        private string searchStr;

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> effectListState;
#else
        private TreeViewState effectListState;
#endif

        internal EffectListTree effectListTree;

        internal EffectData currentEffectData;

        internal int currentShowBuffId = 0;

        internal static int DefaultSelectBuffId = -1;

        private MultiColumnHeaderState effectListMCHState;

        private GUIContent dropdown_excel_content;

        private float lastRepaintCount = 0;

        private int currentID;
        public int idChangeCount;

#endregion

        #region 生命周期

        private void Awake()
        {
            titleContent = new GUIContent("Effect配置窗口", "Effect配置窗口");
            horizontal_split_percent = 0.7f;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += RefreshList;
            currentEffectData = GetCurrentConfig();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshList;
            effectListTree?.Dispose();
            effectListTree = null;
        }

        private void Update()
        {
            lastRepaintCount++;
            if (lastRepaintCount > 15) {
                Repaint();
                lastRepaintCount = 0;
            }
        }

        #endregion


        #region 方法

        internal EffectData GetCurrentConfig()
        {
            if (BuffSystemSettings.Instance.AllEffectDatas == null || BuffSystemSettings.Instance.AllEffectDatas.Count == 0)
                return null;

            return BuffSystemSettings.Instance.AllEffectDatas[0];
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

            GUILayout.BeginHorizontal();
            //

            EditorGUI.BeginChangeCheck();

            searchStr = EditorGUILayout.TextField( string.Empty,searchStr, "SearchTextField",GUILayout.MinWidth(0));

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(searchStr) && effectListTree.state.selectedIDs.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        effectListTree.FrameItem(effectListTree.state.selectedIDs[0]);
                    };
                }
            }


            GUILayout.EndHorizontal();


            if (effectListState == null) {
#if UNITY_6000_2_OR_NEWER
                effectListState = new TreeViewState<int>();
#else
                effectListState = new TreeViewState();
#endif
            }

            if (effectListTree == null)
            {
                var headerState = EffectListTree.CreateDefaultMultiColumnHeaderState();// multiColumnTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(effectListMCHState, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(effectListMCHState, headerState);
                effectListMCHState = headerState;

                effectListTree = new EffectListTree(effectListState, effectListMCHState);
                RefreshList();
            }

            effectListTree.searchString = searchStr;

            rect.Set(rect.x, rect.y + 20, rect.width, rect.height - 20);
            effectListTree.OnGUI(rect);

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

            if (effectListState.selectedIDs.Count == 1)
            {
                DrawData();
            }
            else if (effectListState.selectedIDs.Count == 0)
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
            int id = effectListState.selectedIDs[0];

            if (currentID != id)
            {
                idChangeCount++;

                // 如果当前帧是Layout ，不能设置，因为当前帧会被return
                // 如果当前帧被return , 下一帧repaint 会报错，因为没有位置数据
                if (Event.current.type != EventType.Layout)
                {
                    if (idChangeCount > 2)
                    {
                        currentID = id;
                        idChangeCount = 0;
                    }
                }

                // 这样处理的目的是 为了防止 EditorGUILayout.DelayedIntField 在编辑时 ,
                // 直接切换id,  EditorGUILayout.DelayedIntField  返回的值会直接给新选中的数据， 导致数据错误的情况
                return;
            }


            EffectData effectData = effectListTree.GetEffectData(id);
            EffectDataInspector.Instance.OnInspectorGUI(this, effectData);
        }


        internal bool IsEmpty()
        {
            if (currentEffectData == null)
                return true;

            return false;
        }

        public void RefreshList()
        {
            if (effectListTree == null)
                return;

            effectListTree.Reload();
            if (currentEffectData == null && BuffSystemSettings.Instance.AllEffectDatas.Count > 0)
                currentEffectData = BuffSystemSettings.Instance.AllEffectDatas[0];
            if (currentEffectData != null)
            {
                effectListTree.SetSelection(new int[] { currentEffectData.id });
                effectListTree.SetExpanded(currentEffectData.id, true);
                effectListTree.FrameItem(currentEffectData.id);
            }
        }

#endregion




    }

}
