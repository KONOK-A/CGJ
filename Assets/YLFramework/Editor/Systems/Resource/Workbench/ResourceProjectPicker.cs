using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceProjectPicker : PopupWindowContent
    {
        private readonly ResourceProject[] _projects;
        private readonly Action<ResourceProject> _select;
        private readonly Action _create;
        private readonly Func<bool> _canCreate;
        private string _search = "";
        private Vector2 _scroll;
        private bool _focusSearch = true;

        internal ResourceProjectPicker(ResourceProject[] values, Action<ResourceProject> selected, Action createProject, Func<bool> canCreateProject)
        {
            _projects = values;
            _select = selected;
            _create = createProject;
            _canCreate = canCreateProject;
        }

        public override Vector2 GetWindowSize() => new(440, 360);

        public override void OnGUI(Rect rect)
        {
            GUILayout.Space(6);
            GUI.SetNextControlName("Resource.ProjectSearch");
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (_focusSearch)
            {
                EditorGUI.FocusTextInControl("Resource.ProjectSearch");
                _focusSearch = false;
            }
            GUILayout.Label("按名称、项目标识或配置路径查找", ResourceWorkbenchGui.Secondary);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var count = 0;
            foreach (var item in _projects)
            {
                if (!item)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(item);
                var name = string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;
                if ((name + "\n" + item.projectId + "\n" + path).IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                count++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (GUILayout.Button(new GUIContent(name + "  /  " + item.projectId, path), EditorStyles.linkLabel))
                {
                    _select(item);
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.LabelField(new GUIContent(path, "配置资产 GUID：" + AssetDatabase.AssetPathToGUID(path)), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            if (count == 0)
            {
                GUILayout.Label("没有匹配的资源项目。", ResourceWorkbenchGui.Secondary);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!_canCreate()))
            {
                if (GUILayout.Button("新建资源项目...", GUILayout.Height(25)))
                {
                    editorWindow.Close();
                    _create();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("清除当前项目", EditorStyles.miniButton, GUILayout.Height(25), GUILayout.Width(104)))
            {
                _select(null);
                editorWindow.Close();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
