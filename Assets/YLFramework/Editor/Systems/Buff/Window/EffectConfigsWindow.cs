using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Excel;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;

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

        private string _searchStr;

#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> _effectListState;
#else
        private TreeViewState _effectListState;
#endif

        internal EffectListTree EffectListTree;

        internal EffectData CurrentEffectData;

        internal int CurrentShowBuffId = 0;

        internal static int DefaultSelectBuffId = -1;

        private MultiColumnHeaderState _effectListMCHState;

        private GUIContent _dropdownExcelContent;

        private float _lastRepaintCount = 0;

        private int _currentId;
        public int idChangeCount;

        #endregion

        #region 生命周期

        private void Awake()
        {
            titleContent = new GUIContent("Effect配置窗口", "Effect配置窗口");
            horizontalSplitPercent = 0.7f;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += RefreshList;
            CurrentEffectData = GetCurrentConfig();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshList;
            EffectListTree?.Dispose();
            EffectListTree = null;
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

        #endregion


        #region 方法

        internal EffectData GetCurrentConfig()
        {
            if (BuffSystemSettings.Instance.AllEffectDatas == null || BuffSystemSettings.Instance.AllEffectDatas.Count == 0)
            {
                return null;
            }

            return BuffSystemSettings.Instance.AllEffectDatas[0];
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

            GUILayout.BeginHorizontal();
            //

            EditorGUI.BeginChangeCheck();

            _searchStr = EditorGUILayout.TextField(string.Empty, _searchStr, "SearchTextField", GUILayout.MinWidth(0));

            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(_searchStr) && EffectListTree.state.selectedIDs.Count > 0)
                {
                    EditorApplication.delayCall += () =>
                    {
                        EffectListTree.FrameItem(EffectListTree.state.selectedIDs[0]);
                    };
                }
            }


            GUILayout.EndHorizontal();


            if (_effectListState == null)
            {
#if UNITY_6000_2_OR_NEWER
                _effectListState = new TreeViewState<int>();
#else
                _effectListState = new TreeViewState();
#endif
            }

            if (EffectListTree == null)
            {
                var headerState = EffectListTree.CreateDefaultMultiColumnHeaderState();// multiColumnTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(_effectListMCHState, headerState))
                {
                    MultiColumnHeaderState.OverwriteSerializedFields(_effectListMCHState, headerState);
                }

                _effectListMCHState = headerState;

                EffectListTree = new EffectListTree(_effectListState, _effectListMCHState);
                RefreshList();
            }

            EffectListTree.searchString = _searchStr;

            rect.Set(rect.x, rect.y + 20, rect.width, rect.height - 20);
            EffectListTree.OnGUI(rect);

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

            if (_effectListState.selectedIDs.Count == 1)
            {
                DrawData();
            }
            else if (_effectListState.selectedIDs.Count == 0)
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
            int id = _effectListState.selectedIDs[0];

            if (_currentId != id)
            {
                idChangeCount++;

                // 如果当前帧是Layout ，不能设置，因为当前帧会被return
                // 如果当前帧被return , 下一帧repaint 会报错，因为没有位置数据
                if (Event.current.type != EventType.Layout)
                {
                    if (idChangeCount > 2)
                    {
                        _currentId = id;
                        idChangeCount = 0;
                    }
                }

                // 这样处理的目的是 为了防止 EditorGUILayout.DelayedIntField 在编辑时 ,
                // 直接切换id,  EditorGUILayout.DelayedIntField  返回的值会直接给新选中的数据， 导致数据错误的情况
                return;
            }


            EffectData effectData = EffectListTree.GetEffectData(id);
            EffectDataInspector.Instance.OnInspectorGUI(this, effectData);
        }


        internal bool IsEmpty()
        {
            if (CurrentEffectData == null)
            {
                return true;
            }

            return false;
        }

        public void RefreshList()
        {
            if (EffectListTree == null)
            {
                return;
            }

            EffectListTree.Reload();
            if (CurrentEffectData == null && BuffSystemSettings.Instance.AllEffectDatas.Count > 0)
            {
                CurrentEffectData = BuffSystemSettings.Instance.AllEffectDatas[0];
            }

            if (CurrentEffectData != null)
            {
                EffectListTree.SetSelection(new int[] { CurrentEffectData.id });
                EffectListTree.SetExpanded(CurrentEffectData.id, true);
                EffectListTree.FrameItem(CurrentEffectData.id);
            }
        }

        #endregion




    }

}
