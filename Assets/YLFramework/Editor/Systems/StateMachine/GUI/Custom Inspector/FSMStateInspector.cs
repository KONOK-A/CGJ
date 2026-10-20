using YLFramework.Core.Editor;
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YLFramework.StateMachine
{
    [CustomEditor(typeof(FSMStateInspectorHelper))]
    public class FSMStateInspector : UnityEditor.Editor
    {
        //private string stateName;

        private ReorderableList reorderableList;


        internal static Rect popupRect;

        private GUIContent btn_add_state_script = new GUIContent("Add State Script");

        private GUIStyle ProjectBrowserHeaderBgMiddle = null;

        private GUIStyle DD_HeaderStyle = null;

        private GUIStyle PrefixLabel = null;

        private GUIContent script_gui_content = new GUIContent();

        private Vector2 scroll;

        private void OnEnable()
        {
            FSMStateInspectorHelper helper = target as FSMStateInspectorHelper;
            if (helper == null) { return; }

            //reorderableList = new ReorderableList(helper.node.StateScripts, typeof(FSMStateScriptInfo), true, true, true, true);
            //reorderableList.drawHeaderCallback += OnDrawHeaderCallback;
            //reorderableList.onAddCallback += this.OnAddCallback;
            //reorderableList.onRemoveCallback += this.OnRemoveCallback;
            //reorderableList.drawElementCallback += this.DrawItem;
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();

            if (ProjectBrowserHeaderBgMiddle == null)
                ProjectBrowserHeaderBgMiddle = new GUIStyle(StateStyles.Named("AC BoldHeader", EditorStyles.boldLabel));

            if (DD_HeaderStyle == null)
            {
                DD_HeaderStyle = new GUIStyle(StateStyles.Named("IconButton", EditorStyles.miniButton));
            }

            if (PrefixLabel == null)
            {
                PrefixLabel = new GUIStyle(StateStyles.Named("PrefixLabel", EditorStyles.label));
                PrefixLabel.richText = true;
            }

            FSMStateInspectorHelper helper = target as FSMStateInspectorHelper;
            if (helper == null || !helper.controller || helper.node == null || !helper.controller.states.Contains(helper.node)) return;

            bool disabled = EditorApplication.isPlaying || helper.node.IsAnyState || helper.node.IsEntryState || helper.node.IsUpState;

            EditorGUI.BeginDisabledGroup(disabled);

            //GUILayout.Space(-10);
            //scroll = GUILayout.BeginScrollView(scroll);
            Vector2 mousePosition = Event.current.mousePosition;
            helper.node.RefreshStateScripts(helper.controller);
            foreach (var item in helper.node.StateScripts)
            {

                // 根据guid加载到脚本信息
                string path = AssetDatabase.GUIDToAssetPath(item.guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) { EditorGUILayout.HelpBox($"State '{helper.node.name}'.StateScripts: missing MonoScript GUID '{item.guid}', type '{item.className}'.", MessageType.Error); continue; }
                Type type = script.GetClass();
                if (type == null) continue;

                var r = EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
                r.x = 0;
                r.width += 30;
                GUI.Box(r, string.Empty, ProjectBrowserHeaderBgMiddle);
                GUILayout.Space(-10);
                GUILayout.Label(EditorGUIUtility.IconContent("d_cs Script Icon"), GUILayout.Width(20), GUILayout.Height(20));

                string displayName = string.Empty;

                if (type.IsSubclassOf(typeof(FSMState)))
                {
                    displayName = type.Name;
                    script_gui_content.tooltip = string.Empty;
                }
                else
                {
                    displayName = string.Format("{0}<color=yellow>(Script Missing)</color>", type.Name);
                    script_gui_content.tooltip = "脚本丢失,请检查该脚本是否继承自FSMState!";
                }

                script_gui_content.text = displayName;


                GUILayout.Label(script_gui_content, PrefixLabel, GUILayout.Height(20));

                GUILayout.FlexibleSpace();

                GUILayout.BeginVertical();

                GUILayout.Space(5);

                if ( GUILayout.Button(EditorGUIUtility.IconContent("d__Menu"), DD_HeaderStyle, GUILayout.Width(25), GUILayout.Height(25)))
                {
                    ShowMenu(script);
                }

                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

                if ( Event.current.type == EventType.MouseUp && Event.current.button == 1 && r.Contains(mousePosition))
                {
                    ShowMenu(script);
                    Event.current.Use();
                }

                if ( Event.current.type == EventType.MouseUp && Event.current.button == 0 && r.Contains(mousePosition)) {

                    EditorGUIUtility.PingObject(script);
                    Event.current.Use();
                }
            }


            GUILayout.Space(30);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Rect rect = GUILayoutUtility.GetRect(btn_add_state_script,GUI.skin.button,GUILayout.Width(260), GUILayout.Height(25));

            if (GUI.Button(rect, btn_add_state_script ))
            {
                popupRect = new Rect(rect);
                popupRect.height = 300;

                PopupWindow.Show(rect, new FSMSelectStateWindow(popupRect, helper.controller, helper.node));
            }

            GUILayout.FlexibleSpace();



            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            //if (Event.current.type == EventType.Layout)
            //{

            //}

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if(helper.controller != null)
                helper.controller.Save();

            //GUILayout.EndScrollView();

            EditorGUI.EndDisabledGroup();
        }

        protected override void OnHeaderGUI()
        {
            //base.OnHeaderGUI();
            FSMStateInspectorHelper helper = target as FSMStateInspectorHelper;
            if (helper == null || !helper.controller || helper.node == null || !helper.controller.states.Contains(helper.node)) return;


            string name = null;
            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(EditorGUIUtility.IconContent("icons/processed/unityeditor/animations/animatorstate icon.asset"), GUILayout.Width(30), GUILayout.Height(30));

                EditorGUILayout.LabelField("Name:", GUILayout.Width(60));

                bool disabled = EditorApplication.isPlaying
                    || helper.node.IsAnyState || helper.node.IsEntryState || helper.node.IsUpState;

                EditorGUI.BeginDisabledGroup(disabled);
                name = EditorGUILayout.DelayedTextField(helper.node.DisplayName);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
            {
                string oldName = helper.node.name;
                bool success = FSMStateNodeFactory.Rename(helper.controller, helper.node, name);
                if (success)
                    helper.grap.RenameState(oldName, name);
            }
            EditorGUILayout.Space();
            var rect = EditorGUILayout.BeginHorizontal();

            Handles.color = Color.black;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector3(rect.x + rect.width, rect.y));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }



        private void ShowMenu(MonoScript script)
        {
            FSMStateInspectorHelper helper = target as FSMStateInspectorHelper;
            if (helper == null) return;

            var genricMenu = new GenericMenu();

            genricMenu.AddItem(new GUIContent("Remove Script"), false, () =>
            {
                Undo.RecordObject(helper.controller, "Remove FSM State Script");
                helper.node.RemoveStateScript(script);
                helper.controller.Save();
            });

            genricMenu.AddItem(new GUIContent("Edit Script"), false, () =>
            {
                AssetDatabase.OpenAsset(script);
            });

            genricMenu.ShowAsContext();
        }



        //private void DrawItem(Rect rect, int index, bool isActive, bool isFocused)
        //{


        //    if (index < 0 || index >= reorderableList.list.Count) return;
        //    FSMStateScriptInfo info = reorderableList.list[index] as FSMStateScriptInfo;
        //    if (info == null) return;
        //    info.className = GUI.TextField(rect, info.className);
        //    if (string.IsNullOrEmpty(info.className))
        //    {
        //        EditorGUI.BeginDisabledGroup(true);
        //        GUI.Label(rect, "请输入类的全名(含命名空间)!");
        //        EditorGUI.EndDisabledGroup();
        //    }
        //}

        //private void OnDrawHeaderCallback(Rect rect)
        //{
        //    GUI.Label(rect, "State Scripts");
        //}

    }


}
