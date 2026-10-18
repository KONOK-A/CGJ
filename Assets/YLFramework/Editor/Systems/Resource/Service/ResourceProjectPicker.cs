using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceProjectPicker : PopupWindowContent
    {
        private readonly ResourceProject[] projects;
        private readonly Action<ResourceProject> select;
        private readonly Action create;
        private readonly Func<bool> canCreate;
        private string search = "";
        private Vector2 scroll;
        private bool focus = true;

        internal ResourceProjectPicker(ResourceProject[] values, Action<ResourceProject> selected, Action createProject, Func<bool> canCreateProject)
        { projects = values; select = selected; create = createProject; canCreate = canCreateProject; }

        public override Vector2 GetWindowSize() => new(440, 360);

        public override void OnGUI(Rect rect)
        {
            GUILayout.Space(6);
            GUI.SetNextControlName("Resource.ProjectSearch");
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            if (focus) { EditorGUI.FocusTextInControl("Resource.ProjectSearch"); focus = false; }
            GUILayout.Label("按名称、项目标识或配置路径查找", ResourceWorkbenchGUI.Secondary);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var count = 0;
            foreach (var item in projects)
            {
                if (!item) continue;
                var path = AssetDatabase.GetAssetPath(item);
                var name = string.IsNullOrWhiteSpace(item.DisplayName) ? item.name : item.DisplayName;
                if ((name + "\n" + item.ProjectId + "\n" + path).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                count++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (GUILayout.Button(new GUIContent(name + "  /  " + item.ProjectId, path), EditorStyles.linkLabel))
                { select(item); editorWindow.Close(); GUIUtility.ExitGUI(); }
                EditorGUILayout.LabelField(new GUIContent(path, "配置资产 GUID：" + AssetDatabase.AssetPathToGUID(path)), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            if (count == 0) GUILayout.Label("没有匹配的资源项目。", ResourceWorkbenchGUI.Secondary);
            EditorGUILayout.EndScrollView();
            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!canCreate()))
                if (GUILayout.Button("新建资源项目...", GUILayout.Height(25)))
                { editorWindow.Close(); create(); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("清除当前项目", EditorStyles.miniButton, GUILayout.Height(25), GUILayout.Width(104)))
            { select(null); editorWindow.Close(); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndHorizontal();
        }
    }
}
